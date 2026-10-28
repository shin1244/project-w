using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

// 중앙 HUD의 두 보기를 전환한다. 선택 유닛과 이미 출발한 웨이브는 변경하지 않는다.
public partial class MinionPanel : Control
{
    public event Action<string> CommandRequested;
    public event Action<bool> ViewChanged;
    public bool IsMinionView { get; private set; }
    public bool IsPickerOpen => _picker?.Visible == true;
    public bool IsRequestPending => _pendingLane.HasValue;
    public string TimerText => _timer?.Text ?? "";
    // An extra slot costs this (마석 today); the balance is the commander's STOCK of it.
    public MinionAddCostSnapshot? AddCost { get; private set; }
    public long MagicStones => AddCost is { } cost && _stock.TryGetValue(cost.ResourceId, out long amount) ? amount : 0;
    public bool CanAffordAdd => AddCost is { } cost && MagicStones >= cost.Amount;

    private readonly SortedDictionary<int, MinionLaneSnapshot> _lanes = new();
    private readonly SortedDictionary<uint, MinionOptionSnapshot> _options = new();
    private readonly SortedDictionary<uint, MinionUpgradeSnapshot> _upgrades = new();
    private readonly Dictionary<uint, long> _stock = new();
    private readonly Dictionary<uint, Texture2D> _portraits = new();
    private readonly List<string> _laneNames = new();
    private readonly List<StyleBoxFlat> _styles = new();
    private readonly List<(Button Button, int Lane, bool Add)> _slotButtons = new();
    private readonly List<(Button Button, Label Cost, Choice Option)> _optionButtons = new();
    private StyleBoxFlat _normal, _hover, _pressed, _disabled;
    private PanelContainer _body, _picker;
    private HBoxContainer _tabs;
    private VBoxContainer _rows, _choices;
    private ScrollContainer _laneScroll;
    private Label _timer, _hint, _pickerTitle;
    private Label _stoneLabel;
    private Button _defaultTab, _minionTab;
    private Control _pickerAnchor;
    private MinionRulesSnapshot? _rules;
    private MinionExperienceRuleSnapshot? _experienceRule;
    private uint _tick, _nextWaveTick, _pickerRevision, _pendingRevision;
    private int _pickerLane, _pickerSlot;
    private int _teamTier = 1;
    private int? _pendingLane;
    private MouseButton? _consumedRelease;
    private bool _enabled, _ready, _hasTick, _hasNextWave;
    private bool _draggingSlot, _suppressSlotClick, _consumeDragRelease;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ClipContents = false;
        _normal = Style("101c26", "516653", 4);
        _hover = Style("24392f", "b6d3aa", 4);
        _pressed = Style("365543", "e0dbb4", 4);
        _disabled = Style("152027", "374a44", 4);

        _tabs = new HBoxContainer { Name = "Tabs", MouseFilter = MouseFilterEnum.Ignore,
            Alignment = BoxContainer.AlignmentMode.Center };
        _tabs.AddThemeConstantOverride("separation", 8);
        AddChild(_tabs);
        _tabs.AnchorRight = 1;
        _tabs.OffsetTop = -34;
        _tabs.OffsetBottom = -6;
        _defaultTab = MakeButton("DefaultTab", Lang.T("부대"), new Vector2(86, 28));
        _minionTab = MakeButton("MinionTab", Lang.T("미니언"), new Vector2(86, 28));
        _tabs.AddChild(_defaultTab);
        _tabs.AddChild(_minionTab);
        _defaultTab.Pressed += () => SetView(false);
        _minionTab.Pressed += () => SetView(true);

