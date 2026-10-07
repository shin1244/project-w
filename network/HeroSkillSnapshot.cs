using System.Globalization;

// SKILL 시전자 슬롯 [대상ID | x z] [EMPOWERED]. 강화 여부는 상태 갱신 순서와 독립적이다.
public readonly record struct SkillActivationSnapshot(uint CasterId, int Slot, bool Empowered = false)
{
    public static bool TryParse(string[] parts, out SkillActivationSnapshot snapshot)
    {
        snapshot = default;
        bool empowered = parts.Length > 0 && parts[^1] == "EMPOWERED";
        int length = parts.Length - (empowered ? 1 : 0);
        if (length is < 3 or > 5 || parts[0] != "SKILL" ||
            !uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out uint caster) || caster == 0 ||
            !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out int slot) || slot > 4 ||
            empowered && slot > 2) return false;
        if (length == 4 && (!uint.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out uint target) || target == 0)) return false;
        if (length == 5 &&
            (!float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) || !float.IsFinite(x) ||
             !float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float z) || !float.IsFinite(z))) return false;
        snapshot = new(caster, slot, empowered);
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

// SKILL_ACTIVE 시전자 슬롯 실행중(0|1). 쿨다운이 시작되기 전의 실행 상태를 별도로 동기화한다.
public readonly record struct SkillActiveSnapshot(uint CasterId, int Slot, bool Active)
{
    public static bool TryParse(string[] parts, out SkillActiveSnapshot snapshot)
    {
        snapshot = default;
        if (parts.Length != 4 || parts[0] != "SKILL_ACTIVE" ||
            !uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out uint caster) || caster == 0 ||
            !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out int slot) || slot > 4 ||
            parts[3] is not ("0" or "1")) return false;
        snapshot = new(caster, slot, parts[3] == "1");
        return true;
    }
}
