using Godot;
using System.Collections.Generic;

// Shared geometry, articulated pivots, and team-marked cloth keep this native model inexpensive.
// The rig is built before Unit._Ready so TeamMaterials discovers the banner and cuffs.
[Tool]
public partial class MarshalRig : Node3D
{
    private static readonly Dictionary<string, Mesh> Geometry = new();
    private static readonly StandardMaterial3D Navy = Mat("263849"), NavyLight = Mat("435367"),
        NavyDark = Mat("172432"), Gold = Mat("c6a05e", .65f), GoldLight = Mat("efcf86", .55f),
        Steel = Mat("c9d4d7", .75f), SteelDark = Mat("38444b", .65f), Leather = Mat("322b29"),
        Skin = Mat("bd8c6c"), SkinLight = Mat("d5a783"), Hair = Mat("3f3935"),
        Ivory = Mat("ebe1c4"), Team = Mat("7798b3", 0, "team_cloth"), Black = Mat("101820");

    public override void _Ready()
    {
        if (HasNode("Body")) return;
        Node3D body = Pivot(this, "Body", new Vector3(0, 1.35f, 0));
        Add(body, "CoatChest", Hull("chest", .48f, .35f, .29f, .22f, .73f, Navy), new Vector3(0, .37f, 0));
        Add(body, "Waist", Hull("waist", .35f, .37f, .235f, .25f, .27f, NavyDark), new Vector3(0, -.10f, 0));
        Box(body, "Belt", new Vector3(.78f, .12f, .52f), new Vector3(0, -.07f, 0), Leather);
        Box(body, "Buckle", new Vector3(.18f, .135f, .065f), new Vector3(0, -.065f, -.286f), Gold);
        Box(body, "BuckleInset", new Vector3(.105f, .063f, .025f), new Vector3(0, -.06f, -.327f), NavyDark);
        for (int side = -1; side <= 1; side += 2)
        {
            string suffix = side < 0 ? "Left" : "Right";
            Node3D tail = Pivot(body, suffix + "Tail", new Vector3(side * .25f, -.14f, .12f));
            Add(tail, "CoatTail", Slab("tail", new[] { new Vector2(-.21f, 0), new Vector2(.21f, 0),
                new Vector2(.27f, -.78f), new Vector2(-.26f, -.68f) }, .12f, Navy), new Vector3(0, 0, .05f));
            Box(tail, "TailSeam", new Vector3(.034f, .69f, .015f), new Vector3(side * .2f, -.35f, -.017f), Gold);
            Node3D leg = Pivot(this, suffix + "Leg", new Vector3(side * .235f, 1.14f, 0));
            Add(leg, "Trouser", Hull("trouser", .15f, .125f, .17f, .14f, .53f, NavyLight), new Vector3(0, -.22f, 0));
            Box(leg, "TrouserStripe", new Vector3(.035f, .48f, .045f), new Vector3(side * .151f, -.2f, -.075f), Gold);
            Add(leg, "TallBoot", Hull("boot", .15f, .12f, .17f, .15f, .46f, Leather), new Vector3(0, -.70f, .015f));
            Add(leg, "BootCuff", Hull("bootcuff", .17f, .16f, .185f, .18f, .10f, SteelDark), new Vector3(0, -.485f, .012f));
            Add(leg, "BootToe", Hull("boottoe", .13f, .16f, .25f, .23f, .18f, Leather), new Vector3(0, -1.035f, -.08f));
            Box(leg, "BootSole", new Vector3(.33f, .055f, .51f), new Vector3(0, -1.112f, -.075f), NavyDark);
            Box(leg, "BootBuckle", new Vector3(.065f, .09f, .036f), new Vector3(side * .12f, -.70f, -.156f), Gold);

            Node3D arm = Pivot(body, suffix + "Arm", new Vector3(side * .58f, .62f, 0));
            Add(arm, "Sleeve", Hull("sleeve", .2f, .155f, .21f, .155f, .43f, Navy), new Vector3(side * .025f, -.17f, 0));
            Add(arm, "Epaulet", Hull("epaulet", .225f, .235f, .245f, .26f, .13f, Gold), new Vector3(0, .055f, 0));
            Add(arm, "ShoulderPlate", Hull("shoulderplate", .175f, .21f, .195f, .23f, .10f, GoldLight), new Vector3(0, .13f, 0));
            for (int j = 0; j < 5; j++)
                Add(arm, "Fringe" + j, Rod("fringe", .022f, .022f, .17f, Gold), new Vector3(side * .22f, -.06f, (j - 2) * .085f));
            Add(arm, "Forearm", Hull("forearm", .15f, .135f, .16f, .15f, .32f, NavyLight), new Vector3(0, -.48f, -.035f));
            Add(arm, "Cuff", Hull("cuff", .16f, .16f, .175f, .175f, .13f, Team), new Vector3(0, -.62f, -.04f));
            Add(arm, "CuffBraid", Hull("cuffbraid", .166f, .166f, .18f, .18f, .033f, Gold), new Vector3(0, -.685f, -.04f));
            Add(arm, "Glove", Hull("glove", .125f, .13f, .14f, .135f, .20f, Leather), new Vector3(0, -.76f, -.04f));

            Box(body, suffix + "Lapel", new Vector3(.16f, .47f, .055f), new Vector3(side * .22f, .45f, -.29f), Gold,
                new Vector3(0, 0, side * .36f));
            for (int row = 0; row < 3; row++)
                Add(body, suffix + "Button" + row, Ball("button", .035f, GoldLight), new Vector3(side * .17f, .14f + row * .17f, -.283f));
        }
        Box(body, "ChestSash", new Vector3(.15f, .82f, .035f), new Vector3(.035f, .32f, -.323f), Team, new Vector3(0, 0, -.68f));
        Box(body, "SashEdge", new Vector3(.025f, .82f, .02f), new Vector3(.096f, .35f, -.347f), Gold, new Vector3(0, 0, -.68f));
        Box(body, "MedalRibbon", new Vector3(.10f, .14f, .018f), new Vector3(-.32f, .44f, -.318f), Ivory);
        Add(body, "Medal", Ball("medal", .07f, GoldLight), new Vector3(-.32f, .345f, -.345f), scale: new Vector3(1, 1, .35f));
        Add(body, "Neck", Rod("neck", .13f, .14f, .17f, Skin), new Vector3(0, .83f, 0));
        Box(body, "Collar", new Vector3(.47f, .16f, .38f), new Vector3(0, .76f, 0), NavyDark);
        Box(body, "Cravat", new Vector3(.12f, .21f, .035f), new Vector3(0, .73f, -.235f), Ivory);

        Node3D head = Pivot(body, "Head", new Vector3(0, 1.02f, -.025f));
        Add(head, "Face", Ball("head", .28f, Skin), new Vector3(0, .1f, 0), scale: new Vector3(.88f, 1.20f, .83f));
        Add(head, "Chin", Hull("chin", .15f, .12f, .14f, .105f, .13f, SkinLight), new Vector3(0, -.10f, -.075f));
        Add(head, "Hair", Ball("hair", .28f, Hair), new Vector3(0, .18f, .045f), scale: new Vector3(.94f, 1.03f, .83f));
        Box(head, "FaceFront", new Vector3(.32f, .22f, .08f), new Vector3(0, .09f, -.184f), SkinLight);
        Add(head, "Nose", Hull("nose", .042f, .055f, .035f, .07f, .135f, Skin), new Vector3(0, .065f, -.262f));
        for (int side = -1; side <= 1; side += 2)
        {
            Add(head, "Ear" + side, Ball("ear", .065f, Skin), new Vector3(side * .24f, .055f, -.025f), scale: new Vector3(.55f, 1, .7f));
            Box(head, "Eye" + side, new Vector3(.073f, .030f, .023f), new Vector3(side * .103f, .105f, -.237f), Black);
            Box(head, "Brow" + side, new Vector3(.112f, .038f, .038f), new Vector3(side * .098f, .155f, -.24f), Hair, new Vector3(0, 0, side * .11f));
            Box(head, "Moustache" + side, new Vector3(.112f, .046f, .046f), new Vector3(side * .066f, -.025f, -.248f), Hair, new Vector3(0, 0, side * -.16f));
        }
        Box(head, "Beard", new Vector3(.12f, .065f, .04f), new Vector3(0, -.105f, -.179f), Hair);
        Add(head, "Cap", Rod("cap", .29f, .265f, .26f, Navy), new Vector3(0, .41f, 0), scale: new Vector3(1.1f, 1, .92f));
        Add(head, "CapBand", Rod("capband", .283f, .277f, .077f, Gold), new Vector3(0, .295f, 0), scale: new Vector3(1.1f, 1, .94f));
        Add(head, "CapTop", Rod("captop", .315f, .31f, .065f, NavyLight), new Vector3(0, .556f, 0), scale: new Vector3(1.1f, 1, .92f));
        Add(head, "Visor", Ball("visor", .34f, Leather), new Vector3(0, .275f, -.16f), scale: new Vector3(1.05f, .10f, .78f));
        Add(head, "CapBadge", Slab("capbadge", new[] { new Vector2(0, .105f), new Vector2(.08f, 0),
            new Vector2(0, -.095f), new Vector2(-.08f, 0) }, .04f, GoldLight), new Vector3(0, .408f, -.283f));
        Add(head, "Plume", Ball("plume", .14f, Ivory), new Vector3(-.235f, .63f, .02f), scale: new Vector3(.63f, 1.8f, .7f));

        BuildSabre(body.GetNode<Node3D>("RightArm"));
        BuildPistol(body.GetNode<Node3D>("LeftArm"));
        BuildBanner(body);
    }

