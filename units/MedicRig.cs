using Godot;

// Editable low-poly placeholder: ivory robe, team-color stole and a green crystal staff.
public partial class MedicRig : Node3D
{
    public override void _Ready()
    {
        var robe = Mat("e2e0c8");
        var cloth = Mat("87adcb"); cloth.ResourceName = "team_cloth";
        var leather = Mat("453b32");
        var skin = Mat("cbaa80");
        var gold = Mat("b9a370");
        var crystal = Mat("79e4ae");
        crystal.EmissionEnabled = true; crystal.Emission = new Color("358c60");
        var body = Pivot(this, "Body", new(0, 1.23f, 0));
        Mesh(body, "Robe", new CylinderMesh { TopRadius=.25f, BottomRadius=.41f, Height=.96f, RadialSegments=8 }, new(0,-.06f,0), robe);
        Box(body, "Stole", new(.22f,.77f,.05f), new(0,.02f,-.3f), cloth);
        Box(body, "Belt", new(.66f,.1f,.57f), new(0,-.16f,0), leather);
        var head = Pivot(body, "Head", new(0,.64f,0));
        Mesh(head, "Hood", new SphereMesh { Radius=.29f, Height=.57f, RadialSegments=12, Rings=6 }, Vector3.Zero, robe);
        Box(head, "Face", new(.32f,.31f,.1f), new(0,-.03f,-.245f), skin);
        Box(head, "Band", new(.48f,.09f,.1f), new(0,.17f,-.22f), cloth);
        foreach (int side in new[] {-1,1})
        {
            var leg=Pivot(this,side<0 ? "LeftLeg" : "RightLeg",new(side*.2f,.78f,0));
            Box(leg,"Boot",new(.22f,.61f,.3f),new(0,-.36f,-.025f),leather);
            var arm=Pivot(this,side<0 ? "LeftArm" : "RightArm",new(side*.42f,1.56f,0));
            Box(arm,"Sleeve",new(.24f,.62f,.31f),new(0,-.28f,0),robe);
            Box(arm,"Cuff",new(.25f,.13f,.32f),new(0,-.55f,0),cloth);
            if (side>0)
            {
                Mesh(arm,"Staff",new CylinderMesh { TopRadius=.04f,BottomRadius=.04f,Height=1.84f,RadialSegments=8 },new(.08f,-.36f,-.2f),leather);
                Mesh(arm,"Crystal",new PrismMesh { Size=new(.3f,.52f,.25f) },new(.08f,.7f,-.2f),crystal);
                Box(arm,"Collar",new(.31f,.08f,.25f),new(.08f,.44f,-.2f),gold);
            }
            else Box(arm,"Satchel",new(.3f,.38f,.24f),new(-.01f,-.67f,-.08f),cloth);
        }
    }
    private static StandardMaterial3D Mat(string color) => new() { AlbedoColor=new Color(color),Roughness=.9f };
    private static Node3D Pivot(Node parent,string name,Vector3 position)
    { var node=new Node3D { Name=name,Position=position };parent.AddChild(node);return node; }
    private static void Box(Node parent,string name,Vector3 size,Vector3 position,Material material)
        => Mesh(parent,name,new BoxMesh { Size=size },position,material);
    private static void Mesh(Node parent,string name,Mesh mesh,Vector3 position,Material material)
        => parent.AddChild(new MeshInstance3D { Name=name,Mesh=mesh,Position=position,MaterialOverride=material });
}
