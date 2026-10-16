using System.Globalization;

public enum SiegeMode { Mobile, Deploying, Deployed, Packing }

// SIEGE_STATE id mode startTick endTick. Stable modes have zero timing fields.
public readonly record struct SiegeSnapshot(uint UnitId, SiegeMode Mode, uint StartTick, uint EndTick)
{
    public bool Transitioning => Mode is SiegeMode.Deploying or SiegeMode.Packing;
    public float DeploymentAt(uint tick)
    {
        if (!Transitioning) return Mode == SiegeMode.Deployed ? 1 : 0;
        float progress = tick <= StartTick ? 0 : tick >= EndTick ? 1 : (float)(tick - StartTick) / (EndTick - StartTick);
        return Mode == SiegeMode.Deploying ? progress : 1 - progress;
    }

    public static bool TryParse(string[] parts, out SiegeSnapshot snapshot)
    {
        snapshot = default;
        if (parts.Length != 5 || parts[0] != "SIEGE_STATE" ||
            !uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out uint id) || id == 0 ||
            !uint.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out uint start) ||
            !uint.TryParse(parts[4], NumberStyles.None, CultureInfo.InvariantCulture, out uint end)) return false;
        SiegeMode? mode = parts[2] switch
        {
            "MOBILE" => SiegeMode.Mobile, "DEPLOYING" => SiegeMode.Deploying,
            "DEPLOYED" => SiegeMode.Deployed, "PACKING" => SiegeMode.Packing, _ => null
        };
        if (mode == null || (mode is SiegeMode.Deploying or SiegeMode.Packing ? end <= start : start != 0 || end != 0)) return false;
        snapshot = new(id, mode.Value, start, end);
        return true;
    }
}
