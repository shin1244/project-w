using Godot;
using System.Linq;

public partial class UnitSceneChecks
{
    private void CheckPneumaticReload(Unit shooter)
    {
        var animation = shooter.GetNode<PneumaticRifleAnimation>("PneumaticRifleAnimation");
        var rifle = shooter.GetNode<Node3D>("Visual/Rig/Rifle");
        var muzzle = rifle.GetNode<Node3D>("Muzzle");
        var leftHand = shooter.GetNode<Node3D>("Visual/Rig/LeftArm/Elbow/Hand");
        var rightHand = shooter.GetNode<Node3D>("Visual/Rig/RightArm/Elbow/Hand");
        var button = shooter.GetNode<Node3D>("Visual/Rig/Body/PumpButton");
        var burst = animation.GetNode<Node3D>("PneumaticAirBurst");
        var pellet = shooter.GetChildren().OfType<PneumaticPelletFlight>().Single();
        Check(pellet.GlobalPosition.DistanceTo(muzzle.GlobalPosition) < .001f && burst.Visible,
            "A new rifle swing starts its pellet and pressure burst at the displayed muzzle");
        int before = shooter.GetChildren().OfType<PneumaticPelletFlight>().Count();
        foreach (float interval in new[] { .25f, 1.15f })
        {
            shooter.Stats = new StatsSnapshot(shooter.UnitId, 10, 8, interval, 3, 10);
            uint sequence = shooter.State.SwingSequence + 1;
            shooter.ApplyServerState(shooter.State with { SwingSequence = sequence });
            float reload = interval * .52f;
            animation._Process(reload * .52f);
            Check(leftHand.GlobalPosition.DistanceTo(button.GlobalPosition) < .10f,
                "The support hand reaches the tank button after firing at the received attack cadence");
            Check(rightHand.GlobalPosition.DistanceTo(rifle.GetNode<Node3D>("RightGrip").GlobalPosition) < .035f,
                "The firing hand stays attached to the rifle while the other hand charges the tank");
            CheckHoseEndpoints(shooter);
            animation._Process(reload * .60f);
            Check(leftHand.GlobalPosition.DistanceTo(rifle.GetNode<Node3D>("LeftGrip").GlobalPosition) < .035f,
                "The support hand returns to its rifle grip before the next server attack interval");
            CheckHoseEndpoints(shooter);
        }
        int after = shooter.GetChildren().OfType<PneumaticPelletFlight>().Count();
        animation._Process(5);
        Check(after == before + 2 && shooter.GetChildren().OfType<PneumaticPelletFlight>().Count() == after && !burst.Visible,
            "Reload time cannot invent another shot and muzzle pressure expires without another state update");
        shooter.ApplyServerState(shooter.State with { Activity = UnitActivity.Idle, FocusId = 0 });
        Check(!burst.Visible && leftHand.GlobalPosition.DistanceTo(rifle.GetNode<Node3D>("LeftGrip").GlobalPosition) < .035f,
            "Stopping an attack clears the muzzle effect and restores the support hand");
    }

    private void CheckHoseEndpoints(Unit shooter)
    {
        var rig = shooter.GetNode<Node3D>("Visual/Rig");
        var hose = rig.GetNode<Node3D>("Hose");
        var first = (Node3D)hose.GetChild(0);
        var last = (Node3D)hose.GetChild(hose.GetChildCount() - 1);
        Vector3 start = first.ToGlobal(Vector3.Down * .5f);
        Vector3 end = last.ToGlobal(Vector3.Up * .5f);
        Check(start.DistanceTo(rig.GetNode<Node3D>("Body/HoseTankExit").GlobalPosition) < .002f &&
            end.DistanceTo(rig.GetNode<Node3D>("Rifle/HoseGunInlet").GlobalPosition) < .002f,
            "The pressure hose remains attached to tank and rifle during recoil and reload");
    }
}
