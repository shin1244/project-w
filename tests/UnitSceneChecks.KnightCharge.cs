using Godot;
using System.Threading.Tasks;

public partial class UnitSceneChecks
{
    private async Task CheckKnightChargeSnapshots()
    {
        bool Parse(string line, out KnightChargeSnapshot state) => KnightChargeSnapshot.TryParse(line.Split(' '), out state);
        Check(Parse("KNIGHT_CHARGE 9400 0 0 0 0 0 0 0", out var locked) && !locked.IsReadyAt(100),
            "An unresearched wire-charge snapshot never implies readiness");
        foreach (string invalid in new[] {
            "KNIGHT_CHARGE 0 1 0 0 0 0 0 0", "KNIGHT_CHARGE 9400 2 0 0 0 0 0 0",
            "KNIGHT_CHARGE 9400 0 0 0 20 9401 1 2", "KNIGHT_CHARGE 9400 1 100 20 20 9401 1 2",
            "KNIGHT_CHARGE 9400 1 100 0 20 9400 1 2", "KNIGHT_CHARGE 9400 1 100 0 0 9401 1 2",
            "KNIGHT_CHARGE 9400 1 100 0 20 9401 NaN 2", "KNIGHT_CHARGE 9400 1 100 0 20 9401 1 Infinity",
            "KNIGHT_CHARGE 9400 1 100 0 20 9401 1", "KNIGHT_CHARGE 9400 1 -1 0 0 0 0 0" })
            Check(!Parse(invalid, out _), "Malformed charge timing, target, readiness or coordinates are rejected");

        Receive("UNIT 1 9400 7 -10 6 1");
        Receive("UNIT 2 9401 8 -10 -1 2");
        Units.TryGetUnit(9400, out Unit knight);
        Units.TryGetUnit(9401, out Unit target);
        Check(knight.KnightCharge == null, "A newly visible knight waits for its own charge snapshot");
        Receive("KNIGHT_CHARGE 9400 0 0 0 0 0 0 0");
        Check(knight.KnightCharge == locked, "Main routes authoritative charge state to the live knight");
        int changed = 0;
        knight.KnightChargeChanged += _ => changed++;
        Receive("KNIGHT_CHARGE 9400 1 0 0 0 0 0 0");
        Check(knight.KnightCharge.Value.IsReadyAt(knight.EffectTick), "Research completion can enable an existing knight");
        Receive("KNIGHT_CHARGE 9400 1 100 0 20 9401 -10 -1");
        var active = knight.KnightCharge.Value;
        Check(active.ActiveAt(0) && !active.IsReadyAt(0) && active.ActiveAt(10) && !active.ActiveAt(20),
            "Charge presentation uses bounded server ticks without moving the unit or predicting a new charge");
        Check(knight.KnightChargeTarget == target && knight.GlobalPosition.IsEqualApprox(new Vector3(-10, 0, 6)),
            "A charge packet resolves only its visible target and does not predict movement");
        var animation = knight.GetNode<SteamKnightAnimation>("SteamKnightAnimation");
        var cables = animation.GetNode<Node3D>("ChargeCables");
        Check(cables.Visible, "A live charge presents the visible server-approved wire target");
        int beforeDuplicate = changed;
        Receive("KNIGHT_CHARGE 9400 1 100 0 20 9401 -10 -1");
        Receive("KNIGHT_CHARGE 9400 1 100 0 20 9401 NaN -1");
        Check(changed == beforeDuplicate && knight.KnightCharge == active, "Duplicate or malformed packets cannot retrigger charge effects");
        Receive("KNIGHT_CHARGE 9401 1 100 0 20 9400 -10 6");
        Check(target.KnightCharge == null, "Charge state cannot attach to a rifleman or another unit type");

        Receive("STATE 9400 DASH 0 9401 0");
        target.ApplyServerPosition(-8, -1);
        knight._Process(0);
        Check((-knight.GetNode<Node3D>("Visual").GlobalBasis.Z).IsEqualApprox(Vector3.Forward),
            "The charging knight faces its fixed anchor instead of bending toward a moving target");
        Receive("HIDE 9401");
        Check(knight.KnightChargeTarget == null, "Hiding the target removes the wire target immediately");
        animation._Process(0);
        Check(!cables.Visible, "The wire cannot reveal a target that left visibility");
        Receive("KNIGHT_CHARGE 9400 1 100 0 0 0 0 0");
        Check(!knight.KnightCharge.Value.IsReadyAt(99) && knight.KnightCharge.Value.IsReadyAt(100),
            "An interrupted charge preserves its cooldown until the authoritative ready tick");
        Receive("HIDE 9400");
        Receive("KNIGHT_CHARGE 9400 1 0 0 0 0 0 0");
        Check(!Units.TryGetUnit(9400, out _), "Late charge packets cannot resurrect a hidden knight");
        Receive("UNIT 1 9400 7 -10 6 1");
        Receive("UNIT 2 9401 8 -10 -1 2");
        Units.TryGetUnit(9400, out knight);
        Check(knight.KnightCharge == null, "Visibility re-entry does not reuse a previous instance's charge");
        Units.HandleKnightCharge(active, 10);
        Check(knight.EffectTick == 10 && knight.KnightCharge.Value.ActiveAt(knight.EffectTick),
            "Joining during a charge restores its active state at the current server tick");
        animation = knight.GetNode<SteamKnightAnimation>("SteamKnightAnimation");
        cables = animation.GetNode<Node3D>("ChargeCables");
        Check(cables.Visible && !animation.GetNode<AudioStreamPlayer3D>("PressureRelease").Playing,
            "A late active snapshot restores its wire without replaying the launch sound");
        knight.EffectTick = active.EndTick;
        animation._Process(0);
        Check(!cables.Visible, "The server watchdog expiration clears stale charge presentation");
        knight.Initialize(knight.UnitId, knight.OwnerId, 2);
        Check(knight.KnightCharge == null, "Identity or team changes discard previous charge readiness and effects");
        knight.Initialize(knight.UnitId, knight.OwnerId, 1);
        Units.HandleKnightCharge(active, 10);
        knight.BeginDeath();
        Check(knight.KnightCharge == null, "Death clears charge effects before the corpse animation runs");
        Check(!cables.Visible && !animation.GetNode<AudioStreamPlayer3D>("PressureRelease").Playing,
            "Death clears the wire and stops pressure-release audio immediately");
        knight.ApplyKnightCharge(active);
        Check(knight.KnightCharge == null, "Late charge updates cannot reactivate a dead knight");
        Receive("HIDE 9400"); Receive("HIDE 9401");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
}
