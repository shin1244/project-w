using Godot;

// Only a landmark: healing, damage and shop range remain server-authoritative.
public partial class SpawnZoneView : Node3D
{
    public uint Team { get; set; }
    public Vector2 Radii { get; set; }
    private StandardMaterial3D _material;
    private Label3D _label;
    public override void _Ready()
    {
        _material=new StandardMaterial3D { ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded,AlbedoColor=new Color("c5bda8") };
        AddChild(new MeshInstance3D { Name="Boundary",Position=new Vector3(0,.04f,0),Scale=new Vector3(Radii.X,1,Radii.Y),
            Mesh=new TorusMesh { InnerRadius=.987f,OuterRadius=1.013f,Rings=64,RingSegments=6 },MaterialOverride=_material,
            CastShadow=GeometryInstance3D.ShadowCastingSetting.Off });
        _label=new Label3D { Text="부활 · 보급",Position=new Vector3(0,.3f,0),FontSize=32,PixelSize=.025f,
            Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,OutlineSize=6 };
        AddChild(_label);
    }
    public void SetLocalTeam(uint local)
    {
        if (_material==null) return;
        Color color=local==0 ? new Color("c5bda8") : local==Team ? TeamMaterials.FriendlyColor : TeamMaterials.EnemyColor;
        _material.AlbedoColor=color;_label.Modulate=color;
        _label.Text=local==0 ? "부활 · 보급" : local==Team ? "아군 부활 · 보급" : "적 부활 구역";
    }
}
