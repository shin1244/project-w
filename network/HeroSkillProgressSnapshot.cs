using System.Globalization;

public readonly record struct HeroSkillProgressSnapshot(uint HeroType, int Level, int Points,
    bool UltimateLearned, int Q, int W, int E, int R, int Passive)
{
    public int Rank(int slot) => slot switch { 0 => Q, 1 => W, 2 => E, 3 => R, 4 => Passive, _ => 0 };
    public static bool TryParse(string[] parts, out HeroSkillProgressSnapshot snapshot)
    {
        snapshot = default;
        if (parts.Length != 10 || parts[0] != "HERO_SKILLS" ||
            !uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out uint type) ||
            type is not (UnitCatalog.HeroTest or UnitCatalog.HeroGolem or UnitCatalog.HeroMarshal)) return false;
        int[] values = new int[8];
        for (int i = 0; i < values.Length; i++)
            if (!int.TryParse(parts[i+2], NumberStyles.None, CultureInfo.InvariantCulture, out values[i])) return false;
        int level=values[0], points=values[1], learned=values[2], q=values[3], w=values[4], e=values[5], r=values[6], passive=values[7];
        if (level is < 1 or > 10 || learned > 1 || q > 4 || w > 4 || e > 4 || r > 1 || passive > 4 ||
            learned == 0 && r != 0 || learned == 1 && level < 5 || r > 0 && level < 10 ||
            points+learned+q+w+e+r+passive != level-1) return false;
        snapshot = new(type,level,points,learned == 1,q,w,e,r,passive);
        return true;
    }
}
