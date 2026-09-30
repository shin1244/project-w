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
    private Label _title, _healthText, _activity, _extra, _pageText, _stats, _productionText;
    private Control _empty;
    private HBoxContainer _single, _navigation;
    private GridContainer _grid;
    private TextureRect _portrait;
    private ProgressBar _health;
    private VBoxContainer _production;
    private ProgressBar _productionProgress;
    private HBoxContainer _productionQueue;
    private readonly List<TextureRect> _queuePortraits = new();
    private readonly List<PanelContainer> _queueFrames = new();
    private StyleBoxFlat _queueActive;
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
        information.AddThemeConstantOverride("separation", 3);
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
        _stats = Text("", 13, "d0dcd2");
        _stats.Name = "Stats";
        information.AddChild(_stats);
        _production = new VBoxContainer { Name = "Production", MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        _production.AddThemeConstantOverride("separation", 3);
        information.AddChild(_production);
        _productionText = Text("", 13, "e5dba9");
        _production.AddChild(_productionText);
        _productionProgress = new ProgressBar
        {
            Name = "ProductionProgress", CustomMinimumSize = new Vector2(0, 12),
            ShowPercentage = false, MouseFilter = MouseFilterEnum.Ignore
        };
        _productionProgress.AddThemeStyleboxOverride("background", Style("101c26", "516653"));
        _productionProgress.AddThemeStyleboxOverride("fill", Style("d4bd6a", "d4bd6a"));
        _production.AddChild(_productionProgress);
        _productionQueue = new HBoxContainer { Name = "Queue", MouseFilter = MouseFilterEnum.Ignore };
        _production.AddChild(_productionQueue);
        _queueActive = Style("365543", "d4bd6a");
        for (int i = 0; i < ProductionSnapshot.MaxQueue; i++)
        {
            var frameSlot = new PanelContainer { CustomMinimumSize = new Vector2(30, 30), MouseFilter = MouseFilterEnum.Stop };
            frameSlot.AddThemeStyleboxOverride("panel", i == 0 ? _queueActive : _cardNormal);
            var queuePortrait = new TextureRect
            {
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = MouseFilterEnum.Ignore
            };
            frameSlot.AddChild(queuePortrait);
            int slotIndex = i;
            frameSlot.GuiInput += input =>
            {
                if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
                {
                    frameSlot.AcceptEvent();
                    if (Available(_building) && slotIndex < _building.ProductionJobs.Count)
                        Units.RequestCancelTraining(_building, _building.ProductionJobs[slotIndex].Id);
                }
            };
            _productionQueue.AddChild(frameSlot);
            _queueFrames.Add(frameSlot);
            _queuePortraits.Add(queuePortrait);
        }

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
        if (GodotObject.IsInstanceValid(Buildings))
        {
            Buildings.SelectionChanged += RefreshSelection;
            Buildings.ProductionChanged += RefreshValues;
        }
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
        if (_selection.Count == 0 && GodotObject.IsInstanceValid(Units) && Units.CanInspect(Units.InspectedUnit))
            _selection.Add(Units.InspectedUnit);
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
                _portrait.Texture = _portraits.GetPortrait(UnitScene(unit.UnitType), unit.Team != Units.LocalTeam);
            }
            else if (_building != null)
            {
                bool enemy = GodotObject.IsInstanceValid(Units) && Units.LocalTeam != 0 && _building.SideId != Units.LocalTeam;
                _title.Text = BuildingCatalog.Name(_building.BuildingType);
                _portrait.Texture = _portraits.GetPortrait(Buildings.SceneFor(_building.BuildingType), enemy);
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
        _production.Visible = false;
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
            SetText(_stats, StatsText(single.Stats, true));
        }
        else if (Available(_building))
        {
            health = _building.HealthBar;
            SetText(_activity, _building.IsUnderConstruction ? $"공사 중   {_building.ConstructionPercent}%" :
                _building.IsDefense ? ActivityText(_building.State, _building.HasServerState) : "");
            SetText(_extra, _building.IsUnderConstruction && _building.SideId == Units.LocalTeam ? "내 일꾼으로 우클릭해 이어 짓기" : "");
            string rallyText = _building.IsProducer && _building.SideId == Units.LocalTeam ?
                _building.Rally is not RallySnapshot r ? " · 우클릭으로 랠리 지정" : r.ResourceId != 0 ? " · 랠리: 자동 채집" : " · 랠리: 이동" : "";
            SetText(_title, BuildingCatalog.Name(_building.BuildingType) + rallyText);
            SetText(_stats, StatsText(_building.Stats, false));
            RefreshProduction();
        }
        else return;
        _activity.Visible = _activity.Text.Length > 0;
        _extra.Visible = _extra.Text.Length > 0;
        SetText(_healthText, $"체력   {HealthText(health)}");
        _health.Value = health.MaxHP > 0 ? health.Ratio * 100 : 0;
        Color color = HealthColor(health.Ratio);
        if (_healthFill.BgColor != color) _healthFill.BgColor = _healthFill.BorderColor = color;
    }

    private void RefreshProduction()
    {
        if (_building.SideId != Units.LocalTeam || _building.IsUnderConstruction || _building.ProductionQueue.Count == 0) return;
        _production.Visible = true;
        int count = _building.ProductionQueue.Count;
        string name = UnitName(_building.ProductionQueue[0]);
        SetText(_productionText, $"{name} · {(_building.ProductionPercent == 100 ? "출구 대기" : "생산 중")}   {count} / {ProductionSnapshot.MaxQueue}");
        _productionProgress.Value = _building.ProductionPercent;
        for (int i = 0; i < _queuePortraits.Count; i++)
        {
            bool occupied = i < count;
            _queuePortraits[i].Texture = occupied ? _portraits.GetPortrait(UnitScene(_building.ProductionQueue[i])) : null;
            bool canCancel = occupied && Units.CanCancelTraining(_building, _building.ProductionJobs[i].Id);
            _queueFrames[i].MouseDefaultCursorShape = canCancel ? CursorShape.PointingHand : CursorShape.Arrow;
            _queueFrames[i].TooltipText = occupied ? $"{(i == 0 ? "생산 중" : $"대기 {i}")} · {UnitName(_building.ProductionQueue[i])}" +
                (canCancel ? "\n클릭하여 취소 · 지불 비용 75% 반환" : "") : "빈 생산 슬롯";
        }
    }

    public static string StatsText(StatsSnapshot? snapshot, bool mobile)
    {
        if (snapshot is not StatsSnapshot s) return "공격력 —   사거리 —   공격 간격 —";
        string F(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
        string attack = s.Interval > 0 ? $"공격력 {F(s.Damage)}   사거리 {F(s.Range)}   공격 간격 {F(s.Interval)}초" : "공격 불가";
        return mobile ? $"{attack}\n이동 속도 {F(s.Speed)}   시야 {F(s.Sight)}" : s.Interval > 0 ? $"{attack}\n시야 {F(s.Sight)}" : $"{attack}   시야 {F(s.Sight)}";
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

    private PackedScene UnitScene(uint type) => type switch
    {
        UnitCatalog.Worker => Units.WorkerScene, UnitCatalog.Knight => Units.KnightScene, UnitCatalog.Archer => Units.ArcherScene,
        UnitCatalog.MinionMelee => Units.MinionKnightScene, UnitCatalog.MinionRanged => Units.MinionArcherScene, _ => null
    };

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
    public static string UnitName(uint type) => UnitCatalog.Name(type);
    public static string HealthText(HealthBar health) => health.MaxHP <= 0 ? "— / —" :
        $"{health.CurrentHP.ToString("0.#", CultureInfo.InvariantCulture)} / {health.MaxHP.ToString("0.#", CultureInfo.InvariantCulture)}";
    public static Color HealthColor(float ratio) => new(ratio > .5f ? "64cf79" : ratio > .25f ? "edbf55" : "e96860");
    public static string ActivityText(UnitState state, bool known) => !known ? "상태   —" : state.Activity switch
    {
        UnitActivity.Gather => "상태   채집 중", UnitActivity.Attack => "상태   공격 중",
        UnitActivity.Guard => "상태   경계 중",
        UnitActivity.Build => "상태   건설 중", _ => "상태   대기 / 이동"
    };

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouse) AcceptEvent();
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(Units)) Units.SelectionChanged -= RefreshSelection;
        if (GodotObject.IsInstanceValid(Buildings))
        {
            Buildings.SelectionChanged -= RefreshSelection;
            Buildings.ProductionChanged -= RefreshValues;
        }
        Resized -= RefreshColumns;
        foreach (StyleBoxFlat style in _styles) style.Dispose();
        _styles.Clear();
    }
}
