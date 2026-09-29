using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

// 실제 Main 씬에 서버 스냅샷만 주입해 선택에 따른 표시를 확인합니다. 서버에는 연결하지 않습니다.
public partial class CommandPanelChecks : Main
{
    private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly Dictionary<int, Button> _slots = new();
    private readonly List<string> _commands = new();
    private MemoryStream _wire;
    private bool _capture;

    public override async void _Ready()
    {
        try
        {
            _capture = OS.GetCmdlineUserArgs().Contains("--command-panel-capture");
            var net = GetNode<NetClient>("/root/Net");
            typeof(Main).GetField("_net", PrivateInstance).SetValue(this, net);
            using var wire = new MemoryStream();
            _wire = wire;
            using var writer = new StreamWriter(wire, new UTF8Encoding(false)) { AutoFlush = true };
            typeof(NetClient).GetField("_writer", PrivateInstance).SetValue(net, writer);
            Fog.Configure(Map);
            Check(Map.HasMap && Map.SyncError == null, "The real Main scene loads its map");
            Check(Commands == GetNode<CommandPanel>("SelectionUI/CommandPanel") && Commands.Units == Units &&
                Commands.Buildings == Buildings, "Main wires the command panel to the live selection managers");
            Units.CommandRequested += _commands.Add;
            Units.CommandRequested += command => InvokeMain("SendCommand", command);
            for (int number = 1; number <= 9; number++)
                _slots.Add(number, Commands.GetNode<Button>($"Slots/Slot{number}"));
            Check(Commands.GetNode("Slots").GetChildCount() == 9, "The panel contains exactly nine slots");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            CheckLayout();
            Expect("No selection leaves every slot empty");
            CheckNoWorkerTrain("No player identity or selection cannot request a worker");

            BeginSession(7, 1);
            Receive("SIGHT UNIT 0 8");
            Receive("SIGHT UNIT 1 8");
            Receive("SIGHT BUILDING 0 12");
            Receive("UNIT 0 101 7 -10 0 1");
            Receive("UNIT 1 102 7 -7 0 1");
            Receive("UNIT 1 103 0 -4 0 1");
            Receive("UNIT 2 104 9 -1 0 1");
            Receive("UNIT 1 105 8 2 0 2");
            Receive("BUILDING 0 201 1 -10 8 0");
            Receive("BUILDING 1 202 1 -4 8 0");
            Receive("BUILDING 0 203 2 10 8 0");

            SelectUnit(101);
            ExpectUnit("An owned worker shows attack, stop and hold in numpad slots 4, 5 and 6");
            CheckDisplayOnlyButtons();
            await Capture("command-panel-unit");
            SelectUnit(102);
            ExpectUnit("An owned combat unit shows the same three commands");
            InvokeMain("SelectBox", GetViewport().GetVisibleRect());
            Check(Units.SelectedUnitIds.Count == 2, "Box selection selects only the two owned units");
            ExpectUnit("Multiple owned units retain the unit command layout");

            foreach (uint id in new uint[] { 103, 104, 105 })
            {
                SelectUnit(101);
                SelectUnit(id);
                Expect($"Non-controllable unit {id} clears the prior unit command layout");
                CheckNoWorkerTrain("Non-controllable units cannot request workers");
            }

            SelectBuilding(201);
            Expect("The friendly town hall shows worker in numpad slot 7", (7, "일꾼"));
            Check(Units.SelectedUnitIds.Count == 0, "Building selection clears selected units");
            Receive("STOCK 0 100");
            CheckWorkerTrain();
            CheckWorkerTrain();
            await Capture("command-panel-hall");
            foreach (uint id in new uint[] { 202, 203 })
            {
                SelectBuilding(201);
                SelectBuilding(id);
                Expect($"Building {id} has no available commands");
                CheckNoWorkerTrain("Towers and enemy halls cannot request workers");
            }
            SelectBuilding(201);
            SelectUnit(101);
            Check(Buildings.SelectedBuilding == null, "Unit selection clears the selected building");
            ExpectUnit("Switching directly from a hall to a unit replaces its commands");
            CheckNoWorkerTrain("Unit selection cannot retain the hall action");
            InvokeMain("ClearSelection");
            Expect("Clearing selection removes the visible commands");
            CheckNoWorkerTrain("Clearing selection prevents worker requests");

            SelectUnit(101);
            Receive("UNIT 0 101 9 -10 0 1");
            Expect("Changing a selected unit's owner clears its commands immediately");
            SelectUnit(102);
            Receive("REMOVE 102");
            Expect("Removing a selected unit clears its commands during the death animation");
            Receive("UNIT 0 101 7 -10 0 1");
            SelectUnit(101);
            Receive("HIDE 101");
            Expect("Hiding a selected unit clears its commands immediately");
            SelectBuilding(201);
            Receive("BUILDING 0 201 2 -10 8 0");
            Expect("Changing the selected hall's team clears worker immediately");
            CheckNoWorkerTrain("Changing the hall's team prevents worker requests");
            Receive("BUILDING 0 201 1 -10 8 0");
            Expect("A selected hall returning to our team restores worker", (7, "일꾼"));
            Receive("REMOVE 201");
            Expect("Removing the selected hall clears worker");
            CheckNoWorkerTrain("Removed halls cannot request workers");

            SelectBuilding(203);
            Receive("WELCOME 8 2");
            Expect("Changing the local team refreshes an already selected hall", (7, "일꾼"));
            CheckWorkerTrain();
            Map.StopSync();
            long sentBeforeSyncGate = _wire.Length;
            _slots[7].EmitSignal(BaseButton.SignalName.Pressed);
            Check(_wire.Length == sentBeforeSyncGate, "Main blocks training transmission before world synchronization");
            Receive($"MAP 2 {Map.MapHash}");
            Expect("A new map synchronization clears the previous selection");
            Receive("WORLD_READY");
            CheckNoWorkerTrain("A new session cannot reuse the old player identity");
            Receive("WELCOME 8 2");
            Receive("BUILDING 0 204 2 10 8 0");
            SelectBuilding(204);
            CheckWorkerTrain();
            int unitsBeforeResponse = Units.LiveUnits.Count;
            Receive("UNIT 0 301 8 0 0 2");
            Check(Units.LiveUnits.Count == unitsBeforeResponse + 1,
                "The server UNIT response creates the worker");
            SelectUnit(301);
            ExpectUnit("Selection works after reconnecting as a different player");
            InvokeMain("OnConnectionClosed", "Command panel test disconnect");
            Expect("Disconnect clears the visible commands");
            CheckNoWorkerTrain("Disconnect prevents requests using stale identity or hall selection");

            GD.Print("PASS: command panel layout and selection, worker TRAIN type-only transmission without player ID, one line per click, server-only creation and stock changes, sync gate, ownership/team changes, removal, reconnect and disconnect");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private void CheckLayout()
    {
        Rect2 viewport = GetViewport().GetVisibleRect();
        Rect2 panel = Commands.GetGlobalRect();
        Check(panel.End.IsEqualApprox(viewport.End) && panel.Size.IsEqualApprox(new Vector2(216, 216)),
            "The 216 by 216 panel is flush against the bottom-right viewport edges");
        Check(Commands.MouseFilter == Control.MouseFilterEnum.Stop, "The panel absorbs clicks over the world");
        int[] order = { 7, 8, 9, 4, 5, 6, 1, 2, 3 };
        for (int index = 0; index < order.Length; index++)
        {
            Rect2 slot = _slots[order[index]].GetGlobalRect();
            int row = index / 3, column = index % 3;
            Vector2 center = slot.GetCenter();
            Check(slot.Size.X > 0 && slot.Size.Y > 0 && panel.Encloses(slot), "Every slot is visible inside the panel");
            Check(Mathf.IsEqualApprox(center.X, _slots[order[column]].GetGlobalRect().GetCenter().X) &&
                Mathf.IsEqualApprox(center.Y, _slots[order[row * 3]].GetGlobalRect().GetCenter().Y),
                $"Slot {order[index]} occupies its numpad row and column");
            if (column > 0)
                Check(_slots[order[index - 1]].GetGlobalRect().End.X <= slot.Position.X,
                    "Adjacent slot columns do not overlap");
            if (row > 0)
                Check(_slots[order[index - 3]].GetGlobalRect().End.Y <= slot.Position.Y,
                    "Adjacent slot rows do not overlap");
        }
    }

    private void ExpectUnit(string message) => Expect(message, (4, "공격(A)"), (5, "정지(S)"), (6, "홀드(D)"));

    private void Expect(string message, params (int Slot, string Text)[] entries)
    {
        var expected = entries.ToDictionary(entry => entry.Slot, entry => entry.Text);
        foreach ((int number, Button button) in _slots)
        {
            string text = expected.GetValueOrDefault(number, "");
            Check(button.Text == text && button.Disabled == (text.Length == 0),
                $"{message}: slot {number} expected '{text}', got '{button.Text}' (disabled={button.Disabled})");
        }
    }

    private void CheckDisplayOnlyButtons()
    {
        int commandsBefore = _commands.Count;
        string[] before = _slots.Values.Select(button => button.Text).ToArray();
        foreach (Button button in _slots.Values)
        {
            Check(button.Shortcut == null, "Panel labels do not install new keyboard shortcuts");
            if (!button.Disabled) button.EmitSignal(BaseButton.SignalName.Pressed);
        }
        Check(_commands.Count == commandsBefore && before.SequenceEqual(_slots.Values.Select(button => button.Text)),
            "Pressing populated buttons neither sends commands nor changes the selection layout");
    }

    private void CheckWorkerTrain()
    {
        int commandsBefore = _commands.Count;
        int unitsBefore = Units.LiveUnits.Count;
        string stockBefore = Stock.GetChild<Label>(0).Text;
        int bytesBefore = checked((int)_wire.Length);
        _slots[7].EmitSignal(BaseButton.SignalName.Pressed);
        const string expected = "TRAIN 0";
        Check(_commands.Count == commandsBefore + 1 && _commands[^1] == expected,
            "One worker click requests exactly one TRAIN with only the worker type and no player ID");
        Check(Encoding.UTF8.GetString(_wire.ToArray().AsSpan(bytesBefore)) == expected + System.Environment.NewLine,
            "The existing Main and NetClient path writes exactly one correctly framed training line");
        Check(Units.LiveUnits.Count == unitsBefore && Stock.GetChild<Label>(0).Text == stockBefore,
            "A training request does not create a local unit or deduct local stock");
        Expect("Sending a worker request preserves the hall selection", (7, "일꾼"));
    }

    private void CheckNoWorkerTrain(string message)
    {
        int commandsBefore = _commands.Count;
        long bytesBefore = _wire.Length;
        _slots[7].EmitSignal(BaseButton.SignalName.Pressed);
        Units.RequestTrainWorker();
        Check(_commands.Count == commandsBefore && _wire.Length == bytesBefore, message);
    }

    private void SelectUnit(uint id) => InvokeMain("SelectUnit", Units.LiveUnits.Single(unit => unit.UnitId == id));

    private void SelectBuilding(uint id)
    {
        Check(Buildings.TryGetBuilding(id, out Building building), $"Building {id} exists");
        InvokeMain("SelectBuilding", building);
    }

    private void BeginSession(uint player, uint team)
    {
        Receive($"MAP 2 {Map.MapHash}");
        Receive("WORLD_READY");
        Receive($"WELCOME {player} {team}");
    }

    private void Receive(string message) => InvokeMain("OnMessage", message);
    private void InvokeMain(string name, params object[] args) => typeof(Main).GetMethod(name, PrivateInstance).Invoke(this, args);

    private async Task Capture(string name)
    {
        if (!_capture) return;
        Fog.RefreshVision();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using Image screenshot = GetViewport().GetTexture().GetImage();
        string path = $"res://.godot/{name}.png";
        Check(screenshot.SavePng(path) == Error.Ok, "Save the rendered command panel preview");
        GD.Print("Command panel preview: " + ProjectSettings.GlobalizePath(path));
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
