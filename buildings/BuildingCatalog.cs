// 서버 building_defs.go와 같은 순서입니다. 이름은 표시용이며 통신에는 숫자 ID를 사용합니다.
public static class BuildingCatalog
{
    public const uint TownHall = 0, Fortress = 1, Store = 2, Supply = 3,
        Barracks = 4, Forge = 5, Tower = 6, OilDerrick = 7, Workshop = 8, ResearchLab = 9, CoalDrill = 10, Tunnel = 11;
    public const uint Last = Tunnel;

    public static bool IsPlayerBuildable(uint type) => type is >= Store and <= Last;
    // Buildings the server opens at an HQ tier; their menu stays locked until REQUIRES_TIER arrives.
    public static bool IsTierTwo(uint type) => type is Workshop or ResearchLab or CoalDrill or Tunnel;
    public static string Name(uint type) => type switch
    {
        TownHall => "HQ", Fortress => Lang.T("요새"), Store => Lang.T("브랜치"), Supply => Lang.T("숙소"),
        Barracks => Lang.T("인력 사무소"), Forge => Lang.T("비허가 실험실"), Tower => Lang.T("감시탑"), OilDerrick => Lang.T("시추탑"),
        Workshop => Lang.T("공장"), ResearchLab => Lang.T("기술연구소"), CoalDrill => Lang.T("석탄 시추기"), Tunnel => Lang.T("땅굴"), _ => Lang.T("건물")
    };
}
