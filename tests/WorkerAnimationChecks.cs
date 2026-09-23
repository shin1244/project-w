using Godot;
using System;
using System.Reflection;

// 실제 Main 수신 분기 → UnitManager → Unit → 일꾼 외형까지 검사합니다.
public partial class WorkerAnimationChecks : Main
{
    private static readonly MethodInfo Handler = typeof(Main).GetMethod("OnMessage", BindingFlags.Instance | BindingFlags.NonPublic);
    private void Receive(string message) => Handler.Invoke(this, new object[] { message.Split('\n')[0] });

    public override void _Ready()
    {
        try
        {
            Map = new MapWorld();
            typeof(MapWorld).GetField("<IsSynchronized>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(Map, true);
            Resources = new ResourceManager { OakScene = GD.Load<PackedScene>("res://resources/trees/Oak.tscn") };
            AddChild(Resources);
            Resources.SpawnOrUpdate(700, 0, new Vector3(4, 0, 0));
            Units = new UnitManager { Resources = Resources, WorkerScene = GD.Load<PackedScene>("res://units/Worker.tscn") };
            AddChild(Units);
            Receive("UNIT 0 77 1 0 0");
            Unit worker = Units.GetNode<Unit>("Unit_77");
            AnimationPlayer player = worker.GetNode<AnimationPlayer>("AnimationPlayer");
            player.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual;
            Node3D wood = worker.GetNode<Node3D>("Visual/Torso/CarriedWood");
            Node3D axe = worker.GetNode<Node3D>("Visual/Torso/RightArm/Axe");

            Check(player.CurrentAnimation == "Idle" && axe.Visible && !wood.Visible, "Default pose");
            Receive("STATE 77 IDLE 0 0 0");
            Receive("STATE 77 GATHER 0 700 0");
            player.Advance(.3);
            player.Advance(0);
            Check(worker.GetNode<Node3D>("Visual/Torso/RightArm").Rotation.X > 2, "Swing actually raises the axe arm");
            Check(player.CurrentAnimation == "Swing" && axe.Visible && !wood.Visible, "Gather swings");
            Check((-worker.GetNode<Node3D>("Visual").GlobalBasis.Z).Dot(Vector3.Right) > .99f, "Faces resource");
            Receive("POS 77 0 0.1");
            Check((-worker.GetNode<Node3D>("Visual").GlobalBasis.Z).Dot(Vector3.Right) > .99f, "Separation does not turn away from work");
            double phase = player.CurrentAnimationPosition;
            Receive("STATE 77 GATHER 0 700 0");
            Check(Math.Abs(player.CurrentAnimationPosition - phase) < .001, "Duplicate state does not restart swing");
            player.Advance(1);
            Check(player.CurrentAnimation == "Swing" && player.IsPlaying(), "Gather repeats");

            Basis beforeTreeRemoval = worker.GetNode<Node3D>("Visual").Basis;
            Resources.Remove(700);
            Receive("POS 77 0.1 0.1");
            worker._Process(0);
            Check(worker.GetNode<Node3D>("Visual").Basis.IsEqualApprox(beforeTreeRemoval), "Removed tree keeps last facing even if worker is pushed");

            Receive("STATE 77 IDLE 5 0 0");
            player.Advance(.15);
            player.Advance(0);
            Check(worker.GetNode<Node3D>("Visual/Torso/LeftArm").Rotation.X > 1, "Carry actually raises both arms");
            Check(player.CurrentAnimation == "Carry" && wood.Visible && !axe.Visible, "Harvest shows wood");
            Receive("POS 77 2 3");
            Check(player.CurrentAnimation == "Carry", "Moving with wood keeps carry pose");
            Receive("STATE 77 IDLE 0 0 0");
            Check(player.CurrentAnimation == "Idle" && !wood.Visible && axe.Visible, "Deposit clears wood");

            Receive("UNIT 0 99 2 4 3");
            Receive("STATE 77 ATTACK 0 99 1");
            player.Advance(.2);
            Check(player.CurrentAnimation == "Swing", "Server hit triggers one swing");
            phase = player.CurrentAnimationPosition;
            Receive("STATE 77 ATTACK 0 99 1");
            Check(Math.Abs(player.CurrentAnimationPosition - phase) < .001, "Same hit is not replayed");

            Receive("POS 99 2 8");
            worker._Process(0); // 실제 실행에서는 Godot이 매 렌더 프레임에 호출합니다.
            Check(worker.GlobalPosition.IsEqualApprox(new Vector3(2, 0, 3)) &&
                (-worker.GetNode<Node3D>("Visual").GlobalBasis.Z).Dot(Vector3.Back) > .99f,
                "Stationary attacker follows moving target using only target POS");
            Check(Math.Abs(player.CurrentAnimationPosition - phase) < .001, "Following target does not restart swing");
            // 죽은 대상을 향한 마지막 타격도 끝까지 보여줍니다.
            Receive("STATE 77 ATTACK 0 99 2");
            Basis beforeRemoval = worker.GetNode<Node3D>("Visual").Basis;
            Receive("REMOVE 99");
            worker._Process(0);
            Check(worker.GetNode<Node3D>("Visual").Basis.IsEqualApprox(beforeRemoval), "Removed unit keeps last facing");
            Receive("STATE 77 IDLE 0 0 2");
            player.Advance(1);
            Check(player.CurrentAnimation == "Idle", "Final swing ends; no attack loop during cooldown");
            Receive("STATE 77 ATTACK 5 99 3");
            player.Advance(1);
            Check(player.CurrentAnimation == "Carry" && wood.Visible, "Swing with cargo returns to carrying");

            UnitState before = worker.State;
            foreach (string bad in new[] { "STATE 77 GATHER -1 700 4", "STATE 77 GATHER 0 NaN 4", "STATE 77 GATHER 0 -1 4", "STATE 77 UNKNOWN 0 700 4", "STATE 77 IDLE", "STATE 999 GATHER 0 700 4", "STATE 77 GATHER 0 4 0 4" })
                Receive(bad);
            Check(worker.State == before, "Malformed and unknown states ignored");

            Receive("UNIT 0 88 1 5 0");
            Receive("STATE 88 ATTACK 5 200 32");
            Unit joined = Units.GetNode<Unit>("Unit_88");
            Check(joined.GetNode<AnimationPlayer>("AnimationPlayer").CurrentAnimation == "Carry", "Late join gets cargo without replaying old attacks");
            Receive("UNIT 0 200 2 5 4");
            joined._Process(0);
            Check((-joined.GetNode<Node3D>("Visual").GlobalBasis.Z).Dot(Vector3.Back) > .99f,
                "Snapshot STATE may arrive before target UNIT");
            Receive("STATE 88 GATHER 0 701 32");
            Receive("POS 200 9 0");
            joined._Process(0);
            Check((-joined.GetNode<Node3D>("Visual").GlobalBasis.Z).Dot(Vector3.Back) > .99f,
                "Changing focus discards the previous target reference");
            Resources.SpawnOrUpdate(701, 0, new Vector3(1, 0, 0));
            joined._Process(0);
            Check((-joined.GetNode<Node3D>("Visual").GlobalBasis.Z).Dot(Vector3.Left) > .99f,
                "Delayed resource resolves without another STATE");
            Check(joined.GetNode<AnimationPlayer>("AnimationPlayer").CurrentAnimation == "Swing", "Late join restores gathering");
            Receive("REMOVE 88");
            Receive("STATE 88 IDLE 0 0 33");
            Check(joined.IsDying && !Units.HasNode("Unit_88"), "State never recreates removed unit while its death effect plays");
            Map.Free();
            GD.Print("PASS: worker animations, moving focus via POS, late target spawn, focus switch, removed targets, duplicate hits, cargo and malformed messages");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
    }
}
