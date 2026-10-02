using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

// BUILDING 스냅샷만으로 생성/갱신합니다. 접속할 때 클라이언트가 본진을 자체 생성하지 않습니다.
public partial class BuildingManager : Node3D
{
    [Export] public PackedScene TownHallScene;
    [Export] public PackedScene FortressScene;
    [Export] public PackedScene TowerScene;
    [Export] public PackedScene StoreScene;
    [Export] public PackedScene SupplyScene;
    [Export] public PackedScene BarracksScene;
    [Export] public PackedScene ForgeScene;
    [Export] public UnitManager Units;
    private readonly Dictionary<uint, Building> _buildings = new();
    private Building _selected;
    private uint _localTeam;
    private RallyMarker _rallyMarker;
    public IReadOnlyCollection<Building> LiveBuildings => _buildings.Values;
    public Building SelectedBuilding => _selected;
    public event Action SelectionChanged;
    public event Action ProductionChanged;
    public Func<Building, bool> VisibilityCheck { get; set; }
    public uint LayoutVersion { get; private set; }

    public bool CanInspect(Building building) => GodotObject.IsInstanceValid(building) && building.IsInsideTree() &&
        building.IsVisibleInTree() && !building.IsQueuedForDeletion() &&
        _buildings.TryGetValue(building.BuildingId, out Building registered) && registered == building &&
        (building.SideId == _localTeam || VisibilityCheck == null || VisibilityCheck(building));

    public override void _Process(double delta)
    {
        if (_selected != null && !CanInspect(_selected)) ClearSelection();
    }

    public bool TryGetBuilding(uint id, out Building building) => _buildings.TryGetValue(id, out building);
    public PackedScene SceneFor(uint type) => type switch
    {
        0 => TownHallScene, 1 => FortressScene, 2 => StoreScene, 3 => SupplyScene,
        4 => BarracksScene, 5 => ForgeScene, 6 => TowerScene, _ => null
    };

    public void SetLocalTeam(uint team)
    {
        if (_localTeam == team) return;
        _localTeam = team;
        foreach (Building building in _buildings.Values)
        {
            building.SetLocalTeam(team);
            if (building.SideId != team)
            {
                building.ApplyProduction(new ProductionSnapshot(building.BuildingId, 0, Array.Empty<uint>()));
                building.Rally = null;
            }
        }
        RefreshRally();
        RequirementsChanged?.Invoke();
    }

    public void SelectSingle(Building building)
    {
        Building previousSelection = _selected;
        ClearSelectionCore();
        if (!CanInspect(building))
        {
            if (previousSelection != null) SelectionChanged?.Invoke();
            return;
        }
        _selected = building;
        building.SetSelected(true);
        RefreshRally();
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
        if (_rallyMarker != null) _rallyMarker.Visible = false;
    }

    public void HandleSpawn(string[] parts)
    {
        // BUILDING type id sideId x z yaw (yaw: radians)
        if (parts.Length != 7 ||
            !uint.TryParse(parts[1], out uint type) || type > 6 ||
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
                if (existing.SideId != sideId)
                {
                    existing.ApplyProduction(new ProductionSnapshot(id, 0, Array.Empty<uint>()));
                    existing.Rally = null;
                }
                existing.ApplySnapshot(id, sideId, x, z, yaw);
                LayoutVersion++;
                if (_selected == existing) RefreshRally();
                if (selectedSideChanged) SelectionChanged?.Invoke();
                RequirementsChanged?.Invoke();
            }
            return;
        }

