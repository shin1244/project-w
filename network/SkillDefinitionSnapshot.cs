using System;
using System.Globalization;
using System.Linq;

public enum SkillTargetMode { Enemy, Ally, Point, Self }

public readonly record struct SkillDefinitionSnapshot(uint UnitType, int Slot, string Id,
    SkillTargetMode Target, float Range, float Cooldown, bool MovesCaster)
{
    public static bool TryParse(string[] parts, out SkillDefinitionSnapshot value)
    {
        value = default;
        if (parts.Length != 9 || parts[0] != "ABILITY" || parts[1] != "UNIT" ||
            !uint.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out uint type) ||
            !int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out int slot) || slot > 4 ||
            parts[4].Length is < 1 or > 64 || !parts[4].All(c => char.IsAsciiLetterOrDigit(c) || c == '-') ||
            !float.TryParse(parts[6], NumberStyles.Float, CultureInfo.InvariantCulture, out float range) || !float.IsFinite(range) || range < 0 ||
            !float.TryParse(parts[7], NumberStyles.Float, CultureInfo.InvariantCulture, out float cooldown) || !float.IsFinite(cooldown) || cooldown < 0 ||
            parts[8] is not ("0" or "1")) return false;
        SkillTargetMode? mode = parts[5] switch
        {
            "ENEMY" => SkillTargetMode.Enemy, "ALLY" => SkillTargetMode.Ally,
            "POINT" => SkillTargetMode.Point, "SELF" => SkillTargetMode.Self, _ => null
        };
        if (!mode.HasValue) return false;
        value = new(type, slot, parts[4], mode.Value, range, cooldown, parts[8] == "1");
        return true;
    }
}

[Flags]
public enum ControlRestrictions : uint { None = 0, Stun = 1, Root = 2, Silence = 4, Disarm = 8 }

public readonly record struct ControlSnapshot(uint Id, ControlRestrictions Flags)
{
    public static bool TryParse(string[] parts, out ControlSnapshot snapshot)
    {
        snapshot = default;
        if (parts.Length != 3 || parts[0] != "CONTROL" ||
            !uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out uint id) || id == 0 ||
            !uint.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out uint flags) || flags > 15) return false;
        snapshot = new(id, (ControlRestrictions)flags);
        return true;
    }
}
