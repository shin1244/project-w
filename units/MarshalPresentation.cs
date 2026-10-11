using Godot;

public partial class MarshalPresentation : Node3D, ISkillPresentation
{
    private Unit _unit;
    private MeshInstance3D _march, _rally, _pulse;
    private float _pulseTime;
    public override void _Ready()
    {
        _unit=GetParent<Unit>();
        _march=Ring("March",5,"67d8c5");
        _rally=Ring("Rally",1.4f,"e5bc69");
        _pulse=Ring("Cast",1,"f8dca0");
    }
    private MeshInstance3D Ring(string name,float radius,string color)
    {
        var ring=new MeshInstance3D { Name=name,Visible=false,Position=new Vector3(0,.07f,0),
            Mesh=new TorusMesh { InnerRadius=radius-.035f,OuterRadius=radius+.035f,Rings=40,RingSegments=6 },
            CastShadow=GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride=new StandardMaterial3D { AlbedoColor=new Color(color),ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded } };
        AddChild(ring); return ring;
    }
    public override void _Process(double delta)
    {
        _march.Visible=!_unit.IsDying && _unit.MarshalEffects.Marching(_unit.EffectTick);
        _rally.Visible=!_unit.IsDying && _unit.MarshalEffects.Rallying(_unit.EffectTick);
        _pulseTime=Mathf.Max(0,_pulseTime-(float)delta);
        _pulse.Visible=!_unit.IsDying && _pulseTime>0;
        _pulse.Scale=Vector3.One*(1+(1-_pulseTime/.45f)*2);
    }
    public void PlaySkill(int slot, bool empowered=false) { _pulseTime=.45f; }
}
