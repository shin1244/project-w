using System;
using System.Collections.Generic;

// Base descriptions plus cumulative talents; authoritative availability comes from the server.
public static class SkillDescriptions
{
    public static string Passive(uint? heroType, int rank=0)
    {
        if (heroType == UnitCatalog.HeroTest)
        {
            int rate=rank switch { 0=>5,1=>6,2=>12,_=>14 };
            int maximum=rank switch { 0=>50,1 or 2=>60,_=>70 };
            return $"P · 허기\n공격을 쉬는 동안 초당 공격력 +{rate}%, 최대 +{maximum}%.\n일반 공격 또는 Q 명중 시 초기화."+
                (rank >= 4 ? "\n최대 중첩 명중 시 대상 최대 체력 10% 추가 피해.\nQ 배율은 추가 피해에 곱하지 않으며, 대상 피해 감소·보호막이 적용됩니다." : "")+
                "\n일꾼 공격에는 적용되지 않으며 중첩을 소모하지 않습니다.";
        }
        if (heroType == UnitCatalog.HeroGolem)
            return $"P · 돌가죽\n받는 피해 { (rank >= 3 ? 15 : rank >= 1 ? 12 : 10) }% 감소."+
                (rank >= 2 ? "\n기절·속박 지속 시간 20% 감소." : "")+
                (rank >= 4 ? "\n체력이 30% 이하이면 받는 피해 감소가 총 25%로 증가." : "");
        if (heroType == UnitCatalog.HeroMarshal)
        {
            string amount=rank >= 3 ? "1" : rank >= 1 ? "0.7" : "0.5";
            return $"P · 전선 지휘\n주변 5m 아군 미니언·용병{(rank >= 4 ? "·영웅" : "")}에게 공격 피해 +{amount}, 받는 피해 −{amount}."+
                (rank >= 2 ? "\n오라 대상 이동 속도 +10%." : "")+"\n자신과 일꾼에게는 적용되지 않으며 같은 오라는 중첩되지 않습니다.";
        }
        if (heroType == UnitCatalog.HeroRanger)
        {
            int percent=rank switch { 0=>6,1 or 2=>7,_=>8 };
            int maximum=rank >= 2 ? 5 : 4;
            return $"P · 집중 사격\n같은 대상을 연달아 맞힐 때마다 그 대상에게 주는 피해 +{percent}%, 최대 {maximum}중첩.\n일반 공격과 Q 명중이 쌓고, 대상을 바꾸거나 3초 동안 쏘지 않으면 초기화."+
                (rank >= 4 ? "\n최대 중첩 명중 시 대상 이동 속도 20% 감소, 1초." : "");
        }
        return "";
    }
    public static string PassiveUpgrade(uint? heroType,int next)
    {
        if (next is < 1 or > 4) return "최대 강화";
        return (heroType,next) switch
        {
            (UnitCatalog.HeroTest,1) => "초당 공격력 +6%, 최대 +60% (최대 도달 10초)",
            (UnitCatalog.HeroTest,2) => "초당 공격력 +12%, 최대 +60% (최대 도달 5초)",
            (UnitCatalog.HeroTest,3) => "초당 공격력 +14%, 최대 +70% (최대 도달 5초)",
            (UnitCatalog.HeroTest,4) => "최대 중첩의 일반 공격·Q 명중 시 대상 최대 체력 10% 추가 피해",
            (UnitCatalog.HeroGolem,1) => "받는 피해 감소 10% → 12%",
            (UnitCatalog.HeroGolem,2) => "기절·속박 지속 시간 20% 감소",
            (UnitCatalog.HeroGolem,3) => "받는 피해 감소 12% → 15%",
            (UnitCatalog.HeroGolem,4) => "체력 30% 이하에서 받는 피해 감소 총 25%",
            (UnitCatalog.HeroMarshal,1) => "오라 공격 피해 +0.7, 받는 피해 −0.7",
            (UnitCatalog.HeroMarshal,2) => "오라 대상 이동 속도 +10%",
            (UnitCatalog.HeroMarshal,3) => "오라 공격 피해 +1, 받는 피해 −1",
            (UnitCatalog.HeroMarshal,4) => "주변 아군 영웅도 모든 패시브 오라 효과를 받음",
            (UnitCatalog.HeroRanger,1) => "중첩당 피해 +6% → +7%",
            (UnitCatalog.HeroRanger,2) => "최대 4중첩 → 5중첩",
            (UnitCatalog.HeroRanger,3) => "중첩당 피해 +7% → +8%",
            (UnitCatalog.HeroRanger,4) => "최대 중첩 명중 시 대상 이동 속도 20% 감소, 1초",
            _ => ""
        };
    }
    public static string Active(uint? heroType, int slot, string skillId, bool empowered=false, int rank=0)
    {
        if (skillId == null && heroType == UnitCatalog.HeroTest && slot is >= 0 and < 4)
            skillId = new[] { "wolf-bite", "wolf-drain", "wolf-aura", "wolf-devour" }[slot];
        int steps=(rank+1)/2;
        float scale=1+.2f*steps;
        string text=skillId switch
        {
            "wolf-bite" => $"Q · 뜯기\n적에게 돌진하여 공격력 {200*scale:0}% 피해. 일반 공격 판정.\n사거리 8m · 재사용 7초",
            "wolf-drain" => $"W · 피의 송곳니\n다음 일반 공격 또는 Q로 실제 깎은 체력의 {50+10*steps}% 회복.\nE에는 적용되지 않음 · 명중까지 유지 · 종료 후 재사용 8초",
            "wolf-aura" => $"E · 피의 안개\n주변 3.5m 적에게 즉시 1회, 이후 0.5초마다 10회 타격.\n매 타격 공격력 {25*scale:0}% 피해 · 5초 지속 · 종료 후 재사용 10초",
            "wolf-devour" => rank > 0
                ? "R · 무리 포식\n주변 8m 아군 미니언·용병을 모두 먹습니다.\n가장 높은 최대 체력 ×1 보호막, 가장 높은 기본 공격력 ×2 추가 공격력.\n먹은 유닛 수만큼 일반 공격·Q 명중에 적용. 마지막 명중 후 남은 보호막도 종료.\n재사용 40초 · 재시전 시 이전 효과 교체"
                : "R · 포식\n8m 안 아군 미니언·용병 1마리를 먹습니다.\n대상의 최대 체력 ×1 보호막, 기본 공격력 ×2 추가 공격력.\n다음 일반 공격·Q 명중 후 종료 · 재사용 40초",
            "golem-slam" => $"Q · {(empowered ? "강화 " : "")}내려찍기\n주변 {(empowered ? 5 : 3.5f)}m 적에게 공격력 {(empowered ? 150 : 120)*scale:0}% 피해.\n{(empowered ? "룬 각성 소모" : "2초 동안 이동 속도 35% 감소")} · 재사용 10초",
            "golem-shell" => $"W · {(empowered ? "강화 " : "")}돌갑옷\n자신의 최대 체력 {(empowered ? 40 : 25)*scale:0}% 보호막, {(empowered ? 4 : 5)}초 지속.\n재사용 13초 · 보호막 유지 중 재시전 불가",
            "golem-charge" => $"E · {(empowered ? "강화 " : "")}바위 돌진\n적에게 돌진해 공격력 {100*scale:0}% 피해와 {(empowered ? 1 : .5f)}초 기절.\n사거리 7m · 장애물에 막힘 · 재사용 12초",
            "golem-up" => $"R · 룬 각성\n다음 Q/W/E {(rank > 0 ? 2 : 1)}회를 강화. 사용하거나 사망할 때까지 유지.\nQ: 범위 5m, 기본 피해 150% · W: 기본 보호막 40% · E: 기절 1초\n재사용 40초 · 중첩되지 않음",
            "marshal-shot" => $"Q · 집중 사격\n지정한 적에게 공격력 {150*scale:0}% 피해. 소집 부대가 집중 공격.\n사거리 7m · 재사용 7초",
            "marshal-bulwark" => $"W · 방벽\n자신에게 최대 체력 {10*scale:0.#}%, 주변 5m 아군에게 자신의 최대 체력 {5*scale:0.#}% 보호막.\n5초 지속 · 재사용 12초",
            "marshal-march" => $"E · 진군\n자신과 주변 5m 아군 미니언·용병에게 이동 속도 ×1.25, 공격 속도 ×1.2.\n{4+steps}초 지속 · 재사용 12초",
            "ranger-pierce" => $"Q · 관통 화살\n지정한 적에게 공격력 {160*scale:0}% 피해. 일반 공격 판정이라 집중 사격을 쌓고 적용받음.\n사거리 8m · 재사용 8초",
            "ranger-stride" => $"W · 바람 걸음\n{2.5f+.5f*steps:0.#}초 동안 이동 속도 ×1.35, {4+.5f*steps:0.#}초 동안 공격 속도 ×1.3.\n재사용 12초",
            "ranger-volley" => $"E · 화살비\n찍은 곳 {(rank >= 4 ? 4 : 3)}m 안 적에게 공격력 {110*scale:0}% 피해, 2초 동안 이동 속도 30% 감소.\n사거리 8m · 재사용 11초",
            "ranger-snipe" => $"R · 저격\n지정한 적에게 공격력 {350*scale:0}% 피해.\n사거리 {(rank > 0 ? 18 : 14)}m · 대상이 시야에 있어야 함 · 재사용 45초",
            "marshal-command" => $"R · 부대 소집\n주변 6m의 아군 미니언{(rank > 0 ? "·용병" : "")}을 소집.\n30초 동안 따라 이동하며 지정한 적을 집중 공격. 종료 시 소집 유닛 소멸.\n재사용 60초",
            _ => skillId ?? ""
        };
        if (slot < 3)
        {
            if (rank >= 2) text += "\n특성 2: "+Upgrade(skillId,2);
            if (rank >= 4) text += "\n특성 4: "+Upgrade(skillId,4);
        }
        if (UnitCatalog.IsHero(heroType ?? uint.MaxValue)) text += "\n일꾼은 영웅 스킬 효과의 대상에서 제외됩니다.";
        return text;
    }
    public static string Upgrade(string id, int next)
    {
        if (id is "wolf-devour" or "golem-up" or "marshal-command" or "ranger-snipe")
            return next != 1 ? "최대 강화" : id switch
            {
                "wolf-devour" => "주변 8m 아군 미니언·용병 전체 포식 (일꾼 제외) · 가장 높은 체력 ×1 보호막·공격력 ×2 · 먹은 수만큼 강화 공격",
                "golem-up" => "다음 Q/W/E 2회 강화",
                "ranger-snipe" => "사거리 14m → 18m, 피해 20% 증가",
                _ => "아군 용병도 소집 가능 (일꾼 제외)"
            };
        if (next is < 1 or > 4) return "최대 강화";
        if (next % 2 == 1) return id switch
        {
            "wolf-drain" => $"흡혈 { (next == 1 ? 60 : 70) }%로 증가",
            "marshal-march" => $"진군 지속 시간 {(next == 1 ? 5 : 6)}초로 증가",
            "ranger-stride" => $"바람 걸음 지속 시간 +{(next == 1 ? 0.5 : 1)}초",
            _ => $"기본 피해·보호막의 {(next == 1 ? 20 : 40)}%만큼 증가 (누적)"
        };
        return (id,next) switch
        {
            ("wolf-bite",2) => "대상을 2초간 25% 둔화",
            ("wolf-bite",4) => "명중 시 자신의 최대 체력 5% 회복",
            ("wolf-drain",2) => "흡혈 명중 시 2초간 이동 속도 +20%",
            ("wolf-drain",4) => "흡혈 명중 시 최대 체력 5% 보호막, 4초",
            ("wolf-aura",2) => "매 타격마다 0.6초간 15% 둔화",
            ("wolf-aura",4) => "안개로 실제 깎은 체력의 20% 회복",
            ("golem-slam",2) => "맞은 적이 가하는 피해를 2초간 20% 감소",
            ("golem-slam",4) => "맞은 적을 0.5초 기절",
            ("golem-shell",2) => "보호막 유지 중 이동 속도 +20%",
            ("golem-shell",4) => "시전 시 최대 체력 8% 회복",
            ("golem-charge",2) => "명중 시 최대 체력 8% 보호막, 4초",
            ("golem-charge",4) => "명중 지점 3m 적을 2초간 30% 둔화",
            ("marshal-shot",2) => "대상을 2초간 25% 둔화",
            ("marshal-shot",4) => "대상이 가하는 피해를 3초간 20% 감소",
            ("marshal-bulwark",2) => "보호막을 받은 대상의 이동 속도 +15%, 3초",
            ("marshal-bulwark",4) => "보호막을 받은 대상의 체력을 시전자 최대 체력 2%만큼 회복",
            ("marshal-march",2) => "진군 중 받는 피해 10% 감소",
            ("marshal-march",4) => "진군을 받은 대상의 체력을 시전자 최대 체력 2%만큼 회복",
            ("ranger-pierce",2) => "대상을 2초간 25% 둔화",
            ("ranger-pierce",4) => "대상이 받는 피해를 3초간 10% 증가",
            ("ranger-stride",2) => "바람 걸음 동안 받는 피해 15% 감소",
            ("ranger-stride",4) => "시전 시 최대 체력 6% 보호막, 3초",
            ("ranger-volley",2) => "맞은 적이 가하는 피해를 2초간 15% 감소",
            ("ranger-volley",4) => "범위 3m → 4m",
            _ => ""
        };
    }
}