    private static void BuildSabre(Node3D arm)
    {
        Node3D sword = Pivot(arm, "Sabre", new Vector3(0, -.78f, -.08f));
        sword.Rotation = new Vector3(-1.92f, .05f, -.12f);
        Add(sword, "Grip", Rod("grip", .056f, .061f, .26f, Leather), new Vector3(0, .02f, 0));
        Add(sword, "Pommel", Ball("pommel", .08f, Gold), new Vector3(0, -.13f, 0));
        Box(sword, "Guard", new Vector3(.35f, .06f, .15f), new Vector3(.06f, .15f, 0), Gold);
        Box(sword, "KnuckleBow", new Vector3(.045f, .29f, .05f), new Vector3(.195f, .02f, 0), Gold, new Vector3(0, 0, -.23f));
        Add(sword, "Blade", Slab("blade", new[] { new Vector2(-.067f, .18f), new Vector2(.065f, .18f),
            new Vector2(.10f, .85f), new Vector2(.23f, 1.37f), new Vector2(.12f, 1.22f), new Vector2(-.018f, .88f) }, .032f, Steel), Vector3.Zero);
        Add(sword, "BladeFuller", Slab("fuller", new[] { new Vector2(.018f, .22f), new Vector2(.038f, .22f),
            new Vector2(.067f, .83f), new Vector2(.153f, 1.16f), new Vector2(.054f, .85f) }, .008f, GoldLight), new Vector3(0, 0, -.019f));
    }

