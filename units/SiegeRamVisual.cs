using Godot;

// Cosmetic carriers share one Unit root, selection area and server-owned movement.
// Visual._Ready runs before Unit._Ready so TeamMaterials sees every team_cloth mesh.
public partial class SiegeRamVisual : Node3D
{
    private Unit _unit;
    private Node3D _load, _selectionArea, _selectionRing;
    private readonly Node3D[] _torsos = new Node3D[2];
    private readonly Node3D[] _legs = new Node3D[4];
    private Vector3 _lastPosition;
    private float _stride, _walk, _time;
    private bool _positionKnown;
    private static Parts _parts;

    // Geometry and source materials are shared between the two workers and all rams.
    // Unit's TeamMaterials creates only the per-unit team material overrides.
    private sealed class Parts
    {
        public readonly StandardMaterial3D Cloth = Material("c39448", "team_cloth");
        public readonly StandardMaterial3D Leather = Material("49301d");
        public readonly StandardMaterial3D Skin = Material("c28c61");
        public readonly StandardMaterial3D Wood = Material("70451f");
        public readonly StandardMaterial3D CutWood = Material("bf8b49");
        public readonly StandardMaterial3D Grain = Material("513019");
        public readonly StandardMaterial3D Iron = Material("444e53", metallic: .4f);
        public readonly StandardMaterial3D Edge = Material("8a9598", metallic: .55f);
        public readonly Mesh Body, Apron, Leg, Boot, Head, Hat, HatBrim, UpperArm, Forearm, Hand;
        public readonly Mesh Log, Band, LogEnd, RamHead, RamFace, Handle, GrainStrip, Sash, Rivet;

        public Parts()
        {
            Body = Box(new Vector3(.55f, .65f, .38f), Cloth);
            Apron = Box(new Vector3(.38f, .53f, .06f), Leather);
            Leg = Box(new Vector3(.19f, .48f, .25f), Leather);
            Boot = Box(new Vector3(.23f, .15f, .36f), Leather);
            Head = new SphereMesh { Radius = .24f, Height = .48f, RadialSegments = 12, Rings = 6, Material = Skin };
            Hat = Cylinder(.20f, .28f, .23f, Cloth);
            HatBrim = Cylinder(.35f, .35f, .045f, Cloth);
            UpperArm = Box(new Vector3(.18f, .25f, .22f), Cloth);
            Forearm = Box(new Vector3(.17f, .27f, .19f), Cloth);
            Hand = Box(new Vector3(.17f, .16f, .17f), Skin);
            Log = Cylinder(.36f, .39f, 3.6f, Wood);
            Band = Cylinder(.415f, .415f, .16f, Iron);
            LogEnd = Cylinder(.35f, .35f, .018f, CutWood);
            RamHead = Cylinder(.47f, .43f, .34f, Iron);
            RamFace = Cylinder(.35f, .46f, .18f, Edge);
            Handle = Box(new Vector3(2.12f, .105f, .13f), Wood);
            GrainStrip = Box(new Vector3(.026f, .014f, 3.25f), Grain);
            Sash = Box(new Vector3(.5f, .028f, .48f), Cloth);
            Rivet = new SphereMesh { Radius = .065f, Height = .13f, RadialSegments = 6, Rings = 3, Material = Edge };
        }
    }

    public override void _Ready()
    {
        _unit = GetParent<Unit>();
        _parts ??= new Parts();
        _selectionArea = _unit.GetNode<Node3D>("SelectionArea");
        _selectionRing = _unit.GetNode<Node3D>("SelectionRing");
        _load = new Node3D { Name = "RamLoad" };
        AddChild(_load);
        var alongZ = new Vector3(Mathf.Pi / 2, 0, 0);
        Add(_load, "Timber", _parts.Log, new Vector3(0, 1.34f, -.12f), alongZ);
        Add(_load, "RearCut", _parts.LogEnd, new Vector3(0, 1.34f, 1.691f), alongZ);
        Add(_load, "IronHead", _parts.RamHead, new Vector3(0, 1.34f, -1.91f), alongZ);
        Add(_load, "ImpactFace", _parts.RamFace, new Vector3(0, 1.34f, -2.13f), alongZ);
        for (int i = 0; i < 3; i++)
        {
            float z = -.12f + (i - 1) * 1.17f;
            Add(_load, "IronBand" + i, _parts.Band, new Vector3(0, 1.34f, z), alongZ);
            Add(_load, "LeftRivet" + i, _parts.Rivet, new Vector3(-.40f, 1.43f, z));
            Add(_load, "RightRivet" + i, _parts.Rivet, new Vector3(.40f, 1.43f, z));
            Add(_load, "Grain" + i, _parts.GrainStrip, new Vector3((i - 1) * .12f, 1.70f, -.12f));
        }
        Add(_load, "TeamSash", _parts.Sash, new Vector3(0, 1.752f, .46f));
        BuildCarrier(0, new Vector3(-.62f, 0, -.80f));
        BuildCarrier(1, new Vector3(.62f, 0, 1.03f));
        SyncSelectionFacing();
    }

