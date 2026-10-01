using Godot;

// Shared by the barracks archer and ranged minion. STATE is the only shot trigger.
public partial class ArcherAnimation : Node
{
    private Unit _unit;
    private Node3D _root, _leftArm, _rightArm, _leftLeg, _rightLeg, _body, _bow, _upperString, _lowerString, _nockedArrow;
    private Node3D _target;
    private uint _focusId;
    private Vector3 _leftGrip, _rightGrip, _aimGrip, _lastPosition;
    private PackedScene _arrowScene;
    private bool _positionKnown;
    private float _stride, _walk, _aim, _shotAge = 1;

    public override void _Ready()
    {
        _unit = GetParent<Unit>();
        bool minion = _unit.UnitType == UnitCatalog.MinionRanged;
        _root = _unit.GetNode<Node3D>(minion ? "Visual" : "Visual/Rig");
        _leftArm = _root.GetNode<Node3D>("LeftArm");
        _rightArm = _root.GetNode<Node3D>("RightArm");
        _leftLeg = _root.GetNode<Node3D>("LeftLeg");
        _rightLeg = _root.GetNode<Node3D>("RightLeg");
        _body = _root.GetNode<Node3D>(minion ? "Tunic" : "Body");
        _bow = _leftArm.GetNode<Node3D>("Bow");
        _upperString = _bow.GetNode<Node3D>("UpperString");
        _lowerString = _bow.GetNode<Node3D>("LowerString");
        _nockedArrow = _bow.GetNode<Node3D>("NockedArrow");
        _leftGrip = _bow.Position;
        _rightGrip = minion ? new Vector3(0, -.50f, 0) : new Vector3(0, -.66f, -.08f);
        _aimGrip = minion ? new Vector3(-.12f, 1.30f, -.50f) : new Vector3(-.18f, 1.82f, -.72f);
        _arrowScene = GD.Load<PackedScene>("res://units/ArrowFlight.tscn");
        _unit.StateChanged += OnStateChanged;
    }

    public override void _Process(double delta)
    {
        if (_unit.IsDying) { _nockedArrow.Hide(); SetProcess(false); return; }
        float dt = (float)delta;
        Vector3 position = _unit.GlobalPosition;
        float distance = _positionKnown ? position.DistanceTo(_lastPosition) : 0;
        _lastPosition = position;
        _positionKnown = true;
        float moving = dt > 0 && distance < 1 && _unit.State.Activity is UnitActivity.Idle or UnitActivity.Guard
            ? Mathf.Clamp(distance / dt / 5, 0, 1) : 0;
        _walk = Mathf.MoveToward(_walk, moving, dt * 10);
        _stride += distance < 1 ? distance * 5 : 0;
        _shotAge = Mathf.Min(1, _shotAge + dt);
        _aim = Mathf.MoveToward(_aim, _unit.State.Activity == UnitActivity.Attack ? 1 : 0, dt * 8);
        RefreshTarget(_unit.State.FocusId);
        Pose(Mathf.SmoothStep(0, 1, Mathf.Clamp((_shotAge - .08f) / .35f, 0, 1)));
    }

    private void Pose(float draw)
    {
        float step = Mathf.Sin(_stride) * .4f * _walk;
        _leftLeg.Rotation = new Vector3(step, 0, 0);
        _rightLeg.Rotation = new Vector3(-step, 0, 0);
        float recoil = Mathf.Sin(Mathf.Clamp(_shotAge / .18f, 0, 1) * Mathf.Pi) * .09f;
        _body.Rotation = new Vector3(recoil, step * .08f, 0);
        AimArm(_leftArm, _leftGrip, _aimGrip, -step * .3f);
        // Counter-rotate at the wrist: the bow stays vertical as the arm rises.
        _bow.Quaternion = Quaternion.Identity.Slerp(_leftArm.Quaternion.Inverse() * new Quaternion(Vector3.Up, -Mathf.Pi / 2), _aim);
        Vector3 nock = new(.37f + draw * .32f * _aim, 0, 0);
        StringSegment(_upperString, new Vector3(.37f, .89f, 0), nock);
        StringSegment(_lowerString, new Vector3(.37f, -.86f, 0), nock);
        _nockedArrow.Position = nock - new Vector3(.60f, 0, 0);
        _nockedArrow.Visible = _aim > .5f && _shotAge > .14f;
        AimArm(_rightArm, _rightGrip, _root.ToLocal(_bow.ToGlobal(nock)), step * .4f);
    }

    private void AimArm(Node3D arm, Vector3 grip, Vector3 destination, float walk)
    {
        Vector3 reach = destination - arm.Position;
        Quaternion target = new(grip.Normalized(), reach.Normalized());
        arm.Quaternion = Quaternion.FromEuler(new Vector3(walk, 0, 0)).Slerp(target, _aim);
        // A small length adjustment keeps the single-piece stylized hand on the string.
        arm.Scale = Vector3.One * Mathf.Lerp(1, reach.Length() / grip.Length(), _aim);
    }

    private static void StringSegment(Node3D node, Vector3 tip, Vector3 nock)
    {
        Vector3 direction = nock - tip;
        node.Position = (tip + nock) * .5f;
        node.Quaternion = new Quaternion(Vector3.Up, direction.Normalized());
        node.Scale = new Vector3(1, direction.Length(), 1);
    }

    private void RefreshTarget(uint focusId)
    {
        if (_focusId != focusId) { _focusId = focusId; _target = null; }
        Node3D current = focusId == 0 ? null : _unit.ActionTarget;
        if (GodotObject.IsInstanceValid(current)) _target = current;
        if (!GodotObject.IsInstanceValid(_target) || !_target.IsInsideTree() || !_target.IsVisibleInTree()) _target = null;
    }

    private void OnStateChanged(UnitState state, bool swung)
    {
        RefreshTarget(state.FocusId);
        if (!swung || state.Activity != UnitActivity.Attack || state.FocusId == 0 || _unit.IsDying) return;
        _aim = 1;
        Pose(1);
        Vector3 start = _bow.GlobalPosition;
        Vector3 end = _target == null
            ? start - _root.GlobalBasis.Z.Normalized() * Mathf.Max(1, _unit.Stats?.Range ?? 6)
            : _target.GlobalPosition + Vector3.Up * (_target is Unit unit ? unit.HealthBarHeight * .5f : 1.4f);
        var arrow = _arrowScene.Instantiate<ArrowFlight>();
        _unit.AddChild(arrow); // HIDE, resync and scene exit also remove the projectile.
        arrow.Launch(start, end, _target);
        _shotAge = 0;
        Pose(0);
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_unit)) _unit.StateChanged -= OnStateChanged;
    }
}
