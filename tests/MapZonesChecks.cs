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
            using var before = map.CreateMinimapTerrainImage();
            Check(before.GetPixel(5, 4) != before.GetPixel(4, 4), "Minimap distinguishes road and grass");
            Check(map.ApplyTree(new[] { "TREE", "39", "0" }), "Tree can be harvested");
            Check(Issue(3, 4, 2, 2) == null, "Cleared forest permits construction");
            using var after = map.CreateMinimapTerrainImage();
            Check(after.GetPixel(2, 3) == after.GetPixel(4, 4), "Cleared forest appears as grass");
            Check(map.IsTerrainBlocked(8, 4) && after.GetPixel(8, 4) != after.GetPixel(4, 4),
                "Harvest preserves permanent wall and its minimap color");
            Check(Issue(6, 4, 2, 2)?.Contains("도로") == true, "Harvest never unlocks the road");
            Check(map.AcceptMap(new[] { "MAP", "2", map.MapHash }) && map.CompleteSync() &&
                Issue(3, 4, 2, 2)?.Contains("나무") == true && Issue(6, 4, 2, 2)?.Contains("도로") == true,
                "Reconnect restores trees and retains road restrictions");
            map.MapPath = "res://maps/test.json";
            map.LoadLocalMap();
            Check(map.SyncError == null, "The event/jungle map loads");
            Check(map.AcceptMap(new[] { "MAP", "2", map.MapHash }) && map.CompleteSync(), "The event/jungle map synchronizes");
            using var arena = map.CreateMinimapTerrainImage();
            Color ColorAt(float x, float z) => arena.GetPixel(
                (int)((x - map.GridOrigin.X) / map.CellSize), (int)((z - map.GridOrigin.Y) / map.CellSize));
            Color plaza = ColorAt(0, 0), camp = ColorAt(15, 13), road = ColorAt(0, 20);
            Check(plaza == road && camp == road, "Minimap uses the existing road color for event and jungle areas");
            foreach (int sx in new[] { -1, 1 })
                foreach (int sz in new[] { -1, 1 })
                {
                    Check(ColorAt(sx * 15, sz * 13) == camp && ColorAt(sx * 25, sz * 13) == camp,
                        "All four camps and their side entrances share the road color");
                    Check(Issue(sx * 15, sz * 13, 2, 2)?.Contains("도로") == true,
                        "Jungle combat areas stay reserved from construction");
                }
            Check(Issue(0, 0, 2, 2)?.Contains("도로") == true, "Event plaza stays reserved from construction");
            foreach (int sz in new[] { -1, 1 })
            {
                float x = sz * 34;
                Check(Issue(x + sz * .5f, sz * 36.5f, 3, 3) == null,
                    "Both curved expansion bays fit a store");
                Check(Issue(x - sz * 12, sz * 37, 3, 3)?.Contains("나무") == true &&
                    Issue(x + sz * 12, sz * 37, 3, 3)?.Contains("나무") == true &&
                    Issue(x - sz * 4, sz * 44, 3, 3)?.Contains("나무") == true,
                    "Forest surrounds the expansion bay at its sides and rear");
                Check(ColorAt(x, sz * 37) == before.GetPixel(4, 4),
                    "Both expansion sites use the existing grass color on the minimap");
            }
            GD.Print("PASS: road construction, harvest/reconnect, event/jungle tiles, and buildable expansion sites");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
