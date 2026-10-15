using System.Globalization;

// MANA id current maximum: 영웅의 정수 마나. 체력처럼 그 영웅을 아는 편 모두에게 온다.
public readonly record struct ManaSnapshot(uint Id, float Current, float Maximum)
{
    public static bool TryParse(string[] parts, out ManaSnapshot snapshot)
    {
        snapshot = default;
        if (parts.Length != 4 || parts[0] != "MANA" ||
            !uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out uint id) || id == 0 ||
            !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float current) ||
            !float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float maximum) ||
            !float.IsFinite(current) || !float.IsFinite(maximum) || current < 0 || maximum <= 0) return false;
        snapshot = new(id, System.Math.Min(current, maximum), maximum);
        return true;
    }
}

// MANA_COST type slot cost: 영웅 스킬의 마나 비용. ABILITY 정의와 별도로 온다.
public readonly record struct ManaCostSnapshot(uint UnitType, int Slot, float Cost)
{
    public static bool TryParse(string[] parts, out ManaCostSnapshot snapshot)
    {
        snapshot = default;
        if (parts.Length != 4 || parts[0] != "MANA_COST" ||
            !uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out uint type) ||
            !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out int slot) || slot > 4 ||
            !float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float cost) ||
            !float.IsFinite(cost) || cost < 0) return false;
        snapshot = new(type, slot, cost);
        return true;
    }
}
