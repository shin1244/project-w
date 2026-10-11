using System;
using System.Globalization;

public readonly record struct TeamTierSnapshot(uint Team, int Tier, uint HallId, int TargetTier, double Remaining, double Total)
{
    public bool Upgrading => TargetTier != 0;
    public double Percent => Total > 0 ? 100 * (1 - Remaining / Total) : 0;
    public static bool TryParse(string[] p, out TeamTierSnapshot snapshot)
    {
        snapshot = default;
        if (p.Length != 7 || p[0] != "TEAM_TIER" ||
            !WireNumber.UInt(p[1], out uint team) || team is < 1 or > 2 ||
            !int.TryParse(p[2], NumberStyles.None, CultureInfo.InvariantCulture, out int tier) || tier is < 1 or > 3 ||
            !WireNumber.UInt(p[3], out uint hall) ||
            !int.TryParse(p[4], NumberStyles.None, CultureInfo.InvariantCulture, out int target) ||
            !double.TryParse(p[5], NumberStyles.Float, CultureInfo.InvariantCulture, out double remaining) || !double.IsFinite(remaining) || remaining < 0 ||
            !double.TryParse(p[6], NumberStyles.Float, CultureInfo.InvariantCulture, out double total) || !double.IsFinite(total) || total < remaining ||
            (target == 0 ? remaining != 0 || total != 0 : hall == 0 || target != tier + 1 || target > 3 || total <= 0)) return false;
        snapshot = new(team, tier, hall, target, remaining, total);
        return true;
    }
}
