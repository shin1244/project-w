using Godot;
using System;

public partial class Unit : Node3D
{
    // UnitCatalog의 서버 타입 번호. 용병·미니언·영웅은 각자 별도 타입을 사용한다.
    [Export] public uint UnitType { get; set; }
    [Export] public float HealthBarHeight { get; set; } = 2.4f;
    [Export] public bool AlwaysShowHealth { get; set; }
    // A larger model/picking capsule must not change the authoritative construction footprint.
    [Export] public float BodyRadius { get; set; }
    [Export] public NodePath AimNodePath { get; set; } = new("Visual");
    public HealthBar HealthBar { get; private set; }

    public uint UnitId { get; private set; }
    public uint OwnerId { get; private set; }
    // 소유권은 조종 가능 여부, 진영은 아군/적군 판정에 사용합니다. 미니언의 OwnerId는 0입니다.
    public uint Team { get; private set; }
    public UnitState State { get; private set; }
    public bool HasServerState { get; private set; }
    public StatsSnapshot? Stats { get; set; }
    public ManaSnapshot? Mana { get; set; }
    public WolfEffectSnapshot WolfEffects { get; set; }
    public GolemEffectSnapshot GolemEffects { get; set; }
    public MarshalEffectSnapshot MarshalEffects { get; set; }
    public uint EffectTick { get; set; }
    public SiegeSnapshot? Siege { get; private set; }
    public event Action<Unit> SiegeChanged;
    public KnightChargeSnapshot? KnightCharge { get; private set; }
    public event Action<Unit> KnightChargeChanged;
    public Node3D KnightChargeTarget
    {
        get
        {
            Node3D target = KnightCharge is { Charging: true } charge ? ResolveFocus?.Invoke(charge.TargetId) : null;
            return IsAvailable(target) && target.IsVisibleInTree() ? target : null;
        }
    }
    public bool IsDying { get; private set; }
    public DeathCause DeathCause { get; private set; }
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
    private HeroTeamRing _heroTeamRing;
    private bool _selected;
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
        if (UnitCatalog.IsHero(UnitType))
            _heroTeamRing = new HeroTeamRing(GetNode<MeshInstance3D>("SelectionRing"));
        else
            _teamMaterials = new TeamMaterials(GetNode<Node3D>("Visual"));
        RefreshTeamPresentation();
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
        _heroTeamRing?.Dispose();
        _heroTeamRing = null;
    }

    public void Initialize(uint unitId, uint ownerId, uint team)
    {
        if (UnitId != unitId || OwnerId != ownerId || Team != team)
        {
            Mana = null;
            Siege = null;
            SiegeChanged?.Invoke(this);
            KnightCharge = null;
            KnightChargeChanged?.Invoke(this);
            DeathCause = DeathCause.None;
        }
        UnitId = unitId;
        OwnerId = ownerId;
        Team = team;
        RefreshTeamPresentation();
    }

    public void SetLocalTeam(uint team)
    {
        _localTeam = team;
        RefreshTeamPresentation();
    }

    private void RefreshTeamPresentation()
    {
        _teamMaterials?.Apply(Team, _localTeam);
        _heroTeamRing?.Apply(Team, _localTeam, _selected, IsDying);
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

        // 이동식 포격차는 추적 중에도 보행 차체를 이동 방향으로 향하게 하고, 아래에서 포탑만 표적에 맞춥니다.
        bool mobileArtillery = UnitType == UnitCatalog.SteamArtillery && Siege?.Mode is null or SiegeMode.Mobile;
        // 첫 스폰은 제외하고, 움직였을 때만 모델의 정면(-Z)을 이동 방향으로 돌립니다.
        if (_hasServerPosition && (State.Activity is UnitActivity.Idle or UnitActivity.Guard || mobileArtillery)
            && direction.LengthSquared() > 0.000001f)
        {
            Node3D visual = GetNode<Node3D>("Visual");
            visual.LookAt(visual.GlobalPosition + direction, Vector3.Up);
        }

        _hasServerPosition = true;
        FaceActionTarget();
    }

    public void ApplyServerState(UnitState state)
    {
        if (IsDying || (UnitType != UnitCatalog.Worker && state.CarryType != ResourceType.Wood)) return;
        // 첫 스냅샷의 과거 공격은 재생하지 않습니다. 이후 번호가 바뀔 때만 한 번 재생합니다.
        bool swung = HasServerState && state.SwingSequence != State.SwingSequence;
        if (state.FocusId != State.FocusId) _focusTarget = null;
        bool wasInside = State.Activity == UnitActivity.Inside;
        State = state;
        HasServerState = true;
        // A worker inside a derrick leaves the world until it comes back out.
        if ((state.Activity == UnitActivity.Inside) != wasInside) Visible = state.Activity != UnitActivity.Inside;
        SetProcess(state.Activity != UnitActivity.Idle && state.FocusId != 0);
        FaceActionTarget();
        if (swung && IsVisibleInTree() && SoundBank.Swing(UnitType, state.Activity) is string sound) Sfx.At(sound, GlobalPosition);
        StateChanged?.Invoke(state, swung);
    }

    public void ApplySiege(SiegeSnapshot snapshot)
    {
        if (IsDying || UnitType != UnitCatalog.SteamArtillery || snapshot.UnitId != UnitId || Siege == snapshot) return;
        bool begins = snapshot.Transitioning && Siege?.Mode != snapshot.Mode;
        Siege = snapshot;
        if (begins && IsVisibleInTree()) Sfx.At(snapshot.Mode == SiegeMode.Deploying ? "artillery-deploy" : "artillery-pack", GlobalPosition);
        SiegeChanged?.Invoke(this);
    }

    public void ApplyKnightCharge(KnightChargeSnapshot snapshot)
    {
        if (IsDying || UnitType != UnitCatalog.Knight || snapshot.UnitId != UnitId || KnightCharge == snapshot) return;
        bool charges = snapshot.Charging && KnightCharge?.StartTick != snapshot.StartTick;
        KnightCharge = snapshot;
        if (charges && IsVisibleInTree()) Sfx.At("knight-charge", GlobalPosition);
        FaceActionTarget();
        KnightChargeChanged?.Invoke(this);
    }

    private void FaceActionTarget()
    {
        if (!IsDying && KnightCharge is { } charge && charge.ActiveAt(EffectTick))
        {
            // The launched anchor is fixed. Turning toward a moving victim would bend a straight charge.
            Vector3 anchor = new(charge.Anchor.X, GlobalPosition.Y, charge.Anchor.Y);
            Node3D visual = GetNode<Node3D>(AimNodePath);
            if (anchor.DistanceSquaredTo(GlobalPosition) > .000001f) visual.LookAt(anchor, Vector3.Up);
            return;
        }
        if (IsDying || State.Activity == UnitActivity.Idle || State.FocusId == 0) return;
        if (!IsAvailable(_focusTarget))
            _focusTarget = ResolveFocus?.Invoke(State.FocusId);
        // 대상이 늦게 스폰되면 다음 프레임에 다시 조회하고, 삭제되면 마지막 방향을 유지합니다.
        if (!IsAvailable(_focusTarget) || _focusTarget == this) return;
        Vector3 direction = _focusTarget.GlobalPosition - GlobalPosition;
        direction.Y = 0;
        if (direction.LengthSquared() > 0.000001f)
        {
            Node3D aim = GetNode<Node3D>(AimNodePath);
            aim.LookAt(aim.GlobalPosition + direction, Vector3.Up);
        }
    }

    private static bool IsAvailable(Node3D node) => GodotObject.IsInstanceValid(node)
        && node.IsInsideTree() && !node.IsQueuedForDeletion() && node is not Unit { IsDying: true };

    // DEATH는 같은 틱의 REMOVE보다 먼저 오며 연출만 고릅니다. 사망 뒤의 늦은 원인은 무시합니다.
    public void ApplyDeathCause(DeathCause cause)
    {
        if (!IsDying) DeathCause = cause;
    }

    public Tween BeginDeath()
    {
        if (IsDying) return _deathTween;
        IsDying = true;
        Siege = null;
        SiegeChanged?.Invoke(this);
        KnightCharge = null;
        KnightChargeChanged?.Invoke(this);
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
        _selected = selected && !IsDying;
        if (_heroTeamRing != null) RefreshTeamPresentation();
        else GetNode<MeshInstance3D>("SelectionRing").Visible = _selected;
        HealthBar?.SetSelected(selected && !IsDying);
    }
}
