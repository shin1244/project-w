using Godot;
using System;

public partial class CommandPanelChecks
{
    private void CheckWorkshopUnits()
    {
        PlayerInput input = GetNode<PlayerInput>("PlayerInput");
        Receive("BUILDING 8 9300 1 -15 14 0");
        Receive("BUILDING 8 9301 2 15 14 0");
        Receive("BUILDING_VISION 9301 1");
        Receive("REQUIRES UNIT 4 8");
        Receive("REQUIRES UNIT 5 8");
        Receive("REQUIRES UNIT 6 8 9");
        SelectBuilding(9300);
        int before = _commands.Count;
        KeyStroke(input, Key.Q);
        Units.RequestTrain(UnitCatalog.FlameAutomaton);
        Check(_slots[7].Disabled && _slots[8].Disabled && _commands.Count == before,
            "Workshop production waits for authoritative training definitions");

        // Arbitrary terms prove that UI and validation follow the wire, not balance constants.
        Receive("TRAIN_DEF 4 8 37 11 2 13.5");
        Receive("TRAIN_DEF 5 8 61 23 4 19.5");
        Receive("STOCK 0 37");
        Receive("STOCK 2 10");
        Receive("SUPPLY 3 5");
        Check(_slots[7].Disabled && _slots[7].TooltipText.Contains("기름 11") && _slots[7].TooltipText.Contains("13.5초"),
            "Production shows server oil/time terms and locks insufficient oil");
        KeyStroke(input, Key.Q);
        Units.RequestTrain(UnitCatalog.FlameAutomaton);
        Check(_commands.Count == before, "Insufficient oil blocks both shortcut and direct production requests");
        Receive("STOCK 2 11");
        Receive("STOCK 0 36");
        Check(_slots[7].Disabled, "Insufficient wood also blocks workshop production");
        Receive("STOCK 0 37");
        Receive("SUPPLY 4 5");
        KeyStroke(input, Key.Q);
        Check(_slots[7].Disabled && _commands.Count == before, "Supply uses the server's queued population count");
        Receive("SUPPLY 3 5");
        TrainingDefinition accepted = Buildings.TrainingTerms(UnitCatalog.FlameAutomaton).Value;
        foreach (string invalid in new[] { "TRAIN_DEF 4 8 -1 0 0 1", "TRAIN_DEF 4 8 0 -1 0 1", "TRAIN_DEF 4 8 0 0 -1 1",
            "TRAIN_DEF 4 8 0 0 0 NaN", "TRAIN_DEF 4 8 0 0 0 Infinity", "TRAIN_DEF 4 8 0 0 0 0",
            "TRAIN_DEF 4 8 0 0 0 1 extra", "TRAIN_DEF 4 9 0 0 0 1", "TRAIN_DEF 999 8 0 0 0 1" }) Receive(invalid);
        Check(Buildings.TrainingTerms(UnitCatalog.FlameAutomaton) == accepted && !_slots[7].Disabled,
            "Malformed training definitions cannot partially replace accepted terms");
        long wire = _wire.Length;
        KeyStroke(input, Key.Q);
        Check(_commands.Count == before + 1 && _commands[^1] == "TRAIN 9300 4" &&
            System.Text.Encoding.UTF8.GetString(_wire.ToArray().AsSpan((int)wire)) == "TRAIN 9300 4" + System.Environment.NewLine &&
            Buildings.CommanderWood == 37 && Buildings.CommanderOil == 11 && Buildings.CommanderSupplyUsed == 3,
            "Workshop Q sends one framed TRAIN and does not optimistically spend resources");
        Receive("STOCK 0 61");
        Receive("STOCK 2 23");
        Receive("SUPPLY 1 5");
        KeyStroke(input, Key.W);
        Check(_commands[^1] == "TRAIN 9300 5", "Workshop W produces the second server-defined unit");
        Receive("TRAIN_DEF 6 8 29 7 1 8.5");
        before = _commands.Count;
        KeyStroke(input, Key.E);
        Check(_slots[9].Text.StartsWith(UnitCatalog.Name(UnitCatalog.Refueler)) && _slots[9].Disabled && _commands.Count == before,
            "The refueler waits for a standing research lab");
        Receive("BUILDING 9 9302 1 -25 14 0");
        Check(!_slots[9].Disabled && _slots[9].TooltipText.Contains("기름 7"), "A research lab and the server's terms open the refueler");
        KeyStroke(input, Key.E);
        Check(_commands[^1] == "TRAIN 9300 6", "Workshop E produces the refueler");
        Receive("QUEUE 9300 25 9401:4:7 9402:5:7");
        Check(Buildings.SelectedBuilding.ProductionQueue.Count == 2 && !_slots[3].Disabled,
            "Workshop queues accept both machine types and expose cancellation");
        KeyStroke(input, Key.Escape);
        Check(_commands[^1] == "CANCEL_TRAIN 9300 9401", "Workshop uses the existing paid-job cancellation command");
        Receive("QUEUE 9300 0");
        Receive("CONSTRUCTION 9300 50 7");
        before = _commands.Count;
        Units.RequestTrain(UnitCatalog.SteamArtillery);
        Check(_commands.Count == before, "An unfinished workshop cannot train machines");
        Receive("CONSTRUCTION 9300 100 7");
        Receive("QUEUE 9300 30 9401:4:7 9402:5:7 9403:4:7 9404:5:7 9405:4:7");
        KeyStroke(input, Key.Q);
        Check(_slots[7].Disabled && _slots[8].Disabled && _commands.Count == before, "A full workshop queue blocks both unit buttons");
        Receive("QUEUE 9300 0");
        Building workshop = Buildings.SelectedBuilding;
        Units.RequestContextOrder(null, new Vector3(-12, 0, 11));
        Check(_commands[^1] == "RALLY 9300 MOVE -12 11" && workshop.Rally == null,
            "Workshop supports rally requests and waits for its authoritative marker");
        Receive("RALLY 9300 MOVE -12 11");
        Check(workshop.Rally is { Enabled: true }, "Workshop accepts the server rally snapshot");
        SelectBuilding(9301);
        before = _commands.Count;
        Units.RequestTrain(UnitCatalog.FlameAutomaton);
        Check(_commands.Count == before && _slots[7].Text == "", "Enemy workshops expose no production controls");
        SelectBuilding(9300);
        Units.CommanderControl = false;
        Units.RequestTrain(UnitCatalog.FlameAutomaton);
        Check(_commands.Count == before, "Non-commanders cannot request machine production directly");
        Units.CommanderControl = true;

        Receive("UNIT 4 9200 7 -10 4 1");
        Receive("UNIT 5 9201 7 -8 4 1");
        Receive("UNIT 5 9202 7 -6 4 1");
        Receive("UNIT 5 9203 9 -4 4 1");
        Receive("UNIT 5 9204 8 6 4 2");
        Check(Units.TryGetUnit(9200, out Unit flame) && flame.UnitType == UnitCatalog.FlameAutomaton &&
            Units.SceneFor(flame.UnitType).ResourcePath == "res://units/FlameAutomaton.tscn" &&
            Units.TryGetUnit(9201, out Unit artillery) && artillery.UnitType == UnitCatalog.SteamArtillery &&
            Units.SceneFor(artillery.UnitType).ResourcePath == "res://units/SteamArtillery.tscn",
            "Server unit IDs instantiate both new machine scenes");
        Units.TryGetUnit(9201, out artillery);
        SelectUnit(9201);
        before = _commands.Count;
        KeyStroke(input, Key.Z);
        Check(_slots[1].Disabled && !Units.RequestSiege(true) && _commands.Count == before,
            "Unknown artillery state cannot be guessed as mobile");
        Receive("SIEGE_STATE 9201 MOBILE 0 0");
        Receive("SIEGE_STATE 9202 MOBILE 0 0");
        Receive("SIEGE_STATE 9203 MOBILE 0 0");
        Receive("SIEGE_STATE 9204 DEPLOYED 0 0");
        Receive("SIEGE_STATE 9200 DEPLOYED 0 0");
        Check(flame.Siege == null, "Other unit types ignore artillery-only state");
        SiegeSnapshot acceptedSiege = artillery.Siege.Value;
        foreach (string invalid in new[] { "SIEGE_STATE 9201 MOBILE 1 2", "SIEGE_STATE 9201 DEPLOYED 0 2",
            "SIEGE_STATE 9201 DEPLOYING 10 10", "SIEGE_STATE 9201 PACKING 20 10", "SIEGE_STATE 9201 UNKNOWN 0 0",
            "SIEGE_STATE 9201 MOBILE 0 0 extra", "SIEGE_STATE 9201 DEPLOYING -1 10" }) Receive(invalid);
        Check(artillery.Siege == acceptedSiege, "Malformed siege state preserves the last valid mode");
        SelectUnit(9202);
        Units.TryGetUnit(9201, out artillery);
        Units.ToggleSelection(artillery);
        Units.ToggleSelection(flame);
        Units.TryGetUnit(9203, out Unit allied);
        Units.ToggleSelection(allied);
        // Inspecting another player's unit clears control selection; rebuild the intended mixed group.
        SelectUnit(9202);
        Units.ToggleSelection(artillery);
        Units.ToggleSelection(flame);
        wire = _wire.Length;
        KeyStroke(input, Key.Z);
        Check(_commands[^1] == "SIEGE 1 9201 9202" && artillery.Siege == acceptedSiege &&
            System.Text.Encoding.UTF8.GetString(_wire.ToArray().AsSpan((int)wire)) == "SIEGE 1 9201 9202" + System.Environment.NewLine,
            "Mixed combat selection toggles only owned artillery once, with no optimistic mode change");
        SelectUnit(9201);
        Receive("SIEGE_STATE 9201 DEPLOYING 80 100");
        before = _commands.Count;
        KeyStroke(input, Key.Z);
        Check(_slots[1].Disabled && !Units.RequestSiege(false) && !Units.RequestSiege(true) && _commands.Count == before &&
            artillery.Siege.Value.DeploymentAt(90) == .5f,
            "Transitioning artillery cannot be retoggled and progress follows server ticks");
        Receive("SIEGE_STATE 9201 DEPLOYED 0 0");
        Receive("STATS 9201 71 17 2.7 0 9");
        KeyStroke(input, Key.Z);
        Check(_commands[^1] == "SIEGE 0 9201" && artillery.Siege.Value.Mode == SiegeMode.Deployed && artillery.Stats.Value.Range == 17,
            "Deployed mode offers packing and displays authoritative changed stats");
        Receive("SIEGE_STATE 9201 PACKING 110 150");
        Check(artillery.Siege.Value.DeploymentAt(130) == .5f && _slots[1].Disabled, "Packing follows its own server duration and locks toggling");
        Receive("SIEGE_STATE 9201 MOBILE 0 0");
        SelectUnit(101);
        Units.ToggleSelection(artillery);
        before = _commands.Count;
        KeyStroke(input, Key.Z);
        Check(Commands.IsBuildMenuOpen && _commands.Count == before,
            "Worker and artillery selection gives Z to construction without also sending SIEGE");
        Commands.CancelMenu();
        SelectUnit(9203);
        Check(!Units.RequestSiege(true), "Another player's allied artillery cannot be toggled");
        SelectUnit(9204);
        Check(!Units.RequestSiege(false) && _slots[1].Text == "", "Enemy mode remains inspectable without controls");
        SelectUnit(9201);
        Units.CommanderControl = false;
        Check(!Units.RequestSiege(true), "A non-commander cannot toggle owned artillery");
        Units.CommanderControl = true;
        Units.SetSpectator(true);
        Check(!Units.RequestSiege(true), "Spectators cannot send siege commands");
        Units.SetSpectator(false);
        SelectUnit(9201);
        Receive("HIDE 9201");
        Receive("SIEGE_STATE 9201 DEPLOYED 0 0");
        Check(!Units.TryGetUnit(9201, out _), "Late hidden-unit siege state cannot recreate a unit");
        Receive("UNIT 5 9201 7 -8 4 1");
        Units.TryGetUnit(9201, out artillery);
        Check(artillery.Siege == null, "Rediscovery starts without the previous siege mode");
        Receive("SIEGE_STATE 9201 DEPLOYED 0 0");
        Receive("UNIT 5 9201 9 -8 4 1");
        Check(artillery.Siege == null, "Changed ownership drops the previous siege state until a new snapshot");
        foreach (uint id in new uint[] { 9200, 9201, 9202, 9203, 9204 }) Receive($"HIDE {id}");
        Receive("REMOVE 9300");
        Receive("REMOVE 9301");
        SelectBuilding(201);
    }
}
