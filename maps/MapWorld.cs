using Godot;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Linq;

// 맵의 기본 상태를 로컬에서 생성하고 서버 변경분을 적용합니다.
public partial class MapWorld : Node3D
{
    [Export(PropertyHint.File, "*.json")] public string MapPath = "res://maps/test.json";
    [Export] public ResourceManager Resources;
    public string MapHash { get; private set; }
    public bool HasMap => _map != null;
    public string[] LaneNames => _map?.Lanes?.Select(lane => lane?.Name ?? "").ToArray() ?? Array.Empty<string>();
    public bool IsSynchronized { get; private set; }
    public string SyncError { get; private set; }
    private MapFile _map;
    private bool _accepted;
    private bool[] _visionBlocked;
    public uint OcclusionVersion { get; private set; }
    public Vector2 GridOrigin => new(_map.OriginX, _map.OriginZ);
    public float CellSize => _map.CellSize;
    public Vector2I GridSize => new(_map.Rows[0].Length, _map.Rows.Length);
    public bool TryGetBasePosition(uint team, out Vector3 position)
    {
        position = default;
        if (_map == null || team == 0 || team > _map.Bases.Length) return false;
        float[] coordinates = _map.Bases[(int)team - 1];
        position = new Vector3(coordinates[0], 0, coordinates[1]);
        return true;
    }
    public void CopyVisionObstacles(bool[] destination) => _visionBlocked.CopyTo(destination, 0);
    public bool IsTerrainBlocked(int x, int z) => _map == null || x < 0 || z < 0 ||
        x >= GridSize.X || z >= GridSize.Y || _visionBlocked[z * GridSize.X + x];
    // Roads remain walkable and transparent to vision, but cannot host new buildings.
    public bool IsRoad(int x, int z) => _map != null && x >= 0 && z >= 0 &&
        x < GridSize.X && z < GridSize.Y && _map.Rows[z][x] == 'R';

    // 칸당 한 픽셀. 나무의 전체 점유 영역과 벌목 후 빈 땅을 반영합니다.
    public Image CreateMinimapTerrainImage()
    {
        if (_map == null || _visionBlocked == null) return null;

        int width = _map.Rows[0].Length, height = _map.Rows.Length;
        var image = Image.CreateEmpty(width, height, false, Image.Format.Rgb8);
        var groundColor = new Color("49783e");
        var roadColor = new Color("8b8e8c");
        var treeColor = new Color(0.13f, 0.22f, 0.17f);
        var wallColor = new Color(0.12f, 0.15f, 0.18f);
        for (int z = 0; z < height; z++)
            for (int x = 0; x < width; x++)
            {
                Color color = _map.Rows[z][x] == '#' ? wallColor : _map.Rows[z][x] == 'W' ? new Color("727d80") :
                    _visionBlocked[z * width + x] ? treeColor : IsRoad(x, z) ? roadColor : groundColor;
                image.SetPixel(x, z, color);
            }
        return image;
    }

    public sealed class MapFile
    {
        public string Name { get; set; }
        public float OriginX { get; set; }
        public float OriginZ { get; set; }
        public float CellSize { get; set; }
        public int TreeAmount { get; set; }
        public int TreeSize { get; set; } = 1;
        public float[][] Bases { get; set; }
        public string[] Rows { get; set; }
        public MapLane[] Lanes { get; set; } = Array.Empty<MapLane>();
        public MapActivityArea[] ActivityAreas { get; set; } = Array.Empty<MapActivityArea>();
    }

    public sealed class MapLane
    {
        public string Name { get; set; }
    }

    public sealed class MapActivityArea
    {
        public string Id { get; set; }
        public string Kind { get; set; }
        public float[] Center { get; set; }
        public float[] Radii { get; set; }
        public float[][] Entrances { get; set; } = Array.Empty<float[]>();
        public float EntranceWidth { get; set; }

        public bool IsValid() => !string.IsNullOrWhiteSpace(Id) && (Kind == "event" || Kind == "jungle") &&
            IsPoint(Center) && IsPoint(Radii) && Radii.All(radius => radius > 0) &&
            Entrances != null && Entrances.All(IsPoint) && float.IsFinite(EntranceWidth) &&
            EntranceWidth >= 0 && (Entrances.Length == 0 || EntranceWidth > 0);

        private static bool IsPoint(float[] point) =>
            point != null && point.Length == 2 && point.All(float.IsFinite);

        public bool Contains(Vector2 position)
        {
            var center = new Vector2(Center[0], Center[1]);
            var offset = position - center;
            var normalized = new Vector2(offset.X / Radii[0], offset.Y / Radii[1]);
            if (normalized.LengthSquared() <= 1) return true;
            float halfWidth = EntranceWidth * 0.5f;
            foreach (var entrance in Entrances)
            {
                var segment = new Vector2(entrance[0], entrance[1]) - center;
                float lengthSquared = segment.LengthSquared();
                float progress = lengthSquared > 0 ? Mathf.Clamp(offset.Dot(segment) / lengthSquared, 0, 1) : 0;
                if (position.DistanceSquaredTo(center + segment * progress) <= halfWidth * halfWidth) return true;
            }
            return false;
        }
    }

    public override void _Ready()
    {
        try { LoadLocalMap(); }
        catch (Exception error) { Fail("맵 로드 실패: " + error.Message); }
    }

