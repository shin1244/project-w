using System.Globalization;

// 유닛과 포탑이 공유하는 STATE 메시지.
public readonly record struct StateSnapshot(uint Id, UnitState State)
{
    public static bool TryParse(string[] parts, out StateSnapshot snapshot)
    {
        snapshot = default;
        if (parts.Length is not (6 or 7) || parts[0] != "STATE" ||
            !uint.TryParse(parts[1], out uint id) || id == 0 ||
            !int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out int carrying) ||
            !uint.TryParse(parts[4], out uint focusId) || !uint.TryParse(parts[5], out uint sequence)) return false;
        // 이전 6필드 메시지는 나무 운반으로 읽습니다. 새 자원 번호는 STOCK과 같습니다.
        int carryType = 0;
        if (parts.Length == 7 && (!int.TryParse(parts[6], NumberStyles.None, CultureInfo.InvariantCulture, out carryType) ||
            (carryType != (int)ResourceType.Wood && carryType != (int)ResourceType.Oil))) return false;
        UnitActivity? activity = parts[2] switch
        {
            "IDLE" => UnitActivity.Idle,
            "GUARD" => UnitActivity.Guard,
            "HOLD" => UnitActivity.Hold,
            "GATHER" => UnitActivity.Gather,
            "ATTACK" => UnitActivity.Attack,
            "BUILD" => UnitActivity.Build,
            "DASH" => UnitActivity.Dash,
            "STUN" => UnitActivity.Stun,
            "CHANNEL" => UnitActivity.Channel,
            _ => null
        };
        if (!activity.HasValue) return false;
        snapshot = new StateSnapshot(id, new UnitState(activity.Value, carrying, focusId, sequence, (ResourceType)carryType));
        return true;
    }
}
