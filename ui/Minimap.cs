using Godot;

// 지형은 변경될 때만 다시 만들고, 표식은 서버가 알려준 현재 목록에서 그립니다.
public partial class Minimap : Control
{
    [Export] public MapWorld Map;
    [Export] public UnitManager Units;
    [Export] public BuildingManager Buildings;

    public uint Team { get; private set; }
    public static readonly Color AllyColor = new("63e88a");
    public static readonly Color EnemyColor = new("ff635f");
    private static readonly Color MarkerOutline = new("101b1a");
    private const float Padding = 12;
    private const float HeaderHeight = 20;
    private const float UnitRadius = 2.3f;
    private ImageTexture _terrain;
    private MapWorld _terrainMap;
    private uint _terrainVersion;
    private StyleBoxFlat _frame;

    // 맵의 실제 비율을 유지해 가로/세로 크기가 달라도 원과 사각형이 왜곡되지 않습니다.
    public Rect2 MapRect
    {
        get
        {
            if (!GodotObject.IsInstanceValid(Map) || !Map.HasMap) return default;
            Vector2 available = Size - new Vector2(Padding * 2, Padding * 2 + HeaderHeight);
            if (available.X <= 0 || available.Y <= 0) return default;
            Vector2 grid = new(Map.GridSize.X, Map.GridSize.Y);
            Vector2 fitted = grid * Mathf.Min(available.X / grid.X, available.Y / grid.Y);
            return new Rect2(new Vector2(Padding, Padding + HeaderHeight) + (available - fitted) * .5f, fitted);
        }
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        MouseForcePassScrollEvents = false;
        TextureFilter = TextureFilterEnum.Nearest;
        ClipContents = true;
        _frame = new StyleBoxFlat
        {
            BgColor = new Color("18232ef5"), BorderColor = new Color("65776a"),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6
        };
        Resized += Invalidate;
        Invalidate();
    }

    public void SetTeam(uint team) { Team = team; Invalidate(); }
    public void Reset() { Team = 0; Invalidate(); }
    public void Invalidate() => QueueRedraw();

    public override void _GuiInput(InputEvent @event)
    {
        // 표시용 UI 위의 클릭/휠이 뒤쪽 전장으로 전달되지 않도록 합니다.
        if (@event is InputEventMouse) AcceptEvent();
    }

    public override void _Draw()
    {
        if (_frame == null) return;
        DrawStyleBox(_frame, new Rect2(Vector2.Zero, Size));
        Font font = GetThemeDefaultFont();
        DrawString(font, new Vector2(Padding, 22), "전장", fontSize: 14, modulate: new Color("f2e8cb"));
        DrawCircle(new Vector2(Size.X - 101, 17), 2.5f, AllyColor, antialiased: true);
        DrawString(font, new Vector2(Size.X - 93, 21), "아군", fontSize: 11, modulate: new Color("c3ccc6"));
        DrawCircle(new Vector2(Size.X - 48, 17), 2.5f, EnemyColor, antialiased: true);
        DrawString(font, new Vector2(Size.X - 40, 21), "적군", fontSize: 11, modulate: new Color("c3ccc6"));

        Rect2 mapRect = MapRect;
        if (mapRect.Size == Vector2.Zero) return;
        RefreshTerrain();
        if (_terrain == null) return;
        DrawTextureRect(_terrain, mapRect, false);
        DrawRect(mapRect.Grow(1), new Color("0d171b"), false, 1);
        if (Team == 0 || !Map.IsSynchronized) return;

        // 건물은 서버가 공개한 목록을 그대로 표시합니다. 유닛의 적 시야 여부도 UNIT/HIDE가 결정합니다.
        if (GodotObject.IsInstanceValid(Buildings))
            foreach (Building building in Buildings.LiveBuildings)
            {
                if (!GodotObject.IsInstanceValid(building) || building.IsQueuedForDeletion() ||
                    !TryWorldToMap(building.GlobalPosition, out Vector2 point)) continue;
                float side = building.BuildingType == 0 ? 8 : 6;
                point = KeepMarkerInside(point, side * .5f + 1, mapRect);
                var rect = new Rect2(point - Vector2.One * side * .5f, Vector2.One * side);
                DrawRect(rect.Grow(1), MarkerOutline);
                DrawRect(rect, building.SideId == Team ? AllyColor : EnemyColor);
            }
        if (GodotObject.IsInstanceValid(Units))
            foreach (Unit unit in Units.LiveUnits)
            {
                if (!GodotObject.IsInstanceValid(unit) || unit.IsDying || unit.IsQueuedForDeletion() ||
                    !TryWorldToMap(unit.GlobalPosition, out Vector2 point)) continue;
                point = KeepMarkerInside(point, UnitRadius + 1, mapRect);
                DrawCircle(point, UnitRadius + .8f, MarkerOutline, antialiased: true);
                DrawCircle(point, UnitRadius, unit.Team == Team ? AllyColor : EnemyColor, antialiased: true);
            }
    }

    public bool TryWorldToMap(Vector3 world, out Vector2 local)
    {
        local = default;
        Rect2 rect = MapRect;
        if (rect.Size == Vector2.Zero || !float.IsFinite(world.X) || !float.IsFinite(world.Z)) return false;
        Vector2 extent = new Vector2(Map.GridSize.X, Map.GridSize.Y) * Map.CellSize;
        Vector2 uv = (new Vector2(world.X, world.Z) - Map.GridOrigin) / extent;
        if (uv.X < 0 || uv.X > 1 || uv.Y < 0 || uv.Y > 1) return false;
        local = rect.Position + uv * rect.Size;
        return true;
    }

    private static Vector2 KeepMarkerInside(Vector2 point, float radius, Rect2 rect)
        => point.Clamp(rect.Position + Vector2.One * radius, rect.End - Vector2.One * radius);

    private void RefreshTerrain()
    {
        if (_terrain != null && _terrainMap == Map && _terrainVersion == Map.OcclusionVersion) return;
        using Image image = Map.CreateMinimapTerrainImage();
        if (image == null) return;
        if (_terrain != null && _terrain.GetWidth() == image.GetWidth() && _terrain.GetHeight() == image.GetHeight())
            _terrain.Update(image);
        else
        {
            _terrain?.Dispose();
            _terrain = ImageTexture.CreateFromImage(image);
        }
        _terrainMap = Map;
        _terrainVersion = Map.OcclusionVersion;
    }

    public override void _ExitTree()
    {
        Resized -= Invalidate;
        _terrain?.Dispose();
        _frame?.Dispose();
    }
}
