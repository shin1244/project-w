using Godot;
using System;
using System.Collections.Generic;

// Actual gameplay scenes with synthetic HEALING snapshots. F6: T team, 1/2 camera, Space attack, H heal on/off.
// --capture saves docs/images/healer-units-preview.png; --overhead uses the high game camera.
public partial class HealerUnitsPreview : Node3D
{
    private readonly Dictionary<uint, Unit> _units = new();
    private readonly List<(Unit Healer, Unit Patient)> _pairs = new();
    private Camera3D _camera;
    private Control _layout;
    private bool _enemy, _healing = true;
    private uint _sequence;

    public override async void _Ready()
    {
        bool overhead = Array.Exists(OS.GetCmdlineUserArgs(), a => a == "--overhead");
        AddChild(new WorldEnvironment { Environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color("17212a"),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color("bacbd4"), AmbientLightEnergy = .55f,
            TonemapMode = Godot.Environment.ToneMapper.Filmic
        }});
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-48, -32, 0), LightEnergy = 1.25f,
            LightColor = new Color("ffe6c1"), ShadowEnabled = true, DirectionalShadowMaxDistance = 35 });
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-28, 140, 0), LightEnergy = .7f, LightColor = new Color("a2c8e5") });
        AddChild(new MeshInstance3D { Position = new Vector3(0, -.04f, 0), Mesh = new PlaneMesh
            { Size = new Vector2(200, 200), Material = new StandardMaterial3D { AlbedoColor = new Color("14202b"), Roughness = .85f } }});
        Pair(1, "Medic", new Vector2(2.6f, .2f), 2, "Knight", new Vector2(.7f, -1.7f));
        Pair(3, "Refueler", new Vector2(-2.4f, .2f), 4, "FlameAutomaton", new Vector2(-4.1f, -1.2f));
        _camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = overhead ? 11 : 7.6f, Current = true };
        AddChild(_camera);
        SetView(overhead);
        GetViewport().Msaa3D = Viewport.Msaa.Msaa4X;
        CreateLayout();
        if (!Array.Exists(OS.GetCmdlineUserArgs(), a => a == "--capture")) return;
        for (int i = 0; i < 20; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using Image image = GetViewport().GetTexture().GetImage();
        string path = overhead ? "res://.godot/healer-units-overhead.png" : "res://docs/images/healer-units-preview.png";
        Error result = image.SavePng(path);
        GD.Print($"Healer units captured: {path} ({result})");
        GetTree().Quit(result == Error.Ok ? 0 : 1);
    }

    private void Pair(uint healerId, string healer, Vector2 at, uint patientId, string patient, Vector2 patientAt)
    {
        Unit a = Spawn(healerId, healer, at), b = Spawn(patientId, patient, patientAt);
        b.GetNode<Node3D>("Visual").RotationDegrees = new Vector3(0, 150, 0);
        a.ApplyServerState(new UnitState(UnitActivity.Idle, 0, 0, 0));
        a.ApplyHealing(b.UnitId);
        _pairs.Add((a, b));
    }

    private Unit Spawn(uint id, string scene, Vector2 at)
    {
        var unit = GD.Load<PackedScene>($"res://units/{scene}.tscn").Instantiate<Unit>();
        unit.Initialize(id, 7, 1);
        unit.SetLocalTeam(1);
        AddChild(unit);
        unit.ApplyServerPosition(at.X, at.Y);
        unit.ResolveFocus = target => _units.TryGetValue(target, out Unit found) ? found : null;
        _units[id] = unit;
        return unit;
    }

    private void CreateLayout()
    {
        var canvas = new CanvasLayer(); AddChild(canvas);
        _layout = new Control { MouseFilter = Control.MouseFilterEnum.Ignore }; canvas.AddChild(_layout);
        Text("BURN QUARTER     /     MENDERS", 64, 42, 1100, 18, "b6a282");
        Text("회복 유닛", 60, 80, 1100, 43, "f0e7d4");
        Text("돌팔이 의사: 마나를 쓴 청록 약물 · 급유 기계: 기계에 기름 급유", 64, 143, 1300, 19, "a5b4bb");
        _layout.AddChild(new ColorRect { Position = new Vector2(64, 199), Size = new Vector2(1472, 1), Color = new Color("4c5a61") });
        // The camera looks along +Z, so the doctor (+X) stands on the left.
        Text("돌팔이 의사", 300, 770, 508, 32, "f0e7d4", HorizontalAlignment.Center);
        Text("영웅 > 병력 > 미니언 · 생체만 · 인력 사무소", 300, 821, 508, 20, "bd9c65", HorizontalAlignment.Center);
        Text("급유 기계", 800, 770, 508, 32, "f0e7d4", HorizontalAlignment.Center);
        Text("기계 병력만 · 마나 없음 · 공장", 800, 821, 508, 20, "bd9c65", HorizontalAlignment.Center);
        Text("실제 게임 모델  ·  동일 배율", 64, 931, 900, 17, "94a5af");
    }

    private void Text(string text, float x, float y, float width, int size, string color, HorizontalAlignment alignment = HorizontalAlignment.Left)
    {
        var label = new Label { Text = text, Position = new Vector2(x, y), Size = new Vector2(width, 0), HorizontalAlignment = alignment };
        label.AddThemeFontSizeOverride("font_size", size); label.AddThemeColorOverride("font_color", new Color(color)); _layout.AddChild(label);
    }

    public override void _Process(double delta)
    {
        if (_layout != null) _layout.Scale = GetViewport().GetVisibleRect().Size / new Vector2(1600, 1000);
    }

    private void SetView(bool overhead)
    {
        _camera.Position = overhead ? new Vector3(0, 18, -12) : new Vector3(0, 5.5f, -16);
        _camera.LookAt(new Vector3(0, overhead ? 0 : 1, 0));
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.Keycode is Key.Key1 or Key.Key2) SetView(key.Keycode == Key.Key2);
        if (key.Keycode == Key.Space)
            foreach (var (healer, patient) in _pairs)
                healer.ApplyServerState(new UnitState(UnitActivity.Attack, 0, patient.UnitId, ++_sequence));
        if (key.Keycode == Key.H)
        {
            _healing = !_healing;
            foreach (var (healer, patient) in _pairs) healer.ApplyHealing(_healing ? patient.UnitId : 0);
        }
        if (key.Keycode == Key.T)
        {
            _enemy = !_enemy;
            foreach (Unit unit in _units.Values) unit.Initialize(unit.UnitId, unit.OwnerId, _enemy ? 2u : 1u);
            if (_healing) foreach (var (healer, patient) in _pairs) healer.ApplyHealing(patient.UnitId);
        }
    }
}
