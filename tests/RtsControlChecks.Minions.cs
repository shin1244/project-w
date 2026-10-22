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
            "MINION_RULES 12 30", "MINION_XP_RULE 6 5", "MINION_OPTION 100 0 60 460", "MINION_OPTION 101 0 80 480",
            "MINION_OPTION 102 0 140 540", "MINION_UPGRADE 103 100 2 3 1 0 150 2 150", "MINION_UPGRADE 104 100 2 5 9 0 150 2 150",
            "MINION_LANE 0 0 6 100 100 100 101 101 101", "MINION_LANE 1 0 6 100 100 100 101 101 101", "MINION_WAVE 900",
            "STOCK 0 1000", "STOCK 2 1000", "TEAM_TIER 1 2 0 0 0 0", "TICK 300"
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
        Check(Minions.IsPickerOpen && MinionControl("Picker/Content/Options/Option103").IsVisibleInTree() &&
            Minions.GetNodeOrNull("Picker/Content/Options/Option102") == null,
            "Clicking a melee slot lists only its two tier-two upgrades, never another type");
        Click(MinionControl("Picker/Content/Options/Option103").GetGlobalRect().GetCenter(), MouseButton.Left);
        await Flush();
        Check(MinionWire(wire).Last() == "MINION_SET 0 0 0 103" && Minions.IsRequestPending &&
            _commands.Count == before, "An upgrade sends the slot, lane and expected revision without leaking a world order");
        Expect(1800);
        Receive("STOCK 0 850");
        Receive("STOCK 2 850");
        Receive("MINION_LANE 0 1 6 103 100 100 101 101 101");
        await Flush();
        Check(!Minions.IsRequestPending && MinionControl("Body/Content/Lanes/Rows/Lane0/Slot0").TooltipText.Contains("화로"),
            "Only the authoritative lane snapshot confirms an upgrade");
        Check(((Button)MinionControl("Body/Content/Lanes/Rows/Lane1/Add")).Disabled,
            "Plus needs 마석; wood never buys a slot");
        Receive("MINION_ADD_COST 3 100");
        Receive("STOCK 3 100");
        await Flush();
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
        Receive("STOCK 2 0");
        Click(MinionControl("Body/Content/Lanes/Rows/Lane0/Slot1").GetGlobalRect().GetCenter(), MouseButton.Left);
        await Flush();
        Check(Minions.GetNode<Button>("Picker/Content/Options/Option103") is { Disabled: true } furnace &&
            furnace.FindChild("Cost", true, false) is Label { Text: var price } && price.Contains("목재") && price.Contains("기름"),
            "Missing oil disables an upgrade while keeping both costs visible");
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
        await CheckMinionReordering(wire);
        await CheckMinionUpgrades(wire);
    }

    // Tier-two choices come only from MINION_UPGRADE, unlock with the team's own HQ tier and
    // replace a melee slot once. Other slots never change type. The server validates every request.
    private async Task CheckMinionUpgrades(MemoryStream wire)
    {
        BeginSession();
        foreach (string message in new[] {
            "MINION_RULES 12 30", "MINION_OPTION 100 0 60 460", "MINION_OPTION 101 0 80 480", "MINION_OPTION 102 0 140 540",
            "MINION_UPGRADE 103 100 2 3 1 0 150 2 150", "MINION_UPGRADE 104 100 2 5 9 0 150 2 150",
            "MINION_LANE 0 4 3 100 101 104", "MINION_LANE 1 0 1 100", "STOCK 0 1000", "STOCK 2 1000", "TEAM_TIER 1 1 0 0 0 0"
        }) Receive(message);
        await Flush();
        Click(MinionControl("Tabs/MinionTab").GetGlobalRect().GetCenter(), MouseButton.Left);
        await Flush();
        Click(SlotCenter(0, 0), MouseButton.Left);
        await Flush();
        var furnace = Minions.GetNodeOrNull<Button>("Picker/Content/Options/Option103");
        Check(furnace != null && furnace.Disabled && Minions.GetNodeOrNull<Button>("Picker/Content/Options/Option104") is { Disabled: true } &&
            furnace.FindChild("Cost", true, false) is Label { Text: var locked } && locked.Contains("2티어"),
            "A melee slot lists both tier-two upgrades, locked until the HQ reaches their tier");
        KeyPress(Key.Escape);
        Receive("TEAM_TIER 2 2 0 0 0 0");
        await Flush();
        Click(SlotCenter(0, 0), MouseButton.Left);
        await Flush();
        Check(Minions.GetNode<Button>("Picker/Content/Options/Option103").Disabled, "Only the commander's own HQ tier unlocks upgrades");
        KeyPress(Key.Escape);
        Receive("TEAM_TIER 1 2 0 0 0 0");
        await Flush();
        Click(SlotCenter(0, 0), MouseButton.Left);
        await Flush();
        Click(MinionControl("Picker/Content/Options/Option103").GetGlobalRect().GetCenter(), MouseButton.Left);
        await Flush();
        Check(MinionWire(wire).Last() == "MINION_SET 0 4 0 103", "An unlocked upgrade replaces the clicked melee slot");
        Receive("MINION_LANE 0 5 3 103 101 104");
        await Flush();
        int sent = MinionWire(wire).Length;
        foreach (int slot in new[] { 0, 1, 2 })
        {
            Click(SlotCenter(0, slot), MouseButton.Left);
            await Flush();
            Check(!Minions.IsPickerOpen, $"Slot {slot} (upgraded, ranged or warden) cannot change type");
        }
        Check(MinionWire(wire).Length == sent && MinionControl("Body/Content/Lanes/Rows/Lane0/Slot0").TooltipText.Contains("화로"),
            "Clicking a slot that cannot change sends nothing");
        Receive("MINION_ADD_COST 3 100");
        Receive("STOCK 3 100");
        await Flush();
        Click(MinionControl("Body/Content/Lanes/Rows/Lane1/Add").GetGlobalRect().GetCenter(), MouseButton.Left);
        await Flush();
        Check(Minions.IsPickerOpen && Minions.GetNodeOrNull("Picker/Content/Options/Option101") != null &&
            Minions.GetNodeOrNull("Picker/Content/Options/Option103") == null, "A new slot starts as a base minion, never an upgrade");
        KeyPress(Key.Escape);
        await Flush();
        Check(MinionUpgradeSnapshot.TryParse("MINION_UPGRADE 103 100 2 3 1 0 150 2 150".Split(' '), out var rule) &&
            rule.Costs.Length == 2 && rule.Costs[1] == new MinionCost(2, 150) &&
            !MinionUpgradeSnapshot.TryParse("MINION_UPGRADE 103 103 2 3 1 0 150".Split(' '), out _) &&
            !MinionUpgradeSnapshot.TryParse("MINION_UPGRADE 103 100 2 3 1 0 -5".Split(' '), out _) &&
            !MinionUpgradeSnapshot.TryParse("MINION_UPGRADE 103 100 2 3 1 0 150 0 150".Split(' '), out _) &&
            !MinionUpgradeSnapshot.TryParse("MINION_UPGRADE 103 100 2 3 1 0".Split(' '), out _), "Malformed upgrade rules are rejected");
        Receive("MINION_UPGRADE 105 101 2 3 33.3333 0 200 2 200");
        await Flush();
        Click(SlotCenter(0, 1), MouseButton.Left);
        await Flush();
        Check(Minions.IsPickerOpen && Minions.GetNodeOrNull("Picker/Content/Options/Option105") != null &&
            Minions.GetNodeOrNull("Picker/Content/Options/Option103") == null, "A ranged slot lists only the ranged upgrade");
        KeyPress(Key.Escape);
        await Flush();
        int bolts = Units.GetChildren().OfType<MinionArcBolt>().Count();
        foreach (string malformed in new[] { "MINION_ARC 0 0 0 3 0", "MINION_ARC 9 0 0 3", "MINION_ARC 9 nan 0 3 0" }) Receive(malformed);
        Check(Units.GetChildren().OfType<MinionArcBolt>().Count() == bolts, "Malformed arc bounces draw nothing");
        Receive("MINION_ARC 9 0 0 3 0"); // The source may be out of sight.
        Check(Units.GetChildren().OfType<MinionArcBolt>().Count() == bolts + 1, "Each arc bounce draws one bolt");
        Click(MinionControl("Tabs/DefaultTab").GetGlobalRect().GetCenter(), MouseButton.Left);
        await Flush();
    }

    private async Task CheckMinionReordering(MemoryStream wire)
    {
        Receive("UNIT 0 1800 7 -8 -10 1");
        Units.SelectSingle(Unit(1800));
        foreach (string message in new[] {
            "MINION_RULES 12 30", "MINION_OPTION 100 0 60 460", "MINION_OPTION 101 0 80 480",
            "MINION_OPTION 102 0 140 540", "MINION_LANE 0 7 4 100 101 102 100",
            "MINION_LANE 1 2 4 100 101 102 100", "STOCK 0 0"
        }) Receive(message);
        await Flush();
        Click(MinionControl("Tabs/MinionTab").GetGlobalRect().GetCenter(), MouseButton.Left);
        await Flush();
        int worldCommands = _commands.Count;
        int before = MinionWire(wire).Length;
        await BeginMinionDrag(0, 0);
        await EndMinionDrag(SlotCenter(0, 2));
        Check(MinionWire(wire).Length == before + 1 && MinionWire(wire).Last() == "MINION_MOVE 0 7 0 2" &&
            Minions.IsRequestPending && !Minions.IsPickerOpen, "Native drag sends one revisioned move without opening the purchase picker");
        Check(MinionControl("Body/Content/Lanes/Rows/Lane0/Slot0").TooltipText.Contains("근접") &&
            Minions.GetNode<Button>("Body/Content/Lanes/Rows/Lane0/Slot2").Disabled,
            "Reordering works with zero wood, waits for the server and blocks overlapping requests");
        Receive("MINION_LANE 0 8 4 101 102 100 100");
        await Flush();
        Check(!Minions.IsRequestPending && MinionControl("Body/Content/Lanes/Rows/Lane0/Slot0").TooltipText.Contains("원거리") &&
            MinionControl("Body/Content/Lanes/Rows/Lane0/Slot1").TooltipText.Contains("생명"),
            "Only the authoritative inserted order updates the portraits");
        await BeginMinionDrag(0, 2);
        await EndMinionDrag(SlotCenter(0, 0));
        Check(MinionWire(wire).Last() == "MINION_MOVE 0 8 2 0", "Dragging backwards uses the final destination slot");
        Receive("ERR 미니언 편성이 변경되었습니다");
        Receive("MINION_LANE 0 9 4 100 101 102 100");
        await Flush();
        Check(!Minions.IsRequestPending, "Rejected/stale reorder recovers from the current lane snapshot");

        before = MinionWire(wire).Length;
        await BeginMinionDrag(0, 0);
        await EndMinionDrag(SlotCenter(1, 2));
        await BeginMinionDrag(0, 0);
        await EndMinionDrag(SlotCenter(0, 0));
        await BeginMinionDrag(0, 0);
        await EndMinionDrag(new Vector2(600, 250));
        Check(MinionWire(wire).Length == before && !Minions.IsPickerOpen && _commands.Count == worldCommands,
            "Another lane, the source slot and the world reject drops without leaking selection or move commands");
        Expect(1800);

        await BeginMinionDrag(0, 0);
        KeyPress(Key.Escape);
        await EndMinionDrag(SlotCenter(0, 2));
        Check(!GetViewport().GuiIsDragging() && MinionWire(wire).Length == before, "Escape cancels an active native drag");
        await BeginMinionDrag(0, 0);
        Click(new Vector2(600, 250), MouseButton.Right);
        await EndMinionDrag(SlotCenter(0, 2));
        Check(MinionWire(wire).Length == before && _commands.Count == worldCommands, "Right-click cancels drag without issuing a world move");

        await BeginMinionDrag(0, 0);
        Vector2 destination = SlotCenter(0, 2);
        Receive("MINION_LANE 0 10 4 102 101 100 100");
        await EndMinionDrag(destination);
        Check(!GetViewport().GuiIsDragging() && MinionWire(wire).Length == before,
            "A new server revision cancels the captured drag instead of moving a different minion");
        await BeginMinionDrag(0, 0);
        Receive("WELCOME 7 1 HERO 201");
        await EndMinionDrag(destination);
        Check(!Minions.Visible && !GetViewport().GuiIsDragging() && MinionWire(wire).Length == before,
            "Role changes cancel drag and prevent commander commands");
    }

    private Vector2 SlotCenter(int lane, int slot) =>
        MinionControl($"Body/Content/Lanes/Rows/Lane{lane}/Slot{slot}").GetGlobalRect().GetCenter();

    private async Task BeginMinionDrag(int lane, int slot)
    {
        Vector2 start = SlotCenter(lane, slot);
        GetViewport().PushInput(new InputEventMouseMotion { Position = start, GlobalPosition = start }, true);
        GetViewport().PushInput(new InputEventMouseButton { Position = start, GlobalPosition = start,
            ButtonIndex = MouseButton.Left, ButtonMask = MouseButtonMask.Left, Pressed = true }, true);
        await Flush();
        Vector2 motion = new(16, 0);
        GetViewport().PushInput(new InputEventMouseMotion { Position = start + motion, GlobalPosition = start + motion,
            Relative = motion, ButtonMask = MouseButtonMask.Left }, true);
        await Flush();
        Check(GetViewport().GuiIsDragging(), "Dragging a slot starts Godot's native drag operation");
    }

    private async Task EndMinionDrag(Vector2 destination)
    {
        GetViewport().PushInput(new InputEventMouseMotion { Position = destination, GlobalPosition = destination,
            ButtonMask = MouseButtonMask.Left }, true);
        await Flush();
        GetViewport().PushInput(new InputEventMouseButton { Position = destination, GlobalPosition = destination,
            ButtonIndex = MouseButton.Left, Pressed = false }, true);
        await Flush();
    }

    private Control MinionControl(string path) => Minions.GetNode<Control>(path);
    private static string[] MinionWire(MemoryStream wire) => Encoding.UTF8.GetString(wire.ToArray())
        .Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.TrimEnd('\r'))
        .Where(line => line.StartsWith("MINION_")).ToArray();
}
