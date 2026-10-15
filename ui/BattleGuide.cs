using Godot;

// 서버가 공개한 양 팀 레벨과 경기 시간을 간결하게 표시한다.
public partial class BattleGuide : PanelContainer
{
    private Label _heading;
    private HBoxContainer _content;
    private readonly VBoxContainer[] _teamPanels = new VBoxContainer[2];
    private readonly Label[] _experienceLabels = new Label[2];
    private readonly ProgressBar[] _experienceBars = new ProgressBar[2];
    private readonly StyleBoxFlat[] _experienceFills = new StyleBoxFlat[2];
    private readonly TeamExperienceSnapshot?[] _experience = new TeamExperienceSnapshot?[2];
    private readonly int?[] _levels = new int?[2];
    private PlayerRole _role;
    private uint _team, _tick;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.CenterTop);
        OffsetLeft = -166; OffsetRight = 166; OffsetTop = 12; OffsetBottom = 58;
        AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("14222de6"), BorderColor = new Color("526857"),
            BorderWidthBottom = 1, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
            ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 5, ContentMarginBottom = 6
        });
        _content = new HBoxContainer { Name = "Content", MouseFilter = MouseFilterEnum.Ignore };
        _content.AddThemeConstantOverride("separation", 16);
        AddChild(_content);
        for (int i = 0; i < _teamPanels.Length; i++)
        {
            var panel = new VBoxContainer
            {
                Name = $"Team{i + 1}", CustomMinimumSize = new Vector2(88, 34),
                SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore
            };
            panel.AddThemeConstantOverride("separation", 3);
            _content.AddChild(panel);
            _teamPanels[i] = panel;
            _experienceLabels[i] = Line(panel, $"Team{i + 1}Experience", 20, "ffffff");
            _experienceLabels[i].MouseFilter = MouseFilterEnum.Pass;
            var bar = new ProgressBar
            {
                Name = $"Team{i + 1}ExperienceProgress", MouseFilter = MouseFilterEnum.Pass,
                CustomMinimumSize = new Vector2(0, 3), MaxValue = 1, Step = .001, ShowPercentage = false
            };
            _experienceFills[i] = new StyleBoxFlat { BgColor = Colors.White };
            bar.AddThemeStyleboxOverride("fill", _experienceFills[i]);
            bar.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color("0d161d") });
            panel.AddChild(bar);
            _experienceBars[i] = bar;
        }
        _heading = Line(_content, "Heading", 19, "e4eadf");
        _heading.CustomMinimumSize = new Vector2(80, 0);
        _heading.VerticalAlignment = VerticalAlignment.Center;
        Refresh();
    }

    public void SetRole(PlayerRole role, uint team)
    {
        if (_role != role || _team != team || role == PlayerRole.None)
        {
            _tick = 0;
            System.Array.Clear(_experience);
            System.Array.Clear(_levels);
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
        _levels[experience.Team - 1] = experience.Level;
        Refresh();
    }

    public void ApplyTeamLevel(TeamLevelSnapshot snapshot)
    {
        if (snapshot.Team is not (1 or 2) || snapshot.Level <= 0 ||
            _role is not (PlayerRole.Hero or PlayerRole.Commander or PlayerRole.Spectator)) return;
        _levels[snapshot.Team - 1] = snapshot.Level;
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
        int left = _role != PlayerRole.Spectator && _team == 2 ? 1 : 0;
        _content.MoveChild(_teamPanels[left], 0);
        _content.MoveChild(_heading, 1);
        _content.MoveChild(_teamPanels[1 - left], 2);
        for (int i = 0; i < _experienceLabels.Length; i++)
        {
            Label label = _experienceLabels[i];
            ProgressBar bar = _experienceBars[i];
            Color color = _role == PlayerRole.Spectator
                ? new Color(i == 0 ? "80bce7" : "efae77")
                : i + 1 == _team ? Minimap.AllyColor : Minimap.EnemyColor;
            label.AddThemeColorOverride("font_color", color);
            _experienceFills[i].BgColor = color;
            label.Text = _levels[i] is { } level ? $"Lv.{level}" : "Lv.—";
            label.TooltipText = _role == PlayerRole.Spectator ? $"{i + 1}팀"
                : $"{(i + 1 == _team ? "우리 팀" : "상대 팀")} · {i + 1}팀";
            bar.Visible = _experience[i].HasValue;
            if (_experience[i] is { } progress)
            {
                label.TooltipText += $"\n{(progress.IsMaxLevel ? "MAX" : $"경험치 {progress.Current} / {progress.Required}")}\n누적 경험치 {progress.TotalExperienceText}";
                bar.Value = progress.IsMaxLevel ? 1 : (double)progress.Current / progress.Required;
            }
            else
            {
                bar.Value = 0;
            }
            bar.TooltipText = label.TooltipText;
        }
        uint seconds = _tick / InterpolationClock.TickRate;
        _heading.Text = $"{seconds / 60:00}:{seconds % 60:00}";
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
