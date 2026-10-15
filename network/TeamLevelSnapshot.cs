using System.Globalization;

// Public level only. Opponents' detailed TEAM_EXP remains private.
public readonly record struct TeamLevelSnapshot(uint Team, int Level)
{
    public static bool TryParse(string[] parts, out TeamLevelSnapshot snapshot)
    {
        snapshot = default;
        if (parts.Length != 3 || parts[0] != "TEAM_LEVEL" ||
            !uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out uint team) || team is not (1 or 2) ||
            !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out int level) || level < 1) return false;
        snapshot = new(team, level);
        return true;
    }
}