        PackedScene scene = SceneFor(type);
        if (scene == null)
        {
            GD.PushWarning($"건물 씬이 지정되지 않았습니다. 타입: {type}");
            return;
        }
        Building building = scene.Instantiate<Building>();
        building.SetLocalTeam(_localTeam);
        building.Name = $"Building_{id}";
        building.ResolveFocus = id => GodotObject.IsInstanceValid(Units) ? Units.ResolveFocus(id) : null;
        AddChild(building);
        building.ApplySnapshot(id, sideId, x, z, yaw);
        _buildings.Add(id, building);
        LayoutVersion++;
        RequirementsChanged?.Invoke();
    }

    public void HandleHealth(HealthSnapshot health)
    {
        if (_buildings.TryGetValue(health.Id, out Building building)) building.ApplyHealth(health);
    }

    public void HandleShield(ShieldSnapshot shield)
    {
        if (_buildings.TryGetValue(shield.Id, out Building building)) building.HealthBar.ApplyShield(shield.Amount);
    }

    public void HandleConstruction(string[] parts)
    {
        if (parts.Length is not (3 or 4) || !uint.TryParse(parts[1], out uint id) || id == 0 ||
            !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out int percent) ||
            percent < 0 || percent > 100 || !_buildings.TryGetValue(id, out Building building)) return;
        uint owner = 0;
        if (parts.Length == 4 && !uint.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out owner)) return;
        if (building.ConstructionPercent == percent && building.ConstructionOwnerId == owner) return;
        bool wasUnderConstruction = building.IsUnderConstruction;
        building.ConstructionOwnerId = owner;
        building.ApplyConstruction(percent);
        if (_selected == building) SelectionChanged?.Invoke();
        if (wasUnderConstruction != building.IsUnderConstruction) RequirementsChanged?.Invoke();
    }

    public void HandleState(StateSnapshot snapshot)
    {
        if (_buildings.TryGetValue(snapshot.Id, out Building building)) building.ApplyServerState(snapshot.State);
    }

    public void HandleProduction(string[] parts)
    {
        if (!ProductionSnapshot.TryParse(parts, out var snapshot) ||
            !_buildings.TryGetValue(snapshot.BuildingId, out Building building) ||
            _localTeam == 0 || building.SideId != _localTeam ||
            building.BuildingType is not (BuildingCatalog.TownHall or BuildingCatalog.Barracks)) return;
        foreach (uint type in snapshot.UnitTypes)
            if (!building.CanTrain(type)) return;
        if (building.ApplyProduction(snapshot) && _selected == building) ProductionChanged?.Invoke();
    }

    public void HandleRemove(string[] parts)
    {
        if (parts.Length == 2 && uint.TryParse(parts[1], out uint id)) Remove(id);
    }

    public void HandleRally(string[] parts)
    {
        if (!RallySnapshot.TryParse(parts, out var rally) ||
            !_buildings.TryGetValue(rally.BuildingId, out Building building) ||
            _localTeam == 0 || building.SideId != _localTeam || !building.IsProducer ||
            (rally.ResourceId != 0 && building.BuildingType != BuildingCatalog.TownHall)) return;
        building.Rally = rally.Enabled ? rally : null;
        if (_selected == building) RefreshRally();
    }

    private void RefreshRally()
    {
        if (_selected == null || _localTeam == 0 || _selected.SideId != _localTeam ||
            _selected.Rally is not RallySnapshot { Enabled: true } rally)
        {
            if (_rallyMarker != null) _rallyMarker.Visible = false;
            return;
        }
        if (_rallyMarker == null)
        {
            _rallyMarker = new RallyMarker { Name = "RallyMarker" };
            AddChild(_rallyMarker);
        }
        _rallyMarker.ShowPoint(_selected.GlobalPosition, rally);
    }

    public void Clear()
    {
        _localTeam = 0;
        _unitRequirements.Clear();
        _buildingRequirements.Clear();
        bool selectionChanged = _selected != null;
        ClearSelectionCore();
        foreach (uint id in new List<uint>(_buildings.Keys)) Remove(id);
        if (selectionChanged) SelectionChanged?.Invoke();
        RequirementsChanged?.Invoke();
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
        RequirementsChanged?.Invoke();
    }

    private static bool TryCoordinate(string text, out float value)
        => float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && float.IsFinite(value);
}
