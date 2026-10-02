using System.Globalization;

// SHIELD id remaining: 유닛과 건물의 현재 보호막 합계. 0은 해제.
public readonly record struct ShieldSnapshot(uint Id, float Amount)
{
    public static bool TryParse(string[] parts, out ShieldSnapshot snapshot)
    {
        snapshot = default;
        if (parts.Length != 3 || parts[0] != "SHIELD" ||
            !uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out uint id) || id == 0 ||
            !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float amount) ||
            !float.IsFinite(amount) || amount < 0) return false;
        snapshot = new(id, amount);
        return true;
    }
}
