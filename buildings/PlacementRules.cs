using Godot;

// 클라이언트가 알고 있는 장애물에 대한 사전 검사입니다. 비용/거리/건설 가능 여부의 최종 판정은 서버가 합니다.
public static class PlacementRules
{
    public static Vector3 Snap(MapWorld map, Vector3 point, Vector2 footprint)
    {
        Vector2 half = footprint * .5f;
        return new Vector3(
            map.GridOrigin.X + Mathf.Round((point.X - map.GridOrigin.X - half.X) / map.CellSize) * map.CellSize + half.X,
            0,
            map.GridOrigin.Y + Mathf.Round((point.Z - map.GridOrigin.Y - half.Y) / map.CellSize) * map.CellSize + half.Y);
    }

    public static string Check(MapWorld map, BuildingManager buildings, UnitManager units, Vector3 center, Vector2 footprint)
    {
        if (!GodotObject.IsInstanceValid(map) || !map.HasMap || !map.IsSynchronized) return "맵 동기화가 끝나지 않았습니다.";
        if (!center.IsFinite() || !footprint.IsFinite() || footprint.X <= 0 || footprint.Y <= 0) return "올바른 건설 위치가 아닙니다.";
        var area = new Rect2(new Vector2(center.X, center.Z) - footprint * .5f, footprint);
        Vector2 mapEnd = map.GridOrigin + (Vector2)map.GridSize * map.CellSize;
        if (area.Position.X < map.GridOrigin.X || area.Position.Y < map.GridOrigin.Y ||
            area.End.X > mapEnd.X || area.End.Y > mapEnd.Y) return "맵 밖에는 지을 수 없습니다.";
        Vector2 from = (area.Position - map.GridOrigin) / map.CellSize;
        Vector2 to = (area.End - map.GridOrigin) / map.CellSize;
        for (int z = Mathf.FloorToInt(from.Y + .0001f); z < Mathf.CeilToInt(to.Y - .0001f); z++)
            for (int x = Mathf.FloorToInt(from.X + .0001f); x < Mathf.CeilToInt(to.X - .0001f); x++)
            {
                if (map.IsRoad(x, z)) return "도로에는 건물을 지을 수 없습니다.";
                if (map.IsTerrainBlocked(x, z)) return "벽이나 나무가 있는 곳에는 지을 수 없습니다.";
            }
        if (GodotObject.IsInstanceValid(buildings))
            foreach (Building building in buildings.LiveBuildings)
                if (BuildingFootprint.Overlaps(area, BuildingFootprint.Corners(building))) return "다른 건물과 겹칩니다.";
        if (GodotObject.IsInstanceValid(units))
            foreach (Unit unit in units.LiveUnits)
            {
                if (unit.IsDying) continue;
                Vector2 position = new(unit.ServerPosition.X, unit.ServerPosition.Z);
                Vector2 closest = position.Clamp(area.Position, area.End);
                if (position.DistanceSquaredTo(closest) < unit.PlacementRadius * unit.PlacementRadius)
                    return "유닛이 있는 곳에는 지을 수 없습니다.";
            }
        return null;
    }
}
