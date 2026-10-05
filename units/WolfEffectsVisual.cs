using Godot;

// 서버가 보낸 활성 상태만 표시하므로 실패한 입력·안개 재등장도 같은 상태를 보인다.
public partial class WolfEffectsVisual : Node3D
{
    private Unit _unit;
    private MeshInstance3D _drain, _aura, _fill;
    private float _time;

    public override void _Ready()
    {
        _unit = GetParent<Unit>();
        _drain = Ring("DrainReady", .9f, new Color(1, .3f, .48f, .9f));
        _aura = Ring("DamageAura", 3.5f, new Color(1, .25f, .16f, .85f));
        _fill = new MeshInstance3D
        {
            Name = "AuraFill", Visible = false, Position = new Vector3(0, .045f, 0),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Mesh = new CylinderMesh { TopRadius = 3.5f, BottomRadius = 3.5f, Height = .015f, RadialSegments = 64 },
            MaterialOverride = Material(new Color(.7f, .08f, .12f, .1f))
        };
        AddChild(_fill);
    }

    private MeshInstance3D Ring(string name, float radius, Color color)
    {
        var ring = new MeshInstance3D
        {
            Name = name, Visible = false, Position = new Vector3(0, .09f, 0),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Mesh = new TorusMesh { InnerRadius = radius - .055f, OuterRadius = radius + .055f, Rings = 96, RingSegments = 6 },
            MaterialOverride = Material(color)
        };
        AddChild(ring);
        return ring;
    }

    private static StandardMaterial3D Material(Color color) => new()
    {
        AlbedoColor = color, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled
    };

    public override void _Process(double delta)
    {
        _time += (float)delta;
        _drain.Visible = !_unit.IsDying && _unit.WolfEffects.DrainReady;
        _aura.Visible = _fill.Visible = !_unit.IsDying && _unit.WolfEffects.AuraUntil != 0;
        float pulse = 1 + Mathf.Sin(_time * 5) * .08f;
        _drain.Scale = new Vector3(pulse, 1, pulse);
        if (_unit.IsDying) SetProcess(false);
    }
}
