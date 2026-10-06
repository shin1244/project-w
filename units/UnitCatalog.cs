// 서버 unit_defs.go(용병 0~99) / minion_defs.go(하수인 100~199) / hero_defs.go(영웅 200~299)의 타입 번호.
// 소유권과 유닛 종류는 별개다.
public static class UnitCatalog
{
    public const uint Worker = 0;
    public const uint Knight = 1;
    public const uint Archer = 2;
    public const uint MinionMelee = 100;
    public const uint MinionRanged = 101;
    public const uint MinionHealer = 102;
    public const uint HeroTest = 200;
    public const uint HeroGolem = 201;

    public static bool IsMinion(uint type) => type is MinionMelee or MinionRanged or MinionHealer;
    public static bool IsHero(uint type) => type is HeroTest or HeroGolem;
    public static string Name(uint type) => type switch
    {
        Worker => "일꾼", Knight => "검방병", Archer => "궁수",
        MinionMelee => "근접 미니언", MinionRanged => "원거리 미니언", MinionHealer => "생명의 미니언",
        HeroTest => "돌연변이 늑대", HeroGolem => "룬 골렘", _ => "유닛"
    };
}
