using Godot;
using System.Collections.Generic;
using System.Globalization;

// AOS 조회 대상은 조종 중인 영웅의 선택 목록과 분리한다.
public partial class UnitInfoPanel : PanelContainer
{
    [Export] public UnitManager Units;
    public Unit InspectedUnit { get; private set; }
    public Unit DisplayedUnit { get; private set; }
    public HeroExperienceSnapshot? Experience { get; private set; }
    private uint? _heroType;
    private uint _playerId, _team;
    private bool _active;
    private readonly List<StyleBoxFlat> _styles = new();
    private SelectionPortraits _portraits;
    private Control _content;
    private Label _empty, _name, _healthText, _manaText, _level, _experienceText;
    private readonly Dictionary<string, StatIcon> _stats = new();
    private TextureRect _portrait;
    private ProgressBar _health, _mana, _experience;
    private ColorRect _shieldFill;
    private Control _manaRow, _experienceRow;
    private StyleBoxFlat _healthFill;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        MouseForcePassScrollEvents = false;
        ClipContents = true;
        Theme = GD.Load<Theme>("res://ui/SkillTooltipTheme.tres");
        var frame = Style("18232ef5", "65776a");
        frame.ContentMarginLeft = frame.ContentMarginRight = 8;
        frame.ContentMarginTop = frame.ContentMarginBottom = 8;
        AddThemeStyleboxOverride("panel", frame);
        _portraits = new SelectionPortraits { Name = "Portraits" };
        AddChild(_portraits);

        var body = new Control { Name = "Body", MouseFilter = MouseFilterEnum.Ignore };
        AddChild(body);
        _empty = Text("Empty", "—", 13, "9db7b4");
        _empty.HorizontalAlignment = HorizontalAlignment.Center;
        _empty.VerticalAlignment = VerticalAlignment.Center;
        body.AddChild(_empty);
        _empty.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var content = new VBoxContainer { Name = "Content", MouseFilter = MouseFilterEnum.Ignore };
        content.AddThemeConstantOverride("separation", 4);
        body.AddChild(content);
        content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _content = content;
        var header = new HBoxContainer { Name = "Header", MouseFilter = MouseFilterEnum.Ignore };
        header.AddThemeConstantOverride("separation", 8);
        content.AddChild(header);
        _portrait = new TextureRect
        {
            Name = "Portrait", CustomMinimumSize = new Vector2(32, 32),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Stop, MouseForcePassScrollEvents = false
        };
        header.AddChild(_portrait);
        _name = Text("UnitName", "", 14, "ece6cf");
        _name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _name.VerticalAlignment = VerticalAlignment.Center;
        _name.MouseFilter = MouseFilterEnum.Stop;
        _level = Text("Level", "", 12, "d5be7c");
        _level.VerticalAlignment = VerticalAlignment.Center;
        _level.TooltipText = Lang.T("팀 레벨");
        _level.MouseFilter = MouseFilterEnum.Stop;
        _level.TextOverrunBehavior = TextServer.OverrunBehavior.NoTrimming;
        header.AddChild(_name);
        header.AddChild(_level);

