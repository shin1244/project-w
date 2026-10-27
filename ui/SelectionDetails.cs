using Godot;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

// 선택 이벤트로 레이아웃을 바꾸고, HP/STATE는 기존 서버 스냅샷에서 읽습니다.
public partial class SelectionDetails : PanelContainer
{
    [Export] public UnitManager Units;
    [Export] public BuildingManager Buildings;
    [Export] public ResourceManager Resources;
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
    private ColorRect _shieldFill;
    private VBoxContainer _production;
    private ProgressBar _productionProgress;
    private HBoxContainer _productionQueue;
    private readonly List<TextureRect> _queuePortraits = new();
    private readonly List<PanelContainer> _queueFrames = new();
    private StyleBoxFlat _queueActive;
    private StyleBoxFlat _healthFill, _cardNormal, _cardHover, _cardPressed;
    private Button _previous, _next;
    private Building _building;
    private ResourceNode _tree;
    private PanelContainer _portraitFrame;
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
        var emptyTitle = Text(Lang.T("선택 없음"), 18, "a8b9b1");
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
        _portraitFrame = portraitFrame;
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
        _shieldFill = HealthDisplay.CreateShieldFill(_health);
        _activity = Text("", 14, "d0dcd2");
        _activity.Name = "Activity";
        information.AddChild(_activity);
        _extra = Text("", 13, "a5b6ad");
        _extra.Name = "Extra";
        _extra.MouseFilter = MouseFilterEnum.Stop;
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
        _previous = PageButton("‹", Lang.T("이전 유닛"), -1);
        _navigation.AddChild(_previous);
        _pageText = Text("", 12, "d0dcd2");
        _pageText.CustomMinimumSize = new Vector2(42, 0);
        _pageText.HorizontalAlignment = HorizontalAlignment.Center;
        _navigation.AddChild(_pageText);
        _next = PageButton("›", Lang.T("다음 유닛"), 1);
        _navigation.AddChild(_next);
        if (GodotObject.IsInstanceValid(Units)) Units.SelectionChanged += RefreshSelection;
        if (GodotObject.IsInstanceValid(Buildings))
        {
            Buildings.SelectionChanged += RefreshSelection;
            Buildings.ProductionChanged += RefreshValues;
        }
        if (GodotObject.IsInstanceValid(Resources)) Resources.SelectionChanged += RefreshSelection;
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
        _tree = _selection.Count == 0 && _building == null && GodotObject.IsInstanceValid(Resources) &&
            Available(Resources.SelectedResource) ? Resources.SelectedResource : null;
        _page = Mathf.Clamp(_page, 0, PageCount - 1);
        bool multiple = _selection.Count > 1;
        _empty.Visible = _selection.Count == 0 && _building == null && _tree == null;
        _portraitFrame.Visible = _tree == null;
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
                _portrait.Texture = _portraits.GetPortrait(UnitScene(unit.UnitType), unit.Team != Units.DisplayTeam);
            }
            else if (_building != null)
            {
                bool enemy = GodotObject.IsInstanceValid(Units) && Units.DisplayTeam != 0 && _building.SideId != Units.DisplayTeam;
                _title.Text = BuildingCatalog.Name(_building.BuildingType);
                _portrait.Texture = _portraits.GetPortrait(Buildings.SceneFor(_building.BuildingType), enemy);
            }
            else if (_tree != null)
            {
                _title.Text = Lang.T("나무");
                _portrait.Texture = null;
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
            SetText(_activity, single.UnitType == UnitCatalog.SiegeRam ? Lang.F("상태   {0}", Lang.T("자동 행군")) :
                ActivityText(single.State, single.HasServerState));
            _activity.TooltipText = UnitCatalog.Description(single.UnitType);
            SetText(_extra, single.UnitType == 0 ? single.HasServerState
                ? single.State.CarryType == ResourceType.Oil ? Lang.F("운반 중인 기름   {0}", single.State.Carrying)
                    : Lang.F("운반 중인 목재   {0}", single.State.Carrying)
                : Lang.T("운반 중인 자원   —") : single.UnitType == UnitCatalog.SteamArtillery ? SiegeText(single) :
                single.UnitType == UnitCatalog.Knight ? KnightChargeText(single) : "");
            _extra.TooltipText = single.UnitType == UnitCatalog.Knight && Buildings?.KnightResearch(single.Team) is KnightResearchSnapshot research
                ? CommandPanel.KnightResearchDescription(research) : "";
            SetText(_stats, StatsText(single.Stats, true));
        }
        else if (Available(_building))
        {
            health = _building.HealthBar;
            _activity.TooltipText = "";
            SetText(_activity, _building.IsUnderConstruction ? Lang.F("공사 중   {0}%", _building.ConstructionPercent) :
                _building.IsDefense ? ActivityText(_building.State, _building.HasServerState) : "");
            SetText(_extra, _building.IsUnderConstruction && _building.SideId == Units.LocalTeam
                ? Lang.T("일꾼을 선택하고 우클릭하여 이어 짓기") :
                _building.BuildingType == BuildingCatalog.ResearchLab ? KnightResearchText(_building.SideId) :
                _building.SideId != Units.LocalTeam ? "" :
                _building.BuildingType == BuildingCatalog.Tunnel ? TunnelText(_building) :
                _building.BuildingType == BuildingCatalog.CoalDrill ? Lang.T("일꾼 우클릭: 목재 채집 (줄지 않음)") : "");
            _extra.TooltipText = _building.BuildingType == BuildingCatalog.ResearchLab && Buildings.KnightResearch(_building.SideId) is KnightResearchSnapshot research
                ? CommandPanel.KnightResearchDescription(research) : "";
            string rallyText = _building.IsProducer && _building.SideId == Units.LocalTeam ?
                " · " + (_building.Rally is not RallySnapshot r ? Lang.T("우클릭으로 랠리 지정") : r.ResourceId != 0 ? Lang.T("랠리: 자동 채집") : Lang.T("랠리: 이동")) : "";
            string tierText = _building.IsDefense ? " · " + Lang.F("{0}티어", Buildings.TeamTier(_building.SideId).Tier) : "";
            SetText(_title, BuildingCatalog.Name(_building.BuildingType) + tierText + rallyText);
            SetText(_stats, StatsText(_building.Stats, false));
            RefreshProduction();
        }
        else if (Available(_tree))
        {
            // A tree shows only what is left to gather; the server's TREE line keeps it current.
            SetText(_healthText, Lang.F("남은 채집량   {0}", _tree.Amount));
            _health.Visible = _activity.Visible = _extra.Visible = _stats.Visible = false;
            return;
        }
        else return;
        _health.Visible = _stats.Visible = true;
        _activity.Visible = _activity.Text.Length > 0;
        _extra.Visible = _extra.Text.Length > 0;
        SetText(_healthText, Lang.F("체력   {0}", HealthText(health)));
        HealthDisplay.Apply(_health, _shieldFill, health.CurrentHP, health.MaxHP, health.Shield);
        Color color = HealthColor(health.Ratio);
        if (_healthFill.BgColor != color) _healthFill.BgColor = _healthFill.BorderColor = color;
    }

    // The server's TUNNEL_LINK decides the pair; this only reads it.
    private string TunnelText(Building tunnel) => Buildings.TunnelPartner(tunnel) == null
        ? Units.CommanderControl ? Lang.T("출구 없음 · Q: 출구 설치") : Lang.T("출구 없음")
        : Lang.T("짝 땅굴 연결됨 · 유닛 우클릭: 짝 옆으로 이동 · 한쪽이 무너지면 함께 무너짐");

    private void RefreshProduction()
    {
        if (_building.BuildingType == BuildingCatalog.ResearchLab && !_building.IsUnderConstruction &&
            Buildings.KnightResearch(_building.SideId) is { Researching: true } research && research.LabId == _building.BuildingId)
        {
            _production.Visible = true;
            SetText(_productionText, Lang.F("와이어 돌진 연구 · {0:0}초 남음", research.Remaining));
            _productionProgress.Value = research.Percent;
            foreach (var frame in _queueFrames) frame.Visible = false;
            return;
        }
        if (Buildings.IsTownHallUpgrading(_building))
        {
            var state = Buildings.TeamTier(_building.SideId);
            _production.Visible = true;
            SetText(_productionText, Lang.F("{0} → {1}티어 업그레이드 · {2:0}초 남음", state.Tier, state.TargetTier, state.Remaining));
            _productionProgress.Value = state.Percent;
            foreach (var frame in _queueFrames) frame.Visible = false;
            return;
        }
        foreach (var frame in _queueFrames) frame.Visible = true;
        if (_building.SideId != Units.LocalTeam || _building.IsUnderConstruction || _building.ProductionQueue.Count == 0) return;
        _production.Visible = true;
        int count = _building.ProductionQueue.Count;
        string name = UnitName(_building.ProductionQueue[0]);
        SetText(_productionText, $"{name} · {(_building.ProductionPercent == 100 ? Lang.T("출구 대기") : Lang.T("생산 중"))}   {count} / {ProductionSnapshot.MaxQueue}");
        _productionProgress.Value = _building.ProductionPercent;
        for (int i = 0; i < _queuePortraits.Count; i++)
        {
            bool occupied = i < count;
            _queuePortraits[i].Texture = occupied ? _portraits.GetPortrait(UnitScene(_building.ProductionQueue[i])) : null;
            bool canCancel = occupied && Units.CanCancelTraining(_building, _building.ProductionJobs[i].Id);
            _queueFrames[i].MouseDefaultCursorShape = canCancel ? CursorShape.PointingHand : CursorShape.Arrow;
            _queueFrames[i].TooltipText = occupied ? $"{(i == 0 ? Lang.T("생산 중") : Lang.F("대기 {0}", i))} · {UnitName(_building.ProductionQueue[i])}" +
                (canCancel ? "\n" + Lang.T("클릭하여 취소 · 지불 비용 75% 반환") : "") : Lang.T("빈 생산 슬롯");
        }
    }

    public static string StatsText(StatsSnapshot? snapshot, bool mobile)
    {
        if (snapshot is not StatsSnapshot s) return Lang.T("공격력 —   사거리 —   공격 간격 —");
        string F(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
        string attack = s.Interval > 0
            ? Lang.F("공격력 {0}   사거리 {1}   공격 간격 {2}초", HealthDisplay.Whole(s.Damage), F(s.Range), F(s.Interval)) : Lang.T("공격 불가");
        return mobile ? attack + "\n" + Lang.F("이동 속도 {0}   시야 {1}", F(s.Speed), F(s.Sight))
            : attack + (s.Interval > 0 ? "\n" : "   ") + Lang.F("시야 {0}", F(s.Sight));
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

    private PackedScene UnitScene(uint type) => Units.SceneFor(type);

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
    public static string HealthText(HealthBar health) => HealthDisplay.Text(health.CurrentHP, health.MaxHP, health.Shield);
    public static Color HealthColor(float ratio) => new(ratio > .5f ? "64cf79" : ratio > .25f ? "edbf55" : "e96860");
    private static string SiegeText(Unit unit)
    {
        if (unit.Siege is not SiegeSnapshot state) return Lang.T("공성 상태   —");
        return state.Mode switch
        {
            SiegeMode.Mobile => Lang.T("이동 모드"),
            SiegeMode.Deployed => Lang.T("공성 모드 · 이동 불가"),
            SiegeMode.Deploying => Lang.F("전개 중   {0:0}%", state.DeploymentAt(unit.EffectTick) * 100),
            SiegeMode.Packing => Lang.F("전개 해제 중   {0:0}%", (1 - state.DeploymentAt(unit.EffectTick)) * 100),
            _ => ""
        };
    }

    private string KnightResearchText(uint team) => Buildings?.KnightResearch(team) switch
    {
        { Unlocked: true } => Lang.T("와이어 돌진 · 자동 발동"),
        { Researching: true } state => Lang.F("와이어 돌진 · 연구 중 {0:0}초", state.Remaining),
        KnightResearchSnapshot => Lang.T("와이어 돌진 · 기술연구소 연구 필요"),
        _ => Lang.T("와이어 돌진 · 정보 수신 대기")
    };

    private string KnightChargeText(Unit unit)
    {
        if (unit.KnightCharge is not KnightChargeSnapshot charge)
            return Buildings?.KnightResearch(unit.Team) is { Unlocked: false } ? KnightResearchText(unit.Team) : Lang.T("와이어 돌진 · 정보 수신 대기");
        if (!charge.Enabled) return KnightResearchText(unit.Team);
        if (charge.ActiveAt(unit.EffectTick)) return Lang.T("와이어 돌진 · 돌진 중");
        if (charge.IsReadyAt(unit.EffectTick)) return Lang.T("와이어 돌진 · 준비");
        double seconds = (charge.ReadyTick - unit.EffectTick) / (double)InterpolationClock.TickRate;
        return Lang.F("와이어 돌진 · 재사용 {0:0}초", System.Math.Ceiling(seconds));
    }

    public static string ActivityText(UnitState state, bool known) => Lang.F("상태   {0}", ActivityName(state, known));

    // The activity alone, for places that print their own label.
    public static string ActivityName(UnitState state, bool known) => !known ? "—" : state.Activity switch
    {
        UnitActivity.Gather => Lang.T("채집 중"), UnitActivity.Attack => Lang.T("공격 중"),
        UnitActivity.Guard => Lang.T("경계 중"),
        UnitActivity.Hold => Lang.T("위치 사수"),
        UnitActivity.Dash => Lang.T("돌진 중"), UnitActivity.Stun => Lang.T("기절"),
        UnitActivity.Channel => Lang.T("마석 수집 중"),
        UnitActivity.Inside => Lang.T("시추탑 안"),
        UnitActivity.Build => Lang.T("건설 중"), _ => Lang.T("대기 / 이동")
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
        if (GodotObject.IsInstanceValid(Resources)) Resources.SelectionChanged -= RefreshSelection;
        Resized -= RefreshColumns;
        foreach (StyleBoxFlat style in _styles) style.Dispose();
        _styles.Clear();
    }
}
