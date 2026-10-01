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

    public override async void _Ready()
    {
        try
        {
            var net = GetNode<NetClient>("/root/Net");
            typeof(Main).GetField("_net", PrivateInstance).SetValue(this, net);
            using var wire = new MemoryStream();
            _wire = wire;
            using var writer = new StreamWriter(wire, new UTF8Encoding(false)) { AutoFlush = true };
            typeof(NetClient).GetField("_writer", PrivateInstance).SetValue(net, writer);
            InvokeMain("ConnectInput");
            Fog.Configure(Map);
            Check(Map.HasMap && Map.SyncError == null, "The real Main scene loads its map");
            Check(Commands == GetNode<CommandPanel>("SelectionUI/CommandPanel") && Commands.Units == Units &&
                Commands.Buildings == Buildings, "Main wires the command panel to the live selection managers");
            Check(GetNode<PlayerInput>("PlayerInput").Commands == Commands,
                "PlayerInput routes construction hotkeys through the command panel");
            Units.CommandRequested += _commands.Add;
            Units.CommandRequested += command => InvokeMain("SendCommand", command);
            for (int number = 1; number <= 9; number++)
                _slots.Add(number, Commands.GetNode<Button>($"Slots/Slot{number}"));
            Check(Commands.GetNode("Slots").GetChildCount() == 9, "The panel contains exactly nine slots");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Expect("No selection leaves every slot empty");
            CheckNoWorkerTrain("No player identity or selection cannot request a worker");

            BeginSession(7, 1);
            Receive("SIGHT UNIT 0 8");
            Receive("SIGHT UNIT 1 8");
            Receive("SIGHT UNIT 100 8");
            Receive("SIGHT BUILDING 0 12");
            Receive("UNIT 0 101 7 -10 0 1");
            Receive("UNIT 1 102 7 -7 0 1");
            Receive("UNIT 100 103 0 -4 0 1");
            Receive("UNIT 2 104 9 -1 0 1");
            Receive("UNIT 1 105 8 2 0 2");
            Receive("BUILDING 0 201 1 -10 8 0");
            Receive("BUILDING 1 202 1 -4 8 0");
            Receive("BUILDING 0 203 2 10 8 0");

            SelectUnit(101);
            ExpectUnit("An owned worker shows attack, stop and hold in numpad slots 4, 5 and 6");
            CheckTierMenu();
            CheckRequirements();
            CheckCombatCommands();
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
            Receive("SUPPLY 4 10");
            CheckWorkerTrain();
            await CheckBarracksProduction();
            await CheckCancellation();
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
            Receive("WELCOME 8 2 COMMANDER");
            Expect("Changing the local team refreshes an already selected hall", (7, "일꾼"));
            CheckWorkerTrain();
            Map.StopSync();
            long sentBeforeSyncGate = _wire.Length;
            _slots[7].EmitSignal(BaseButton.SignalName.Pressed);
            Check(_wire.Length == sentBeforeSyncGate, "Main blocks training transmission before world synchronization");
            Buildings.HandleRequirements("REQUIRES UNIT 0 5".Split(' '));
            Receive($"MAP 2 {Map.MapHash}");
            Expect("A new map synchronization clears the previous selection");
            Check(Buildings.UnitRequirementBlockReason(0) == null, "New sessions discard old requirement definitions");
            Receive("WORLD_READY");
            CheckNoWorkerTrain("A new session cannot reuse the old player identity");
            Receive("WELCOME 8 2 COMMANDER");
            Receive("BUILDING 0 204 2 10 8 0");
            SelectBuilding(204);
            CheckWorkerTrain();
            int unitsBeforeResponse = Units.LiveUnits.Count;
            Receive("UNIT 0 301 8 0 0 2");
            Check(Units.LiveUnits.Count == unitsBeforeResponse + 1,
                "The server UNIT response creates the worker");
            SelectUnit(301);
            ExpectUnit("Selection works after reconnecting as a different player");
            _slots[1].EmitSignal(BaseButton.SignalName.Pressed);
            Check(Commands.IsTierOneMenuOpen, "A reconnected worker can reopen the tier-one menu");
            InvokeMain("OnConnectionClosed", "Command panel test disconnect");
            Expect("Disconnect clears the visible commands");
            Check(!Commands.IsTierOneMenuOpen, "Disconnect exits the tier-one menu");
            CheckNoWorkerTrain("Disconnect prevents requests using stale identity or hall selection");

            GD.Print("PASS: production/construction cancellation, stable job IDs, payer ownership, queue portrait clicks, Escape, server-only refunds, commands, independent queues, progress, removal and reconnect");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private void ExpectUnit(string message)
    {
        if (Units.TryGetSelectedWorker(out _))
            Expect(message, (1, "1티어(Z)"), (4, "공격(A)"), (5, "정지(S)"), (6, "홀드(D)"));
        else Expect(message, (4, "공격(A)"), (5, "정지(S)"), (6, "홀드(D)"));
    }

    private void CheckRequirements()
    {
        // 임의의 서버 정의를 사용해 클라이언트에 기술 의존성이 하드코딩되지 않았는지 확인합니다.
        Receive("REQUIRES UNIT 1 4 5");
        Receive("REQUIRES BUILDING 5 4");
        Receive("REQUIRES BUILDING 6 5");
        Receive("BUILDING 4 901 1 -12 12 0");
        Receive("BUILDING 5 902 2 12 12 0");
        Receive("BUILDING 5 903 1 0 12 0");
        Receive("CONSTRUCTION 903 99");
        SelectBuilding(901);
        int sent = _commands.Count;
        _slots[7].EmitSignal(BaseButton.SignalName.Pressed);
        Units.RequestTrain(UnitCatalog.Knight);
        Check(_slots[7].Disabled && !_slots[8].Disabled && _slots[7].TooltipText.Contains("대장간") &&
            _commands.Count == sent, "Enemy and incomplete prerequisites do not unlock training or direct commands");
        Receive("CONSTRUCTION 903 100");
        Check(!_slots[7].Disabled, "Completion immediately refreshes the selected producer");
        _slots[7].EmitSignal(BaseButton.SignalName.Pressed);
        Check(_commands.Count == sent + 1 && _commands[^1] == "TRAIN 901 1", "Unlocked training uses the normal request path");
        Receive("QUEUE 901 40 9901:1:7");
        Receive("REMOVE 903");
        Check(_slots[7].Disabled && Buildings.SelectedBuilding.ProductionQueue.Count == 1,
            "Destroying a prerequisite locks new training without removing already queued work");
        Receive("REQUIRES UNIT 1 4 invalid");
        Check(_slots[7].Disabled, "Malformed requirements cannot partially replace a valid definition");
        Receive("REQUIRES UNIT 1");
        Check(!_slots[7].Disabled, "An explicit empty requirement list restores training");
        Receive("REMOVE 901");
        Receive("REMOVE 902");

        SelectUnit(101);
        Placement.Cancel();
        _slots[1].EmitSignal(BaseButton.SignalName.Pressed);
        Check(_slots[4].Disabled && _slots[5].Disabled && !_slots[7].Disabled,
            "The build menu derives each lock from its own server requirements");
        KeyStroke(GetNode<PlayerInput>("PlayerInput"), Key.A);
        _slots[4].EmitSignal(BaseButton.SignalName.Pressed);
        Check(Commands.IsTierOneMenuOpen && !Placement.Active, "Locked buttons and hotkeys leave the build menu open");
        Receive("BUILDING 4 904 2 0 12 0");
        Check(_slots[4].Disabled, "Enemy prerequisites do not unlock construction");
        Receive("BUILDING 4 904 1 0 12 0");
        Receive("CONSTRUCTION 904 0");
        Check(_slots[4].Disabled, "Construction sites do not count as complete prerequisites");
        Receive("CONSTRUCTION 904 100");
        Check(!_slots[4].Disabled && Commands.IsTierOneMenuOpen, "Completion unlocks the existing open menu");
        Receive("BUILDING 4 905 1 8 12 0");
        Receive("REMOVE 904");
        Check(!_slots[4].Disabled, "One remaining completed friendly prerequisite is sufficient");
        Receive("BUILDING 4 905 2 8 12 0");
        Check(_slots[4].Disabled, "Changing the last prerequisite's team immediately relocks construction");
        Receive("REMOVE 905");
        Receive("REQUIRES BUILDING 5");
        Receive("REQUIRES BUILDING 6");
        Commands.CancelMenu();
    }

    private void Expect(string message, params (int Slot, string Text)[] entries)
    {
        var expected = entries.ToDictionary(entry => entry.Slot, entry => entry.Text);
        foreach ((int number, Button button) in _slots)
        {
            string text = expected.GetValueOrDefault(number, "");
            bool disabled = text.Length == 0;
            Check(button.Text == text && button.Disabled == disabled,
                $"{message}: slot {number} expected '{text}', got '{button.Text}' (disabled={button.Disabled})");
        }
    }

    private void CheckCombatCommands()
    {
        var input = GetNode<PlayerInput>("PlayerInput");
        Unit worker = Units.LiveUnits.Single(unit => unit.UnitId == 101);
        Unit knight = Units.LiveUnits.Single(unit => unit.UnitId == 102);
        SelectUnit(101);
        Units.ToggleSelection(knight);
        Receive("STATE 101 GATHER 17 0 1");
        UnitState state = worker.State;
        Vector3 position = worker.GlobalPosition;
        foreach ((int slot, string verb) in new[] { (5, "STOP"), (6, "HOLD") })
        {
            int before = _commands.Count;
            _slots[4].EmitSignal(BaseButton.SignalName.Pressed);
            Check(input.IsAttackTargeting && _commands.Count == before, "Attack button enters the existing A targeting mode");
            int bytes = (int)_wire.Length;
            _slots[slot].EmitSignal(BaseButton.SignalName.Pressed);
            input._PhysicsProcess(0);
            string[] parts = _commands[^1].Split(' ');
            Check(_commands.Count == before + 1 && parts[0] == verb && parts.Length == 3 &&
                parts.Skip(1).ToHashSet().SetEquals(new[] { "101", "102" }) &&
                Encoding.UTF8.GetString(_wire.ToArray().AsSpan(bytes)) == _commands[^1] + System.Environment.NewLine,
                "One button press sends selected IDs through Main/Net without client coordinates");
            Check(!input.IsAttackTargeting && worker.State == state && worker.GlobalPosition == position,
                "Stop/hold exit targeting and wait for server state and position");
        }
        foreach (Key key in new[] { Key.S, Key.D })
        {
            int before = _commands.Count;
            Vector3 camera = Units.Camera.Position;
            try
            {
                Input.ParseInputEvent(KeyEvent(key, true));
                Input.FlushBufferedEvents();
                input._UnhandledInput(KeyEvent(key, true, echo: true));
                input._PhysicsProcess(0);
                Units.Camera.Call("_process", .1);
                Check(_commands.Count == before + 1 && _commands[^1].StartsWith(key == Key.S ? "STOP " : "HOLD ") &&
                    Units.Camera.Position.IsEqualApprox(camera), "S/D issue one order, ignore repetition and do not pan the camera");
                InvokeMain("ClearSelection");
                Units.Camera.Call("_process", .1);
                Check(Units.Camera.Position.IsEqualApprox(camera), "Losing selection does not turn a held command key into camera movement");
            }
            finally
            {
                Input.ParseInputEvent(KeyEvent(key, false));
                Input.FlushBufferedEvents();
                Units.Camera.Call("_process", 0);
                Units.Camera.Position = camera;
                SelectUnit(101);
                Units.ToggleSelection(knight);
            }
        }
        int sent = _commands.Count;
        foreach (uint id in new uint[] { 103, 104, 105 })
        {
            SelectUnit(id);
            _slots[5].EmitSignal(BaseButton.SignalName.Pressed);
            _slots[6].EmitSignal(BaseButton.SignalName.Pressed);
            Units.RequestStop();
            Units.RequestHold();
        }
        InvokeMain("ClearSelection");
        Units.RequestStop();
        Units.RequestHold();
        Check(_commands.Count == sent, "Foreign inspection and empty selection cannot stop or hold units");
        SelectUnit(101);
        Receive("STATE 101 HOLD 17 0 1");
        Check(worker.State.Activity == UnitActivity.Hold && SelectionDetails.ActivityText(worker.State, true).Contains("위치 사수"),
            "Server HOLD state reaches the selection display");
    }

    private void CheckTierMenu()
    {
        PlayerInput input = GetNode<PlayerInput>("PlayerInput");
        var requested = new List<uint>();
        Commands.BuildRequested += requested.Add;
        try
        {
            _slots[1].EmitSignal(BaseButton.SignalName.Pressed);
            ExpectTierMenu("The lower-left worker button opens the tier-one construction menu");
            Check(Units.Camera.GetMeta(CommandPanel.CameraMenuMeta, false).AsBool(),
                "An open tier-one menu suppresses camera letter polling");
            Check(!_slots[5].Disabled, "Tower construction is available with the updated server definition");
            Check(!Commands.TryHandleShortcut(new InputEventKey { Pressed = true, Keycode = Key.Q, CtrlPressed = true }),
                "Modified keys are not reinterpreted as plain building shortcuts");
            KeyStroke(input, Key.Z);
            ExpectUnit("Z returns to the default worker panel");
            KeyStroke(input, Key.Z);
            KeyStroke(input, Key.Escape);
            ExpectUnit("Escape returns to the default worker panel");

            foreach ((int slot, Key key, uint type) in new[]
            {
                (7, Key.Q, BuildingCatalog.Store), (8, Key.W, BuildingCatalog.Supply),
                (9, Key.E, BuildingCatalog.Barracks), (4, Key.A, BuildingCatalog.Forge), (5, Key.S, BuildingCatalog.Tower)
            })
            {
                _slots[1].EmitSignal(BaseButton.SignalName.Pressed);
                int before = requested.Count;
                _slots[slot].EmitSignal(BaseButton.SignalName.Pressed);
                Check(requested.Count == before + 1 && requested[^1] == type,
                    $"Slot {slot} requests exactly one construction of type {type}");
                ExpectUnit("A building selection returns to the default panel before placement");
                KeyStroke(input, Key.Z);
                before = requested.Count;
                input._UnhandledInput(KeyEvent(key, true));
                input._UnhandledInput(KeyEvent(key, true, echo: true));
                Check(requested.Count == before + 1 && requested[^1] == type && !input.IsAttackTargeting,
                    $"{key} requests one type {type}, while key repetition cannot enter attack targeting");
                Check(!Commands.IsTierOneMenuOpen &&
                    Units.Camera.GetMeta(CommandPanel.CameraReleaseMeta, false).AsBool(),
                    "Closing the menu holds camera letters until the selecting key is released");
                input._Input(KeyEvent(key, false));
            }

            // Input의 실제 물리 키 상태를 사용해 프레임마다 폴링하는 카메라까지 확인합니다.
            Vector3 cameraPosition = Units.Camera.Position;
            try
            {
                KeyStroke(input, Key.Z);
                Input.ParseInputEvent(KeyEvent(Key.W, true));
                Input.FlushBufferedEvents();
                Check(!Commands.IsTierOneMenuOpen && requested[^1] == BuildingCatalog.Supply,
                    "A real W event selects quarters through the PlayerInput path");
                Units.Camera.Call("_process", .1);
                Check(Units.Camera.Position.IsEqualApprox(cameraPosition),
                    "Holding the selecting W key after menu closure does not pan the camera");
                Input.ParseInputEvent(KeyEvent(Key.W, false));
                Input.FlushBufferedEvents();
                Units.Camera.Call("_process", .1);
                Input.ParseInputEvent(KeyEvent(Key.W, true));
                Input.FlushBufferedEvents();
                Units.Camera.Call("_process", .1);
                Check(!Units.Camera.Position.IsEqualApprox(cameraPosition),
                    "Releasing and pressing W again restores normal camera movement");
            }
            finally
            {
                Input.ParseInputEvent(KeyEvent(Key.W, false));
                Input.FlushBufferedEvents();
                Units.Camera.Position = cameraPosition;
            }

            KeyStroke(input, Key.A);
            Check(input.IsAttackTargeting, "A still activates attack targeting outside the construction menu");
            KeyStroke(input, Key.Z);
            Check(!input.IsAttackTargeting && Commands.IsTierOneMenuOpen,
                "Opening the construction menu exits attack targeting");
            SelectUnit(102);
            Check(!Commands.IsTierOneMenuOpen, "Changing selection closes the construction menu");
            SelectUnit(101);
            KeyStroke(input, Key.Z);
            GetWindow().EmitSignal(Window.SignalName.FocusExited);
            Check(!Commands.IsTierOneMenuOpen, "Losing window focus closes the construction menu");
            KeyStroke(input, Key.Z);
            input.ResetInteraction();
            Check(!Commands.IsTierOneMenuOpen, "Session reset closes the construction menu");

            int group = 0;
            void OnGroup(int number, bool save, bool focus) { group = number; }
            input.ControlGroupRequested += OnGroup;
            try
            {
                KeyStroke(input, Key.Z);
                KeyStroke(input, Key.Key1);
                input._PhysicsProcess(0);
                Check(group == 1 && !Commands.IsTierOneMenuOpen,
                    "Number-row control groups retain their input path and close the construction menu");
            }
            finally { input.ControlGroupRequested -= OnGroup; }
            ExpectUnit("Menu checks leave the normal worker commands visible");
        }
        finally
        {
            Commands.BuildRequested -= requested.Add;
            input.ResetInteraction();
        }
    }

    private void ExpectTierMenu(string message) => Expect(message,
        (7, "저장소\n(Q)"), (8, "합숙소\n(W)"), (9, "병영\n(E)"),
        (4, "대장간\n(A)"), (5, "포탑\n(S)"), (1, "뒤로(Z)"));

    private static InputEventKey KeyEvent(Key key, bool pressed, bool echo = false) =>
        new() { Keycode = key, PhysicalKeycode = key, Pressed = pressed, Echo = echo };

    private static void KeyStroke(PlayerInput input, Key key)
    {
        if (key == Key.Escape) input._Input(KeyEvent(key, true));
        else input._UnhandledInput(KeyEvent(key, true));
        input._Input(KeyEvent(key, false));
    }

    private void CheckWorkerTrain()
    {
        int commandsBefore = _commands.Count;
        int unitsBefore = Units.LiveUnits.Count;
        string stockBefore = Stock.GetNode<Label>("Resources/Resource0/Amount").Text;
        int bytesBefore = checked((int)_wire.Length);
        _slots[7].EmitSignal(BaseButton.SignalName.Pressed);
        string expected = $"TRAIN {Buildings.SelectedBuilding.BuildingId} 0";
        Check(_commands.Count == commandsBefore + 1 && _commands[^1] == expected,
            "One worker click includes the selected town hall and unit type, without a player ID");
        Check(Encoding.UTF8.GetString(_wire.ToArray().AsSpan(bytesBefore)) == expected + System.Environment.NewLine,
            "The existing Main and NetClient path writes exactly one correctly framed training line");
        Check(Units.LiveUnits.Count == unitsBefore && Stock.GetNode<Label>("Resources/Resource0/Amount").Text == stockBefore,
            "A training request does not create a local unit or deduct local stock");
        Expect("Sending a worker request preserves the hall selection", (7, "일꾼"));
    }

    private async Task CheckBarracksProduction()
    {
        Receive("BUILDING 4 601 1 -10 15 0");
        Receive("BUILDING 4 602 1 0 15 0");
        Receive("BUILDING 4 603 1 10 15 0");
        Receive("CONSTRUCTION 603 20");
        Receive("BUILDING 4 604 2 20 15 0");
        Receive("HP 601 800 800");
        Receive("STATS 601 0 0 0 0 6");
        SelectBuilding(601);
        Expect("A completed friendly barracks trains both soldiers", (7, "검방병"), (8, "궁수"));
        Check(_slots[7].TooltipText.Contains("200") && _slots[8].TooltipText.Contains("인구 2"), "Both soldiers display their initial cost and supply");
        int unitCount = Units.LiveUnits.Count;
        string stock = Stock.GetNode<Label>("Resources/Resource0/Amount").Text;
        foreach (int slot in new[] { 7, 8 })
        {
            int before = _commands.Count, bytes = checked((int)_wire.Length);
            _slots[slot].EmitSignal(BaseButton.SignalName.Pressed);
            string expected = $"TRAIN 601 {(slot == 7 ? 1 : 2)}";
            Check(_commands.Count == before + 1 && _commands[^1] == expected &&
                Encoding.UTF8.GetString(_wire.ToArray().AsSpan(bytes)) == expected + System.Environment.NewLine,
                "Each barracks button sends the selected building ID and correct unit type exactly once");
        }
        Check(Units.LiveUnits.Count == unitCount && Stock.GetNode<Label>("Resources/Resource0/Amount").Text == stock,
            "Training never predicts local spending or spawns a local soldier");
        int count = _commands.Count;
        Units.RequestTrainWorker();
        Check(_commands.Count == count, "Barracks cannot train workers");
        Receive("QUEUE 601 25 1 2");
        var details = GetNode<SelectionDetails>("SelectionUI/SelectionDetails");
        var progress = details.FindChildren("ProductionProgress", "ProgressBar", true, false).OfType<ProgressBar>().Single();
        Check(progress.IsVisibleInTree() && progress.Value == 25 && !progress.ShowPercentage,
            "Server production fills a bar in the information panel without a percent button");
        Expect("Command slots contain only production actions", (7, "검방병"), (8, "궁수"));
        var queue = details.FindChildren("Queue", "HBoxContainer", true, false).OfType<HBoxContainer>().Single();
        Check(queue.GetChildCount() == 5 && queue.GetChild(0).GetChild<TextureRect>(0).Texture != null &&
            queue.GetChild(1).GetChild<TextureRect>(0).Texture != null && queue.GetChild(2).GetChild<TextureRect>(0).Texture == null,
            "Production queue shows current and waiting portraits, with vacant slots empty");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Check(details.GetGlobalRect().Encloses(queue.GetGlobalRect()), "Production queue fits inside the information panel");
        SelectBuilding(602);
        Check(!progress.IsVisibleInTree(), "Another empty producer hides the previous progress");
        Expect("Another barracks starts with its own empty queue", (7, "검방병"), (8, "궁수"));
        _slots[8].EmitSignal(BaseButton.SignalName.Pressed);
        Check(_commands[^1] == "TRAIN 602 2", "Changing selection changes the producer ID");
        Receive("QUEUE 602 75 2");
        SelectBuilding(601);
        Check(progress.Value == 25 && progress.IsVisibleInTree(), "Switching back restores the first building's progress");
        foreach (string invalid in new[]
        {
            "QUEUE 601 -1 1", "QUEUE 601 101 1", "QUEUE 601 25 0", "QUEUE 601 25 99",
            "QUEUE 601 25", "QUEUE 601 nope 1", "QUEUE 601 25 1 1 1 1 1 1", "QUEUE 0 25 1", "QUEUE 999 50 1"
        }) Receive(invalid);
        Check(progress.Value == 25, "Malformed queues cannot corrupt current progress");
        Receive("QUEUE 601 30 1 2 1 2 1");
        count = _commands.Count;
        _slots[7].EmitSignal(BaseButton.SignalName.Pressed);
        _slots[8].EmitSignal(BaseButton.SignalName.Pressed);
        Check(_slots[7].Disabled && _slots[8].Disabled && _commands.Count == count, "Full queue disables further production");
        Receive("QUEUE 601 100 1");
        Check(progress.Value == 100 && details.FindChildren("*", "Label", true, false).OfType<Label>().Any(label => label.IsVisibleInTree() && label.Text.Contains("출구 대기")),
            "A completed unit with no exit keeps its full progress bar and waiting status");
        Receive("QUEUE 601 0");
        Check(!progress.IsVisibleInTree(), "Completing production hides the production area");
        Expect("Completing all work clears progress and unlocks both buttons", (7, "검방병"), (8, "궁수"));

        foreach (uint id in new uint[] { 603, 604 })
        {
            SelectBuilding(id);
            Expect("Unfinished or enemy barracks expose no production actions");
            count = _commands.Count;
            Units.RequestTrain(1);
            Units.RequestTrain(2);
            Check(_commands.Count == count, "Client rejects wrong-team and unfinished producers");
        }
        Receive("QUEUE 604 50 1");
        Check(Buildings.SelectedBuilding.ProductionQueue.Count == 0, "Enemy production snapshots are ignored");
        SelectBuilding(603);
        Receive("CONSTRUCTION 603 100");
        Expect("Finishing construction enables production immediately", (7, "검방병"), (8, "궁수"));
        Receive("REMOVE 603");
        Expect("Destroying the selected barracks clears its commands and progress");
        count = _commands.Count;
        Units.RequestTrain(1);
        Check(_commands.Count == count, "Destroyed producer ID cannot be reused");
        SelectBuilding(601);
        Receive("QUEUE 601 80 2");
        Receive("BUILDING 4 601 2 -10 15 0");
        Expect("Losing the barracks team hides production");
        Receive("BUILDING 4 601 1 -10 15 0");
        Check(Buildings.SelectedBuilding.ProductionQueue.Count == 0, "Changed ownership cannot expose stale queue state");
        SelectBuilding(201);
    }

    private async Task CheckCancellation()
    {
        SelectBuilding(601);
        Receive("QUEUE 601 40 9001:1:7 9002:2:7 9003:1:9");
        Building producer = Buildings.SelectedBuilding;
        Expect("Owned active production shows cancel", (7, "검방병"), (8, "궁수"), (3, "생산 취소\n(Esc)"));
        Check(_slots[3].TooltipText.Contains("75%"), "Cancellation explains the refund rate");
        var details = GetNode<SelectionDetails>("SelectionUI/SelectionDetails");
        var queue = details.FindChildren("Queue", "HBoxContainer", true, false).OfType<HBoxContainer>().Single();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        string stockBefore = Stock.GetNode<Label>("Resources/Resource0/Amount").Text;
        int before = _commands.Count;
        long bytes = _wire.Length;
        Vector2 click = queue.GetChild<Control>(1).GetGlobalRect().GetCenter();
        using (var down = new InputEventMouseButton { Position = click, GlobalPosition = click, ButtonIndex = MouseButton.Left, Pressed = true, ButtonMask = MouseButtonMask.Left }) GetViewport().PushInput(down, true);
        using (var up = new InputEventMouseButton { Position = click, GlobalPosition = click, ButtonIndex = MouseButton.Left, Pressed = false }) GetViewport().PushInput(up, true);
        Check(_commands.Count == before + 1 && _commands[^1] == "CANCEL_TRAIN 601 9002" &&
            Encoding.UTF8.GetString(_wire.ToArray().AsSpan((int)bytes)) == "CANCEL_TRAIN 601 9002" + System.Environment.NewLine,
            "Clicking the queued portrait sends exactly that stable job ID");
        Check(producer.ProductionJobs.Count == 3 && producer.ProductionPercent == 40 && Stock.GetNode<Label>("Resources/Resource0/Amount").Text == stockBefore,
            "Cancel click does not predict queue removal, progress or refund");
        Check(Buildings.SelectedBuilding == producer && Units.SelectedUnitIds.Count == 0, "Queue click does not select the world beneath it");

        foreach (string malformed in new[] { "QUEUE 601 50 0:1:7", "QUEUE 601 50 9001:1:0", "QUEUE 601 50 9001:1:7 9001:2:7", "QUEUE 601 50 9001:1:7 2", "QUEUE 601 50 9001:3:7", "QUEUE 601 50 9001:1:7:8" }) Receive(malformed);
        Check(producer.ProductionPercent == 40 && producer.ProductionJobs.Count == 3, "Malformed job IDs and owners do not corrupt the queue");
        _slots[3].EmitSignal(BaseButton.SignalName.Pressed);
        Check(_commands[^1] == "CANCEL_TRAIN 601 9001", "Command button cancels active production");
        var input = GetNode<PlayerInput>("PlayerInput");
        before = _commands.Count;
        input._Input(KeyEvent(Key.Escape, true));
        input._Input(KeyEvent(Key.Escape, true, echo: true));
        input._Input(KeyEvent(Key.Escape, false));
        Check(_commands.Count == before + 1 && _commands[^1] == "CANCEL_TRAIN 601 9001", "Escape repeats do not cancel multiple items");
        Receive("QUEUE 601 0 9002:2:7 9003:1:9");
        before = _commands.Count;
        Units.RequestCancelTraining(producer, 9001);
        Units.RequestCancelTraining(producer, 9003);
        Check(_commands.Count == before, "Stale jobs and another payer's reservation cannot be canceled");
        Receive("QUEUE 601 0 9003:1:9 9004:2:7");
        Check(_slots[3].Disabled, "Another player's active production has no cancel action");
        Units.RequestCancelTraining(producer, 9004);
        Check(_commands[^1] == "CANCEL_TRAIN 601 9004", "The player can still cancel their own waiting job");
        Receive("QUEUE 601 0");
        before = _commands.Count;
        Units.RequestCancelTraining(producer, 9004);
        Check(_commands.Count == before && _slots[3].Disabled, "An empty queue cannot send stale cancellations");

        Receive("BUILDING 3 701 1 -15 18 0");
        Receive("CONSTRUCTION 701 35 7");
        Receive("HP 701 140 400");
        SelectBuilding(701);
        Building site = Buildings.SelectedBuilding;
        Expect("The payer can cancel unfinished construction", (3, "건설 취소\n(Esc)"));
        _slots[3].EmitSignal(BaseButton.SignalName.Pressed);
        Check(_commands[^1] == "CANCEL_BUILD 701" && Buildings.TryGetBuilding(701, out _) && site.ConstructionPercent == 35,
            "Construction click sends the building ID and waits for authoritative removal");
        before = _commands.Count;
        KeyStroke(input, Key.Escape);
        Check(_commands.Count == before + 1 && _commands[^1] == "CANCEL_BUILD 701", "Escape cancels owned construction");
        Receive("CONSTRUCTION 701 35 9");
        Expect("Same-team construction funded by someone else cannot be canceled");
        before = _commands.Count;
        Units.RequestCancelConstruction(site);
        Check(_commands.Count == before, "The current builder or team does not replace the payer");
        Receive("CONSTRUCTION 701 35 7");
        Receive("CONSTRUCTION 701 100 7");
        Expect("Completed construction no longer offers cancellation");
        Units.RequestCancelConstruction(site);
        Check(_commands.Count == before, "Completed buildings cannot be canceled");
        Receive("CONSTRUCTION 701 35 7");
        Receive("BUILDING 3 701 2 -15 18 0");
        Expect("Enemy construction cannot be canceled");
        Units.RequestCancelConstruction(site);
        Check(_commands.Count == before, "Wrong-team cancellation cannot be sent");
        Receive("BUILDING 3 701 1 -15 18 0");
        Receive("REMOVE 701");
        Receive("STOCK 0 212");
        Units.RequestCancelConstruction(site);
        Check(_commands.Count == before && Stock.GetNode<Label>("Resources/Resource0/Amount").Text == "212", "Server removal and refund apply once; stale building references cannot cancel");
        SelectBuilding(201);
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
        Receive($"WELCOME {player} {team} COMMANDER");
    }

    private void Receive(string message) => InvokeMain("OnMessage", message);
    private void InvokeMain(string name, params object[] args) => typeof(Main).GetMethod(name, PrivateInstance).Invoke(this, args);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
