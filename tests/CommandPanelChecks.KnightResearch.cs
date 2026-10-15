using Godot;
using System;
using System.Linq;
using System.Text;

public partial class CommandPanelChecks
{
    private void CheckKnightResearch()
    {
        const string idle = "KNIGHT_RESEARCH 1 0 0 0 0 37 11 13.5 7.25 3.5 8.5";
        const string complete = "KNIGHT_RESEARCH 1 1 0 0 0 37 11 13.5 7.25 3.5 8.5";
        PlayerInput input = GetNode<PlayerInput>("PlayerInput");
        var details = GetNode<SelectionDetails>("SelectionUI/SelectionDetails");
        var extra = details.FindChildren("Extra", "Label", true, false).OfType<Label>().Single();
        var progress = details.FindChildren("ProductionProgress", "ProgressBar", true, false).OfType<ProgressBar>().Single();
        Receive("BUILDING 9 9600 1 -15 14 0");
        Receive("BUILDING 9 9601 1 -12 14 0");
        Receive("BUILDING 9 9602 2 15 14 0");
        Receive("BUILDING_VISION 9602 1");
        SelectBuilding(9600);
        int before = _commands.Count;
        KeyStroke(input, Key.Q);
        Units.RequestKnightResearch();
        Check(_slots[7].Disabled && _slots[7].TooltipText.Contains("정보 수신 대기") && _commands.Count == before,
            "Laboratories cannot guess research terms before an authoritative snapshot");

        Receive(idle);
        Receive("REQUIRES_TIER BUILDING 9 2");
        Receive("STOCK 0 37");
        Receive("STOCK 2 11");
        Receive("TEAM_TIER 1 1 201 2 4 9");
        Receive("TEAM_TIER 2 3 203 0 0 0");
        KeyStroke(input, Key.Q);
        Check(_slots[7].Disabled && _slots[7].TooltipText.Contains("회관 2티어 완료") && _commands.Count == before,
            "An upgrading hall or the enemy tier cannot unlock charge research");
        Receive("TEAM_TIER 1 2 201 0 0 0");
        Receive("STOCK 0 36");
        KeyStroke(input, Key.Q);
        Check(_slots[7].Disabled && _slots[7].TooltipText.Contains("목재 부족") && _commands.Count == before,
            "Insufficient wood blocks charge research");
        Receive("STOCK 0 37");
        Receive("STOCK 2 10");
        Units.RequestKnightResearch();
        Check(_slots[7].Disabled && _slots[7].TooltipText.Contains("기름 부족") && _commands.Count == before,
            "Insufficient oil blocks both UI and direct research requests");
        Receive("STOCK 2 11");
        Check(!_slots[7].Disabled && _slots[7].TooltipText.Contains("목재 37") && _slots[7].TooltipText.Contains("기름 11") &&
            _slots[7].TooltipText.Contains("13.5초") && _slots[7].TooltipText.Contains("7.25m") &&
            _slots[7].TooltipText.Contains("+250%") && _slots[7].TooltipText.Contains("8.5초"),
            "Research cost, duration and passive terms use arbitrary server values");

        KnightResearchSnapshot accepted = Buildings.KnightResearch(1).Value;
        foreach (string invalid in new[] {
            "KNIGHT_RESEARCH 0 0 0 0 0 37 11 13.5 7.25 3.5 8.5",
            "KNIGHT_RESEARCH 1 2 0 0 0 37 11 13.5 7.25 3.5 8.5",
            "KNIGHT_RESEARCH 1 0 0 1 2 37 11 13.5 7.25 3.5 8.5",
            "KNIGHT_RESEARCH 1 1 9600 1 2 37 11 13.5 7.25 3.5 8.5",
            "KNIGHT_RESEARCH 1 0 9600 3 2 37 11 13.5 7.25 3.5 8.5",
            "KNIGHT_RESEARCH 1 0 9600 0 0 37 11 13.5 7.25 3.5 8.5",
            "KNIGHT_RESEARCH 1 0 0 0 0 -1 11 13.5 7.25 3.5 8.5",
            "KNIGHT_RESEARCH 1 0 0 0 0 37 -1 13.5 7.25 3.5 8.5",
            "KNIGHT_RESEARCH 1 0 0 0 0 37 11 NaN 7.25 3.5 8.5",
            "KNIGHT_RESEARCH 1 0 0 0 0 37 11 13.5 Infinity 3.5 8.5",
            "KNIGHT_RESEARCH 1 0 0 0 0 37 11 13.5 7.25 1 8.5",
            "KNIGHT_RESEARCH 1 0 0 0 0 37 11 13.5 7.25 3.5 -1",
            idle + " extra", "KNIGHT_RESEARCH 1 0"
        }) Receive(invalid);
        Check(Buildings.KnightResearch(1) == accepted && !_slots[7].Disabled,
            "Malformed snapshots never partially replace accepted research terms");

        long bytes = _wire.Length;
        input._UnhandledInput(KeyEvent(Key.Q, true));
        input._UnhandledInput(KeyEvent(Key.Q, true, echo: true));
        input._Input(KeyEvent(Key.Q, false));
        Check(_commands.Count == before + 1 && _commands[^1] == "RESEARCH_KNIGHT_CHARGE 9600" &&
            Encoding.UTF8.GetString(_wire.ToArray().AsSpan((int)bytes)) == "RESEARCH_KNIGHT_CHARGE 9600" + System.Environment.NewLine &&
            Buildings.KnightResearch(1) == accepted && Buildings.CommanderWood == 37 && Buildings.CommanderOil == 11,
            "Q sends one framed research command, ignores key repetition and awaits authoritative spending/progress");
        Receive("KNIGHT_RESEARCH 1 0 9600 4.5 13.5 37 11 13.5 7.25 3.5 8.5");
        details._Process(0);
        Check(_slots[7].Disabled && progress.IsVisibleInTree() && Math.Abs(progress.Value - 100 * (1 - 4.5 / 13.5)) < .01,
            "Laboratory progress uses server remaining and total durations");
        before = _commands.Count;
        KeyStroke(input, Key.Q);
        SelectBuilding(9601);
        KeyStroke(input, Key.Q);
        Units.RequestKnightResearch();
        Check(_slots[7].Disabled && _slots[7].TooltipText.Contains("다른 기술연구소") && _commands.Count == before,
            "An active team research prevents duplicates in every laboratory");
        Receive("REMOVE 9600");
        Check(_slots[7].Disabled, "Removing a lab does not invent a cancellation before the server snapshot");
        Receive(idle);
        Check(!_slots[7].Disabled, "Authoritative cancellation unlocks another completed laboratory");

        Receive("CONSTRUCTION 9601 50 7");
        Units.RequestKnightResearch();
        Check(_commands.Count == before, "Unfinished laboratories cannot begin research");
        Receive("CONSTRUCTION 9601 100 7");
        SelectBuilding(9602);
        Units.RequestKnightResearch();
        Check(_commands.Count == before && _slots[7].Text == "", "Enemy laboratories expose no research controls");
        SelectBuilding(201);
        Units.RequestKnightResearch();
        Check(_commands.Count == before, "Research cannot be started from another building type");
        SelectBuilding(9601);
        Units.CommanderControl = false;
        KeyStroke(input, Key.Q);
        Units.RequestKnightResearch();
        Check(_commands.Count == before && _slots[7].Disabled, "Non-commanders cannot research");
        Units.CommanderControl = true;
        Units.SetSpectator(true);
        Units.RequestKnightResearch();
        Check(_commands.Count == before, "Spectators cannot issue research requests");
        Units.SetSpectator(false);
        SelectBuilding(9601);
        Receive("KNIGHT_RESEARCH 2 1 0 0 0 61 23 19 8 4 10");
        Check(!_slots[7].Disabled && Buildings.KnightResearch(1) is { Unlocked: false } && Buildings.KnightResearch(2) is { Unlocked: true },
            "Enemy completion cannot unlock or lock the local research");
        Receive(complete);
        KeyStroke(input, Key.Q);
        Check(_slots[7].Disabled && _slots[7].Text == "와이어 돌진\n완료" && _commands.Count == before,
            "Completed research remains permanently unavailable without hiding its server-defined effects");

        Receive("KNIGHT_CHARGE 102 1 0 0 0 0 0 0");
        SelectUnit(102);
        details._Process(0);
        Check(extra.Text == "와이어 돌진 · 준비" && extra.TooltipText.Contains("+250%"),
            "An existing soldier shows its authoritative unlocked passive and team terms");
        Receive("UNIT 1 9610 7 -10 4 1");
        Receive("KNIGHT_CHARGE 9610 1 0 0 0 0 0 0");
        SelectUnit(9610);
        details._Process(0);
        Check(extra.Text == "와이어 돌진 · 준비", "A newly produced soldier uses the same completed team research");
        Receive("HIDE 9610");
        SelectBuilding(9601);
        Building removed = Buildings.SelectedBuilding;
        Receive("REMOVE 9601");
        Units.RequestKnightResearch();
        Check(_commands.Count == before && Units.KnightResearchBlockReason(removed) != null,
            "Destroyed or stale laboratory selections cannot send research requests");
        Receive("REMOVE 9602");
        Receive("TEAM_TIER 1 1 201 0 0 0");
        SelectBuilding(201);
    }
}
