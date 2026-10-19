using System;
using System.Globalization;

// TopTier: the server sends zero next-tier terms once the HQ reached its last tier, which
// may be below three (the demo stops at two). The old seven-field message only knew three.
public readonly record struct TeamTierSnapshot(uint Team, int Tier, uint HallId, int TargetTier, double Remaining, double Total,
    int NextWood = 0, int NextOil = 0, double NextSeconds = 0, bool TopTier = false)
{
    public bool Upgrading => TargetTier != 0;
    public bool HasUpgradeTerms => NextSeconds > 0;
    public double Percent => Total > 0 ? 100 * (1 - Remaining / Total) : 0;
    public static bool TryParse(string[] p, out TeamTierSnapshot snapshot)
    {
        snapshot = default;
        if (p.Length is not (7 or 10) || p[0] != "TEAM_TIER" ||
            !WireNumber.UInt(p[1], out uint team) || team is < 1 or > 2 ||
            !int.TryParse(p[2], NumberStyles.None, CultureInfo.InvariantCulture, out int tier) || tier is < 1 or > 3 ||
            !WireNumber.UInt(p[3], out uint hall) ||
            !int.TryParse(p[4], NumberStyles.None, CultureInfo.InvariantCulture, out int target) ||
            !double.TryParse(p[5], NumberStyles.Float, CultureInfo.InvariantCulture, out double remaining) || !double.IsFinite(remaining) || remaining < 0 ||
            !double.TryParse(p[6], NumberStyles.Float, CultureInfo.InvariantCulture, out double total) || !double.IsFinite(total) || total < remaining ||
            (target == 0 ? remaining != 0 || total != 0 : hall == 0 || target != tier + 1 || target > 3 || total <= 0)) return false;
        int nextWood = 0, nextOil = 0;
        double nextSeconds = 0;
        if (p.Length == 10 && (
            !int.TryParse(p[7], NumberStyles.None, CultureInfo.InvariantCulture, out nextWood) ||
            !int.TryParse(p[8], NumberStyles.None, CultureInfo.InvariantCulture, out nextOil) ||
            !double.TryParse(p[9], NumberStyles.Float, CultureInfo.InvariantCulture, out nextSeconds) || !double.IsFinite(nextSeconds) ||
            nextSeconds < 0 || (nextSeconds > 0 ? tier >= 3 : nextWood != 0 || nextOil != 0 || target != 0))) return false;
        // 이전 7필드 메시지도 진행 상황은 표시하되, 비용을 추측해서 업그레이드를 허용하지 않습니다.
        bool top = p.Length == 10 ? nextSeconds == 0 : tier >= 3;
        snapshot = new(team, tier, hall, target, remaining, total, nextWood, nextOil, nextSeconds, top);
        return true;
    }
}
