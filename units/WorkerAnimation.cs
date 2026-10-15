using Godot;

// 일꾼 외형 전용. 피해·채집량·이동은 계산하지 않습니다.
public partial class WorkerAnimation : Node
{
    private Unit _unit;
    private AnimationPlayer _player;
    private Node3D _axe;
    private Node3D _wood;
    private Node3D _oil;
    private bool _attackSwing;
    private Node3D _gatherTarget;

    public override void _Ready()
    {
        _unit = GetParent<Unit>();
        _player = _unit.GetNode<AnimationPlayer>("AnimationPlayer");
        _axe = _unit.GetNode<Node3D>("Visual/Torso/RightArm/Axe");
        _wood = _unit.GetNode<Node3D>("Visual/Torso/CarriedWood");
        _oil = _unit.GetNode<Node3D>("Visual/Torso/CarriedOil");
        _unit.StateChanged += OnStateChanged;
        _player.AnimationFinished += OnAnimationFinished;
        RefreshPose();
    }

    private void OnStateChanged(UnitState state, bool swung)
    {
        if (swung && state.Activity != UnitActivity.Gather)
        {
            _attackSwing = true;
            Play("Swing", restart: true);
        }
        else
        {
            if (state.Activity == UnitActivity.Gather) _attackSwing = false;
            // 마지막 타격 직후 대상이 사라져도 시작한 휘두르기는 끝까지 보여줍니다.
            RefreshPose();
        }
    }

    private void RefreshPose()
    {
        if (_unit.IsDying) return;
        bool gathering = _unit.State.Activity == UnitActivity.Gather;
        SetProcess(gathering);
        _gatherTarget = gathering ? _unit.ActionTarget : null;
        if (gathering && IsOilGather)
        {
            // 전환 직전의 목재는 실제로 반납할 때까지 그대로 보여 줍니다.
            Play(_unit.State.Carrying > 0 && _unit.State.CarryType != ResourceType.Oil ? "Carry" : "OilWork");
        }
        else if (gathering && _unit.State.FocusId != 0 && _gatherTarget == null)
            Play(_unit.State.Carrying > 0 ? "Carry" : "Idle");
        else if (_attackSwing || _unit.State.Activity is UnitActivity.Gather or UnitActivity.Build)
            Play("Swing");
        else
            Play(_unit.State.Carrying > 0 ? "Carry" : "Idle");
    }

    private bool IsOilGather => _unit.ActionTarget is Building { BuildingType: BuildingCatalog.OilDerrick }
        || _unit.ActionTarget == null && _unit.State.CarryType == ResourceType.Oil;

    // STATE가 먼저 온 재접속에서도 건물이 나타나는 즉시 채집 자세를 갱신합니다.
    public override void _Process(double delta)
    {
        if (_unit.ActionTarget != _gatherTarget) RefreshPose();
    }

    private void Play(StringName animation, bool restart = false)
    {
        _wood.Visible = animation == "Carry" && _unit.State.CarryType != ResourceType.Oil;
        _oil.Visible = animation == "Carry" && _unit.State.CarryType == ResourceType.Oil;
        _axe.Visible = animation != "Carry" && animation != "OilWork";
        if (!restart && _player.IsPlaying() && _player.CurrentAnimation == animation) return;
        if (restart) _player.Stop();
        _player.Play(animation, customBlend: 0.12);
    }

    private void OnAnimationFinished(StringName animation)
    {
        if (animation != "Swing") return;
        _attackSwing = false;
        RefreshPose();
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_unit)) _unit.StateChanged -= OnStateChanged;
        if (GodotObject.IsInstanceValid(_player)) _player.AnimationFinished -= OnAnimationFinished;
    }
}
