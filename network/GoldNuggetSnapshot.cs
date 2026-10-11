using Godot;
using System.Globalization;

// NUGGET id x z gold xp credit capturer team start end. 정글몹이 떨어뜨린 공개 목표이며 수집 완료는 서버가 NUGGET_REMOVE로 확정한다.
// credit 1은 고목 수호자의 큰 금덩어리(수집한 팀에 미니언 무료권), 0은 가시 멧돼지의 작은 금덩어리다.
public readonly record struct GoldNuggetSnapshot(
    uint Id, Vector3 Position, uint Gold, uint Experience, bool Credit,
    uint CapturerId, uint CapturerTeam, uint ChannelStartTick, uint ChannelEndTick)
{
    public bool Channeling => CapturerId != 0;

    public float Progress(uint tick) => !Channeling || ChannelEndTick <= ChannelStartTick ? 0 :
        (float)Mathf.Clamp(((double)tick - ChannelStartTick) / (ChannelEndTick - ChannelStartTick), 0, 1);

    public float RemainingSeconds(uint tick) =>
        Channeling && ChannelEndTick > tick ? (ChannelEndTick - tick) / (float)InterpolationClock.TickRate : 0;

    public static bool TryParse(string[] p, out GoldNuggetSnapshot snapshot)
    {
        snapshot = default;
        if (p.Length != 11 || p[0] != "NUGGET" || !WireNumber.UInt(p[1], out uint id) || id == 0 ||
            !float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) || !float.IsFinite(x) ||
            !float.TryParse(p[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float z) || !float.IsFinite(z) ||
            !WireNumber.UInt(p[4], out uint gold) || !WireNumber.UInt(p[5], out uint xp) || p[6] is not ("0" or "1") ||
            !WireNumber.UInt(p[7], out uint capturer) || !WireNumber.UInt(p[8], out uint team) ||
            !WireNumber.UInt(p[9], out uint start) || !WireNumber.UInt(p[10], out uint end)) return false;
        if (capturer == 0 ? team != 0 || start != 0 || end != 0 : team is not (1 or 2) || end <= start) return false;
        snapshot = new(id, new Vector3(x, 0, z), gold, xp, p[6] == "1", capturer, team, start, end);
        return true;
    }
}
