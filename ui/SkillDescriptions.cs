// 스킬 설명은 표시 데이터로 관리한다. 전투 판정과 입력 가능 여부는 서버 정의를 따른다.
public static class SkillDescriptions
{
    private const string Hunger = "P · 허기\n일반 공격을 하지 않으면 초당 3%씩 공격력이 증가합니다.\n(최대 30%) 일반 공격 시 초기화됩니다.";
    private const string Bite = "Q · 뜯기\n상대에게 돌진해 현재 공격력의 2배 피해를 줍니다.";
    private const string Drain = "W · 피의 송곳니\n다음 일반 공격으로 실제 깎은 체력의 50%를 회복합니다.\n일반 공격 명중까지 유지 · Q/E에는 적용되지 않음 · 재사용 8초";
    private const string Aura = "E · 피의 안개\n5초 동안 자신 주변 3.5m의 적에게 매초 현재 공격력의 30% 피해.\n총 5회 · 이동 중에도 유지 · 재사용 12초";
    private const string Devour = "R · 포식\n아군 하수인 혹은 용병을 즉사시킵니다.\n해당 유닛의 체력과 공격력을 얻습니다.\n일반 공격 전까지 유지됩니다.\n체력은 보호막으로 적용됩니다.";

    public static string Passive(uint? heroType) => heroType switch
    {
        UnitCatalog.HeroTest => Hunger,
        UnitCatalog.HeroGolem => "P · 돌가죽\n받는 피해가 항상 10% 감소합니다.",
        _ => ""
    };

    public static string Active(uint? heroType, int slot, string skillId)
    {
        if (skillId == "wolf-bite") return Bite;
        if (skillId == "wolf-drain") return Drain;
        if (skillId == "wolf-aura") return Aura;
        if (skillId == "wolf-devour") return Devour;
        if (skillId == "golem-slam") return "Q · 내려찍기\n주변 3.5m의 적에게 공격력 150% 피해.\n2초 동안 이동 속도 35% 감소 · 재사용 6초";
        if (skillId == "golem-shell") return "W · 돌갑옷\n4초 동안 최대 체력 25%의 보호막.\n재사용 12초";
        if (skillId == "golem-charge") return "E · 바위 돌진\n적을 지정하면 돌진하여 공격력 100% 피해와 0.6초 기절.\n장애물에 막힘 · 사거리 7m · 재사용 10초";
        if (skillId == "golem-quake") return "R · 지진\n주변 5m의 적에게 공격력 250% 피해와 1.25초 기절.\n재사용 30초";
        if (heroType == UnitCatalog.HeroTest && skillId == null)
            return slot switch { 0 => Bite, 1 => Drain, 2 => Aura, 3 => Devour, _ => "" };
        return skillId ?? "";
    }
}
