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
    private IReadOnlyCollection<uint> SelectedUnitIds => Units.SelectedUnitIds;
    private uint SelectedUnitId => SelectedUnitIds.Count == 1 ? SelectedUnitIds.First() : 0;
    private static readonly MethodInfo MessageHandler = typeof(Main).GetMethod(
        "OnMessage", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly MethodInfo CommandHandler = typeof(Main).GetMethod(
        "SendCommand", BindingFlags.Instance | BindingFlags.NonPublic);

    public override async void _Ready()
    {
        try
        {
            Map = new MapWorld();
            typeof(MapWorld).GetField("<IsSynchronized>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(Map, true);
            Units = new UnitManager { Name = "Units" };
            AddChild(Units);
            Units.CommandRequested += command => CommandHandler.Invoke(this, new object[] { command });
            Units.WorkerScene = GD.Load<PackedScene>("res://units/Worker.tscn");
            Units.KnightScene = GD.Load<PackedScene>("res://units/Knight.tscn");
            Units.ArcherScene = GD.Load<PackedScene>("res://units/Archer.tscn");
            Check(Units.GetChildCount() == 0, "No units before server messages");
            Receive("WELCOME 7");
            Receive("UNIT 1 101 7 -4 2");
            Receive("UNIT 2 202 8 5 -3");

            Unit knight = Units.GetNode<Unit>("Unit_101");
            Unit archer = Units.GetNode<Unit>("Unit_202");
            Check(knight.UnitType == 1 && archer.UnitType == 2, "Type-to-scene mapping");
            Check(knight.UnitId == 101 && knight.OwnerId == 7, "Server identity");
            Check(archer.OwnerId == 8 && SelectedUnitId == 0, "Spawn does not auto-select");
            Check(knight.GetNode<Node3D>("Visual").GetChildCount() > 0, "Knight model");
            Check(archer.GetNode<Node3D>("Visual").GetChildCount() > 0, "Archer model");
            Check(knight.GetNode<Area3D>("SelectionArea").CollisionLayer == 2, "Selection layer");
            Check(!knight.GetNode<MeshInstance3D>("SelectionRing").Visible, "No selection before click");
            Check(!archer.GetNode<MeshInstance3D>("SelectionRing").Visible, "Enemy unselected");

            Receive("UNIT 1 101 7 -6 4");
            Check(Units.GetChildCount() == 2 && Units.GetNode<Unit>("Unit_101") == knight, "Duplicate ID updates without respawn");
            Check(knight.GlobalPosition.IsEqualApprox(new Vector3(-6, 0, 4)), "Feet at Y=0");
            Receive("POS 202 10.25 -7.5");
            Check(archer.GlobalPosition.IsEqualApprox(new Vector3(10.25f, 0, -7.5f)), "POS routes by ID");
            Check(knight.GlobalPosition.IsEqualApprox(new Vector3(-6, 0, 4)), "Other unit unaffected");
            Receive("POS 202 NaN 3");
            Receive("UNIT 2 broken 7 0 0");
            Check(Units.GetChildCount() == 2 && float.IsFinite(archer.GlobalPosition.X), "Malformed data ignored");

            await CheckInput(knight, archer);
            await CheckResources();

            Receive("UNIT 0 707 7 1 2");
            Unit worker = Units.GetNode<Unit>("Unit_707");
            Check(worker.UnitType == 0 && worker.HasNode("Visual/Torso/RightArm/Axe"), "Worker type spawns worker model");
            Receive("UNIT 0 707 7 2 3");
            Check(Units.GetNode<Unit>("Unit_707") == worker, "Worker duplicate updates without respawn");
            Receive("POS 707 3 4");
            Check(worker.GlobalPosition.IsEqualApprox(new Vector3(3, 0, 4)), "Worker uses common movement");
            Units.SelectSingle(worker);
            Check(SelectedUnitId == 707 && worker.GetNode<MeshInstance3D>("SelectionRing").Visible,
                "Worker uses common selection");
            Receive("REMOVE 707");
            Check(SelectedUnitIds.Count == 0, "Worker removal clears selection");
            Units.SelectSingle(knight);

            Receive("REMOVE 101");
            Receive("POS 101 99 99");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(!Units.HasNode("Unit_101") && Units.HasNode("Unit_202") && SelectedUnitId == 0, "REMOVE clears the correct unit and selection");
            GD.Print("PASS: unit lifecycle, click/drag selection, unified MOVE/ATTACK/GATHER context orders, nearest target, invalid targets, cancellation, resource lifecycle, manager scene wiring");
            Map.Free();
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }


    private async Task CheckResources()
    {
        Resources = new ResourceManager
        {
            Name = "Resources",
            OakScene = GD.Load<PackedScene>("res://resources/trees/Oak.tscn"),
            PineScene = GD.Load<PackedScene>("res://resources/trees/Pine.tscn"),
            BirchScene = GD.Load<PackedScene>("res://resources/trees/Birch.tscn")
        };
        AddChild(Resources);
        // Scene-placed trees are kept as visuals until the server assigns their identity.
        ResourceNode placed = Resources.OakScene.Instantiate<ResourceNode>();
        placed.Position = new Vector3(8, 3.75f, 18);
        Resources.AddChild(placed);

        for (uint variant = 0; variant < 3; variant++)
        {
            uint id = 900 + variant;
            Vector3 point = new(variant * 3, 2, 6);
            ResourceNode tree = Resources.SpawnOrUpdate(id, variant, point);
            Check(tree.ResourceId == id && tree.VisualVariant == variant && tree.GlobalPosition.IsEqualApprox(point),
                "Resource identity, variant and height");
            Check(Resources.TryGetResource(id, out ResourceNode found) && found == tree, "Resource lookup by ID");
            Check(Resources.SpawnOrUpdate(id, variant, point + Vector3.Right) == tree, "Resource update does not duplicate");
            tree.SetSelected(true);
            Resources.Remove(id);
            Check(!Resources.TryGetResource(id, out _) && tree.IsQueuedForDeletion()
                && !tree.GetNode<MeshInstance3D>("SelectionRing").Visible, "Resource removal clears lookup and selection");
            Resources.Remove(id);
        }
        Check(Resources.SpawnOrUpdate(999, 0, new Vector3(float.NaN, 0, 0)) == null,
            "Invalid resource position is ignored");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Check(Resources.GetChildCount() == 1 && placed.Position.IsEqualApprox(new Vector3(8, 3.75f, 18)),
            "Server resource lifecycle preserves scene-placed trees");
        Check(Units.HasNode("Unit_101") && Units.HasNode("Unit_202"), "Resource removal does not affect units");

        // Check serialized manager assignments and manually placed tree transforms without connecting.
        Main gameScene = GD.Load<PackedScene>("res://game/Main.tscn").Instantiate<Main>();
        Check(gameScene.Units == gameScene.GetNode<UnitManager>("Units") &&
            gameScene.Resources == gameScene.GetNode<ResourceManager>("Resources") &&
            gameScene.Units.Resources == gameScene.Resources,
            "Main scene references both managers");
        Check(gameScene.Units.WorkerScene != null && gameScene.Units.Camera != null &&
            gameScene.Resources.OakScene != null, "Manager scenes and camera assigned");
        Check(gameScene.Resources.GetChildCount() == 0 && gameScene.Map.Resources == gameScene.Resources,
            "Trees are generated from the map instead of placed in Main.tscn");
        gameScene.Free();
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
        Units.Camera = camera;
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
            Units.SelectSingle(unit);
        };
        input.SelectionCleared += () => Units.ClearSelection();
        input.BoxSelectionRequested += rect => Units.SelectBox(rect);
        input.ContextClicked += (target, point) => { if (target == null) moves.Add((SelectedUnitId, point)); };

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

        Receive("UNIT 1 101 8 -6 4");
        Check(SelectedUnitId == 0, "Ownership update clears selection");
        Receive("UNIT 1 101 7 -6 4");
        await CheckDrag(input, box, camera, knight, archer);
        await CheckAttack(input, camera, knight, archer);
        await CheckGather(input, camera);
        Units.SelectSingle(knight);
    }

    private async Task CheckAttack(PlayerInput input, Camera3D camera, Unit knight, Unit enemy)
    {
        using var sent = new MemoryStream();
        using var writer = new StreamWriter(sent, new UTF8Encoding(false), 1024, true) { AutoFlush = true };
        var net = new NetClient();
        typeof(NetClient).GetField("_writer", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(net, writer);
        typeof(Main).GetField("_net", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(this, net);
        Action<Unit> attack = target => Units.RequestAttack(target);
        Action<Node3D, Vector3> right = Units.RequestContextOrder;

        input.AttackTargetClicked += attack;
        input.ContextClicked += right;


        string[] Lines() => Encoding.UTF8.GetString(sent.ToArray()).Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim()).ToArray();
        Vector2 ownPoint = camera.UnprojectPosition(knight.GlobalPosition + Vector3.Up);
        Vector2 enemyPoint = camera.UnprojectPosition(enemy.GlobalPosition + Vector3.Up);
        Vector2 ground = camera.UnprojectPosition(new Vector3(0, 0, 10));

        // Selection and attack must retain their order even in the same physics frame.
        Units.ClearSelection();
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

        Units.ClearSelection();
        Click(input, enemyPoint, MouseButton.Right);
        KeyPress(input, Key.A);
        Click(input, enemyPoint, MouseButton.Left);
        await FlushPhysics();
        Check(Lines().Length == 4, "No selection means no attack command");

        Receive("UNIT 2 505 7 -2 4");
        await FlushPhysics();
        Drag(input, Vector2.One, GetViewport().GetVisibleRect().Size - Vector2.One);
        KeyPress(input, Key.A);
        Click(input, enemyPoint, MouseButton.Left);
        await FlushPhysics();
        string[] multi = Lines().Last().Split(' ');
        Check(Lines().Length == 5 && multi.Length == 4 && multi[0] == "ATTACK" && multi[1] == "202" &&
            multi.Skip(2).ToHashSet().SetEquals(new[] { "101", "505" }), "Box selection then attack sends all selected IDs");

        Receive("UNIT 1 606 8 0 0");
        Unit removed = Units.GetNode<Unit>("Unit_606");
        Receive("REMOVE 606");
        Units.RequestAttack(removed);
        Units.RequestContextOrder(removed, Vector3.Zero);
        Check(Lines().Length == 5, "Removed target is rejected");
        Receive("REMOVE 505");

        input.AttackTargetClicked -= attack;
        input.ContextClicked -= right;

        typeof(Main).GetField("_net", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(this, null);
        net.Free();
    }

    private async Task CheckGather(PlayerInput input, Camera3D camera)
    {
        using var sent = new MemoryStream();
        using var writer = new StreamWriter(sent, new UTF8Encoding(false), 1024, true) { AutoFlush = true };
        var net = new NetClient();
        typeof(NetClient).GetField("_writer", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(net, writer);
        typeof(Main).GetField("_net", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(this, net);
        input.ContextClicked += Units.RequestContextOrder;
        input.AttackTargetClicked += Units.RequestAttack;
        string[] Lines() => Encoding.UTF8.GetString(sent.ToArray()).Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim()).ToArray();

        var resources = new ResourceManager { OakScene = GD.Load<PackedScene>("res://resources/trees/Oak.tscn") };
        AddChild(resources);
        var tree = resources.SpawnOrUpdate(9900, 0, new Vector3(4, 0, 6));
        tree.SetAmount(400);
        Receive("UNIT 0 808 7 -10 -4");
        Receive("UNIT 0 809 7 -9 -4");
        Receive("UNIT 0 810 8 -8 -4");
        Unit worker = Units.GetNode<Unit>("Unit_808");
        await FlushPhysics();
        Vector2 treePoint = camera.UnprojectPosition(tree.GlobalPosition + Vector3.Up * 1.9f);
        Units.SelectSingle(worker);
        Click(input, treePoint, MouseButton.Right);
        await FlushPhysics();
        Check(Lines().SequenceEqual(new[] { "GATHER 9900 808" }) && SelectedUnitId == 808,
            "Tree right click sends one GATHER and preserves worker selection");
        Check(tree.GetNodeOrNull<MeshInstance3D>("CommandTargetRing")?.Visible == true,
            "Tree right click also shows target ring");

        KeyPress(input, Key.A);
        Click(input, treePoint, MouseButton.Left);
        await FlushPhysics();
        Check(Lines().Length == 1 && !input.IsAttackTargeting, "A-left click on tree does not issue gather or attack");
        KeyPress(input, Key.A);
        Click(input, treePoint, MouseButton.Right);
        await FlushPhysics();
        Check(Lines().Length == 2 && Lines().Last() == "GATHER 9900 808" && !input.IsAttackTargeting,
            "Right click cancels A-mode and uses normal tree context");

        Units.ClearSelection();
        Click(input, treePoint, MouseButton.Right);
        await FlushPhysics();
        Check(Lines().Length == 2, "No selected units means no gather");
        Drag(input, Vector2.One, GetViewport().GetVisibleRect().Size - Vector2.One);
        await FlushPhysics();
        string[] expectedIds = SelectedUnitIds.Select(id => id.ToString()).ToArray();
        Click(input, treePoint, MouseButton.Right);
        await FlushPhysics();
        string[] command = Lines().Last().Split(' ');
        Check(Lines().Length == 3 && command[0] == "GATHER" && command[1] == "9900" &&
            command.Skip(2).ToHashSet().SetEquals(expectedIds) && !command.Skip(2).Contains("810"),
            "Mixed selection sends own IDs only; server decides gather capability");

        Units.SelectSingle(worker);
        Receive("UNIT 1 811 8 4 6");
        Unit behindTree = Units.GetNode<Unit>("Unit_811");
        await FlushPhysics();
        Click(input, treePoint, MouseButton.Right);
        await FlushPhysics();
        Check(Lines().Length == 4 && Lines().Last() == "GATHER 9900 808", "Tree closer to ray than unit wins");
        behindTree.GlobalPosition += Vector3.Up * 5;
        await FlushPhysics();
        Click(input, treePoint, MouseButton.Right);
        await FlushPhysics();
        Check(Lines().Length == 5 && Lines().Last() == "ATTACK 811 808", "Unit closer to ray than tree wins");
        Check(behindTree.GetNodeOrNull<MeshInstance3D>("CommandTargetRing")?.Visible == true &&
            tree.GetNodeOrNull<MeshInstance3D>("CommandTargetRing") == null, "Enemy right click transfers target ring");
        Receive("REMOVE 811");
        await FlushPhysics();

        tree.SetAmount(0);
        Click(input, treePoint, MouseButton.Right);
        await FlushPhysics();
        Check(Lines().Length == 5, "Depleted tree sends no command");
        tree.SetAmount(400);
        resources.Remove(9900);
        Units.RequestContextOrder(tree, Vector3.Zero);
        Check(Lines().Length == 5, "Removed tree cannot issue a context order");

        input.ContextClicked -= Units.RequestContextOrder;
        input.AttackTargetClicked -= Units.RequestAttack;
        typeof(Main).GetField("_net", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(this, null);
        net.Free();
        resources.QueueFree();
        foreach (uint id in new uint[] { 808, 809, 810 }) Receive($"REMOVE {id}");
        await FlushPhysics();
    }

    private static void KeyPress(PlayerInput input, Key key, bool echo = false)
    {
        using var press = new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true, Echo = echo };
        input._UnhandledInput(press);
    }

    private async Task CheckDrag(PlayerInput input, SelectionBox box, Camera3D camera, Unit knight, Unit enemy)
    {
        Receive("UNIT 2 303 7 -2 4");
        Unit friend = Units.GetNode<Unit>("Unit_303");
        Receive("UNIT 1 404 7 0 0");
        Unit behind = Units.GetNode<Unit>("Unit_404");
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
        Action<Node3D, Vector3> sendMove = Units.RequestContextOrder;
        input.ContextClicked += sendMove;
        Units.ClearSelection();
        Button(input, start, MouseButton.Left, true);
        Motion(input, end);
        Button(input, end, MouseButton.Left, false);
        Click(input, camera.UnprojectPosition(new Vector3(3, 0, 8)), MouseButton.Right);
        await FlushPhysics();
        string[] command = Encoding.UTF8.GetString(sent.ToArray()).Trim().Split(' ');
        Check(command.Length == 5 && command[0] == "MOVE" &&
            command.Skip(3).ToHashSet().SetEquals(new[] { "101", "303" }), "Drag then right click sends both IDs in one MOVE");
        input.ContextClicked -= sendMove;
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
        Units.ClearSelection();
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
        Receive("UNIT 2 303 8 -2 4");
        Check(SelectedUnitId == 101 && !friend.GetNode<MeshInstance3D>("SelectionRing").Visible, "Ownership change removes only that selection");
        Receive("UNIT 2 303 7 -2 4");
        Drag(input, start, end);
        await FlushPhysics();
        Receive("REMOVE 303");
        Check(SelectedUnitId == 101, "REMOVE preserves other selected units");

        Drag(input, new Vector2(10, 10), new Vector2(30, 30));
        await FlushPhysics();
        Check(SelectedUnitIds.Count == 0 && !knight.GetNode<MeshInstance3D>("SelectionRing").Visible, "Empty box clears selection");

        for (uint id = 1000; id < 1070; id++)
            Receive($"UNIT 1 {id} 7 -2 4");
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
