using Godot;
using System.Collections.Generic;

// Presentation only: wheels follow rendered travel, the hose crane turns to the server's
// HEALING patient, the operator pumps while it refuels, the chimney puffs, and each attack
// swing jolts the cart forward.
public partial class RefuelerVisual : Node3D
{
    private Unit _unit;
    private Node3D _chassis, _arm, _lever, _chimney;
    private readonly List<Node3D> _wheels = new();
    private readonly List<(MeshInstance3D Puff, float Age)> _puffs = new();
    private Vector3 _lastPosition;
    private bool _positionKnown;
    private float _clock, _jolt = 1, _puffTimer;
    private static readonly SphereMesh PuffMesh = new() { Radius = .5f, Height = 1, RadialSegments = 8, Rings = 4 };
    private static readonly StandardMaterial3D SmokeMaterial = new()
    {
        AlbedoColor = new Color(.48f, .45f, .42f, .4f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
    };

    public override void _Ready()
    {
        _unit = GetParent<Unit>();
        _chassis = _unit.GetNode<Node3D>("Visual/Rig/Chassis");
        _arm = _chassis.GetNode<Node3D>("HoseArm");
        _lever = _chassis.GetNode<Node3D>("PumpLever");
        _chimney = _chassis.GetNode<Node3D>("ChimneyTop");
        foreach (string name in new[] { "WheelLeft", "WheelRight", "WheelFront" }) _wheels.Add(_chassis.GetNode<Node3D>(name));
        _unit.StateChanged += OnStateChanged;
    }

    public override void _Process(double delta)
    {
        if (_unit.IsDying) { SetProcess(false); return; }
        float dt = (float)delta;
        _clock += dt;
        Vector3 position = _unit.GlobalPosition;
        float distance = _positionKnown ? position.DistanceTo(_lastPosition) : 0;
        _lastPosition = position;
        _positionKnown = true;
        if (distance < 1) // Spawn and reconnect teleports are not travel.
            foreach (Node3D wheel in _wheels)
                wheel.Rotation = new Vector3(wheel.Rotation.X - distance / (wheel.Name == "WheelFront" ? .18f : .3f), 0, 0);
        float rolling = dt > 0 && distance < 1 ? Mathf.Clamp(distance / dt / 8f, 0, 1) : 0;

        _jolt = Mathf.Min(1, _jolt + dt / .35f);
        float jolt = Mathf.Sin(_jolt * Mathf.Pi);
        _chassis.Position = new Vector3(0, Mathf.Abs(Mathf.Sin(_clock * 18)) * .02f * rolling, -jolt * .18f);
        _chassis.Rotation = new Vector3(jolt * .08f, 0, 0);

        // The crane swings toward the patient in the cart's own frame; it rests forward otherwise.
        Node3D patient = _unit.HealingTarget;
        float yaw = 0;
        if (patient != null)
        {
            Vector3 local = _arm.GetParent<Node3D>().ToLocal(patient.GlobalPosition);
            yaw = Mathf.Atan2(-local.X, -local.Z);
        }
        _arm.Rotation = new Vector3(0, Mathf.LerpAngle(_arm.Rotation.Y, yaw, Mathf.Min(1, dt * 8)), 0);
        _lever.Rotation = new Vector3(patient != null ? Mathf.Sin(_clock * 9) * .45f : 0, 0, 0);

        _puffTimer -= dt;
        if (_puffTimer <= 0)
        {
            _puffTimer = patient != null ? .25f : .7f;
            var puff = new MeshInstance3D { Mesh = PuffMesh, MaterialOverride = SmokeMaterial, TopLevel = true };
            AddChild(puff);
            puff.GlobalPosition = _chimney.GlobalPosition;
            _puffs.Add((puff, 0));
        }
        for (int i = _puffs.Count - 1; i >= 0; i--)
        {
            (MeshInstance3D node, float age) = _puffs[i];
            age += dt;
            if (age > 1.4f || !GodotObject.IsInstanceValid(node))
            {
                if (GodotObject.IsInstanceValid(node)) node.QueueFree();
                _puffs.RemoveAt(i);
                continue;
            }
            node.GlobalPosition += new Vector3(0, dt * .8f, 0);
            node.Scale = Vector3.One * (.1f + age * .22f);
            _puffs[i] = (node, age);
        }
    }

    private void OnStateChanged(UnitState state, bool swung)
    {
        if (swung) _jolt = 0; // Initial snapshots never replay a historical swing.
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_unit)) _unit.StateChanged -= OnStateChanged;
    }
}
