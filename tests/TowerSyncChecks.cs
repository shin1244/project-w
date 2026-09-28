using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

// 실제 STATE 분기, 건물 공격 입력, 조준 및 파괴 처리를 서버 없이 검증합니다.
public partial class TowerSyncChecks : Main
{
    private static readonly MethodInfo Handler = typeof(Main).GetMethod("OnMessage", BindingFlags.Instance | BindingFlags.NonPublic);
    private void Receive(string message) => Handler.Invoke(this, new object[] { message });

    public override async void _Ready()
    {
        try
        {
            var game = GD.Load<PackedScene>("res://game/Main.tscn").Instantiate<Main>();
            Check(game.Units.Buildings == game.Buildings && game.Buildings.Units == game.Units, "Main links target lookups in both directions");
            game.Free();
            Map = new MapWorld();
            typeof(MapWorld).GetField("<IsSynchronized>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(Map, true);
            Units = new UnitManager { WorkerScene = GD.Load<PackedScene>("res://units/Worker.tscn") };
            Buildings = new BuildingManager
            {
                TownHallScene = GD.Load<PackedScene>("res://buildings/TownHall.tscn"),
                TowerScene = GD.Load<PackedScene>("res://buildings/Tower.tscn"), Units = Units
            };
            Units.Buildings = Buildings;
            AddChild(Units);
            AddChild(Buildings);
            var camera = new Camera3D
            {
                Position = new Vector3(0, 25, 0), RotationDegrees = new Vector3(-90, 0, 0),
                Projection = Camera3D.ProjectionType.Orthogonal, Size = 28, Current = true
            };
            AddChild(camera);
            Units.Camera = camera;
            var input = new PlayerInput { Camera = camera };
            AddChild(input);
            input.UnitClicked += Bind<Action<Unit>>("SelectUnit");
            input.BuildingClicked += Bind<Action<Building>>("SelectBuilding");
            input.SelectionCleared += Bind<Action>("ClearSelection");
            input.BoxSelectionRequested += Bind<Action<Rect2>>("SelectBox");
            input.ContextClicked += Units.RequestContextOrder;
            input.AttackTargetClicked += Units.RequestAttack;
            var commands = new List<string>();
            Units.CommandRequested += commands.Add;

            Receive("WELCOME 7 2");
            Receive("BUILDING 1 501 1 5 0 1.5707963");
            Receive("BUILDING 0 502 2 -6 -5 0");
            Building tower = Buildings.GetNode<Building>("Building_501");
            Building hall = Buildings.GetNode<Building>("Building_502");
            int shots = 0;
            tower.StateChanged += (_, fired) => { if (fired) shots++; };
            Receive("STATE 501 ATTACK 0 101 8"); // 초기 스냅샷은 UNIT보다 먼저 옵니다.
            Check(tower.HasServerState && shots == 0 && !tower.HasNode("ShotTrace"), "Reconnect snapshot does not replay historical shots");
            Receive("UNIT 0 101 7 -5 2 2");
            Unit own = Units.GetNode<Unit>("Unit_101");
            await Flush();
            Node3D turret = tower.GetNode<Node3D>("Turret");
            CheckFacing(turret, own, "Late UNIT snapshot becomes the tower's aim target");
            Check(Mathf.IsEqualApprox(tower.GlobalRotation.Y, Mathf.Pi / 2), "Masonry keeps server yaw while weapon rotates");
            Receive("POS 101 -5 -2");
            await Flush();
            CheckFacing(turret, own, "Aim follows POS even without another STATE");
            Receive("STATE 501 ATTACK 0 101 9");
            Check(shots == 1 && tower.HasNode("ShotTrace") && tower.GetNode<Node3D>("Turret/Ballista").Position.Z > .1f,
                "One new shot produces recoil and trace");
            Receive("STATE 501 ATTACK 0 101 9");
            Check(shots == 1, "Duplicate STATE does not fire again");
            Receive("HP 501 1375 1500");
            Receive("HP 502 1000 1000");
            Check(tower.HealthBar.CurrentHP == 1375 && tower.HealthBar.MaxHP == 1500 && !tower.HealthBar.Visible,
                "HP remains server authoritative and hidden until selection");
            Receive("STATE 101 ATTACK 0 501 1");
            CheckFacing(own.GetNode<Node3D>("Visual"), tower, "Unit attack resolves building focus");

            foreach (string bad in new[] { "STATE 501 GATHER 0 101 10", "STATE 501 ATTACK 3 101 10",
                "STATE 501 OTHER 0 101 10", "STATE 501 ATTACK -1 101 10", "STATE 501 ATTACK 0 nope 10",
                "STATE 501 ATTACK 0 101", "STATE 501 ATTACK 0 101 10 extra", "STATE 0 IDLE 0 0 0" }) Receive(bad);
            Receive("STATE 502 ATTACK 0 101 1");
            Check(tower.State.SwingSequence == 9 && shots == 1 && !hall.HasServerState, "Invalid tower states and non-attacking hall state ignored");
            await ToSignal(GetTree().CreateTimer(.3), SceneTreeTimer.SignalName.Timeout);
            Check(!tower.HasNode("ShotTrace") && tower.GetNode<Node3D>("Turret/Ballista").Position.IsZeroApprox(), "Shot effects complete without client damage simulation");

            Vector2 ownPoint = camera.UnprojectPosition(own.GlobalPosition + Vector3.Up);
            Vector2 towerPoint = camera.UnprojectPosition(tower.GlobalPosition + Vector3.Up * 2);
            Click(input, ownPoint, MouseButton.Left);
            Click(input, towerPoint, MouseButton.Right);
            await Flush();
            Check(commands.SequenceEqual(new[] { "ATTACK 501 101" }) && Units.SelectedUnitIds.Contains(101u),
                "Physics ray right-click attacks enemy building with selected own units");
            Check(tower.HasNode("CommandTargetRing"), "Enemy building attack target is indicated");
            input._UnhandledInput(new InputEventKey { Keycode = Key.A, PhysicalKeycode = Key.A, Pressed = true });
            Click(input, towerPoint, MouseButton.Left);
            await Flush();
            Check(commands.Count == 2 && commands.Last() == "ATTACK 501 101" && !input.IsAttackTargeting,
                "A-click attacks the building once and preserves selection");
            int count = commands.Count;
            Units.RequestAttack(hall);
            Check(commands.Count == count, "Own team building cannot be attacked even when player ID differs");
            Units.RequestContextOrder(hall, hall.GlobalPosition);
            Check(commands.Last().StartsWith("MOVE "), "Friendly building right-click is movement");

            Click(input, towerPoint, MouseButton.Left);
            await Flush();
            Check(Units.SelectedUnitIds.Count == 0 && tower.HealthBar.Visible && tower.GetNode<MeshInstance3D>("SelectionRing").Visible,
                "Building left-click inspects HP and clears unit selection");
            Click(input, ownPoint, MouseButton.Left);
            await Flush();
            Check(Units.SelectedUnitIds.Contains(101u) && !tower.HealthBar.Visible && !tower.GetNode<MeshInstance3D>("SelectionRing").Visible,
                "Unit selection clears building selection");
            Bind<Action<Building>>("SelectBuilding")(tower);
            Bind<Action<Rect2>>("SelectBox")(GetViewport().GetVisibleRect());
            Check(Units.SelectedUnitIds.SequenceEqual(new uint[] { 101 }) && !tower.HealthBar.Visible,
                "Box selection includes only own units and clears building selection");

            Units.RequestAttack(tower);
            Receive("STATE 501 ATTACK 0 101 10");
            Receive("REMOVE 101");
            tower._Process(0);
            Receive("REMOVE 501");
            Receive("REMOVE 501");
            Check(!Buildings.TryGetBuilding(501, out _) && tower.IsQueuedForDeletion(), "REMOVE immediately unregisters a firing tower");
            count = commands.Count;
            Units.RequestAttack(tower);
            Check(commands.Count == count, "Removed targets cannot receive stale commands");
            await Flush();
            Receive("BUILDING 1 501 1 5 0 0");
            Receive("STATE 501 ATTACK 0 999 100");
            Building reconnected = Buildings.GetNode<Building>("Building_501");
            Check(!reconnected.HasNode("ShotTrace"), "Recreated tower starts with a fresh snapshot baseline");
            Receive("STATE 501 IDLE 0 0 100");
            Check(!reconnected.IsProcessing(), "Idle stops aim updates");
            Buildings.Clear();
            Check(Buildings.GetChildCount() == 0, "Map reset clears building visuals and state");
            GD.Print("PASS: tower STATE, late targets, aim, shot deduplication, HP, building attack rays, selection, removal and reconnect");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
        finally { Map?.Free(); }
    }

    private T Bind<T>(string name) where T : Delegate => typeof(Main).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).CreateDelegate<T>(this);
    private async Task Flush()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
    private static void Click(PlayerInput input, Vector2 position, MouseButton button)
    {
        input._UnhandledInput(new InputEventMouseButton { Position = position, ButtonIndex = button, Pressed = true });
        if (button == MouseButton.Left)
            input._UnhandledInput(new InputEventMouseButton { Position = position, ButtonIndex = button, Pressed = false });
    }
    private static void CheckFacing(Node3D facing, Node3D target, string message)
    {
        Vector3 direction = target.GlobalPosition - facing.GlobalPosition;
        direction.Y = 0;
        Check((-facing.GlobalBasis.Z).Dot(direction.Normalized()) > .999f, $"{message}: facing={-facing.GlobalBasis.Z}, target={direction.Normalized()}");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
