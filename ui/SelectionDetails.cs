using Godot;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

// 선택 이벤트로 레이아웃을 바꾸고, HP/STATE는 기존 서버 스냅샷에서 읽습니다.
public partial class SelectionDetails : PanelContainer
{
    [Export] public UnitManager Units;
    [Export] public BuildingManager Buildings;
    private readonly List<StyleBoxFlat> _styles = new();
    private readonly List<Unit> _selection = new();
    private readonly List<SelectionUnitCard> _cards = new();
    private SelectionPortraits _portraits;
    private Label _title, _healthText, _activity, _extra, _pageText;
    private Control _empty;
    private HBoxContainer _single, _navigation;
    private GridContainer _grid;
    private TextureRect _portrait;
    private ProgressBar _health;
    private StyleBoxFlat _healthFill, _cardNormal, _cardHover, _cardPressed;
    private Button _previous, _next;
    private Building _building;
    private int _page, _columns = 1;
    private bool _ready;

    public int PageSize => _columns * 2;
    public int PageCount => Mathf.Max(1, (_selection.Count + PageSize - 1) / PageSize);

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        MouseForcePassScrollEvents = false;
        ClipContents = true;
        var frame = Style("18232ef5", "65776a");
        frame.ContentMarginLeft = frame.ContentMarginRight = 12;
        frame.ContentMarginTop = frame.ContentMarginBottom = 10;
        AddThemeStyleboxOverride("panel", frame);
        _cardNormal = Style("101c26", "516653");
        _cardHover = Style("24392f", "b6d3aa");
        _cardPressed = Style("365543", "e0dbb4");
        _portraits = new SelectionPortraits { Name = "Portraits" };
        AddChild(_portraits);

        var content = new VBoxContainer { Name = "Content", MouseFilter = MouseFilterEnum.Ignore };
        content.AddThemeConstantOverride("separation", 6);
        AddChild(content);
        _title = Text("", 17, "ece6cf");
        _title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        content.AddChild(_title);

        var body = new Control
        {
            Name = "Body", CustomMinimumSize = new Vector2(0, 132),
            SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore
        };
        content.AddChild(body);
        var empty = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        body.AddChild(empty);
        empty.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var emptyTitle = Text("선택한 대상이 없습니다", 18, "a8b9b1");
        emptyTitle.HorizontalAlignment = HorizontalAlignment.Center;
        empty.AddChild(emptyTitle);
        _empty = empty;

        _single = new HBoxContainer { Name = "Single", MouseFilter = MouseFilterEnum.Ignore };
        _single.AddThemeConstantOverride("separation", 16);
        body.AddChild(_single);
        _single.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var portraitFrame = new PanelContainer { CustomMinimumSize = new Vector2(124, 132), MouseFilter = MouseFilterEnum.Ignore };
        var portraitStyle = Style("101c26", "6a8271");
        portraitStyle.ContentMarginLeft = portraitStyle.ContentMarginTop = portraitStyle.ContentMarginRight = portraitStyle.ContentMarginBottom = 3;
        portraitFrame.AddThemeStyleboxOverride("panel", portraitStyle);
        _single.AddChild(portraitFrame);
        _portrait = new TextureRect
        {
            Name = "Portrait", ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore
        };
        portraitFrame.AddChild(_portrait);
        var information = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        information.AddThemeConstantOverride("separation", 5);
        _single.AddChild(information);
        _healthText = Text("", 17, "e3ebdd");
        _healthText.Name = "HealthText";
        information.AddChild(_healthText);
        _health = new ProgressBar { Name = "Health", CustomMinimumSize = new Vector2(0, 10), ShowPercentage = false, MouseFilter = MouseFilterEnum.Ignore };
        _health.AddThemeStyleboxOverride("background", Style("36424a", "36424a"));
        _healthFill = Style("64cf79", "64cf79");
        _health.AddThemeStyleboxOverride("fill", _healthFill);
        information.AddChild(_health);
        _activity = Text("", 14, "d0dcd2");
        _activity.Name = "Activity";
        information.AddChild(_activity);
        _extra = Text("", 13, "a5b6ad");
        information.AddChild(_extra);

        _grid = new GridContainer { Name = "UnitGrid", MouseFilter = MouseFilterEnum.Ignore };
        _grid.AddThemeConstantOverride("h_separation", 6);
        _grid.AddThemeConstantOverride("v_separation", 4);
        body.AddChild(_grid);

