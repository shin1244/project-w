using System.Globalization;

// SKILL 시전자 슬롯 [대상ID | x z]. 효과 표시에는 시전자와 슬롯이 필요하다.
public readonly record struct SkillActivationSnapshot(uint CasterId, int Slot)
{
    public static bool TryParse(string[] parts, out SkillActivationSnapshot snapshot)
    {
        snapshot = default;
        if (parts.Length is < 3 or > 5 || parts[0] != "SKILL" ||
            !uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out uint caster) || caster == 0 ||
            !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out int slot) || slot > 4) return false;
        if (parts.Length == 4 && (!uint.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out uint target) || target == 0)) return false;
        if (parts.Length == 5 &&
            (!float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) || !float.IsFinite(x) ||
             !float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float z) || !float.IsFinite(z))) return false;
        snapshot = new(caster, slot);
        return true;
    }
}

public readonly record struct SkillCooldownSnapshot(uint CasterId, int Slot, uint ReadyTick)
{
    public static bool TryParse(string[] parts, out SkillCooldownSnapshot snapshot)
    {
        snapshot = default;
        if (parts.Length != 4 || parts[0] != "COOLDOWN" ||
            !uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out uint caster) || caster == 0 ||
            !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out int slot) || slot > 4 ||
            !uint.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out uint tick)) return false;
        snapshot = new(caster, slot, tick);
        return true;
    }
}
