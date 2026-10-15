using Godot;
using System.Linq;
using System.Threading.Tasks;

public partial class UnitSceneChecks
{
    private async Task CheckWorkshopSnapshots()
    {
        Receive("UNIT 4 9500 7 -8 3 1");
        Receive("UNIT 5 9501 7 -7 3 1");
        Receive("UNIT 1 9502 8 -6 3 2");
        Units.TryGetUnit(9500, out Unit flame);
        Units.TryGetUnit(9501, out Unit artillery);
        var fire = flame.GetNode<Node3D>("WorkshopMachineVisual/MuzzleFire");
        var artilleryFire = artillery.GetNode<Node3D>("WorkshopMachineVisual/MuzzleFire");
        var motion = artillery.GetNode<WorkshopMachineVisual>("WorkshopMachineVisual");
        var pressureSteam = motion.GetNode<MultiMeshInstance3D>("PressureSteam");
        MultiMesh cloudPool = pressureSteam.Multimesh;
        int cloudCount = cloudPool.InstanceCount;
        var hull = artillery.GetNode<Node3D>("Visual/Rig/Hull");
        var vents = hull.GetNode<Node3D>("PressureVents").GetChildren().OfType<Node3D>().ToArray();
        var legs = artillery.GetNode<Node3D>("Visual/Rig/Legs").GetChildren().OfType<Node3D>().ToArray();
        var smallMuzzle = hull.GetNode<Node3D>("MobileTurret/Barrel/Tube/Muzzle");
        var largeMuzzle = hull.GetNode<Node3D>("MainTurret/Barrel/Tube/Muzzle");
        float mobileHeight = hull.Position.Y;
        int Shells() => artillery.GetChildren().OfType<WorkshopShellFlight>().Count(shell => !shell.IsQueuedForDeletion());
        Receive("SIEGE_STATE 9501 DEPLOYED 0 0");
        float deployedHeight = hull.Position.Y;
        Check(deployedHeight < mobileHeight && legs.All(leg =>
        {
            float bottom = leg.GetNode<Node3D>("Foot").GetChildren().OfType<MeshInstance3D>()
                .Min(mesh => (mesh.GlobalTransform * mesh.GetAabb().Position).Y);
            return bottom >= -.005f && bottom <= .025f;
        }), "A deployed snapshot lowers the hull and grounds every supporting pad without sinking or floating");
        Check(!pressureSteam.Visible && vents.Length > 0 && vents.All(vent => vent.GlobalPosition.Y > 0),
            "Stable siege emits no pressure burst and leaves every lower vent above the ground");
        Receive("STATE 9500 ATTACK 0 9502 70");
        Receive("STATE 9501 ATTACK 0 9502 80");
        Check(!fire.Visible && Shells() == 0, "Initial machine snapshots do not replay historical flame or artillery shots");
        Receive("STATE 9500 ATTACK 0 9502 71");
        Receive("STATE 9501 ATTACK 0 9502 81");
        Check(fire.Visible && Shells() == 1, "New authoritative swing numbers trigger the actual machine controllers once");
        Check(artillery.GetChildren().OfType<WorkshopShellFlight>().Single().GlobalPosition.IsEqualApprox(largeMuzzle.GlobalPosition)
            && artilleryFire.GlobalPosition.IsEqualApprox(largeMuzzle.GlobalPosition),
            "A deployed shot and its flash both start at the aimed large gun muzzle");
        Receive("STATE 9501 ATTACK 0 9502 81");
        Check(Shells() == 1, "Duplicate artillery snapshots do not duplicate a projectile");

        Units.TryGetUnit(9502, out Unit target);
        var mainTurret = hull.GetNode<Node3D>("MainTurret");
        var pinion = hull.GetNode<Node3D>("TraversePinion");
        var bearing = hull.GetNode<Node3D>("TraverseBearing");
        var traverseRing = mainTurret.GetNode<Node3D>("TraverseRing");
        Basis ringLocalBasis = traverseRing.Basis;
        Transform3D fixedHull = hull.GlobalTransform, fixedBearing = bearing.GlobalTransform;
        Transform3D[] fixedLegs = legs.Select(leg => leg.GlobalTransform).ToArray();
        Vector3 originalTarget = target.GlobalPosition;
        float previousYaw = 0;
        foreach (int side in new[] { -1, 1 })
        {
            target.ApplyServerPosition(artillery.GlobalPosition.X + side * 2.5f, artillery.GlobalPosition.Z - 8);
            artillery._Process(0); motion._Process(0);
            Vector3 bore = -largeMuzzle.GlobalBasis.Z;
            bore.Y = 0;
            Vector3 toTarget = target.GlobalPosition - largeMuzzle.GlobalPosition;
            toTarget.Y = 0;
            Check(bore.Normalized().Dot(toTarget.Normalized()) > .9999f,
                "The deployed gun's actual bore tracks a lateral target from its own offset pivot");
            Check(hull.GlobalTransform.IsEqualApprox(fixedHull) && bearing.GlobalTransform.IsEqualApprox(fixedBearing)
                && legs.Select((leg, i) => leg.GlobalTransform.IsEqualApprox(fixedLegs[i])).All(same => same),
                "Traversing the siege gun leaves the grounded hull, bearing and folded legs fixed");
            Check(hull.GetNode<Node3D>("MobileTurret").Rotation.IsEqualApprox(Vector3.Zero),
                "The mobile gun stays forward while the deployed siege gun tracks lateral targets");
            Check(Mathf.Sign(pinion.Rotation.Y) == -Mathf.Sign(mainTurret.Rotation.Y)
                && traverseRing.GlobalBasis.IsEqualApprox(mainTurret.GlobalBasis * ringLocalBasis),
                "The horizontal carriage ring follows the gun and the meshing pinion turns the opposite way");
            if (side == 1) Check(previousYaw * mainTurret.Rotation.Y < 0, "Opposite lateral targets turn the gun to opposite sides");
            previousYaw = mainTurret.Rotation.Y;
        }
        target.ApplyServerPosition(originalTarget.X, originalTarget.Z);
        artillery._Process(0); motion._Process(0);

        artillery.EffectTick = 120;
        Receive("SIEGE_STATE 9501 DEPLOYING 100 140");
        Check(pressureSteam.Visible && hull.GetNode<Node3D>("MobileTurret").Rotation.IsEqualApprox(Vector3.Zero),
            "An active deployment emits lower pressure steam while the mobile gun remains neutral");
        artillery.Hide(); motion._Process(0);
        Check(!pressureSteam.Visible, "An invisible machine immediately clears its lower pressure steam");
        artillery.Show(); motion._Process(0);
        Check(pressureSteam.Visible, "A visible machine reconstructs pressure steam from the current transition tick");
        Vector3 halfwayHull = hull.Position;
        Vector3[] halfwayLegs = legs.Select(leg => leg.Rotation).ToArray();
        Check(!artilleryFire.Visible && legs.All(leg => leg.GetNode<Node3D>("Foot").GlobalPosition.Y >= 0),
            "Beginning a mode change clears the old muzzle flash and keeps folding feet above ground");
        Receive("STATE 9501 ATTACK 0 9502 82");
        Check(Shells() == 1 && !artilleryFire.Visible, "A delayed attack state cannot fire while the artillery changes mode");
        Receive("SIEGE_STATE 9501 PACKING 100 140");
        Check(hull.Position.IsEqualApprox(halfwayHull) && legs.Select((leg, i) => leg.Rotation.IsEqualApprox(halfwayLegs[i])).All(same => same),
            "Deployment and packing at the same authoritative fraction reproduce the same rig pose");
        Check(pressureSteam.Visible && pressureSteam.Multimesh == cloudPool && cloudPool.InstanceCount == cloudCount,
            "Packing reuses the same bounded cloud pool rather than allocating another burst");
        Receive("SIEGE_STATE 9501 DEPLOYED 0 0");
        Check(!pressureSteam.Visible, "A stable-mode snapshot immediately clears an ongoing pressure burst");
        Receive("SIEGE_STATE 9501 PACKING 100 140");

        artillery.EffectTick = 140;
        motion._Process(0);
        Check(!pressureSteam.Visible, "An expired transition stops venting even before its stable-mode message arrives");
        Receive("SIEGE_STATE 9501 DEPLOYING 100 140");
        Check(Mathf.IsEqualApprox(hull.Position.Y, deployedHeight),
            "Joining with an already completed transition reconstructs the final pose without replaying a tween");
        Check(!pressureSteam.Visible, "A late historical transition snapshot cannot replay pressure steam");
        Receive("SIEGE_STATE 9501 MOBILE 0 0");
        Check(Mathf.IsEqualApprox(hull.Position.Y, mobileHeight), "The mobile snapshot restores the raised hull immediately");
        target.ApplyServerPosition(artillery.GlobalPosition.X + 3, artillery.GlobalPosition.Z - 8);
        Receive("STATE 9501 ATTACK 0 9502 83");
        Check(Shells() == 2 && artillery.GetChildren().OfType<WorkshopShellFlight>().Last().GlobalPosition.IsEqualApprox(smallMuzzle.GlobalPosition)
            && artilleryFire.GlobalPosition.IsEqualApprox(smallMuzzle.GlobalPosition),
            "After packing, a new swing fires only from the small mobile turret");
        Vector3 mobileBore = -smallMuzzle.GlobalBasis.Z, mobileTarget = target.GlobalPosition - smallMuzzle.GlobalPosition;
        mobileBore.Y = mobileTarget.Y = 0;
        Check(mobileBore.Normalized().Dot(mobileTarget.Normalized()) > .9999f,
            "The mobile gun also aims from its own pivot instead of inheriting the siege gun's offset");
        foreach (string activity in new[] { "IDLE", "GUARD" })
        {
            Receive($"STATE 9501 {activity} 0 9502 83");
            Check(hull.GetNode<Node3D>("MobileTurret").Rotation.IsEqualApprox(Vector3.Zero),
                "A non-attacking mobile gun remains neutral even when the state contains a focus target");
        }
        Receive("STATE 9501 ATTACK 0 9502 83");
        Check(!hull.GetNode<Node3D>("MobileTurret").Rotation.IsEqualApprox(Vector3.Zero),
            "The small turret reacquires its target only when mobile attack resumes");

        artillery.ApplyServerPosition(artillery.GlobalPosition.X + .15f, artillery.GlobalPosition.Z);
        Vector3 hullFacing = -artillery.GetNode<Node3D>("Visual").GlobalBasis.Z;
        Vector3 aimFacing = -artillery.GetNode<Node3D>(artillery.AimNodePath).GlobalBasis.Z;
        Vector3 targetDirection = (artillery.ActionTarget.GlobalPosition - artillery.GlobalPosition).Normalized();
        Check(hullFacing.IsEqualApprox(Vector3.Right) && aimFacing.Dot(targetDirection) > .999f,
            "A pursuing mobile artillery faces its walking direction while the turret keeps aiming at its target");

        Receive("STATE 9501 IDLE 0 0 83");
        motion.SetProcess(false);
        // Ignore the original spawn jump, then render a small authoritative movement.
        motion._Process(.1);
        Vector3 footBefore = legs[0].GetNode<Node3D>("Foot").Position;
        artillery.ApplyServerPosition(artillery.GlobalPosition.X, artillery.GlobalPosition.Z - .15f);
        motion._Process(.1);
        Vector3 footAfter = legs[0].GetNode<Node3D>("Foot").Position;
        motion._Process(.1);
        Check(!footBefore.IsEqualApprox(footAfter) && footAfter.IsEqualApprox(legs[0].GetNode<Node3D>("Foot").Position),
            "Walking linkages advance with rendered movement and stop cycling when the unit stops");
        motion.SetProcess(true);
        Receive("HIDE 9502");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Check(!fire.Visible && Shells() == 0, "Hidden targets stop machine flame and discard cosmetic shells");
        artillery.EffectTick = 220;
        Receive("SIEGE_STATE 9501 DEPLOYING 200 240");
        Check(pressureSteam.Visible, "A later distinct transition can vent through the existing cloud pool");
        artillery.BeginDeath();
        Check(!pressureSteam.Visible, "Death immediately stops pressure steam during a mode transition");
        Receive("HIDE 9500");
        Receive("HIDE 9501");
        Receive("SIEGE_STATE 9501 MOBILE 0 0");
        Check(!Units.TryGetUnit(9501, out _), "Late siege state cannot resurrect a hidden machine");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
}
