using Godot;

// 급유 기계: a fast three-wheeled oil cart. Lacquered iron tank with brass bands and a gilt
// serial plate (a cherished machine), team colour on the painted side plates, a copper
// chimney, a hand pump with a pressure gauge, and a copper hose crane ending in a brass
// nozzle (HoseArm turns toward the patient; HealingBeam starts at BeamOrigin). A goggled
// operator in a numbered smock sits strapped to the back seat. RefuelerVisual animates it.
public partial class RefuelerRig : Node3D
{
    public override void _Ready()
    {
        var lacquer = Metal("2f363d", .75f, .3f);
        var sooty = Metal("3b434b", .45f, .6f);
        var brass = Metal("a88349", .75f, .3f);
        var gilt = Metal("d9ae45", .85f, .2f);
        var copper = Metal("b0673e", .7f, .35f);
        var steel = Metal("8f979c", .8f, .25f);
        var paint = Metal("8dbde3", .3f, .45f); paint.ResourceName = "team_cloth";
        var oil = Mat("2a2622");
        var rubber = Mat("1f2124");
        var smock = Mat("b9a784");
        var skin = Mat("bd9370");
        var lens = Metal("27413c", .3f, .3f);
        var face = Mat("d8cdac");

        var chassis = Pivot(this, "Chassis", Vector3.Zero);
        Box(chassis, "Frame", new(.62f, .14f, 1.12f), new(0, .42f, .02f), sooty);
        Box(chassis, "DripTray", new(.5f, .03f, .3f), new(0, .34f, -.38f), oil);
        // Wheels: big rear pair on one axle, a small steering wheel up front.
        foreach (int side in new[] { -1, 1 })
            Wheel(chassis, side < 0 ? "WheelLeft" : "WheelRight", new(side * .4f, .3f, .32f), .3f, .1f, rubber, brass);
        Box(chassis, "Fork", new(.08f, .3f, .06f), new(0, .32f, -.5f), sooty);
        Wheel(chassis, "WheelFront", new(0, .18f, -.5f), .18f, .08f, rubber, brass);

        var tank = Pivot(chassis, "Tank", new(0, .8f, .02f));
        Mesh(tank, "Shell", new CylinderMesh { TopRadius = .31f, BottomRadius = .31f, Height = .86f, RadialSegments = 16 }, Vector3.Zero, lacquer)
            .Rotation = new(Mathf.Pi / 2, 0, 0);
        foreach (int end in new[] { -1, 1 })
            Mesh(tank, "Dome", new SphereMesh { Radius = .31f, Height = .3f, RadialSegments = 16, Rings = 4 }, new(0, 0, end * .43f), lacquer)
                .Rotation = new(end * Mathf.Pi / 2, 0, 0);
        foreach (float z in new[] { -.3f, 0, .3f })
            Mesh(tank, "Band", new CylinderMesh { TopRadius = .325f, BottomRadius = .325f, Height = .05f, RadialSegments = 16 }, new(0, 0, z), brass)
                .Rotation = new(Mathf.Pi / 2, 0, 0);
        foreach (int side in new[] { -1, 1 })
        {
            Box(tank, "SidePlate", new(.03f, .26f, .4f), new(side * .31f, -.02f, .15f), paint);
            Box(tank, "Rivets", new(.035f, .03f, .42f), new(side * .312f, .12f, .15f), brass);
        }
        Box(tank, "SerialPlate", new(.035f, .1f, .18f), new(.315f, -.02f, -.18f), gilt);
        Mesh(tank, "Filler", new CylinderMesh { TopRadius = .08f, BottomRadius = .1f, Height = .1f, RadialSegments = 10 }, new(0, .33f, -.2f), brass);
        var gauge = Mesh(tank, "Gauge", new CylinderMesh { TopRadius = .09f, BottomRadius = .09f, Height = .04f, RadialSegments = 12 }, new(.16f, .3f, .05f), brass);
        gauge.Rotation = new(-Mathf.Pi / 3, 0, 0);
        Mesh(tank, "GaugeFace", new CylinderMesh { TopRadius = .07f, BottomRadius = .07f, Height = .045f, RadialSegments = 12 }, new(.16f, .305f, .045f), face)
            .Rotation = new(-Mathf.Pi / 3, 0, 0);
        Box(tank, "OilStreak", new(.1f, .16f, .02f), new(-.12f, -.22f, -.42f), oil);

        // Copper chimney with a brass cap; RefuelerVisual puffs smoke from ChimneyTop.
        Mesh(chassis, "Chimney", new CylinderMesh { TopRadius = .06f, BottomRadius = .07f, Height = .55f, RadialSegments = 10 }, new(-.2f, 1.3f, .32f), copper);
        Mesh(chassis, "ChimneyCap", new CylinderMesh { TopRadius = .1f, BottomRadius = .07f, Height = .08f, RadialSegments = 10 }, new(-.2f, 1.6f, .32f), brass);
        chassis.AddChild(new Node3D { Name = "ChimneyTop", Position = new(-.2f, 1.66f, .32f) });

        // Hand pump: a steel rod and a lever the operator works while refuelling.
        Mesh(chassis, "PumpBody", new CylinderMesh { TopRadius = .06f, BottomRadius = .06f, Height = .3f, RadialSegments = 10 }, new(.18f, 1.2f, .3f), brass);
        var lever = Pivot(chassis, "PumpLever", new(.18f, 1.36f, .3f));
        Box(lever, "Handle", new(.04f, .04f, .42f), new(0, 0, .14f), steel);

        // Hose crane: a brass turret ring, a copper boom and a drooping nozzle arm.
        Mesh(chassis, "Turret", new CylinderMesh { TopRadius = .14f, BottomRadius = .16f, Height = .08f, RadialSegments = 12 }, new(0, 1.12f, -.22f), brass);
        var arm = Pivot(chassis, "HoseArm", new(0, 1.16f, -.22f));
        var boom = Pivot(arm, "Boom", Vector3.Zero);
        boom.Rotation = new(-.75f, 0, 0);
        Mesh(boom, "BoomPipe", new CylinderMesh { TopRadius = .045f, BottomRadius = .05f, Height = .6f, RadialSegments = 8 }, new(0, .3f, 0), copper);
        var elbow = Pivot(boom, "Elbow", new(0, .6f, 0));
        Mesh(elbow, "Joint", new SphereMesh { Radius = .07f, Height = .14f, RadialSegments = 10, Rings = 5 }, Vector3.Zero, brass);
        var reach = Pivot(elbow, "Reach", Vector3.Zero);
        reach.Rotation = new(2.1f, 0, 0);
        Mesh(reach, "ReachPipe", new CylinderMesh { TopRadius = .04f, BottomRadius = .045f, Height = .45f, RadialSegments = 8 }, new(0, .22f, 0), copper);
        Mesh(reach, "Nozzle", new CylinderMesh { TopRadius = .03f, BottomRadius = .07f, Height = .16f, RadialSegments = 10 }, new(0, .52f, 0), brass);
        reach.AddChild(new Node3D { Name = "BeamOrigin", Position = new(0, .62f, 0) });

        // The operator: goggles, a numbered smock, team-colour scarf, strapped to the seat.
        var seat = Pivot(chassis, "Operator", new(.12f, .5f, .56f));
        Box(seat, "Seat", new(.3f, .06f, .28f), new(0, .02f, 0), sooty);
        Box(seat, "Torso", new(.26f, .34f, .2f), new(0, .24f, .02f), smock);
        Box(seat, "Scarf", new(.28f, .07f, .22f), new(0, .38f, .02f), paint);
        Box(seat, "Tag", new(.08f, .06f, .01f), new(-.06f, .26f, -.085f), face);
        Box(seat, "SafetyStrap", new(.3f, .04f, .24f), new(0, .14f, .02f), rubber);
        Mesh(seat, "Head", new SphereMesh { Radius = .1f, Height = .2f, RadialSegments = 10, Rings = 5 }, new(0, .5f, .02f), skin);
        Box(seat, "Goggles", new(.17f, .05f, .05f), new(0, .52f, -.07f), lens);
        Box(seat, "Cap", new(.2f, .06f, .2f), new(0, .59f, .02f), sooty);
    }

    private static void Wheel(Node parent, string name, Vector3 position, float radius, float width, Material tyre, Material hub)
    {
        var wheel = Pivot(parent, name, position);
        Mesh(wheel, "Tyre", new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = width, RadialSegments = 14 }, Vector3.Zero, tyre)
            .Rotation = new(0, 0, Mathf.Pi / 2);
        Mesh(wheel, "Hub", new CylinderMesh { TopRadius = radius * .45f, BottomRadius = radius * .45f, Height = width * 1.2f, RadialSegments = 10 }, Vector3.Zero, hub)
            .Rotation = new(0, 0, Mathf.Pi / 2);
        // Spokes show the wheel turning.
        Box(wheel, "Spoke", new(width * .5f, radius * 1.7f, .03f), Vector3.Zero, hub);
        Box(wheel, "SpokeCross", new(width * .5f, .03f, radius * 1.7f), Vector3.Zero, hub);
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
}