    private static void BuildPistol(Node3D arm)
    {
        Node3D pistol = Pivot(arm, "Pistol", new Vector3(0, -.76f, -.09f));
        Box(pistol, "Grip", new Vector3(.115f, .27f, .16f), new Vector3(0, -.08f, .07f), Leather, new Vector3(-.23f, 0, 0));
        Box(pistol, "GripCap", new Vector3(.135f, .05f, .16f), new Vector3(0, -.213f, .1f), Gold);
        Box(pistol, "Lock", new Vector3(.15f, .15f, .28f), new Vector3(0, .09f, -.08f), Gold);
        Add(pistol, "Barrel", Rod("barrel", .065f, .075f, .49f, SteelDark), new Vector3(0, .12f, -.40f), new Vector3(Mathf.Pi / 2, 0, 0));
        Add(pistol, "MuzzleBand", Rod("muzzleband", .078f, .078f, .07f, Gold), new Vector3(0, .12f, -.635f), new Vector3(Mathf.Pi / 2, 0, 0));
        Add(pistol, "MuzzleHole", Rod("muzzlehole", .052f, .052f, .008f, Black), new Vector3(0, .12f, -.676f), new Vector3(Mathf.Pi / 2, 0, 0));
        Box(pistol, "Sight", new Vector3(.03f, .05f, .075f), new Vector3(0, .21f, -.55f), GoldLight);
        Box(pistol, "Hammer", new Vector3(.055f, .11f, .055f), new Vector3(.079f, .21f, .03f), Steel);
        Box(pistol, "TriggerGuard", new Vector3(.035f, .10f, .23f), new Vector3(.071f, -.015f, -.13f), Gold);
        Pivot(pistol, "Muzzle", new Vector3(0, .12f, -.71f));
    }

