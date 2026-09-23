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
    public bool IsSynchronized { get; private set; }
    public string SyncError { get; private set; }
    private MapFile _map;
    private bool _accepted;

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
            map.Rows.Any(r => r == null || r.Length != map.Rows[0].Length || r.Any(c => c != '#' && c != '.' && c != 'T')))
            throw new InvalidOperationException("잘못된 맵 형식");

        var occupied = new bool[map.Rows.Length, map.Rows[0].Length];
        for (int z = 0; z < map.Rows.Length; z++)
            for (int x = 0; x < map.Rows[z].Length; x++)
                if (map.Rows[z][x] == 'T')
                    for (int dz = 0; dz < map.TreeSize; dz++)
                        for (int dx = 0; dx < map.TreeSize; dx++)
                        {
                            int cx = x + dx, cz = z + dz;
                            if (cz >= map.Rows.Length || cx >= map.Rows[0].Length ||
                                map.Rows[cz][cx] == '#' || occupied[cz, cx])
                                throw new InvalidOperationException("나무 점유 영역이 겹치거나 맵 밖입니다.");
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
        Resources.Clear();
        for (int z = 0; z < _map.Rows.Length; z++)
            for (int x = 0; x < _map.Rows[z].Length; x++)
                if (_map.Rows[z][x] == 'T')
                {
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
            Resources.Remove(id);
        else if (Resources.TryGetResource(id, out ResourceNode tree))
            tree.SetAmount(amount);
        else
            return Fail("제거된 나무의 자원량 변경");
        return true;
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
