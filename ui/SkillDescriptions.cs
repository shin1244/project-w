// 스킬 설명은 표시 데이터로 관리한다. 전투 판정과 입력 가능 여부는 서버 정의를 따른다.
public static class SkillDescriptions
{
    private const string Hunger = "P · 허기\n일반 공격을 하지 않으면 초당 3%씩 공격력이 증가합니다.\n(최대 30%) 일반 공격 시 초기화됩니다.";
    private const string Bite = "Q · 뜯기\n상대에게 돌진해 현재 공격력의 2배 피해를 줍니다.";
    private const string Devour = "R · 포식\n아군 하수인 혹은 용병을 즉사시킵니다.\n해당 유닛의 체력과 공격력을 얻습니다.\n일반 공격 전까지 유지됩니다.\n체력은 보호막으로 적용됩니다.";

    public static string Passive(uint? heroType) => heroType == UnitCatalog.HeroTest ? Hunger : "";

    public static string Active(uint? heroType, int slot, string skillId)
    {
        if (skillId == "wolf-bite") return Bite;
        if (skillId == "wolf-devour") return Devour;
        if (heroType == UnitCatalog.HeroTest && skillId == null)
            return slot switch { 0 => Bite, 1 => "W · 제작 중", 2 => "E · 제작 중", 3 => Devour, _ => "" };
        return skillId ?? "";
    }
}
