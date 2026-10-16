// 서버 building_defs.go와 같은 순서입니다. 이름은 표시용이며 통신에는 숫자 ID를 사용합니다.
public static class BuildingCatalog
{
    public const uint TownHall = 0, Fortress = 1, Store = 2, Supply = 3,
        Barracks = 4, Forge = 5, Tower = 6, OilDerrick = 7, Workshop = 8, ResearchLab = 9;

    public static bool IsPlayerBuildable(uint type) => type is Store or Supply or Barracks or Forge or Tower or OilDerrick or Workshop or ResearchLab;
    public static string Name(uint type) => type switch
    {
        TownHall => "HQ", Fortress => "요새", Store => "브랜치", Supply => "숙소",
        Barracks => "인력 사무소", Forge => "조병창", Tower => "감시탑", OilDerrick => "시추탑",
        Workshop => "공장", ResearchLab => "기술연구소", _ => "건물"
    };
}
