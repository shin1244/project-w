using Godot;
using System;
using System.Collections.Generic;

// F6: both real archer scenes repeatedly draw and release. No server connection.
public partial class ArcherAttackPreview : Node3D
{
    private readonly List<Unit> _archers = new();
    private uint _sequence;
    private double _clock;

    public override async void _Ready()
    {
        AddChild(new WorldEnvironment { Environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color("14232c"),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color("bfced8"), AmbientLightEnergy = .5f,
            TonemapMode = Godot.Environment.ToneMapper.Filmic
        }});
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-50, -32, 0), LightEnergy = 1.3f,
            LightColor = new Color("ffebc8"), ShadowEnabled = true, DirectionalShadowMaxDistance = 30 });
        AddChild(new MeshInstance3D { Position = new Vector3(0, -.03f, 0), Mesh = new PlaneMesh
        {
            Size = new Vector2(200, 200), Material = new StandardMaterial3D { AlbedoColor = new Color("36464a"), Roughness = .9f }
        }});
        for (int i = 0; i < 2; i++)
        {
            float x = i == 0 ? -2.4f : 2.4f;
            var target = GD.Load<PackedScene>("res://units/Knight.tscn").Instantiate<Unit>();
            target.Initialize((uint)(30 + i), 9, 2);
            target.SetLocalTeam(1);
            AddChild(target);
            target.ApplyServerPosition(x, -4.8f);
            target.GetNode<Node3D>("Visual").RotationDegrees = new Vector3(0, 180, 0);
            var archer = GD.Load<PackedScene>($"res://units/{(i == 0 ? "Archer" : "MinionArcher")}.tscn").Instantiate<Unit>();
            archer.Initialize((uint)(10 + i), i == 0 ? 7u : 0u, 1);
            archer.SetLocalTeam(1);
            archer.ResolveFocus = id => id == target.UnitId ? target : null;
            AddChild(archer);
            archer.ApplyServerPosition(x, 1.6f);
            archer.ApplyServerState(new UnitState(UnitActivity.Attack, 0, target.UnitId, 0));
            _archers.Add(archer);
            AddChild(new Label3D { Text = i == 0 ? "궁수" : "원거리 미니언", Position = new Vector3(x, .1f, 2.7f),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 36, PixelSize = .009f, OutlineSize = 6 });
        }
        var camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = 10.5f,
            Position = new Vector3(-9, 7, 5), Current = true };
        AddChild(camera);
        camera.LookAt(new Vector3(0, .8f, -1));
        GetViewport().Msaa3D = Viewport.Msaa.Msaa4X;
        var canvas = new CanvasLayer();
        AddChild(canvas);
        var title = new Label { Text = "궁수 · 발사 연출", Position = new Vector2(28, 22) };
        title.AddThemeFontSizeOverride("font_size", 28);
        canvas.AddChild(title);
        if (Array.IndexOf(OS.GetCmdlineUserArgs(), "--capture") < 0) return;
        SetProcess(false);
        await ToSignal(GetTree().CreateTimer(.6), SceneTreeTimer.SignalName.Timeout);
        bool draw = Array.IndexOf(OS.GetCmdlineUserArgs(), "--draw") >= 0;
        if (!draw)
        {
            Shoot();
            await ToSignal(GetTree().CreateTimer(.10), SceneTreeTimer.SignalName.Timeout);
        }
        foreach (Unit archer in _archers)
        {
            archer.GetNode("ArcherAnimation").SetProcess(false);
            foreach (Node child in archer.GetChildren()) if (child is ArrowFlight) child.SetProcess(false);
        }
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using Image image = GetViewport().GetTexture().GetImage();
        string path = draw ? "res://.godot/archer-draw.png" : "res://docs/images/archer-attack.png";
        Error result = image.SavePng(path);
        GD.Print($"Archer preview captured: {result} ({path})");
        GetTree().Quit(result == Error.Ok ? 0 : 1);
    }

    public override void _Process(double delta)
    {
        _clock += delta;
        if (_clock < 1.1) return;
        _clock = 0;
        Shoot();
    }

    private void Shoot()
    {
        _sequence++;
        foreach (Unit archer in _archers)
            archer.ApplyServerState(new UnitState(UnitActivity.Attack, 0, archer.State.FocusId, _sequence));
    }
}