        (_, _health, _healthText) = Vital(content, "Health", "HealthText", 16, "64cf79");
        _health.GetParent<Control>().TooltipText = Lang.T("체력 · 보호막");
        _shieldFill = HealthDisplay.CreateShieldFill(_health);
        _healthFill = Style("64cf79", "64cf79");
        _health.AddThemeStyleboxOverride("fill", _healthFill);
        (_manaRow, _mana, _manaText) = Vital(content, "Mana", "ManaText", 14, "519bd1");
        _manaRow.TooltipText = Lang.T("마나 · 스킬 사용에 필요한 자원");
        (_experienceRow, _experience, _experienceText) = Vital(content, "Experience", "ExperienceText", 14, "ac945a");
        _experienceRow.TooltipText = Lang.T("팀 경험치 · 다음 팀 레벨까지 필요한 경험치");
        var stats = new GridContainer { Name = "Stats", Columns = 3, MouseFilter = MouseFilterEnum.Ignore };
        stats.AddThemeConstantOverride("h_separation", 4);
        stats.AddThemeConstantOverride("v_separation", 0);
        content.AddChild(stats);
        foreach (string key in new[] { "Damage", "AttackSpeed", "Range", "Sight", "Speed" })
        {
            var cell = new StatIcon { Name = key + "Stat", ValueName = key,
                Kind = System.Enum.Parse<StatIcon.StatKind>(key) };
            stats.AddChild(cell);
            _stats.Add(key, cell);
        }
        Clear();
    }

    public void SetHero(uint? heroType, uint playerId = 0, uint team = 0)
    {
        if (_heroType != heroType || _playerId != playerId || _team != team || !heroType.HasValue)
        {
            Experience = null;
            InspectedUnit = null;
        }
        _heroType = heroType;
        _playerId = playerId;
        _team = team;
        _active = heroType.HasValue && playerId != 0 && team != 0;
        Visible = _active;
        SetProcess(_active);
        Refresh();
    }

    public void ApplyExperience(HeroExperienceSnapshot experience)
    {
        if (!_active) return;
        Experience = experience;
        Refresh();
    }

    public void Inspect(Unit unit)
    {
        if (!_active) return;
        if (!GodotObject.IsInstanceValid(Units) || !Units.CanInspect(unit)) { Clear(); return; }
        InspectedUnit = unit;
        Refresh();
    }

    public void Clear()
    {
        InspectedUnit = null;
        SetProcess(_active);
        Refresh();
    }

    public override void _Process(double delta) => Refresh();

    private void Refresh()
    {
        if (_content == null) return;
        if (!_active || !GodotObject.IsInstanceValid(Units))
        {
            DisplayedUnit = InspectedUnit = null;
            _content.Hide();
            _empty.Show();
            _portrait.Texture = null;
            _name.Text = _healthText.Text = _manaText.Text = _level.Text = _experienceText.Text = "";
            _name.TooltipText = _portrait.TooltipText = "";
            foreach (StatIcon stat in _stats.Values) stat.SetValue("", "");
            _health.Value = _mana.Value = _experience.Value = 0;
            _manaRow.Hide();
            _shieldFill.Hide();
            return;
        }
        if (!Units.CanInspect(InspectedUnit)) InspectedUnit = null;
        // The AOS command selection always contains the owned hero, never the inspection target.
        Unit hero = null;
        foreach (uint id in Units.SelectedUnitIds)
            if (Units.TryGetUnit(id, out Unit candidate) && IsOwnHero(candidate) && Units.CanInspect(candidate))
            { hero = candidate; break; }
        Unit unit = DisplayedUnit = InspectedUnit ?? hero;
        bool own = InspectedUnit == null || IsOwnHero(unit);
        uint type = unit?.UnitType ?? _heroType.Value;
        _empty.Hide();
        _content.Show();
        _name.Text = UnitCatalog.Name(type);
        _name.TooltipText = UnitCatalog.Description(type);
        _portrait.Texture = _portraits.GetPortrait(Units.SceneFor(type), unit != null && unit.Team != Units.LocalTeam);
        string activity = unit == null ? "—" : unit.UnitType == UnitCatalog.SiegeRam ? Lang.T("자동 행군") :
            SelectionDetails.ActivityName(unit.State, unit.HasServerState);
        _portrait.TooltipText = $"{UnitCatalog.Name(type)} · {activity}\n{UnitCatalog.Description(type)}";
        _healthText.Text = unit == null ? "— / —" : SelectionDetails.HealthText(unit.HealthBar);
        float ratio = unit?.HealthBar.Ratio ?? 0;
        HealthDisplay.Apply(_health, _shieldFill, unit?.HealthBar.CurrentHP ?? 0, unit?.HealthBar.MaxHP ?? 0, unit?.HealthBar.Shield ?? 0);
        _healthFill.BgColor = _healthFill.BorderColor = SelectionDetails.HealthColor(ratio);
        _manaRow.Visible = unit?.Mana is { Maximum: > 0 };
        _manaText.Text = unit?.Mana is { } mana ? $"{mana.Current} / {mana.Maximum}" : "— / —";
        _mana.Value = unit?.Mana is { Maximum: > 0 } manaValue ? 100.0 * manaValue.Current / manaValue.Maximum : 0;
        StatsSnapshot? stats = unit?.Stats;
        SetStat("Damage", stats is { } shown ? HealthDisplay.Whole(shown.Damage) : "—", Lang.T("공격력"), Lang.T("기본 공격 한 번의 피해량"));
        SetStat("Range", Number(stats?.Range), Lang.T("공격 사거리"), Lang.T("기본 공격이 닿는 거리"));
        SetStat("Sight", Number(stats?.Sight), Lang.T("시야"), Lang.T("주변을 볼 수 있는 거리"));
        SetStat("Speed", Number(stats?.Speed), Lang.T("이동 속도"), Lang.T("초당 이동 거리"));
        string attackSpeed = stats is { Interval: > 0 } attack
            ? (1.0 / attack.Interval).ToString("0.##", CultureInfo.InvariantCulture) : "—";
        SetStat("AttackSpeed", attackSpeed, Lang.T("공격 속도"), Lang.T("초당 기본 공격 횟수"));
        _level.Visible = _experienceRow.Visible = own;
        _level.Text = own && Experience is { } progress ? $"Lv.{progress.Level}" : "Lv.—";
        _experienceText.Text = own && Experience is { } exp
            ? exp.IsMaxLevel ? "MAX" : $"{exp.Current} / {exp.Required}" : "— / —";
        _experience.Value = own && Experience is { } value
            ? value.IsMaxLevel ? 100 : 100.0 * value.Current / value.Required : 0;
    }

    private bool IsOwnHero(Unit unit) => GodotObject.IsInstanceValid(unit) && unit.OwnerId == _playerId &&
        unit.Team == _team && unit.UnitType == _heroType;

    private static string Number(float? value) => value?.ToString("0.##", CultureInfo.InvariantCulture) ?? "—";

    private void SetStat(string key, string value, string title, string description) =>
        _stats[key].SetValue(value, $"{title} {value}\n{description}");

    private (Control, ProgressBar, Label) Vital(VBoxContainer parent, string name, string textName, int height, string color)
    {
        var row = new Control { Name = name + "Row", CustomMinimumSize = new Vector2(0, height),
            MouseFilter = MouseFilterEnum.Stop, MouseForcePassScrollEvents = false };
        parent.AddChild(row);
        var bar = new ProgressBar { Name = name, Step = 0, ShowPercentage = false, MouseFilter = MouseFilterEnum.Ignore };
        bar.AddThemeStyleboxOverride("background", Style("101c26", "36424a"));
        bar.AddThemeStyleboxOverride("fill", Style(color, color));
        row.AddChild(bar);
        bar.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var label = Text(textName, "", height <= 14 ? 10 : 11, "f2f4ee");
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.AddThemeColorOverride("font_outline_color", new Color("101820"));
        label.AddThemeConstantOverride("outline_size", 2);
        row.AddChild(label);
        label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        return (row, bar, label);
    }

    private static Label Text(string name, string value, int size, string color)
    {
        var label = new Label { Name = name, Text = value, MouseFilter = MouseFilterEnum.Ignore,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", new Color(color));
        return label;
    }

    private StyleBoxFlat Style(string background, string border)
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(background), BorderColor = new Color(border),
            BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6
        };
        _styles.Add(style);
        return style;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouse) AcceptEvent();
    }

    public override void _ExitTree()
    {
        foreach (StyleBoxFlat style in _styles) style.Dispose();
        _styles.Clear();
    }
}
