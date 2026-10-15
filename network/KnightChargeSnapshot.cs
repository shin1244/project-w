using Godot;
using System.Globalization;

// KNIGHT_CHARGE id enabled readyTick startTick endTick targetId anchorX anchorZ.
// Only the server starts/cancels movement. Ticks reconstruct presentation after visibility changes.
public readonly record struct KnightChargeSnapshot(uint UnitId, bool Enabled, uint ReadyTick,
    uint StartTick, uint EndTick, uint TargetId, Vector2 Anchor)
{
    public bool Charging => EndTick > StartTick;
    public bool ActiveAt(uint tick) => Charging && tick >= StartTick && tick < EndTick;
    public bool IsReadyAt(uint tick) => Enabled && !ActiveAt(tick) && tick >= ReadyTick;

    public static bool TryParse(string[] p, out KnightChargeSnapshot snapshot)
    {
        snapshot = default;
        if (p.Length != 9 || p[0] != "KNIGHT_CHARGE" || !UInt(p[1], out uint id) || id == 0 || p[2] is not ("0" or "1")
            || !UInt(p[3], out uint ready) || !UInt(p[4], out uint start) || !UInt(p[5], out uint end)
            || !UInt(p[6], out uint target) || !Float(p[7], out float x) || !Float(p[8], out float z)) return false;
        bool enabled = p[2] == "1";
        if (!enabled && (ready != 0 || start != 0 || end != 0 || target != 0 || x != 0 || z != 0)) return false;
        if (end == 0 ? start != 0 || target != 0 || x != 0 || z != 0
            : !enabled || end <= start || target == 0 || target == id || ready < start) return false;
        snapshot = new(id, enabled, ready, start, end, target, new Vector2(x, z));
        return true;
    }
    private static bool UInt(string value, out uint parsed) => uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out parsed);
    private static bool Float(string value, out float parsed) => float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) && float.IsFinite(parsed);
}