        _body = new PanelContainer { Name = "Body", MouseFilter = MouseFilterEnum.Stop,
            MouseForcePassScrollEvents = false, Visible = false };
        _body.AddThemeStyleboxOverride("panel", FrameStyle("18232ef5", "65776a", 10, 8));
        AddChild(_body);
        _body.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ConsumeMouse(_body);
        var content = new VBoxContainer { Name = "Content", MouseFilter = MouseFilterEnum.Ignore };
        content.AddThemeConstantOverride("separation", 4);
        _body.AddChild(content);
        var header = new HBoxContainer { Name = "Header", MouseFilter = MouseFilterEnum.Ignore };
        content.AddChild(header);
        var title = Text(Lang.T("미니언 편성"), 15, "ece6cf");
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(title);
        _stoneLabel = Text("", 12, "9ce7b5");
        _stoneLabel.Name = "MagicStone";
        _stoneLabel.TextOverrunBehavior = TextServer.OverrunBehavior.NoTrimming;
        _stoneLabel.MouseFilter = MouseFilterEnum.Pass;
        _stoneLabel.Visible = false;
        header.AddChild(_stoneLabel);
        _timer = Text(Lang.T("웨이브 정보 대기 중"), 13, "e6d59e");
        _timer.Name = "WaveTimer";
        _timer.TextOverrunBehavior = TextServer.OverrunBehavior.NoTrimming;
        header.AddChild(_timer);
        _laneScroll = new ScrollContainer { Name = "Lanes", SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Stop,
            MouseForcePassScrollEvents = false, HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto };
        content.AddChild(_laneScroll);
        _rows = new VBoxContainer { Name = "Rows", MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _rows.AddThemeConstantOverride("separation", 4);
        _laneScroll.AddChild(_rows);
        _hint = Text("", 12, "a8b9b1");
        _hint.Name = "Status";
        _hint.Visible = false;
        content.AddChild(_hint);

        _picker = new PanelContainer { Name = "Picker", Visible = false, ZIndex = 30,
            MouseFilter = MouseFilterEnum.Stop, MouseForcePassScrollEvents = false,
            CustomMinimumSize = new Vector2(286, 0) };
        _picker.AddThemeStyleboxOverride("panel", FrameStyle("14231bfc", "9ab58b", 8, 8));
        AddChild(_picker);
        ConsumeMouse(_picker);
        var pickerContent = new VBoxContainer { Name = "Content", MouseFilter = MouseFilterEnum.Ignore };
        pickerContent.AddThemeConstantOverride("separation", 6);
        _picker.AddChild(pickerContent);
        var pickerHeader = new HBoxContainer { Name = "Header", MouseFilter = MouseFilterEnum.Ignore };
        pickerContent.AddChild(pickerHeader);
        _pickerTitle = Text(Lang.T("미니언 변경"), 15, "ece6cf");
        _pickerTitle.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        pickerHeader.AddChild(_pickerTitle);
        var close = MakeButton("Close", "×", new Vector2(24, 24));
        close.TooltipText = Lang.T("닫기 (Esc)");
        close.Pressed += ClosePicker;
        pickerHeader.AddChild(close);
        _choices = new VBoxContainer { Name = "Options", MouseFilter = MouseFilterEnum.Ignore };
        _choices.AddThemeConstantOverride("separation", 4);
        pickerContent.AddChild(_choices);
        Resized += PositionPicker;
        _ready = true;
        RefreshStoneLabel();
        Visible = _enabled;
        RefreshView();
        RebuildLanes();
        RefreshTimer();
    }

    public void SetRole(bool enabled)
    {
        _enabled = enabled;
        if (!enabled) Clear();
        Visible = enabled;
    }

    public void ConfigureLanes(IReadOnlyList<string> names)
    {
        _laneNames.Clear();
        if (names != null)
            foreach (string name in names) _laneNames.Add(name);
        RebuildLanes();
    }

    public void Clear()
    {
        CancelSlotDrag();
        _lanes.Clear();
        _options.Clear();
        _upgrades.Clear();
        _teamTier = 1;
        _stock.Clear();
        _laneNames.Clear();
        _rules = null;
        _experienceRule = null;
        AddCost = null;
        RefreshStoneLabel();
        _hasTick = _hasNextWave = false;
        _tick = _nextWaveTick = 0;
        _pendingLane = null;
        _consumedRelease = null;
        ClosePicker();
        SetView(false);
        RebuildLanes();
        RefreshTimer();
        SetHint("");
    }

    private void SetView(bool minions)
    {
        CancelSlotDrag();
        bool next = _enabled && minions;
        ClosePicker();
        if (IsMinionView == next) { RefreshView(); return; }
        IsMinionView = next;
        RefreshView();
        ViewChanged?.Invoke(next);
    }

    private void RefreshView()
    {
        if (!_ready) return;
        _body.Visible = IsMinionView;
        _defaultTab.AddThemeStyleboxOverride("normal", IsMinionView ? _normal : _pressed);
        _minionTab.AddThemeStyleboxOverride("normal", IsMinionView ? _pressed : _normal);
    }

