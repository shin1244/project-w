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
    private Label _empty, _name, _activity, _healthText, _level, _experienceText;
    private readonly Dictionary<string, Label> _stats = new();
    private TextureRect _portrait;
    private ProgressBar _health, _experience;
    private ColorRect _shieldFill;
    private Control _experienceRow;
    private StyleBoxFlat _healthFill;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        MouseForcePassScrollEvents = false;
        ClipContents = true;
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
            Name = "Portrait", CustomMinimumSize = new Vector2(40, 40),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore
        };
        header.AddChild(_portrait);
        var title = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        title.AddThemeConstantOverride("separation", 2);
        header.AddChild(title);
        _name = Text("UnitName", "", 14, "ece6cf");
        _activity = Text("Activity", "", 11, "a8b9b1");
        _activity.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _level = Text("Level", "", 12, "d5be7c");
        _level.TextOverrunBehavior = TextServer.OverrunBehavior.NoTrimming;
        title.AddChild(_name);
        var status = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        title.AddChild(status);
        status.AddChild(_activity);
        status.AddChild(_level);

        (_, _health, _healthText) = Vital(content, "Health", "HealthText", 16, "64cf79");
        _shieldFill = HealthDisplay.CreateShieldFill(_health);
        _healthFill = Style("64cf79", "64cf79");
        _health.AddThemeStyleboxOverride("fill", _healthFill);
        (_experienceRow, _experience, _experienceText) = Vital(content, "Experience", "ExperienceText", 14, "ac945a");
        var stats = new GridContainer { Name = "Stats", Columns = 2, MouseFilter = MouseFilterEnum.Ignore };
        stats.AddThemeConstantOverride("h_separation", 8);
        stats.AddThemeConstantOverride("v_separation", 0);
        content.AddChild(stats);
        foreach (string key in new[] { "Damage", "AttackSpeed", "Range", "Sight", "Speed" })
        {
            var label = Text(key, "", 11, "d0dcd2");
            label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            stats.AddChild(label);
            _stats.Add(key, label);
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
            _name.Text = _activity.Text = _healthText.Text = _level.Text = _experienceText.Text = "";
            foreach (Label label in _stats.Values) label.Text = "";
            _health.Value = _experience.Value = 0;
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
        _activity.Text = unit == null ? "—" : unit.UnitType == UnitCatalog.SiegeRam ? "자동 행군" :
            SelectionDetails.ActivityText(unit.State, unit.HasServerState).Replace("상태   ", "");
        _healthText.Text = unit == null ? "— / —" : SelectionDetails.HealthText(unit.HealthBar);
        float ratio = unit?.HealthBar.Ratio ?? 0;
        HealthDisplay.Apply(_health, _shieldFill, unit?.HealthBar.CurrentHP ?? 0, unit?.HealthBar.MaxHP ?? 0, unit?.HealthBar.Shield ?? 0);
        _healthFill.BgColor = _healthFill.BorderColor = SelectionDetails.HealthColor(ratio);
        StatsSnapshot? stats = unit?.Stats;
        _stats["Damage"].Text = $"공격 {Number(stats?.Damage)}";
        _stats["Range"].Text = $"사거리 {Number(stats?.Range)}";
        _stats["Sight"].Text = $"시야 {Number(stats?.Sight)}";
        _stats["Speed"].Text = $"이속 {Number(stats?.Speed)}";
        _stats["AttackSpeed"].Text = stats is { Interval: > 0 } attack
            ? $"공속 {(1.0 / attack.Interval).ToString("0.##", CultureInfo.InvariantCulture)}/초" : "공속 —";
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

    private (Control, ProgressBar, Label) Vital(VBoxContainer parent, string name, string textName, int height, string color)
    {
        var row = new Control { Name = name + "Row", CustomMinimumSize = new Vector2(0, height), MouseFilter = MouseFilterEnum.Ignore };
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
