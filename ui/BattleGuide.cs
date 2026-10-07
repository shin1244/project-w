using Godot;

// 전장을 가리지 않도록 진영·역할·경기 시간만 한 줄로 표시한다.
public partial class BattleGuide : PanelContainer
{
    private Label _heading;
    private PlayerRole _role;
    private uint _team, _tick;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.CenterTop);
        OffsetLeft = -115; OffsetRight = 115; OffsetTop = 12; OffsetBottom = 38;
        AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("14222dbf"), BorderColor = new Color("526857"),
            BorderWidthBottom = 1, CornerRadiusBottomLeft = 5, CornerRadiusBottomRight = 5,
            ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 4, ContentMarginBottom = 4
        });
        var content = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        content.AddThemeConstantOverride("separation", 3);
        AddChild(content);
        _heading = Line(content, "Heading", 12, "c6dfad");
        Refresh();
    }

    public void SetRole(PlayerRole role, uint team)
    {
        if (_role != role || _team != team) _tick = 0;
        _role = role; _team = team;
        Refresh();
    }

    public void HandleTick(string[] parts)
    {
        if (parts.Length == 2 && uint.TryParse(parts[1], out uint tick)) { _tick = tick; Refresh(); }
    }

    private void Refresh()
    {
        if (_heading == null) return;
        Visible = _role is PlayerRole.Hero or PlayerRole.Commander;
        if (!Visible) return;
        uint seconds = _tick / InterpolationClock.TickRate;
        _heading.Text = $"{_team}팀  ·  {(_role == PlayerRole.Hero ? "영웅" : "지휘관")}     {seconds / 60:00}:{seconds % 60:00}";
    }

    private static Label Line(Node parent, string name, int size, string color)
    {
        var label = new Label { Name = name, HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", new Color(color));
        parent.AddChild(label);
        return label;
    }
}
