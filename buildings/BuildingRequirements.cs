using System;
using System.Collections.Generic;
using System.Globalization;

public partial class BuildingManager
{
    // 접속 시 받은 정의만 보관하고, 충족 여부는 현재 아군 건물에서 매번 계산합니다.
    private readonly Dictionary<uint, uint[]> _unitRequirements = new();
    private readonly Dictionary<uint, uint[]> _buildingRequirements = new();
    public event Action RequirementsChanged;

    public void HandleRequirements(string[] parts)
    {
        if (parts.Length < 3 || parts[1] is not ("UNIT" or "BUILDING") ||
            !uint.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out uint type)) return;
        var required = new uint[parts.Length - 3];
        for (int i = 0; i < required.Length; i++)
            if (!uint.TryParse(parts[i + 3], NumberStyles.None, CultureInfo.InvariantCulture, out required[i])) return;
        var definitions = parts[1] == "UNIT" ? _unitRequirements : _buildingRequirements;
        definitions[type] = required;
        RequirementsChanged?.Invoke();
    }

    public string UnitRequirementBlockReason(uint type) => RequirementBlockReason(_unitRequirements, type);
    public string BuildingRequirementBlockReason(uint type) => RequirementBlockReason(_buildingRequirements, type);

    private string RequirementBlockReason(Dictionary<uint, uint[]> definitions, uint type)
    {
        if (!definitions.TryGetValue(type, out uint[] required)) return null;
        List<string> missing = null;
        foreach (uint prerequisite in required)
        {
            bool complete = false;
            foreach (Building building in _buildings.Values)
                if (_localTeam != 0 && building.SideId == _localTeam && building.BuildingType == prerequisite &&
                    !building.IsUnderConstruction && !building.IsQueuedForDeletion())
                {
                    complete = true;
                    break;
                }
            if (!complete) (missing ??= new()).Add(BuildingCatalog.Name(prerequisite));
        }
        return missing == null ? null : $"선행 건물 필요: {string.Join(", ", missing)} (완공)";
    }
}
