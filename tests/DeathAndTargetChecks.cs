using Godot;
using System;
using System.Reflection;
using System.Collections.Generic;

public partial class DeathAndTargetChecks : Main
{
    private static readonly MethodInfo Handler = typeof(Main).GetMethod("OnMessage", BindingFlags.Instance | BindingFlags.NonPublic);
    private void Receive(string message) => Handler.Invoke(this, new object[] { message });

    public override async void _Ready()
    {
        try
        {
            Map = new MapWorld();
            typeof(MapWorld).GetField("<IsSynchronized>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(Map, true);
            Resources = new ResourceManager { OakScene = GD.Load<PackedScene>("res://resources/trees/Oak.tscn") };
            AddChild(Resources);
            Units = new UnitManager
            {
                Resources = Resources,
                WorkerScene = GD.Load<PackedScene>("res://units/Worker.tscn"),
                KnightScene = GD.Load<PackedScene>("res://units/Knight.tscn"),
                ArcherScene = GD.Load<PackedScene>("res://units/Archer.tscn")
            };
            AddChild(Units);
            var sent = new List<string>();
            Units.CommandRequested += sent.Add;
            Receive("WELCOME 1");
            for (int type = 0; type < 3; type++) Receive($"UNIT {type} {11 + type} 1 {type * 3} 0");
            Receive("UNIT 1 99 2 8 0");
            Unit worker = Units.GetNode<Unit>("Unit_11");
            Unit enemy = Units.GetNode<Unit>("Unit_99");
            ResourceNode tree = Resources.SpawnOrUpdate(700, 0, new Vector3(5, 0, 4));
            tree.SetAmount(400);

            Units.RequestGather(tree);
            Check(Ring(tree) == null && sent.Count == 0, "No selection means no target indication or command");
            Units.SelectSingle(worker);
            Units.RequestContextOrder(tree, tree.Position);
            MeshInstance3D yellow = Ring(tree);
            Color color = ((StandardMaterial3D)yellow.MaterialOverride).AlbedoColor;
            Check(yellow.Visible && color.R > .9f && color.G > .7f && color.B < .1f, "Gather ring is yellow");
            Check(worker.GetNode<MeshInstance3D>("SelectionRing").Visible, "Own green selection stays independent");
            Units.RequestContextOrder(enemy, enemy.Position);
            MeshInstance3D red = Ring(enemy);
            color = ((StandardMaterial3D)red.MaterialOverride).AlbedoColor;
            Check(red.Visible && color.R > .9f && color.G < .2f && color.B < .2f && !yellow.Visible,
                "Attack ring is red and replaces previous gather ring");
            Receive("POS 99 9 3");
            Check(red.GlobalPosition.IsEqualApprox(enemy.GlobalPosition + new Vector3(0, .055f, 0)), "Target ring follows movement");
            Units.RequestMove(new Vector3(2, 0, 1));
            Check(!red.Visible && Ring(enemy) == null, "Move clears target ring");
            Units.RequestAttack(enemy); // A-left-click uses this same path.
            Units.ClearSelection();
            Check(Ring(enemy) == null, "Selection changes clear target indication");
            Units.SelectSingle(worker);
            Units.RequestGather(tree);
            tree.SetAmount(0);
            Units._Process(0);
            Check(Ring(tree) == null, "Depleted resource clears target indication");
            tree.SetAmount(400);
            Units.RequestGather(tree);
            Resources.Remove(700);
            Units._Process(0);
            Check(Ring(tree) == null, "Removed resource clears indication before it is freed");
            Units.RequestAttack(enemy);
            Receive("REMOVE 99");
            Check(enemy.IsDying && Ring(enemy) == null, "Death clears red ring immediately");
            int commands = sent.Count;
            Units.RequestAttack(enemy);
            Check(sent.Count == commands, "Corpse is not an attack target");
            enemy.BeginDeath().Pause();

            for (int type = 0; type < 3; type++)
            {
                uint id = (uint)(11 + type);
                Unit unit = Units.GetNode<Unit>($"Unit_{id}");
                Units.SelectSingle(unit);
                if (type == 0)
                {
                    Receive($"STATE {id} GATHER 0 700 0");
                    unit.GetNode<AnimationPlayer>("AnimationPlayer").Advance(.3);
                }
                Vector3 position = unit.GlobalPosition;
                Receive($"REMOVE {id}");
                Tween tween = unit.BeginDeath(); // Repeated calls must return the same effect.
                tween.Pause();
                Check(unit.IsDying && !unit.IsQueuedForDeletion() && !Units.HasNode($"Unit_{id}") &&
                    Units.SelectedUnitIds.Count == 0 && unit.GetNode<Area3D>("SelectionArea").CollisionLayer == 0,
                    "REMOVE disables gameplay immediately but retains visual");
                if (type == 0) Check(!unit.GetNode<AnimationPlayer>("AnimationPlayer").IsPlaying(), "Worker swing freezes on death");
                Receive($"REMOVE {id}");
                Receive($"POS {id} 100 100");
                Receive($"STATE {id} GATHER 0 700 9");
                unit.SetSelected(true);
                Check(unit.BeginDeath() == tween && unit.GlobalPosition == position &&
                    !unit.GetNode<MeshInstance3D>("SelectionRing").Visible, "Duplicate/stale packets do not restart or move corpse");
                Units.SelectSingle(unit);
                Check(Units.SelectedUnitIds.Count == 0, "Corpse cannot be selected");

                Node3D visual = unit.GetNode<Node3D>("Visual");
                MeshInstance3D mesh = visual.FindChildren("*", "MeshInstance3D", true, false)[0] as MeshInstance3D;
                tween.CustomStep(UnitDeathEffect.FallSeconds + .001);
                Check(Mathf.Abs(visual.Basis.Y.Normalized().Dot(Vector3.Up)) < .01f && mesh.Transparency == 0,
                    "Every unit falls sideways before fading");
                tween.CustomStep(UnitDeathEffect.HoldSeconds + UnitDeathEffect.FadeSeconds / 2);
                Check(mesh.Transparency > .4f && mesh.Transparency < .7f, "Corpse fades gradually");
                tween.CustomStep(1);
                Check(unit.IsQueuedForDeletion(), "Corpse is freed after fade");
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                Check(!GodotObject.IsInstanceValid(unit), "Death effect leaves no unit node behind");
            }

            Receive("UNIT 0 55 1 0 0");
            Receive("REMOVE 55");
            Receive("UNIT 0 55 1 1 1");
            Check(!Units.GetNode<Unit>("Unit_55").IsDying, "New unit ID is independent from old death visual");
            Units.Clear();
            Check(Units.GetChildCount() == 0, "Map reset clears living units and unfinished deaths");
            Map.Free();
            GD.Print("PASS: all three death effects, fade, corpse exclusion, stale messages, target colors/follow/cleanup and map reset");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private static MeshInstance3D Ring(Node target) => target.GetNodeOrNull<MeshInstance3D>("CommandTargetRing");
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
