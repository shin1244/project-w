using Godot;
using System;

// 건물은 서버가 보내준 식별자와 배치를 표시합니다. SideId는 접속한 플레이어 ID가 아닙니다.
public partial class Building : Node3D
{
    [Export] public uint BuildingType { get; set; }
    [Export] public float HealthBarHeight { get; set; } = 4.7f;
    public HealthBar HealthBar { get; private set; }
    public uint BuildingId { get; private set; }
    public uint SideId { get; private set; }
    public UnitState State { get; private set; }
    public bool HasServerState { get; private set; }
    public Func<uint, Node3D> ResolveFocus { get; set; }
    public event Action<UnitState, bool> StateChanged;
    private Node3D _turret;
    private Node3D _ballista;
    private Marker3D _muzzle;
    private Vector3 _restPosition;
    private Tween _recoil;
    private MeshInstance3D _selection;

    public override void _Ready()
    {
        HealthBar = HealthBar.Attach(this, HealthBarHeight, 76);
        _turret = GetNodeOrNull<Node3D>("Turret");
        _ballista = GetNodeOrNull<Node3D>("Turret/Ballista");
        _muzzle = GetNodeOrNull<Marker3D>("Turret/Muzzle");
        if (_ballista != null) _restPosition = _ballista.Position;
        SetProcess(false);
    }

    public void SetSelected(bool selected)
    {
        HealthBar?.SetSelected(selected);
        if (_selection == null && selected)
        {
            float radius = GetMeta("footprint").AsVector2I().X * .7072f + .08f;
            _selection = new MeshInstance3D
            {
                Name = "SelectionRing",
                Position = new Vector3(0, .045f, 0),
                Mesh = new TorusMesh { InnerRadius = radius, OuterRadius = radius + .045f, Rings = 48, RingSegments = 8 },
                MaterialOverride = new StandardMaterial3D
                {
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    AlbedoColor = new Color("63e88a")
                },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            AddChild(_selection);
        }
        if (_selection != null) _selection.Visible = selected;
    }

    public void ApplyServerState(UnitState state)
    {
        if (BuildingType != 1 || state.Activity == UnitActivity.Gather || state.Carrying != 0) return;
        // 접속 스냅샷의 누적 발사 횟수로 지난 공격을 재생하지 않습니다.
        bool fired = HasServerState && state.SwingSequence != State.SwingSequence;
        State = state;
        HasServerState = true;
        SetProcess(state.Activity == UnitActivity.Attack && state.FocusId != 0);
        Node3D target = FaceTarget();
        if (fired) PlayShot(target);
        StateChanged?.Invoke(state, fired);
    }

    public override void _Process(double delta) => FaceTarget();

    private Node3D FaceTarget()
    {
        if (_turret == null || State.Activity != UnitActivity.Attack || State.FocusId == 0) return null;
        Node3D target = ResolveFocus?.Invoke(State.FocusId);
        if (!GodotObject.IsInstanceValid(target) || !target.IsInsideTree() ||
            target.IsQueuedForDeletion() || target is Unit { IsDying: true }) return null;
        Vector3 aim = target.GlobalPosition;
        aim.Y = _turret.GlobalPosition.Y;
        if (aim.DistanceSquaredTo(_turret.GlobalPosition) > .0001f) _turret.LookAt(aim);
        return target;
    }

    private void PlayShot(Node3D target)
    {
        if (_ballista == null) return;
        _recoil?.Kill();
        _ballista.Position = _restPosition + new Vector3(0, 0, .15f);
        _recoil = CreateTween();
        _recoil.TweenProperty(_ballista, "position", _restPosition, .22).SetTrans(Tween.TransitionType.Quad);
        if (target == null || _muzzle == null) return;

        // 서버는 즉시 피해를 적용합니다. 짧은 궤적은 발사 피드백만 표시합니다.
        Vector3 start = _muzzle.GlobalPosition;
        Vector3 end = target.GlobalPosition + Vector3.Up;
        float distance = start.DistanceTo(end);
        if (distance < .01f) return;
        var trace = new MeshInstance3D
        {
            Name = "ShotTrace",
            Mesh = new BoxMesh { Size = new Vector3(.035f, .035f, distance) },
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                AlbedoColor = new Color("ffd27b")
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        AddChild(trace);
        trace.GlobalPosition = (start + end) * .5f;
        trace.LookAt(end);
        trace.CreateTween().TweenCallback(Callable.From(trace.QueueFree)).SetDelay(.09);
    }

    public override void _ExitTree() => _recoil?.Kill();

    public void ApplyHealth(HealthSnapshot health) => HealthBar.Apply(health.Current, health.Maximum);

    public void ApplySnapshot(uint id, uint sideId, float x, float z, float yaw)
    {
        BuildingId = id;
        GlobalPosition = new Vector3(x, 0, z);
        GlobalRotation = new Vector3(0, yaw, 0);
        if (SideId == sideId) return;
        SideId = sideId;
        UpdateBanner();
    }

    private void UpdateBanner()
    {
        MeshInstance3D visual = GetNode<MeshInstance3D>("Visual");
        for (int i = 0; i < visual.Mesh.GetSurfaceCount(); i++)
        {
            if (visual.Mesh.SurfaceGetMaterial(i) is not StandardMaterial3D source ||
                source.ResourceName != "banner") continue;

            // 메시 재질은 공유됩니다. 각 건물에 별도 재질을 덮어씌웁니다.
            var banner = (StandardMaterial3D)source.Duplicate();
            banner.AlbedoColor = SideId switch
            {
                1 => new Color("376a94"),
                2 => new Color("a33c37"),
                _ => source.AlbedoColor
            };
            visual.SetSurfaceOverrideMaterial(i, banner);
        }
    }
}
