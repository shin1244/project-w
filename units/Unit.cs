using Godot;
using System;

public partial class Unit : Node3D
{
    // 서버의 타입 번호: 0 = 일꾼, 1 = 기사, 2 = 궁수. 각 씬에서 지정합니다.
    [Export] public uint UnitType { get; set; }

    public uint UnitId { get; private set; }
    public uint OwnerId { get; private set; }
    public UnitState State { get; private set; }
    public bool HasServerState { get; private set; }
    public bool IsDying { get; private set; }
    public event Action<UnitState, bool> StateChanged;
    // 관리자가 조회 방법만 연결합니다. Unit은 자원/유닛 목록을 직접 소유하지 않습니다.
    public Func<uint, Node3D> ResolveFocus { private get; set; }
    private bool _hasServerPosition;
    private Node3D _focusTarget;
    private Tween _deathTween;

    public override void _Ready() => SetProcess(false);

    // 공격자가 가만히 있어도 대상의 최신 POS를 따라 바라봅니다.
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
    }

    public void Initialize(uint unitId, uint ownerId)
    {
        UnitId = unitId;
        OwnerId = ownerId;
    }

    // 루트는 발밑(Y=0)에 둡니다. 실제 이동 계산은 서버에서만 합니다.
    public void ApplyServerPosition(float x, float z)
    {
        if (IsDying) return;
        Vector3 next = new(x, 0f, z);
        Vector3 direction = next - GlobalPosition;
        direction.Y = 0f;
        GlobalPosition = next;

        // 첫 스폰은 제외하고, 움직였을 때만 모델의 정면(-Z)을 이동 방향으로 돌립니다.
        if (_hasServerPosition && State.Activity == UnitActivity.Idle && direction.LengthSquared() > 0.000001f)
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
    }
}
