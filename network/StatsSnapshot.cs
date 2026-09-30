using System.Globalization;

// STATS id damage range interval speed sight. Unknown values remain absent until the server sends them.
public readonly record struct StatsSnapshot(uint Id, float Damage, float Range, float Interval, float Speed, float Sight)
{
    public static bool TryParse(string[] parts, out StatsSnapshot snapshot)
    {
        snapshot = default;
        if (parts.Length != 7 || parts[0] != "STATS" || !uint.TryParse(parts[1], out uint id) || id == 0) return false;
        var values = new float[5];
        for (int i = 0; i < values.Length; i++)
            if (!float.TryParse(parts[i + 2], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]) ||
                !float.IsFinite(values[i]) || values[i] < 0) return false;
        snapshot = new(id, values[0], values[1], values[2], values[3], values[4]);
        return true;
    }
}
