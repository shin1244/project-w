using System.Globalization;

// BUILD_DEF: the server's terms for one building a worker can build. Supply is what it adds once finished (0 for most).
public readonly record struct BuildingDefinition(uint BuildingType, long Wood, long Oil, double Seconds, long Supply)
{
    public static bool TryParse(string[] parts, out BuildingDefinition definition)
    {
        definition = default;
        if (parts.Length != 6 || parts[0] != "BUILD_DEF" ||
            !uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out uint building) || !BuildingCatalog.IsPlayerBuildable(building) ||
            !long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out long wood) ||
            !long.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out long oil) ||
            !double.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds) ||
            !double.IsFinite(seconds) || seconds <= 0 ||
            !long.TryParse(parts[5], NumberStyles.None, CultureInfo.InvariantCulture, out long supply)) return false;
        definition = new(building, wood, oil, seconds, supply);
        return true;
    }
}
