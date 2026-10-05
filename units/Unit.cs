using Godot;
using System;

public partial class Unit : Node3D
{
    // UnitCatalog의 서버 타입 번호. 용병·미니언·영웅은 각자 별도 타입을 사용한다.
    [Export] public uint UnitType { get; set; }
    [Export] public float HealthBarHeight { get; set; } = 2.4f;
    // A larger model/picking capsule must not change the authoritative construction footprint.
    [Export] public float BodyRadius { get; set; }
    public HealthBar HealthBar { get; private set; }

    public uint UnitId { get; private set; }
    public uint OwnerId { get; private set; }
    // 소유권은 조종 가능 여부, 진영은 아군/적군 판정에 사용합니다. 미니언의 OwnerId는 0입니다.
    public uint Team { get; private set; }
    public UnitState State { get; private set; }
    public bool HasServerState { get; private set; }
    public StatsSnapshot? Stats { get; set; }
    public WolfEffectSnapshot WolfEffects { get; set; }
    public bool IsDying { get; private set; }
    public event Action<UnitState, bool> StateChanged;
    // 관리자가 조회 방법만 연결합니다. Unit은 자원/유닛/건물 목록을 직접 소유하지 않습니다.
    public Func<uint, Node3D> ResolveFocus { private get; set; }
    private bool _hasServerPosition;
    private Node3D _focusTarget;
    // Presentation can use the same visible target already resolved for facing.
    public Node3D ActionTarget => IsAvailable(_focusTarget) ? _focusTarget : null;
    private Tween _deathTween;
    private uint _localTeam;
    private TeamMaterials _teamMaterials;
    private Vector3 _serverPosition;
    // 배치 검사는 50ms 늦은 표시 위치 대신 현재까지 수신한 서버 위치를 사용합니다.
    public Vector3 ServerPosition => _hasServerPosition ? _serverPosition : GlobalPosition;
    public float PlacementRadius { get; private set; }
    private readonly PositionHistory _positions = new();

    public override void _Ready()
    {
        SetProcess(false);
        PlacementRadius = BodyRadius > 0 ? BodyRadius :
            ((CapsuleShape3D)GetNode<CollisionShape3D>("SelectionArea/CollisionShape3D").Shape).Radius;
        HealthBar = HealthBar.Attach(this, HealthBarHeight);
        _teamMaterials = new TeamMaterials(GetNode<Node3D>("Visual"));
        _teamMaterials.Apply(Team, _localTeam);
    }

    public void ApplyHealth(HealthSnapshot health)
    {
        if (!IsDying) HealthBar.Apply(health.Current, health.Maximum);
    }

    // 공격자가 가만히 있어도 대상의 보간된 화면 위치를 따라 바라봅니다.
    public override void _Process(double delta) => FaceActionTarget();

    public override void _ExitTree()
    {
        // 맵 재설정/게임 종료로 연출이 중단되어도 콜백과 모델 참조를 남기지 않습니다.
        if (GodotObject.IsInstanceValid(_deathTween))
        {
            _deathTween.Kill();
            _deathTween.Dispose();
        }
        _deathTween = null;
        _teamMaterials?.Dispose();
        _teamMaterials = null;
    }

    public void Initialize(uint unitId, uint ownerId, uint team)
    {
        UnitId = unitId;
        OwnerId = ownerId;
        Team = team;
        _teamMaterials?.Apply(Team, _localTeam);
    }

    public void SetLocalTeam(uint team)
    {
        _localTeam = team;
        _teamMaterials?.Apply(Team, _localTeam);
    }

    // UNIT 스냅샷은 즉시 배치하고 이전 보간 이력을 버립니다.
    public void ApplyServerPosition(float x, float z)
    {
        if (IsDying) return;
        _serverPosition = new Vector3(x, 0f, z);
        _positions.Clear();
        ApplyDisplayPosition(_serverPosition);
    }

    public void BufferServerPosition(float x, float z)
    {
        if (!IsDying) _serverPosition = new Vector3(x, 0f, z);
    }

    public void CapturePosition(long tick) => _positions.Add(tick, _serverPosition);

    public bool RenderPosition(double tick)
    {
        if (IsDying) return false;
        Vector3 next = _positions.Sample(tick, _serverPosition);
        if (next == GlobalPosition) return false;
        ApplyDisplayPosition(next);
        return true;
    }

    // 루트 전체를 보간해 선택 영역·체력바·대상 표시가 모델과 같은 위치를 따르게 합니다.
    private void ApplyDisplayPosition(Vector3 next)
    {
        Vector3 direction = next - GlobalPosition;
        direction.Y = 0f;
        GlobalPosition = next;

        // 첫 스폰은 제외하고, 움직였을 때만 모델의 정면(-Z)을 이동 방향으로 돌립니다.
        if (_hasServerPosition && State.Activity is UnitActivity.Idle or UnitActivity.Guard && direction.LengthSquared() > 0.000001f)
        {
            Node3D visual = GetNode<Node3D>("Visual");
            visual.LookAt(visual.GlobalPosition + direction, Vector3.Up);
        }

        _hasServerPosition = true;
        FaceActionTarget();
    }

    public void ApplyServerState(UnitState state)
    {
        if (IsDying) return;
        // 첫 스냅샷의 과거 공격은 재생하지 않습니다. 이후 번호가 바뀔 때만 한 번 재생합니다.
        bool swung = HasServerState && state.SwingSequence != State.SwingSequence;
        if (state.FocusId != State.FocusId) _focusTarget = null;
        State = state;
        HasServerState = true;
        SetProcess(state.Activity != UnitActivity.Idle && state.FocusId != 0);
        FaceActionTarget();
        StateChanged?.Invoke(state, swung);
    }

    private void FaceActionTarget()
    {
        if (IsDying || State.Activity == UnitActivity.Idle || State.FocusId == 0) return;
        if (!IsAvailable(_focusTarget))
            _focusTarget = ResolveFocus?.Invoke(State.FocusId);
        // 대상이 늦게 스폰되면 다음 프레임에 다시 조회하고, 삭제되면 마지막 방향을 유지합니다.
        if (!IsAvailable(_focusTarget) || _focusTarget == this) return;
        Vector3 direction = _focusTarget.GlobalPosition - GlobalPosition;
        direction.Y = 0;
        if (direction.LengthSquared() > 0.000001f)
        {
            Node3D visual = GetNode<Node3D>("Visual");
            visual.LookAt(visual.GlobalPosition + direction, Vector3.Up);
        }
    }

    private static bool IsAvailable(Node3D node) => GodotObject.IsInstanceValid(node)
        && node.IsInsideTree() && !node.IsQueuedForDeletion() && node is not Unit { IsDying: true };

    public Tween BeginDeath()
    {
        if (IsDying) return _deathTween;
        IsDying = true;
        _positions.Clear();
        HealthBar.Clear();
        SetProcess(false);
        _focusTarget = null;
        ResolveFocus = null;
        SetSelected(false);
        var area = GetNode<Area3D>("SelectionArea");
        area.CollisionLayer = 0;
        area.InputRayPickable = false;
        area.GetNode<CollisionShape3D>("CollisionShape3D").SetDeferred(CollisionShape3D.PropertyName.Disabled, true);
        _deathTween = UnitDeathEffect.Play(this);
        return _deathTween;
    }

    public void SetSelected(bool selected)
    {
        GetNode<MeshInstance3D>("SelectionRing").Visible = selected && !IsDying;
        HealthBar?.SetSelected(selected && !IsDying);
    }
}
