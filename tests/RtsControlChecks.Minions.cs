using Godot;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public partial class RtsControlChecks
{
    private async Task CheckMinionFormation(MemoryStream wire)
    {
        BeginSession();
        Receive("UNIT 0 1800 7 -8 -10 1");
        Units.SelectSingle(Unit(1800));
        foreach (string message in new[] {
            "MINION_RULES 12 30", "MINION_OPTION 100 0 60 460", "MINION_OPTION 101 0 80 480",
            "MINION_OPTION 102 0 140 540", "MINION_LANE 0 0 6 100 100 100 101 101 101",
            "MINION_LANE 1 0 6 100 100 100 101 101 101", "MINION_WAVE 900", "STOCK 0 1000", "TICK 300"
        }) Receive(message);
        await Flush();
        Check(Minions.Visible && !Minions.IsMinionView && Details.Visible && Commands.Visible && Minimap.Visible,
            "Commander starts with existing center details and two minion view tabs");
        Click(MinionControl("Tabs/MinionTab").GetGlobalRect().GetCenter(), MouseButton.Left);
        await Flush();
        Check(Minions.IsMinionView && !Details.Visible && Commands.Visible && Minimap.Visible && Minions.TimerText.Contains("20"),
            "Minion tab replaces only the center HUD and shows the server wave countdown");
        Expect(1800);
        int before = _commands.Count;
        Click(MinionControl("Body/Content/Lanes/Rows/Lane0/Slot0").GetGlobalRect().GetCenter(), MouseButton.Left);
        await Flush();
        Check(Minions.IsPickerOpen && MinionControl("Picker/Content/Options/Option102").IsVisibleInTree(),
            "Clicking an existing wave slot lists purchasable replacements");
        Click(MinionControl("Picker/Content/Options/Option102").GetGlobalRect().GetCenter(), MouseButton.Left);
        await Flush();
        Check(MinionWire(wire).Last() == "MINION_SET 0 0 0 102" && Minions.IsRequestPending &&
            _commands.Count == before, "Purchase sends the slot, lane and expected revision without leaking a world order");
        Expect(1800);
        Receive("STOCK 0 860");
        Receive("MINION_LANE 0 1 6 102 100 100 101 101 101");
        await Flush();
        Check(!Minions.IsRequestPending && MinionControl("Body/Content/Lanes/Rows/Lane0/Slot0").TooltipText.Contains("생명"),
            "Only the authoritative lane snapshot confirms a purchased replacement");
        Click(MinionControl("Body/Content/Lanes/Rows/Lane1/Add").GetGlobalRect().GetCenter(), MouseButton.Left);
        await Flush();
        Click(MinionControl("Picker/Content/Options/Option102").GetGlobalRect().GetCenter(), MouseButton.Left);
        await Flush();
        Check(MinionWire(wire).Last() == "MINION_ADD 1 0 102", "Plus requests a permanent slot on the selected lane");
        Receive("ERR 편성이 변경되었습니다");
        Receive("MINION_LANE 1 1 7 100 100 100 101 101 101 102");
        await Flush();
        Check(!Minions.IsRequestPending && MinionControl("Body/Content/Lanes/Rows/Lane1/Slot6").IsVisibleInTree(),
            "A stale purchase rejection recovers from the current server composition");
        Receive("STOCK 0 0");
        Click(MinionControl("Body/Content/Lanes/Rows/Lane0/Slot1").GetGlobalRect().GetCenter(), MouseButton.Left);
        await Flush();
        Check(Minions.GetNode<Button>("Picker/Content/Options/Option102").Disabled,
            "Unavailable resources disable purchases while keeping costs visible");
        int purchases = MinionWire(wire).Length;
        KeyPress(Key.Escape);
        await Flush();
        Check(!Minions.IsPickerOpen && MinionWire(wire).Length == purchases, "Escape closes the list without purchasing");
        Click(MinionControl("Body/Content/Lanes/Rows/Lane0/Slot1").GetGlobalRect().GetCenter(), MouseButton.Left);
        await Flush();
        Click(new Vector2(600, 250), MouseButton.Right);
        await Flush();
        Check(!Minions.IsPickerOpen && _commands.Count == before, "Closing the picker with a world click cannot move selected units");
        Receive("MINION_LANE 0 2 12 102 100 100 101 101 101 100 101 102 100 101 102");
        await Flush();
        Check(Minions.GetNode<Button>("Body/Content/Lanes/Rows/Lane0/Add").Disabled,
            "The server slot limit disables further permanent additions");
        Check(!MinionLaneSnapshot.TryParse("MINION_LANE 0 3 2 100".Split(' '), out _) &&
            !MinionOptionSnapshot.TryParse("MINION_OPTION 102 0 -1 540".Split(' '), out _) &&
            !MinionWaveSnapshot.TryParse("MINION_WAVE nope".Split(' '), out _), "Malformed composition and cost snapshots are rejected");
        Click(MinionControl("Tabs/DefaultTab").GetGlobalRect().GetCenter(), MouseButton.Left);
        await Flush();
        Check(!Minions.IsMinionView && Details.Visible, "Existing UI restores without changing the selected army");
        Expect(1800);
        Receive("WELCOME 7 1 HERO 201");
        Receive("MINION_LANE 0 10 1 102");
        Check(!Minions.Visible && !Minions.IsMinionView && !Minions.IsRequestPending,
            "Hero mode cannot retain or edit the commander wave panel");
        BeginSession();
        Check(!Minions.IsMinionView && !Minions.IsPickerOpen && !Minions.IsRequestPending,
            "A new match clears purchased options, pending requests and the open picker");
    }

    private Control MinionControl(string path) => Minions.GetNode<Control>(path);
    private static string[] MinionWire(MemoryStream wire) => Encoding.UTF8.GetString(wire.ToArray())
        .Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.TrimEnd('\r'))
        .Where(line => line.StartsWith("MINION_")).ToArray();
}
