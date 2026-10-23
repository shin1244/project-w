using System.Collections.Generic;
using System.Globalization;

public partial class BuildingManager
{
    private readonly Dictionary<uint, TrainingDefinition> _trainingDefinitions = new();
    public long CommanderSupplyUsed { get; private set; }
    public long CommanderSupplyLimit { get; private set; }
    public bool HasCommanderSupply { get; private set; }
    public TrainingDefinition? TrainingTerms(uint type) => _trainingDefinitions.TryGetValue(type, out var terms) ? terms : null;

    public void HandleTrainingDefinition(string[] parts)
    {
        if (!TrainingDefinition.TryParse(parts, out var definition)) return;
        _trainingDefinitions[definition.UnitType] = definition;
        RequirementsChanged?.Invoke();
    }

    public void HandleCommanderSupply(string[] parts)
    {
        if (parts.Length != 3 || parts[0] != "SUPPLY" ||
            !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out long used) ||
            !long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out long limit)) return;
        CommanderSupplyUsed = used;
        CommanderSupplyLimit = limit;
        HasCommanderSupply = true;
        RequirementsChanged?.Invoke();
    }

    public string TrainingTermsBlockReason(Building producer, uint type)
    {
        if (TrainingTerms(type) is not TrainingDefinition terms)
            return UnitCatalog.IsWorkshopUnit(type) || type == UnitCatalog.Medic ? "생산 정보 수신 대기" : null;
        if (producer == null || producer.BuildingType != terms.BuildingType) return "생산 건물 불일치";
        if (CommanderWood < terms.Wood) return "목재 부족";
        if (CommanderOil < terms.Oil) return "기름 부족";
        if (!HasCommanderSupply) return "인구 정보 수신 대기";
        return CommanderSupplyUsed > CommanderSupplyLimit || terms.Supply > CommanderSupplyLimit - CommanderSupplyUsed ? "인구 부족" : null;
    }
}
