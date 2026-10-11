using Godot;

// A compact procedural placeholder. All parts remain editable primitives.
public partial class MarshalRig : Node3D
{
    public override void _Ready()
    {
        var steel = Mat("6c8191", .6f);
        var dark = Mat("263240");
        var gold = Mat("d1ac65", .5f);
        var cloth = Mat("87adcb"); cloth.ResourceName = "team_cloth";
        var skin = Mat("d5af86");
        var body = Pivot(this, "Body", new(0, 1.65f, 0));
        Box(body, "Breastplate", new(.83f, .88f, .48f), Vector3.Zero, steel);
        Box(body, "Tabard", new(.56f, .9f, .035f), new(0, -.08f, -.265f), cloth);
        Box(body, "Belt", new(.88f, .12f, .54f), new(0, -.34f, 0), dark);
        Box(body, "Buckle", new(.18f, .16f, .055f), new(0, -.34f, -.30f), gold);
        Box(body, "Cloak", new(.9f, 1.22f, .065f), new(0, -.08f, .35f), cloth);
        var head = Pivot(body, "Head", new(0, .68f, 0));
        Mesh(head, "Face", new SphereMesh { Radius=.26f, Height=.5f, RadialSegments=12, Rings=6 }, Vector3.Zero, skin);
        Mesh(head, "Helm", new CylinderMesh { TopRadius=.24f, BottomRadius=.29f, Height=.3f, RadialSegments=12 }, new(0,.18f,0), steel);
        Box(head, "Brow", new(.56f,.075f,.1f), new(0,.05f,-.22f), gold);
        Box(head, "Plume", new(.1f,.38f,.44f), new(0,.42f,.02f), cloth);
        foreach (int side in new[] { -1, 1 })
        {
            var leg = Pivot(this, side < 0 ? "LeftLeg" : "RightLeg", new(side*.23f,1.1f,0));
            Box(leg, "Greave", new(.31f,.75f,.34f), new(0,-.39f,0), steel);
            Box(leg, "Boot", new(.35f,.22f,.53f), new(0,-.98f,-.08f), dark);
            var arm = Pivot(this, side < 0 ? "LeftArm" : "RightArm", new(side*.57f,1.95f,0));
            Box(arm, "Pauldron", new(.39f,.3f,.51f), Vector3.Zero, gold);
            Box(arm, "Sleeve", new(.24f,.66f,.27f), new(0,-.42f,0), steel);
            if (side < 0)
            {
                Box(arm, "ShieldRim", new(.57f,.92f,.13f), new(-.05f,-.45f,-.29f), gold);
                Box(arm, "Shield", new(.48f,.81f,.15f), new(-.05f,-.45f,-.32f), cloth);
                Box(arm, "Insignia", new(.09f,.47f,.04f), new(-.05f,-.45f,-.42f), gold);
            }
            else
            {
                Box(arm, "Sword", new(.14f,.075f,1.05f), new(0,-.62f,-.57f), steel);
                Box(arm, "Guard", new(.4f,.08f,.12f), new(0,-.62f,-.08f), gold);
            }
        }
        var flag = Pivot(this, "Standard", new(.35f,0,.39f));
        Mesh(flag, "Pole", new CylinderMesh { TopRadius=.035f,BottomRadius=.035f,Height=3.1f,RadialSegments=8 }, new(0,1.8f,0), gold);
        Box(flag,"Banner",new(.68f,.66f,.05f),new(.34f,2.86f,0),cloth);
        Box(flag,"Stripe",new(.12f,.53f,.06f),new(.34f,2.88f,-.015f),gold);
    }
    private static StandardMaterial3D Mat(string color, float metal = 0)
        => new() { AlbedoColor=new Color(color), Roughness=.76f, Metallic=metal };
    private static Node3D Pivot(Node parent, string name, Vector3 pos)
    { var node=new Node3D { Name=name,Position=pos }; parent.AddChild(node); return node; }
    private static void Box(Node parent, string name, Vector3 size, Vector3 pos, Material material)
        => Mesh(parent,name,new BoxMesh { Size=size },pos,material);
    private static void Mesh(Node parent,string name,Mesh mesh,Vector3 pos,Material material)
        => parent.AddChild(new MeshInstance3D { Name=name,Mesh=mesh,Position=pos,MaterialOverride=material });
}
