using System.Globalization;

// TEAM_EXP team level current required totalCentis. Shared progress is delivered
// to that team's players and to spectators; the lifetime total continues at max level.
public readonly record struct TeamExperienceSnapshot(uint Team, int Level, long Current, long Required, long TotalCentis)
{
    public bool IsMaxLevel => Required == 0;
    public string TotalExperienceText => (TotalCentis / 100m).ToString("0.##", CultureInfo.InvariantCulture);

    public static bool TryParse(string[] parts, out TeamExperienceSnapshot snapshot)
    {
        snapshot = default;
        if (parts.Length != 6 || parts[0] != "TEAM_EXP" ||
            !uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out uint team) || team is not (1 or 2) ||
            !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out int level) || level < 1 ||
            !long.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out long current) ||
            !long.TryParse(parts[4], NumberStyles.None, CultureInfo.InvariantCulture, out long required) ||
            !long.TryParse(parts[5], NumberStyles.None, CultureInfo.InvariantCulture, out long totalCentis) ||
            (required == 0 ? current != 0 : current >= required)) return false;
        snapshot = new(team, level, current, required, totalCentis);
        return true;
    }
}
