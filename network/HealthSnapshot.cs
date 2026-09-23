using System.Globalization;

// HP id current maximum: 유닛과 건물이 같은 메시지를 사용합니다.
public readonly record struct HealthSnapshot(uint Id, float Current, float Maximum)
{
    public static bool TryParse(string[] parts, out HealthSnapshot snapshot)
    {
        snapshot = default;
        if (parts.Length != 4 || parts[0] != "HP" ||
            !uint.TryParse(parts[1], out uint id) || id == 0 ||
            !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float current) ||
            !float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float maximum) ||
            !float.IsFinite(current) || !float.IsFinite(maximum) || current < 0 || maximum <= 0)
            return false;
        snapshot = new HealthSnapshot(id, current, maximum);
        return true;
    }
}
