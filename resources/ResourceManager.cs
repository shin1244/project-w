using Godot;
using System.Collections.Generic;

// 서버 자원 ID로 나무를 관리합니다. 채집과 통행 판정은 서버에서 처리합니다.
public partial class ResourceManager : Node3D
{
    [Export] public PackedScene OakScene;
    [Export] public PackedScene PineScene;
    [Export] public PackedScene BirchScene;

    private readonly Dictionary<uint, ResourceNode> _resources = new();

    public void Clear()
    {
        foreach (uint id in new List<uint>(_resources.Keys))
            Remove(id);
    }

    public bool TryGetResource(uint id, out ResourceNode resource)
        => _resources.TryGetValue(id, out resource);

    // 서버 메시지 형식과 독립적인 생성/갱신 진입점입니다. Y도 서버/맵 좌표를 그대로 씁니다.
    public ResourceNode SpawnOrUpdate(uint id, uint visualVariant, Vector3 position)
    {
        if (!position.IsFinite())
            return null;

        PackedScene scene = visualVariant switch
        {
            0 => OakScene,
            1 => PineScene,
            2 => BirchScene,
            _ => null
        };
        if (scene == null)
        {
            GD.PushWarning($"나무 리소스를 확인하세요. 외형: {visualVariant}");
            return null;
        }

        if (_resources.TryGetValue(id, out ResourceNode existing))
        {
            if (existing.VisualVariant != visualVariant)
            {
                GD.PushWarning($"이미 등록된 자원의 외형이 다릅니다. ID: {id}");
                return null;
            }
            existing.GlobalPosition = position;
            return existing;
        }

        ResourceNode resource = scene.Instantiate<ResourceNode>();
        resource.Initialize(id);
        resource.Name = $"Resource_{id}";
        Vary(resource, id);
        AddChild(resource);
        resource.GlobalPosition = position;
        _resources.Add(id, resource);
        return resource;
    }

    // 같은 종류도 한 그루씩 다르게 보이도록 자원 ID로 회전·크기를 정합니다.
    // Foliage.gdshader는 이 회전 각도로 나무별 색조를 정합니다.
    // 모든 클라이언트에서 같고, 선택·클릭 영역과 서버 판정은 바꾸지 않습니다.
    private static void Vary(ResourceNode resource, uint id)
    {
        uint hash = id * 2654435761u;
        var visual = resource.GetNode<Node3D>("Visual");
        visual.Rotation = new Vector3(0, (hash >> 8) / 16777216f * Mathf.Tau, 0);
        visual.Scale = Vector3.One * (0.9f + ((hash >> 4) & 0xFF) / 255f * 0.2f);
    }

    public void Remove(uint id)
    {
        if (!_resources.Remove(id, out ResourceNode resource))
            return;

        resource.SetSelected(false);
        RemoveChild(resource);
        resource.QueueFree();
    }
}
