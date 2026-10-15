using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

// Actual unit scenes driven by synthetic authoritative snapshots; never a gameplay simulator.
public partial class SteamKnightPreview : Node3D
{
    private const int FramesPerSecond = 30, FrameCount = 120, LaunchFrame = 15, ArrivalFrame = 25;
    private const uint LaunchTick = 15, WatchdogTick = 75, ReadyTick = 375;
    private readonly List<Unit> _knights = new();
    private Unit _target;
    private Camera3D _camera;
    private Control _layout;
    private Label _phase;
    private bool _battle, _capture, _cycle;
    private int _frame;
    private double _accumulator;

    public override async void _Ready()
    {
        _capture = HasArgument("--capture");
        _cycle = HasArgument("--cycle");
        _battle = HasArgument("--charge") || _cycle;
        CreateWorld();
        if (_battle) CreateBattle();
        else CreateTurnaround();
        CreateLayout();
        _camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = _battle ? 8.1f : 7.6f, Current = true };
        AddChild(_camera);
        SetView(false);
        GetViewport().Msaa3D = Viewport.Msaa.Msaa4X;
        ResizeLayout();
        if (!_capture && !_cycle) return;
        SetProcess(false);
        for (int i = 0; i < 8; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        ResizeLayout();
        if (_cycle) { await CaptureCycle(); return; }
        if (_battle)
        {
            // A fresh start followed by two ticks shows the real cable launch, steam and sliding pose together.
            for (int frame = 0; frame <= LaunchFrame + 2; frame++) StepBattle(frame);
        }
        else
        {
            for (int frame = 0; frame < 18; frame++) StepTurnaround(1.0 / FramesPerSecond);
        }
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        string path = _battle ? "res://docs/images/steam-knight-charge.png" : "res://docs/images/steam-knight-preview.png";
        using Image image = GetViewport().GetTexture().GetImage();
        Error result = image.SavePng(path);
        GD.Print($"Steam knight captured: {path} ({result})");
        await FinishCapture(result == Error.Ok ? 0 : 1);
    }

    private static bool HasArgument(string argument) => Array.Exists(OS.GetCmdlineUserArgs(), a => a == argument);
    private static StandardMaterial3D Material(string color) => new() { AlbedoColor = new Color(color), Roughness = .85f };

