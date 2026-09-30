// 서버 building_defs.go와 같은 순서입니다. 이름은 표시용이며 통신에는 숫자 ID를 사용합니다.
public static class BuildingCatalog
{
    public const uint TownHall = 0, Fortress = 1, Store = 2, Supply = 3,
        Barracks = 4, Forge = 5, Tower = 6;

    public static bool IsPlayerBuildable(uint type) => type is Store or Supply or Barracks or Forge or Tower;
    public static string Name(uint type) => type switch
    {
        TownHall => "회관", Fortress => "요새", Store => "저장소", Supply => "합숙소",
        Barracks => "병영", Forge => "대장간", Tower => "포탑", _ => "건물"
    };
}
