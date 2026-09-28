using System.Globalization;

// 유닛과 포탑이 공유하는 STATE 메시지.
public readonly record struct StateSnapshot(uint Id, UnitState State)
{
    public static bool TryParse(string[] parts, out StateSnapshot snapshot)
    {
        snapshot = default;
        if (parts.Length != 6 || parts[0] != "STATE" ||
            !uint.TryParse(parts[1], out uint id) || id == 0 ||
            !int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out int carrying) ||
            !uint.TryParse(parts[4], out uint focusId) || !uint.TryParse(parts[5], out uint sequence)) return false;
        UnitActivity? activity = parts[2] switch
        {
            "IDLE" => UnitActivity.Idle,
            "GATHER" => UnitActivity.Gather,
            "ATTACK" => UnitActivity.Attack,
            _ => null
        };
        if (!activity.HasValue) return false;
        snapshot = new StateSnapshot(id, new UnitState(activity.Value, carrying, focusId, sequence));
        return true;
    }
}
