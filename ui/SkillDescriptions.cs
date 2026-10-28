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
            return Lang.T("P · 허기")+"\n"+Lang.F("공격을 쉬는 동안 초당 공격력 +{0}%, 최대 +{1}%.\n일반 공격 또는 Q 명중 시 초기화.",rate,maximum)+
                (rank >= 4 ? "\n"+Lang.T("최대 중첩 명중 시 대상 최대 체력 10% 추가 피해.\nQ 배율은 추가 피해에 곱하지 않으며, 대상 피해 감소·보호막이 적용됩니다.") : "")+
                "\n"+Lang.T("일꾼 공격에는 적용되지 않으며 중첩을 소모하지 않습니다.");
        }
        if (heroType == UnitCatalog.HeroGolem)
            return Lang.T("P · 돌가죽")+"\n"+Lang.F("받는 피해 {0}% 감소.",rank >= 3 ? 15 : rank >= 1 ? 12 : 10)+
                (rank >= 2 ? "\n"+Lang.T("기절·속박 지속 시간 20% 감소.") : "")+
                (rank >= 4 ? "\n"+Lang.T("체력이 30% 이하이면 받는 피해 감소가 총 25%로 증가.") : "");
        if (heroType == UnitCatalog.HeroMarshal)
        {
            string amount=rank >= 3 ? "1" : rank >= 1 ? "0.7" : "0.5";
            return Lang.T("P · 전선 지휘")+"\n"+(rank >= 4
                    ? Lang.F("주변 5m 아군 미니언·병력·영웅에게 공격 피해 +{0}, 받는 피해 −{0}.",amount)
                    : Lang.F("주변 5m 아군 미니언·병력에게 공격 피해 +{0}, 받는 피해 −{0}.",amount))+
                (rank >= 2 ? "\n"+Lang.T("오라 대상 이동 속도 +10%.") : "")+"\n"+Lang.T("자신과 일꾼에게는 적용되지 않으며 같은 오라는 중첩되지 않습니다.");
        }
        if (heroType == UnitCatalog.HeroRanger)
        {
            int percent=rank switch { 0=>6,1 or 2=>7,_=>8 };
            int maximum=rank >= 2 ? 5 : 4;
            return Lang.T("P · 집중 사격")+"\n"+
                Lang.F("같은 대상을 연달아 맞힐 때마다 그 대상에게 주는 피해 +{0}%, 최대 {1}중첩.\n일반 공격과 Q 명중이 쌓고, 대상을 바꾸거나 3초 동안 쏘지 않으면 초기화.",percent,maximum)+
                (rank >= 4 ? "\n"+Lang.T("최대 중첩 명중 시 대상 이동 속도 20% 감소, 1초.") : "");
        }
        return "";
    }
    public static string PassiveUpgrade(uint? heroType,int next)
    {
        if (next is < 1 or > 4) return Lang.T("최대 강화");
        return (heroType,next) switch
        {
            (UnitCatalog.HeroTest,1) => Lang.T("초당 공격력 +6%, 최대 +60% (최대 도달 10초)"),
            (UnitCatalog.HeroTest,2) => Lang.T("초당 공격력 +12%, 최대 +60% (최대 도달 5초)"),
            (UnitCatalog.HeroTest,3) => Lang.T("초당 공격력 +14%, 최대 +70% (최대 도달 5초)"),
            (UnitCatalog.HeroTest,4) => Lang.T("최대 중첩의 일반 공격·Q 명중 시 대상 최대 체력 10% 추가 피해"),
            (UnitCatalog.HeroGolem,1) => Lang.T("받는 피해 감소 10% → 12%"),
            (UnitCatalog.HeroGolem,2) => Lang.T("기절·속박 지속 시간 20% 감소"),
            (UnitCatalog.HeroGolem,3) => Lang.T("받는 피해 감소 12% → 15%"),
            (UnitCatalog.HeroGolem,4) => Lang.T("체력 30% 이하에서 받는 피해 감소 총 25%"),
            (UnitCatalog.HeroMarshal,1) => Lang.T("오라 공격 피해 +0.7, 받는 피해 −0.7"),
            (UnitCatalog.HeroMarshal,2) => Lang.T("오라 대상 이동 속도 +10%"),
            (UnitCatalog.HeroMarshal,3) => Lang.T("오라 공격 피해 +1, 받는 피해 −1"),
            (UnitCatalog.HeroMarshal,4) => Lang.T("주변 아군 영웅도 모든 패시브 오라 효과를 받음"),
            (UnitCatalog.HeroRanger,1) => Lang.T("중첩당 피해 +6% → +7%"),
            (UnitCatalog.HeroRanger,2) => Lang.T("최대 4중첩 → 5중첩"),
            (UnitCatalog.HeroRanger,3) => Lang.T("중첩당 피해 +7% → +8%"),
            (UnitCatalog.HeroRanger,4) => Lang.T("최대 중첩 명중 시 대상 이동 속도 20% 감소, 1초"),
            _ => ""
        };
    }
    public static string Active(uint? heroType, int slot, string skillId, bool empowered=false, int rank=0)
    {
        if (skillId == null && heroType == UnitCatalog.HeroTest && slot is >= 0 and < 4)
            skillId = new[] { "wolf-bite", "wolf-drain", "wolf-aura", "wolf-devour" }[slot];
        int steps=(rank+1)/2;
        float scale=1+.2f*steps;
        // The skill's name line, then what it does.
        (string name, string body) = skillId switch
        {
            "wolf-bite" => (Lang.T("Q · 뜯기"), Lang.F("적에게 돌진하여 공격력 {0:0}% 피해. 일반 공격 판정.\n사거리 8m · 재사용 7초",200*scale)),
            "wolf-drain" => (Lang.T("W · 피의 송곳니"),
                Lang.F("다음 일반 공격 또는 Q로 실제 깎은 체력의 {0}% 회복.\nE에는 적용되지 않음 · 명중까지 유지 · 종료 후 재사용 8초",50+10*steps)),
            "wolf-aura" => (Lang.T("E · 피의 안개"),
                Lang.F("주변 3.5m 적에게 즉시 1회, 이후 0.5초마다 10회 타격.\n매 타격 공격력 {0:0}% 피해 · 5초 지속 · 종료 후 재사용 10초",25*scale)),
            "wolf-devour" => rank > 0
                ? (Lang.T("R · 무리 포식"), Lang.T("주변 8m 아군 미니언·병력을 모두 먹습니다.\n가장 높은 최대 체력 ×1 보호막, 가장 높은 기본 공격력 ×2 추가 공격력.\n먹은 유닛 수만큼 일반 공격·Q 명중에 적용. 마지막 명중 후 남은 보호막도 종료.\n재사용 40초 · 재시전 시 이전 효과 교체"))
                : (Lang.T("R · 포식"), Lang.T("8m 안 아군 미니언·병력 중 1마리를 먹습니다.\n대상의 최대 체력 ×1 보호막, 기본 공격력 ×2 추가 공격력.\n다음 일반 공격·Q 명중 후 종료 · 재사용 40초")),
            "golem-slam" => empowered
                ? (Lang.T("Q · 강화 내려찍기"), Lang.F("주변 {0}m 적에게 공격력 {1:0}% 피해.\n룬 각성 소모 · 재사용 10초",5,150*scale))
                : (Lang.T("Q · 내려찍기"), Lang.F("주변 {0}m 적에게 공격력 {1:0}% 피해.\n2초 동안 이동 속도 35% 감소 · 재사용 10초",3.5f,120*scale)),
            "golem-shell" => (empowered ? Lang.T("W · 강화 돌갑옷") : Lang.T("W · 돌갑옷"),
                Lang.F("자신의 최대 체력 {0:0}% 보호막, {1}초 지속.\n재사용 13초 · 보호막 유지 중 재시전 불가",(empowered ? 40 : 25)*scale,empowered ? 4 : 5)),
            "golem-charge" => (empowered ? Lang.T("E · 강화 바위 돌진") : Lang.T("E · 바위 돌진"),
                Lang.F("적에게 돌진해 공격력 {0:0}% 피해와 {1}초 기절.\n사거리 7m · 장애물에 막힘 · 재사용 12초",100*scale,empowered ? 1 : .5f)),
            "golem-up" => (Lang.T("R · 룬 각성"),
                Lang.F("다음 Q/W/E {0}회를 강화. 사용하거나 사망할 때까지 유지.\nQ: 범위 5m, 기본 피해 150% · W: 기본 보호막 40% · E: 기절 1초\n재사용 40초 · 중첩되지 않음",rank > 0 ? 2 : 1)),
            "marshal-shot" => (Lang.T("Q · 집중 사격"), Lang.F("지정한 적에게 공격력 {0:0}% 피해. 소집 부대가 집중 공격.\n사거리 7m · 재사용 7초",150*scale)),
            "marshal-bulwark" => (Lang.T("W · 방벽"),
                Lang.F("자신에게 최대 체력 {0:0.#}%, 주변 5m 아군에게 자신의 최대 체력 {1:0.#}% 보호막.\n5초 지속 · 재사용 12초",10*scale,5*scale)),
            "marshal-march" => (Lang.T("E · 진군"),
                Lang.F("자신과 주변 5m 아군 미니언·병력에게 이동 속도 ×1.25, 공격 속도 ×1.2.\n{0}초 지속 · 재사용 12초",4+steps)),
            "ranger-pierce" => (Lang.T("Q · 관통 화살"),
                Lang.F("지정한 적에게 공격력 {0:0}% 피해. 일반 공격 판정이라 집중 사격을 쌓고 적용받음.\n사거리 8m · 재사용 8초",160*scale)),
            "ranger-stride" => (Lang.T("W · 바람 걸음"),
                Lang.F("{0:0.#}초 동안 이동 속도 ×1.35, {1:0.#}초 동안 공격 속도 ×1.3.\n재사용 12초",2.5f+.5f*steps,4+.5f*steps)),
            "ranger-volley" => (Lang.T("E · 화살비"),
                Lang.F("찍은 곳 {0}m 안 적에게 공격력 {1:0}% 피해, 2초 동안 이동 속도 30% 감소.\n사거리 8m · 재사용 11초",rank >= 4 ? 4 : 3,110*scale)),
            "ranger-snipe" => (Lang.T("R · 저격"),
                Lang.F("지정한 적에게 공격력 {0:0}% 피해.\n사거리 {1}m · 대상이 시야에 있어야 함 · 재사용 45초",350*scale,rank > 0 ? 18 : 14)),
            "marshal-command" => (Lang.T("R · 부대 소집"), rank > 0
                ? Lang.T("주변 6m의 아군 미니언·병력을 소집.\n30초 동안 따라 이동하며 지정한 적을 집중 공격. 종료 시 소집 유닛 소멸.\n재사용 60초")
                : Lang.T("주변 6m의 아군 미니언을 소집.\n30초 동안 따라 이동하며 지정한 적을 집중 공격. 종료 시 소집 유닛 소멸.\n재사용 60초")),
            _ => (skillId ?? "", (string)null)
        };
        string text=body == null ? name : name+"\n"+body;
        if (slot < 3)
        {
            if (rank >= 2) text += "\n"+Lang.F("특성 2: {0}",Upgrade(skillId,2));
            if (rank >= 4) text += "\n"+Lang.F("특성 4: {0}",Upgrade(skillId,4));
        }
        if (UnitCatalog.IsHero(heroType ?? uint.MaxValue)) text += "\n"+Lang.T("일꾼은 영웅 스킬 효과의 대상에서 제외됩니다.");
        return text;
    }
    public static string Upgrade(string id, int next)
    {
        if (id is "wolf-devour" or "golem-up" or "marshal-command" or "ranger-snipe")
            return next != 1 ? Lang.T("최대 강화") : id switch
            {
                "wolf-devour" => Lang.T("주변 8m 아군 미니언·병력 전체 포식 (일꾼 제외) · 가장 높은 체력 ×1 보호막·공격력 ×2 · 먹은 수만큼 강화 공격"),
                "golem-up" => Lang.T("다음 Q/W/E 2회 강화"),
                "ranger-snipe" => Lang.T("사거리 14m → 18m, 피해 20% 증가"),
                _ => Lang.T("아군 병력도 소집 가능 (일꾼 제외)")
            };
        if (next is < 1 or > 4) return Lang.T("최대 강화");
        if (next % 2 == 1) return id switch
        {
            "wolf-drain" => Lang.F("흡혈 {0}%로 증가",next == 1 ? 60 : 70),
            "marshal-march" => Lang.F("진군 지속 시간 {0}초로 증가",next == 1 ? 5 : 6),
            "ranger-stride" => Lang.F("바람 걸음 지속 시간 +{0}초",next == 1 ? 0.5 : 1),
            _ => Lang.F("기본 피해·보호막의 {0}%만큼 증가 (누적)",next == 1 ? 20 : 40)
        };
        return (id,next) switch
        {
            ("wolf-bite",2) => Lang.T("대상을 2초간 25% 둔화"),
            ("wolf-bite",4) => Lang.T("명중 시 자신의 최대 체력 5% 회복"),
            ("wolf-drain",2) => Lang.T("흡혈 명중 시 2초간 이동 속도 +20%"),
            ("wolf-drain",4) => Lang.T("흡혈 명중 시 최대 체력 5% 보호막, 4초"),
            ("wolf-aura",2) => Lang.T("매 타격마다 0.6초간 15% 둔화"),
            ("wolf-aura",4) => Lang.T("안개로 실제 깎은 체력의 20% 회복"),
            ("golem-slam",2) => Lang.T("맞은 적이 가하는 피해를 2초간 20% 감소"),
            ("golem-slam",4) => Lang.T("맞은 적을 0.5초 기절"),
            ("golem-shell",2) => Lang.T("보호막 유지 중 이동 속도 +20%"),
            ("golem-shell",4) => Lang.T("시전 시 최대 체력 8% 회복"),
            ("golem-charge",2) => Lang.T("명중 시 최대 체력 8% 보호막, 4초"),
            ("golem-charge",4) => Lang.T("명중 지점 3m 적을 2초간 30% 둔화"),
            ("marshal-shot",2) => Lang.T("대상을 2초간 25% 둔화"),
            ("marshal-shot",4) => Lang.T("대상이 가하는 피해를 3초간 20% 감소"),
            ("marshal-bulwark",2) => Lang.T("보호막을 받은 대상의 이동 속도 +15%, 3초"),
            ("marshal-bulwark",4) => Lang.T("보호막을 받은 대상의 체력을 시전자 최대 체력 2%만큼 회복"),
            ("marshal-march",2) => Lang.T("진군 중 받는 피해 10% 감소"),
            ("marshal-march",4) => Lang.T("진군을 받은 대상의 체력을 시전자 최대 체력 2%만큼 회복"),
            ("ranger-pierce",2) => Lang.T("대상을 2초간 25% 둔화"),
            ("ranger-pierce",4) => Lang.T("대상이 받는 피해를 3초간 10% 증가"),
            ("ranger-stride",2) => Lang.T("바람 걸음 동안 받는 피해 15% 감소"),
            ("ranger-stride",4) => Lang.T("시전 시 최대 체력 6% 보호막, 3초"),
            ("ranger-volley",2) => Lang.T("맞은 적이 가하는 피해를 2초간 15% 감소"),
            ("ranger-volley",4) => Lang.T("범위 3m → 4m"),
            _ => ""
        };
    }
}
