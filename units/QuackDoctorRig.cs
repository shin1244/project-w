using Godot;

// 돌팔이 의사: a back-alley chemist with no license. Battered top hat with a doctor's head
// mirror, green-glass goggles and a linen mask, a canvas frock coat under an oil-stained
// leather apron, a vial bandolier, a numbered doctor's bag, and an oversized brass syringe
// fed by a copper tank on the back (teal dose in glass, docs/art-direction.md). Grime stays
// on hems, cuffs and the apron; team colour is the armband and sash. QuackDoctorAnimation
// moves the limbs; HealingBeam starts at BeamOrigin on the needle.
public partial class QuackDoctorRig : Node3D
{
    public override void _Ready()
    {
        var coat = Mat("b9a784");
        var apron = Mat("6b4a33");
        var grime = Mat("2a2622");
        var ash = Mat("7d7a74");
        var cloth = Mat("8dbde3"); cloth.ResourceName = "team_cloth";
        var skin = Mat("bd9370");
        var linen = Mat("d8cdac");
        var felt = Mat("2b2826");
        var leather = Mat("4a3426");
        var trousers = Mat("4b4a44");
        var brass = Metal("a88349", .55f, .45f);
        var copper = Metal("b0673e", .5f, .45f);
        var steel = Metal("8f979c", .8f, .25f);
        var iron = Metal("3b434b", .4f, .6f);
        var lens = Metal("27413c", .3f, .3f);
        var dose = Mat("5fe3d0"); dose.EmissionEnabled = true; dose.Emission = new Color("5fe3d0"); dose.EmissionEnergyMultiplier = .8f;
        var amber = Mat("c98a2b");
        var tonic = Mat("6f9a54");

        foreach (int side in new[] { -1, 1 })
        {
            var leg = Pivot(this, side < 0 ? "LeftLeg" : "RightLeg", new(side * .17f, .8f, 0));
            Box(leg, "Trousers", new(.21f, .6f, .25f), new(0, -.3f, 0), trousers);
            // Mismatched boots: a worn leather one and a brass-capped work boot.
            Box(leg, "Boot", new(.23f, .24f, .34f), new(0, -.68f, -.04f), side < 0 ? leather : iron);
            if (side > 0) Box(leg, "ToeCap", new(.24f, .1f, .1f), new(0, -.74f, -.2f), brass);
            Box(leg, "KneeGrime", new(.22f, .1f, .02f), new(0, -.36f, -.13f), grime);
        }

        var body = Pivot(this, "Body", new(0, 1.2f, 0));
        Box(body, "Coat", new(.6f, .74f, .36f), new(0, .02f, 0), coat);
        Mesh(body, "CoatTail", new CylinderMesh { TopRadius = .31f, BottomRadius = .43f, Height = .58f, RadialSegments = 8 }, new(0, -.56f, .02f), coat);
        Box(body, "HemAsh", new(.8f, .06f, .2f), new(0, -.83f, .28f), ash);
        Box(body, "Apron", new(.44f, 1.02f, .04f), new(0, -.2f, -.2f), apron);
        Box(body, "ApronStain", new(.14f, .18f, .02f), new(.09f, -.38f, -.225f), grime);
        Box(body, "ApronSplash", new(.09f, .09f, .02f), new(-.12f, -.1f, -.225f), grime);
        Box(body, "Sash", new(.64f, .12f, .4f), new(0, -.27f, 0), cloth);
        Box(body, "Collar", new(.4f, .1f, .3f), new(0, .4f, 0), linen);
        // Stencilled number tag: hired by the day like everyone else.
        Box(body, "NumberTag", new(.13f, .1f, .02f), new(-.17f, .22f, -.19f), linen);
        Box(body, "TagNumber", new(.08f, .025f, .01f), new(-.17f, .22f, -.201f), grime);
        // Bandolier of tonics across the chest, buckled on the left hip.
        var strap = Box(body, "Bandolier", new(.07f, .9f, .05f), new(0, .02f, -.19f), leather);
        strap.Rotation = new(0, 0, .7f);
        for (int i = 0; i < 4; i++)
        {
            Vector3 at = new(-.19f + i * .12f, -.17f + i * .12f, -.235f);
            Mesh(body, $"Vial{i}", new CylinderMesh { TopRadius = .03f, BottomRadius = .03f, Height = .11f, RadialSegments = 6 }, at,
                i == 1 ? dose : i == 2 ? amber : tonic);
            Mesh(body, $"Cork{i}", new CylinderMesh { TopRadius = .022f, BottomRadius = .022f, Height = .03f, RadialSegments = 6 }, at + new Vector3(0, .07f, 0), leather);
        }
        // Copper dose tank on the back with its gauge; the hose climbs over the right shoulder.
        Mesh(body, "Tank", new CylinderMesh { TopRadius = .13f, BottomRadius = .13f, Height = .5f, RadialSegments = 10 }, new(.04f, .08f, .27f), copper);
        foreach (float y in new[] { -.1f, .26f })
            Mesh(body, "TankBand", new CylinderMesh { TopRadius = .14f, BottomRadius = .14f, Height = .04f, RadialSegments = 10 }, new(.04f, y, .27f), brass);
        Mesh(body, "TankCap", new SphereMesh { Radius = .13f, Height = .13f, RadialSegments = 10, Rings = 4 }, new(.04f, .33f, .27f), copper);
        var gauge = Mesh(body, "Gauge", new CylinderMesh { TopRadius = .06f, BottomRadius = .06f, Height = .03f, RadialSegments = 10 }, new(.17f, .12f, .32f), brass);
        gauge.Rotation = new(0, 0, Mathf.Pi / 2);
        Mesh(body, "GaugeFace", new CylinderMesh { TopRadius = .045f, BottomRadius = .045f, Height = .01f, RadialSegments = 10 }, new(.19f, .12f, .32f), linen)
            .Rotation = new(0, 0, Mathf.Pi / 2);
        Pipe(body, "Hose", new(.1f, .38f, .26f), new(.36f, .46f, .02f), .03f, copper);

        var head = Pivot(body, "Head", new(0, .6f, 0));
        Mesh(head, "Face", new SphereMesh { Radius = .19f, Height = .4f, RadialSegments = 10, Rings = 6 }, new(0, .02f, 0), skin);
        Box(head, "Mask", new(.3f, .14f, .1f), new(0, -.07f, -.15f), linen);
        Box(head, "MaskStain", new(.08f, .05f, .02f), new(.07f, -.1f, -.205f), ash);
        Box(head, "Strap", new(.42f, .05f, .4f), new(0, .08f, 0), leather);
        foreach (int side in new[] { -1, 1 })
        {
            Mesh(head, "GoggleRim", new CylinderMesh { TopRadius = .06f, BottomRadius = .06f, Height = .05f, RadialSegments = 10 }, new(side * .075f, .07f, -.17f), brass)
                .Rotation = new(Mathf.Pi / 2, 0, 0);
            Mesh(head, "GoggleLens", new CylinderMesh { TopRadius = .045f, BottomRadius = .045f, Height = .02f, RadialSegments = 10 }, new(side * .075f, .07f, -.2f), lens)
                .Rotation = new(Mathf.Pi / 2, 0, 0);
            Box(head, "Tuft", new(.06f, .1f, .14f), new(side * .19f, .02f, .03f), Mat("44372d"));
        }
        // The doctor's head mirror: a bright steel disc clipped above the goggles.
        var mirror = Pivot(head, "HeadMirror", new(-.06f, .21f, -.16f));
        mirror.Rotation = new(Mathf.Pi / 2 - .35f, 0, 0);
        Mesh(mirror, "MirrorRim", new CylinderMesh { TopRadius = .1f, BottomRadius = .1f, Height = .025f, RadialSegments = 14 }, Vector3.Zero, brass);
        Mesh(mirror, "Mirror", new CylinderMesh { TopRadius = .08f, BottomRadius = .08f, Height = .03f, RadialSegments = 14 }, Vector3.Zero, steel);
        // A crooked, dented stovepipe hat: the tall silhouette the RTS camera reads.
        var hat = Pivot(head, "Hat", new(.02f, .2f, .02f));
        hat.Rotation = new(-.06f, 0, -.14f);
        Mesh(hat, "Brim", new CylinderMesh { TopRadius = .27f, BottomRadius = .27f, Height = .03f, RadialSegments = 14 }, Vector3.Zero, felt);
        Mesh(hat, "Crown", new CylinderMesh { TopRadius = .19f, BottomRadius = .16f, Height = .42f, RadialSegments = 12 }, new(0, .22f, 0), felt);
        Mesh(hat, "HatBand", new CylinderMesh { TopRadius = .165f, BottomRadius = .165f, Height = .06f, RadialSegments = 12 }, new(0, .05f, 0), brass);
        Box(hat, "Dent", new(.12f, .08f, .02f), new(.08f, .3f, -.17f), ash);

        foreach (int side in new[] { -1, 1 })
        {
            var arm = Pivot(this, side < 0 ? "LeftArm" : "RightArm", new(side * .4f, 1.55f, 0));
            Box(arm, "Sleeve", new(.2f, .6f, .24f), new(0, -.27f, 0), coat);
            Box(arm, "Cuff", new(.22f, .08f, .26f), new(0, -.55f, 0), grime);
            Box(arm, "Hand", new(.15f, .13f, .15f), new(0, -.65f, 0), skin);
            if (side < 0)
            {
                Box(arm, "Armband", new(.22f, .1f, .26f), new(0, -.18f, 0), cloth);
                // Battered doctor's bag with a brass clasp and its own number.
                Box(arm, "Bag", new(.3f, .22f, .18f), new(-.02f, -.82f, -.03f), leather);
                Box(arm, "BagTop", new(.32f, .05f, .2f), new(-.02f, -.7f, -.03f), apron);
                Box(arm, "Clasp", new(.07f, .06f, .02f), new(-.02f, -.74f, -.13f), brass);
                Box(arm, "BagTag", new(.1f, .06f, .01f), new(.06f, -.86f, -.125f), linen);
            }
            else
            {
                // Oversized brass syringe gripped needle-down along the arm, so raising the arm
                // points it at the patient; the glass barrel is full of teal dose.
                var syringe = Pivot(arm, "Syringe", new(.02f, -.66f, -.04f));
                syringe.Rotation = new(Mathf.Pi, 0, 0);
                Mesh(syringe, "Barrel", new CylinderMesh { TopRadius = .06f, BottomRadius = .06f, Height = .4f, RadialSegments = 10 }, new(0, .12f, 0), brass);
                Mesh(syringe, "Glass", new CylinderMesh { TopRadius = .045f, BottomRadius = .045f, Height = .3f, RadialSegments = 10 }, new(.035f, .12f, 0), dose);
                Mesh(syringe, "Plunger", new CylinderMesh { TopRadius = .02f, BottomRadius = .02f, Height = .22f, RadialSegments = 6 }, new(0, -.17f, 0), steel);
                Mesh(syringe, "Thumb", new CylinderMesh { TopRadius = .06f, BottomRadius = .06f, Height = .02f, RadialSegments = 10 }, new(0, -.28f, 0), brass);
                Mesh(syringe, "Needle", new CylinderMesh { TopRadius = .004f, BottomRadius = .012f, Height = .2f, RadialSegments = 6 }, new(0, .42f, 0), steel);
                syringe.AddChild(new Node3D { Name = "BeamOrigin", Position = new(0, .52f, 0) });
            }
        }
    }

