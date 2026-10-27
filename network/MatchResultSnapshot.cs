using System.Globalization;

public sealed record MatchResultSnapshot(uint Winner, string Reason, uint Seconds)
{
    public string Title(uint localTeam) => Winner == 0 ? Lang.T("무승부") : localTeam == 0 ? Lang.F("{0}팀 승리", Winner) :
        Winner == localTeam ? Lang.T("승리") : Lang.T("패배");
    public string Description => Reason switch
    {
        "BOTH_BASES" => Lang.T("두 팀의 본진이 동시에 파괴되었습니다."),
        "TIME_LIMIT" => Lang.T("제한 시간이 지나 경기가 종료되었습니다."),
        _ => Lang.F("{0}팀의 본진이 파괴되었습니다.", Winner == 1 ? 2 : 1)
    };

    public static bool TryParse(string[] parts, out MatchResultSnapshot result)
    {
        result = null;
        if (parts.Length != 4 || parts[0] != "MATCH_END" ||
            !uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out uint winner) || winner > 2 ||
            !uint.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out uint seconds)) return false;
        bool validReason = winner == 0 ? parts[2] is "BOTH_BASES" or "TIME_LIMIT" : parts[2] == "BASE_DESTROYED";
        if (!validReason) return false;
        result = new(winner, parts[2], seconds);
        return true;
    }
}
