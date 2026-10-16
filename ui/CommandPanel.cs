using Godot;
using System;
using System.Collections.Generic;

// 선택한 일꾼의 건설 메뉴, HQ·인력 사무소의 생산 버튼, 조병창 연구 버튼을 표시합니다. 생산 진행도는 정보 패널에 표시합니다.
public partial class CommandPanel : PanelContainer
{
    [Export] public UnitManager Units;
    [Export] public BuildingManager Buildings;

    public event Action<uint> BuildRequested;
    public event Action BuildMenuOpened;
    public event Action AttackRequested;
    public event Action StopRequested;
    public event Action HoldRequested;
    public event Action SiegeRequested;
    public bool IsBuildMenuOpen => _buildMenu;
    private enum DisplayMode { Unset, Empty, Units, Workers, BuildMenu, TownHall, Barracks, Workshop, Forge, ResearchLab, Construction }
    private static readonly int[] NumpadOrder = { 7, 8, 9, 4, 5, 6, 1, 2, 3 };
    private readonly Button[] _slots = new Button[9];
    private readonly List<StyleBoxFlat> _styles = new();
    private readonly HashSet<Key> _heldMenuKeys = new();
    private DisplayMode _display;
    private bool _buildMenu;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        MouseForcePassScrollEvents = false;
        var frame = Style("18232ef5", "65776a");
        frame.CornerRadiusTopLeft = 6;
        frame.ContentMarginLeft = frame.ContentMarginTop = frame.ContentMarginRight = frame.ContentMarginBottom = 6;
        AddThemeStyleboxOverride("panel", frame);

