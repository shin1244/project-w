using Godot;

// Presentation only: walk from rendered motion; attacks from server swing sequence.
public partial class SoldierAnimation : Node
{
    private Unit _unit;
    private Node3D _rig, _leftLeg, _rightLeg, _leftArm, _rightArm, _body;
    private Vector3 _lastPosition;
    private float _stride, _walk, _swing = 1;
    private bool _positionKnown;

    public override void _Ready()
    {
        _unit = GetParent<Unit>();
        _rig = _unit.GetNode<Node3D>("Visual/Rig");
        _leftLeg = _rig.GetNode<Node3D>("LeftLeg");
        _rightLeg = _rig.GetNode<Node3D>("RightLeg");
        _leftArm = _rig.GetNode<Node3D>("LeftArm");
        _rightArm = _rig.GetNode<Node3D>("RightArm");
        _body = _rig.GetNode<Node3D>("Body");
        _unit.StateChanged += OnStateChanged;
    }

    public override void _Process(double delta)
    {
        if (_unit.IsDying) { SetProcess(false); return; }
        float dt = (float)delta;
        Vector3 position = _unit.GlobalPosition;
        float distance = _positionKnown ? position.DistanceTo(_lastPosition) : 0;
        _lastPosition = position;
        _positionKnown = true;
        // Spawn/reconnect teleports are not steps. The server still owns speed and movement.
        float moving = dt > 0 && distance < 1 && _unit.State.Activity is UnitActivity.Idle or UnitActivity.Guard
            ? Mathf.Clamp(distance / dt / 5f, 0, 1) : 0;
        _walk = Mathf.MoveToward(_walk, moving, dt * 10);
        _stride += distance < 1 ? distance * 5 : 0;
        float step = Mathf.Sin(_stride) * .40f * _walk;
        _leftLeg.Rotation = new Vector3(step, 0, 0);
        _rightLeg.Rotation = new Vector3(-step, 0, 0);
        _rig.Position = new Vector3(0, Mathf.Abs(Mathf.Cos(_stride)) * .025f * _walk, 0);
        _body.Rotation = new Vector3(0, step * .08f, 0);

        _swing = Mathf.Min(1, _swing + dt / .48f);
        float attack = Mathf.Sin(_swing * Mathf.Pi);
        bool aiming = _unit.UnitType == 2 && _unit.State.Activity == UnitActivity.Attack;
        if (_unit.UnitType == 1)
        {
            _leftArm.Rotation = new Vector3(-step * .25f - attack * .15f, 0, 0);
            _rightArm.Rotation = new Vector3(step * .5f - attack * 1.5f, 0, -attack * .3f);
        }
        else
        {
            _leftArm.Rotation = new Vector3(aiming ? -.7f : -step * .3f, 0, 0);
            _rightArm.Rotation = new Vector3(aiming ? -.9f + attack * .38f : step * .45f, 0, aiming ? -.28f : 0);
        }
    }

    private void OnStateChanged(UnitState state, bool swung)
    {
        if (swung) _swing = 0; // Do not replay historical swings from an initial snapshot.
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_unit)) _unit.StateChanged -= OnStateChanged;
    }
}
