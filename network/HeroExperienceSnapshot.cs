using System.Globalization;

// EXP level current required: owner-only progress, independent of the respawned unit ID.
public readonly record struct HeroExperienceSnapshot(int Level, long Current, long Required)
{
    public bool IsMaxLevel => Required == 0;

    public static bool TryParse(string[] parts, out HeroExperienceSnapshot snapshot)
    {
        snapshot = default;
        if (parts.Length != 4 || parts[0] != "EXP" ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int level) || level < 1 ||
            !long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out long current) ||
            !long.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out long required) ||
            (required == 0 ? current != 0 : current >= required)) return false;
        snapshot = new(level, current, required);
        return true;
    }
}