    private void BuildCarrier(int index, Vector3 position)
    {
        var worker = new Node3D { Name = index == 0 ? "FrontCarrier" : "RearCarrier", Position = position };
        AddChild(worker);
        var torso = new Node3D { Name = "Torso", Position = new Vector3(0, .82f, 0) };
        worker.AddChild(torso);
        _torsos[index] = torso;
        Add(torso, "Body", _parts.Body, new Vector3(0, .11f, 0));
        Add(torso, "Apron", _parts.Apron, new Vector3(0, .05f, -.22f));
        Add(torso, "Head", _parts.Head, new Vector3(0, .66f, -.025f));
        Add(torso, "Hat", _parts.Hat, new Vector3(0, .90f, 0));
        Add(torso, "HatBrim", _parts.HatBrim, new Vector3(0, .80f, -.025f));
        for (int side = 0; side < 2; side++)
        {
            float sign = side == 0 ? -1 : 1;
            var leg = new Node3D { Name = side == 0 ? "LeftLeg" : "RightLeg", Position = new Vector3(sign * .16f, .55f, 0) };
            worker.AddChild(leg);
            _legs[index * 2 + side] = leg;
            Add(leg, "Trouser", _parts.Leg, new Vector3(0, -.24f, 0));
            Add(leg, "Boot", _parts.Boot, new Vector3(0, -.475f, -.045f));
            // Both bent arms hold the transverse wooden handle in front of the body.
            Add(torso, "UpperArm" + side, _parts.UpperArm,
                new Vector3(sign * .35f, .27f, -.05f), new Vector3(.28f, 0, sign * -.16f));
            Add(torso, "Forearm" + side, _parts.Forearm,
                new Vector3(sign * .36f, .255f, -.205f), new Vector3(-1.15f, 0, 0));
            Add(torso, "Hand" + side, _parts.Hand, new Vector3(sign * .35f, .31f, -.31f));
        }
        Add(_load, "CarryHandle" + index, _parts.Handle, new Vector3(0, 1.13f, position.Z - .31f));
    }

    public override void _Process(double delta)
    {
        if (_unit.IsDying) { SetProcess(false); return; }
        SyncSelectionFacing();
        float dt = (float)delta;
        _time += dt;
        Vector3 position = _unit.GlobalPosition;
        float distance = _positionKnown ? position.DistanceTo(_lastPosition) : 0;
        _positionKnown = true;
        _lastPosition = position;
        bool walking = dt > 0 && distance < 1 && _unit.State.Activity != UnitActivity.Stun;
        _walk = Mathf.MoveToward(_walk, walking ? Mathf.Clamp(distance / dt / 2.5f, 0, 1) : 0, dt * 7);
        if (distance < 1) _stride += distance * 3.7f;
        float bob = Mathf.Abs(Mathf.Sin(_stride)) * .032f * _walk + Mathf.Sin(_time * 1.8f) * .004f;
        for (int carrier = 0; carrier < 2; carrier++)
        {
            float step = Mathf.Sin(_stride + carrier * .28f) * _walk;
            _legs[carrier * 2].Rotation = new Vector3(step * .39f, 0, 0);
            _legs[carrier * 2 + 1].Rotation = new Vector3(-step * .39f, 0, 0);
            _torsos[carrier].Position = new Vector3(0, .82f + bob, 0);
            _torsos[carrier].Rotation = new Vector3(-.04f * _walk, 0, step * .015f);
        }
        _load.Position = new Vector3(0, bob, 0);
        _load.Rotation = new Vector3(Mathf.Sin(_stride + .14f) * .009f * _walk, 0, 0);
    }

    private void SyncSelectionFacing()
    {
        // The long pick capsule and selection ellipse follow the same heading as the load.
        _selectionArea.Rotation = new Vector3(0, Rotation.Y, 0);
        _selectionRing.Rotation = new Vector3(0, Rotation.Y, 0);
    }

    private static MeshInstance3D Add(Node3D parent, string name, Mesh mesh, Vector3 position, Vector3 rotation = default)
    {
        var instance = new MeshInstance3D { Name = name, Mesh = mesh, Position = position, Rotation = rotation };
        parent.AddChild(instance);
        return instance;
    }

    private static StandardMaterial3D Material(string color, string name = "", float metallic = 0) =>
        new() { ResourceName = name, AlbedoColor = new Color(color), Roughness = .85f, Metallic = metallic };

    private static BoxMesh Box(Vector3 size, Material material) => new() { Size = size, Material = material };

    private static CylinderMesh Cylinder(float top, float bottom, float height, Material material) =>
        new() { TopRadius = top, BottomRadius = bottom, Height = height, RadialSegments = 10, Rings = 1, Material = material };
}
