using Godot;

// Movement follows rendered server positions; staff strikes follow STATE swing numbers.
public partial class MinionHealerAnimation : Node
{
    private Unit _unit;
    private Node3D _body, _leftArm, _rightArm, _leftLeg, _rightLeg, _crystal;
    private Vector3 _rest, _lastPosition;
    private bool _positionKnown;
    private float _time, _stride, _walk, _strike = 1;

    public override void _Ready()
    {
        _unit = GetParent<Unit>();
        var rig = _unit.GetNode<Node3D>("Visual/Rig");
        _body = rig.GetNode<Node3D>("Body");
        _leftArm = _body.GetNode<Node3D>("LeftArm");
        _rightArm = _body.GetNode<Node3D>("RightArm");
        _leftLeg = rig.GetNode<Node3D>("LeftLeg");
        _rightLeg = rig.GetNode<Node3D>("RightLeg");
        _crystal = _rightArm.GetNode<Node3D>("Staff/Crystal");
        _rest = _body.Position;
        _unit.StateChanged += OnStateChanged;
    }

    public override void _Process(double delta)
    {
        if (_unit.IsDying) { SetProcess(false); return; }
        float dt = (float)delta;
        _time += dt;
        float distance = _positionKnown ? _unit.GlobalPosition.DistanceTo(_lastPosition) : 0;
        _positionKnown = true;
        _lastPosition = _unit.GlobalPosition;
        bool stepping = dt > 0 && distance < 1 && _unit.State.Activity is UnitActivity.Idle or UnitActivity.Guard;
        _walk = Mathf.MoveToward(_walk, stepping ? Mathf.Clamp(distance / dt / 3, 0, 1) : 0, dt * 7);
        if (distance < 1) _stride += distance * 5;
        _strike = Mathf.Min(1, _strike + dt / .55f);
        float gait = Mathf.Sin(_stride) * _walk;
        float strike = Mathf.Sin(_strike * Mathf.Pi);
        _body.Position = _rest + Vector3.Up * (Mathf.Sin(_time * 2) * .014f + Mathf.Abs(gait) * .035f);
        _body.Rotation = new Vector3(strike * -.08f, strike * .1f, gait * .025f);
        _leftLeg.Rotation = new Vector3(gait * .45f, 0, 0);
        _rightLeg.Rotation = new Vector3(-gait * .45f, 0, 0);
        _leftArm.Rotation = new Vector3(-gait * .3f, 0, .06f);
        _rightArm.Rotation = new Vector3(gait * .12f - strike * .9f, 0, -.06f);
        _crystal.Rotation = new Vector3(0, _time * .8f, 0);
        _crystal.Scale = Vector3.One * (1 + Mathf.Sin(_time * 3) * .055f + strike * .14f);
    }

    private void OnStateChanged(UnitState state, bool swung)
    {
        if (swung && state.Activity == UnitActivity.Attack) _strike = 0;
        if (state.Activity == UnitActivity.Stun) { _strike = 1; _walk = 0; }
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_unit)) _unit.StateChanged -= OnStateChanged;
    }
}
