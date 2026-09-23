using Godot;
using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Reflection;

public partial class MapSyncChecks : Node3D
{
    public override async void _Ready()
    {
        try
        {
            var resources = new ResourceManager
            {
                OakScene = GD.Load<PackedScene>("res://resources/trees/Oak.tscn"),
                PineScene = GD.Load<PackedScene>("res://resources/trees/Pine.tscn"),
                BirchScene = GD.Load<PackedScene>("res://resources/trees/Birch.tscn")
            };
            AddChild(resources);
            var map = new MapWorld { Resources = resources, MapPath = "res://tests/fixtures/map-sync.json" };
            AddChild(map);
            Check(map.SyncError == null && !map.IsSynchronized, "Load local map before handshake");
            Check(resources.GetChildCount() == 39, "39 local trees without RESOURCE packets");
            const uint firstTree = 4412; // row 26, column 43, width 168
            Check(resources.TryGetResource(firstTree, out var tree) &&
                tree.Position.IsEqualApprox(new Vector3(-40.5f, 0, -19.5f)) && tree.Amount == 400,
                "Cross-language cell ID and center");
            Check(resources.GetChildren().Cast<ResourceNode>().Select(t => t.VisualVariant).Distinct().Count() == 3,
                "Deterministic three tree variants");
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            var mesh = map.GetNode<MeshInstance3D>("SymmetricArena/TerrainSurface").Mesh;
            Check(mesh != null, "Runtime terrain created from JSON");
            Check(!map.AcceptMap(new[] { "MAP", "2", new string('0', 64) }) && !map.CompleteSync(), "Mismatched map blocks play");
            Check(map.AcceptMap(new[] { "MAP", "2", map.MapHash }), "Matching hash accepted");
            Check(!map.IsSynchronized, "Wait for snapshot completion");
            Check(map.ApplyTree(new[] { "TREE", "4412", "375" }) && map.CompleteSync(), "Partial harvest snapshot");
            Check(resources.TryGetResource(firstTree, out tree) && tree.Amount == 375, "Remaining amount applied");
            Check(map.ApplyTree(new[] { "TREE", "4412", "0" }) && !resources.TryGetResource(firstTree, out _), "Live removal");
            Check(map.ApplyTree(new[] { "TREE", "4412", "0" }), "Duplicate deletion is safe");
            Check(map.AcceptMap(new[] { "MAP", "2", map.MapHash }) && resources.GetChildCount() == 39, "Reconnect resets base state");
            Check(map.ApplyTree(new[] { "TREE", "4412", "0" }) && map.CompleteSync() && resources.GetChildCount() == 38,
                "Reconnect applies tombstone without respawning deleted tree");
            Check(!map.ApplyTree(new[] { "TREE", "1", "10" }) && !map.IsSynchronized, "Reject non-tree ID");
            Check(map.AcceptMap(new[] { "MAP", "2", map.MapHash }) &&
                !map.ApplyTree(new[] { "TREE", "4412", "-1" }), "Reject invalid amount");
            CheckMainRouting(map, resources);
            map.MapPath = "res://tests/fixtures/map-trees-2x2.json";
            map.LoadLocalMap();
            Check(resources.GetChildCount() == 2 && resources.TryGetResource(10, out var largeTree) &&
                largeTree.Position.IsEqualApprox(new Vector3(2, 0, 2)) && largeTree.Scale == Vector3.One,
                "2x2 tree uses footprint center and unchanged model scale");
            Check(map.AcceptMap(new[] { "MAP", "2", map.MapHash }) && map.ApplyTree(new[] { "TREE", "10", "0" }) &&
                !resources.TryGetResource(10, out _) && resources.TryGetResource(13, out _), "Remove one 2x2 tree without affecting neighbor");
            GD.Print($"PASS: runtime map, hash handshake, tree deltas, tombstones, reconnect; SHA256 {map.MapHash}");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private void CheckMainRouting(MapWorld map, ResourceManager resources)
    {
        using var sent = new MemoryStream();
        using var writer = new StreamWriter(sent, new UTF8Encoding(false), 1024, true) { AutoFlush = true };
        var net = new NetClient();
        typeof(NetClient).GetField("_writer", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(net, writer);
        var units = new UnitManager { KnightScene = GD.Load<PackedScene>("res://units/Knight.tscn") };
        AddChild(units);
        var main = new Main { Map = map, Resources = resources, Units = units };
        typeof(Main).GetField("_net", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(main, net);
        var onMessage = typeof(Main).GetMethod("OnMessage", BindingFlags.Instance | BindingFlags.NonPublic);
        var sendCommand = typeof(Main).GetMethod("SendCommand", BindingFlags.Instance | BindingFlags.NonPublic);
        void Receive(string msg) => onMessage.Invoke(main, new object[] { msg });
        sendCommand.Invoke(main, new object[] { "MOVE 0 0 16000" });
        Check(sent.Length == 0, "Commands blocked before map verification");
        Receive($"MAP 2 {map.MapHash}");
        Check(Encoding.UTF8.GetString(sent.ToArray()).Trim() == $"MAP_READY {map.MapHash}", "Main acknowledges exact hash");
        Receive("UNIT 1 16000 7 0 0");
        Check(units.GetChildCount() == 0, "Units ignored before snapshot end");
        Receive("TREE 4412 0");
        Receive("WORLD_READY");
        Receive("WELCOME 7");
        Receive("UNIT 1 16000 7 0 0");
        Check(map.IsSynchronized && units.GetChildCount() == 1 && !resources.TryGetResource(4412, out _), "Main dispatches snapshot and unit messages");
        Receive($"MAP 2 {map.MapHash}");
        Check(units.GetChildCount() == 0 && !map.IsSynchronized && resources.TryGetResource(4412, out _), "Reconnect clears stale units and resets trees");
        main.Free();
        net.Free();
    }
}
