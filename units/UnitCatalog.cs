// 서버 타입 번호: 용병 0~99 / 하수인 100~199 / 영웅 200~299 / 이벤트 유닛 300~.
// 소유권과 유닛 종류는 별개다.
public static class UnitCatalog
{
    public const uint Worker = 0;
    public const uint Knight = 1;
    public const uint Archer = 2;
    public const uint Medic = 3;
    public const uint FlameAutomaton = 4;
    public const uint SteamArtillery = 5;
    public const uint MinionMelee = 100;
    public const uint MinionRanged = 101;
    public const uint MinionHealer = 102;
    public const uint HeroTest = 200;
    public const uint HeroGolem = 201;
    public const uint HeroMarshal = 202;
    public const uint HeroRanger = 203;
    public const uint SiegeRam = 300;
    public const uint JungleBoar = 400;
    public const uint JungleTreant = 401;

    public static bool IsMinion(uint type) => type is MinionMelee or MinionRanged or MinionHealer;
    public static bool IsHero(uint type) => type is HeroTest or HeroGolem or HeroMarshal or HeroRanger;
    public static bool IsTrainable(uint type) => type is Worker or Knight or Archer or Medic or FlameAutomaton or SteamArtillery;
    public static bool IsWorkshopUnit(uint type) => type is FlameAutomaton or SteamArtillery;
    public static bool IsObjective(uint type) => type == SiegeRam;
    // 정글몹: 팀 0. 먼저 맞기 전에는 공격하지 않는다.
    public static bool IsNeutral(uint type) => type is JungleBoar or JungleTreant;
    public static bool IsAutonomous(uint type) => IsMinion(type) || IsObjective(type) || IsNeutral(type);
    public static string Description(uint type) => type switch
    {
        Knight => "검과 방패를 갖춘 근접 보병\n기술연구소의 와이어 돌진 연구로 자동 돌진 해금",
        Medic => "2초마다 반경 5m의 다른 아군 유닛 체력 40 회복\n자신·건물·공성추 제외 · 조병창 필요",
        FlameAutomaton => "전방 부채꼴 화염 공격 · 공장 생산",
        SteamArtillery => "일반 모드 단일 공격 · 전개 시 장거리 범위 포격\n전개 중 이동·근접 표적 공격 불가 · 전환 중 이동·공격 불가",
        SiegeRam => "자동 행군 · 마석 수집 횟수에 따른 건물 충돌 피해\n기절·넉백 면역 · 치유·보호막·포식 불가",
        JungleBoar => "중립 정글몹 · 먼저 공격받으면 반격\n처치 시 작은 금덩어리(수집한 영웅에게 골드·경험치) · 수집 2분 20초 뒤 더 강해져 부활",
        JungleTreant => "강한 중립 정글몹 · 먼저 공격받으면 반격\n처치 시 큰 금덩어리(골드·경험치 + 팀에 미니언 무료권) · 수집 2분 20초 뒤 더 강해져 부활",
        _ => ""
    };
    public static string Name(uint type) => type switch
    {
        Worker => "일꾼", Knight => "스팀돌격병", Archer => "공압사수", Medic => "치유사", FlameAutomaton => "화염기계", SteamArtillery => "증기포격차",
        MinionMelee => "근접 미니언", MinionRanged => "원거리 미니언", MinionHealer => "생명의 미니언",
        HeroTest => "돌연변이 늑대", HeroGolem => "룬 골렘", HeroMarshal => "사령관", HeroRanger => "레인저", SiegeRam => "공성추",
        JungleBoar => "가시 멧돼지", JungleTreant => "고목 수호자", _ => "유닛"
    };
}
