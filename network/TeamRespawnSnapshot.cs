using System.Globalization;

// TEAM_RESPAWN playerId tick: 같은 편 다른 플레이어의 영웅이 부활할 틱. 팀원 창이 남은 시간을 그린다.
public readonly record struct TeamRespawnSnapshot(uint PlayerId, uint Tick)
{
    public static bool TryParse(string[] parts, out TeamRespawnSnapshot snapshot)
    {
        snapshot = default;
        if (parts.Length != 3 || parts[0] != "TEAM_RESPAWN" ||
            !uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out uint player) || player == 0 ||
            !uint.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out uint tick)) return false;
        snapshot = new(player, tick);
        return true;
    }
}
