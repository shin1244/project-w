// 서버 타입 번호: 용병 0~99 / 하수인 100~199 / 영웅 200~299 / 이벤트 유닛 300~.
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
    public const uint SiegeRam = 300;

    public static bool IsMinion(uint type) => type is MinionMelee or MinionRanged or MinionHealer;
    public static bool IsHero(uint type) => type is HeroTest or HeroGolem;
    public static bool IsObjective(uint type) => type == SiegeRam;
    public static bool IsAutonomous(uint type) => IsMinion(type) || IsObjective(type);
    public static string Description(uint type) => type == SiegeRam
        ? "자동 행군 · 남은 체력에 비례한 건물 충돌 피해\n기절·넉백 면역 · 치유·보호막·포식 불가" : "";
    public static string Name(uint type) => type switch
    {
        Worker => "일꾼", Knight => "검방병", Archer => "궁수",
        MinionMelee => "근접 미니언", MinionRanged => "원거리 미니언", MinionHealer => "생명의 미니언",
        HeroTest => "돌연변이 늑대", HeroGolem => "룬 골렘", SiegeRam => "공성추", _ => "유닛"
    };
}