    private void CreateWorld()
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
            { Size = new Vector2(200, 200), Material = Material("14202b") } });
        if (_battle)
        {
            AddChild(new MeshInstance3D { Position = new Vector3(0, -.035f, 0), Mesh = new BoxMesh
                { Size = new Vector3(11.3f, .07f, 3.4f), Material = Material("35434b") } });
        }
        else
        {
            foreach (float x in new[] { 2.3f, -2.3f })
                AddChild(new MeshInstance3D { Position = new Vector3(x, -.025f, 0), Mesh = new CylinderMesh
                    { TopRadius = 1.5f, BottomRadius = 1.54f, Height = .05f, RadialSegments = 64, Material = Material("41505a") } });
        }
    }

    private Unit AddUnit(string path, uint id, uint team, float x)
    {
        Unit unit = GD.Load<PackedScene>(path).Instantiate<Unit>();
        unit.Initialize(id, team == 1 ? 7u : 8u, team);
        unit.SetLocalTeam(1);
        AddChild(unit);
        unit.ApplyServerPosition(x, 0);
        unit.ApplyServerState(new UnitState(UnitActivity.Idle, 0, 0, 0));
        unit.SetProcess(false);
        if (unit.UnitType == UnitCatalog.Knight) unit.GetNode<SteamKnightAnimation>("SteamKnightAnimation").SetProcess(false);
        if (unit.UnitType == UnitCatalog.Archer) unit.GetNode<PneumaticRifleAnimation>("PneumaticRifleAnimation").SetProcess(false);
        return unit;
    }

    private static void MarkChargeReady(Unit unit)
    {
        unit.EffectTick = 0;
        unit.ApplyKnightCharge(new(unit.UnitId, false, 0, 0, 0, 0, Vector2.Zero));
        unit.ApplyKnightCharge(new(unit.UnitId, true, 0, 0, 0, 0, Vector2.Zero));
    }

    private void CreateTurnaround()
    {
        for (int i = 0; i < 2; i++)
        {
            Unit knight = AddUnit("res://units/Knight.tscn", (uint)(101 + i), 1, i == 0 ? 2.3f : -2.3f);
            knight.GetNode<Node3D>("Visual").RotationDegrees = new Vector3(0, i == 0 ? -35 : 145, 0);
            MarkChargeReady(knight);
            _knights.Add(knight);
        }
    }

    private void CreateBattle()
    {
        _target = AddUnit("res://units/Archer.tscn", 202, 2, -4);
        Unit knight = AddUnit("res://units/Knight.tscn", 101, 1, 4);
        _target.ResolveFocus = id => id == knight.UnitId ? knight : null;
        knight.ResolveFocus = id => id == _target.UnitId ? _target : null;
        _target.ApplyServerState(new UnitState(UnitActivity.Hold, 0, knight.UnitId, 0));
        knight.ApplyServerState(new UnitState(UnitActivity.Guard, 0, _target.UnitId, 0));
        _target.SetProcess(false);
        knight.SetProcess(false);
        MarkChargeReady(knight);
        _knights.Add(knight);
    }

    private void CreateLayout()
    {
        var canvas = new CanvasLayer(); AddChild(canvas);
        _layout = new Control { MouseFilter = Control.MouseFilterEnum.Ignore }; canvas.AddChild(_layout);
        Text("PROJECT W     /     BARRACKS", 64, 42, 1100, 18, "b6a282");
        Text("스팀돌격병", 60, 80, 1100, 43, "f0e7d4");
        Text(_battle ? "와이어 사출 · 증기 분사 · 고속 돌진" : "검과 방패 · 황동 압력통 · 양옆 톱니 윈치", 64, 143, 1200, 19, "a5b4bb");
        _layout.AddChild(new ColorRect { Position = new Vector2(64, 199), Size = new Vector2(1472, 1), Color = new Color("4c5a61") });
        if (_battle)
        {
            Text("와이어 돌진", 64, 775, 1472, 32, "f0e7d4", HorizontalAlignment.Center);
            Text("표적 고정 → 압력 해방 → 저상 슬라이딩 → 검 공격", 64, 827, 1472, 20, "bd9c65", HorizontalAlignment.Center);
            _phase = Text("압력 충전 완료 · 자동 돌진 준비", 64, 891, 1472, 21, "c6b18b", HorizontalAlignment.Center);
        }
        else
        {
            Text("전면", 174, 770, 508, 32, "f0e7d4", HorizontalAlignment.Center);
            Text("리벳 흉갑 · 가죽 하네스 · 검과 방패", 174, 821, 508, 20, "bd9c65", HorizontalAlignment.Center);
            Text("후면", 916, 770, 508, 32, "f0e7d4", HorizontalAlignment.Center);
            Text("압력 게이지 · 증기 배출구 · 와이어 윈치", 916, 821, 508, 20, "bd9c65", HorizontalAlignment.Center);
            _phase = Text("붉은 게이지 · 미세한 증기 누출 = 돌진 준비 완료", 64, 891, 1472, 21, "c6b18b", HorizontalAlignment.Center);
        }
        Text("실제 유닛 모델 · 모의 서버 상태로 재현한 연출 미리보기", 64, 950, 1472, 15, "85969f", HorizontalAlignment.Center);
    }

    private Label Text(string text, float x, float y, float width, int size, string color, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        var label = new Label { Text = text, Position = new Vector2(x, y), Size = new Vector2(width, 0), HorizontalAlignment = align };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", new Color(color));
        _layout.AddChild(label);
        return label;
    }

    private void ResizeLayout() => _layout.Scale = GetViewport().GetVisibleRect().Size / new Vector2(1600, 1000);

    private void StepTurnaround(double delta)
    {
        foreach (Unit knight in _knights) knight.GetNode<SteamKnightAnimation>("SteamKnightAnimation")._Process(delta);
    }

    private void StepBattle(int frame)
    {
        Unit knight = _knights[0];
        knight.EffectTick = (uint)frame;
        _target.EffectTick = (uint)frame;
        if (frame == 0)
        {
            MarkChargeReady(knight);
            knight.ApplyServerPosition(4, 0);
            knight.ApplyServerState(new UnitState(UnitActivity.Guard, 0, _target.UnitId, 0));
        }
        if (frame == LaunchFrame)
        {
            knight.ApplyServerState(new UnitState(UnitActivity.Dash, 0, _target.UnitId, 0));
            knight.ApplyKnightCharge(new(knight.UnitId, true, ReadyTick, LaunchTick, WatchdogTick, _target.UnitId, new(-4, 0)));
        }
        if (frame >= LaunchFrame && frame <= ArrivalFrame)
        {
            float portion = (frame - LaunchFrame) / (float)(ArrivalFrame - LaunchFrame);
            knight.ApplyServerPosition(Mathf.Lerp(4, -2.5f, portion), 0);
        }
        if (frame == ArrivalFrame)
        {
            knight.ApplyKnightCharge(new(knight.UnitId, true, ReadyTick, 0, 0, 0, Vector2.Zero));
            knight.ApplyServerState(new UnitState(UnitActivity.Attack, 0, _target.UnitId, 1));
        }
        knight.SetProcess(false);
        _target.SetProcess(false);
        knight._Process(1.0 / FramesPerSecond);
        _target._Process(1.0 / FramesPerSecond);
        knight.GetNode<SteamKnightAnimation>("SteamKnightAnimation")._Process(1.0 / FramesPerSecond);
        _target.GetNode<PneumaticRifleAnimation>("PneumaticRifleAnimation")._Process(1.0 / FramesPerSecond);
        _phase.Text = frame < LaunchFrame ? "압력 충전 완료 · 자동 돌진 준비" :
            frame < ArrivalFrame ? "앵커 고정 · 증기 분사 · 와이어 회수" :
            frame < ArrivalFrame + 18 ? "돌진 종료 · 검 공격으로 전환" : "돌진 재사용 대기 · 게이지 압력 낮음";
    }

    private async Task CaptureCycle()
    {
        const string directory = "res://.godot/steam-knight-cycle";
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(directory));
        for (int frame = 0; frame < FrameCount; frame++)
        {
            StepBattle(frame);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using Image image = GetViewport().GetTexture().GetImage();
            if (image.SavePng($"{directory}/frame-{frame:D4}.png") == Error.Ok) continue;
            GD.PushError("Could not save steam knight animation frame");
            GetTree().Quit(1);
            return;
        }
        GD.Print($"PASS: captured {FrameCount} steam knight frames at {FramesPerSecond} fps");
        await FinishCapture(0);
    }

    private async Task FinishCapture(int exitCode)
    {
        // Let the audio mixer release a still-playing launch burst before the one-shot renderer exits.
        foreach (Unit knight in _knights)
            knight.GetNode<SteamKnightAnimation>("SteamKnightAnimation").GetNode<AudioStreamPlayer3D>("PressureRelease").Stop();
        await ToSignal(GetTree().CreateTimer(.1), SceneTreeTimer.SignalName.Timeout);
        GetTree().Quit(exitCode);
    }

    private void SetView(bool low)
    {
        _camera.Position = new Vector3(0, low ? 3.2f : 5.2f, -14);
        _camera.LookAt(new Vector3(0, 1.3f, 0));
    }

    public override void _Process(double delta)
    {
        ResizeLayout();
        if (!_battle) { StepTurnaround(delta); return; }
        _accumulator += delta;
        while (_accumulator >= 1.0 / FramesPerSecond && _frame < FrameCount)
        {
            _accumulator -= 1.0 / FramesPerSecond;
            StepBattle(_frame++);
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.Keycode is Key.Key1 or Key.Key2) SetView(key.Keycode == Key.Key1);
    }
}
