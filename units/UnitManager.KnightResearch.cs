public partial class UnitManager
{
    public string KnightResearchBlockReason(Building lab)
    {
        if (!CommanderControl || Spectating || _controlledHeroType.HasValue || !CanUseSelectedBuilding(lab) ||
            lab.BuildingType != BuildingCatalog.ResearchLab) return "아군 기술연구소를 선택하세요";
        if (lab.IsUnderConstruction) return "기술연구소 건설 중";
        if (Buildings.KnightResearch(lab.SideId) is not KnightResearchSnapshot state) return "연구 정보 수신 대기";
        if (state.Unlocked) return "연구 완료";
        if (state.Researching) return state.LabId == lab.BuildingId ? "연구 중" : "다른 기술연구소에서 연구 중";
        if (Buildings.KnightResearchTierBlockReason(lab.SideId) is string reason) return reason;
        if (Buildings.CommanderWood < state.Wood) return "목재 부족";
        return Buildings.CommanderOil < state.Oil ? "기름 부족" : null;
    }

    public void RequestKnightResearch()
    {
        var lab = Buildings?.SelectedBuilding;
        if (KnightResearchBlockReason(lab) == null)
            CommandRequested?.Invoke(Protocol.BuildKnightResearch(lab.BuildingId));
    }
}
