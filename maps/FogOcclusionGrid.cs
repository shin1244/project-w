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
    private readonly int[] _buildings;
    private readonly Dictionary<uint, int> _tokens = new();
    private uint _mapVersion = uint.MaxValue;
    private uint _layoutVersion = uint.MaxValue;
    public uint Revision { get; private set; }

    public FogOcclusionGrid(Vector2 origin, float cell, Vector2I size)
    {
        _origin = origin; _cell = cell; _size = size;
        _terrain = new bool[checked(size.X * size.Y)];
        _buildings = new int[_terrain.Length];
    }

    public void Refresh(MapWorld map, BuildingManager buildings)
    {
        uint layout = GodotObject.IsInstanceValid(buildings) ? buildings.LayoutVersion : 0;
        if (_mapVersion == map.OcclusionVersion && _layoutVersion == layout) return;
        _mapVersion = map.OcclusionVersion; _layoutVersion = layout;
        map.CopyVisionObstacles(_terrain);
        Array.Clear(_buildings);
        _tokens.Clear();
        if (GodotObject.IsInstanceValid(buildings))
            foreach (Building building in buildings.LiveBuildings)
            {
                int token = _tokens.Count + 1;
                _tokens[building.BuildingId] = token;
                Vector2[] corners = BuildingFootprint.Corners(building);
                Rect2 bounds = BuildingFootprint.Bounds(corners);
                Vector2 min = bounds.Position, max = bounds.End;
                int x0 = Math.Max(0, Mathf.FloorToInt((min.X - _origin.X) / _cell));
                int z0 = Math.Max(0, Mathf.FloorToInt((min.Y - _origin.Y) / _cell));
                int x1 = Math.Min(_size.X - 1, Mathf.CeilToInt((max.X - _origin.X) / _cell) - 1);
                int z1 = Math.Min(_size.Y - 1, Mathf.CeilToInt((max.Y - _origin.Y) / _cell) - 1);
                for (int z = z0; z <= z1; z++)
                    for (int x = x0; x <= x1; x++)
                    {
                        if (!BuildingFootprint.Overlaps(new Rect2(_origin + new Vector2(x, z) * _cell, Vector2.One * _cell), corners)) continue;
                        int i = z * _size.X + x;
                        _buildings[i] = _buildings[i] == 0 ? token : -1;
                    }
            }
        Revision++;
    }

    public int BuildingToken(uint id) => _tokens.TryGetValue(id, out int token) ? token : 0;

    private bool Blocked(int x, int z, int ignore)
    {
        if (x < 0 || z < 0 || x >= _size.X || z >= _size.Y) return true;
        int i = z * _size.X + x;
        return _terrain[i] || (_buildings[i] != 0 && _buildings[i] != ignore);
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
