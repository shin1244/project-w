public partial class BuildingManager
{
    // 생산 대기열은 우리 팀 건물만 알고, HQ 업그레이드와 연구는 서버가 양 팀에 공개합니다.
    // 그래서 적 건물은 연구할 때만 움직이고, 생산 중인지는 드러나지 않습니다.
    private bool IsWorking(Building building) => building.ProductionQueue.Count > 0 || IsTownHallUpgrading(building) ||
        building.BuildingType == BuildingCatalog.Forge && ArmyUpgrade(building.SideId).ForgeBusy(building.BuildingId) ||
        building.BuildingType == BuildingCatalog.ResearchLab &&
        KnightResearch(building.SideId) is { Researching: true } research && research.LabId == building.BuildingId;

    private void UpdateWork(Building building)
    {
        if (building.IsWorkplace) building.SetWorking(IsWorking(building));
    }

    private void RefreshWork()
    {
        foreach (Building building in _buildings.Values) UpdateWork(building);
    }
}
