using Godot;

// A scrap-built quadruped siege walker: one Unit root, selection area and server-owned movement.
// No two legs match, so it sags toward the short one and limps; the gait follows rendered distance.
// Visual._Ready runs before Unit._Ready so TeamMaterials sees every team_cloth mesh.
public partial class SiegeRamVisual : Node3D
{
    private const float ChassisHeight = 1.05f;
    // Static sag toward the short rear-left leg: nose slightly up, left side low.
    private static readonly Vector3 ChassisTilt = new(.03f, 0, .045f);
    private Unit _unit;
    private Node3D _chassis, _ram, _stokerTorso, _chimneyTop, _selectionArea, _selectionRing;
    private readonly Leg[] _legs = new Leg[4];
    private readonly MeshInstance3D[] _smoke = new MeshInstance3D[4];
    private Vector3 _lastPosition, _ramRest;
    private float _stride, _walk, _time;
    private bool _positionKnown;
    private static Parts _parts;

    private sealed class Leg
    {
        public Node3D Hip, Knee;
        public float Yaw, Side, End, Phase, Stride;
    }

    // Every leg came from a different repair: the factory original, one bent in a crash, a copper pipe
    // too short to reach the ground without a timber block, and an overlong spare held by splints.
    // Knee is (outward, up) from the hip; Reach is how far out the foot lands; Ground lifts the foot.
    private readonly record struct LegSpec(string Name, float Side, float End, float Splay, Vector2 Knee,
        float Reach, float Ground, float Phase, float Stride);

    private static readonly LegSpec[] Specs =
    {
        new("FactoryLeg", -1, -1, .42f, new Vector2(.50f, .36f), .98f, 0, 0, 1),
        new("BentLeg", 1, -1, .55f, new Vector2(.44f, .54f), .92f, 0, Mathf.Pi + .4f, .85f),
        new("PipeLeg", -1, 1, .28f, new Vector2(.40f, .16f), .78f, .22f, Mathf.Pi - .3f, .7f),
        new("SplintLeg", 1, 1, .50f, new Vector2(.38f, .76f), 1.10f, 0, .25f, 1.15f)
    };

