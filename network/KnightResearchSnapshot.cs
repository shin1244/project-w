using System.Globalization;

// KNIGHT_RESEARCH team unlocked labID remaining total wood oil researchSeconds range speedMultiplier cooldownSeconds.
public readonly record struct KnightResearchSnapshot(uint Team, bool Unlocked, uint LabId, double Remaining,
    double Total, int Wood, int Oil, double ResearchSeconds, double TriggerRange, double SpeedMultiplier, double CooldownSeconds)
{
    public bool Researching => LabId != 0;
    public double Percent => Total > 0 ? 100 * (1 - Remaining / Total) : 0;

    public static bool TryParse(string[] p, out KnightResearchSnapshot snapshot)
    {
        snapshot = default;
        if (p.Length != 12 || p[0] != "KNIGHT_RESEARCH" ||
            !WireNumber.UInt(p[1], out uint team) || team is < 1 or > 2 || p[2] is not ("0" or "1") ||
            !WireNumber.UInt(p[3], out uint lab) || !Number(p[4], out double remaining) ||
            !Number(p[5], out double total) || remaining > total ||
            !int.TryParse(p[6], NumberStyles.None, CultureInfo.InvariantCulture, out int wood) ||
            !int.TryParse(p[7], NumberStyles.None, CultureInfo.InvariantCulture, out int oil) ||
            !Number(p[8], out double seconds) || seconds <= 0 ||
            !Number(p[9], out double range) || range <= 0 ||
            !Number(p[10], out double speed) || speed <= 1 ||
            !Number(p[11], out double cooldown) || cooldown <= 0 ||
            (lab == 0 ? remaining != 0 || total != 0 : total <= 0 || p[2] == "1")) return false;
        snapshot = new(team, p[2] == "1", lab, remaining, total, wood, oil, seconds, range, speed, cooldown);
        return true;
    }

    private static bool Number(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value) && value >= 0;
}
