using Godot;
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;

// Runs the existing message handler without connecting to a real game server.
// Godot --headless --path . res://tests/UnitSceneChecks.tscn
public partial class UnitSceneChecks : Main
{
    private static readonly MethodInfo MessageHandler = typeof(Main).GetMethod(
        "OnMessage", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly MethodInfo ClickHandler = typeof(Main).GetMethod(
        "OnUnitClicked", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly MethodInfo ClearHandler = typeof(Main).GetMethod(
        "OnSelectionCleared", BindingFlags.Instance | BindingFlags.NonPublic);

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
            GD.Print("PASS: unit lifecycle, ray selection, ownership, empty click, ordered move request, ignored inputs");
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
        var input = new PlayerInput { Camera = camera };
        AddChild(input);
        int picked = 0;
        var moves = new List<(uint Id, Vector3 Point)>();
        input.UnitClicked += unit =>
        {
            picked++;
            ClickHandler.Invoke(this, new object[] { unit });
        };
        input.SelectionCleared += () => ClearHandler.Invoke(this, null);
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
        ClickHandler.Invoke(this, new object[] { knight });
    }

    private async Task FlushPhysics()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }

    private static void Click(PlayerInput input, Vector2 position, MouseButton button, bool pressed = true)
    {
        using var mouse = new InputEventMouseButton { Position = position, ButtonIndex = button, Pressed = pressed };
        input._UnhandledInput(mouse);
    }

    private static void Check(bool condition, string description)
    {
        if (!condition)
            throw new InvalidOperationException(description);
    }
}
