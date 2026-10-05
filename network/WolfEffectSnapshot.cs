using System.Globalization;

// WOLF id 다음일반공격흡혈(0|1) E종료틱(0이면 비활성).
public readonly record struct WolfEffectSnapshot(uint UnitId, bool DrainReady, uint AuraUntil)
{
    public static bool TryParse(string[] parts, out WolfEffectSnapshot snapshot)
    {
        snapshot = default;
        if (parts.Length != 4 || parts[0] != "WOLF" ||
            !uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out uint id) || id == 0 ||
            parts[2] is not ("0" or "1") ||
            !uint.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out uint until)) return false;
        snapshot = new(id, parts[2] == "1", until);
        return true;
    }
}
