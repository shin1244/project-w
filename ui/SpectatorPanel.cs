using Godot;
using System.Linq;

// Camera-only controls: never sends gameplay commands or changes either team's vision.
public partial class SpectatorPanel : PanelContainer
{
    public UnitManager Units { get; set; }
    public MapWorld Map { get; set; }
    public string AIName { get; set; } = "AI";
    public bool Following => _follow?.ButtonPressed == true;
    private Button _follow;
    private Label _counts, _target, _heading;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight);
        OffsetLeft = -292; OffsetRight = -12; OffsetTop = 58; OffsetBottom = 228;
        MouseFilter = MouseFilterEnum.Stop;
        AddThemeStyleboxOverride("panel", new StyleBoxFlat {
            BgColor = new Color("14222df2"), BorderColor = new Color("65776a"),
            BorderWidthBottom = 1, CornerRadiusTopLeft = 8, CornerRadiusBottomLeft = 8,
            ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 10, ContentMarginBottom = 10
        });
        var content = new VBoxContainer { Name = "Content" };
        content.AddThemeConstantOverride("separation", 8);
        AddChild(content);
        _heading = new Label { Text = "전체 맵 관전", HorizontalAlignment = HorizontalAlignment.Center };
        content.AddChild(_heading);
        _counts = new Label { Name = "Counts", HorizontalAlignment = HorizontalAlignment.Center };
        content.AddChild(_counts);
        var cameras = new HBoxContainer();
        content.AddChild(cameras);
        foreach (uint team in new uint[] { 1, 0, 2 })
        {
            uint chosen = team;
            var button = new Button { Name = $"Focus{team}", Text = team == 0 ? "중앙" : $"{team}팀 본진", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            button.Pressed += () => FocusTeam(chosen);
            cameras.AddChild(button);
        }
        _follow = new Button { Name = "Follow", Text = "선택 유닛 따라가기", ToggleMode = true };
        _follow.Toggled += enabled => { if (enabled && !Units.CanInspect(Units.InspectedUnit)) StopFollowing(); };
        content.AddChild(_follow);
        _target = new Label { Name = "Target", HorizontalAlignment = HorizontalAlignment.Center };
        _target.AddThemeFontSizeOverride("font_size", 12);
        content.AddChild(_target);
        var help = new Label { Text = "방향키 / 휠 / 가운데 버튼 드래그\n미니맵 클릭 · 유닛 클릭으로 정보 확인", HorizontalAlignment = HorizontalAlignment.Center };
        help.AddThemeFontSizeOverride("font_size", 12);
        content.AddChild(help);
    }

    public void StopFollowing() => _follow?.SetPressedNoSignal(false);

    public void FocusTeam(uint team)
    {
        if (!Visible || !Map.IsSynchronized) return;
        StopFollowing();
        Vector3 target;
        if (team == 0)
        {
            Vector2 center = Map.GridOrigin + new Vector2(Map.GridSize.X, Map.GridSize.Y) * Map.CellSize * .5f;
            target = new Vector3(center.X, 0, center.Y);
        }
        else if (!Map.TryGetBasePosition(team, out target)) return;
        CameraNavigation.FocusGround(Units.Camera, target);
    }

    public override void _Process(double delta)
    {
        if (!Visible || Units == null) return;
        _heading.Text = $"{AIName} · 전체 맵 관전";
        int Count(uint team) => Units.LiveUnits.Count(u => u.Team == team && !u.IsDying);
        _counts.Text = $"1팀 {Count(1)}기     ·     2팀 {Count(2)}기";
        Unit selected = Units.InspectedUnit;
        bool inspect = Units.CanInspect(selected);
        _target.Text = inspect ? $"{(selected.Team == 0 ? "중립" : $"{selected.Team}팀")} · {UnitCatalog.Name(selected.UnitType)}" : "전장의 유닛을 선택하세요";
        if (!inspect) StopFollowing();
        else if (Following) CameraNavigation.FocusGround(Units.Camera, selected.GlobalPosition);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (!Visible) return;
        if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Middle } ||
            e is InputEventKey { Pressed: true, PhysicalKeycode: Key.Up or Key.Down or Key.Left or Key.Right or Key.Q or Key.W or Key.S or Key.D or Key.Home }) StopFollowing();
    }
}
