using Godot;
using System;

// 서버 range.go와 같은 원/축 정렬 사각형의 가장자리 거리. 모델 장식은 제외합니다.
public readonly record struct RangeBody(Vector2 Center, Vector2 HalfExtents, float Radius)
{
    public float DistanceTo(RangeBody other)
    {
        Vector2 gap = (Center - other.Center).Abs() - HalfExtents - other.HalfExtents;
        return Math.Max(0, gap.Max(Vector2.Zero).Length() - Radius - other.Radius);
    }

    // 중심에서 정규화된 방향으로, 몸 표면 + reach 경계까지의 거리.
    // 사각형은 둥근 모서리로 확장합니다. 외접원으로 바꾸면 벽면/모서리에서 범위가 달라집니다.
    public float BoundaryDistance(Vector2 direction, float reach)
    {
        Vector2 d = direction.Abs();
        float r = Math.Max(0, reach) + Radius;
        float x = d.X > 1e-7f ? (HalfExtents.X + r) / d.X : float.PositiveInfinity;
        float y = d.Y > 1e-7f ? (HalfExtents.Y + r) / d.Y : float.PositiveInfinity;
        float limit = Math.Min(x, y);
        Vector2 point = d * limit;
        if (point.X <= HalfExtents.X || point.Y <= HalfExtents.Y) return limit;
        float projection = d.Dot(HalfExtents);
        return projection + MathF.Sqrt(Math.Max(0, projection * projection - HalfExtents.LengthSquared() + r * r));
    }
}
