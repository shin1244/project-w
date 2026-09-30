using Godot;
using System;

// 배치 검사와 시야 가림이 같은 직사각형 점유 영역을 사용합니다. 메시 장식/선택 원은 제외합니다.
public static class BuildingFootprint
{
    public static Vector2[] Corners(Building building)
    {
        Vector2 half = (Vector2)building.GetMeta("footprint").AsVector2I() * .5f;
        var corners = new Vector2[4];
        Vector3[] local = { new(-half.X, 0, -half.Y), new(half.X, 0, -half.Y),
            new(half.X, 0, half.Y), new(-half.X, 0, half.Y) };
        for (int i = 0; i < 4; i++)
        {
            Vector3 point = building.GlobalTransform * local[i];
            corners[i] = new Vector2(point.X, point.Z);
        }
        return corners;
    }

    public static Rect2 Bounds(Vector2[] corners)
    {
        var bounds = new Rect2(corners[0], Vector2.Zero);
        for (int i = 1; i < corners.Length; i++) bounds = bounds.Expand(corners[i]);
        return bounds;
    }

    public static bool Overlaps(Rect2 area, Vector2[] corners)
    {
        Vector2[] rectangle = { area.Position, new(area.End.X, area.Position.Y), area.End, new(area.Position.X, area.End.Y) };
        Vector2[] axes = { Vector2.Right, Vector2.Down,
            (corners[1] - corners[0]).Normalized(), (corners[3] - corners[0]).Normalized() };
        foreach (Vector2 axis in axes)
        {
            Project(rectangle, axis, out float a0, out float a1);
            Project(corners, axis, out float b0, out float b1);
            if (a1 <= b0 + .0001f || b1 <= a0 + .0001f) return false;
        }
        return true;
    }

    private static void Project(Vector2[] corners, Vector2 axis, out float min, out float max)
    {
        min = float.PositiveInfinity; max = float.NegativeInfinity;
        foreach (Vector2 point in corners)
        {
            float projection = point.Dot(axis);
            min = Math.Min(min, projection); max = Math.Max(max, projection);
        }
    }
}
