using Godot;
using System.Globalization;

public readonly record struct RallySnapshot(uint BuildingId, bool Enabled, Vector3 Position, uint ResourceId)
{
    public static bool TryParse(string[] parts, out RallySnapshot snapshot)
    {
        snapshot = default;
        if (parts.Length < 3 || parts[0] != "RALLY" || !Id(parts[1], out uint id)) return false;
        if (parts[2] == "CLEAR" && parts.Length == 3)
        {
            snapshot = new(id, false, Vector3.Zero, 0);
            return true;
        }
        bool gather = parts[2] == "GATHER";
        if ((!gather && parts[2] != "MOVE") || parts.Length != (gather ? 6 : 5) ||
            !Coordinate(parts[3], out float x) || !Coordinate(parts[4], out float z)) return false;
        uint resource = 0;
        if (gather && !Id(parts[5], out resource)) return false;
        snapshot = new(id, true, new Vector3(x, 0, z), resource);
        return true;
    }

    private static bool Id(string text, out uint value) =>
        uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value != 0;
    private static bool Coordinate(string text, out float value) =>
        float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && float.IsFinite(value);
}
