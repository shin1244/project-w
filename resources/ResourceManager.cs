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
        AddChild(resource);
        resource.GlobalPosition = position;
        _resources.Add(id, resource);
        return resource;
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
