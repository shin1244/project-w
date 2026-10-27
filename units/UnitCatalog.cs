// 서버 타입 번호: 용병 0~99 / 하수인 100~199 / 영웅 200~299 / 이벤트 유닛 300~.
// 소유권과 유닛 종류는 별개다.
public static class UnitCatalog
{
    public const uint Worker = 0;
    public const uint Knight = 1;
    public const uint Archer = 2;
    public const uint Medic = 3; // 돌팔이 의사. 지운 치유사의 번호를 서버와 함께 이어 쓴다.
    public const uint FlameAutomaton = 4;
    public const uint SteamArtillery = 5;
    public const uint Refueler = 6;
    public const uint MinionMelee = 100;
    public const uint MinionRanged = 101;
    public const uint MinionHealer = 102;
    public const uint MinionFurnace = 103; // 2티어 근접
    public const uint MinionWarden = 104;  // 2티어 근접
    public const uint MinionArc = 105;     // 2티어 원거리
    public const uint HeroTest = 200;
    public const uint HeroGolem = 201;
    public const uint HeroMarshal = 202;
    public const uint HeroRanger = 203;
    public const uint SiegeRam = 300;
    public const uint JungleBoar = 400;
    public const uint JungleTreant = 401;

    public static bool IsMinion(uint type) => type is MinionMelee or MinionRanged or MinionHealer or MinionFurnace or MinionWarden or MinionArc;
    public static bool IsHero(uint type) => type is HeroTest or HeroGolem or HeroMarshal or HeroRanger;
    public static bool IsTrainable(uint type) => type is Worker or Knight or Archer or Medic or FlameAutomaton or SteamArtillery or Refueler;
    public static bool IsWorkshopUnit(uint type) => type is FlameAutomaton or SteamArtillery or Refueler;
    public static bool IsBarracksUnit(uint type) => type is Knight or Archer or Medic;
    // 서버 TraitMechanical과 같다. 돌팔이 의사는 기계가 아닌 쪽을, 급유 기계는 기계를 고친다.
    public static bool IsMechanical(uint type) => type is FlameAutomaton or SteamArtillery or Refueler or SiegeRam;
    public static bool HasMana(uint type) => type == Medic || IsHero(type);
    public static bool IsObjective(uint type) => type == SiegeRam;
    // 정글몹: 팀 0. 먼저 맞기 전에는 공격하지 않는다.
    public static bool IsNeutral(uint type) => type is JungleBoar or JungleTreant;
    public static bool IsAutonomous(uint type) => IsMinion(type) || IsObjective(type) || IsNeutral(type);
    public static string Description(uint type) => type switch
    {
        Knight => Lang.T("검과 방패를 갖춘 근접 보병\n기술연구소의 와이어 돌진 연구로 자동 돌진 해금"),
        Medic => Lang.T("면허 없는 의사 · 마나를 써서 사거리 안 아군 한 명을 고치며 공격도 함\n영웅 > 용병 > 미니언 순 · 영웅은 모두, 용병·미니언은 생체만 · 비허가 실험실 필요"),
        Refueler => Lang.T("기계 전용 수리기 · 마나 없이 사거리 안 기계 하나를 고치며 공격도 함\n용병 > 미니언 순 · 영웅·생체는 못 고침 · 기술연구소 필요"),
        FlameAutomaton => Lang.T("전방 부채꼴 화염 공격 · 공장 생산"),
        SteamArtillery => Lang.T("일반 모드 단일 공격 · 전개 시 장거리 범위 포격\n전개 중 이동·근접 표적 공격 불가 · 전환 중 이동·공격 불가"),
        MinionFurnace => Lang.T("2티어 근접 미니언 · 등에 진 화로로 주변 적 유닛을 계속 태움"),
        MinionWarden => Lang.T("2티어 근접 미니언 · 주변 아군 영웅·용병이 받는 피해 일부를 대신 받음\n여러 기가 있어도 가장 가까운 한 기만 받음"),
        MinionArc => Lang.T("2티어 원거리 미니언 · 맞힌 공격이 주변의 다른 적에게 줄어든 피해로 튕김"),
        SiegeRam => Lang.T("자동 행군 · 마석 수집 횟수에 따른 건물 충돌 피해\n기절·넉백 면역 · 치유·보호막·포식 불가"),
        JungleBoar => Lang.T("중립 정글몹 · 먼저 공격받으면 반격\n처치 시 작은 금덩어리(수집한 영웅에게 골드·경험치) · 수집 2분 20초 뒤 더 강해져 부활"),
        JungleTreant => Lang.T("강한 중립 정글몹 · 먼저 공격받으면 반격\n처치 시 큰 금덩어리(골드·경험치 + 팀에 미니언 무료권) · 수집 2분 20초 뒤 더 강해져 부활"),
        _ => ""
    };
    public static string Name(uint type) => type switch
    {
        Worker => Lang.T("일꾼"), Knight => Lang.T("스팀돌격병"), Archer => Lang.T("공압사수"), Medic => Lang.T("돌팔이 의사"), FlameAutomaton => Lang.T("화염기계"), SteamArtillery => Lang.T("증기포격차"), Refueler => Lang.T("급유 기계"),
        MinionMelee => Lang.T("근접 미니언"), MinionRanged => Lang.T("원거리 미니언"), MinionHealer => Lang.T("생명의 미니언"),
        MinionFurnace => Lang.T("화로 미니언"), MinionWarden => Lang.T("호위 미니언"), MinionArc => Lang.T("전격 미니언"),
        HeroTest => Lang.T("돌연변이 늑대"), HeroGolem => Lang.T("룬 골렘"), HeroMarshal => Lang.T("사령관"), HeroRanger => Lang.T("레인저"), SiegeRam => Lang.T("공성추"),
        JungleBoar => Lang.T("가시 멧돼지"), JungleTreant => Lang.T("고목 수호자"), _ => Lang.T("유닛")
    };
}
