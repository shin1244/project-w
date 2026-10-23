using System.Globalization;

// HEALING healer target: the ally a quack doctor or refueler mends now; target 0 while it mends nobody.
public readonly record struct HealingSnapshot(uint UnitId, uint TargetId)
{
    public static bool TryParse(string[] parts, out HealingSnapshot snapshot)
    {
        snapshot = default;
        if (parts.Length != 3 || parts[0] != "HEALING" ||
            !uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out uint unit) ||
            !uint.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out uint target) || unit == target) return false;
        snapshot = new(unit, target);
        return true;
    }
}