        // 페이지 이동이 필요할 때만 하단 공간을 사용합니다.
        _navigation = new HBoxContainer
        {
            Name = "Pages", MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ShrinkEnd
        };
        content.AddChild(_navigation);
        _previous = PageButton("‹", "이전 유닛", -1);
        _navigation.AddChild(_previous);
        _pageText = Text("", 12, "d0dcd2");
        _pageText.CustomMinimumSize = new Vector2(42, 0);
        _pageText.HorizontalAlignment = HorizontalAlignment.Center;
        _navigation.AddChild(_pageText);
        _next = PageButton("›", "다음 유닛", 1);
        _navigation.AddChild(_next);
        if (GodotObject.IsInstanceValid(Units)) Units.SelectionChanged += RefreshSelection;
        if (GodotObject.IsInstanceValid(Buildings)) Buildings.SelectionChanged += RefreshSelection;
        Resized += RefreshColumns;
        _ready = true;
        RefreshColumns();
        RefreshSelection();
    }

    private void RefreshSelection()
    {
        _selection.Clear();
        if (GodotObject.IsInstanceValid(Units))
            foreach (uint id in Units.SelectedUnitIds)
                if (Units.TryGetUnit(id, out Unit unit) && Available(unit)) _selection.Add(unit);
        _selection.Sort((left, right) => left.UnitType == right.UnitType
            ? left.UnitId.CompareTo(right.UnitId) : left.UnitType.CompareTo(right.UnitType));
        _building = GodotObject.IsInstanceValid(Buildings) && Available(Buildings.SelectedBuilding)
            ? Buildings.SelectedBuilding : null;
        _page = Mathf.Clamp(_page, 0, PageCount - 1);
        bool multiple = _selection.Count > 1;
        _empty.Visible = _selection.Count == 0 && _building == null;
        _single.Visible = !multiple && !_empty.Visible;
        _title.Visible = _single.Visible;
        _grid.Visible = multiple;
        _navigation.Visible = multiple && PageCount > 1;
        if (multiple)
        {
            FillPage();
        }
        else
        {
            ClearCards();
            if (_selection.Count == 1)
            {
                Unit unit = _selection[0];
                _title.Text = UnitName(unit.UnitType);
                _portrait.Texture = _portraits.GetPortrait(UnitScene(unit.UnitType));
            }
            else if (_building != null)
            {
                bool enemy = GodotObject.IsInstanceValid(Units) && Units.LocalTeam != 0 && _building.SideId != Units.LocalTeam;
                _title.Text = _building.BuildingType == 0 ? "회관" : "포탑";
                _portrait.Texture = _portraits.GetPortrait(_building.BuildingType == 0 ? Buildings.TownHallScene : Buildings.TowerScene, enemy);
            }
            else
            {
                _title.Text = "";
                _portrait.Texture = null;
            }
        }
        RefreshValues();
        SetProcess(!_empty.Visible);
    }

    public override void _Process(double delta) => RefreshValues();

    private void RefreshValues()
    {
        if (_selection.Count > 1)
        {
            foreach (SelectionUnitCard card in _cards)
                if (Units.TryGetUnit(card.UnitId, out Unit unit) && Available(unit)) card.Refresh(unit);
            return;
        }
        Unit single = _selection.Count == 1 ? _selection[0] : null;
        HealthBar health;
        if (Available(single))
        {
            health = single.HealthBar;
            SetText(_activity, ActivityText(single.State, single.HasServerState));
            SetText(_extra, single.UnitType == 0 ? single.HasServerState ? $"운반 중인 목재   {single.State.Carrying}" : "운반 중인 목재   —" : "");
        }
        else if (Available(_building))
        {
            health = _building.HealthBar;
            SetText(_activity, _building.BuildingType == 0 ? "" : ActivityText(_building.State, _building.HasServerState));
            SetText(_extra, "");
        }
        else return;
        SetText(_healthText, $"체력   {HealthText(health)}");
        _health.Value = health.MaxHP > 0 ? health.Ratio * 100 : 0;
        Color color = HealthColor(health.Ratio);
        if (_healthFill.BgColor != color) _healthFill.BgColor = _healthFill.BorderColor = color;
    }

    private void RefreshColumns()
    {
        if (!_ready) return;
        int columns = Mathf.Clamp((int)((Size.X - 24 + 6) / 66), 1, 16);
        if (columns == _columns) return;
        int firstIndex = _page * PageSize;
        _columns = columns;
        _page = Mathf.Clamp(firstIndex / PageSize, 0, PageCount - 1);
        if (_selection.Count > 1) FillPage();
    }

    private void FillPage()
    {
        ClearCards();
        _grid.Columns = _columns;
        foreach (Unit unit in _selection.Skip(_page * PageSize).Take(PageSize))
        {
            var card = new SelectionUnitCard
            {
                Name = $"Unit{unit.UnitId}", CustomMinimumSize = new Vector2(60, 64),
                FocusMode = FocusModeEnum.None, MouseFilter = MouseFilterEnum.Stop,
                MouseForcePassScrollEvents = false, MouseDefaultCursorShape = CursorShape.PointingHand
            };
            card.AddThemeStyleboxOverride("normal", _cardNormal);
            card.AddThemeStyleboxOverride("hover", _cardHover);
            card.AddThemeStyleboxOverride("pressed", _cardPressed);
            card.Bind(unit, _portraits.GetPortrait(UnitScene(unit.UnitType)));
            uint id = unit.UnitId;
            // MouseButton의 수정키를 그대로 사용해 GUI 입력과 선택을 같은 프레임에 처리합니다.
            card.GuiInput += input =>
            {
                if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mouse)
                {
                    card.AcceptEvent();
                    Units.SelectFromPortrait(id, mouse.ShiftPressed, mouse.CtrlPressed);
                }
            };
            _grid.AddChild(card);
            _cards.Add(card);
        }
        _navigation.Visible = PageCount > 1;
        _pageText.Text = $"{_page + 1} / {PageCount}";
        _previous.Disabled = _page == 0;
        _next.Disabled = _page >= PageCount - 1;
    }

    private void ClearCards()
    {
        foreach (SelectionUnitCard card in _cards) { _grid.RemoveChild(card); card.QueueFree(); }
        _cards.Clear();
    }

    private PackedScene UnitScene(uint type) => type switch { 0 => Units.WorkerScene, 1 => Units.KnightScene, 2 => Units.ArcherScene, _ => null };

    private Button PageButton(string text, string tooltip, int direction)
    {
        var button = new Button
        {
            Text = text, TooltipText = tooltip, CustomMinimumSize = new Vector2(28, 24),
            FocusMode = FocusModeEnum.None, MouseFilter = MouseFilterEnum.Stop, MouseForcePassScrollEvents = false
        };
        button.AddThemeStyleboxOverride("normal", _cardNormal);
        button.AddThemeStyleboxOverride("hover", _cardHover);
        button.AddThemeStyleboxOverride("pressed", _cardPressed);
        button.AddThemeStyleboxOverride("disabled", _cardNormal);
        button.Pressed += () => { _page = Mathf.Clamp(_page + direction, 0, PageCount - 1); FillPage(); };
        return button;
    }

    private static Label Text(string value, int size, string color)
    {
        var label = new Label { Text = value, MouseFilter = MouseFilterEnum.Ignore, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", new Color(color));
        return label;
    }

    private StyleBoxFlat Style(string background, string border)
    {
        var style = new StyleBoxFlat { BgColor = new Color(background), BorderColor = new Color(border), BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1 };
        _styles.Add(style);
        return style;
    }

    private static void SetText(Label label, string text) { if (label.Text != text) label.Text = text; }
    private static bool Available(Node node) => GodotObject.IsInstanceValid(node) && node.IsInsideTree() && !node.IsQueuedForDeletion() && node is not Unit { IsDying: true };
    public static string UnitName(uint type) => type switch { 0 => "일꾼", 1 => "기사", 2 => "궁수", _ => "유닛" };
    public static string HealthText(HealthBar health) => health.MaxHP <= 0 ? "— / —" :
        $"{health.CurrentHP.ToString("0.#", CultureInfo.InvariantCulture)} / {health.MaxHP.ToString("0.#", CultureInfo.InvariantCulture)}";
    public static Color HealthColor(float ratio) => new(ratio > .5f ? "64cf79" : ratio > .25f ? "edbf55" : "e96860");
    public static string ActivityText(UnitState state, bool known) => !known ? "상태   —" : state.Activity switch
    {
        UnitActivity.Gather => "상태   채집 중", UnitActivity.Attack => "상태   공격 중", _ => "상태   대기 / 이동"
    };

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouse) AcceptEvent();
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(Units)) Units.SelectionChanged -= RefreshSelection;
        if (GodotObject.IsInstanceValid(Buildings)) Buildings.SelectionChanged -= RefreshSelection;
        Resized -= RefreshColumns;
        foreach (StyleBoxFlat style in _styles) style.Dispose();
        _styles.Clear();
    }
}
