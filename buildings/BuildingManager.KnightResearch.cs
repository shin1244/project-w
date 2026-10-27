using System.Collections.Generic;

public partial class BuildingManager
{
    private readonly Dictionary<uint, KnightResearchSnapshot> _knightResearch = new();
    public KnightResearchSnapshot? KnightResearch(uint team) => _knightResearch.TryGetValue(team, out var state) ? state : null;

    public void HandleKnightResearch(string[] parts)
    {
        if (!KnightResearchSnapshot.TryParse(parts, out var state)) return;
        _knightResearch[state.Team] = state;
        RefreshWork();
        RequirementsChanged?.Invoke();
        ProductionChanged?.Invoke();
    }

    // Research uses the same server-defined tier as its laboratory, without rechecking construction-only prerequisites.
    public string KnightResearchTierBlockReason(uint team) =>
        !_buildingTierRequirements.TryGetValue(BuildingCatalog.ResearchLab, out int tier) ? Lang.T("연구 조건 수신 대기") :
        TeamTier(team).Tier < tier ? Lang.F("HQ {0}티어 완료 필요", tier) : null;
}
