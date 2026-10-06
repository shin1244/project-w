using System.Globalization;

// GOLEM id 다음일반스킬강화(0|1). 다음에 성공한 Q/W/E 하나에만 적용된다.
public readonly record struct GolemEffectSnapshot(uint UnitId, bool EmpowerReady)
{
    public static bool TryParse(string[] parts, out GolemEffectSnapshot snapshot)
    {
        snapshot = default;
        if (parts.Length != 3 || parts[0] != "GOLEM" ||
            !uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out uint id) || id == 0 ||
            parts[2] is not ("0" or "1")) return false;
        snapshot = new(id, parts[2] == "1");
        return true;
    }
}
