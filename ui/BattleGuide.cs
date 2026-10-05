using Godot;

// 공개된 경기 정보와 내 역할만 안내한다. 안개 밖의 전투 정보는 추측하지 않는다.
public partial class BattleGuide : PanelContainer
{
    public HeroSkillPanel Skills { get; set; }
    private Label _heading, _objective, _help;
    private PlayerRole _role;
    private uint _team, _tick;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.CenterTop);
        OffsetLeft = -250; OffsetRight = 250; OffsetTop = 12; OffsetBottom = 91;
        AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("14222deb"), BorderColor = new Color("526857"),
            BorderWidthBottom = 2, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
            ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 8, ContentMarginBottom = 8
        });
        var content = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        content.AddThemeConstantOverride("separation", 3);
        AddChild(content);
        _heading = Line(content, "Heading", 14, "c6dfad");
        _objective = Line(content, "Objective", 16, "f0ebd7");
        _help = Line(content, "Help", 12, "acbec4");
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

    public override void _Process(double delta) => Refresh();

    private void Refresh()
    {
        if (_heading == null) return;
        Visible = _role is PlayerRole.Hero or PlayerRole.Commander;
        if (!Visible) return;
        uint seconds = _tick / InterpolationClock.TickRate;
        _heading.Text = $"{_team}팀  ·  {(_role == PlayerRole.Hero ? "영웅" : "지휘관")}     {seconds / 60:00}:{seconds % 60:00}";
        _objective.Text = "승리 목표  ·  적 본진 회관 파괴";
        bool respawning = _role == PlayerRole.Hero && Skills?.IsRespawning == true;
        _help.Text = respawning ? Skills.StatusText : _role == PlayerRole.Hero
            ? "우클릭 이동·공격  ·  Space 내 영웅  ·  휠 확대/축소"
            : "드래그 선택  ·  나무 우클릭 채집  ·  Z 건설  ·  Space 본진";
        _help.Modulate = respawning ? new Color("ffc393") : Colors.White;
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
