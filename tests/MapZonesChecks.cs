using Godot;
using System;

public partial class MapZonesChecks : Node3D
{
    public override void _Ready()
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
            var map = new MapWorld { Resources = resources, MapPath = "res://tests/fixtures/map-zones.json" };
            AddChild(map);
            Check(map.SyncError == null, "Road-aware map loads");
            Check(map.AcceptMap(new[] { "MAP", "2", map.MapHash }) && map.CompleteSync(), "Road-aware map synchronizes");
            string Issue(float x, float z, float width, float depth) =>
                PlacementRules.Check(map, null, null, new Vector3(x, 0, z), new Vector2(width, depth));
            Check(map.IsRoad(5, 4) && !map.IsTerrainBlocked(5, 4), "Road is not a vision obstacle");
            Check(map.IsTerrainBlocked(8, 4) && Issue(8.5f, 4.5f, 1, 1)?.Contains("벽") == true &&
                resources.GetChildCount() == 1, "Stone wall blocks vision/building without spawning a tree");
            Check(Issue(6, 4, 2, 2)?.Contains("도로") == true, "Road rejects construction");
            Check(Issue(4.5f, 6.5f, 3, 3)?.Contains("도로") == true, "Footprint partly crossing the road is rejected");
            Check(Issue(3.5f, 6.5f, 3, 3) == null, "A grass building touching the road edge is allowed");
            Check(Issue(3, 4, 2, 2)?.Contains("나무") == true, "The complete initial tree footprint blocks building");
            Check(map.ApplyTree(new[] { "TREE", "39", "0" }), "Tree can be harvested");
            Check(Issue(3, 4, 2, 2) == null, "Cleared forest permits construction");
            Check(map.IsTerrainBlocked(8, 4), "Harvest preserves permanent walls");
            Check(Issue(6, 4, 2, 2)?.Contains("도로") == true, "Harvest never unlocks the road");
            Check(map.AcceptMap(new[] { "MAP", "2", map.MapHash }) && map.CompleteSync() &&
                Issue(3, 4, 2, 2)?.Contains("나무") == true && Issue(6, 4, 2, 2)?.Contains("도로") == true,
                "Reconnect restores trees and retains road restrictions");
            GD.Print("PASS: road/wall construction restrictions and tree harvest/reconnect");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
