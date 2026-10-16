using System.Globalization;

// Visible hero mana is authoritative; never regenerate or spend it locally.
public readonly record struct ManaSnapshot(uint Id, int Current, int Maximum)
{
    public static bool TryParse(string[] parts, out ManaSnapshot snapshot)
    {
        snapshot = default;
        if (parts.Length != 4 || parts[0] != "MANA" ||
            !uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out uint id) || id == 0 ||
            !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out int current) ||
            !int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out int maximum) ||
            maximum <= 0 || current > maximum) return false;
        snapshot = new(id, current, maximum);
        return true;
    }
}

public readonly record struct ManaCostSnapshot(uint HeroType, int Slot, float Cost)
{
    public static bool TryParse(string[] parts, out ManaCostSnapshot snapshot)
    {
        snapshot = default;
        if (parts.Length != 4 || parts[0] != "MANA_COST" ||
            !uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out uint type) || !UnitCatalog.IsHero(type) ||
            !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out int slot) || slot is < 0 or > 3 ||
            !float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float cost) ||
            !float.IsFinite(cost) || cost < 0) return false;
        snapshot = new(type, slot, cost);
        return true;
    }
}
