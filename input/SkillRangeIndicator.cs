using Godot;

// 대상 지정 중에만 보이는 사거리 안내. 시전 가능 여부는 서버가 판단한다.
public sealed class SkillRangeIndicator
{
    // 서버 hero_defs.go의 늑대 슬롯 0 Range. 아직 스킬 정의를 보내는 메시지는 없다.
    public const float WolfQRange = 6f;
    private MeshInstance3D _ring;

    public void Show(Unit hero, float reach, float bodyRadius)
    {
        Clear();
        // 몸 가장자리 기준: 이 경계에 적의 충돌 몸체가 닿으면 사거리 안이다.
        float radius = reach + bodyRadius;
        _ring = new MeshInstance3D
        {
            Name = "SkillRangeRing",
            Position = new Vector3(0, .07f, 0),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Mesh = new TorusMesh { InnerRadius = radius - .07f, OuterRadius = radius + .07f, Rings = 128, RingSegments = 8 },
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                NoDepthTest = true,
                RenderPriority = 2,
                AlbedoColor = new Color(.22f, .95f, .78f, .95f)
            }
        };
        // 모델 회전과 무관하게, 보간된 영웅 위치를 그대로 따라간다.
        hero.AddChild(_ring);
        // 밝은 바닥에서도 읽히는 가는 테두리. 지형·나무에 가리지 않는 안내선이다.
        _ring.AddChild(new MeshInstance3D
        {
            Name = "Outline",
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Mesh = new TorusMesh { InnerRadius = radius - .12f, OuterRadius = radius + .12f, Rings = 128, RingSegments = 8 },
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
            _ring.Name = "ExpiredSkillRangeRing";
            _ring.QueueFree();
        }
        _ring = null;
    }
}
