using System.Globalization;

// 조합과 가격은 서버 스냅샷만 사용한다. 클라이언트는 구매 요청 후 새 리비전을 기다린다.
public readonly record struct MinionRulesSnapshot(int MaxSlots, int IntervalSeconds)
{
    public const int SupportedSlotLimit = 64;

    public static bool TryParse(string[] parts, out MinionRulesSnapshot snapshot)
    {
        snapshot = default;
        if (parts.Length != 3 || parts[0] != "MINION_RULES" ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int maximum) ||
            maximum is < 1 or > SupportedSlotLimit ||
            !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out int interval) || interval < 1) return false;
        snapshot = new(maximum, interval);
        return true;
    }
}

public readonly record struct MinionOptionSnapshot(uint UnitType, uint ResourceId, long ReplaceCost, long AddCost)
{
    public static bool TryParse(string[] parts, out MinionOptionSnapshot snapshot)
    {
        snapshot = default;
        if (parts.Length != 5 || parts[0] != "MINION_OPTION" ||
            !uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out uint type) || type is < 100 or >= 200 ||
            !uint.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out uint resource) ||
            !long.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out long replace) ||
            !long.TryParse(parts[4], NumberStyles.None, CultureInfo.InvariantCulture, out long add)) return false;
        snapshot = new(type, resource, replace, add);
        return true;
    }
}

public readonly record struct MinionLaneSnapshot(int LaneIndex, uint Revision, uint[] UnitTypes)
{
    public static bool TryParse(string[] parts, out MinionLaneSnapshot snapshot)
    {
        snapshot = default;
        if (parts.Length < 4 || parts[0] != "MINION_LANE" ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int lane) ||
            !uint.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out uint revision) ||
            !int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out int count) ||
            count > MinionRulesSnapshot.SupportedSlotLimit || parts.Length != 4 + count) return false;
        var types = new uint[count];
        for (int i = 0; i < count; i++)
            if (!uint.TryParse(parts[4 + i], NumberStyles.None, CultureInfo.InvariantCulture, out types[i]) ||
                types[i] is < 100 or >= 200) return false;
        snapshot = new(lane, revision, types);
        return true;
    }
}

public readonly record struct MinionWaveSnapshot(uint NextWaveTick)
{
    public static bool TryParse(string[] parts, out MinionWaveSnapshot snapshot)
    {
        snapshot = default;
        if (parts.Length != 2 || parts[0] != "MINION_WAVE" ||
            !uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out uint tick)) return false;
        snapshot = new(tick);
        return true;
    }
}
