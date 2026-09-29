using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

// BUILDING 스냅샷만으로 생성/갱신합니다. 접속할 때 클라이언트가 본진을 자체 생성하지 않습니다.
public partial class BuildingManager : Node3D
{
    [Export] public PackedScene TownHallScene;
    [Export] public PackedScene TowerScene;
    [Export] public UnitManager Units;
    private readonly Dictionary<uint, Building> _buildings = new();
    private Building _selected;
    public IReadOnlyCollection<Building> LiveBuildings => _buildings.Values;
    public Building SelectedBuilding => _selected;
    public event Action SelectionChanged;
    public uint LayoutVersion { get; private set; }

    public bool TryGetBuilding(uint id, out Building building) => _buildings.TryGetValue(id, out building);

    public void SelectSingle(Building building)
    {
        Building previousSelection = _selected;
        ClearSelectionCore();
        if (!GodotObject.IsInstanceValid(building) || building.IsQueuedForDeletion() ||
            !_buildings.TryGetValue(building.BuildingId, out Building registered) || registered != building)
        {
            if (previousSelection != null) SelectionChanged?.Invoke();
            return;
        }
        _selected = building;
        building.SetSelected(true);
        if (previousSelection != _selected) SelectionChanged?.Invoke();
    }

    public void ClearSelection()
    {
        bool selectionChanged = _selected != null;
        ClearSelectionCore();
        if (selectionChanged) SelectionChanged?.Invoke();
    }

    private void ClearSelectionCore()
    {
        if (GodotObject.IsInstanceValid(_selected)) _selected.SetSelected(false);
        _selected = null;
    }

    public void HandleSpawn(string[] parts)
    {
        // BUILDING type id sideId x z yaw (yaw: radians)
        if (parts.Length != 7 ||
            !uint.TryParse(parts[1], out uint type) || type > 1 ||
            !uint.TryParse(parts[2], out uint id) || id == 0 ||
            !uint.TryParse(parts[3], out uint sideId) || sideId == 0 ||
            !TryCoordinate(parts[4], out float x) ||
            !TryCoordinate(parts[5], out float z) ||
            !TryCoordinate(parts[6], out float yaw)) return;

        if (_buildings.TryGetValue(id, out Building existing))
        {
            if (existing.BuildingType == type)
            {
                bool selectedSideChanged = _selected == existing && existing.SideId != sideId;
                existing.ApplySnapshot(id, sideId, x, z, yaw);
                LayoutVersion++;
                if (selectedSideChanged) SelectionChanged?.Invoke();
            }
            return;
        }

        PackedScene scene = type == 0 ? TownHallScene : TowerScene;
        if (scene == null)
        {
            GD.PushWarning($"건물 씬이 지정되지 않았습니다. 타입: {type}");
            return;
        }
        Building building = scene.Instantiate<Building>();
        building.Name = $"Building_{id}";
        building.ResolveFocus = id => GodotObject.IsInstanceValid(Units) ? Units.ResolveFocus(id) : null;
        AddChild(building);
        building.ApplySnapshot(id, sideId, x, z, yaw);
        _buildings.Add(id, building);
        LayoutVersion++;
    }

    public void HandleHealth(HealthSnapshot health)
    {
        if (_buildings.TryGetValue(health.Id, out Building building)) building.ApplyHealth(health);
    }

    public void HandleState(StateSnapshot snapshot)
    {
        if (_buildings.TryGetValue(snapshot.Id, out Building building)) building.ApplyServerState(snapshot.State);
    }

    public void HandleRemove(string[] parts)
    {
        if (parts.Length == 2 && uint.TryParse(parts[1], out uint id)) Remove(id);
    }

    public void Clear()
    {
        bool selectionChanged = _selected != null;
        ClearSelectionCore();
        foreach (uint id in new List<uint>(_buildings.Keys)) Remove(id);
        if (selectionChanged) SelectionChanged?.Invoke();
    }

    private void Remove(uint id)
    {
        if (!_buildings.Remove(id, out Building building)) return;
        LayoutVersion++;
        bool selectionChanged = _selected == building;
        if (selectionChanged) ClearSelectionCore();
        RemoveChild(building);
        building.QueueFree();
        if (selectionChanged) SelectionChanged?.Invoke();
    }

    private static bool TryCoordinate(string text, out float value)
        => float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && float.IsFinite(value);
}