    private static StandardMaterial3D Mat(string color) => new() { AlbedoColor = new Color(color), Roughness = .9f };
    private static StandardMaterial3D Metal(string color, float metallic, float roughness) =>
        new() { AlbedoColor = new Color(color), Metallic = metallic, Roughness = roughness };
    private static Node3D Pivot(Node parent, string name, Vector3 position)
    {
        var node = new Node3D { Name = name, Position = position };
        parent.AddChild(node);
        return node;
    }
    private static MeshInstance3D Box(Node parent, string name, Vector3 size, Vector3 position, Material material)
        => Mesh(parent, name, new BoxMesh { Size = size }, position, material);
    private static MeshInstance3D Mesh(Node parent, string name, Mesh mesh, Vector3 position, Material material)
    {
        var node = new MeshInstance3D { Name = name, Mesh = mesh, Position = position, MaterialOverride = material };
        parent.AddChild(node);
        return node;
    }
    // A straight round pipe between two points of the same parent.
    private static void Pipe(Node parent, string name, Vector3 from, Vector3 to, float radius, Material material)
    {
        Vector3 along = to - from;
        var pipe = Mesh(parent, name, new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = along.Length(), RadialSegments = 6 },
            (from + to) / 2, material);
        pipe.Basis = new Basis(new Quaternion(Vector3.Up, along.Normalized()));
    }
}
