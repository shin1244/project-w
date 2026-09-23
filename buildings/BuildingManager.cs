using Godot;
using System.Collections.Generic;
using System.Globalization;

// BUILDING 스냅샷만으로 생성/갱신합니다. 접속할 때 클라이언트가 본진을 자체 생성하지 않습니다.
public partial class BuildingManager : Node3D
{
    [Export] public PackedScene TownHallScene;
    private readonly Dictionary<uint, Building> _buildings = new();

    public void HandleSpawn(string[] parts)
    {
        // BUILDING type id sideId x z yaw (yaw: radians)
        if (parts.Length != 7 ||
            !uint.TryParse(parts[1], out uint type) || type != 0 ||
            !uint.TryParse(parts[2], out uint id) || id == 0 ||
            !uint.TryParse(parts[3], out uint sideId) || sideId == 0 ||
            !TryCoordinate(parts[4], out float x) ||
            !TryCoordinate(parts[5], out float z) ||
            !TryCoordinate(parts[6], out float yaw)) return;

        if (_buildings.TryGetValue(id, out Building existing))
        {
            if (existing.BuildingType == type)
                existing.ApplySnapshot(id, sideId, x, z, yaw);
            return;
        }

        if (TownHallScene == null)
        {
            GD.PushWarning("회관 씬이 지정되지 않았습니다.");
            return;
        }
        Building building = TownHallScene.Instantiate<Building>();
        building.Name = $"Building_{id}";
        AddChild(building);
        building.ApplySnapshot(id, sideId, x, z, yaw);
        _buildings.Add(id, building);
    }

    public void HandleHealth(HealthSnapshot health)
    {
        if (_buildings.TryGetValue(health.Id, out Building building)) building.ApplyHealth(health);
    }

    public void HandleRemove(string[] parts)
    {
        if (parts.Length == 2 && uint.TryParse(parts[1], out uint id)) Remove(id);
    }

    public void Clear()
    {
        foreach (uint id in new List<uint>(_buildings.Keys)) Remove(id);
    }

    private void Remove(uint id)
    {
        if (!_buildings.Remove(id, out Building building)) return;
        RemoveChild(building);
        building.QueueFree();
    }

    private static bool TryCoordinate(string text, out float value)
        => float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && float.IsFinite(value);
}
