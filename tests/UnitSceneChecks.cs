using Godot;
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using System.Text;

// Runs the existing message handler without connecting to a real game server.
// Godot --headless --path . res://tests/UnitSceneChecks.tscn
public partial class UnitSceneChecks : Main
{
    private uint SelectedUnitId => SelectedUnitIds.Count == 1 ? SelectedUnitIds.First() : 0;
    private static readonly MethodInfo MessageHandler = typeof(Main).GetMethod(
        "OnMessage", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly MethodInfo ClickHandler = typeof(Main).GetMethod(
        "OnUnitClicked", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly MethodInfo ClearHandler = typeof(Main).GetMethod(
        "OnSelectionCleared", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly MethodInfo BoxHandler = typeof(Main).GetMethod(
        "OnBoxSelectionRequested", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly MethodInfo MoveHandler = typeof(Main).GetMethod(
        "OnGroundRightClicked", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly MethodInfo AttackHandler = typeof(Main).GetMethod(
        "OnAttackTargetClicked", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly MethodInfo RightHandler = typeof(Main).GetMethod(
        "OnUnitRightClicked", BindingFlags.Instance | BindingFlags.NonPublic);

    public override async void _Ready()
    {
        try
        {
            KnightScene = GD.Load<PackedScene>("res://units/Knight.tscn");
            ArcherScene = GD.Load<PackedScene>("res://units/Archer.tscn");
            Check(GetChildCount() == 0, "No units before server messages");
            Receive("WELCOME 7");
            Receive("UNIT 0 101 7 -4 2");
            Receive("UNIT 1 202 8 5 -3");

            Unit knight = GetNode<Unit>("Unit_101");
            Unit archer = GetNode<Unit>("Unit_202");
            Check(knight.UnitType == 0 && archer.UnitType == 1, "Type-to-scene mapping");
            Check(knight.UnitId == 101 && knight.OwnerId == 7, "Server identity");
            Check(archer.OwnerId == 8 && SelectedUnitId == 0, "Spawn does not auto-select");
            Check(knight.GetNode<Node3D>("Visual").GetChildCount() > 0, "Knight model");
            Check(archer.GetNode<Node3D>("Visual").GetChildCount() > 0, "Archer model");
            Check(knight.GetNode<Area3D>("SelectionArea").CollisionLayer == 2, "Selection layer");
            Check(!knight.GetNode<MeshInstance3D>("SelectionRing").Visible, "No selection before click");
            Check(!archer.GetNode<MeshInstance3D>("SelectionRing").Visible, "Enemy unselected");

            Receive("UNIT 0 101 7 -6 4");
            Check(GetChildCount() == 2 && GetNode<Unit>("Unit_101") == knight, "Duplicate ID updates without respawn");
            Check(knight.GlobalPosition.IsEqualApprox(new Vector3(-6, 0, 4)), "Feet at Y=0");
            Receive("POS 202 10.25 -7.5");
            Check(archer.GlobalPosition.IsEqualApprox(new Vector3(10.25f, 0, -7.5f)), "POS routes by ID");
            Check(knight.GlobalPosition.IsEqualApprox(new Vector3(-6, 0, 4)), "Other unit unaffected");
            Receive("POS 202 NaN 3");
            Receive("UNIT 1 broken 7 0 0");
            Check(GetChildCount() == 2 && float.IsFinite(archer.GlobalPosition.X), "Malformed data ignored");

            await CheckInput(knight, archer);

            Receive("REMOVE 101");
            Receive("POS 101 99 99");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(!HasNode("Unit_101") && HasNode("Unit_202") && SelectedUnitId == 0, "REMOVE clears the correct unit and selection");
            GD.Print("PASS: unit lifecycle, click/drag selection, MOVE, A-click/right-click ATTACK, invalid targets, cancellation");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private void Receive(string message) => MessageHandler.Invoke(this, new object[] { message });

    private async Task CheckInput(Unit knight, Unit archer)
    {
        GetWindow().Size = new Vector2I(1000, 800);
        var camera = new Camera3D
        {
            Position = new Vector3(0, 18, 0),
            RotationDegrees = new Vector3(-90, 0, 0),
            Projection = Camera3D.ProjectionType.Orthogonal,
            Size = 32,
            Current = true
        };
        AddChild(camera);
        Camera = camera;
        var overlay = new CanvasLayer();
        AddChild(overlay);
        var box = new SelectionBox();
        overlay.AddChild(box);
        var input = new PlayerInput { Camera = camera, SelectionBox = box };
        AddChild(input);
        int picked = 0;
        var moves = new List<(uint Id, Vector3 Point)>();
        input.UnitClicked += unit =>
        {
            picked++;
            ClickHandler.Invoke(this, new object[] { unit });
        };
        input.SelectionCleared += () => ClearHandler.Invoke(this, null);
        input.BoxSelectionRequested += rect => BoxHandler.Invoke(this, new object[] { rect });
        input.GroundRightClicked += point => moves.Add((SelectedUnitId, point));

        await FlushPhysics();
        // Hit the capsule off-center; selecting does not require clicking the unit origin.
        Vector2 ownPoint = camera.UnprojectPosition(knight.GlobalPosition + new Vector3(0.3f, 1.2f, 0));
        Click(input, ownPoint, MouseButton.Left);
        await FlushPhysics();
        Check(SelectedUnitId == 101 && knight.GetNode<MeshInstance3D>("SelectionRing").Visible,
            "Ray selects own capsule and shows ring");

        Click(input, camera.UnprojectPosition(archer.GlobalPosition + Vector3.Up), MouseButton.Left);
        await FlushPhysics();
        Check(picked == 2 && SelectedUnitId == 0, "Enemy collider detected but ownership rejects selection");
        Check(!knight.GetNode<MeshInstance3D>("SelectionRing").Visible, "Old ring cleared");

        Click(input, ownPoint, MouseButton.Left);
        await FlushPhysics();
        Vector2 emptyPoint = camera.UnprojectPosition(new Vector3(0, 0, 10));
        Click(input, emptyPoint, MouseButton.Left);
        await FlushPhysics();
        Check(SelectedUnitId == 0, "Empty click clears selection");

        // Two events before the next physics frame must preserve selection -> move order.
        Click(input, ownPoint, MouseButton.Left);
        Vector3 destination = new(3, 0, 8);
        Click(input, camera.UnprojectPosition(destination), MouseButton.Right);
        camera.Position += new Vector3(2, 0, 0);
        await FlushPhysics();
        Check(moves.Count == 1 && moves[0].Id == 101, "Queued selection happens before movement event");
        Check(moves[0].Point.DistanceTo(destination) < 0.01f, "Ray uses click-time camera, not later camera pose");

        int previousPicks = picked;
        Click(input, emptyPoint, MouseButton.Left, false);
        Click(input, emptyPoint, MouseButton.WheelUp);
        input.Camera = null;
        Click(input, emptyPoint, MouseButton.Left);
        await FlushPhysics();
        Check(picked == previousPicks && moves.Count == 1 && SelectedUnitId == 101,
            "Release, wheel and missing camera do not generate commands");
        input.Camera = camera;

        Receive("UNIT 0 101 8 -6 4");
        Check(SelectedUnitId == 0, "Ownership update clears selection");
        Receive("UNIT 0 101 7 -6 4");
        await CheckDrag(input, box, camera, knight, archer);
        await CheckAttack(input, camera, knight, archer);
        ClickHandler.Invoke(this, new object[] { knight });
    }

    private async Task CheckAttack(PlayerInput input, Camera3D camera, Unit knight, Unit enemy)
    {
        using var sent = new MemoryStream();
        using var writer = new StreamWriter(sent, new UTF8Encoding(false), 1024, true) { AutoFlush = true };
        var net = new NetClient();
        typeof(NetClient).GetField("_writer", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(net, writer);
        typeof(Main).GetField("_net", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(this, net);
        Action<Unit> attack = target => AttackHandler.Invoke(this, new object[] { target });
        Action<Unit, Vector3> right = (target, point) => RightHandler.Invoke(this, new object[] { target, point });
        Action<Vector3> move = point => MoveHandler.Invoke(this, new object[] { point });
        input.AttackTargetClicked += attack;
        input.UnitRightClicked += right;
        input.GroundRightClicked += move;

        string[] Lines() => Encoding.UTF8.GetString(sent.ToArray()).Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim()).ToArray();
        Vector2 ownPoint = camera.UnprojectPosition(knight.GlobalPosition + Vector3.Up);
        Vector2 enemyPoint = camera.UnprojectPosition(enemy.GlobalPosition + Vector3.Up);
        Vector2 ground = camera.UnprojectPosition(new Vector3(0, 0, 10));

        // Selection and attack must retain their order even in the same physics frame.
        ClearHandler.Invoke(this, null);
        Click(input, ownPoint, MouseButton.Left);
        Click(input, enemyPoint, MouseButton.Right);
        await FlushPhysics();
        Check(Lines().SequenceEqual(new[] { "ATTACK 202 101" }) && SelectedUnitId == 101,
            "Enemy right click sends ATTACK, not MOVE, and preserves selection");

        KeyPress(input, Key.A);
        Check(input.IsAttackTargeting, "A arms attack targeting");
        Click(input, enemyPoint, MouseButton.Left);
        await FlushPhysics();
        Check(Lines().Length == 2 && Lines()[1] == "ATTACK 202 101" && SelectedUnitId == 101 && !input.IsAttackTargeting,
            "A-left click sends once and does not select the enemy");
        KeyPress(input, Key.A, true);
        Check(!input.IsAttackTargeting, "Key repeat does not re-arm targeting");

        foreach (Vector2 point in new[] { ground, ownPoint })
        {
            KeyPress(input, Key.A);
            Click(input, point, MouseButton.Left);
            await FlushPhysics();
            Check(Lines().Length == 2 && SelectedUnitId == 101 && !input.IsAttackTargeting,
                "Attack click on ground or own unit sends nothing and preserves selection");
        }

        KeyPress(input, Key.A);
        using (var escape = new InputEventKey { Keycode = Key.Escape, Pressed = true })
            input._Input(escape);
        Check(!input.IsAttackTargeting, "Escape cancels targeting");
        KeyPress(input, Key.A);
        GetWindow().EmitSignal(Window.SignalName.FocusExited);
        Check(!input.IsAttackTargeting, "Focus loss cancels targeting");

        KeyPress(input, Key.A);
        Click(input, ground, MouseButton.Right);
        await FlushPhysics();
        Check(!input.IsAttackTargeting && Lines().Length == 3 && Lines()[2].StartsWith("MOVE "),
            "Ground right click exits targeting and still moves");
        Click(input, ownPoint, MouseButton.Right);
        await FlushPhysics();
        Check(Lines().Length == 4 && Lines()[3].StartsWith("MOVE "), "Own unit right click retains movement");

        ClearHandler.Invoke(this, null);
        Click(input, enemyPoint, MouseButton.Right);
        KeyPress(input, Key.A);
        Click(input, enemyPoint, MouseButton.Left);
        await FlushPhysics();
        Check(Lines().Length == 4, "No selection means no attack command");

        Receive("UNIT 1 505 7 -2 4");
        await FlushPhysics();
        Drag(input, Vector2.One, GetViewport().GetVisibleRect().Size - Vector2.One);
        KeyPress(input, Key.A);
        Click(input, enemyPoint, MouseButton.Left);
        await FlushPhysics();
        string[] multi = Lines().Last().Split(' ');
        Check(Lines().Length == 5 && multi.Length == 4 && multi[0] == "ATTACK" && multi[1] == "202" &&
            multi.Skip(2).ToHashSet().SetEquals(new[] { "101", "505" }), "Box selection then attack sends all selected IDs");

        Receive("UNIT 0 606 8 0 0");
        Unit removed = GetNode<Unit>("Unit_606");
        Receive("REMOVE 606");
        AttackHandler.Invoke(this, new object[] { removed });
        Check(Lines().Length == 5, "Removed target is rejected");
        Receive("REMOVE 505");

        input.AttackTargetClicked -= attack;
        input.UnitRightClicked -= right;
        input.GroundRightClicked -= move;
        typeof(Main).GetField("_net", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(this, null);
        net.Free();
    }

    private static void KeyPress(PlayerInput input, Key key, bool echo = false)
    {
        using var press = new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true, Echo = echo };
        input._UnhandledInput(press);
    }

    private async Task CheckDrag(PlayerInput input, SelectionBox box, Camera3D camera, Unit knight, Unit enemy)
    {
        Receive("UNIT 1 303 7 -2 4");
        Unit friend = GetNode<Unit>("Unit_303");
        Receive("UNIT 0 404 7 0 0");
        Unit behind = GetNode<Unit>("Unit_404");
        behind.GlobalPosition = new Vector3(0, 30, 0);
        Vector2 a = camera.UnprojectPosition(knight.GlobalPosition + Vector3.Up);
        Vector2 b = camera.UnprojectPosition(friend.GlobalPosition + Vector3.Up);
        Vector2 start = a.Min(b) - new Vector2(15, 15);
        Vector2 end = a.Max(b) + new Vector2(15, 15);

        // Every drag direction must select the same two units.
        foreach (var (from, to) in new[] {
            (start, end), (end, start),
            (new Vector2(start.X, end.Y), new Vector2(end.X, start.Y)),
            (new Vector2(end.X, start.Y), new Vector2(start.X, end.Y)) })
        {
            Button(input, from, MouseButton.Left, true);
            Motion(input, to);
            Check(box.Visible && box.MouseFilter == Control.MouseFilterEnum.Ignore, "Drag box is visible without blocking input");
            Button(input, to, MouseButton.Left, false);
            Check(!box.Visible, "Release hides box");
            await FlushPhysics();
            Check(SelectedUnitIds.ToHashSet().SetEquals(new uint[] { 101, 303 }), "All drag directions select own units");
            Check(knight.GetNode<MeshInstance3D>("SelectionRing").Visible && friend.GetNode<MeshInstance3D>("SelectionRing").Visible,
                "All selected units show rings");
        }

        // Exercise Main's actual MOVE handler and writer, without a game server.
        using var sent = new MemoryStream();
        using var writer = new StreamWriter(sent, new UTF8Encoding(false), 1024, true) { AutoFlush = true };
        var net = new NetClient();
        typeof(NetClient).GetField("_writer", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(net, writer);
        typeof(Main).GetField("_net", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(this, net);
        Action<Vector3> sendMove = point => MoveHandler.Invoke(this, new object[] { point });
        input.GroundRightClicked += sendMove;
        ClearHandler.Invoke(this, null);
        Button(input, start, MouseButton.Left, true);
        Motion(input, end);
        Button(input, end, MouseButton.Left, false);
        Click(input, camera.UnprojectPosition(new Vector3(3, 0, 8)), MouseButton.Right);
        await FlushPhysics();
        string[] command = Encoding.UTF8.GetString(sent.ToArray()).Trim().Split(' ');
        Check(command.Length == 5 && command[0] == "MOVE" &&
            command.Skip(3).ToHashSet().SetEquals(new[] { "101", "303" }), "Drag then right click sends both IDs in one MOVE");
        input.GroundRightClicked -= sendMove;
        typeof(Main).GetField("_net", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(this, null);
        net.Free();

        // A full-screen drag must not select enemies or units behind the camera.
        Vector2 fullEnd = GetViewport().GetVisibleRect().Size - Vector2.One;
        Drag(input, Vector2.One, fullEnd);
        await FlushPhysics();
        Check(SelectedUnitIds.ToHashSet().SetEquals(new uint[] { 101, 303 }) &&
            !enemy.GetNode<MeshInstance3D>("SelectionRing").Visible, "Box filters ownership and camera frustum");

        Button(input, start, MouseButton.Left, true);
        Motion(input, end);
        using (var escape = new InputEventKey { Keycode = Key.Escape, Pressed = true })
            input._Input(escape);
        Button(input, end, MouseButton.Left, false);
        await FlushPhysics();
        Check(!box.Visible && SelectedUnitIds.Count == 2, "Escape cancels drag without changing selection");

        Button(input, start, MouseButton.Left, true);
        Motion(input, end);
        GetWindow().EmitSignal(Window.SignalName.FocusExited);
        Button(input, end, MouseButton.Left, false);
        await FlushPhysics();
        Check(!box.Visible && SelectedUnitIds.Count == 2, "Focus loss cancels drag");

        // Small hand movement remains a single click; selection waits for release.
        ClearHandler.Invoke(this, null);
        Button(input, a, MouseButton.Left, true);
        await FlushPhysics();
        Check(SelectedUnitIds.Count == 0, "Press alone does not select");
        Motion(input, a + Vector2.One);
        Check(!box.Visible, "Small movement does not show drag box");
        Button(input, a + Vector2.One, MouseButton.Left, false);
        await FlushPhysics();
        Check(SelectedUnitId == 101, "Small movement remains a single click");

        Drag(input, start, end);
        await FlushPhysics();
        Receive("UNIT 1 303 8 -2 4");
        Check(SelectedUnitId == 101 && !friend.GetNode<MeshInstance3D>("SelectionRing").Visible, "Ownership change removes only that selection");
        Receive("UNIT 1 303 7 -2 4");
        Drag(input, start, end);
        await FlushPhysics();
        Receive("REMOVE 303");
        Check(SelectedUnitId == 101, "REMOVE preserves other selected units");

        Drag(input, new Vector2(10, 10), new Vector2(30, 30));
        await FlushPhysics();
        Check(SelectedUnitIds.Count == 0 && !knight.GetNode<MeshInstance3D>("SelectionRing").Visible, "Empty box clears selection");

        for (uint id = 1000; id < 1070; id++)
            Receive($"UNIT 0 {id} 7 -2 4");
        Drag(input, Vector2.One, fullEnd);
        await FlushPhysics();
        Check(SelectedUnitIds.Count == 64, "Selection obeys server MOVE limit");
        for (uint id = 1000; id < 1070; id++)
            Receive($"REMOVE {id}");
        Receive("REMOVE 404");
        Click(input, a, MouseButton.Left);
        await FlushPhysics();
        Check(SelectedUnitId == 101, "Single click replaces multiple selection");
    }

    private async Task FlushPhysics()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }

    private static void Click(PlayerInput input, Vector2 position, MouseButton button, bool pressed = true)
    {
        Button(input, position, button, pressed);
        if (pressed && button == MouseButton.Left)
            Button(input, position, button, false);
    }

    private static void Button(PlayerInput input, Vector2 position, MouseButton button, bool pressed)
    {
        using var mouse = new InputEventMouseButton { Position = position, ButtonIndex = button, Pressed = pressed };
        input._UnhandledInput(mouse);
    }

    private static void Motion(PlayerInput input, Vector2 position)
    {
        using var motion = new InputEventMouseMotion { Position = position, ButtonMask = MouseButtonMask.Left };
        input._Input(motion);
    }

    private static void Drag(PlayerInput input, Vector2 from, Vector2 to)
    {
        Button(input, from, MouseButton.Left, true);
        Motion(input, to);
        using var release = new InputEventMouseButton { Position = to, ButtonIndex = MouseButton.Left, Pressed = false };
        input._Input(release);
    }

    private static void Check(bool condition, string description)
    {
        if (!condition)
            throw new InvalidOperationException(description);
    }
}
