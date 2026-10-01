using Godot;

// 건물 미리보기는 미리 저장한 투명 이미지를 사용하며 실제 Building·충돌·체력·시야에는 등록하지 않습니다.
public partial class BuildingPlacement : Node3D
{
    [Export] public MapWorld Map;
    [Export] public UnitManager Units;
    [Export] public BuildingManager Buildings;
    [Export] public Camera3D Camera;
    [Export] public Label Status;
    public bool Active { get; private set; }
    public bool CanPlace { get; private set; }
    public string BlockReason { get; private set; }
    public Vector3 PlacementPosition { get; private set; }
    private PlacementPreviewImage _preview;
    private Vector2 _footprint;
    private uint _type, _worker;
    private double _noticeTime;
    private static readonly Plane Ground = new(Vector3.Up, 0);

    public override void _Ready()
    {
        Units.SelectionChanged += ValidateWorker;
        var canvas = new CanvasLayer { Name = "PreviewCanvas", Layer = 0 };
        AddChild(canvas);
        _preview = new PlacementPreviewImage { Name = "BuildPreview" };
        canvas.AddChild(_preview);
        if (Status != null)
        {
            Status.MouseFilter = Control.MouseFilterEnum.Ignore;
            Status.AddThemeConstantOverride("outline_size", 5);
            Status.AddThemeColorOverride("font_outline_color", new Color("101820"));
            Status.Hide();
        }
    }

    public bool Begin(uint type)
    {
        Cancel();
        if (!Map.IsSynchronized || !Units.TryGetSelectedWorker(out Unit worker))
        {
            Notice("건설할 내 일꾼을 선택하세요.", true);
            return false;
        }
        if (!BuildingCatalog.IsPlayerBuildable(type) || !_preview.Select(type, out _footprint)) return false;
        if (Buildings.BuildingRequirementBlockReason(type) is string reason)
        {
            Notice(reason, true);
            return false;
        }
        Active = true;
        _type = type;
        _worker = worker.UnitId;
        UpdatePreview(GetViewport().GetMousePosition());
        return true;
    }

    public override void _Process(double delta)
    {
        if (Active)
        {
            if (!Map.IsSynchronized || !WorkerAvailable()) { Cancel(); return; }
            UpdatePreview(GetViewport().GetMousePosition()); // 카메라 이동/줌·새 장애물도 반영합니다.
        }
        else if (_noticeTime > 0)
        {
            _noticeTime -= delta;
            if (_noticeTime <= 0) Status?.Hide();
        }
    }

    public void UpdatePreview(Vector2 screenPosition)
    {
        if (!Active) return;
        CanPlace = false;
        if (!GodotObject.IsInstanceValid(Camera) || !GetViewport().GetVisibleRect().HasPoint(screenPosition) ||
            GetViewport().GuiGetHoveredControl() != null ||
            Ground.IntersectsRay(Camera.ProjectRayOrigin(screenPosition), Camera.ProjectRayNormal(screenPosition)) is not Vector3 point)
        {
            _preview.Hide();
            BlockReason = "전장에서 건설 위치를 선택하세요.";
            SetStatus(BlockReason, false);
            return;
        }
        PlacementPosition = PlacementRules.Snap(Map, point, _footprint);
        BlockReason = Buildings.BuildingRequirementBlockReason(_type) ??
            PlacementRules.Check(Map, Buildings, Units, PlacementPosition, _footprint);
        CanPlace = BlockReason == null;
        _preview.Update(Camera, PlacementPosition, CanPlace);
        SetStatus(CanPlace ? $"{BuildingName} 배치 · 좌클릭: 건설 · 우클릭/Esc: 취소" : $"건설 불가: {BlockReason}", !CanPlace);
    }

    public bool TryPlace(Vector2 screenPosition)
    {
        if (!Active) return false;
        if (!Map.IsSynchronized || !WorkerAvailable()) { Cancel(); return false; }
        UpdatePreview(screenPosition); // 클릭 순간의 장애물/위치로 다시 검사합니다.
        if (!CanPlace) return false;
        if (!Units.RequestBuild(_type, PlacementPosition, _worker)) { Cancel(); return false; }
        string name = BuildingName;
        Cancel();
        Notice($"{name} 건설을 요청했습니다.", false);
        return true;
    }

    private string BuildingName => BuildingCatalog.Name(_type);
    private bool WorkerAvailable()
    {
        if (!Units.TryGetUnit(_worker, out Unit worker) || worker.UnitType != 0 || !Units.CanControl(worker)) return false;
        foreach (uint id in Units.SelectedUnitIds) if (id == _worker) return true;
        return false;
    }

    private void ValidateWorker() { if (Active && !WorkerAvailable()) Cancel(); }

    public void Cancel()
    {
        Active = false;
        _preview?.Hide();
        CanPlace = false;
        BlockReason = null;
        _noticeTime = 0;
        Status?.Hide();
    }

    private void SetStatus(string text, bool error)
    {
        if (Status == null) return;
        Status.Text = text;
        Status.Modulate = error ? new Color("ff9382") : new Color("d4f4cf");
        Status.Show();
    }

    public void Notice(string text, bool error)
    {
        SetStatus(text, error);
        _noticeTime = 3;
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(Units)) Units.SelectionChanged -= ValidateWorker;
    }
}
