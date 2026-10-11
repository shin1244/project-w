using Godot;

// The server sends only allied crates. Position, carrier and removal are never predicted.
public partial class ItemCrateView : Node3D
{
    public ItemCrateSnapshot Snapshot { get; private set; }
    public uint LocalTeam { get; private set; }
    public bool Available => _available && Snapshot.CarrierId == 0;
    private bool _available;
    private Area3D _pick;
    private Label3D _label;
    private MeshInstance3D _crate;
    public override void _Ready()
    {
        _crate=new MeshInstance3D { Name="Chest",Mesh=new BoxMesh { Size=new Vector3(.85f,.6f,.65f) },
            MaterialOverride=new StandardMaterial3D { AlbedoColor=new Color("94704a"),Roughness=.9f },Position=new Vector3(0,.4f,0) };
        AddChild(_crate);
        var metal=new StandardMaterial3D { AlbedoColor=new Color("e5bc69"),Metallic=.45f,Roughness=.65f };
        foreach (float x in new[] { -.27f,.27f })
            _crate.AddChild(new MeshInstance3D { Mesh=new BoxMesh { Size=new Vector3(.09f,.63f,.68f) },
                MaterialOverride=metal,Position=new Vector3(x,0,0) });
        _crate.AddChild(new MeshInstance3D { Mesh=new BoxMesh { Size=new Vector3(.17f,.2f,.06f) },
            MaterialOverride=metal,Position=new Vector3(0,0,-.36f) });
        _label=new Label3D { FontSize=26,PixelSize=.014f,OutlineSize=6,
            Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,Modulate=new Color("f8dca0"),Position=new Vector3(0,1.2f,0) };
        AddChild(_label);
        _pick=new Area3D { Name="Pick",CollisionMask=0,Monitoring=false,Monitorable=false,InputRayPickable=false };
        _pick.AddChild(new CollisionShape3D { Shape=new BoxShape3D { Size=new Vector3(1.35f,1.25f,1.25f) },Position=new Vector3(0,.625f,0) });
        AddChild(_pick);
        Refresh();
    }
    public void Apply(ItemCrateSnapshot snapshot,uint team,string name)
    {
        Snapshot=snapshot;LocalTeam=team;_available=true;
        Position=snapshot.Position;
        if (_label!=null) _label.Text=$"{name}\n{(snapshot.CarrierId==0 ? "우클릭 · 줍기 / 일꾼 배달" : "배달 중")}";
        Refresh();
    }
    private void Refresh()
    {
        if (_pick==null) return;
        _pick.CollisionLayer=Available ? TributeEventView.PickLayer : 0;
        _crate.Position=new Vector3(0,Snapshot.CarrierId==0 ? .4f : 1.55f,0);
        _label.Position=new Vector3(0,Snapshot.CarrierId==0 ? 1.2f : 2.25f,0);
    }
    public void Deactivate() { _available=false;Hide();Refresh(); }
}
