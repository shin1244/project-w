using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

// 실제 HUD와 GUI 입력을 사용합니다. 서버 대신 Main에 스냅샷을 주입합니다.
public partial class SelectionDetailsChecks : Main
{
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private SelectionDetails _details;
    private GridContainer Grid => _details.GetNode<GridContainer>("Content/Body/UnitGrid");
    private Control Single => _details.GetNode<Control>("Content/Body/Single");
    private bool _capture;
    private int _commands;

    public override async void _Ready()
    {
        try
        {
            _capture = OS.GetCmdlineUserArgs().Contains("--selection-capture");
            typeof(Main).GetField("_net", Private).SetValue(this, GetNode<NetClient>("/root/Net"));
            _details = GetNode<SelectionDetails>("SelectionUI/SelectionDetails");
            Units.CommandRequested += _ => _commands++;
            Fog.Configure(Map);
            await Layout();
            CheckLayout();
            Check(!Single.Visible && !Grid.Visible, "Empty selection has no stale unit/building contents");
            Receive($"MAP 2 {Map.MapHash}");
            Receive("WORLD_READY");
            Receive("WELCOME 7 1");
            Receive("SIGHT UNIT 0 8");
            Receive("SIGHT UNIT 1 8");
            Receive("SIGHT UNIT 2 8");
            Receive("SIGHT UNIT 3 8");
            Receive("SIGHT UNIT 4 8");
            Receive("SIGHT BUILDING 0 12");
            Receive("SIGHT BUILDING 1 9");
            Receive("STOCK 0 240");
            for (int i = 0; i < 64; i++)
            {
                Receive($"UNIT {i % 3} {101 + i} 7 {-18 + i % 8 * 3} {-15 + i / 8 * 3} 1");
                if (i > 0) Receive($"HP {101 + i} {20 + i % 4 * 20} 100");
                Receive($"STATE {101 + i} IDLE 0 0 0");
            }
            Receive("UNIT 1 999 8 12 0 2");
            Receive("BUILDING 0 201 1 -24 8 0");
            Receive("BUILDING 1 202 2 14 8 0");
            Receive("HP 201 740 1000");
            Receive("HP 202 220 500");

            InvokeMain("SelectUnit", Unit(101));
            await Layout();
            Check(Single.Visible && !Grid.Visible && Labels().Any(label => label.Text == "체력   — / —"), "Single selection does not invent HP before a server snapshot");
            var unitPortrait = Single.FindChildren("Portrait", "TextureRect", true, false).OfType<TextureRect>().Single();
            Check(unitPortrait.Texture is CompressedTexture2D && unitPortrait.Texture.ResourcePath == "res://ui/portraits/worker.png",
                "Selection loads a pre-imported PNG instead of rendering a 3D portrait");
            Receive("HP 101 32.5 50");
            Receive("STATE 101 GATHER 7 0 1");
            await Layout();
            Check(Labels().Any(label => label.Text == "체력   32.5 / 50") &&
                Labels().Any(label => label.Text == "운반 중인 목재   7") &&
                Labels().Any(label => label.Text == "상태   채집 중"), "HP, activity and carrying update without reselection");
            await Capture("selection-single");
            await CheckInspection();
            foreach (uint id in new uint[] { 102, 103 })
            {
                InvokeMain("SelectUnit", Unit(id));
                await Layout();
                Check(!Labels().Any(label => label.IsVisibleInTree() && label.Text.StartsWith("운반 중인 목재")), "Combat units do not retain worker-only details");
                await Capture(id == 102 ? "selection-knight" : "selection-archer");
            }

            SelectAll();
            for (uint id = 113; id <= 164; id++) Units.SelectFromPortrait(id, true, false);
            await Layout();
            Check(Units.SelectedUnitIds.Count == 12, "A mixed twelve-unit group retains each selected type");
            await Capture("selection-group");

            SelectAll();
            await Layout();
            Check(Units.SelectedUnitIds.Count == 64 && Grid.Visible && !Single.Visible, "All 64 owned units use multi-selection; enemy is excluded");
            Check(Grid.GetChildCount() == _details.PageSize && _details.PageCount > 1, "Multiple pages retain the full selection");
            Check(Grid.GetChild<SelectionUnitCard>(0).UnitId == 101 && Grid.GetChild<SelectionUnitCard>(1).UnitId == 104, "Portraits sort by unit type and stable id");
            Receive("HP 101 5 50");
            await Layout();
            Check(Grid.GetChild<SelectionUnitCard>(0).TooltipText.Contains("5 / 50"), "Each portrait reflects changing health");
            CheckCardsInside();
            await Capture("selection-multiple");
            await CheckHudInput();
            var seen = new HashSet<uint>();
            var pages = _details.GetNode<HBoxContainer>("Content/Pages");
            Button next = pages.GetChild<Button>(2);
            do
            {
                foreach (SelectionUnitCard card in Grid.GetChildren()) seen.Add(card.UnitId);
                CheckCardsInside();
                if (next.Disabled) break;
                next.EmitSignal(BaseButton.SignalName.Pressed);
                await Layout();
            } while (true);
            Check(seen.SetEquals(Units.SelectedUnitIds), "Every selected unit is reachable through the pages");
            Check(Units.SelectedUnitIds.Count == 64, "Page navigation never narrows command selection");
            uint lastPageId = Grid.GetChild<SelectionUnitCard>(0).UnitId;
            await ClickCard(lastPageId);
            Check(Units.SelectedUnitIds.SequenceEqual(new[] { lastPageId }) && Single.Visible,
                $"A real GUI click selects only that portrait (expected {lastPageId}, got {string.Join(',', Units.SelectedUnitIds)}, single={Single.Visible})");

            SelectAll();
            await Layout();
            await ClickCard(101, shift: true);
            Check(Units.SelectedUnitIds.Count == 63 && !Units.SelectedUnitIds.Contains(101u), "Shift-click excludes exactly one unit");
            SelectAll();
            await Layout();
            await ClickCard(101, ctrl: true);
            Check(Units.SelectedUnitIds.Count == 22 && Units.SelectedUnitIds.All(id => Unit(id).UnitType == 0), "Ctrl-click retains only the clicked type in the selection");
            await ClickCard(101, shift: true, ctrl: true);
            Check(Units.SelectedUnitIds.Count == 0 && !Single.Visible && !Grid.Visible, "Ctrl+Shift can exclude an entire type and return to the empty state");
            SelectAll();
            Units.SelectFromPortrait(999, false, false);
            Check(Units.SelectedUnitIds.Count == 64, "An unselected or enemy id cannot enter the portrait selection path");
            await Layout();
            next.EmitSignal(BaseButton.SignalName.Pressed);
            for (uint id = 102; id <= 164; id++) Receive($"REMOVE {id}");
            await Layout();
            Check(Single.Visible && Units.SelectedUnitIds.SequenceEqual(new uint[] { 101 }), "Deaths on other pages collapse to the surviving single unit");
            Receive("HIDE 101");
            Check(!Single.Visible && !Grid.Visible, "HIDE immediately clears selection details");

            InvokeMain("SelectBuilding", Buildings.LiveBuildings.Single(building => building.BuildingId == 201));
            await Layout();
            Check(Single.Visible && Labels().Any(label => label.Text.StartsWith("회관")) && Labels().Any(label => label.Text == "체력   740 / 1000"), "Building displays its name and current HP");
            await Capture("selection-building");
            InvokeMain("SelectBuilding", Buildings.LiveBuildings.Single(building => building.BuildingId == 202));
            Receive("STATE 202 ATTACK 0 201 1");
            await Layout();
            Check(Labels().Any(label => label.Text == "요새") && Labels().Any(label => label.Text == "상태   공격 중"), "Type 1 fortress displays its name and server activity");
            await Capture("selection-tower");
            var portrait = Single.FindChildren("Portrait", "TextureRect", true, false).OfType<TextureRect>().Single();
            Texture2D enemyPortrait = portrait.Texture;
            Receive("WELCOME 7 2");
            Check(portrait.Texture != enemyPortrait, "Team changes still update the selected building's portrait colors");
            Receive("REMOVE 202");
            Check(!Single.Visible, "Building destruction clears details");
            InvokeMain("SelectBuilding", Buildings.LiveBuildings.Single());
            Receive($"MAP 2 {Map.MapHash}");
            Check(!Single.Visible && !Grid.Visible, "Map resynchronization clears details");
            Receive("WORLD_READY");
            Receive("WELCOME 7 1");
            Receive("UNIT 0 301 7 0 0 1");
            InvokeMain("SelectUnit", Unit(301));
            Receive("UNIT 0 301 9 0 0 1");
            Check(!Single.Visible, "Ownership changes clear the previous unit details");
            Receive("UNIT 0 301 7 0 0 1");
            InvokeMain("SelectUnit", Unit(301));
            InvokeMain("OnConnectionClosed", "Selection details test disconnect");
            Check(!Single.Visible && !Grid.Visible, "Disconnect clears details");
            Check(_commands == 0, "All inspection and selection gestures send no server commands");
            Check(_details.FindChildren("*", "SubViewport", true, false).Count == 0 &&
                _details.FindChildren("*", "Node3D", true, false).Count == 0,
                "Unit and building selections never create portrait viewports, models, lights or cameras");
            GD.Print("PASS: selection details layout, live HP/STATE, 64-unit paging, real GUI click/Shift/Ctrl, input isolation, building details, ownership/team/removal/HIDE/reset/disconnect");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private void CheckLayout()
    {
        Rect2 details = _details.GetGlobalRect(), map = Minimap.GetGlobalRect(), commands = Commands.GetGlobalRect();
        Check(Mathf.IsEqualApprox(details.Position.X, map.End.X) && Mathf.IsEqualApprox(details.End.X, commands.Position.X) &&
            Mathf.IsEqualApprox(details.End.Y, commands.End.Y) && Mathf.IsEqualApprox(details.Size.Y, 186), "Details fill the gap with a shorter, bottom-anchored panel");
        Check(_details.MouseFilter == Control.MouseFilterEnum.Stop && !_details.MouseForcePassScrollEvents, "Details block world clicks and scroll events");
    }

    private async Task CheckInspection()
    {
        Receive("STATS 101 5 0.5 1 5 8");
        await Layout();
        Check(Labels().Any(label => label.Text.Contains("공격력 5") && label.Text.Contains("사거리 0.5") && label.Text.Contains("이동 속도 5")),
            "Info panel uses server damage, range, attack interval, speed and sight");
        foreach (string invalid in new[] { "STATS 101 NaN 1 1 1 1", "STATS 101 -1 1 1 1 1", "STATS 0 1 1 1 1 1", "STATS 101 1 1 1 1", "STATS 101 1 1 1 Infinity 1" }) Receive(invalid);
        Check(Unit(101).Stats?.Damage == 5, "Malformed stats do not overwrite the last known server values");
        Receive("STATS 101 7 0.75 1.2 6 9");
        await Layout();
        Check(Labels().Any(label => label.Text.Contains("공격력 7") && label.Text.Contains("사거리 0.75")), "Stats update without reselection");
        Receive("HP 999 61 100");
        Receive("STATS 999 12 0.5 1 5 8");
        Receive("STATE 999 GUARD 0 0 0");
        Receive("UNIT 2 998 9 10 0 1");
        Receive("UNIT 3 997 0 8 0 2");
        Receive("STATS 997 4 0.4 1.4 4 6");
        foreach (uint id in new uint[] { 999, 998, 997 })
        {
            InvokeMain("SelectUnit", Unit(id));
            await Layout();
            Check(Units.InspectedUnit == Unit(id) && Units.SelectedUnitIds.Count == 0 && Single.Visible,
                "Enemy, allied other-owner and minion clicks all inspect without entering the command selection");
            Check(Commands.FindChildren("*", "Button", true, false).OfType<Button>().All(button => button.Disabled), "Foreign inspection exposes no unit commands");
            Units.RequestMove(Vector3.Zero);
            Units.RequestAttackMove(Vector3.Zero);
            Units.RequestAttack(Unit(999));
            Units.SaveControlGroup(8);
            Check(!Units.RecallControlGroup(8), "Inspected foreign units cannot enter a control group");
            if (id == 999)
            {
                Check(Labels().Any(label => label.Text == "체력   61 / 100") && Labels().Any(label => label.Text == "상태   경계 중"),
                    "Enemy inspection shows current server health and guarding state");
                Check(Single.FindChildren("Portrait", "TextureRect", true, false).OfType<TextureRect>().Single().Texture.ResourcePath == "res://ui/portraits/knight-enemy.png",
                    "Enemy inspection uses the opposing team's portrait colors");
                await Capture("selection-enemy");
            }
            if (id == 997)
            {
                Check(Single.FindChildren("Portrait", "TextureRect", true, false).OfType<TextureRect>().Single().Texture.ResourcePath == "res://ui/portraits/minion-knight-enemy.png",
                    "Inspected minions retain their own model portrait");
                Check(Labels().Any(label => label.Text == "근접 미니언") && Labels().Any(label => label.Text.Contains("공격력 4") && label.Text.Contains("시야 6")),
                    "Minion inspection shows its independent name and combat stats");
            }
        }
        InvokeMain("SelectUnit", Unit(999));
        Receive("HIDE 999");
        Check(Units.InspectedUnit == null && !Single.Visible, "Losing vision immediately clears enemy inspection");
        Receive("UNIT 1 999 8 12 0 2");
        InvokeMain("SelectUnit", Unit(997));
        Receive("REMOVE 997");
        Check(!Single.Visible && Units.InspectedUnit == null, "Inspected minion death clears the info panel");

        Building enemy = Buildings.LiveBuildings.Single(building => building.BuildingId == 202);
        bool visible = false;
        Buildings.VisibilityCheck = _ => visible;
        InvokeMain("SelectBuilding", enemy);
        Check(Buildings.SelectedBuilding == null, "A building hidden by fog cannot be inspected");
        visible = true;
        Receive("STATS 202 15 7 1.5 0 11");
        InvokeMain("SelectBuilding", enemy);
        await Layout();
        Check(Single.Visible && Labels().Any(label => label.Text.Contains("공격력 15") && label.Text.Contains("사거리 7")), "Visible enemy buildings show combat stats");
        visible = false;
        await Layout();
        Check(Buildings.SelectedBuilding == null && !Single.Visible, "Losing building vision closes its current-status panel");
        Buildings.VisibilityCheck = null;
        InvokeMain("SelectUnit", Unit(101));
    }

    private void CheckCardsInside()
    {
        foreach (SelectionUnitCard card in Grid.GetChildren())
            Check(_details.GetGlobalRect().Encloses(card.GetGlobalRect()), "Every page's portrait is inside the panel");
    }

    private async Task ClickCard(uint id, bool shift = false, bool ctrl = false)
    {
        var card = Grid.GetChildren().OfType<SelectionUnitCard>().Single(item => item.UnitId == id);
        Vector2 position = card.GetGlobalRect().GetCenter();
        using var motion = new InputEventMouseMotion { Position = position, GlobalPosition = position };
        GetViewport().PushInput(motion, true);
        PushMouse(position, MouseButton.Left, true, shift, ctrl);
        PushMouse(position, MouseButton.Left, false, shift, ctrl);
        await Layout();
    }

    private async Task CheckHudInput()
    {
        var input = GetNode<PlayerInput>("PlayerInput");
        int worldClicks = 0;
        input.ContextClicked += (_, _) => worldClicks++;
        float cameraSize = Units.Camera.Size;
        Vector2 blank = _details.GetGlobalRect().Position + new Vector2(_details.Size.X - 12, 10);
        PushMouse(blank, MouseButton.Right, true);
        PushMouse(blank, MouseButton.Right, false);
        PushMouse(blank, MouseButton.WheelUp, true);
        PushMouse(blank, MouseButton.WheelUp, false);
        PushMouse(Grid.GetChild<SelectionUnitCard>(0).GetGlobalRect().GetCenter(), MouseButton.Right, true);
        PushMouse(Grid.GetChild<SelectionUnitCard>(0).GetGlobalRect().GetCenter(), MouseButton.Right, false);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        Check(worldClicks == 0 && Mathf.IsEqualApprox(cameraSize, Units.Camera.Size), "Right clicks and wheels over the HUD never reach world orders or camera zoom");
    }

    private void PushMouse(Vector2 position, MouseButton button, bool pressed, bool shift = false, bool ctrl = false)
    {
        using var input = new InputEventMouseButton
        {
            Position = position, GlobalPosition = position, ButtonIndex = button, Pressed = pressed,
            ShiftPressed = shift, CtrlPressed = ctrl,
            ButtonMask = pressed ? (MouseButtonMask)(1 << ((int)button - 1)) : 0
        };
        GetViewport().PushInput(input, true);
    }

    private IEnumerable<Label> Labels() => _details.FindChildren("*", "Label", true, false).OfType<Label>();
    private Unit Unit(uint id) => Units.LiveUnits.Single(unit => unit.UnitId == id);
    private void SelectAll() => InvokeMain("SelectBox", GetViewport().GetVisibleRect());
    private void Receive(string message) => InvokeMain("OnMessage", message);
    private void InvokeMain(string method, params object[] args) => typeof(Main).GetMethod(method, Private).Invoke(this, args);
    private async Task Layout()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
    private async Task Capture(string name)
    {
        if (!_capture) return;
        Fog.RefreshVision();
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using Image image = GetViewport().GetTexture().GetImage();
        Check(image.SavePng($"res://.godot/{name}.png") == Error.Ok, "Save rendered selection preview");
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