    private static void BuildBanner(Node3D body)
    {
        Node3D banner = Pivot(body, "Banner", new Vector3(-.40f, .06f, .34f));
        Add(banner, "Pole", Rod("pole", .035f, .039f, 2.5f, Gold), new Vector3(0, .42f, 0));
        Add(banner, "Spear", Slab("speartip", new[] { new Vector2(0, .22f), new Vector2(.10f, 0),
            new Vector2(0, -.10f), new Vector2(-.10f, 0) }, .06f, GoldLight), new Vector3(0, 1.8f, 0));
        Box(banner, "Crossbar", new Vector3(.81f, .05f, .05f), new Vector3(.36f, 1.60f, 0), Gold);
        Node3D cloth = Pivot(banner, "Cloth", new Vector3(.06f, 1.55f, .015f));
        Add(cloth, "Pennant", Slab("pennant", new[] { new Vector2(0, 0), new Vector2(.65f, 0),
            new Vector2(.65f, -.89f), new Vector2(.32f, -.71f), new Vector2(0, -.89f) }, .035f, Team), Vector3.Zero);
        Box(cloth, "TopBraid", new Vector3(.65f, .035f, .047f), new Vector3(.325f, -.025f, 0), GoldLight);
        for (int side = 0; side < 2; side++)
            Box(cloth, "SideBraid" + side, new Vector3(.026f, .86f, .047f), new Vector3(side * .625f + .012f, -.45f, 0), Gold);
        // Raised front and reverse emblems make the flag readable from the battlefield camera.
        for (int side = -1; side <= 1; side += 2)
        {
            Add(cloth, "Emblem" + side, Slab("bannercrest", new[] { new Vector2(0, .20f), new Vector2(.18f, .02f),
                new Vector2(.09f, -.14f), new Vector2(0, -.22f), new Vector2(-.09f, -.14f), new Vector2(-.18f, .02f) }, .02f, GoldLight),
                new Vector3(.325f, -.37f, side * .032f));
            Box(cloth, "CrestStripe" + side, new Vector3(.045f, .21f, .018f), new Vector3(.325f, -.35f, side * .053f), NavyDark);
        }
        Box(body, "BannerHarness", new Vector3(.19f, .62f, .09f), new Vector3(-.33f, .13f, .325f), Leather, new Vector3(0, 0, .18f));
    }

