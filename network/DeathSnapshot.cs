using System.Globalization;

public enum DeathCause { None, Blade, Shot, Fire, Blast }

// DEATH id BLADE|SHOT|FIRE|BLAST: the weapon of the killing hit, sent once before the same tick's REMOVE.
// It only picks the death presentation; removal still follows REMOVE.
public readonly record struct DeathSnapshot(uint UnitId, DeathCause Cause)
{
    public static bool TryParse(string[] parts, out DeathSnapshot value)
    {
        value = default;
        if (parts.Length != 3 || parts[0] != "DEATH" ||
            !uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out uint id) || id == 0) return false;
        DeathCause cause = parts[2] switch
        {
            "BLADE" => DeathCause.Blade, "SHOT" => DeathCause.Shot, "FIRE" => DeathCause.Fire, "BLAST" => DeathCause.Blast,
            _ => DeathCause.None
        };
        if (cause == DeathCause.None) return false;
        value = new(id, cause);
        return true;
    }
}
