using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class CommandPanelChecks
{
    // A fresh session so the research and tier messages below cannot leak into the earlier checks.
    private void CheckBuildingWork()
    {
        BeginSession(9, 1);
        Receive("BUILDING 0 9400 1 -20 20 0");
        Receive("BUILDING 8 9401 1 -10 20 0");
        Receive("BUILDING 5 9402 1 0 20 0");
        Receive("BUILDING 9 9403 1 10 20 0");
        Receive("BUILDING 8 9404 2 20 20 0");
        Receive("BUILDING 5 9405 2 20 10 0");
        Receive("BUILDING 1 9406 1 -20 10 0");
        foreach (uint id in new uint[] { 9400, 9401, 9402, 9403 })
            Check(WorkParts(id).Any(), $"Building {id} has gears, hammers or emitters to switch");
        ExpectWork("Workplaces stand still without production or research", 9400, 9401, 9402, 9403, 9404, 9405);
        Check(WorkParts(9406).OfType<CpuParticles3D>().All(smoke => smoke.Emitting),
            "Buildings that never produce or research keep their smoke");

        Receive("QUEUE 9400 10 9900:0:9");
        Receive("QUEUE 9401 0 9901:4:9");
        ExpectWork("Queued workers and machines start the hall and the workshop", 9402, 9403, 9404, 9405);
        Receive("QUEUE 9400 0");
        ExpectWork("An emptied queue stops the hall", 9400, 9402, 9403, 9404, 9405);
        Receive("QUEUE 9404 10 9902:4:20");
        ExpectWork("An enemy queue line cannot reveal enemy production", 9400, 9402, 9403, 9404, 9405);

        Receive("TEAM_TIER 1 1 9400 2 6.5 13 37 11 13");
        Receive("ARMY_UPGRADE 1 0 0 15 5 9402 1 30 45 300 120 45 0 0 0 0 300 120 45");
        Receive("KNIGHT_RESEARCH 1 0 9403 4.5 13.5 37 11 13.5 7.25 3.5 8.5");
        Receive("ARMY_UPGRADE 2 0 0 15 5 0 0 0 0 300 120 45 9405 1 30 45 300 120 45");
        ExpectWork("Hall upgrades and both research kinds start their own buildings, public enemy research included", 9404);
        Receive("TEAM_TIER 1 2 9400 0 0 0 0 0 0");
        Receive("ARMY_UPGRADE 1 1 0 15 5 0 0 0 0 300 120 45 0 0 0 0 300 120 45");
        Receive("KNIGHT_RESEARCH 1 1 0 0 0 37 11 13.5 7.25 3.5 8.5");
        ExpectWork("Finished upgrades and research stop their buildings", 9400, 9402, 9403, 9404);

        Receive("BUILDING 8 9401 2 -10 20 0");
        ExpectWork("A captured workshop drops our queue and stops", 9400, 9401, 9402, 9403, 9404);
        Receive("BUILDING 8 9401 1 -10 20 0");
        Receive("QUEUE 9401 0 9903:4:9");
        Receive("WELCOME 10 2 COMMANDER");
        ExpectWork("A new role forgets the previous team's queue and the research until fresh snapshots",
            9400, 9401, 9402, 9403, 9404, 9405);
        Receive("ARMY_UPGRADE 2 0 0 15 5 0 0 0 0 300 120 45 9405 1 30 45 300 120 45");
        ExpectWork("The fresh research snapshot restarts our new team's forge", 9400, 9401, 9402, 9403, 9404);
        Buildings.ResetTeamTiers();
        ExpectWork("Resetting team state stops research buildings", 9400, 9401, 9402, 9403, 9404, 9405);
    }

    private IEnumerable<Node> WorkParts(uint id)
    {
        Check(Buildings.TryGetBuilding(id, out Building building), $"Building {id} exists");
        return building.GetNode("Visual").FindChildren("*", "", true, false)
            .Where(part => part is SpinningPart or ReciprocatingPart or CpuParticles3D);
    }

    // Every listed workplace is idle and every other one works; the parts follow the building.
    private void ExpectWork(string message, params uint[] idle)
    {
        foreach (uint id in new uint[] { 9400, 9401, 9402, 9403, 9404, 9405 })
        {
            Check(Buildings.TryGetBuilding(id, out Building building), $"Building {id} exists");
            bool working = !idle.Contains(id);
            Check(building.IsWorking == working && WorkParts(id).All(part => part switch
            {
                SpinningPart gear => gear.Running == working,
                ReciprocatingPart hammer => hammer.Running == working,
                CpuParticles3D smoke => smoke.Emitting == working,
                _ => true
            }), $"{message} (building {id})");
        }
    }
}
