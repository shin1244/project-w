using Godot;
using System;
using System.Collections.Generic;

// F6: 세 유닛의 사망 연출을 반복하고, 두 종류의 대상 표시를 나란히 보여줍니다.
public partial class CombatFeedback : Node3D
{
    private readonly string[] _scenes = { "Worker", "Knight", "Archer" };
    private readonly List<Unit> _subjects = new();
    private readonly List<CommandTargetIndicator> _indicators = new();
    private double _clock;
    private bool _falling;

    public override async void _Ready()
    {
        var tree = GD.Load<PackedScene>("res://resources/trees/Oak.tscn").Instantiate<ResourceNode>();
        AddChild(tree);
        tree.Position = new Vector3(3, 0, 2);
        tree.Initialize(700);
        tree.SetAmount(400);
        var target = GD.Load<PackedScene>("res://units/Knight.tscn").Instantiate<Unit>();
        AddChild(target);
        target.Position = new Vector3(-3, 0, 2);
        foreach (Node3D node in new Node3D[] { tree, target })
        {
            var marker = new CommandTargetIndicator();
            marker.Show(node);
            _indicators.Add(marker);
        }
        AddLabel("GATHER TARGET", new Vector3(3, .15f, .1f));
        AddLabel("ATTACK TARGET", new Vector3(-3, .15f, .1f));
        for (int i = 0; i < 3; i++) AddLabel(_scenes[i].ToUpperInvariant(), new Vector3(3 - i * 3, .15f, -4.6f));
        SpawnSubjects();
        GetNode<Camera3D>("Camera3D").LookAt(new Vector3(0, .6f, -.3f));
        if (!Array.Exists(OS.GetCmdlineUserArgs(), arg => arg == "--capture")) return;
        SetProcess(false);
        foreach (Unit unit in _subjects)
        {
            Tween tween = unit.BeginDeath();
            tween.Pause();
            tween.CustomStep(UnitDeathEffect.FallSeconds + UnitDeathEffect.HoldSeconds / 2);
        }
        for (int i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        Error result = GetViewport().GetTexture().GetImage().SavePng("res://docs/images/combat-feedback.png");
        GetTree().Quit(result == Error.Ok ? 0 : 1);
    }

    public override void _Process(double delta)
    {
        _clock += delta;
        if (!_falling && _clock > 1)
        {
            foreach (Unit unit in _subjects) unit.BeginDeath();
            _falling = true;
        }
        if (_clock > 3.2) SpawnSubjects();
    }

    private void SpawnSubjects()
    {
        foreach (Unit unit in _subjects)
            if (GodotObject.IsInstanceValid(unit)) unit.QueueFree();
        _subjects.Clear();
        for (int i = 0; i < 3; i++)
        {
            Unit unit = GD.Load<PackedScene>($"res://units/{_scenes[i]}.tscn").Instantiate<Unit>();
            AddChild(unit);
            unit.Position = new Vector3(3 - i * 3, 0, -3);
            _subjects.Add(unit);
        }
        _clock = 0;
        _falling = false;
    }

    private void AddLabel(string text, Vector3 position) => AddChild(new Label3D
    {
        Text = text, Position = position, FontSize = 38, PixelSize = .005f,
        Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, OutlineSize = 5
    });

    public override void _ExitTree()
    {
        foreach (CommandTargetIndicator indicator in _indicators) indicator.Clear();
    }
}