    private static StandardMaterial3D Mat(string color, float metal = 0, string name = "") =>
        new() { AlbedoColor = new Color(color), Metallic = metal, Roughness = metal > 0 ? .38f : .85f, ResourceName = name };
    private static Node3D Pivot(Node3D parent, string name, Vector3 position)
    { var node = new Node3D { Name = name, Position = position }; parent.AddChild(node); return node; }
    private static MeshInstance3D Add(Node3D parent, string name, Mesh mesh, Vector3 position, Vector3 rotation = default, Vector3? scale = null)
    { var node = new MeshInstance3D { Name = name, Mesh = mesh, Position = position, Rotation = rotation, Scale = scale ?? Vector3.One }; parent.AddChild(node); return node; }
    private static void Box(Node3D parent, string name, Vector3 size, Vector3 pos, Material mat, Vector3 rotation = default)
    {
        string key = $"box:{size}:{mat.GetInstanceId()}";
        if (!Geometry.TryGetValue(key, out Mesh mesh)) Geometry[key] = mesh = new BoxMesh { Size = size, Material = mat };
        Add(parent, name, mesh, pos, rotation);
    }
    private static Mesh Rod(string key, float top, float bottom, float height, Material mat)
    {
        if (!Geometry.TryGetValue(key, out Mesh mesh)) Geometry[key] = mesh = new CylinderMesh {
            TopRadius = top, BottomRadius = bottom, Height = height, RadialSegments = 10, Rings = 1, Material = mat };
        return mesh;
    }
    private static Mesh Ball(string key, float radius, Material mat)
    {
        if (!Geometry.TryGetValue(key, out Mesh mesh)) Geometry[key] = mesh = new SphereMesh {
            Radius = radius, Height = radius * 2, RadialSegments = 12, Rings = 6, Material = mat };
        return mesh;
    }
    private static Mesh Hull(string key, float topX, float bottomX, float topZ, float bottomZ, float height, Material mat)
    {
        if (Geometry.TryGetValue(key, out Mesh mesh)) return mesh;
        using var surface = new SurfaceTool(); surface.Begin(Mesh.PrimitiveType.Triangles);
        Vector2[] outline = { new(-.7f,-1),new(.7f,-1),new(1,-.65f),new(1,.65f),new(.7f,1),new(-.7f,1),new(-1,.65f),new(-1,-.65f) };
        for (int i = 0; i < outline.Length; i++)
        {
            Vector2 a = outline[i], b = outline[(i + 1) % outline.Length];
            Vector3 ta = new(a.X * topX, height / 2, a.Y * topZ), tb = new(b.X * topX, height / 2, b.Y * topZ),
                ba = new(a.X * bottomX, -height / 2, a.Y * bottomZ), bb = new(b.X * bottomX, -height / 2, b.Y * bottomZ);
            Triangle(surface, ta, ba, tb); Triangle(surface, tb, ba, bb);
            Triangle(surface, new Vector3(0, height / 2, 0), ta, tb);
            Triangle(surface, new Vector3(0, -height / 2, 0), bb, ba);
        }
        surface.GenerateNormals(); mesh = surface.Commit(); ((ArrayMesh)mesh).SurfaceSetMaterial(0, mat);
        Geometry[key] = mesh; return mesh;
    }
    private static Mesh Slab(string key, Vector2[] points, float depth, Material mat)
    {
        if (Geometry.TryGetValue(key, out Mesh mesh)) return mesh;
        using var surface = new SurfaceTool(); surface.Begin(Mesh.PrimitiveType.Triangles);
        if (Geometry2D.IsPolygonClockwise(points)) System.Array.Reverse(points);
        int[] triangles = Geometry2D.TriangulatePolygon(points);
        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector2 a = points[triangles[i]], b = points[triangles[i + 1]], c = points[triangles[i + 2]];
            Triangle(surface, new Vector3(a.X, a.Y, -depth / 2), new Vector3(b.X, b.Y, -depth / 2), new Vector3(c.X, c.Y, -depth / 2));
            Triangle(surface, new Vector3(a.X, a.Y, depth / 2), new Vector3(c.X, c.Y, depth / 2), new Vector3(b.X, b.Y, depth / 2));
        }
        for (int i = 0; i < points.Length; i++)
        {
            Vector2 a = points[i], b = points[(i + 1) % points.Length];
            Triangle(surface, new Vector3(a.X, a.Y, -depth/2), new Vector3(b.X, b.Y, -depth/2), new Vector3(a.X, a.Y, depth/2));
            Triangle(surface, new Vector3(b.X, b.Y, -depth/2), new Vector3(b.X, b.Y, depth/2), new Vector3(a.X, a.Y, depth/2));
        }
        surface.GenerateNormals(); mesh = surface.Commit(); ((ArrayMesh)mesh).SurfaceSetMaterial(0, mat);
        Geometry[key] = mesh; return mesh;
    }
    private static void Triangle(SurfaceTool surface, Vector3 a, Vector3 b, Vector3 c)
    { surface.AddVertex(a); surface.AddVertex(b); surface.AddVertex(c); }
}