        var empty = Style("101a23", "2a3940");
        var normal = Style("22332d", "728a77");
        var hover = Style("2d463a", "a1b897");
        var pressed = Style("365543", "b6d3aa");
        var grid = new GridContainer { Name = "Slots", Columns = 3 };
        grid.AddThemeConstantOverride("h_separation", 6);
        grid.AddThemeConstantOverride("v_separation", 6);
        AddChild(grid);
        for (int i = 0; i < NumpadOrder.Length; i++)
        {
            var slot = new Button
            {
                Name = $"Slot{NumpadOrder[i]}", Text = "", Disabled = true,
                CustomMinimumSize = new Vector2(64, 64),
                SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill,
                FocusMode = FocusModeEnum.None, MouseFilter = MouseFilterEnum.Stop,
                MouseForcePassScrollEvents = false
            };
            slot.AddThemeStyleboxOverride("normal", normal);
            slot.AddThemeStyleboxOverride("hover", hover);
            slot.AddThemeStyleboxOverride("pressed", pressed);
            slot.AddThemeStyleboxOverride("disabled", empty);
            slot.AddThemeFontSizeOverride("font_size", 14);
            slot.AddThemeColorOverride("font_color", new Color("f2e8cb"));
            slot.AddThemeColorOverride("font_hover_color", new Color("fff4d6"));
            slot.AddThemeColorOverride("font_pressed_color", new Color("fff4d6"));
            slot.AddThemeColorOverride("font_disabled_color", new Color("a8b8bd"));
            int number = NumpadOrder[i];
            slot.Pressed += () => RequestSlot(number);
            grid.AddChild(slot);
            _slots[i] = slot;
        }
        if (GodotObject.IsInstanceValid(Units))
        {
            Units.SelectionChanged += OnSelectionChanged;
            Units.CommandStateChanged += RefreshProduction;
        }
        if (GodotObject.IsInstanceValid(Buildings))
        {
            Buildings.SelectionChanged += OnSelectionChanged;
            Buildings.ProductionChanged += RefreshProduction;
            Buildings.RequirementsChanged += RefreshProduction;
        }
        GetWindow().FocusExited += OnFocusExited;
        RefreshSelection();
    }

    private StyleBoxFlat Style(string background, string border)
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(background), BorderColor = new Color(border),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1
        };
        _styles.Add(style);
        return style;
    }

    private void RefreshSelection()
    {
        DisplayMode next = DisplayMode.Empty;
        if (GodotObject.IsInstanceValid(Units) && Units.LocalTeam != 0)
        {
            if (Units.SelectedUnitIds.Count > 0) next = Units.TryGetSelectedWorker(out _) ? DisplayMode.Workers : DisplayMode.Units;
            else if (Units.InspectedUnit == null && GodotObject.IsInstanceValid(Buildings))
            {
                Building selected = Buildings.SelectedBuilding;
                if (GodotObject.IsInstanceValid(selected) && !selected.IsQueuedForDeletion() &&
                    selected.SideId == Units.LocalTeam)
                    next = selected.IsUnderConstruction ? Units.CanCancelConstruction(selected) ? DisplayMode.Construction : DisplayMode.Empty : selected.BuildingType switch
                    {
                        BuildingCatalog.TownHall => DisplayMode.TownHall,
                        BuildingCatalog.Barracks => DisplayMode.Barracks,
                        BuildingCatalog.Workshop => DisplayMode.Workshop,
                        BuildingCatalog.Forge => DisplayMode.Forge,
                        BuildingCatalog.ResearchLab => DisplayMode.ResearchLab,
                        _ => DisplayMode.Empty
                    };
            }
        }
        if (next != DisplayMode.Workers) SetMenuOpen(false);
        else if (_buildMenu) next = DisplayMode.BuildMenu;
        if (next == _display) { RefreshProduction(); return; }
        _display = next;
        for (int i = 0; i < _slots.Length; i++)
        {
            string text = (next, NumpadOrder[i]) switch
            {
                (DisplayMode.Units or DisplayMode.Workers, 4) => "공격(A)",
                (DisplayMode.Units or DisplayMode.Workers, 5) => "정지(S)",
                (DisplayMode.Units or DisplayMode.Workers, 6) => "홀드(D)",
                (DisplayMode.Workers, 1) => "건설(Z)",
                (DisplayMode.BuildMenu, 7) => "브랜치\n(Q)",
                (DisplayMode.BuildMenu, 8) => "숙소\n(W)",
                (DisplayMode.BuildMenu, 9) => "시추탑\n(E)",
                (DisplayMode.BuildMenu, 4) => "인력\n사무소\n(A)",
                (DisplayMode.BuildMenu, 5) => "조병창\n(S)",
                (DisplayMode.BuildMenu, 6) => "감시탑\n(D)",
                (DisplayMode.BuildMenu, 1) => "공장\n(Z)",
                (DisplayMode.BuildMenu, 2) => "기술연구소\n(X)",
                (DisplayMode.BuildMenu, 3) => "뒤로(C)",
                (DisplayMode.TownHall, 7) => "일꾼",
                (DisplayMode.TownHall, 1) => "업그레이드\n(Z)",
                (DisplayMode.Barracks, 7) => UnitCatalog.Name(UnitCatalog.Knight),
                (DisplayMode.Barracks, 8) => UnitCatalog.Name(UnitCatalog.Archer),
                (DisplayMode.Barracks, 9) => "치유사",
                (DisplayMode.Workshop, 7) => "화염기계\n(Q)",
                (DisplayMode.Workshop, 8) => "증기포격차\n(W)",
                (DisplayMode.Forge, 7) => "공격력 연구\n(Q)",
                (DisplayMode.Forge, 8) => "체력 연구\n(W)",
                (DisplayMode.ResearchLab, 7) => "와이어 돌진\n(Q)",
                (DisplayMode.Construction, 3) => "건설 취소\n(Esc)",
                _ => ""
            };
            _slots[i].Text = text;
            _slots[i].Disabled = text.Length == 0;
            _slots[i].TooltipText = "";
        }
        RefreshProduction();
        if (next is DisplayMode.Units or DisplayMode.Workers)
        {
            _slots[3].TooltipText = "A: 공격 사거리 표시 · 적 클릭: 공격 · 지형 클릭: 공격 이동 · Esc: 취소";
            _slots[4].TooltipText = "현재 작업 중단 · 주변 적을 추격하고 제자리로 복귀";
            _slots[5].TooltipText = "현재 작업 중단 · 자리를 지키며 사거리 안의 적만 공격";
        }
    }

    private void RefreshProduction()
    {
        if (_display == DisplayMode.Units)
        {
            bool available = Units.HasSelectedArtillery;
            bool allowed = Units.TryGetSiegeToggle(out bool deploy, out string reason);
            _slots[6].Text = available ? allowed ? (deploy ? "공성 전개\n(Z)" : "전개 해제\n(Z)") : "공성 전환\n(Z)" : "";
            _slots[6].Disabled = !allowed;
            _slots[6].TooltipText = available ? reason ?? "선택한 내 증기포격차만 전환 · 전환 중 이동·공격 불가" : "";
            return;
        }
        if (_display == DisplayMode.BuildMenu)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                uint type = BuildingTypeForSlot(NumpadOrder[i]);
                if (!BuildingCatalog.IsPlayerBuildable(type)) continue;
                string reason = Buildings.BuildingRequirementBlockReason(type);
                _slots[i].Disabled = reason != null;
                _slots[i].TooltipText = reason ?? (type == BuildingCatalog.OilDerrick ? "유전 지대에만 건설 · 유전마다 1개" : "");
            }
            return;
        }
        if (_display == DisplayMode.Construction)
        {
            _slots[8].Disabled = !Units.CanCancelConstruction(Buildings.SelectedBuilding);
            _slots[8].TooltipText = "공사를 취소하고 지불 비용의 75% 반환 (소수점 버림)";
            return;
        }
        if (_display == DisplayMode.Forge)
        {
            RefreshResearch(_slots[0], true);
            RefreshResearch(_slots[1], false);
            return;
        }
        if (_display == DisplayMode.ResearchLab)
        {
            RefreshKnightResearch(_slots[0]);
            return;
        }
        if (_display is not (DisplayMode.TownHall or DisplayMode.Barracks or DisplayMode.Workshop)) return;
        Building producer = Buildings.SelectedBuilding;
        if (!GodotObject.IsInstanceValid(producer)) return;
        int count = producer.ProductionQueue.Count;
        bool canCancel = producer.ProductionJobs.Count > 0 && Units.CanCancelTraining(producer, producer.ProductionJobs[0].Id);
        _slots[8].Text = canCancel ? "생산 취소\n(Esc)" : "";
        _slots[8].Disabled = !canCancel;
        _slots[8].TooltipText = canCancel ? "현재 생산을 취소하고 지불 비용의 75% 반환 · 대기 항목은 정보 패널에서 클릭" : "";
        _slots[0].Disabled = count >= ProductionSnapshot.MaxQueue || Buildings.IsTownHallUpgrading(producer);
        const string rallyHint = "\n지형 우클릭: 랠리 지정 · 선택한 건물 우클릭: 랠리 해제";
        if (_display == DisplayMode.Workshop)
        {
            RefreshWorkshopTraining(_slots[0], producer, UnitCatalog.FlameAutomaton, rallyHint);
            RefreshWorkshopTraining(_slots[1], producer, UnitCatalog.SteamArtillery, rallyHint);
            return;
        }
        _slots[0].TooltipText = (_display == DisplayMode.TownHall ? "나무 100 · 인구 1 · 생산 9초\n나무 우클릭: 새 일꾼 자동 채집" : "나무 200 · 인구 2 · 생산 12초") + rallyHint;
        if (_display == DisplayMode.TownHall)
        {
            var state = Buildings.TeamTier(producer.SideId);
            string reason = Units.TownHallUpgradeBlockReason(producer);
            _slots[6].Disabled = reason != null;
            _slots[6].Text = state.Upgrading ? $"{state.TargetTier}티어 강화\n{state.Remaining:0}초" :
                state.Tier >= 3 ? "3티어\n최고 단계" : $"{state.Tier + 1}티어\n업그레이드(Z)";
            string terms = state.HasUpgradeTerms ? $"나무 {state.NextWood:N0} · 기름 {state.NextOil:N0} · {state.NextSeconds:0.##}초\n" : "";
            _slots[6].TooltipText = state.Tier >= 3 ? "최고 단계" : terms +
                "HQ·요새·감시탑 최대 체력과 공격력: 기본값의 +20%\n팀의 다음 티어 영웅 장비 해금" + (reason == null ? "" : "\n" + reason);
            if (Buildings.IsTownHallUpgrading(producer)) _slots[0].TooltipText = "HQ 업그레이드 중에는 일꾼을 생산할 수 없습니다.";
        }
        if (_display == DisplayMode.Barracks)
        {
            _slots[1].Disabled = count >= ProductionSnapshot.MaxQueue;
            _slots[1].TooltipText = "나무 200 · 인구 2 · 생산 12초" + rallyHint;
            _slots[2].Disabled = count >= ProductionSnapshot.MaxQueue;
            _slots[2].TooltipText = "나무 200 · 인구 2 · 생산 15초\n" + UnitCatalog.Description(UnitCatalog.Medic) + rallyHint;
        }
        ApplyUnitRequirement(_slots[0], _display == DisplayMode.TownHall ? UnitCatalog.Worker : UnitCatalog.Knight);
        if (_display == DisplayMode.Barracks)
        {
            ApplyUnitRequirement(_slots[1], UnitCatalog.Archer);
            ApplyUnitRequirement(_slots[2], UnitCatalog.Medic);
        }
    }

    private void RefreshWorkshopTraining(Button slot, Building producer, uint type, string rallyHint)
    {
        string reason = !Units.CommanderControl || Units.Spectating ? "지휘관만 생산할 수 있습니다" :
            producer.ProductionQueue.Count >= ProductionSnapshot.MaxQueue ? "생산 대기열 가득 참" :
            Buildings.UnitRequirementBlockReason(type) ?? Buildings.TrainingTermsBlockReason(producer, type);
        slot.Disabled = reason != null;
        string terms = Buildings.TrainingTerms(type) is TrainingDefinition definition
            ? $"목재 {definition.Wood:N0} · 기름 {definition.Oil:N0} · 인구 {definition.Supply} · {definition.Seconds:0.##}초\n" : "";
        slot.TooltipText = terms + UnitCatalog.Description(type) + (reason == null ? "" : "\n" + reason) + rallyHint;
    }

    // Terms and progress come from ARMY_UPGRADE; the client copies no research numbers.
    private void RefreshResearch(Button slot, bool attack)
    {
        Building forge = Buildings.SelectedBuilding;
        if (!GodotObject.IsInstanceValid(forge)) return;
        var state = Buildings.ArmyUpgrade(forge.SideId);
        var research = state.Research(attack);
        int level = state.Level(attack);
        string name = attack ? "공격력" : "체력";
        string key = attack ? "Q" : "W";
        string reason = Units.ArmyUpgradeBlockReason(forge, attack);
        slot.Disabled = reason != null;
        slot.Text = research.Researching ? $"{name} {research.TargetLevel}단계\n{research.Remaining}초" :
            level >= ArmyUpgradeSnapshot.MaxLevel ? $"{name}\n최고 단계" : $"{name} {level + 1}단계\n({key})";
        string terms = research.NextWood > 0 ? $"나무 {research.NextWood:N0} · {research.NextSeconds}초\n" : "";
        slot.TooltipText = terms + $"미니언·용병의 기본 {name} +{state.StepPercent}% (일꾼·영웅 제외) · 현재 {level}/{ArmyUpgradeSnapshot.MaxLevel}단계" +
            (reason == null ? "" : "\n" + reason);
    }

    private void ApplyUnitRequirement(Button slot, uint type)
    {
        if (Buildings.UnitRequirementBlockReason(type) is not string reason) return;
        slot.Disabled = true;
        slot.TooltipText = reason + "\n" + slot.TooltipText;
    }

    private void RefreshKnightResearch(Button slot)
    {
        Building lab = Buildings.SelectedBuilding;
        if (!GodotObject.IsInstanceValid(lab)) return;
        string reason = Units.KnightResearchBlockReason(lab);
        slot.Disabled = reason != null;
        var state = Buildings.KnightResearch(lab.SideId);
        slot.Text = state is { Unlocked: true } ? "와이어 돌진\n완료" :
            state is { Researching: true } research ? $"와이어 돌진\n{research.Remaining:0}초" : "와이어 돌진\n(Q)";
        string terms = state is KnightResearchSnapshot s
            ? $"목재 {s.Wood:N0} · 기름 {s.Oil:N0} · {s.ResearchSeconds:0.##}초\n" + KnightResearchDescription(s) : "";
        slot.TooltipText = terms + (reason == null ? "" : "\n" + reason);
    }

    public static string KnightResearchDescription(KnightResearchSnapshot state) =>
        $"아군 {UnitCatalog.Name(UnitCatalog.Knight)} 전체에 자동 돌진 해금 · 이후 생산한 병력에도 적용\n" +
        $"적 유닛 추격 중 {state.TriggerRange:0.##}m 이내에서 자동 발동 · 이동 속도 +{(state.SpeedMultiplier - 1) * 100:0.##}% · 재사용 {state.CooldownSeconds:0.##}초";

    private static uint BuildingTypeForSlot(int number) => number switch
    {
        7 => BuildingCatalog.Store, 8 => BuildingCatalog.Supply, 9 => BuildingCatalog.OilDerrick,
        4 => BuildingCatalog.Barracks, 5 => BuildingCatalog.Forge, 6 => BuildingCatalog.Tower,
        1 => BuildingCatalog.Workshop, 2 => BuildingCatalog.ResearchLab, _ => uint.MaxValue
    };

    private void RequestSlot(int number)
    {
        RefreshSelection();
        int index = Array.IndexOf(NumpadOrder, number);
        if (index < 0 || _slots[index].Disabled) return;
        if (_display is DisplayMode.Units or DisplayMode.Workers && number is 4 or 5 or 6)
        {
            if (number == 4) AttackRequested?.Invoke();
            else if (number == 5) StopRequested?.Invoke();
            else HoldRequested?.Invoke();
        }
        else if (_display == DisplayMode.Construction && number == 3) Units.RequestCancelConstruction(Buildings.SelectedBuilding);
        else if (_display == DisplayMode.Units && number == 1)
        {
            SiegeRequested?.Invoke();
            Units.RequestSiegeToggle();
        }
        else if (_display is DisplayMode.TownHall or DisplayMode.Barracks or DisplayMode.Workshop && number == 3)
        {
            Building producer = Buildings.SelectedBuilding;
            if (producer.ProductionJobs.Count > 0) Units.RequestCancelTraining(producer, producer.ProductionJobs[0].Id);
        }
        else if (_display == DisplayMode.TownHall && number == 7) Units.RequestTrainWorker();
        else if (_display == DisplayMode.TownHall && number == 1) Units.RequestUpgradeTownHall();
        else if (_display == DisplayMode.Forge && number is 7 or 8) Units.RequestUpgradeArmy(number == 7);
        else if (_display == DisplayMode.ResearchLab && number == 7) Units.RequestKnightResearch();
        else if (_display == DisplayMode.Barracks && number is 7 or 8 or 9)
            Units.RequestTrain(number == 7 ? UnitCatalog.Knight : number == 8 ? UnitCatalog.Archer : UnitCatalog.Medic);
        else if (_display == DisplayMode.Workshop && number is 7 or 8)
            Units.RequestTrain(number == 7 ? UnitCatalog.FlameAutomaton : UnitCatalog.SteamArtillery);
        else if (_display == DisplayMode.Workers && number == 1)
        {
            SetMenuOpen(true);
            RefreshSelection();
            BuildMenuOpened?.Invoke();
        }
        else if (_display == DisplayMode.BuildMenu)
        {
            if (number == 3) { CancelMenu(); return; }
            uint type = BuildingTypeForSlot(number);
            if (!BuildingCatalog.IsPlayerBuildable(type)) return;
            // 배치 모드로 들어가기 전에 기본 패널로 돌아갑니다.
            CancelMenu();
            BuildRequested?.Invoke(type);
        }
    }

    public bool TryHandleShortcut(InputEvent @event)
    {
        if (@event is not InputEventKey key) return false;
        Key code = key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode;
        if (!key.Pressed) return _heldMenuKeys.Remove(code);
        // 건물 선택 직후 키 반복으로 A 공격이나 메뉴가 다시 실행되지 않도록 합니다.
        if (_heldMenuKeys.Contains(code)) return true;
        if (key.CtrlPressed || key.AltPressed || key.MetaPressed || key.ShiftPressed) return false;
        RefreshSelection();
        int slot = code switch
        {
            Key.Z when _display is DisplayMode.Workers or DisplayMode.BuildMenu or DisplayMode.TownHall => 1,
            Key.Z when _display == DisplayMode.Units && Units.HasSelectedArtillery => 1,
            Key.Q when _display == DisplayMode.Workshop => 7,
            Key.W when _display == DisplayMode.Workshop => 8,
            Key.Q when _display == DisplayMode.Forge => 7,
            Key.Q when _display == DisplayMode.ResearchLab => 7,
            Key.W when _display == DisplayMode.Forge => 8,
            Key.Q when _buildMenu => 7,
            Key.W when _buildMenu => 8,
            Key.E when _buildMenu => 9,
            Key.A when _buildMenu => 4,
            Key.S when _buildMenu => 5,
            Key.D when _buildMenu => 6,
            Key.X when _buildMenu => 2,
            Key.C when _buildMenu => 3,
            Key.A when _display is DisplayMode.Units or DisplayMode.Workers => 4,
            Key.S when _display is DisplayMode.Units or DisplayMode.Workers => 5,
            Key.D when _display is DisplayMode.Units or DisplayMode.Workers => 6,
            Key.Escape when _buildMenu => 3,
            Key.Escape when _display is DisplayMode.Construction or DisplayMode.TownHall or DisplayMode.Barracks or DisplayMode.Workshop && !_slots[8].Disabled => 3,
            _ => 0
        };
        if (slot == 0) return false;
        if (!key.Echo)
        {
            _heldMenuKeys.Add(code);
            RequestSlot(slot);
        }
        return true;
    }

    // 키 놓기 입력은 UI가 소비하기 전에도 받아야 합니다.
    public void ObserveKeyRelease(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: false } key)
            _heldMenuKeys.Remove(key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode);
    }

    public bool CancelMenu()
    {
        if (!_buildMenu) return false;
        SetMenuOpen(false);
        RefreshSelection();
        return true;
    }

    private void SetMenuOpen(bool open)
    {
        if (_buildMenu == open) return;
        _buildMenu = open;
    }

    private void OnSelectionChanged()
    {
        SetMenuOpen(false);
        RefreshSelection();
    }

    private void OnFocusExited()
    {
        CancelMenu();
        _heldMenuKeys.Clear();
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouse) AcceptEvent();
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(Units))
        {
            Units.SelectionChanged -= OnSelectionChanged;
            Units.CommandStateChanged -= RefreshProduction;
        }
        if (GodotObject.IsInstanceValid(Buildings))
        {
            Buildings.SelectionChanged -= OnSelectionChanged;
            Buildings.ProductionChanged -= RefreshProduction;
            Buildings.RequirementsChanged -= RefreshProduction;
        }
        GetWindow().FocusExited -= OnFocusExited;
        SetMenuOpen(false);
        foreach (StyleBoxFlat style in _styles) style.Dispose();
        _styles.Clear();
    }
}
