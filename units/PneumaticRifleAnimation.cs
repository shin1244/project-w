using Godot;
using System.Collections.Generic;

// Barracks rifle presentation only. STATE triggers shots; STATS supplies the reload cadence.
// The ranged lane minion deliberately keeps ArcherAnimation and its bow.
public partial class PneumaticRifleAnimation : Node
{
    private Unit _unit;
    private Node3D _rig, _rifle, _muzzle, _leftGrip, _rightGrip, _button, _tankExit, _gunInlet, _hose;
    private Node3D _leftLeg, _rightLeg, _airBurst, _shotTarget;
    private Arm _leftArm, _rightArm;
    private readonly List<Node3D> _hoseSegments = new();
    private readonly List<MeshInstance3D> _puffs = new();
    private Transform3D _rifleRest;
    private Vector3 _rigRest, _lastPosition;
    private float _stride, _walk, _aim, _shotAge = 10, _reloadDuration = .7f;
    private bool _ready;

    private static readonly SphereMesh PuffMesh = new() { Radius = .5f, Height = 1, RadialSegments = 8, Rings = 4 };
    private static readonly StandardMaterial3D AirMaterial = new()
    {
        AlbedoColor = new Color(.82f, .88f, .83f, .44f),
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
    };

    public override void _Ready()
    {
        _unit = GetParent<Unit>();
        _rig = _unit.GetNode<Node3D>("Visual/Rig");
        _rifle = _rig.GetNode<Node3D>("Rifle");
        _muzzle = _rifle.GetNode<Node3D>("Muzzle");
        _leftGrip = _rifle.GetNode<Node3D>("LeftGrip");
        _rightGrip = _rifle.GetNode<Node3D>("RightGrip");
        _button = _rig.GetNode<Node3D>("Body/PumpButton");
        _tankExit = _rig.GetNode<Node3D>("Body/HoseTankExit");
        _gunInlet = _rifle.GetNode<Node3D>("HoseGunInlet");
        _hose = _rig.GetNode<Node3D>("Hose");
        foreach (Node child in _hose.GetChildren()) if (child is Node3D segment) _hoseSegments.Add(segment);
        _leftArm = new Arm(_rig.GetNode<Node3D>("LeftArm"), -1);
        _rightArm = new Arm(_rig.GetNode<Node3D>("RightArm"), 1);
        _leftLeg = _rig.GetNode<Node3D>("LeftLeg");
        _rightLeg = _rig.GetNode<Node3D>("RightLeg");
        _rifleRest = _rifle.Transform; _rigRest = _rig.Position; _lastPosition = _unit.GlobalPosition;
        _airBurst = new Node3D { Name = "PneumaticAirBurst", TopLevel = true, Visible = false };
        AddChild(_airBurst);
        for (int i = 0; i < 4; i++)
        {
            var puff = new MeshInstance3D { Name = $"Air{i}", Mesh = PuffMesh, MaterialOverride = AirMaterial,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            _airBurst.AddChild(puff); _puffs.Add(puff);
        }
        ProcessPriority = 10;
        _unit.StateChanged += OnStateChanged;
        _ready = true;
        Pose();
    }

    public override void _Process(double delta)
    {
        if (!_ready) return;
        if (_unit.IsDying) { ClearShot(); SetProcess(false); return; }
        float dt = Mathf.Max(0, (float)delta);
        Vector3 position = _unit.GlobalPosition;
        float distance = position.DistanceTo(_lastPosition);
        _lastPosition = position;
        if (!_unit.IsVisibleInTree()) { ClearShot(); return; }
        float moving = dt > 0 && distance < 1 ? Mathf.Clamp(distance / dt / 4, 0, 1) : 0;
        _walk = Mathf.MoveToward(_walk, moving, dt * 8);
        if (distance < 1) _stride += distance * 5.5f;
        _shotAge = Mathf.Min(10, _shotAge + dt);
        Node3D target = VisibleTarget();
        if (_shotTarget != null && !Available(_shotTarget)) ClearShot();
        _aim = Mathf.MoveToward(_aim, _unit.State.Activity == UnitActivity.Attack && target != null ? 1 : 0, dt * 8);
        Pose();
        AnimateAirBurst();
    }

    private void Pose()
    {
        float step = Mathf.Sin(_stride) * .36f * _walk;
        _leftLeg.Rotation = new Vector3(step, 0, 0);
        _rightLeg.Rotation = new Vector3(-step, 0, 0);
        _rig.Position = _rigRest + Vector3.Up * (Mathf.Abs(Mathf.Sin(_stride)) * .025f * _walk);
        float recoil = Mathf.Sin(Mathf.Clamp(_shotAge / Mathf.Min(.14f, _reloadDuration * .22f), 0, 1) * Mathf.Pi);
        _rifle.Position = _rifleRest.Origin + Vector3.Back * (recoil * .085f);
        Quaternion rest = _rifleRest.Basis.GetRotationQuaternion() * new Quaternion(Vector3.Right, -.12f * (1 - _aim));
        _rifle.Quaternion = rest;
        Node3D target = VisibleTarget();
        if (target != null && _aim > 0)
        {
            Vector3 point = AimPoint(target);
            Vector3 direction = point - _rifle.GlobalPosition;
            if (direction.LengthSquared() > .001f && Mathf.Abs(direction.Normalized().Dot(Vector3.Up)) < .98f)
            {
                _rifle.LookAt(point, Vector3.Up);
                // Muzzle offset is not necessarily on the rifle pivot's centreline.
                Vector3 offset = _muzzle.GlobalPosition - _rifle.GlobalPosition;
                Vector3 forward = -_rifle.GlobalBasis.Z.Normalized();
                Vector3 perpendicularOffset = offset - forward * offset.Dot(forward);
                _rifle.LookAt(point - perpendicularOffset, Vector3.Up);
                _rifle.Quaternion = rest.Slerp(_rifle.Quaternion, _aim);
            }
        }
        _rifle.Quaternion *= new Quaternion(Vector3.Right, recoil * .035f);

        float p = Mathf.Clamp(_shotAge / _reloadDuration, 0, 1);
        float reach = p < .45f ? Ease((p - .14f) / .31f) : 1 - Ease((p - .60f) / .35f);
        Vector3 grip = _rig.ToLocal(_leftGrip.GlobalPosition);
        Vector3 button = _rig.ToLocal(_button.GlobalPosition);
        float press = p > .45f && p < .60f ? Mathf.Sin((p - .45f) / .15f * Mathf.Pi) * .035f : 0;
        // An outward arc lets the support hand pass beside the shoulder instead of through the chest.
        Vector3 hand = grip.Lerp(button + Vector3.Down * press, reach)
            + new Vector3(-.24f, .23f, .08f) * Mathf.Sin(reach * Mathf.Pi);
        _leftArm.Reach(hand);
        _rightArm.Reach(_rig.ToLocal(_rightGrip.GlobalPosition));
        PoseHose();
    }

    private void PoseHose()
    {
        Vector3 start = _hose.ToLocal(_tankExit.GlobalPosition), end = _hose.ToLocal(_gunInlet.GlobalPosition);
        float outside = Mathf.Max(start.X, end.X) + .34f;
        Vector3 control1 = new(outside, start.Y - .04f, start.Z + .12f);
        Vector3 control2 = new(outside + .06f, end.Y - .28f, end.Z + .14f);
        Vector3 previous = start;
        for (int i = 0; i < _hoseSegments.Count; i++)
        {
            float t = (i + 1f) / _hoseSegments.Count, u = 1 - t;
            Vector3 next = start * (u*u*u) + control1 * (3*u*u*t) + control2 * (3*u*t*t) + end * (t*t*t);
            Vector3 direction = next - previous;
            Node3D segment = _hoseSegments[i];
            segment.Position = (previous + next) * .5f;
            segment.Quaternion = new Quaternion(Vector3.Up, direction.Normalized());
            segment.Scale = new Vector3(1, direction.Length(), 1);
            previous = next;
        }
    }

    private void OnStateChanged(UnitState state, bool swung)
    {
        if (!_ready || _unit.IsDying) return;
        if (state.Activity != UnitActivity.Attack || state.FocusId == 0)
        {
            ClearShot(); _aim = 0; Pose(); return;
        }
        Node3D target = VisibleTarget();
        if (!swung || target == null || !_unit.IsVisibleInTree()) return;
        // Finish well inside the authoritative interval; unknown STATS uses only a cosmetic fallback.
        _reloadDuration = _unit.Stats is { Interval: > 0 } stats ? Mathf.Min(stats.Interval * .52f, .82f) : .7f;
        _shotAge = 10; _aim = 1; Pose();
        Vector3 start = _muzzle.GlobalPosition, end = AimPoint(target);
        var pellet = new PneumaticPelletFlight();
        _unit.AddChild(pellet);
        pellet.Launch(start, end, target);
        _shotAge = 0; _shotTarget = target;
        _airBurst.GlobalTransform = new Transform3D(_muzzle.GlobalBasis.Orthonormalized(), start);
        _airBurst.Show();
        Pose(); AnimateAirBurst();
    }

    private void AnimateAirBurst()
    {
        const float life = .20f;
        if (_shotAge >= life || !Available(_shotTarget)) { _airBurst.Hide(); return; }
        float t = _shotAge / life;
        for (int i = 0; i < _puffs.Count; i++)
        {
            MeshInstance3D puff = _puffs[i];
            float offset = i / 3f;
            puff.Position = new Vector3(Mathf.Sin(i * 2.1f) * t * .08f, t * .04f,
                -(.045f + t * .48f + offset * .13f));
            float size = .035f + t * .15f + offset * .015f;
            puff.Scale = new Vector3(size, size, size * (1.8f - t));
            puff.Transparency = Mathf.Clamp(t * .95f + offset * .08f, 0, 1);
        }
    }

    private void ClearShot() { _shotAge = 10; _shotTarget = null; _airBurst?.Hide(); }
    private Node3D VisibleTarget() => Available(_unit.ActionTarget) ? _unit.ActionTarget : null;
    private static bool Available(Node3D target) => target != null && GodotObject.IsInstanceValid(target)
        && target.IsInsideTree() && target.IsVisibleInTree() && !target.IsQueuedForDeletion() && target is not Unit { IsDying: true };
    private static Vector3 AimPoint(Node3D target) => target.GlobalPosition
        + Vector3.Up * (target is Unit unit ? unit.HealthBarHeight * .48f : 1.2f);
    private static float Ease(float t) => Mathf.SmoothStep(0, 1, Mathf.Clamp(t, 0, 1));

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_unit)) _unit.StateChanged -= OnStateChanged;
    }

    private sealed class Arm
    {
        private readonly Node3D _shoulder, _elbow;
        private readonly float _upperLength, _lowerLength, _side;
        public Arm(Node3D shoulder, float side)
        {
            _shoulder = shoulder; _side = side; _elbow = shoulder.GetNode<Node3D>("Elbow");
            _upperLength = _elbow.Position.Length();
            _lowerLength = _elbow.GetNode<Node3D>("Hand").Position.Length();
        }
        public void Reach(Vector3 target)
        {
            Vector3 delta = target - _shoulder.Position;
            float distance = Mathf.Clamp(delta.Length(), .02f, (_upperLength + _lowerLength) * .999f);
            Vector3 direction = delta.Normalized();
            Vector3 pole = new(_side * .8f, -.8f, .16f);
            pole = (pole - direction * pole.Dot(direction)).Normalized();
            float along = (_upperLength*_upperLength - _lowerLength*_lowerLength + distance*distance) / (2*distance);
            float height = Mathf.Sqrt(Mathf.Max(0, _upperLength*_upperLength - along*along));
            Vector3 bend = direction * along + pole * height;
            Quaternion upper = new(Vector3.Down, bend.Normalized());
            Quaternion lower = new(Vector3.Down, (direction * distance - bend).Normalized());
            _shoulder.Quaternion = upper;
            _elbow.Quaternion = upper.Inverse() * lower;
        }
    }
}
