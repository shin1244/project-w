using Godot;

// 마지막으로 명령을 내린 대상을 표시합니다. 서버의 명령 승인/공격 판정과는 별개입니다.
public sealed class CommandTargetIndicator
{
    public Node3D Target { get; private set; }
    private MeshInstance3D _ring;

    public void Show(Node3D target)
    {
        if (Target == target && GodotObject.IsInstanceValid(_ring)) return;
        Clear();
        bool gather = target is ResourceNode;
        float radius = gather ? 1.18f : 0.84f;
        _ring = new MeshInstance3D
        {
            Name = "CommandTargetRing",
            Position = new Vector3(0, 0.055f, 0),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Mesh = new TorusMesh { InnerRadius = radius, OuterRadius = radius + 0.075f, Rings = 48, RingSegments = 8 },
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                AlbedoColor = gather ? new Color(1, 0.8f, 0.06f) : new Color(1, 0.12f, 0.08f)
            }
        };
        Target = target;
        target.AddChild(_ring); // 유닛이 움직이면 원도 같이 움직이며, 모델의 기울기와는 독립적입니다.
    }

    public void Refresh()
    {
        if (!GodotObject.IsInstanceValid(Target) || !Target.IsInsideTree() || Target.IsQueuedForDeletion() ||
            Target is Unit { IsDying: true } || Target is ResourceNode { Amount: <= 0 })
            Clear();
    }

    public void Clear()
    {
        if (GodotObject.IsInstanceValid(_ring))
        {
            _ring.Hide();
            _ring.Name = "ExpiredTargetRing";
            _ring.QueueFree();
        }
        _ring = null;
        Target = null;
    }
}
