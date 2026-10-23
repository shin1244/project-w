using Godot;

// Presentation of HEALING: a stream from the healer's device (BeamOrigin) to its patient's
// chest. The quack doctor's dose is teal chemical light from the syringe's glass barrel;
// the refueler pumps dark oil. Droplets travel along the stream; nothing is drawn while
// the server reports no patient or the patient is not shown here.
public partial class HealingBeam : Node3D
{
    [Export] public bool Oil { get; set; }
    private Unit _unit;
    private Node3D _origin;
    private MeshInstance3D _stream;
    private readonly MeshInstance3D[] _drops = new MeshInstance3D[5];
    private float _clock;

    public override void _Ready()
    {
        _unit = GetParent<Unit>();
        _origin = _unit.FindChild("BeamOrigin", true, false) as Node3D;
        TopLevel = true;
        Color core = new(Oil ? "9c6a22" : "5fe3d0");
        var stream = new StandardMaterial3D
        {
            AlbedoColor = new Color(core, Oil ? .85f : .45f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };
        var drop = new StandardMaterial3D
        {
            AlbedoColor = Oil ? new Color("d9a04a") : new Color("c9fff6"), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };
        if (!Oil) { drop.EmissionEnabled = true; drop.Emission = core; }
        _stream = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = Oil ? .05f : .03f, BottomRadius = Oil ? .05f : .03f, Height = 1, RadialSegments = 6 },
            MaterialOverride = stream, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        AddChild(_stream);
        for (int i = 0; i < _drops.Length; i++)
        {
            _drops[i] = new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = Oil ? .07f : .055f, Height = Oil ? .14f : .11f, RadialSegments = 6, Rings = 3 },
                MaterialOverride = drop, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            AddChild(_drops[i]);
        }
        Visible = false;
    }

    public override void _Process(double delta)
    {
        _clock += (float)delta;
        Node3D patient = _unit.IsDying || _origin == null ? null : _unit.HealingTarget;
        Visible = patient != null;
        if (patient == null) return;
        Vector3 from = _origin.GlobalPosition;
        float chest = patient is Unit unit ? unit.HealthBarHeight * .45f : 1;
        Vector3 to = patient.GlobalPosition + Vector3.Up * chest;
        Vector3 along = to - from;
        float length = along.Length();
        if (length < .05f) { Visible = false; return; }
        Vector3 up = along / length;
        _stream.GlobalTransform = new Transform3D(new Basis(new Quaternion(Vector3.Up, up)) * Basis.FromScale(new Vector3(1, length, 1)), (from + to) / 2);
        for (int i = 0; i < _drops.Length; i++)
        {
            float t = Mathf.PosMod(_clock * (Oil ? 1.4f : 1.1f) + i / (float)_drops.Length, 1);
            _drops[i].GlobalPosition = from + along * t;
        }
    }
}
