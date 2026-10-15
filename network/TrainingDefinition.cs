using System;
using System.Globalization;

public readonly record struct TrainingDefinition(uint UnitType, uint BuildingType, long Wood, long Oil, long Supply, double Seconds)
{
    public static bool TryParse(string[] parts, out TrainingDefinition definition)
    {
        definition = default;
        if (parts.Length != 7 || parts[0] != "TRAIN_DEF" ||
            !uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out uint unit) || !UnitCatalog.IsTrainable(unit) ||
            !uint.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out uint building) ||
            building is not (BuildingCatalog.TownHall or BuildingCatalog.Barracks or BuildingCatalog.Workshop) ||
            !long.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out long wood) ||
            !long.TryParse(parts[4], NumberStyles.None, CultureInfo.InvariantCulture, out long oil) ||
            !long.TryParse(parts[5], NumberStyles.None, CultureInfo.InvariantCulture, out long supply) ||
            !double.TryParse(parts[6], NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds) ||
            !double.IsFinite(seconds) || seconds <= 0) return false;
        definition = new(unit, building, wood, oil, supply, seconds);
        return true;
    }
}
