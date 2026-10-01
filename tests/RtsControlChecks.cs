using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

// 실제 Main 연결과 Viewport 입력 경로를 사용하며 네트워크 출력만 메모리에 기록합니다.
public partial class RtsControlChecks : Main
{
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<string> _commands = new();
    private PlayerInput _input;

    public override async void _Ready()
    {
        try
        {
            var net = GetNode<NetClient>("/root/Net");
            using var wire = new MemoryStream();
            using var writer = new StreamWriter(wire, new UTF8Encoding(false)) { AutoFlush = true };
            typeof(NetClient).GetField("_writer", Private).SetValue(net, writer);
            typeof(Main).GetField("_net", Private).SetValue(this, net);
            InvokeMain("ConnectInput");
            Units.CommandRequested += command => { _commands.Add(command); InvokeMain("SendCommand", command); };
            _input = GetNode<PlayerInput>("PlayerInput");
            Fog.Configure(Map);
            BeginSession();
            foreach (string message in new[]
            {
                "UNIT 0 101 7 -8 -10 1", "UNIT 0 102 7 0 -10 1", "UNIT 1 103 7 8 -10 1",
                "UNIT 2 104 7 0 0 1", "UNIT 0 105 8 14 -10 2", "UNIT 0 106 9 20 -10 1",
                "UNIT 0 107 7 75 35 1", "BUILDING 0 201 1 -18 -10 0"
            }) Receive(message);
            Units.Camera.Size = 60;
            CameraNavigation.FocusGround(Units.Camera, Vector3.Zero);
            await Flush();

            ClickUnit(101);
            await Flush();
            Expect(101);
            ClickUnit(102, shift: true);
            await Flush();
            Expect(101, 102);
            ClickUnit(101, shift: true);
            await Flush();
            Expect(102);
            ClickUnit(105, shift: true);
            ClickUnit(106, shift: true);
            Click(Units.Camera.UnprojectPosition(new Vector3(40, 0, -15)), MouseButton.Left, shift: true);
            await Flush();
            Expect(102);

            InvokeMain("SelectUnit", Unit(101));
            Rect2 box = UnitBox(101, 102);
            Drag(box.Position, box.End, true);
            await Flush();
            Expect(101, 102);
            Drag(box.End, box.Position, true);
            await Flush();
            Expect();
            InvokeMain("SelectUnit", Unit(104));
            Drag(box.Position, box.End, false);
            await Flush();
            Expect(101, 102);
            ClickUnit(101);
            ClickUnit(101, twice: true);
            await Flush();
            Expect(101, 102); // 다른 소유자와 화면 밖의 같은 종류는 제외합니다.
            ClickUnit(103);
            ClickUnit(101, shift: true);
            ClickUnit(101, shift: true, twice: true);
            await Flush();
            Expect(101, 102, 103);

            // 같은 물리 프레임에 선택 -> 저장 순서를 보존합니다.
            ClickUnit(101);
            ClickUnit(102, shift: true);
            KeyPress(Key.Key1, ctrl: true);
            await Flush();
            ClickUnit(103);
            KeyPress(Key.Key2);
            await Flush();
            Expect(103); // Empty group is a no-op.
            Vector3 beforeRecall = Units.Camera.GlobalPosition;
            KeyPress(Key.Key1);
            await Flush();
            Expect(101, 102);
            Check(Units.Camera.GlobalPosition.IsEqualApprox(beforeRecall), "One group press selects without moving the camera");
            KeyPress(Key.Key1, echo: true);
            await Flush();
            Check(Units.Camera.GlobalPosition.IsEqualApprox(beforeRecall), "Key repeat cannot masquerade as a second tap");
            // 두 키를 한 프레임에 넣어 렌더러/CI의 프레임 시간에 영향받지 않습니다.
            _input.CancelTargeting();
            KeyPress(Key.Key1);
            KeyPress(Key.Key1);
            await Flush();
            Expect(101, 102);
            Check(GroundCenter().DistanceTo(new Vector3(-4, 0, -10)) < .02f, "Double tap focuses the selected group's centroid without changing zoom");
            Check(Mathf.IsEqualApprox(Units.Camera.Size, 60), "Group focus preserves zoom");
            Check(_commands.Count == 0, "Selection and control groups never send network commands");

            // 미니맵 클릭은 선택을 유지하고 공격 대상 지정 모드를 종료합니다.
            KeyPress(Key.A);
            Vector3 panTarget = new(12, 0, 8);
            Click(MiniPoint(panTarget), MouseButton.Left);
            await Flush();
            Expect(101, 102);
            Check(!_input.IsAttackTargeting && GroundCenter().DistanceTo(panTarget) < .03f, "Minimap click pans to map coordinates and cancels A targeting");
            ClickUnit(104);
            await Flush();
            Expect(104);
            Units.RecallControlGroup(1);
            Rect2 footprint = Minimap.CameraRect;
            Check(footprint.HasArea() && Minimap.MapRect.Encloses(footprint), "Camera footprint is clipped to the actual minimap terrain");
            Units.Camera.Size = 30;
            await Flush();
            Check(Minimap.CameraRect.Size.X < footprint.Size.X * .6f, "Zoom shrinks the camera footprint");
            Vector3 dragTarget = new(-20, 0, -12);
            Drag(MiniPoint(panTarget), MiniPoint(dragTarget), false);
            await Flush();
            Check(GroundCenter().DistanceTo(dragTarget) < .03f, "Minimap drag follows the pointer");
            Vector2 outside = new(GetViewport().GetVisibleRect().Size.X - 30, 200);
            Drag(MiniPoint(dragTarget), outside, false);
            await Flush();
            Vector3 afterOutsideRelease = Units.Camera.GlobalPosition;
            Motion(GetViewport().GetVisibleRect().GetCenter());
            await Flush();
            Check(Units.Camera.GlobalPosition.IsEqualApprox(afterOutsideRelease), "Releasing outside the minimap ends panning");
            Expect(101, 102);

            Mouse(MiniPoint(Vector3.Zero), MouseButton.Left, true);
            GetWindow().EmitSignal(Window.SignalName.FocusExited);
            Vector3 afterFocusLoss = Units.Camera.GlobalPosition;
            Motion(MiniPoint(new Vector3(-30, 0, 20)));
            Mouse(MiniPoint(Vector3.Zero), MouseButton.Left, false);
            Check(Units.Camera.GlobalPosition.IsEqualApprox(afterFocusLoss), "Focus loss cancels minimap dragging");
            Mouse(MiniPoint(Vector3.Zero), MouseButton.Left, true);
            KeyPress(Key.Escape);
            Vector3 afterEscape = Units.Camera.GlobalPosition;
            Motion(MiniPoint(new Vector3(-30, 0, 20)));
            Mouse(MiniPoint(Vector3.Zero), MouseButton.Left, false);
            Check(Units.Camera.GlobalPosition.IsEqualApprox(afterEscape), "Escape cancels minimap dragging");

            Vector2 border = Minimap.GetGlobalTransformWithCanvas() * Vector2.One;
            Vector3 beforeBorder = Units.Camera.GlobalPosition;
            Click(border, MouseButton.Left);
            Click(border, MouseButton.Right);
            Click(MiniPoint(Vector3.Zero), MouseButton.WheelUp);
            await Flush();
            Check(_commands.Count == 0 && Units.Camera.GlobalPosition.IsEqualApprox(beforeBorder) && Units.Camera.Size == 30,
                "Minimap padding and wheel input cannot pan, zoom or issue orders");
            Check(!Minimap.TryMapToWorld(new Vector2(float.NaN, 0), out _), "Invalid minimap coordinates are rejected");

            CameraNavigation.FocusGround(Units.Camera, Vector3.Zero);
            await Flush();
            Vector3 destination = new(25, 0, -18);
            // 선택과 미니맵 명령도 같은 입력 큐를 사용합니다.
            ClickUnit(103);
            KeyPress(Key.A);
            Click(MiniPoint(destination), MouseButton.Right);
            await Flush();
            Expect(103);
            Check(!_input.IsAttackTargeting && _commands.Count == 1, "One minimap right-click sends exactly one MOVE and exits A targeting");
            string[] move = _commands.Single().Split(' ');
            Check(move.Length == 4 && move[0] == "MOVE" && move[3] == "103" &&
                Math.Abs(float.Parse(move[1], CultureInfo.InvariantCulture) - destination.X) < .02f &&
                Math.Abs(float.Parse(move[2], CultureInfo.InvariantCulture) - destination.Z) < .02f,
                "Minimap uses world coordinates and the existing type/coordinate/id MOVE format");
            Check(Encoding.UTF8.GetString(wire.ToArray()).Split('\n').Count(line => line.StartsWith("MOVE ")) == 1,
                "Exactly one MOVE reaches the existing network writer");
            Units.ClearSelection();
            Click(MiniPoint(destination), MouseButton.Right);
            await Flush();
            Check(_commands.Count == 1, "No selection sends no minimap command");

            // 제거, 숨김, 소유권 변경 후 같은 ID가 재사용되어도 이전 부대에 복귀하지 않습니다.
            Units.SelectSingle(Unit(101));
            Units.ToggleSelection(Unit(102));
            Units.ToggleSelection(Unit(103));
            KeyPress(Key.Key3, ctrl: true);
            await Flush();
            Receive("HIDE 101");
            Receive("REMOVE 102");
            Receive("UNIT 1 103 9 8 -10 1");
            Receive("UNIT 0 101 7 -8 -10 1");
            Receive("UNIT 0 102 7 0 -10 1");
            Receive("UNIT 1 103 7 8 -10 1");
            Units.SelectSingle(Unit(104));
            KeyPress(Key.Key3);
            await Flush();
            Expect(104);

            Units.Camera.Size = 60;
            CameraNavigation.FocusGround(Units.Camera, Vector3.Zero);
            for (int i = 0; i < 70; i++) Receive($"UNIT 1 {1000 + i} 7 {i % 10 - 5} {i / 10 - 8} 1");
            Units.SelectSameTypeOnScreen(Unit(1000), false);
            Check(Units.SelectedUnitIds.Count == 64 && Units.SelectedUnitIds.Contains(1000u), "Same-type selection retains the clicked unit and enforces the server's 64-unit limit");
            Units.ToggleSelection(Unit(101));
            Check(Units.SelectedUnitIds.Count == 64, "Shift-add cannot exceed the selection limit");
            KeyPress(Key.Key9, ctrl: true);
            await Flush();
            Units.ClearSelection();
            KeyPress(Key.Key9);
            await Flush();
            Check(Units.SelectedUnitIds.Count == 64, "Control groups recall a complete 64-unit group");
            Receive("WELCOME 7 2 COMMANDER");
            Units.ClearSelection();
            KeyPress(Key.Key9);
            await Flush();
            Expect();

            Units.SelectSingle(Unit(101));
            KeyPress(Key.Key4, ctrl: true);
            await Flush();
            Click(MiniPoint(destination), MouseButton.Right);
            Receive($"MAP 2 {Map.MapHash}");
            Receive("WORLD_READY");
            Receive("WELCOME 7 1 COMMANDER");
            Receive("UNIT 0 101 7 -8 -10 1");
            KeyPress(Key.Key4);
            await Flush();
            Expect();
            Check(_commands.Count == 1, "Resync clears groups and pending minimap commands before a new world can reuse IDs");
            Units.SelectSingle(Unit(101));
            KeyPress(Key.Key4, ctrl: true);
            await Flush();
            InvokeMain("OnConnectionClosed", "RTS controls test disconnect");
            KeyPress(Key.Key4);
            Click(MiniPoint(destination), MouseButton.Right);
            await Flush();
            Expect();
            Check(_commands.Count == 1, "Disconnect disables stale group recalls and minimap commands");

            typeof(NetClient).GetField("_writer", Private).SetValue(net, writer);
            await CheckAosControls();

            GD.Print("PASS: RTS selection/groups/minimap; AOS hero control and independent inspection via real input; ownership/death/respawn/resync and role changes");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private async Task CheckAosControls()
    {
        BeginSession();
        Receive("WELCOME 7 1 HERO 200");
        Units.Camera.Size = 60;
        CameraNavigation.FocusGround(Units.Camera, Vector3.Zero);
        await Flush();
        int before = _commands.Count;
        Vector3 destination = new(12, 0, 3);
        Vector2 ground = Units.Camera.UnprojectPosition(destination);
        Click(ground, MouseButton.Right);
        await Flush();
        Check(_commands.Count == before, "AOS waits for its own hero before sending orders");

        Receive("UNIT 200 501 8 8 -8 2");
        Receive("UNIT 200 502 9 -8 -8 1");
        Receive("UNIT 0 503 7 -12 -15 1");
        Receive("BUILDING 0 201 1 -20 -20 0");
        Expect();
        Receive("UNIT 200 500 7 0 -8 1");
        await Flush();
        Expect(500);
        Check(!Commands.Visible && UnitInfo.Visible && UnitInfo.DisplayedUnit == Unit(500),
            "AOS shows the own hero in the compact information panel without clicking");
        Check(Unit(500).GetNode<MeshInstance3D>("SelectionRing").Visible && !Units.CanControl(Unit(503)),
            "AOS automatically selects only the matching owned hero, even when other owned units exist");
        Click(ground, MouseButton.Right);
        await Flush();
        Check(_commands.Count == before + 1 && _commands.Last().StartsWith("MOVE ") && _commands.Last().EndsWith(" 500"),
            "First world right-click moves the hero without a prior selection click");

        ClickUnit(501);
        ClickUnit(502);
        ClickUnit(503, shift: true);
        ClickUnit(500, shift: true, twice: true);
        Click(ground, MouseButton.Left);
        Drag(UnitBox(501, 502).Position, UnitBox(501, 502).End, false);
        Click(Units.Camera.UnprojectPosition(new Vector3(-20, 1, -20)), MouseButton.Left);
        await Flush();
        Units.ClearSelection();
        Units.SelectFromPortrait(500, true, false);
        KeyPress(Key.Key1, ctrl: true);
        KeyPress(Key.Key1);
        await Flush();
        Expect(500);
        Check(Units.InspectedUnit == null && Buildings.SelectedBuilding == null && _commands.Count == before + 1 &&
            UnitInfo.DisplayedUnit == Unit(500),
            "Empty clicks, other units, buildings, drag, Shift and group inputs cannot replace or clear the AOS hero");

        ClickUnit(501);
        await Flush();
        Expect(500);
        Check(UnitInfo.InspectedUnit == Unit(501), "AOS left click inspects the enemy while the own hero stays controlled");
        Click(UnitPoint(501), MouseButton.Right);
        KeyPress(Key.A);
        Click(ground, MouseButton.Left);
        KeyPress(Key.S);
        KeyPress(Key.D);
        Click(MiniPoint(destination), MouseButton.Right);
        await Flush();
        string[] orders = _commands.Skip(before + 1).ToArray();
        Check(orders.Length == 5 && orders[0] == "ATTACK 501 500" && orders[1].StartsWith("ATTACK_MOVE ") &&
            orders[2] == "STOP 500" && orders[3] == "HOLD 500" && orders[4].StartsWith("MOVE ") &&
            orders.All(order => order.EndsWith(" 500")) && UnitInfo.InspectedUnit == Unit(501),
            "World, A/S/D and minimap orders address the hero while preserving the inspected enemy");
        Click(UnitInfo.GetGlobalRect().GetCenter(), MouseButton.Right);
        await Flush();
        Check(_commands.Count == before + 6, "Read-only information panel consumes right clicks without issuing orders");

        Receive("UNIT 200 500 9 0 -8 1");
        Expect();
        before = _commands.Count;
        Click(MiniPoint(destination), MouseButton.Right);
        await Flush();
        Check(_commands.Count == before, "Losing ownership immediately disables hero commands");
        Receive("UNIT 200 504 7 0 -8 1");
        Expect(504);
        Receive("REMOVE 504");
        Expect();
        Units.RequestMove(destination);
        Units.RequestStop();
        Check(_commands.Count == before, "A dead hero cannot receive commands");
        Receive("UNIT 200 505 7 0 -8 1");
        Expect(505);
        Receive("HIDE 505");
        Expect();
        Receive("UNIT 200 505 7 0 -8 1");
        Expect(505);
        Click(MiniPoint(destination), MouseButton.Right);
        Receive($"MAP 2 {Map.MapHash}");
        Receive("WORLD_READY");
        Receive("WELCOME 7 1 HERO 200");
        Receive("UNIT 200 506 7 0 -8 1");
        await Flush();
        Expect(506);
        Check(_commands.Count == before, "Resync binds the new hero and drops pending commands for the old session");

        await CheckWolfSkill();
        before = _commands.Count;
        Receive("WELCOME 7 1 COMMANDER");
        KeyPress(Key.Q);
        Check(!_input.IsSkillTargeting && !Skills.CanUseQ, "Returning to RTS removes the hero Q shortcut");
        Receive("UNIT 0 507 7 -8 -8 1");
        Expect();
        await Flush();
        ClickUnit(507);
        await Flush();
        Expect(507);
        Click(ground, MouseButton.Left);
        await Flush();
        Expect();
        Check(_commands.Count == before, "Returning to RTS restores manual selection and deselection");
    }

    private async Task CheckWolfSkill()
    {
        Receive("UNIT 1 950 8 3 -8 2");
        Receive("UNIT 1 951 9 -4 -8 1");
        Receive("TICK 100");
        await Flush();
        var button = Skills.GetNode<Button>("Content/Slots/Q");
        var cooldown = (Label)button.FindChild("Cooldown", true, false);
        int before = _commands.Count;
        Check(Skills.CanUseQ && !button.Disabled, "A live owned wolf can arm Q without selecting itself");
        ClickUnit(950);
        await Flush();
        KeyPress(Key.Q, ctrl: true);
        KeyPress(Key.Q, echo: true);
        Check(!_input.IsSkillTargeting, "Modified and repeated Q presses do not arm the skill");
        KeyPress(Key.Q);
        Check(_input.IsSkillTargeting && !_input.IsAttackTargeting, "Q arms enemy targeting without issuing a command");
        Check(Unit(506).GetNodeOrNull<MeshInstance3D>("SkillRangeRing") is { Visible: true } &&
            Unit(950).GetNodeOrNull<MeshInstance3D>("SkillRangeRing") == null,
            "Q shows range around the controlled hero, not the inspected enemy");
        if (OS.GetCmdlineUserArgs().Contains("--capture-skill-range"))
        {
            Receive("SIGHT UNIT 200 10");
            Receive("HP 506 300 300");
            Fog.RefreshVision();
            await Flush();
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using Image image = GetViewport().GetTexture().GetImage();
            image.SavePng("res://.godot/wolf-q-range.png");
        }
        ClickUnit(951);
        Click(Units.Camera.UnprojectPosition(new Vector3(15, 0, -8)), MouseButton.Left);
        await Flush();
        Check(_commands.Count == before && _input.IsSkillTargeting, "Allies and ground cannot consume a unit-targeted skill");
        ClickUnit(950);
        ClickUnit(950);
        await Flush();
        Check(_commands.Count == before + 1 && _commands.Last() == "SKILL 0 950" && !_input.IsSkillTargeting &&
            Skills.CanUseQ && UnitInfo.InspectedUnit == Unit(950) && Units.SelectedUnitIds.SequenceEqual(new uint[] { 506 }) &&
            Unit(506).GetNodeOrNull<MeshInstance3D>("SkillRangeRing") == null,
            "Q sends exactly slot and enemy ID, preserves hero control/inspection, and waits for server cooldown");
        Receive("SKILL 506 0 950");
        Receive("STATE 506 DASH 0 950 0");
        Check(Unit(506).State.Activity == UnitActivity.Dash, "Server DASH is accepted for facing and wolf presentation");
        Receive("COOLDOWN 0 220");
        Check(!Skills.CanUseQ && button.Disabled && cooldown.Text == "6", "COOLDOWN uses server ready tick, not a copied skill duration");
        KeyPress(Key.Q);
        Click(button.GetGlobalRect().GetCenter(), MouseButton.Left);
        await Flush();
        Check(!_input.IsSkillTargeting && _commands.Count == before + 1, "Cooldown blocks both keyboard and button casts");
        Receive("TICK 160");
        foreach (string invalid in new[] { "COOLDOWN -1 220", "COOLDOWN 5 220", "COOLDOWN 0 -1", "COOLDOWN 0 NaN",
            "COOLDOWN 0 4294967296", "COOLDOWN 0 220 extra", "COOLDOWN 1 500", "COOLDOWN 0 180", "TICK 120" }) Receive(invalid);
        Check(cooldown.Text == "3" && Skills.QRemainingSeconds == 3, "Malformed, foreign-slot and stale timing messages cannot alter Q cooldown");
        Receive("TICK 220");
        Receive("STATE 506 IDLE 0 0 0");
        Check(Skills.CanUseQ && cooldown.Text == "", "The server ready tick re-enables Q");

        Click(button.GetGlobalRect().GetCenter(), MouseButton.Left);
        Check(_input.IsSkillTargeting && Unit(506).GetNodeOrNull<MeshInstance3D>("SkillRangeRing") is { Visible: true },
            "The Q button arms targeting and shows the same range as the key");
        ClickUnit(950);
        KeyPress(Key.Escape);
        await Flush();
        Check(_commands.Count == before + 1 && !_input.IsSkillTargeting &&
            Unit(506).GetNodeOrNull<MeshInstance3D>("SkillRangeRing") == null,
            "Escape removes the range and cancels even a skill click awaiting the physics frame");
        KeyPress(Key.Q);
        Click(UnitPoint(950), MouseButton.Right);
        await Flush();
        Check(_commands.Count == before + 1 && !_input.IsSkillTargeting, "Right click cancels Q without issuing an accidental attack/move");
        KeyPress(Key.Q);
        GetWindow().EmitSignal(Window.SignalName.FocusExited);
        Check(!_input.IsSkillTargeting, "Window focus loss cancels skill targeting");
        KeyPress(Key.Q);
        KeyPress(Key.A);
        Check(!_input.IsSkillTargeting && _input.IsAttackTargeting, "Attack targeting replaces Q targeting");
        KeyPress(Key.Q);
        Check(_input.IsSkillTargeting && !_input.IsAttackTargeting, "Q replaces attack targeting");
        Receive("STATE 506 STUN 0 0 0");
        Check(Unit(506).State.Activity == UnitActivity.Stun && !Skills.CanUseQ && !_input.IsSkillTargeting,
            "Server stun immediately cancels Q and disables the skill");
        Receive("STATE 506 IDLE 0 0 0");
        Click(button.GetGlobalRect().GetCenter(), MouseButton.Left);
        ClickUnit(950);
        await Flush();
        Check(_commands.Count == before + 2 && _commands.Last() == "SKILL 0 950", "Button targeting sends the same skill request after stun ends");
        Receive("COOLDOWN 0 340");
        Receive("REMOVE 506");
        KeyPress(Key.Q);
        Receive("UNIT 200 952 7 0 -8 1");
        Check(!Skills.CanUseQ && Skills.HeroUnitId == 952 && Skills.QRemainingSeconds == 6,
            "Cooldown belongs to the player and survives hero death and a new unit ID");
        Receive("TICK 340");
        KeyPress(Key.Q);
        ClickUnit(950);
        Receive($"MAP 2 {Map.MapHash}");
        Receive("WORLD_READY");
        Receive("WELCOME 7 1 HERO 200");
        Receive("UNIT 200 953 7 0 -8 1");
        await Flush();
        Check(_commands.Count == before + 2 && !_input.IsSkillTargeting && Skills.CanUseQ && cooldown.Text == "",
            "Resync drops queued skill requests and previous cooldowns before binding the new hero");
    }

    private Vector3 GroundCenter()
    {
        Check(CameraNavigation.TryGroundPoint(Units.Camera, GetViewport().GetVisibleRect().GetCenter(), out Vector3 point), "Camera sees ground");
        return point;
    }
    private Vector2 MiniPoint(Vector3 world)
    {
        Check(Minimap.TryWorldToMap(world, out Vector2 point), "Test point is inside map");
        return Minimap.GetGlobalTransformWithCanvas() * point;
    }
    private Rect2 UnitBox(uint a, uint b)
    {
        Vector2 from = UnitPoint(a), to = UnitPoint(b);
        return new Rect2(from.Min(to) - Vector2.One * 7, (from - to).Abs() + Vector2.One * 14);
    }
    private Vector2 UnitPoint(uint id) => Units.Camera.UnprojectPosition(Unit(id).GlobalPosition + Vector3.Up);
    private Unit Unit(uint id) => Units.LiveUnits.Single(unit => unit.UnitId == id);
    private void ClickUnit(uint id, bool shift = false, bool twice = false) => Click(UnitPoint(id), MouseButton.Left, shift, twice);
    private void Click(Vector2 point, MouseButton button, bool shift = false, bool twice = false)
    {
        Motion(point);
        Mouse(point, button, true, shift, twice);
        Mouse(point, button, false, shift);
    }
    private void Drag(Vector2 from, Vector2 to, bool shift)
    {
        Motion(from);
        Mouse(from, MouseButton.Left, true, shift);
        Motion(to, true);
        Mouse(to, MouseButton.Left, false); // Press-time modifier survives release of Shift during dragging.
    }
    private void Mouse(Vector2 point, MouseButton button, bool pressed, bool shift = false, bool twice = false)
    {
        using var input = new InputEventMouseButton
        {
            Position = point, GlobalPosition = point, ButtonIndex = button, Pressed = pressed,
            ButtonMask = pressed ? (MouseButtonMask)(1 << ((int)button - 1)) : 0, ShiftPressed = shift, DoubleClick = twice
        };
        GetViewport().PushInput(input, true);
    }
    private void Motion(Vector2 point, bool held = false)
    {
        using var input = new InputEventMouseMotion { Position = point, GlobalPosition = point, ButtonMask = held ? MouseButtonMask.Left : 0 };
        GetViewport().PushInput(input, true);
    }
    private void KeyPress(Key key, bool ctrl = false, bool echo = false)
    {
        using var input = new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true, CtrlPressed = ctrl, Echo = echo };
        GetViewport().PushInput(input, true);
        input.Pressed = false;
        input.Echo = false;
        GetViewport().PushInput(input, true);
    }
    private void Expect(params uint[] ids) => Check(Units.SelectedUnitIds.ToHashSet().SetEquals(ids),
        $"Selection expected [{string.Join(',', ids)}], got [{string.Join(',', Units.SelectedUnitIds)}]");
    private void BeginSession() { Receive($"MAP 2 {Map.MapHash}"); Receive("WORLD_READY"); Receive("WELCOME 7 1 COMMANDER"); }
    private void Receive(string message) => InvokeMain("OnMessage", message);
    private void InvokeMain(string name, params object[] args) => typeof(Main).GetMethod(name, Private).Invoke(this, args);
    private async Task Flush()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
