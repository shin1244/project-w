using Godot;
using System;
using System.Collections.Generic;

// 시야를 막는 물체만 격자로 조회합니다. 화면 시야의 외곽선은 이 격자로 그리지 않습니다.
public sealed class FogOcclusionGrid
{
    private readonly Vector2 _origin;
    private readonly float _cell;
    private readonly Vector2I _size;
    private readonly bool[] _terrain;
    private uint _mapVersion = uint.MaxValue;
    public uint Revision { get; private set; }

    public FogOcclusionGrid(Vector2 origin, float cell, Vector2I size)
    {
        _origin = origin; _cell = cell; _size = size;
        _terrain = new bool[checked(size.X * size.Y)];
    }

    public void Invalidate() => _mapVersion = uint.MaxValue;

    public void Refresh(MapWorld map, BuildingManager buildings, IReadOnlyDictionary<uint, Vector2> bodySizes = null)
    {
        if (_mapVersion == map.OcclusionVersion) return;
        _mapVersion = map.OcclusionVersion;
        map.CopyVisionObstacles(_terrain);
        Revision++;
    }

    private bool Blocked(int x, int z, int ignore)
    {
        if (x < 0 || z < 0 || x >= _size.X || z >= _size.Y) return true;
        int i = z * _size.X + x;
        return _terrain[i]; // 건물 충돌은 이동/배치 격자에만 남깁니다.
    }

    // 정규화된 방향으로 격자를 통과하며 최초 장애물 경계까지만 반환합니다. 물리 쿼리는 없습니다.
    public float RayDistance(Vector2 from, Vector2 direction, float radius, int ignore = 0)
    {
        int x = Mathf.FloorToInt((from.X - _origin.X) / _cell);
        int z = Mathf.FloorToInt((from.Y - _origin.Y) / _cell);
        if (x < 0 || z < 0 || x >= _size.X || z >= _size.Y) return 0;
        int sx = direction.X < 0 ? -1 : 1, sz = direction.Y < 0 ? -1 : 1;
        double nextX = double.PositiveInfinity, nextZ = double.PositiveInfinity;
        double dx = double.PositiveInfinity, dz = double.PositiveInfinity;
        if (Math.Abs(direction.X) > 1e-7f)
        {
            float edge = _origin.X + (x + (sx > 0 ? 1 : 0)) * _cell;
            nextX = (edge - from.X) / direction.X;
            dx = _cell / Math.Abs(direction.X);
        }
        if (Math.Abs(direction.Y) > 1e-7f)
        {
            float edge = _origin.Y + (z + (sz > 0 ? 1 : 0)) * _cell;
            nextZ = (edge - from.Y) / direction.Y;
            dz = _cell / Math.Abs(direction.Y);
        }
        while (Math.Min(nextX, nextZ) < radius)
        {
            double distance = Math.Min(nextX, nextZ);
            if (Math.Abs(nextX - nextZ) < 1e-6)
            {
                if (Blocked(x + sx, z, ignore) || Blocked(x, z + sz, ignore)) return (float)Math.Max(0, distance);
                x += sx; z += sz; nextX += dx; nextZ += dz;
            }
            else if (nextX < nextZ) { x += sx; nextX += dx; }
            else { z += sz; nextZ += dz; }
            if (Blocked(x, z, ignore)) return (float)Math.Max(0, distance);
        }
        return radius;
    }
}
