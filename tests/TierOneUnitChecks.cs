using Godot;
using System;

public partial class TierOneUnitChecks : Node3D
{
    public override void _Ready()
    {
        try
        {
            var manager = new UnitManager
            {
                KnightScene = GD.Load<PackedScene>("res://units/Knight.tscn"),
                ArcherScene = GD.Load<PackedScene>("res://units/Archer.tscn")
            };
            AddChild(manager);
            manager.SetLocalPlayer(7, 1);
            foreach (uint type in new uint[] { 1, 2 })
            {
                uint id = type * 10;
                uint minionType = type == UnitCatalog.Knight ? UnitCatalog.MinionMelee : UnitCatalog.MinionRanged;
                manager.HandleSpawn($"UNIT {type} {id} 7 0 0 1".Split(' '));
                manager.HandleSpawn($"UNIT {minionType} {id + 1} 0 4 0 1".Split(' '));
                manager.TryGetUnit(id, out Unit soldier);
                manager.TryGetUnit(id + 1, out Unit minion);
                Check(soldier.HasNode("Visual/Rig") && !minion.HasNode("Visual/Rig") && soldier.UnitType == type && minion.UnitType == minionType,
                    "Distinct server type IDs select independent RTS and minion models");
                Aabb large = Bounds(soldier.GetNode<Node3D>("Visual"));
                Aabb small = Bounds(minion.GetNode<Node3D>("Visual"));
                float ratio = large.End.Y / small.End.Y;
                Check(ratio >= 1.25f && ratio <= 1.4f, $"Readable intermediate size: {ratio}");
                Check(large.Position.Y >= -.001f && large.Position.Y < .01f, "Feet stay on the ground");
                Check(soldier.Scale == Vector3.One && soldier.HealthBarHeight > large.End.Y, "World positions stay unscaled and HP is above the head");
                Check(soldier.PlacementRadius == (type == 1 ? .5f : .4f), "Picking model does not enlarge server construction footprint");
                foreach (string part in new[] { "Head", "Body", "LeftArm", "RightArm", "LeftLeg", "RightLeg" })
                    Check(soldier.HasNode("Visual/Rig/" + part), "Articulated soldier part: " + part);
                manager.HandleSpawn($"UNIT {type} {id} 7 1 2 1".Split(' '));
                manager.TryGetUnit(id, out Unit again);
                Check(again == soldier && soldier.GlobalPosition == new Vector3(1, 0, 2), "Repeated snapshots preserve model and update location");
                manager.SelectSingle(minion);
                Check(manager.SelectedUnitIds.Count == 0 && manager.InspectedUnit == minion, "Minions can be inspected without being controlled");
                manager.SelectSingle(soldier);
                Check(manager.SelectedUnitIds.Count == 1, "RTS soldiers retain ownership and selection");
                manager.ClearSelection();
            }
            GD.Print("PASS: RTS/minion model routing, actual size hierarchy, grounded rigs, HP anchors, unchanged placement footprint, selection and snapshot reuse");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private static Aabb Bounds(Node3D root)
    {
        Aabb result = default;
        bool found = false;
        void Visit(Node node, Transform3D transform)
        {
            if (node is Node3D spatial) transform *= spatial.Transform;
            if (node is MeshInstance3D mesh)
            {
                Aabb bounds = transform * mesh.GetAabb();
                result = found ? result.Merge(bounds) : bounds;
                found = true;
            }
            foreach (Node child in node.GetChildren()) Visit(child, transform);
        }
        foreach (Node child in root.GetChildren()) Visit(child, Transform3D.Identity);
        return result;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
