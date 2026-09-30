// 서버 unit_defs.go의 타입 번호. 소유권과 유닛 종류는 별개다.
public static class UnitCatalog
{
    public const uint Worker = 0;
    public const uint Knight = 1;
    public const uint Archer = 2;
    public const uint MinionMelee = 3;
    public const uint MinionRanged = 4;

    public static bool IsMinion(uint type) => type is MinionMelee or MinionRanged;
    public static string Name(uint type) => type switch
    {
        Worker => "일꾼", Knight => "검방병", Archer => "궁수",
        MinionMelee => "근접 미니언", MinionRanged => "원거리 미니언", _ => "유닛"
    };
}
