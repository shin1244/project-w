using Godot;

// 진영·역할·경기 시간과 팀 공통 성장을 표시한다. 관전자는 양 팀을 비교한다.
public partial class BattleGuide : PanelContainer
{
    private Label _heading;
    private readonly Label[] _experienceLabels = new Label[2];
    private readonly TeamExperienceSnapshot?[] _experience = new TeamExperienceSnapshot?[2];
    private PlayerRole _role;
    private uint _team, _tick;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.CenterTop);
        OffsetLeft = -200; OffsetRight = 200; OffsetTop = 12; OffsetBottom = 38;
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
        for (int i = 0; i < _experienceLabels.Length; i++)
            _experienceLabels[i] = Line(content, $"Team{i + 1}Experience", 11, i == 0 ? "a6d9ef" : "efbdab");
        Refresh();
    }

    public void SetRole(PlayerRole role, uint team)
    {
        if (_role != role || _team != team || role == PlayerRole.None)
        {
            _tick = 0;
            System.Array.Clear(_experience);
        }
        _role = role; _team = team;
        Refresh();
    }

    public void ApplyTeamExperience(TeamExperienceSnapshot experience)
    {
        if (experience.Team is not (1 or 2) ||
            !(_role == PlayerRole.Spectator ||
              _role is PlayerRole.Hero or PlayerRole.Commander && experience.Team == _team)) return;
        _experience[experience.Team - 1] = experience;
        Refresh();
    }

    public void HandleTick(string[] parts)
    {
        if (parts.Length == 2 && uint.TryParse(parts[1], out uint tick)) { _tick = tick; Refresh(); }
    }

    private void Refresh()
    {
        if (_heading == null) return;
        Visible = _role is PlayerRole.Hero or PlayerRole.Commander or PlayerRole.Spectator;
        int rows = 0;
        for (int i = 0; i < _experienceLabels.Length; i++)
        {
            Label label = _experienceLabels[i];
            bool show = Visible && (_role == PlayerRole.Spectator || _team == i + 1);
            label.Visible = show;
            label.Text = _experience[i] is { } progress
                ? $"{i + 1}팀 공통 Lv.{progress.Level} · {(progress.IsMaxLevel ? "MAX" : $"XP {progress.Current} / {progress.Required}")} · 누적 {progress.TotalExperienceText}"
                : $"{i + 1}팀 공통 Lv.— · XP —";
            if (show) rows++;
        }
        OffsetBottom = 38 + rows * 20;
        if (!Visible) return;
        uint seconds = _tick / InterpolationClock.TickRate;
        _heading.Text = (_role == PlayerRole.Spectator ? "관전 · 영웅 3 vs 3 · 지휘관 각 1명" : $"{_team}팀  ·  {(_role == PlayerRole.Hero ? "영웅" : "지휘관")}") + $"     {seconds / 60:00}:{seconds % 60:00}";
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