    // Geometry and source materials are shared between all rams.
    // Unit's TeamMaterials creates only the per-unit team material overrides.
    private sealed class Parts
    {
        public readonly StandardMaterial3D Paint = Material("c39448", "team_cloth", .3f, .45f);
        public readonly StandardMaterial3D Lacquer = Material("2f363d", metallic: .7f, roughness: .3f);
        public readonly StandardMaterial3D Iron = Material("3b434b", metallic: .45f, roughness: .6f);
        public readonly StandardMaterial3D Steel = Material("8f979c", metallic: .7f, roughness: .3f);
        public readonly StandardMaterial3D Brass = Material("a88349", metallic: .7f, roughness: .35f);
        public readonly StandardMaterial3D Gold = Material("d9ae45", metallic: .85f, roughness: .25f);
        public readonly StandardMaterial3D Copper = Material("b0673e", metallic: .6f, roughness: .4f);
        public readonly StandardMaterial3D Wood = Material("5a4232");
        public readonly StandardMaterial3D Rope = Material("b9a784");
        public readonly StandardMaterial3D Soot = Material("2a2622");
        public readonly StandardMaterial3D Shirt = Material("8c7d63");
        public readonly StandardMaterial3D Trousers = Material("4a4038");
        public readonly StandardMaterial3D Leather = Material("6b4a33");
        public readonly StandardMaterial3D Skin = Material("c28c61");
        public readonly StandardMaterial3D Tag = Material("e8dcc0");
        public readonly StandardMaterial3D Furnace = new()
        {
            AlbedoColor = new Color("ff8a3d"), EmissionEnabled = true, Emission = new Color("ff8a3d"),
            EmissionEnergyMultiplier = 1.6f, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };
        public readonly StandardMaterial3D Smoke = new()
        {
            AlbedoColor = new Color(.46f, .43f, .39f, .55f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };
        // Hull, ram and boiler.
        public readonly Mesh Hull, Deck, Keel, Prow, SidePlate, Rivet, Shield, ShieldFrame, Emblem, HipHousing;
        public readonly Mesh Sleeve, SleeveBand, RamRod, RamBlock, Mane, LionFace, Brow, Snout, Jaw, Eye;
        public readonly Mesh Drum, DrumBand, DrumCap, Chimney, ChimneyCap, Firebox, FireGrate, Gauge, GaugeFace;
        public readonly Mesh Pipe, Pole, Flag, Finial, Footplate, Bracket, CoalBin, Coal, Chain, Puff;
        // Legs: span meshes are one unit long on their local Y and stretched between two joints.
        public readonly Mesh Beam, IronBeam, Shin, CopperPipe, Rod, Plank, Lash, PipeLash, Weld, Joint, KneeBolt, PipeJoint;
        public readonly Mesh Toe, Ankle, RoundPad, PadRim, PipeCap, Block, BlockStrap, FlatPad, PadBolt;
        // Stoker.
        public readonly Mesh StokerLeg, Boot, Body, Apron, NumberTag, TagMark, Smudge, Head, Cap, Brim;
        public readonly Mesh Respirator, Canister, Arm, Hand, Handle, Blade;

        public Parts()
        {
            Hull = Box(1.2f, .5f, 2.1f, Lacquer);
            Deck = Box(1.04f, .08f, 1.9f, Iron);
            Keel = Box(.78f, .24f, 1.7f, Iron);
            Prow = Box(1.12f, .4f, .5f, Lacquer);
            SidePlate = Box(.05f, .3f, 1.5f, Paint);
            Rivet = Sphere(.035f, Steel);
            Shield = Box(.5f, .024f, .42f, Paint);
            ShieldFrame = Box(.58f, .016f, .5f, Gold);
            Emblem = Box(.2f, .032f, .24f, Gold);
            HipHousing = Box(.16f, .3f, .36f, Iron);
            Sleeve = Cylinder(.26f, .26f, .7f, Iron);
            SleeveBand = Cylinder(.29f, .29f, .07f, Brass);
            RamRod = Cylinder(.12f, .12f, .5f, Steel);
            RamBlock = Box(.62f, .56f, .26f, Lacquer);
            Mane = new CylinderMesh { TopRadius = .44f, BottomRadius = .44f, Height = .1f, RadialSegments = 8, Rings = 1, Material = Brass };
            LionFace = Box(.42f, .4f, .12f, Gold);
            Brow = Box(.48f, .08f, .1f, Gold);
            Snout = Box(.22f, .16f, .14f, Gold);
            Jaw = Box(.3f, .08f, .08f, Gold);
            Eye = Sphere(.035f, Furnace);
            Drum = Cylinder(.34f, .34f, .78f, Copper);
            DrumBand = Cylinder(.355f, .355f, .05f, Brass);
            DrumCap = Cylinder(.16f, .34f, .14f, Lacquer);
            Chimney = Cylinder(.085f, .085f, .5f, Iron);
            ChimneyCap = Cylinder(.13f, .09f, .08f, Iron);
            Firebox = Box(.36f, .28f, .12f, Iron);
            FireGrate = Box(.22f, .08f, .02f, Furnace);
            Gauge = Cylinder(.08f, .08f, .03f, Brass);
            GaugeFace = Cylinder(.065f, .065f, .035f, Tag);
            Pipe = Cylinder(.05f, .05f, 1, Copper);
            Pole = Cylinder(.025f, .025f, 1, Iron);
            Flag = Box(.025f, .24f, .38f, Paint);
            Finial = Sphere(.045f, Gold);
            Footplate = Box(.86f, .05f, .34f, Iron);
            Bracket = Box(.05f, .2f, .3f, Iron);
            CoalBin = Box(.3f, .26f, .26f, Wood);
            Coal = Box(.26f, .07f, .22f, Soot);
            Chain = Box(.025f, 1, .025f, Steel);
            Puff = new SphereMesh { Radius = .5f, Height = 1, RadialSegments = 8, Rings = 4, Material = Smoke };

            Beam = Box(.2f, 1, .24f, Lacquer);
            IronBeam = Box(.2f, 1, .24f, Iron);
            Shin = Box(.16f, 1, .2f, Iron);
            CopperPipe = Cylinder(.07f, .07f, 1, Copper);
            Rod = Cylinder(.035f, .035f, 1, Steel);
            Plank = Box(.06f, 1, .05f, Wood);
            Lash = Box(.25f, 1, .38f, Rope);
            PipeLash = Box(.17f, 1, .17f, Rope);
            Weld = Box(.22f, .2f, .27f, Soot);
            Joint = Cylinder(.12f, .12f, .32f, Brass);
            KneeBolt = Cylinder(.11f, .11f, .3f, Iron);
            PipeJoint = Sphere(.11f, Brass);
            Toe = Box(.08f, .06f, .26f, Iron);
            Ankle = Sphere(.08f, Brass);
            RoundPad = Cylinder(.2f, .2f, .07f, Iron);
            PadRim = Cylinder(.21f, .21f, .025f, Brass);
            PipeCap = Cylinder(.1f, .1f, .06f, Iron);
            Block = Box(.42f, .22f, .36f, Wood);
            BlockStrap = Box(.44f, .04f, .38f, Rope);
            FlatPad = Box(.42f, .06f, .3f, Iron);
            PadBolt = Sphere(.03f, Steel);

            StokerLeg = Box(.12f, .36f, .15f, Trousers);
            Boot = Box(.14f, .08f, .22f, Soot);
            Body = Box(.32f, .4f, .22f, Shirt);
            Apron = Box(.26f, .34f, .03f, Leather);
            NumberTag = Box(.14f, .1f, .012f, Tag);
            TagMark = Box(.08f, .025f, .006f, Soot);
            Smudge = Box(.1f, .08f, .01f, Soot);
            Head = Sphere(.14f, Skin);
            Cap = Cylinder(.13f, .14f, .07f, Trousers);
            Brim = Box(.18f, .02f, .1f, Trousers);
            Respirator = Box(.12f, .08f, .07f, Iron);
            Canister = Cylinder(.035f, .035f, .07f, Brass);
            Arm = Box(.08f, 1, .09f, Shirt);
            Hand = Box(.07f, .07f, .07f, Skin);
            Handle = Box(.035f, 1, .035f, Wood);
            Blade = Box(.17f, .02f, .19f, Iron);
        }
    }

