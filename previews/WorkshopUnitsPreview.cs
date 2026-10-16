using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

// Actual gameplay scenes. F6: S deploy/pack, M walk, A traverse, Space attack, T team, 1/2 camera.
public partial class WorkshopUnitsPreview : Node3D
{
    private readonly List<Unit> _units = new();
    private readonly List<Vector3> _stagePositions = new();
    private readonly List<Node3D> _targets = new();
    private Camera3D _camera;
    private Control _layout;
    private float _time;
    private uint _sequence, _tick;
    private bool _enemy, _deployed = true, _flameOnly, _artilleryOnly, _walking, _traverse, _sweepAim, _venting;

    public override async void _Ready()
    {
        _flameOnly = Array.Exists(OS.GetCmdlineUserArgs(), a => a == "--flame-only");
        _artilleryOnly = !_flameOnly && Array.Exists(OS.GetCmdlineUserArgs(), a => a == "--artillery-only");
        _traverse = _artilleryOnly && Array.Exists(OS.GetCmdlineUserArgs(), a => a == "--traverse");
        _venting = _artilleryOnly && Array.Exists(OS.GetCmdlineUserArgs(), a => a == "--venting");
        AddChild(new WorldEnvironment { Environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color("17212a"),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color("bacbd4"), AmbientLightEnergy = .55f,
            TonemapMode = Godot.Environment.ToneMapper.Filmic
        }});
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-48, -32, 0), LightEnergy = 1.25f,
            LightColor = new Color("ffe6c1"), ShadowEnabled = true, DirectionalShadowMaxDistance = 35 });
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-28, 140, 0), LightEnergy = .7f,
            LightColor = new Color("a2c8e5") });
        AddChild(new MeshInstance3D { Position = new Vector3(0, -.04f, 0), Mesh = new PlaneMesh
            { Size = new Vector2(200, 200), Material = Material("14202b") }});
        float[] positions = _flameOnly ? new[] { 2.3f, -2.3f } : _artilleryOnly ? new[] { 3.25f, -3.25f } : new[] { 5.3f, 0, -5.3f };
        for (int i = 0; i < positions.Length; i++)
        {
            float x = positions[i];
            AddChild(new MeshInstance3D { Position = new Vector3(x, -.025f, 0), Mesh = new CylinderMesh
                { TopRadius = _artilleryOnly || (!_flameOnly && i > 0) ? 2.5f : 1.64f, BottomRadius = _artilleryOnly || (!_flameOnly && i > 0) ? 2.55f : 1.68f,
                    Height = .05f, RadialSegments = 64, Material = Material("41505a") }});
            var unit = GD.Load<PackedScene>($"res://units/{(_flameOnly || (!_artilleryOnly && i == 0) ? "FlameAutomaton" : "SteamArtillery")}.tscn").Instantiate<Unit>();
            unit.Initialize((uint)i + 1, 7, 1); unit.SetLocalTeam(1); AddChild(unit);
            unit.ApplyServerPosition(x, 0);
            unit.GetNode<Node3D>("Visual").RotationDegrees = new Vector3(0, _flameOnly ? (i == 1 ? -90 : -28) : -48, 0);
            var target = new Node3D { Position = unit.Position + unit.GetNode<Node3D>("Visual").Basis * new Vector3(0, 0, -8) }; AddChild(target);
            unit.ResolveFocus = _ => target;
            unit.ApplyServerState(new UnitState(UnitActivity.Idle, 0, 0, 0));
            if (unit.UnitType == UnitCatalog.SteamArtillery) unit.ApplySiege(new SiegeSnapshot(unit.UnitId, i == positions.Length - 1 ? SiegeMode.Deployed : SiegeMode.Mobile, 0, 0));
            _units.Add(unit);
            _stagePositions.Add(unit.Position);
            _targets.Add(target);
        }
        _camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = _flameOnly ? 6.2f : _artilleryOnly ? 8.7f : 10.5f, Current = true };
        AddChild(_camera); SetView(false);
        GetViewport().Msaa3D = Viewport.Msaa.Msaa4X;
        CreateLayout();
        bool capture = Array.Exists(OS.GetCmdlineUserArgs(), a => a == "--capture");
        bool attack = Array.Exists(OS.GetCmdlineUserArgs(), a => a == "--attack");
        if (_traverse) SetTraverseTarget(Mathf.DegToRad(30));
        if (_artilleryOnly && Array.Exists(OS.GetCmdlineUserArgs(), a => a == "--cycle"))
        {
            for (int i = 0; i < 8; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await CaptureCycle();
            return;
        }
        if (!capture) { _sweepAim = _traverse; return; }
        for (int i = 0; i < 8; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (_venting)
        {
            // Freeze the authoritative transition midpoint while the pooled steam fills out.
            SetProcess(false);
            Unit artillery = _units[^1];
            artillery.SetProcess(false);
            artillery.EffectTick = 20;
            artillery.ApplySiege(new SiegeSnapshot(artillery.UnitId, SiegeMode.Deploying, 0, 40));
            var visual = artillery.GetNode<WorkshopMachineVisual>("WorkshopMachineVisual");
            visual.SetProcess(false);
            for (int i = 0; i < 8; i++) visual._Process(.05);
        }
        if (attack)
        {
            Fire();
            // A fixed presentation delta makes the attack frame reproducible.
            foreach (Unit unit in _units) unit.GetNode<WorkshopMachineVisual>("WorkshopMachineVisual")._Process(.065);
        }
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using Image image = GetViewport().GetTexture().GetImage();
        string path = $"res://docs/images/{(_flameOnly ? "flame-automaton-dark" : _venting ? "steam-artillery-venting" : _traverse ? "steam-artillery-traverse" : _artilleryOnly ? "steam-artillery-walker" : "workshop-units")}{(attack ? "-attack" : "-preview")}.png";
        Error result = image.SavePng(path); GD.Print($"Workshop units captured: {path} ({result})");
        GetTree().Quit(result == Error.Ok ? 0 : 1);
    }

    private static StandardMaterial3D Material(string color) => new() { AlbedoColor = new Color(color), Roughness = .85f };
    private void CreateLayout()
    {
        var canvas = new CanvasLayer(); AddChild(canvas); _layout = new Control { MouseFilter = Control.MouseFilterEnum.Ignore }; canvas.AddChild(_layout);
        Text("BURN QUARTER     /     WORKSHOP", 64, 42, 1100, 18, "b6a282");
        Text(_flameOnly ? "화염기계" : _artilleryOnly ? "증기포격차" : "공장 기계 병력", 60, 80, 1100, 43, "f0e7d4");
        Text(_flameOnly ? "그을린 철판 · 리벳 장갑 · 삼각형 무한궤도" : _venting ? "하부 배출구 · 전환 압력 증기 · 지면 여유" : _traverse ? "수평 회전 톱니 · 독립 포가 · 표적 추적" : _artilleryOnly ? "연결 다리 보행 · 접이식 차체 · 톱니 구동 거포" : "황동 압력계 · 철제 장갑 · 증기 구동", 64, 143, 1200, 19, "a5b4bb");
        _layout.AddChild(new ColorRect { Position = new Vector2(64, 199), Size = new Vector2(1472, 1), Color = new Color("4c5a61") });
        string[] names = _flameOnly ? new[] { "전면", "측면" } : _artilleryOnly ? new[] { "이동 모드", _venting ? "모드 전환" : "공성 모드" } : new[] { "화염기계", "증기포격차", "증기포격차" };
        string[] modes = _flameOnly ? new[] { "열에 그을린 분사구와 철제 보일러", "높은 선두 궤도와 리벳 측판" } : _venting ? new[] { "차체 아래 여섯 배출구", "다리 수납 · 하부 압력 증기 분출" } : _traverse ? new[] { "거포 운반 · 소형 포탑 대기", "차체 고정 · 거포만 좌우 조준" } : _artilleryOnly ? new[] { "여덟 다리 보행 · 차체 소형 포탑", "다리 수납 · 하부 공간 · 거포 전개" } : new[] { "가압 화염 분사", "이동", "공성 전개" };
        for (int i = 0; i < names.Length; i++)
        {
            float x = _flameOnly || _artilleryOnly ? 174 + i * 742 : 38 + i * 508;
            Text(names[i], x, 770, 508, 32, "f0e7d4", HorizontalAlignment.Center);
            Text(modes[i], x, 821, 508, 20, "bd9c65", HorizontalAlignment.Center);
        }
        Text("실제 게임 모델  ·  동일 배율", 64, 931, 900, 17, "94a5af");
    }
    private void Text(string text, float x, float y, float width, int size, string color, HorizontalAlignment alignment = HorizontalAlignment.Left)
    {
        var label = new Label { Text = text, Position = new Vector2(x, y), Size = new Vector2(width, 0), HorizontalAlignment = alignment };
        label.AddThemeFontSizeOverride("font_size", size); label.AddThemeColorOverride("font_color", new Color(color)); _layout.AddChild(label);
    }
    public override void _Process(double delta)
    {
        _time += (float)delta; _tick = (uint)(_time * 20);
        foreach (Unit unit in _units)
        {
            unit.EffectTick = _tick;
            if (unit.Siege is SiegeSnapshot siege && siege.Transitioning && _tick >= siege.EndTick)
                unit.ApplySiege(new SiegeSnapshot(unit.UnitId, siege.Mode == SiegeMode.Deploying ? SiegeMode.Deployed : SiegeMode.Mobile, 0, 0));
        }
        if (_walking)
            for (int i = 0; i < _units.Count; i++)
                if (_units[i].Siege is not SiegeSnapshot siege || siege.Mode == SiegeMode.Mobile)
                    _units[i].Position = _stagePositions[i] + Vector3.Forward * (.35f * Mathf.Sin(_time * 2));
        if (_sweepAim && !_flameOnly) SetTraverseTarget(Mathf.Sin(_time * Mathf.Tau / 8) * .75f);
        if (_layout != null) _layout.Scale = GetViewport().GetVisibleRect().Size / new Vector2(1600, 1000);
    }

    // Deterministic presentation frames from the actual controllers, with synthetic server snapshots.
    private async Task CaptureCycle()
    {
        const int fps = 24, frameCount = fps * 8;
        string directory = _traverse ? "res://.godot/artillery-traverse" : "res://.godot/artillery-cycle";
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(directory));
        SetProcess(false);
        foreach (Unit unit in _units)
        {
            unit.SetProcess(false);
            unit.GetNode<WorkshopMachineVisual>("WorkshopMachineVisual").SetProcess(false);
        }
        for (int frame = 0; frame < frameCount; frame++)
        {
            float time = frame / (float)fps;
            uint tick = (uint)Mathf.Round(time * 20);
            if (_traverse) SetTraverseTarget(Mathf.Sin(time * Mathf.Tau / 8) * .75f);
            for (int i = 0; i < _units.Count; i++)
            {
                Unit unit = _units[i];
                unit.EffectTick = tick;
                SiegeSnapshot snapshot = _traverse && i == 1 ? new(unit.UnitId, SiegeMode.Deployed, 0, 0)
                    : i == 0 || time < 1 || time >= 7
                    ? new(unit.UnitId, SiegeMode.Mobile, 0, 0)
                    : time < 3 ? new(unit.UnitId, SiegeMode.Deploying, 20, 60)
                    : time < 5 ? new(unit.UnitId, SiegeMode.Deployed, 0, 0)
                    : new(unit.UnitId, SiegeMode.Packing, 100, 140);
                unit.ApplySiege(snapshot);
                bool walking = !_traverse && snapshot.Mode == SiegeMode.Mobile && !(i == 0 && time >= 2 && time < 2.6f);
                float travel = walking ? .35f * Mathf.Sin(time * 2) : 0;
                unit.Position = _stagePositions[i] + Vector3.Forward * travel;
                if ((i == 0 && frame == fps * 2) || (i == 1 && frame == fps * 7 / 2))
                    unit.ApplyServerState(new UnitState(UnitActivity.Attack, 0, 100, ++_sequence));
                if (!_traverse && ((i == 0 && frame == fps * 3) || (i == 1 && frame == fps * 4)))
                    unit.ApplyServerState(new UnitState(UnitActivity.Idle, 0, 0, _sequence));
                unit.SetProcess(false);
                if (_traverse && i == 1) unit._Process(1.0 / fps);
                unit.GetNode<WorkshopMachineVisual>("WorkshopMachineVisual")._Process(1.0 / fps);
            }
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using Image image = GetViewport().GetTexture().GetImage();
            if (image.SavePng($"{directory}/frame-{frame:D4}.png") != Error.Ok)
            {
                GD.PushError("Cannot save artillery animation frame"); GetTree().Quit(1); return;
            }
        }
        GD.Print($"PASS: captured {frameCount} artillery {(_traverse ? "traverse" : "walking/deployment")} frames at {fps} fps");
        GetTree().Quit();
    }

    private void SetTraverseTarget(float angle)
    {
        Unit unit = _units[^1];
        _targets[^1].GlobalPosition = unit.GlobalPosition + unit.GetNode<Node3D>("Visual").GlobalBasis
            * new Vector3(Mathf.Sin(angle) * 8, 0, -Mathf.Cos(angle) * 8);
        if (unit.State.FocusId == 0) unit.ApplyServerState(new UnitState(UnitActivity.Guard, 0, 100, _sequence));
    }

    private void Fire()
    {
        _sequence++;
        foreach (Unit unit in _units) unit.ApplyServerState(new UnitState(UnitActivity.Attack, 0, 100, _sequence));
    }
    private void SetView(bool overhead)
    {
        _camera.Position = new Vector3(0, overhead ? 18 : _artilleryOnly ? 8 : 7, -16); _camera.LookAt(new Vector3(0, _artilleryOnly ? 1.25f : 1, 0));
    }
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.Keycode == Key.Space) Fire();
        if (key.Keycode is Key.Key1 or Key.Key2) SetView(key.Keycode == Key.Key2);
        if (key.Keycode == Key.M) _walking = !_walking;
        if (key.Keycode == Key.A && !_flameOnly) _sweepAim = !_sweepAim;
        if (key.Keycode == Key.S && !_flameOnly)
        {
            _deployed = !_deployed;
            // Synthetic preview snapshots only; gameplay consumes server start/end ticks.
            Unit artillery = _units[^1];
            artillery.ApplySiege(new SiegeSnapshot(artillery.UnitId, _deployed ? SiegeMode.Deploying : SiegeMode.Packing, _tick, _tick + 40));
        }
        if (key.Keycode == Key.T)
        {
            _enemy = !_enemy;
            foreach (Unit unit in _units)
            {
                SiegeSnapshot? siege = unit.Siege;
                unit.Initialize(unit.UnitId, unit.OwnerId, _enemy ? 2u : 1u);
                if (siege is SiegeSnapshot snapshot) unit.ApplySiege(snapshot);
            }
        }
    }
}
