using System.Collections.Generic;
using System.Globalization;

public partial class BuildingManager
{
    private readonly Dictionary<uint, TeamTierSnapshot> _teamTiers = new();
    public long CommanderWood { get; private set; }
    public TeamTierSnapshot TeamTier(uint team) => _teamTiers.GetValueOrDefault(team, new(team, 1, 0, 0, 0, 0));
    public bool IsTownHallUpgrading(Building building) => building != null &&
        building.BuildingType == BuildingCatalog.TownHall && TeamTier(building.SideId) is { Upgrading: true } state && state.HallId == building.BuildingId;

    public void HandleTeamTier(string[] parts)
    {
        if (!TeamTierSnapshot.TryParse(parts, out var state)) return;
        _teamTiers[state.Team] = state;
        RequirementsChanged?.Invoke();
        ProductionChanged?.Invoke();
    }

    public void HandleCommanderStock(string[] parts)
    {
        if (parts.Length != 3 || parts[0] != "STOCK" || parts[1] != "0" ||
            !long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out long wood)) return;
        CommanderWood = wood;
        RequirementsChanged?.Invoke();
    }

    public void ResetTeamTiers()
    {
        _teamTiers.Clear();
        CommanderWood = 0;
        RequirementsChanged?.Invoke();
        ProductionChanged?.Invoke();
    }
}