    public override void _Ready()
    {
        _unit = GetParent<Unit>();
        _parts ??= new Parts();
        _selectionArea = _unit.GetNode<Node3D>("SelectionArea");
        _selectionRing = _unit.GetNode<Node3D>("SelectionRing");
        _chassis = new Node3D { Name = "Chassis", Position = new Vector3(0, ChassisHeight, 0), Rotation = ChassisTilt };
        AddChild(_chassis);
        BuildHull();
        BuildRam();
        BuildBoiler();
        BuildStoker();
        for (int i = 0; i < Specs.Length; i++) BuildLeg(i);
        for (int i = 0; i < _smoke.Length; i++)
        {
            _smoke[i] = new MeshInstance3D { Name = "Smoke" + i, Mesh = _parts.Puff, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            AddChild(_smoke[i]);
        }
        SyncSelectionFacing();
    }

    private void BuildHull()
    {
        Add(_chassis, "Hull", _parts.Hull, new Vector3(0, 0, .05f));
        Add(_chassis, "Deck", _parts.Deck, new Vector3(0, .29f, .05f));
        Add(_chassis, "Keel", _parts.Keel, new Vector3(0, -.37f, .05f));
        Add(_chassis, "Prow", _parts.Prow, new Vector3(0, 0, -1.12f), new Vector3(.4f, 0, 0));
        // Painted armour, a gilded crest on a team shield and a pennant: the machine is the treasure.
        for (int side = -1; side <= 1; side += 2)
        {
            string name = side < 0 ? "Left" : "Right";
            Add(_chassis, name + "TeamPlate", _parts.SidePlate, new Vector3(side * .625f, .02f, .05f));
            for (int i = 0; i < 3; i++)
                Add(_chassis, $"{name}Rivet{i}", _parts.Rivet, new Vector3(side * .655f, .15f, -.55f + i * .5f));
        }
        Add(_chassis, "CrestFrame", _parts.ShieldFrame, new Vector3(0, .337f, -.55f));
        Add(_chassis, "TeamShield", _parts.Shield, new Vector3(0, .349f, -.55f));
        Add(_chassis, "GoldCrest", _parts.Emblem, new Vector3(0, .36f, -.55f));
        Add(_chassis, "PressureGauge", _parts.Gauge, new Vector3(-.3f, .345f, .1f));
        Add(_chassis, "GaugeFace", _parts.GaugeFace, new Vector3(-.3f, .35f, .1f));
        Add(_chassis, "PennantPole", _parts.Pole, new Vector3(.48f, .83f, 1));
        Add(_chassis, "Pennant", _parts.Flag, new Vector3(.48f, 1.2f, 1.2f));
        Add(_chassis, "PennantFinial", _parts.Finial, new Vector3(.48f, 1.35f, 1));
        // The stoker rides a footplate hung off the stern, chained to the machine he feeds.
        Add(_chassis, "Footplate", _parts.Footplate, new Vector3(0, -.15f, 1.36f));
        Add(_chassis, "LeftBracket", _parts.Bracket, new Vector3(-.38f, -.08f, 1.25f));
        Add(_chassis, "RightBracket", _parts.Bracket, new Vector3(.38f, -.08f, 1.25f));
        Add(_chassis, "CoalBin", _parts.CoalBin, new Vector3(.22f, .005f, 1.38f));
        Add(_chassis, "Coal", _parts.Coal, new Vector3(.22f, .15f, 1.38f), new Vector3(0, .2f, .06f));
        Span(_chassis, "SafetyChain", _parts.Chain, new Vector3(-.17f, .26f, 1.32f), new Vector3(-.18f, .5f, 1));
    }

    private void BuildRam()
    {
        var alongZ = new Vector3(Mathf.Pi / 2, 0, 0);
        Add(_chassis, "RamSleeve", _parts.Sleeve, new Vector3(0, -.08f, -1.3f), alongZ);
        Add(_chassis, "SleeveBand", _parts.SleeveBand, new Vector3(0, -.08f, -1.62f), alongZ);
        _ramRest = new Vector3(0, -.08f, -1.55f);
        _ram = new Node3D { Name = "Ram", Position = _ramRest };
        _chassis.AddChild(_ram);
        Add(_ram, "RamRod", _parts.RamRod, new Vector3(0, 0, -.2f), alongZ);
        Add(_ram, "RamBlock", _parts.RamBlock, new Vector3(0, 0, -.42f));
        Add(_ram, "Mane", _parts.Mane, new Vector3(0, 0, -.42f), alongZ);
        Add(_ram, "LionFace", _parts.LionFace, new Vector3(0, 0, -.6f));
        Add(_ram, "Brow", _parts.Brow, new Vector3(0, .14f, -.67f));
        Add(_ram, "Snout", _parts.Snout, new Vector3(0, -.06f, -.7f));
        Add(_ram, "Jaw", _parts.Jaw, new Vector3(0, -.17f, -.66f));
        Add(_ram, "LeftEye", _parts.Eye, new Vector3(-.1f, .06f, -.67f));
        Add(_ram, "RightEye", _parts.Eye, new Vector3(.1f, .06f, -.67f));
        // Boiler steam drives the ram: the copper line runs from the drum over the deck to the sleeve.
        Vector3 drum = new(.28f, .72f, .5f), deck = new(.36f, .36f, -.25f), sleeve = new(.12f, .14f, -1);
        Span(_chassis, "SteamLine", _parts.Pipe, drum, deck);
        Span(_chassis, "SteamFeed", _parts.Pipe, deck, sleeve);
    }

    private void BuildBoiler()
    {
        var boiler = new Node3D { Name = "Boiler", Position = new Vector3(0, .33f, .62f) };
        _chassis.AddChild(boiler);
        Add(boiler, "Drum", _parts.Drum, new Vector3(0, .39f, 0));
        Add(boiler, "LowerBand", _parts.DrumBand, new Vector3(0, .12f, 0));
        Add(boiler, "UpperBand", _parts.DrumBand, new Vector3(0, .62f, 0));
        Add(boiler, "Dome", _parts.DrumCap, new Vector3(0, .85f, 0));
        Add(boiler, "Chimney", _parts.Chimney, new Vector3(.14f, 1.1f, -.05f));
        Add(boiler, "ChimneyCap", _parts.ChimneyCap, new Vector3(.14f, 1.37f, -.05f));
        _chimneyTop = new Node3D { Name = "ChimneyTop", Position = new Vector3(.14f, 1.42f, -.05f) };
        boiler.AddChild(_chimneyTop);
        Add(boiler, "Firebox", _parts.Firebox, new Vector3(0, .2f, .37f));
        Add(boiler, "FireGrate", _parts.FireGrate, new Vector3(0, .2f, .44f));
    }

    private void BuildStoker()
    {
        var stoker = new Node3D { Name = "Stoker", Position = new Vector3(-.17f, -.125f, 1.38f) };
        _chassis.AddChild(stoker);
        for (int side = -1; side <= 1; side += 2)
        {
            string name = side < 0 ? "Left" : "Right";
            Add(stoker, name + "Leg", _parts.StokerLeg, new Vector3(side * .07f, .18f, 0));
            Add(stoker, name + "Boot", _parts.Boot, new Vector3(side * .07f, .04f, -.03f));
        }
        Add(stoker, "KneeGrime", _parts.Smudge, new Vector3(.07f, .17f, -.081f));
        _stokerTorso = new Node3D { Name = "Torso", Position = new Vector3(0, .36f, 0) };
        stoker.AddChild(_stokerTorso);
        Add(_stokerTorso, "Body", _parts.Body, new Vector3(0, .2f, 0));
        Add(_stokerTorso, "Apron", _parts.Apron, new Vector3(0, .16f, -.12f));
        Add(_stokerTorso, "ApronGrime", _parts.Smudge, new Vector3(.05f, .06f, -.137f));
        Add(_stokerTorso, "NumberTag", _parts.NumberTag, new Vector3(0, .26f, .116f));
        Add(_stokerTorso, "TagNumber", _parts.TagMark, new Vector3(0, .26f, .124f));
        Add(_stokerTorso, "Head", _parts.Head, new Vector3(0, .55f, -.01f));
        Add(_stokerTorso, "Cap", _parts.Cap, new Vector3(0, .67f, 0));
        Add(_stokerTorso, "CapBrim", _parts.Brim, new Vector3(0, .645f, -.13f));
        Add(_stokerTorso, "Respirator", _parts.Respirator, new Vector3(0, .51f, -.14f));
        Add(_stokerTorso, "Canister", _parts.Canister, new Vector3(.07f, .48f, -.17f), new Vector3(1.2f, 0, 0));
        // Both hands grip one shovel; the whole torso turns between the coal bin and the firebox.
        Vector3 grip = new(-.12f, .3f, -.12f), blade = new(.08f, -.12f, -.5f);
        Vector3 upperHand = grip.Lerp(blade, .1f), lowerHand = grip.Lerp(blade, .45f);
        Span(_stokerTorso, "ShovelHandle", _parts.Handle, grip, blade);
        Add(_stokerTorso, "ShovelBlade", _parts.Blade, blade, new Vector3(-.6f, 0, 0));
        Span(_stokerTorso, "LeftArm", _parts.Arm, new Vector3(-.17f, .34f, 0), upperHand);
        Span(_stokerTorso, "RightArm", _parts.Arm, new Vector3(.17f, .34f, 0), lowerHand);
        Add(_stokerTorso, "LeftHand", _parts.Hand, upperHand);
        Add(_stokerTorso, "RightHand", _parts.Hand, lowerHand);
    }

    private void BuildLeg(int index)
    {
        LegSpec spec = Specs[index];
        float side = spec.Side, yaw = -spec.End * side * spec.Splay;
        var hipPosition = new Vector3(side * .66f, -.18f, spec.End < 0 ? -.7f : .78f);
        Add(_chassis, spec.Name + "Housing", _parts.HipHousing, new Vector3(side * .6f, -.18f, hipPosition.Z));
        var hip = new Node3D { Name = spec.Name, Position = hipPosition, Rotation = new Vector3(0, yaw, 0) };
        _chassis.AddChild(hip);
        var knee = new Vector3(side * spec.Knee.X, spec.Knee.Y, 0);
        float footOut = side * spec.Reach;
        var foot = new Vector3(footOut, FootHeight(hipPosition, yaw, footOut, spec.Ground), 0) - knee;
        var kneeNode = new Node3D { Name = "Knee", Position = knee };
        hip.AddChild(kneeNode);
        var footNode = new Node3D { Name = "Foot", Position = foot };
        kneeNode.AddChild(footNode);
        _legs[index] = new Leg { Hip = hip, Knee = kneeNode, Yaw = yaw, Side = side, End = spec.End, Phase = spec.Phase, Stride = spec.Stride };
        var alongZ = new Vector3(Mathf.Pi / 2, 0, 0);
        switch (index)
        {
            case 0: // Factory original: lacquered thigh, piston, brass joints and a three-toed claw.
                Add(hip, "HipJoint", _parts.Joint, Vector3.Zero, alongZ);
                Span(hip, "Thigh", _parts.Beam, Vector3.Zero, knee);
                Span(hip, "Piston", _parts.Rod, new Vector3(0, .16f, 0), knee + new Vector3(-side * .08f, .1f, 0));
                Add(hip, "KneeJoint", _parts.Joint, knee, alongZ);
                Span(kneeNode, "Shin", _parts.Shin, Vector3.Zero, foot);
                Add(footNode, "Ankle", _parts.Ankle, new Vector3(0, .08f, 0));
                for (int toe = -1; toe <= 1; toe++)
                    Add(footNode, "Toe" + (toe + 1), _parts.Toe, new Vector3(toe * .07f, .03f, -.06f), new Vector3(0, toe * -.5f, 0));
                break;
            case 1: // Bent in a crash: the shin kinks outward at a sooty weld and ends on a round pad.
                Add(hip, "HipJoint", _parts.Joint, Vector3.Zero, alongZ);
                Span(hip, "Thigh", _parts.Beam, Vector3.Zero, knee);
                Add(hip, "Dent", _parts.Weld, knee * .45f, new Vector3(.15f, .3f, .2f));
                Add(hip, "KneeJoint", _parts.Joint, knee, alongZ);
                Vector3 kink = foot * .5f + new Vector3(side * .17f, .05f, 0);
                Span(kneeNode, "UpperShin", _parts.Shin, Vector3.Zero, kink);
                Span(kneeNode, "LowerShin", _parts.Shin, kink, foot);
                Add(kneeNode, "KinkWeld", _parts.Weld, kink, new Vector3(0, 0, side * .4f));
                Add(footNode, "RoundPad", _parts.RoundPad, new Vector3(0, .035f, 0));
                Add(footNode, "PadRim", _parts.PadRim, new Vector3(0, .07f, 0));
                break;
            case 2: // Copper pipe spare, too short: a timber block is roped under its foot.
                Add(hip, "HipJoint", _parts.PipeJoint, Vector3.Zero);
                Span(hip, "Thigh", _parts.CopperPipe, Vector3.Zero, knee);
                Add(hip, "KneeJoint", _parts.PipeJoint, knee);
                Span(hip, "KneeLashing", _parts.PipeLash, knee * .8f, knee * .9f);
                Span(kneeNode, "Shin", _parts.CopperPipe, Vector3.Zero, foot);
                Add(footNode, "PipeCap", _parts.PipeCap, new Vector3(0, .03f, 0));
                Add(footNode, "Block", _parts.Block, new Vector3(0, -spec.Ground / 2, 0), new Vector3(0, .25f, 0));
                Add(footNode, "BlockRope", _parts.BlockStrap, new Vector3(0, -spec.Ground * .45f, 0), new Vector3(0, .25f, 0));
                break;
            default: // Overlong spare: knee pokes up, wooden splints lashed along a bare iron thigh.
                Add(hip, "HipJoint", _parts.KneeBolt, Vector3.Zero, alongZ);
                Span(hip, "Thigh", _parts.IronBeam, Vector3.Zero, knee);
                for (int splint = -1; splint <= 1; splint += 2)
                    Span(hip, splint < 0 ? "FrontSplint" : "BackSplint", _parts.Plank,
                        knee * .1f + new Vector3(0, 0, splint * .14f), knee * .9f + new Vector3(0, 0, splint * .14f));
                for (int lash = 0; lash < 3; lash++)
                {
                    float t = .25f + lash * .25f;
                    Span(hip, "Lashing" + lash, _parts.Lash, knee * (t - .025f), knee * (t + .025f));
                }
                Add(hip, "KneeJoint", _parts.KneeBolt, knee, alongZ);
                Span(kneeNode, "Shin", _parts.Beam, Vector3.Zero, foot);
                Add(footNode, "FlatPad", _parts.FlatPad, new Vector3(0, .03f, 0), new Vector3(0, .35f, 0));
                Add(footNode, "PadBolt0", _parts.PadBolt, new Vector3(-.1f, .065f, .05f));
                Add(footNode, "PadBolt1", _parts.PadBolt, new Vector3(.1f, .065f, -.05f));
                break;
        }
    }

    // Foot height below the hip that rests it on the ground (or on its block) despite the chassis tilt.
    private static float FootHeight(Vector3 hip, float yaw, float outward, float ground)
    {
        Basis tilt = Basis.FromEuler(ChassisTilt), leg = tilt * new Basis(Vector3.Up, yaw);
        float hipHeight = ChassisHeight + (tilt * hip).Y;
        return (ground - hipHeight - (leg * new Vector3(outward, 0, 0)).Y) / (leg * Vector3.Up).Y;
    }

    public override void _Process(double delta)
    {
        if (_unit.IsDying)
        {
            foreach (MeshInstance3D puff in _smoke) puff.Visible = false;
            SetProcess(false);
            return;
        }
        SyncSelectionFacing();
        float dt = (float)delta;
        _time += dt;
        Vector3 position = _unit.GlobalPosition;
        float distance = _positionKnown ? position.DistanceTo(_lastPosition) : 0;
        _positionKnown = true;
        _lastPosition = position;
        bool walking = dt > 0 && distance < 1 && _unit.State.Activity != UnitActivity.Stun;
        _walk = Mathf.MoveToward(_walk, walking ? Mathf.Clamp(distance / dt / 2.5f, 0, 1) : 0, dt * 5);
        if (distance < 1) _stride += distance * 4.4f;
        // Each leg keeps its own phase and stride, so lifts never balance and the hull lurches.
        float bob = Mathf.Sin(_time * 11) * .003f, roll = 0, pitch = 0;
        // The bent leg twitches now and then even while standing.
        float twitch = Mathf.Max(0, Mathf.Sin(_time * 1.3f) - .9f) * 10 * Mathf.Sin(_time * 40) * .05f;
        for (int i = 0; i < _legs.Length; i++)
        {
            Leg leg = _legs[i];
            float phase = _stride + leg.Phase;
            float lift = Mathf.Max(0, Mathf.Sin(phase)) * _walk;
            float forward = -Mathf.Cos(phase) * .2f * leg.Stride * _walk;
            leg.Hip.Rotation = new Vector3(0, leg.Yaw + leg.Side * forward, leg.Side * lift * .3f);
            leg.Knee.Rotation = new Vector3(0, 0, leg.Side * (lift * .25f + (i == 1 ? twitch : 0)));
            bob -= lift * .025f;
            roll -= leg.Side * lift * .035f;
            pitch += leg.End * lift * .03f;
        }
        _chassis.Position = new Vector3(0, ChassisHeight + bob, 0);
        _chassis.Rotation = ChassisTilt + new Vector3(pitch, 0, roll);
        // The ram pumps with the stride and jabs once in a while when idle.
        float idle = (_time % 2.6f) / 2.6f;
        float jab = idle < .08f ? Mathf.Sin(idle / .08f * Mathf.Pi) * (1 - _walk) : 0;
        _ram.Position = _ramRest + Vector3.Forward * ((Mathf.Sin(_stride * .5f) * .5f + .5f) * .06f * _walk + jab * .07f);
        // The stoker turns from the coal bin to the firebox and dips at both ends; faster on the march.
        float shovel = (_time * (.5f + .4f * _walk)) % 1 * Mathf.Tau;
        _stokerTorso.Rotation = new Vector3(-.12f - .18f * Mathf.Cos(shovel * 2), -.5f - .5f * Mathf.Cos(shovel), 0);
        float puffRate = .45f + .5f * _walk;
        for (int i = 0; i < _smoke.Length; i++)
        {
            float t = (_time * puffRate + i / (float)_smoke.Length) % 1;
            _smoke[i].GlobalPosition = _chimneyTop.GlobalPosition + new Vector3(Mathf.Sin(t * 6 + i) * .06f, t * 1.1f, 0);
            _smoke[i].Scale = Vector3.One * (.16f + t * .34f);
            _smoke[i].Transparency = .3f + t * .7f;
        }
    }

    private void SyncSelectionFacing()
    {
        // The long pick capsule and selection ellipse follow the same heading as the walker.
        _selectionArea.Rotation = new Vector3(0, Rotation.Y, 0);
        _selectionRing.Rotation = new Vector3(0, Rotation.Y, 0);
    }

    private static MeshInstance3D Add(Node3D parent, string name, Mesh mesh, Vector3 position, Vector3 rotation = default)
    {
        var instance = new MeshInstance3D { Name = name, Mesh = mesh, Position = position, Rotation = rotation };
        parent.AddChild(instance);
        return instance;
    }

    // Stretches a one-unit span mesh along its local Y from one joint to the other.
    private static MeshInstance3D Span(Node3D parent, string name, Mesh mesh, Vector3 from, Vector3 to)
    {
        Vector3 along = to - from, axis = along.Normalized();
        Vector3 side = Mathf.Abs(axis.Dot(Vector3.Back)) > .99f ? Vector3.Right : axis.Cross(Vector3.Back).Normalized();
        Vector3 depth = side.Cross(axis).Normalized();
        var instance = new MeshInstance3D { Name = name, Mesh = mesh, Transform = new Transform3D(new Basis(side, along, depth), (from + to) / 2) };
        parent.AddChild(instance);
        return instance;
    }

    private static StandardMaterial3D Material(string color, string name = "", float metallic = 0, float roughness = .85f) =>
        new() { ResourceName = name, AlbedoColor = new Color(color), Roughness = roughness, Metallic = metallic };

    private static BoxMesh Box(float x, float y, float z, Material material) => new() { Size = new Vector3(x, y, z), Material = material };

    private static SphereMesh Sphere(float radius, Material material) =>
        new() { Radius = radius, Height = radius * 2, RadialSegments = 8, Rings = 4, Material = material };

    private static CylinderMesh Cylinder(float top, float bottom, float height, Material material) =>
        new() { TopRadius = top, BottomRadius = bottom, Height = height, RadialSegments = 10, Rings = 1, Material = material };
}
