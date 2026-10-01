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
            Receive("WELCOME 7 2");
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
            Receive("WELCOME 7 1");
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

            GD.Print("PASS: real Shift click/drag/double-click; group save/recall/double-tap/repeat/64-limit/cleanup; minimap pan/drag/footprint/zoom/padding/focus/cancel; ordered MOVE wire format and session gates");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
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
    private void BeginSession() { Receive($"MAP 2 {Map.MapHash}"); Receive("WORLD_READY"); Receive("WELCOME 7 1"); }
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
