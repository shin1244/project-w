using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

// 화면용 안개. 적의 존재 여부는 서버의 UNIT/HIDE만 결정하며 이 마스크로 판단하지 않습니다.
public partial class FogOfWar : MeshInstance3D
{
    [Export] public UnitManager Units;
    [Export] public BuildingManager Buildings;
    public uint Team { get; private set; }
    private readonly Dictionary<uint, float> _unitSight = new();
    private readonly Dictionary<uint, float> _buildingSight = new();
    private Vector2 _origin;
    private Vector2I _size;
    private float _cellSize;
    public const float VisualInsetCells = .75f;
    public const int VisualPixelsPerCell = 8;
    private readonly Dictionary<uint, FogLightMesh> _sources = new();
    private readonly HashSet<uint> _seen = new();
    private readonly List<uint> _stale = new();
    private MapWorld _map;
    private FogOcclusionGrid _occlusion;
    private SubViewport _maskViewport;
    private Node2D _canvas;
    private bool _dirty;
    private double _elapsed;

    public void Configure(MapWorld map)
    {
        Reset();
        if (_maskViewport != null) { RemoveChild(_maskViewport); _maskViewport.QueueFree(); }
        _map = map;
        _origin = map.GridOrigin;
        _size = map.GridSize;
        _cellSize = map.CellSize;
        _occlusion = new FogOcclusionGrid(_origin, _cellSize, _size);
        _maskViewport = new SubViewport
        {
            Name = "SmoothVisionMask", Size = _size * VisualPixelsPerCell,
            Disable3D = true, TransparentBg = true, GuiDisableInput = true,
            World2D = new World2D(), Msaa2D = Viewport.Msaa.Msaa4X,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Once
        };
        AddChild(_maskViewport);
        _maskViewport.AddChild(new ColorRect
        {
            Size = new Vector2(_maskViewport.Size.X, _maskViewport.Size.Y),
            Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore
        });
        _canvas = new Node2D();
        _maskViewport.AddChild(_canvas);
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://maps/FogOfWar.gdshader"), RenderPriority = 120 };
        material.SetShaderParameter("visibility_mask", _maskViewport.GetTexture());
        material.SetShaderParameter("map_origin", _origin);
        material.SetShaderParameter("map_extent", new Vector2(_size.X, _size.Y) * _cellSize);
        MaterialOverride = material;
        Mesh = new QuadMesh { Size = Vector2.One * 2 };
        CastShadow = ShadowCastingSetting.Off;
        Position = new Vector3(0, 0, -1); // Camera3D의 자식으로 두어 카메라 이동 시에도 화면을 덮습니다.
        ExtraCullMargin = 16384;
        Reset();
    }

    public void SetTeam(uint team)
    {
        Team = team;
        Invalidate();
    }

    // SIGHT UNIT|BUILDING type radius: 접속 시 서버 정의를 받아 하드코딩을 피합니다.
    public void HandleSight(string[] parts)
    {
        if (parts.Length != 4 || !uint.TryParse(parts[2], out uint type) ||
            !float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float radius) ||
            !float.IsFinite(radius) || radius < 0) return;
        Dictionary<uint, float> definitions = parts[1] switch
        {
            "UNIT" => _unitSight, "BUILDING" => _buildingSight, _ => null
        };
        if (definitions == null) return;
        definitions[type] = radius;
        Invalidate();
    }

    public void Invalidate() => _dirty = true;

    public void Reset()
    {
        Team = 0;
        _unitSight.Clear();
        _buildingSight.Clear();
        foreach (FogLightMesh source in _sources.Values) source.Remove();
        _sources.Clear();
        if (_maskViewport != null) _maskViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
        _dirty = false;
        _elapsed = 0;
    }

    public override void _Process(double delta)
    {
        _elapsed += delta;
        if (!_dirty || _elapsed < .05) return;
        RefreshVision(); // 메시지를 모아서 최대 20Hz로 갱신합니다.
    }

    public void RefreshVision()
    {
        _elapsed = 0;
        _dirty = false;
        if (_occlusion == null) return;
        _occlusion.Refresh(_map, Buildings);
        _seen.Clear();
        bool changed = false;
        if (Team != 0)
        {
            if (GodotObject.IsInstanceValid(Units))
                foreach (Unit unit in Units.LiveUnits)
                    if (unit.Team == Team && _unitSight.TryGetValue(unit.UnitType, out float sight))
                        changed |= UpdateSource(unit.UnitId, unit.GlobalPosition, sight, 0);
            if (GodotObject.IsInstanceValid(Buildings))
                foreach (Building building in Buildings.LiveBuildings)
                    if (building.SideId == Team && _buildingSight.TryGetValue(building.BuildingType, out float sight))
                        changed |= UpdateSource(building.BuildingId, building.GlobalPosition, sight, _occlusion.BuildingToken(building.BuildingId));
        }
        _stale.Clear();
        foreach (uint id in _sources.Keys)
            if (!_seen.Contains(id)) _stale.Add(id);
        foreach (uint id in _stale) { _sources[id].Remove(); _sources.Remove(id); changed = true; }
        if (changed) _maskViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
    }

    private bool UpdateSource(uint id, Vector3 position, float serverRadius, int ignore)
    {
        _seen.Add(id);
        if (!_sources.TryGetValue(id, out FogLightMesh source))
        {
            source = new FogLightMesh(_canvas);
            _sources.Add(id, source);
        }
        float radius = Math.Max(0, serverRadius - VisualInsetCells * _cellSize);
        return source.Update(new Vector2(position.X, position.Z), radius, ignore, _occlusion,
            _origin, VisualPixelsPerCell / _cellSize, _cellSize);
    }

    public bool IsVisibleAt(Vector3 position)
    {
        var point = new Vector2(position.X, position.Z);
        foreach (FogLightMesh source in _sources.Values)
            if (source.Contains(point)) return true;
        return false;
    }

    public override void _ExitTree()
    {
        foreach (FogLightMesh source in _sources.Values) source.Remove();
        _sources.Clear();
    }
}
