using Godot;
using System.Globalization;

// 충돌 연출은 명시적인 서버 이벤트에서만 재생한다. 피해/소멸은 HP/REMOVE를 따른다.
public readonly record struct RamImpactSnapshot(uint UnitId, Vector3 Position, uint Team)
{
    public static bool TryParse(string[] parts, out RamImpactSnapshot value)
    {
        value = default;
        if (parts.Length != 5 || parts[0] != "RAM_IMPACT" ||
            !uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out uint id) || id == 0 ||
            !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
            !float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float z) ||
            !float.IsFinite(x) || !float.IsFinite(z) ||
            !uint.TryParse(parts[4], NumberStyles.None, CultureInfo.InvariantCulture, out uint team) || team is not (1 or 2)) return false;
        value = new(id, new Vector3(x, 0, z), team);
        return true;
    }
}
