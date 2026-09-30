using Godot;
using System.Collections.Generic;
using System.Text.Json;

// 한 장의 사전 렌더 이미지와 지면 점유 사각형만 그립니다. 노드와 텍스처는 배치 사이에도 재사용합니다.
public partial class PlacementPreviewImage : Node2D
{
    public sealed record Asset(Texture2D Texture, Vector2 Footprint, float WorldSpan, Vector2 Anchor);
    private readonly Dictionary<uint, Asset> _assets = new();
    private readonly Vector2[] _footprint = new Vector2[4];
    private Asset _asset;
    public Texture2D Texture => _asset?.Texture;
    public Rect2 ImageRect { get; private set; }
    public Color Tint { get; private set; }

    public override void _Ready()
    {
        TextureFilter = TextureFilterEnum.Linear;
        Hide();
        // 클릭 경로에는 파일 읽기, 씬 인스턴스화 또는 재질 생성이 없습니다.
        using JsonDocument catalog = JsonDocument.Parse(FileAccess.GetFileAsString("res://ui/build-previews/catalog.json"));
        foreach (JsonElement entry in catalog.RootElement.EnumerateArray())
        {
            var footprint = entry.GetProperty("Footprint");
            var anchor = entry.GetProperty("Anchor");
            _assets.Add(entry.GetProperty("Type").GetUInt32(), new Asset(
                GD.Load<Texture2D>(entry.GetProperty("Texture").GetString()),
                new Vector2(footprint[0].GetSingle(), footprint[1].GetSingle()),
                entry.GetProperty("WorldSpan").GetSingle(), new Vector2(anchor[0].GetSingle(), anchor[1].GetSingle())));
        }
    }

    public bool Select(uint type, out Vector2 footprint)
    {
        footprint = default;
        if (!_assets.TryGetValue(type, out Asset asset) || asset.Texture == null) return false;
        _asset = asset;
        footprint = asset.Footprint;
        return true;
    }

    public void Update(Camera3D camera, Vector3 center, bool valid)
    {
        if (_asset == null) return;
        Vector2 anchor = camera.UnprojectPosition(center);
        // 화면 크기/KeepAspect/줌이 달라도 3D 모델과 같은 월드 크기를 유지합니다.
        float pixels = anchor.DistanceTo(camera.UnprojectPosition(center + camera.GlobalBasis.X * _asset.WorldSpan));
        ImageRect = new Rect2(anchor - _asset.Anchor * pixels, Vector2.One * pixels);
        Vector2 half = _asset.Footprint * .5f;
        _footprint[0] = camera.UnprojectPosition(center + new Vector3(-half.X, 0, -half.Y));
        _footprint[1] = camera.UnprojectPosition(center + new Vector3(half.X, 0, -half.Y));
        _footprint[2] = camera.UnprojectPosition(center + new Vector3(half.X, 0, half.Y));
        _footprint[3] = camera.UnprojectPosition(center + new Vector3(-half.X, 0, half.Y));
        Tint = valid ? new Color(.65f, 1f, .75f, .65f) : new Color(1f, .35f, .28f, .72f);
        Show();
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_asset == null) return;
        DrawColoredPolygon(_footprint, new Color(Tint, .15f));
        DrawTextureRect(_asset.Texture, ImageRect, false, Tint);
        for (int i = 0; i < 4; i++) DrawLine(_footprint[i], _footprint[(i + 1) % 4], new Color(Tint, .85f), 1.5f, true);
    }
}
