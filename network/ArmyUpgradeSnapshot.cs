using System.Globalization;

// One forge research kind as the server reports it (ARMY_UPGRADE).
public readonly record struct ArmyResearchState(uint ForgeId, int TargetLevel, int Remaining, int Total, int NextWood, int NextOil,
    int NextSeconds)
{
    public bool Researching => TargetLevel != 0;
}

// MINION_UPGRADE_STEP unitType hpPerLevel attackPerLevel: lane minions gain these fixed amounts per
// forge research level; stepPercent in ARMY_UPGRADE applies to mercenaries only.
public readonly record struct MinionUpgradeStep(uint UnitType, float Hp, float Attack)
{
    public static bool TryParse(string[] p, out MinionUpgradeStep step)
    {
        step = default;
        if (p.Length != 4 || p[0] != "MINION_UPGRADE_STEP" || !WireNumber.UInt(p[1], out uint type) || !UnitCatalog.IsMinion(type) ||
            !float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float hp) || !float.IsFinite(hp) || hp < 0 ||
            !float.TryParse(p[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float attack) || !float.IsFinite(attack) || attack < 0) return false;
        step = new(type, hp, attack);
        return true;
    }
}

// ARMY_UPGRADE team attackLevel hpLevel stepPercent maxLevel, then ATTACK and HP:
// forgeID targetLevel remainingSeconds totalSeconds nextWood nextOil nextSeconds.
// MaxLevel 0 means no snapshot has arrived yet.
public readonly record struct ArmyUpgradeSnapshot(uint Team, int AttackLevel, int HpLevel, int StepPercent, int MaxLevel,
    ArmyResearchState Attack, ArmyResearchState Hp)
{
    public const int SupportedMaxLevel = 20;
    public const string AttackKind = "ATTACK", HpKind = "HP";

    public static ArmyUpgradeSnapshot Empty(uint team) => new(team, 0, 0, 0, 0, default, default);
    public int Level(bool attack) => attack ? AttackLevel : HpLevel;
    public ArmyResearchState Research(bool attack) => attack ? Attack : Hp;
    public bool ForgeBusy(uint forgeId) => Attack.Researching && Attack.ForgeId == forgeId || Hp.Researching && Hp.ForgeId == forgeId;

    public static bool TryParse(string[] p, out ArmyUpgradeSnapshot snapshot)
    {
        snapshot = default;
        if (p.Length != 20 || p[0] != "ARMY_UPGRADE" || !WireNumber.UInt(p[1], out uint team) || team is < 1 or > 2 ||
            !Int(p[5], out int max) || max is < 1 or > SupportedMaxLevel ||
            !Int(p[2], out int attack) || attack > max || !Int(p[3], out int hp) || hp > max || !Int(p[4], out int step) ||
            !TryResearch(p, 6, attack, max, out var attackResearch) || !TryResearch(p, 13, hp, max, out var hpResearch)) return false;
        snapshot = new(team, attack, hp, step, max, attackResearch, hpResearch);
        return true;
    }

    private static bool TryResearch(string[] p, int at, int level, int max, out ArmyResearchState state)
    {
        state = default;
        if (!WireNumber.UInt(p[at], out uint forge) || !Int(p[at + 1], out int target) || !Int(p[at + 2], out int remaining) ||
            !Int(p[at + 3], out int total) || !Int(p[at + 4], out int wood) || !Int(p[at + 5], out int oil) ||
            !Int(p[at + 6], out int seconds) || remaining > total ||
            (target == 0 ? forge != 0 || remaining != 0 || total != 0 : forge == 0 || target != level + 1 || total <= 0) ||
            (level >= max ? wood != 0 || oil != 0 || seconds != 0 : wood + oil <= 0 || seconds <= 0)) return false;
        state = new(forge, target, remaining, total, wood, oil, seconds);
        return true;
    }

    private static bool Int(string text, out int value) => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
}
