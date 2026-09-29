using Godot;
using System;

// 지형은 변경될 때만 다시 만들고, 표식은 서버가 알려준 현재 목록에서 그립니다.
public partial class Minimap : Control
{
    [Export] public MapWorld Map;
    [Export] public UnitManager Units;
    [Export] public BuildingManager Buildings;
    public event Action<Vector3> CameraMoveRequested;
    public event Action<Vector3> MoveRequested;

    public uint Team { get; private set; }
    public static readonly Color AllyColor = new("63e88a");
    public static readonly Color EnemyColor = new("ff635f");
    private static readonly Color MarkerOutline = new("101b1a");
    private const float Padding = 4;
    private const float UnitRadius = 2.3f;
    private ImageTexture _terrain;
    private MapWorld _terrainMap;
    private uint _terrainVersion;
    private StyleBoxFlat _frame;
    private bool _dragging;
    private Transform3D _cameraTransform;
    private float _cameraSize;
    private Rect2 _screenRect;
    private Camera3D Camera => GodotObject.IsInstanceValid(Units) ? Units.Camera : null;

    // 맵의 실제 비율을 유지해 가로/세로 크기가 달라도 원과 사각형이 왜곡되지 않습니다.
    public Rect2 MapRect
    {
        get
        {
            if (!GodotObject.IsInstanceValid(Map) || !Map.HasMap) return default;
            Vector2 available = Size - Vector2.One * Padding * 2;
            if (available.X <= 0 || available.Y <= 0) return default;
            Vector2 grid = new(Map.GridSize.X, Map.GridSize.Y);
            Vector2 fitted = grid * Mathf.Min(available.X / grid.X, available.Y / grid.Y);
            return new Rect2(Vector2.One * Padding + (available - fitted) * .5f, fitted);
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
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusTopRight = 6
        };
        Resized += Invalidate;
        GetWindow().FocusExited += CancelDrag;
        Invalidate();
    }

    public void SetTeam(uint team) { Team = team; Invalidate(); }
    public void Reset() { Team = 0; CancelDrag(); Invalidate(); }
    public void Invalidate() => QueueRedraw();

    public override void _Process(double delta)
    {
        Camera3D camera = Camera;
        if (!GodotObject.IsInstanceValid(camera)) return;
        Rect2 screen = GetViewport().GetVisibleRect();
        if (_cameraTransform == camera.GlobalTransform && _cameraSize == camera.Size && _screenRect == screen) return;
        _cameraTransform = camera.GlobalTransform;
        _cameraSize = camera.Size;
        _screenRect = screen;
        Invalidate();
    }

    // 미니맵 밖에서 놓아도 드래그가 끝나며, 전장 선택/명령으로 이어지지 않습니다.
    public override void _Input(InputEvent @event)
    {
        if (!_dragging) return;
        if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape } ||
            @event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right })
            CancelDrag();
        else if (@event is InputEventMouseMotion motion)
            Pan(GetGlobalTransformWithCanvas().AffineInverse() * motion.Position, true);
        else if (@event is InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Left } release)
        {
            Pan(GetGlobalTransformWithCanvas().AffineInverse() * release.Position, true);
            CancelDrag();
        }
        else return;
        GetViewport().SetInputAsHandled();
    }

    private void CancelDrag() => _dragging = false;

    private bool Pan(Vector2 local, bool clamp)
    {
        if (!TryMapToWorld(local, out Vector3 world, clamp)) return false;
        CameraMoveRequested?.Invoke(world);
        return true;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true } button)
        {
            if (button.ButtonIndex == MouseButton.Left) _dragging = Pan(button.Position, false);
            else if (button.ButtonIndex == MouseButton.Right && Team != 0 &&
                GodotObject.IsInstanceValid(Map) && Map.IsSynchronized &&
                TryMapToWorld(button.Position, out Vector3 world)) MoveRequested?.Invoke(world);
        }
        // 미니맵에서 처리한 입력과 여백/휠 입력 모두 전장으로 전달하지 않습니다.
        if (@event is InputEventMouse) AcceptEvent();
    }

    public override void _Draw()
    {
        if (_frame == null) return;
        DrawStyleBox(_frame, new Rect2(Vector2.Zero, Size));
        Rect2 mapRect = MapRect;
        if (mapRect.Size == Vector2.Zero) return;
        RefreshTerrain();
        if (_terrain == null) return;
        DrawTextureRect(_terrain, mapRect, false);
        DrawRect(mapRect.Grow(1), new Color("0d171b"), false, 1);
        Rect2 cameraRect = CameraRect;
        if (cameraRect.HasArea())
        {
            DrawRect(cameraRect, new Color("101b1acc"), false, 3);
            DrawRect(cameraRect, new Color("e4ede4"), false, 1);
        }
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

    public bool TryMapToWorld(Vector2 local, out Vector3 world, bool clamp = false)
    {
        world = default;
        Rect2 rect = MapRect;
        if (!rect.HasArea() || !float.IsFinite(local.X) || !float.IsFinite(local.Y) ||
            (!clamp && !rect.HasPoint(local))) return false;
        Vector2 uv = ((local - rect.Position) / rect.Size).Clamp(Vector2.Zero, Vector2.One);
        Vector2 point = Map.GridOrigin + uv * new Vector2(Map.GridSize.X, Map.GridSize.Y) * Map.CellSize;
        world = new Vector3(point.X, 0, point.Y);
        return true;
    }

    // 현재 카메라는 축에 정렬된 직교 투영입니다. 네 모서리의 지면 범위를 맵 안에서 잘라 표시합니다.
    public Rect2 CameraRect
    {
        get
        {
            Rect2 mapRect = MapRect;
            if (!mapRect.HasArea() || !GodotObject.IsInstanceValid(Camera)) return default;
            Rect2 screen = GetViewport().GetVisibleRect();
            Vector2 extent = new Vector2(Map.GridSize.X, Map.GridSize.Y) * Map.CellSize;
            Vector2 minimum = new(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maximum = new(float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < 4; i++)
            {
                Vector2 corner = new(i % 2 == 0 ? screen.Position.X : screen.End.X, i < 2 ? screen.Position.Y : screen.End.Y);
                if (!CameraNavigation.TryGroundPoint(Camera, corner, out Vector3 world)) return default;
                Vector2 mapped = mapRect.Position + (new Vector2(world.X, world.Z) - Map.GridOrigin) / extent * mapRect.Size;
                minimum = minimum.Min(mapped);
                maximum = maximum.Max(mapped);
            }
            return new Rect2(minimum, maximum - minimum).Intersection(mapRect);
        }
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
        GetWindow().FocusExited -= CancelDrag;
        _terrain?.Dispose();
        _frame?.Dispose();
    }
}