    public void LoadLocalMap()
    {
        byte[] bytes = FileAccess.GetFileAsBytes(MapPath);
        var map = JsonSerializer.Deserialize<MapFile>(bytes,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (map?.Rows == null || map.Rows.Length == 0 || string.IsNullOrEmpty(map.Rows[0]) ||
            !float.IsFinite(map.OriginX) || !float.IsFinite(map.OriginZ) ||
            !float.IsFinite(map.CellSize) || map.CellSize <= 0 || map.TreeAmount <= 0 ||
            (map.TreeSize != 1 && map.TreeSize != 2) ||
            string.IsNullOrWhiteSpace(map.Name) || map.Bases == null || map.Bases.Length == 0 ||
            map.Bases.Any(b => b == null || b.Length != 2 || b.Any(v => !float.IsFinite(v))) ||
            map.Rows.Any(r => r == null || r.Length != map.Rows[0].Length || r.Any(c => c != '#' && c != '.' && c != 'T' && c != 'R' && c != 'W')))
            throw new InvalidOperationException("잘못된 맵 형식");

        if (map.ActivityAreas == null || map.ActivityAreas.Any(area => area == null || !area.IsValid()))
            throw new InvalidOperationException("잘못된 이벤트·정글 구역 형식");

        var occupied = new bool[map.Rows.Length, map.Rows[0].Length];
        for (int z = 0; z < map.Rows.Length; z++)
            for (int x = 0; x < map.Rows[z].Length; x++)
                if (map.Rows[z][x] == 'T')
                    for (int dz = 0; dz < map.TreeSize; dz++)
                        for (int dx = 0; dx < map.TreeSize; dx++)
                        {
                            int cx = x + dx, cz = z + dz;
                            if (cz >= map.Rows.Length || cx >= map.Rows[0].Length ||
                                map.Rows[cz][cx] == '#' || map.Rows[cz][cx] == 'R' || map.Rows[cz][cx] == 'W' || occupied[cz, cx])
                                throw new InvalidOperationException("나무 점유 영역이 겹치거나 도로·맵 밖입니다.");
                            occupied[cz, cx] = true;
                        }

        _map = map;
        MapHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        IsSynchronized = false;
        _accepted = false;
        SyncError = null;
        foreach (Node child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }
        using var builder = (RefCounted)GD.Load<GDScript>("res://maps/TerrainBuilder.gd").New();
        Node3D terrain = builder.Call("generate", Json.ParseString(Encoding.UTF8.GetString(bytes))).As<Node3D>();
        AddChild(terrain);
        ResetTrees();
    }

    private void ResetTrees()
    {
        _visionBlocked = new bool[_map.Rows.Length * _map.Rows[0].Length];
        for (int z = 0; z < _map.Rows.Length; z++)
            for (int x = 0; x < _map.Rows[z].Length; x++)
                if (_map.Rows[z][x] == '#' || _map.Rows[z][x] == 'W') _visionBlocked[z * _map.Rows[0].Length + x] = true;
        OcclusionVersion++;
        Resources.Clear();
        for (int z = 0; z < _map.Rows.Length; z++)
            for (int x = 0; x < _map.Rows[z].Length; x++)
                if (_map.Rows[z][x] == 'T')
                {
                    SetTreeObstacle(x, z, true);
                    uint id = checked((uint)(z * _map.Rows[0].Length + x + 1));
                    var tree = Resources.SpawnOrUpdate(id, (uint)((x + z) % 3), new Vector3(
                        _map.OriginX + (x + _map.TreeSize * 0.5f) * _map.CellSize, 0,
                        _map.OriginZ + (z + _map.TreeSize * 0.5f) * _map.CellSize));
                    if (tree == null) throw new InvalidOperationException("나무 씬이 지정되지 않았습니다.");
                    tree.SetAmount(_map.TreeAmount);
                }
    }

    public bool AcceptMap(string[] parts)
    {
        IsSynchronized = false;
        _accepted = false;
        if (_map == null || parts.Length != 3 || parts[1] != "2" || parts[2] != MapHash)
            return Fail("서버와 맵 파일 또는 맵 프로토콜이 다릅니다. 같은 maps/test.json을 사용하세요.");
        ResetTrees(); // 재접속 시 기본 상태에서 현재 변경분을 다시 적용합니다.
        SyncError = null;
        _accepted = true;
        return true;
    }

    public bool ApplyTree(string[] parts)
    {
        if (!_accepted || parts.Length != 3 || !uint.TryParse(parts[1], out uint id) ||
            !int.TryParse(parts[2], out int amount) || amount < 0 || amount > _map.TreeAmount ||
            id == 0 || id > (long)_map.Rows.Length * _map.Rows[0].Length)
            return Fail("잘못된 TREE 메시지");
        int index = (int)id - 1;
        if (_map.Rows[index / _map.Rows[0].Length][index % _map.Rows[0].Length] != 'T')
            return Fail("맵에 없는 나무 ID");
        if (amount == 0)
        {
            Resources.Remove(id);
            SetTreeObstacle(index % _map.Rows[0].Length, index / _map.Rows[0].Length, false);
            OcclusionVersion++;
        }
        else if (Resources.TryGetResource(id, out ResourceNode tree))
            tree.SetAmount(amount);
        else
            return Fail("제거된 나무의 자원량 변경");
        return true;
    }

    private void SetTreeObstacle(int x, int z, bool blocked)
    {
        for (int dz = 0; dz < _map.TreeSize; dz++)
            for (int dx = 0; dx < _map.TreeSize; dx++)
                _visionBlocked[(z + dz) * _map.Rows[0].Length + x + dx] = blocked;
    }

    public bool CompleteSync()
    {
        if (!_accepted) return Fail("맵 확인 전 동기화 완료 메시지");
        IsSynchronized = true;
        return true;
    }

    public void StopSync() { _accepted = false; IsSynchronized = false; }

    private bool Fail(string message)
    {
        StopSync();
        SyncError = message;
        return false;
    }
}
