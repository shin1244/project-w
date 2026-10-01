using Godot;
using System;
using System.Collections.Generic;

// F6: 실제 영웅 씬의 세 시점과 보행·타격 연출. 서버 연결 없음.
public partial class MutantWolfPreview : Control
{
    private sealed record View(Unit Unit, Camera3D Camera, Vector3 Home);
    private readonly List<View> _views = new();
    private Control _layout;
    private bool _walk, _rotate, _enemy, _freeze;
    private float _travel, _angle, _attackRemaining;
    private uint _swing;

    public override async void _Ready()
    {
        var background = new ColorRect { Color = new Color("101a22"), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(background);
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _layout = new Control { MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_layout);
        Text("PROJECT W   /   HERO 01", new Vector2(26, 18), 650, 17, "92a8b2");
        Text("돌연변이 늑대", new Vector2(24, 48), 950, 40, "e9e3d1");
        Text("네 개의 다리 · 작은 팔 하나", new Vector2(1000, 61), 576, 20, "b9a184", HorizontalAlignment.Right);
        AddView(new Rect2(24, 125, 1050, 735), new Vector3(5.8f, 3.5f, -6.2f), 3.9f);
        AddView(new Rect2(1090, 125, 486, 345), new Vector3(7, 2.5f, .15f), 3.6f);
        AddView(new Rect2(1090, 515, 486, 345), new Vector3(5, 8, -7), 4.3f);
        Text("오른쪽 작은 팔 · 굽힌 손목 · 세 갈래 발톱", new Vector2(38, 881), 1000, 23, "cbbda5");
        Text("측면", new Vector2(1104, 96), 458, 18, "a7b9be");
        Text("게임 시점", new Vector2(1104, 484), 458, 18, "a7b9be");
        Text("Space 걷기    A 물기    Q 돌진 자세    R 회전    T 진영색", new Vector2(26, 952), 1548, 19, "91a6ad");
        string[] args = OS.GetCmdlineUserArgs();
        _walk = Array.IndexOf(args, "--walk") >= 0;
        if (Array.IndexOf(args, "--attack") >= 0) Attack();
        if (Array.IndexOf(args, "--skill") >= 0) Skill();
        if (Array.IndexOf(args, "--capture") < 0) return;
        await ToSignal(GetTree().CreateTimer(.23), SceneTreeTimer.SignalName.Timeout);
        _freeze = true;
        foreach (View view in _views) view.Unit.GetNode("BeastAnimation").SetProcess(false);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        string path = Array.IndexOf(args, "--skill") >= 0 ? "res://.godot/mutant-wolf-skill.png"
            : Array.IndexOf(args, "--attack") >= 0 ? "res://.godot/mutant-wolf-attack.png"
            : _walk ? "res://.godot/mutant-wolf-walk.png" : "res://docs/images/mutant-wolf.png";
        using Image image = GetViewport().GetTexture().GetImage();
        Error result = image.SavePng(path);
        GD.Print($"Wolf preview captured: {result} ({path})");
        GetTree().Quit(result == Error.Ok ? 0 : 1);
    }

    private void AddView(Rect2 rect, Vector3 cameraPosition, float cameraSize)
    {
        var container = new SubViewportContainer
        {
            Position = rect.Position, Size = rect.Size, Stretch = true, MouseFilter = MouseFilterEnum.Ignore
        };
        _layout.AddChild(container);
        var viewport = new SubViewport
        {
            Size = new Vector2I((int)rect.Size.X, (int)rect.Size.Y), OwnWorld3D = true,
            GuiDisableInput = true, Msaa3D = Viewport.Msaa.Msaa4X, RenderTargetUpdateMode = SubViewport.UpdateMode.Always
        };
        container.AddChild(viewport);
        viewport.AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color("1b2b35"),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color("abc9d8"), AmbientLightEnergy = .55f,
                TonemapMode = Godot.Environment.ToneMapper.Filmic
            }
        });
        viewport.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-42, -36, 0),
            LightColor = new Color("ffe5bd"), LightEnergy = 1.35f, ShadowEnabled = true, DirectionalShadowMaxDistance = 25 });
        viewport.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-28, 130, 0),
            LightColor = new Color("9acdea"), LightEnergy = .75f });
        viewport.AddChild(new MeshInstance3D
        {
            Position = new Vector3(0, -.035f, 0),
            Mesh = new PlaneMesh { Size = new Vector2(200, 200), Material = new StandardMaterial3D { AlbedoColor = new Color("23343b"), Roughness = .9f } }
        });
        Unit unit = GD.Load<PackedScene>("res://units/HeroTest.tscn").Instantiate<Unit>();
        unit.Initialize((uint)_views.Count + 1, 7, 1);
        unit.SetLocalTeam(1);
        viewport.AddChild(unit);
        unit.ApplyServerPosition(0, 0);
        unit.ApplyServerState(new UnitState(UnitActivity.Idle, 0, 0, 0));
        var camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = cameraSize,
            Position = cameraPosition, Current = true };
        viewport.AddChild(camera);
        camera.LookAt(new Vector3(.1f, 1.15f, .10f));
        _views.Add(new View(unit, camera, cameraPosition));
    }

    public override void _Process(double delta)
    {
        if (_layout != null) _layout.Scale = GetViewportRect().Size / new Vector2(1600, 1000);
        if (_freeze) return;
        float dt = (float)delta;
        if (_rotate) _angle += dt * .55f;
        if (_attackRemaining > 0)
        {
            _attackRemaining -= dt;
            if (_attackRemaining <= 0)
                foreach (View view in _views) view.Unit.ApplyServerState(new UnitState(UnitActivity.Idle, 0, 0, _swing));
        }
        if (_walk && _attackRemaining <= 0) _travel -= dt * 3.2f;
        foreach (View view in _views)
        {
            view.Unit.ApplyServerPosition(0, _travel);
            view.Unit.GetNode<Node3D>("Visual").Rotation = new Vector3(0, _angle, 0);
            view.Camera.Position = view.Home + Vector3.Back * _travel;
        }
    }

    private void Attack()
    {
        _attackRemaining = .65f;
        _swing++;
        foreach (View view in _views) view.Unit.ApplyServerState(new UnitState(UnitActivity.Attack, 0, 0, _swing));
    }

    private void Skill()
    {
        _attackRemaining = .65f;
        foreach (View view in _views)
        {
            view.Unit.GetNode<BeastAnimation>("BeastAnimation").PlaySkill(0);
            view.Unit.ApplyServerState(new UnitState(UnitActivity.Dash, 0, 0, _swing));
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.Keycode == Key.Space) _walk = !_walk;
        if (key.Keycode == Key.A) Attack();
        if (key.Keycode == Key.Q) Skill();
        if (key.Keycode == Key.R) _rotate = !_rotate;
        if (key.Keycode != Key.T) return;
        _enemy = !_enemy;
        foreach (View view in _views) view.Unit.Initialize(view.Unit.UnitId, 7, _enemy ? 2u : 1u);
    }

    private void Text(string text, Vector2 position, float width, int size, string color, HorizontalAlignment alignment = HorizontalAlignment.Left)
    {
        var label = new Label { Text = text, Position = position, Size = new Vector2(width, 0),
            HorizontalAlignment = alignment, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", new Color(color));
        _layout.AddChild(label);
    }
}
