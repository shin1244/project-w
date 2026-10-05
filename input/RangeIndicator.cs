using Godot;

// 공격·스킬 대상 지정 중에만 보이는 사거리 안내. 실제 판정은 서버가 담당한다.
public sealed class RangeIndicator
{
    private MeshInstance3D _ring;
    private readonly string _name;
    private readonly Color _color;

    public RangeIndicator(string name, Color color) { _name = name; _color = color; }

    public void Show(Unit unit, float reach, float bodyRadius)
    {
        Clear();
        // 몸 가장자리 기준: 이 경계에 적의 충돌 몸체가 닿으면 사거리 안이다.
        float radius = reach + bodyRadius;
        _ring = new MeshInstance3D
        {
            Name = _name,
            Position = new Vector3(0, .07f, 0),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Mesh = new TorusMesh { InnerRadius = Mathf.Max(0, radius - .07f), OuterRadius = radius + .07f, Rings = 128, RingSegments = 8 },
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                NoDepthTest = true,
                RenderPriority = 2,
                AlbedoColor = _color
            }
        };
        // 모델 회전과 무관하게, 보간된 유닛 위치를 그대로 따라간다.
        unit.AddChild(_ring);
        // 밝은 바닥에서도 읽히는 가는 테두리. 지형·나무에 가리지 않는 안내선이다.
        _ring.AddChild(new MeshInstance3D
        {
            Name = "Outline",
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Mesh = new TorusMesh { InnerRadius = Mathf.Max(0, radius - .12f), OuterRadius = radius + .12f, Rings = 128, RingSegments = 8 },
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                NoDepthTest = true,
                RenderPriority = 1,
                AlbedoColor = new Color(.02f, .13f, .12f, .9f)
            }
        });
    }

    public void Clear()
    {
        if (GodotObject.IsInstanceValid(_ring))
        {
            _ring.Hide();
            _ring.Name = "Expired" + _name;
            _ring.QueueFree();
        }
        _ring = null;
    }
}
