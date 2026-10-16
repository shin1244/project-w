using System.Collections.Generic;
using System.Globalization;

public partial class BuildingManager
{
    private readonly Dictionary<uint, TeamTierSnapshot> _teamTiers = new();
    private readonly Dictionary<uint, ArmyUpgradeSnapshot> _armyUpgrades = new();
    private readonly SortedDictionary<uint, MinionUpgradeStep> _minionUpgradeSteps = new();
    public long CommanderWood { get; private set; }
    public long CommanderOil { get; private set; }
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

    public ArmyUpgradeSnapshot ArmyUpgrade(uint team) => _armyUpgrades.GetValueOrDefault(team, ArmyUpgradeSnapshot.Empty(team));

    public void HandleArmyUpgrade(string[] parts)
    {
        if (!ArmyUpgradeSnapshot.TryParse(parts, out var state)) return;
        _armyUpgrades[state.Team] = state;
        RequirementsChanged?.Invoke();
        ProductionChanged?.Invoke();
    }

    // Fixed per-level forge research gains of each lane minion type, in type order.
    public IEnumerable<MinionUpgradeStep> MinionUpgradeSteps => _minionUpgradeSteps.Values;

    public void HandleMinionUpgradeStep(string[] parts)
    {
        if (!MinionUpgradeStep.TryParse(parts, out var step)) return;
        _minionUpgradeSteps[step.UnitType] = step;
        ProductionChanged?.Invoke();
    }

    public void HandleCommanderStock(string[] parts)
    {
        if (parts.Length != 3 || parts[0] != "STOCK" || parts[1] is not ("0" or "2") ||
            !long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out long amount)) return;
        if (parts[1] == "0") CommanderWood = amount;
        else CommanderOil = amount;
        RequirementsChanged?.Invoke();
    }

    public void ResetTeamTiers()
    {
        _teamTiers.Clear();
        _armyUpgrades.Clear();
        _minionUpgradeSteps.Clear();
        _knightResearch.Clear();
        CommanderWood = 0;
        CommanderOil = 0;
        CommanderSupplyUsed = CommanderSupplyLimit = 0;
        HasCommanderSupply = false;
        RequirementsChanged?.Invoke();
        ProductionChanged?.Invoke();
    }
}
