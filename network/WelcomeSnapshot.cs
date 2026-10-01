using System.Globalization;

public enum PlayerRole { None, Commander, Hero }

public readonly record struct WelcomeSnapshot(uint PlayerId, uint Team, PlayerRole Role, uint? HeroType)
{
    public static bool TryParse(string[] parts, out WelcomeSnapshot snapshot)
    {
        snapshot = default;
        if (parts.Length is not (4 or 5) || parts[0] != "WELCOME" ||
            !uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out uint playerId) || playerId == 0 ||
            !uint.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out uint team) || team == 0) return false;
        if (parts.Length == 4 && parts[3] == "COMMANDER")
        {
            snapshot = new(playerId, team, PlayerRole.Commander, null);
            return true;
        }
        if (parts.Length == 5 && parts[3] == "HERO" &&
            uint.TryParse(parts[4], NumberStyles.None, CultureInfo.InvariantCulture, out uint heroType))
        {
            snapshot = new(playerId, team, PlayerRole.Hero, heroType);
            return true;
        }
        return false;
    }
}
