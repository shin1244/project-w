using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

// Real barracks scenes with synthetic STATS/STATE. Space: fire, T: team, 1/2: camera.
public partial class PneumaticRiflePreview : Node3D
{
    private const float PreviewInterval = 1.4f; // Presentation fixture, never used by gameplay.
    private readonly List<Unit> _units = new();
    private Camera3D _camera;
    private Control _layout;
    private Label _phase;
    private float _clock;
    private uint _sequence;
    private bool _enemy;

    public override async void _Ready()
    {
        AddChild(new WorldEnvironment { Environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color("17212a"),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color("bacbd4"), AmbientLightEnergy = .55f,
            TonemapMode = Godot.Environment.ToneMapper.Filmic
        }});
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-48, -32, 0), LightEnergy = 1.25f,
            LightColor = new Color("ffe6c1"), ShadowEnabled = true, DirectionalShadowMaxDistance = 30 });
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-28, 140, 0), LightEnergy = .7f,
            LightColor = new Color("a2c8e5") });
        AddChild(new MeshInstance3D { Position = new Vector3(0, -.04f, 0), Mesh = new PlaneMesh
            { Size = new Vector2(200, 200), Material = Material("14202b") }});
        for (int i = 0; i < 2; i++)
        {
            float x = i == 0 ? 2.3f : -2.3f;
            AddChild(new MeshInstance3D { Position = new Vector3(x, -.025f, 0), Mesh = new CylinderMesh
                { TopRadius = 1.5f, BottomRadius = 1.54f, Height = .05f, RadialSegments = 64, Material = Material("41505a") }});
            var unit = GD.Load<PackedScene>("res://units/Archer.tscn").Instantiate<Unit>();
            unit.Initialize((uint)i + 1, 7, 1); unit.SetLocalTeam(1); AddChild(unit);
            unit.ApplyServerPosition(x, 0);
            unit.Stats = new StatsSnapshot(unit.UnitId, 1, 8, PreviewInterval, 4, 10);
            var visual = unit.GetNode<Node3D>("Visual");
            visual.RotationDegrees = new Vector3(0, i == 0 ? -35 : 145, 0);
            var target = new Node3D { Position = unit.Position - visual.Basis.Z * 8 + Vector3.Up * .4f };
            AddChild(target);
            unit.ResolveFocus = _ => target;
            unit.ApplyServerState(new UnitState(UnitActivity.Attack, 0, 100, 0));
            _units.Add(unit);
        }
        _camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = 7.6f, Current = true };
        AddChild(_camera); SetView(false);
        GetViewport().Msaa3D = Viewport.Msaa.Msaa4X;
        CreateLayout();
        bool capture = Array.Exists(OS.GetCmdlineUserArgs(), a => a == "--capture");
        bool cycle = Array.Exists(OS.GetCmdlineUserArgs(), a => a == "--cycle");
        if (!capture && !cycle) return;
        SetProcess(false);
        for (int i = 0; i < 8; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        _layout.Scale = GetViewport().GetVisibleRect().Size / new Vector2(1600, 1000);
        foreach (Unit unit in _units)
        {
            unit.SetProcess(false);
            unit.GetNode<PneumaticRifleAnimation>("PneumaticRifleAnimation").SetProcess(false);
        }
        if (cycle) { await CaptureCycle(); return; }
        bool reload = Array.Exists(OS.GetCmdlineUserArgs(), a => a == "--reload");
        bool attack = Array.Exists(OS.GetCmdlineUserArgs(), a => a == "--attack");
        if (reload || attack) Fire();
        foreach (Unit unit in _units) unit.GetNode<PneumaticRifleAnimation>("PneumaticRifleAnimation")._Process(reload ? .34 : attack ? .045 : .5);
        _phase.Text = reload ? "공기통 밸브 누르기" : attack ? "압축공기 발사" : "조준 자세";
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using Image image = GetViewport().GetTexture().GetImage();
        string path = $"res://docs/images/pneumatic-rifle-{(reload ? "reload" : attack ? "attack" : "preview")}.png";
        Error result = image.SavePng(path);
        GD.Print($"Pneumatic rifle captured: {path} ({result})");
        GetTree().Quit(result == Error.Ok ? 0 : 1);
    }

    private static StandardMaterial3D Material(string color) => new() { AlbedoColor = new Color(color), Roughness = .85f };
    private void CreateLayout()
    {
        var canvas = new CanvasLayer(); AddChild(canvas);
        _layout = new Control { MouseFilter = Control.MouseFilterEnum.Ignore }; canvas.AddChild(_layout);
        Text("BURN QUARTER     /     BARRACKS", 64, 42, 1100, 18, "b6a282");
        Text("공압사수", 60, 80, 1100, 43, "f0e7d4");
        Text("목제 총몸 · 황동 압력 장치 · 등짐 공기통", 64, 143, 1200, 19, "a5b4bb");
        _layout.AddChild(new ColorRect { Position = new Vector2(64, 199), Size = new Vector2(1472, 1), Color = new Color("4c5a61") });
        Text("전면", 174, 770, 508, 32, "f0e7d4", HorizontalAlignment.Center);
        Text("공압식 조총 · 연결 호스", 174, 821, 508, 20, "bd9c65", HorizontalAlignment.Center);
        Text("후면", 916, 770, 508, 32, "f0e7d4", HorizontalAlignment.Center);
        Text("압축공기통 · 어깨 뒤 재충전 밸브", 916, 821, 508, 20, "bd9c65", HorizontalAlignment.Center);
        _phase = Text("조준 → 발사 → 밸브 누르기 → 조준", 64, 920, 1472, 21, "c6b18b", HorizontalAlignment.Center);
    }
    private Label Text(string text, float x, float y, float width, int size, string color, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        var label = new Label { Text = text, Position = new Vector2(x, y), Size = new Vector2(width, 0), HorizontalAlignment = align };
        label.AddThemeFontSizeOverride("font_size", size); label.AddThemeColorOverride("font_color", new Color(color));
        _layout.AddChild(label); return label;
    }
    private void Fire()
    {
        foreach (Unit unit in _units) unit.ApplyServerState(new UnitState(UnitActivity.Attack, 0, 100, _sequence + 1));
        _sequence++;
    }
    public override void _Process(double delta)
    {
        _clock += (float)delta;
        if (_clock >= PreviewInterval) { _clock %= PreviewInterval; Fire(); }
        if (_layout != null) _layout.Scale = GetViewport().GetVisibleRect().Size / new Vector2(1600, 1000);
    }
    private async Task CaptureCycle()
    {
        const int fps = 30, frameCount = 168, framesPerShot = 42;
        const string directory = "res://.godot/pneumatic-rifle-cycle";
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(directory));
        for (int frame = 0; frame < frameCount; frame++)
        {
            int phaseFrame = frame % framesPerShot;
            if (phaseFrame == 0) Fire();
            _phase.Text = phaseFrame < 5 ? "압축공기 발사" : phaseFrame < 20 ? "공기통 밸브 누르기" : "조준 · 다음 발사 대기";
            foreach (Unit unit in _units)
            {
                unit.SetProcess(false);
                unit.GetNode<PneumaticRifleAnimation>("PneumaticRifleAnimation")._Process(1.0 / fps);
                foreach (Node child in unit.GetChildren()) if (child is PneumaticPelletFlight pellet) { pellet.SetProcess(false); pellet._Process(1.0 / fps); }
            }
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using Image image = GetViewport().GetTexture().GetImage();
            if (image.SavePng($"{directory}/frame-{frame:D4}.png") != Error.Ok)
            { GD.PushError("Could not save pneumatic rifle frame"); GetTree().Quit(1); return; }
        }
        GD.Print($"PASS: captured {frameCount} pneumatic rifle frames at {fps} fps");
        GetTree().Quit();
    }
    private void SetView(bool low)
    {
        _camera.Position = new Vector3(0, low ? 3.2f : 5.2f, -14);
        _camera.LookAt(new Vector3(0, 1.3f, 0));
    }
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.Keycode == Key.Space) { Fire(); _clock = 0; }
        if (key.Keycode is Key.Key1 or Key.Key2) SetView(key.Keycode == Key.Key1);
        if (key.Keycode == Key.T)
        {
            _enemy = !_enemy;
            foreach (Unit unit in _units) unit.Initialize(unit.UnitId, unit.OwnerId, _enemy ? 2u : 1u);
        }
    }
}
