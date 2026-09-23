using Godot;

// 일꾼 외형 전용. 피해·채집량·이동은 계산하지 않습니다.
public partial class WorkerAnimation : Node
{
    private Unit _unit;
    private AnimationPlayer _player;
    private Node3D _axe;
    private Node3D _wood;
    private bool _attackSwing;

    public override void _Ready()
    {
        _unit = GetParent<Unit>();
        _player = _unit.GetNode<AnimationPlayer>("AnimationPlayer");
        _axe = _unit.GetNode<Node3D>("Visual/Torso/RightArm/Axe");
        _wood = _unit.GetNode<Node3D>("Visual/Torso/CarriedWood");
        _unit.StateChanged += OnStateChanged;
        _player.AnimationFinished += OnAnimationFinished;
        RefreshPose();
    }

    private void OnStateChanged(UnitState state, bool swung)
    {
        if (swung)
        {
            _attackSwing = true;
            Play("Swing", restart: true);
        }
        else
        {
            // 마지막 타격 직후 대상이 사라져도 시작한 휘두르기는 끝까지 보여줍니다.
            RefreshPose();
        }
    }

    private void RefreshPose()
    {
        if (_unit.IsDying) return;
        if (_attackSwing || _unit.State.Activity == UnitActivity.Gather)
            Play("Swing");
        else
            Play(_unit.State.Carrying > 0 ? "Carry" : "Idle");
    }

    private void Play(StringName animation, bool restart = false)
    {
        _wood.Visible = animation == "Carry";
        _axe.Visible = !_wood.Visible;
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