    public void HandleMessage(string[] parts)
    {
        if (!_enabled || parts.Length == 0) return;
        if (MinionAddCostSnapshot.TryParse(parts, out var addCost))
        {
            AddCost = addCost;
            RefreshStoneLabel();
            RefreshPurchases();
        }
        else if (MinionRulesSnapshot.TryParse(parts, out var rules))
        {
            _rules = rules;
            ClosePicker();
            RebuildLanes();
            RefreshTimer();
        }
        else if (MinionExperienceRuleSnapshot.TryParse(parts, out var experienceRule))
        {
            _experienceRule = experienceRule;
            RebuildLanes();
        }
        else if (MinionOptionSnapshot.TryParse(parts, out var option))
        {
            _options[option.UnitType] = option;
            RebuildLanes();
        }
        else if (MinionUpgradeSnapshot.TryParse(parts, out var upgrade))
        {
            _upgrades[upgrade.UnitType] = upgrade;
            RebuildLanes();
        }
        else if (MinionLaneSnapshot.TryParse(parts, out var lane))
        {
            if (_rules is MinionRulesSnapshot limits && lane.UnitTypes.Length > limits.MaxSlots ||
                _lanes.TryGetValue(lane.LaneIndex, out var previous) && unchecked((int)(lane.Revision - previous.Revision)) < 0) return;
            _lanes[lane.LaneIndex] = lane;
            if (_pendingLane == lane.LaneIndex)
            {
                _pendingLane = null;
                SetHint(lane.Revision != _pendingRevision
                    ? ""
                    : Lang.T("편성 변경됨 · 다시 선택"));
            }
            else if (!_pendingLane.HasValue) SetHint("");
            RebuildLanes();
        }
        else if (MinionWaveSnapshot.TryParse(parts, out var wave))
        {
            if (_hasNextWave && unchecked((int)(wave.NextWaveTick - _nextWaveTick)) < 0) return;
            _nextWaveTick = wave.NextWaveTick;
            _hasNextWave = true;
            RefreshTimer();
        }
        else if (parts[0] == "ERR" && _pendingLane.HasValue)
        {
            _pendingLane = null;
            SetHint(parts.Length > 1 ? string.Join(" ", parts, 1, parts.Length - 1) : Lang.T("구매 요청을 처리하지 못했습니다"), true);
            RefreshPurchases();
        }
    }

