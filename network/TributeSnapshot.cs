using Godot;
using System.Globalization;

// 공개 목표의 전체 상태. 진행률은 표시용이며 수집 완료는 서버 응답으로만 확정한다.
public sealed record TributeSnapshot(
    uint EventId, bool Active, Vector3 Position, uint NextSpawnTick,
    uint CapturerId, uint CapturerTeam, uint ChannelStartTick, uint ChannelEndTick,
    uint Team1Count, uint Team2Count)
{
    public bool Channeling => Active && CapturerId != 0;
    public bool RamsMarching => !Active && NextSpawnTick == 0;

    public float Progress(uint tick) => !Channeling || ChannelEndTick <= ChannelStartTick ? 0 :
        (float)Mathf.Clamp(((double)tick - ChannelStartTick) / (ChannelEndTick - ChannelStartTick), 0, 1);

    public float RemainingSeconds(uint tick)
    {
        uint end = Active ? ChannelEndTick : NextSpawnTick;
        return end > tick ? (end - tick) / (float)InterpolationClock.TickRate : 0;
    }

    public static bool TryParse(string[] parts, out TributeSnapshot snapshot)
    {
        snapshot = null;
        if (parts.Length != 12 || parts[0] != "TRIBUTE" || parts[2] is not ("WAITING" or "ACTIVE") ||
            !UInt(parts[1], out uint id) ||
            !float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
            !float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float z) ||
            !float.IsFinite(x) || !float.IsFinite(z) ||
            !UInt(parts[5], out uint next) || !UInt(parts[6], out uint capturer) ||
            !UInt(parts[7], out uint team) || !UInt(parts[8], out uint start) ||
            !UInt(parts[9], out uint end) || !UInt(parts[10], out uint score1) ||
            !UInt(parts[11], out uint score2)) return false;
        bool active = parts[2] == "ACTIVE";
        if (active ? id == 0 || next != 0 : capturer != 0) return false;
        if (capturer == 0 ? team != 0 || start != 0 || end != 0 :
            team is not (1 or 2) || end <= start) return false;
        snapshot = new(id, active, new Vector3(x, 0, z), next, capturer, team, start, end, score1, score2);
        return true;
    }

    private static bool UInt(string value, out uint number)
        => uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out number);
}
