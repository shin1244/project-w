using System.Globalization;

// Targeted SKILL broadcast, including the caster (unlike the outbound command).
public readonly record struct TargetSkillSnapshot(uint CasterId, int Slot, uint TargetId)
{
    public static bool TryParse(string[] parts, out TargetSkillSnapshot snapshot)
    {
        snapshot = default;
        if (parts.Length != 4 || parts[0] != "SKILL" ||
            !uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out uint caster) || caster == 0 ||
            !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out int slot) || slot > 4 ||
            !uint.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out uint target) || target == 0) return false;
        snapshot = new(caster, slot, target);
        return true;
    }
}

public readonly record struct SkillCooldownSnapshot(int Slot, uint ReadyTick)
{
    public static bool TryParse(string[] parts, out SkillCooldownSnapshot snapshot)
    {
        snapshot = default;
        if (parts.Length != 3 || parts[0] != "COOLDOWN" ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int slot) || slot > 4 ||
            !uint.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out uint tick)) return false;
        snapshot = new(slot, tick);
        return true;
    }
}