    public void HandleStock(string[] parts)
    {
        if (!_enabled || parts.Length != 3 || parts[0] != "STOCK" ||
            !uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out uint resource) ||
            !long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out long amount)) return;
        _stock[resource] = amount;
        RefreshStoneLabel();
        RefreshPurchases();
    }

    // HQ tier of the commander's team: tier-two choices unlock when it reaches theirs.
    public void SetTeamTier(int tier)
    {
        if (tier == _teamTier) return;
        _teamTier = tier;
        RebuildLanes();
    }

    public void SetTick(uint tick)
    {
        if (_hasTick && unchecked((int)(tick - _tick)) < 0) return;
        _tick = tick;
        _hasTick = true;
        RefreshTimer();
    }

    private void RefreshTimer()
    {
        if (!_ready) return;
        _timer.Text = _hasTick && _hasNextWave
            ? Lang.F("웨이브 {0:0}초", Math.Ceiling(Math.Max(0, unchecked((int)(_nextWaveTick - _tick))) / (double)InterpolationClock.TickRate))
            : Lang.T("웨이브 정보 대기 중");
    }

    private void RebuildLanes()
    {
        if (!_ready) return;
        CancelSlotDrag();
        ClosePicker();
        ClearChildren(_rows);
        _slotButtons.Clear();
        if (_lanes.Count == 0)
        {
            var empty = Text(Lang.T("라인별 미니언 편성을 불러오는 중…"), 14, "a8b9b1");
            empty.CustomMinimumSize = new Vector2(0, 92);
            empty.VerticalAlignment = VerticalAlignment.Center;
            _rows.AddChild(empty);
            return;
        }
        foreach (var (index, lane) in _lanes)
        {
            var row = new HBoxContainer { Name = $"Lane{index}", MouseFilter = MouseFilterEnum.Ignore };
            row.AddThemeConstantOverride("separation", 4);
            _rows.AddChild(row);
            string maximum = _rules is MinionRulesSnapshot rules ? $" / {rules.MaxSlots}" : "";
            string experience = _experienceRule is MinionExperienceRuleSnapshot xp
                ? "\n" + Lang.F("경험치 {0}%", xp.PercentForCount(lane.UnitTypes.Length)) : "";
            var name = Text($"{LaneName(index)}\n{lane.UnitTypes.Length}{maximum}{experience}", 12, "cbd6c5");
            name.Name = "Name";
            name.CustomMinimumSize = new Vector2(80, 48);
            name.VerticalAlignment = VerticalAlignment.Center;
            if (_experienceRule is MinionExperienceRuleSnapshot rule)
            {
                name.MouseFilter = MouseFilterEnum.Pass;
                name.TooltipText = Lang.F("상대 영웅이 미니언 한 기에서 받는 경험치\n기본 {0}칸에서 추가 1칸마다 {1}%p 감소\n다음 웨이브의 이 라인 모든 미니언에 적용",
                    rule.BaseSlots, rule.ReductionPerSlot);
            }
            row.AddChild(name);
            for (int i = 0; i < lane.UnitTypes.Length; i++)
            {
                int slot = i;
                uint type = lane.UnitTypes[i];
                var button = MakeButton($"Slot{i}", "", new Vector2(44, 48));
                button.TooltipText = Lang.F("{0} · {1}번째 생성", MinionName(type), i + 1) + $"\n{Description(type)}\n{SlotActions(type)}";
                button.Disabled = _pendingLane.HasValue || !_rules.HasValue || _options.Count == 0;
                AddPortrait(button, type, 3);
                row.AddChild(button);
                _slotButtons.Add((button, index, false));
                button.SetDragForwarding(
                    Callable.From<Vector2, Variant>(_ => BeginSlotDrag(index, slot, button)),
                    Callable.From<Vector2, Variant, bool>((_, data) => CanMoveSlot(index, slot, data)),
                    Callable.From<Vector2, Variant>((_, data) => MoveSlot(index, slot, data)));
                button.Pressed += () => OpenPicker(index, slot, button);
            }
            var add = MakeButton("Add", "+", new Vector2(44, 48));
            add.AddThemeFontSizeOverride("font_size", 28);
            bool full = _rules is MinionRulesSnapshot limit && lane.UnitTypes.Length >= limit.MaxSlots;
            add.Disabled = full || _pendingLane.HasValue || !_rules.HasValue || _options.Count == 0;
            add.TooltipText = full ? Lang.F("최대 {0}명까지 편성할 수 있습니다", _rules.Value.MaxSlots)
                : Lang.T("미니언 영구 추가\n한 번 구매하면 이후 매 웨이브마다 생성됩니다");
            row.AddChild(add);
            _slotButtons.Add((add, index, true));
            add.Pressed += () => OpenPicker(index, -1, add);
        }
        RefreshPurchases();
    }

    private void OpenPicker(int laneIndex, int slot, Control anchor)
    {
        if (_suppressSlotClick || !_enabled || !IsMinionView || _pendingLane.HasValue || !_rules.HasValue || _options.Count == 0 ||
            !_lanes.TryGetValue(laneIndex, out var lane) || slot >= lane.UnitTypes.Length ||
            slot < 0 && lane.UnitTypes.Length >= _rules.Value.MaxSlots) return;
        // Slots never change type; only a melee slot's one tier-two upgrade opens a picker.
        if (slot >= 0 && !Choices(lane.UnitTypes[slot]).Any())
        {
            ClosePicker();
            SetHint(Lang.T("미니언 종류는 바꿀 수 없습니다 · 근접 미니언만 2티어로 강화"));
            return;
        }
        _pickerLane = laneIndex;
        _pickerSlot = slot;
        _pickerRevision = lane.Revision;
        _pickerAnchor = anchor;
        _pickerTitle.Text = slot < 0 ? Lang.F("{0} · 미니언 추가", LaneName(laneIndex)) : Lang.F("{0} · {1}번 2티어 강화", LaneName(laneIndex), slot + 1);
        ClearChildren(_choices);
        _optionButtons.Clear();
        foreach (Choice option in Choices(slot < 0 ? null : lane.UnitTypes[slot]))
        {
            var button = MakeButton($"Option{option.UnitType}", "", new Vector2(270, 54));
            button.TooltipText = $"{MinionName(option.UnitType)}\n{Description(option.UnitType)}" +
                (option.Upgrade is { } upgrade ? $"\n{Effect(upgrade)}\n" + Lang.F("HQ {0}티어부터 · 한 번 바꾸면 되돌릴 수 없음", upgrade.Tier) : "");
            var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            row.AddThemeConstantOverride("separation", 8);
            button.AddChild(row);
            row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            row.OffsetLeft = row.OffsetTop = 6;
            row.OffsetRight = row.OffsetBottom = -6;
            row.AddChild(new TextureRect { Texture = Portrait(option.UnitType), CustomMinimumSize = new Vector2(42, 42),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = MouseFilterEnum.Ignore });
            var information = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
            information.AddThemeConstantOverride("separation", 0);
            row.AddChild(information);
            information.AddChild(Text(option.Upgrade.HasValue ? Lang.F("{0} · 2티어", MinionName(option.UnitType)) : MinionName(option.UnitType), 13, "ece6cf"));
            var cost = Text("", 12, "e6d59e");
            cost.Name = "Cost";
            information.AddChild(cost);
            _choices.AddChild(button);
            _optionButtons.Add((button, cost, option));
            uint requestedType = option.UnitType;
            button.Pressed += () => Purchase(requestedType);
        }
        _picker.Show();
        RefreshPurchases();
        _picker.ResetSize();
        PositionPicker();
        Callable.From(PositionPicker).CallDeferred();
    }

    private void Purchase(uint type)
    {
        if (!IsPickerOpen || !_enabled || !IsMinionView || _pendingLane.HasValue || !_rules.HasValue ||
            !_lanes.TryGetValue(_pickerLane, out var lane) ||
            lane.Revision != _pickerRevision || _pickerSlot >= lane.UnitTypes.Length ||
            _pickerSlot >= 0 && lane.UnitTypes[_pickerSlot] == type ||
            _pickerSlot < 0 && (lane.UnitTypes.Length >= _rules.Value.MaxSlots || !CanAffordAdd)) return;
        Choice? choice = null;
        foreach (Choice candidate in Choices(_pickerSlot < 0 ? null : lane.UnitTypes[_pickerSlot]))
            if (candidate.UnitType == type) choice = candidate;
        // 슬롯 추가는 마석, 2티어 강화는 서버가 보낸 비용(목재·기름)으로 한다.
        if (choice is not { } option || option.Upgrade is { } upgrade && upgrade.Tier > _teamTier || !Affordable(option)) return;
        string command = _pickerSlot < 0
            ? FormattableString.Invariant($"MINION_ADD {_pickerLane} {lane.Revision} {type}")
            : FormattableString.Invariant($"MINION_SET {_pickerLane} {lane.Revision} {_pickerSlot} {type}");
        _pendingLane = _pickerLane;
        _pendingRevision = lane.Revision;
        ClosePicker();
        SetHint(Lang.T("편성 변경 요청 중…"));
        RefreshPurchases();
        CommandRequested?.Invoke(command);
    }

    private void RefreshPurchases()
    {
        if (!_ready) return;
        foreach (var (button, laneIndex, add) in _slotButtons)
        {
            bool full = add && _rules is MinionRulesSnapshot rules &&
                _lanes.TryGetValue(laneIndex, out var lane) && lane.UnitTypes.Length >= rules.MaxSlots;
            button.Disabled = full || (add && !CanAffordAdd) || _pendingLane.HasValue || !_rules.HasValue || _options.Count == 0;
            if (add && !full) button.TooltipText = AddCost is not { } price ? Lang.T("슬롯 추가 가격을 받는 중")
                : CanAffordAdd ? Lang.F("{0} {1:N0}으로 원하는 미니언을 이 라인에 영구 추가합니다\n보유 {0} {2:N0}",
                    ResourceName(price.ResourceId), price.Amount, MagicStones)
                : Lang.F("{0} {1:N0}이 필요합니다 (보유 {2:N0})\n중앙 마석·정글 큰 금덩어리를 수집하거나 적 요새를 무너뜨리면 얻습니다",
                    ResourceName(price.ResourceId), price.Amount, MagicStones);
        }
        if (!IsPickerOpen || !_lanes.TryGetValue(_pickerLane, out var selected)) return;
        foreach (var (button, label, option) in _optionButtons)
        {
            bool affordable = Affordable(option);
            string price = string.Join(" · ", option.Costs.Select(cost => $"{ResourceName(cost.Resource)} {cost.Amount:N0}"));
            int? lockedTier = option.Upgrade is { } upgrade && upgrade.Tier > _teamTier ? upgrade.Tier : null;
            label.Text = lockedTier is int tier ? Lang.F("HQ {0}티어 필요 · {1}", tier, price)
                : option.Costs.Length == 0 ? Lang.T("가격 받는 중")
                : price + (affordable ? "" : " · " + Lang.T("부족"));
            label.AddThemeColorOverride("font_color", new Color(lockedTier.HasValue ? "a8b9b1" : affordable ? "e6d59e" : "ed9785"));
            button.Disabled = lockedTier.HasValue || !affordable || _pendingLane.HasValue || selected.Revision != _pickerRevision;
        }
    }

    private void RefreshStoneLabel()
    {
        if (_stoneLabel == null) return;
        _stoneLabel.Visible = AddCost.HasValue;
        if (AddCost is not { } price) return;
        _stoneLabel.Text = $"{ResourceName(price.ResourceId)} {MagicStones:N0}";
        _stoneLabel.TooltipText = Lang.F("슬롯 추가 1번에 {0} {1:N0}", ResourceName(price.ResourceId), price.Amount);
    }

    private Variant BeginSlotDrag(int laneIndex, int slot, Button source)
    {
        if (!_enabled || !IsMinionView || !IsVisibleInTree() || source.Disabled || _pendingLane.HasValue ||
            !_lanes.TryGetValue(laneIndex, out var lane) || slot >= lane.UnitTypes.Length) return default;
        ClosePicker();
        _draggingSlot = _suppressSlotClick = true;
        var preview = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        preview.AddChild(new TextureRect { Texture = Portrait(lane.UnitTypes[slot]),
            CustomMinimumSize = new Vector2(48, 48), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore });
        preview.AddChild(Text(Lang.F("{0} · 순서 이동", MinionName(lane.UnitTypes[slot])), 13, "ece6cf"));
        source.SetDragPreview(preview);
        SetHint("");
        return new Godot.Collections.Dictionary {
            ["panel"] = GetInstanceId(), ["lane"] = laneIndex, ["revision"] = lane.Revision, ["slot"] = slot
        };
    }

    private bool CanMoveSlot(int laneIndex, int destination, Variant data)
    {
        if (!_enabled || !IsMinionView || !IsVisibleInTree() || _pendingLane.HasValue ||
            !_lanes.TryGetValue(laneIndex, out var lane) || destination < 0 || destination >= lane.UnitTypes.Length ||
            data.VariantType != Variant.Type.Dictionary) return false;
        var payload = data.AsGodotDictionary();
        foreach (string key in new[] { "panel", "lane", "revision", "slot" })
            if (!payload.TryGetValue(key, out var value) || value.VariantType != Variant.Type.Int) return false;
        long source = payload["slot"].AsInt64();
        return payload["panel"].AsUInt64() == GetInstanceId() && payload["lane"].AsInt64() == laneIndex &&
            payload["revision"].AsInt64() == lane.Revision && source >= 0 && source < lane.UnitTypes.Length && source != destination;
    }

    private void MoveSlot(int laneIndex, int destination, Variant data)
    {
        if (!CanMoveSlot(laneIndex, destination, data)) return;
        var lane = _lanes[laneIndex];
        int source = data.AsGodotDictionary()["slot"].AsInt32();
        _draggingSlot = false; // A synchronous preview reply may rebuild the row during this callback.
        _pendingLane = laneIndex;
        _pendingRevision = lane.Revision;
        ClosePicker();
        SetHint(Lang.T("순서 변경 요청 중…"));
        RefreshPurchases();
        CommandRequested?.Invoke(FormattableString.Invariant($"MINION_MOVE {laneIndex} {lane.Revision} {source} {destination}"));
    }

    private void CancelSlotDrag()
    {
        if (!_draggingSlot || !IsInsideTree()) return;
        _consumeDragRelease = true;
        GetViewport().GuiCancelDrag();
        _draggingSlot = false;
    }

    public override void _Notification(int what)
    {
        if (what != NotificationDragEnd || !_suppressSlotClick) return;
        _draggingSlot = false;
        // The drop release must not also open the purchase picker.
        Callable.From(() => _suppressSlotClick = false).CallDeferred();
        if (!_pendingLane.HasValue) SetHint("");
    }

    public void ClosePicker()
    {
        _picker?.Hide();
        _pickerAnchor = null;
    }

    private void PositionPicker()
    {
        if (!IsPickerOpen || !GodotObject.IsInstanceValid(_pickerAnchor)) return;
        Rect2 anchor = _pickerAnchor.GetGlobalRect();
        Rect2 viewport = GetViewportRect();
        Vector2 size = _picker.GetCombinedMinimumSize();
        _picker.Size = size;
        float x = Mathf.Clamp(anchor.Position.X, viewport.Position.X + 6, Mathf.Max(6, viewport.End.X - size.X - 6));
        float y = Mathf.Max(viewport.Position.Y + 6, anchor.Position.Y - size.Y - 6);
        _picker.GlobalPosition = new Vector2(x, y);
    }

    public bool IsPointerOverUi(Vector2 viewportPosition) => _ready && _enabled && IsVisibleInTree() &&
        (_defaultTab.GetGlobalRect().HasPoint(viewportPosition) || _minionTab.GetGlobalRect().HasPoint(viewportPosition) ||
         IsMinionView && _body.GetGlobalRect().HasPoint(viewportPosition) ||
         IsPickerOpen && _picker.GetGlobalRect().HasPoint(viewportPosition));

    public override void _Input(InputEvent input)
    {
        if (_consumeDragRelease && input is InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Left })
        {
            _consumeDragRelease = false;
            GetViewport().SetInputAsHandled();
            return;
        }
        if (_draggingSlot &&
            (input is InputEventKey { Pressed: true } dragKey && (dragKey.Keycode == Key.Escape || dragKey.PhysicalKeycode == Key.Escape) ||
             input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right }))
        {
            CancelSlotDrag();
            if (input is InputEventMouseButton) _consumedRelease = MouseButton.Right;
            GetViewport().SetInputAsHandled();
            return;
        }
        if (input is InputEventMouseButton { Pressed: false } release && _consumedRelease == release.ButtonIndex)
        {
            _consumedRelease = null;
            GetViewport().SetInputAsHandled();
            return;
        }
        if (!IsPickerOpen || !_enabled || !IsVisibleInTree()) return;
        if (input is InputEventKey { Pressed: true } key && (key.Keycode == Key.Escape || key.PhysicalKeycode == Key.Escape))
        {
            ClosePicker();
            GetViewport().SetInputAsHandled();
        }
        else if (input is InputEventMouseButton { Pressed: true } mouse &&
                 mouse.ButtonIndex is MouseButton.Left or MouseButton.Right && !_picker.GetGlobalRect().HasPoint(mouse.Position))
        {
            // 바깥 클릭은 닫기에만 쓴다. 유닛 선택/이동 명령으로 전달하지 않는다.
            ClosePicker();
            _consumedRelease = mouse.ButtonIndex;
            GetViewport().SetInputAsHandled();
        }
    }

    private string LaneName(int index) => index >= 0 && index < _laneNames.Count && !string.IsNullOrWhiteSpace(_laneNames[index])
        ? _laneNames[index] : Lang.F("라인 {0}", index + 1);
    private static string ResourceName(uint resource) => resource switch
    {
        0 => Lang.T("목재"), 2 => Lang.T("기름"), 3 => Lang.T("마석"), _ => Lang.F("자원 {0}", resource)
    };
    private static string MinionName(uint type) => UnitCatalog.IsMinion(type) ? UnitCatalog.Name(type) : Lang.F("미니언 {0}", type);
    private static string Description(uint type) => type switch
    {
        100 => Lang.T("앞에서 적을 막는 근접 전투원"), 101 => Lang.T("뒤에서 공격하는 원거리 전투원"),
        102 => Lang.T("사망 시 주변 아군 최대 HP 10% 회복"),
        103 => Lang.T("등에 진 화로로 주변 적 유닛을 계속 태우는 근접 전투원"),
        104 => Lang.T("주변 아군 영웅·병력이 받는 피해 일부를 대신 받는 근접 전투원"),
        105 => Lang.T("맞힌 화살의 전기가 주변 적에게 튕기는 원거리 전투원"), _ => Lang.T("라인을 따라 전진하는 미니언")
    };

    // Effect numbers come from MINION_UPGRADE; the server applies the effect.
    private static string Effect(MinionUpgradeSnapshot upgrade) => upgrade.UnitType switch
    {
        103 => Lang.F("주변 {0:0.#}m의 적 유닛에게 1초마다 자기 최대 체력의 {1:0.#}% 피해 (건물·일꾼·정글몹 제외)", upgrade.Radius, upgrade.Percent),
        104 => Lang.F("주변 {0:0.#}m 아군 영웅·병력이 받는 피해의 {1:0.#}%를 대신 받음 (여러 기여도 가장 가까운 한 기만)", upgrade.Radius, upgrade.Percent),
        105 => Lang.F("공격이 맞으면 {0:0.#}m 안의 다른 적에게 공격력의 {1:0.#}%, 다시 {2:0.#}%로 튕김 (소수점 내림·최소 1, 이미 맞은 적 제외, 건물·정글몹 제외)",
            upgrade.Radius, upgrade.Percent, upgrade.Percent * upgrade.Percent / 100),
        _ => ""
    };

    // A picker entry: a minion a new slot starts as (paid in 마석), or the tier-two upgrade of
    // the clicked slot's type.
    private readonly record struct Choice(uint UnitType, MinionCost[] Costs, MinionUpgradeSnapshot? Upgrade);

    private IEnumerable<Choice> Choices(uint? slotType)
    {
        if (slotType is not uint current)
        {
            MinionCost[] slot = AddCost is { } price ? new[] { new MinionCost(price.ResourceId, price.Amount) } : Array.Empty<MinionCost>();
            foreach (MinionOptionSnapshot option in _options.Values) yield return new(option.UnitType, slot, null);
            yield break;
        }
        foreach (MinionUpgradeSnapshot upgrade in _upgrades.Values)
            if (upgrade.FromType == current) yield return new(upgrade.UnitType, upgrade.Costs, upgrade);
    }

    private bool Affordable(Choice choice) => choice.Costs.Length > 0 &&
        choice.Costs.All(cost => (_stock.TryGetValue(cost.Resource, out long amount) ? amount : 0) >= cost.Amount);

    private string SlotActions(uint slotType)
    {
        string drag = Lang.T("드래그: 같은 라인에서 무료 순서 이동");
        var names = Choices(slotType).Select(choice => MinionName(choice.UnitType)).ToList();
        if (names.Count == 0) return drag;
        int tier = Choices(slotType).Max(choice => choice.Upgrade?.Tier ?? 0);
        string upgrades = string.Join("·", names);
        return (tier > _teamTier ? Lang.F("HQ {0}티어부터 클릭: {1}으로 2티어 강화", tier, upgrades) : Lang.F("클릭: {0}으로 2티어 강화", upgrades)) +
            " · " + drag;
    }

    private Texture2D Portrait(uint type)
    {
        if (_portraits.TryGetValue(type, out var texture)) return texture;
        string path = type switch
        {
            100 => "res://ui/portraits/minion-knight.png", 101 => "res://ui/portraits/minion-archer.png",
            102 => "res://ui/icons/minion-healer.svg", 103 => "res://ui/portraits/minion-furnace.png",
            104 => "res://ui/portraits/minion-warden.png", 105 => "res://ui/portraits/minion-arc.png", _ => "res://ui/icons/resource.svg"
        };
        texture = GD.Load<Texture2D>(path);
        _portraits[type] = texture;
        return texture;
    }

    private void AddPortrait(Control parent, uint type, int margin)
    {
        var icon = new TextureRect { Name = "Portrait", Texture = Portrait(type),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore };
        parent.AddChild(icon);
        icon.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        icon.OffsetLeft = icon.OffsetTop = margin;
        icon.OffsetRight = icon.OffsetBottom = -margin;
    }

    private Button MakeButton(string name, string text, Vector2 minimum)
    {
        var button = new Button { Name = name, Text = text, CustomMinimumSize = minimum, FocusMode = FocusModeEnum.None,
            MouseFilter = MouseFilterEnum.Stop, MouseForcePassScrollEvents = false, MouseDefaultCursorShape = CursorShape.PointingHand };
        button.AddThemeStyleboxOverride("normal", _normal);
        button.AddThemeStyleboxOverride("hover", _hover);
        button.AddThemeStyleboxOverride("pressed", _pressed);
        button.AddThemeStyleboxOverride("disabled", _disabled);
        button.AddThemeColorOverride("font_color", new Color("ece6cf"));
        button.AddThemeFontSizeOverride("font_size", 14);
        return button;
    }

    private static void ConsumeMouse(Control control) => control.GuiInput += input =>
    {
        if (input is InputEventMouse) control.AcceptEvent();
    };

    private static Label Text(string value, int size, string color)
    {
        var label = new Label { Text = value, MouseFilter = MouseFilterEnum.Ignore,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", new Color(color));
        return label;
    }

    private void SetHint(string text, bool error = false)
    {
        if (!_ready) return;
        _hint.Text = text;
        _hint.Visible = text.Length > 0;
        _hint.AddThemeColorOverride("font_color", new Color(error ? "ed9785" : "a8b9b1"));
    }

    private StyleBoxFlat FrameStyle(string background, string border, int horizontalMargin, int verticalMargin)
    {
        var style = Style(background, border, 4);
        style.ContentMarginLeft = style.ContentMarginRight = horizontalMargin;
        style.ContentMarginTop = style.ContentMarginBottom = verticalMargin;
        return style;
    }

    private StyleBoxFlat Style(string background, string border, int radius)
    {
        var style = new StyleBoxFlat { BgColor = new Color(background), BorderColor = new Color(border),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius };
        _styles.Add(style);
        return style;
    }

    private static void ClearChildren(Node parent)
    {
        foreach (Node child in parent.GetChildren()) { parent.RemoveChild(child); child.QueueFree(); }
    }

    public override void _ExitTree()
    {
        Resized -= PositionPicker;
        foreach (StyleBoxFlat style in _styles) style.Dispose();
        _styles.Clear();
        _portraits.Clear();
    }
}
