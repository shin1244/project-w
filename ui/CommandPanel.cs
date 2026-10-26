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
    // Slots keep their old numpad numbers (7 8 9 / 4 5 6 / 1 2 3, plus 10 11 12) as identities for commands and
    // hotkeys; ArrangeSlots decides where each one sits in the 4x3 grid.
    private static readonly int[] SlotNumbers = { 7, 8, 9, 10, 4, 5, 6, 11, 1, 2, 3, 12 };
    private readonly CommandSlot[] _slots = new CommandSlot[12];
    private readonly List<StyleBoxFlat> _styles = new();
    private readonly HashSet<Key> _heldMenuKeys = new();
    private GridContainer _grid;
    private SelectionPortraits _portraits;
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
        _portraits = new SelectionPortraits { Name = "Portraits" };
        AddChild(_portraits);

        var empty = Style("101a23", "2a3940");
        var normal = Style("22332d", "728a77");
        var hover = Style("2d463a", "a1b897");
        var pressed = Style("365543", "b6d3aa");
        _grid = new GridContainer { Name = "Slots", Columns = 4 };
        _grid.AddThemeConstantOverride("h_separation", 6);
        _grid.AddThemeConstantOverride("v_separation", 6);
        AddChild(_grid);
        for (int i = 0; i < SlotNumbers.Length; i++)
        {
            var slot = new CommandSlot
            {
                Name = $"Slot{SlotNumbers[i]}", Disabled = true, CustomMinimumSize = new Vector2(64, 64),
                SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill,
                FocusMode = FocusModeEnum.None, MouseFilter = MouseFilterEnum.Stop,
                MouseForcePassScrollEvents = false
            };
            slot.AddThemeStyleboxOverride("normal", normal);
            slot.AddThemeStyleboxOverride("hover", hover);
            slot.AddThemeStyleboxOverride("pressed", pressed);
            slot.AddThemeStyleboxOverride("disabled", empty);
            int number = SlotNumbers[i];
            slot.Pressed += () => RequestSlot(number);
            _grid.AddChild(slot);
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

    private CommandSlot Slot(int number) => _slots[Array.IndexOf(SlotNumbers, number)];

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

    private static Texture2D Icon(string name) => GD.Load<Texture2D>($"res://ui/icons/{name}.svg");
    private Texture2D UnitIcon(uint type) => _portraits.GetPortrait(Units.SceneFor(type));
    private Texture2D BuildingIcon(uint type) => _portraits.GetPortrait(Buildings.SceneFor(type));

    // Each command sits on its hotkey's cell (Q W E R / A S D F / Z X C V), which the slot numbers already follow.
    // Production without a grid key takes the top row; cancel (Esc) has no cell of its own and takes the free
    // bottom-right one.
    private void ArrangeSlots(DisplayMode mode)
    {
        bool cancel = mode is DisplayMode.TownHall or DisplayMode.Barracks or DisplayMode.Workshop or DisplayMode.Construction;
        for (int i = 0; i < SlotNumbers.Length; i++)
        {
            int number = SlotNumbers[i];
            if (cancel && number is 3 or 12) number = number == 3 ? 12 : 3;
            _grid.MoveChild(Slot(number), i);
        }
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
        ArrangeSlots(next);
        for (int i = 0; i < _slots.Length; i++)
        {
            (string name, string key, Texture2D icon) = (next, SlotNumbers[i]) switch
            {
                (DisplayMode.Units or DisplayMode.Workers, 4) => ("공격", "A", Icon("command-attack")),
                (DisplayMode.Units or DisplayMode.Workers, 5) => ("정지", "S", Icon("command-stop")),
                (DisplayMode.Units or DisplayMode.Workers, 6) => ("홀드", "D", Icon("command-hold")),
                (DisplayMode.Workers, 1) => ("건설", "Z", Icon("command-build")),
                (DisplayMode.BuildMenu, 3) => ("뒤로", "C", Icon("command-back")),
                (DisplayMode.BuildMenu, int number) when BuildingCatalog.IsPlayerBuildable(BuildingTypeForSlot(number)) =>
                    (BuildingCatalog.Name(BuildingTypeForSlot(number)), BuildKey(number), BuildingIcon(BuildingTypeForSlot(number))),
                (DisplayMode.TownHall, 7) => (UnitCatalog.Name(UnitCatalog.Worker), "", UnitIcon(UnitCatalog.Worker)),
                (DisplayMode.TownHall, 1) => ("업그레이드", "Z", Icon("command-upgrade")),
                (DisplayMode.Barracks, 7) => (UnitCatalog.Name(UnitCatalog.Knight), "", UnitIcon(UnitCatalog.Knight)),
                (DisplayMode.Barracks, 8) => (UnitCatalog.Name(UnitCatalog.Archer), "", UnitIcon(UnitCatalog.Archer)),
                (DisplayMode.Barracks, 9) => (UnitCatalog.Name(UnitCatalog.Medic), "", UnitIcon(UnitCatalog.Medic)),
                (DisplayMode.Workshop, 7) => (UnitCatalog.Name(UnitCatalog.FlameAutomaton), "Q", UnitIcon(UnitCatalog.FlameAutomaton)),
                (DisplayMode.Workshop, 8) => (UnitCatalog.Name(UnitCatalog.SteamArtillery), "W", UnitIcon(UnitCatalog.SteamArtillery)),
                (DisplayMode.Workshop, 9) => (UnitCatalog.Name(UnitCatalog.Refueler), "E", UnitIcon(UnitCatalog.Refueler)),
                (DisplayMode.Forge, 7) => ("공격력 연구", "Q", Icon("research-attack")),
                (DisplayMode.Forge, 8) => ("체력 연구", "W", Icon("research-armor")),
                (DisplayMode.ResearchLab, 7) => ("와이어 돌진", "Q", Icon("research-wire")),
                (DisplayMode.Construction, 3) => ("건설 취소", "Esc", Icon("command-cancel")),
                _ => ("", "", null)
            };
            _slots[i].Set(name, key, icon);
        }
        if (next is DisplayMode.Units or DisplayMode.Workers)
        {
            Slot(4).Detail = "A: 공격 사거리 표시 · 적 클릭: 공격 · 지형 클릭: 공격 이동 · Esc: 취소";
            Slot(5).Detail = "현재 작업 중단 · 주변 적을 추격하고 제자리로 복귀";
            Slot(6).Detail = "현재 작업 중단 · 자리를 지키며 사거리 안의 적만 공격";
        }
        RefreshProduction();
    }

    private static string BuildKey(int number) => number switch
    {
        7 => "Q", 8 => "W", 9 => "E", 4 => "A", 5 => "S", 6 => "D", 1 => "Z", 2 => "X", _ => ""
    };

    private void RefreshProduction()
    {
        UpdateProduction();
        foreach (CommandSlot slot in _slots) slot.Paint();
    }

    private void UpdateProduction()
    {
        if (_display == DisplayMode.Units)
        {
            bool available = Units.HasSelectedArtillery;
            bool allowed = Units.TryGetSiegeToggle(out bool deploy, out string reason);
            if (!available) Slot(1).Set("");
            else Slot(1).Set(allowed ? deploy ? "공성 전개" : "전개 해제" : "공성 전환", "Z",
                Icon(allowed && !deploy ? "siege-pack" : "siege-deploy"));
            Slot(1).Disabled = !allowed;
            Slot(1).Detail = available ? reason ?? "선택한 내 증기포격차만 전환 · 전환 중 이동·공격 불가" : "";
            return;
        }
        if (_display == DisplayMode.BuildMenu)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                uint type = BuildingTypeForSlot(SlotNumbers[i]);
                if (!BuildingCatalog.IsPlayerBuildable(type)) continue;
                string reason = Buildings.BuildingRequirementBlockReason(type);
                _slots[i].Disabled = reason != null;
                _slots[i].Detail = reason ?? (type == BuildingCatalog.OilDerrick ? "유전 지대에만 건설 · 유전마다 1개" : "");
            }
            return;
        }
        if (_display == DisplayMode.Construction)
        {
            Slot(3).Disabled = !Units.CanCancelConstruction(Buildings.SelectedBuilding);
            Slot(3).Detail = "공사를 취소하고 지불 비용의 75% 반환 (소수점 버림)";
            return;
        }
        if (_display == DisplayMode.Forge)
        {
            RefreshResearch(Slot(7), true);
            RefreshResearch(Slot(8), false);
            return;
        }
        if (_display == DisplayMode.ResearchLab)
        {
            RefreshKnightResearch(Slot(7));
            return;
        }
        if (_display is not (DisplayMode.TownHall or DisplayMode.Barracks or DisplayMode.Workshop)) return;
        Building producer = Buildings.SelectedBuilding;
        if (!GodotObject.IsInstanceValid(producer)) return;
        int count = producer.ProductionQueue.Count;
        bool canCancel = producer.ProductionJobs.Count > 0 && Units.CanCancelTraining(producer, producer.ProductionJobs[0].Id);
        if (canCancel) Slot(3).Set("생산 취소", "Esc", Icon("command-cancel"));
        else Slot(3).Set("");
        Slot(3).Detail = canCancel ? "현재 생산을 취소하고 지불 비용의 75% 반환 · 대기 항목은 정보 패널에서 클릭" : "";
        Slot(7).Disabled = count >= ProductionSnapshot.MaxQueue || Buildings.IsTownHallUpgrading(producer);
        const string rallyHint = "\n지형 우클릭: 랠리 지정 · 선택한 건물 우클릭: 랠리 해제";
        if (_display == DisplayMode.Workshop)
        {
            RefreshUnitTraining(Slot(7), producer, UnitCatalog.FlameAutomaton, rallyHint);
            RefreshUnitTraining(Slot(8), producer, UnitCatalog.SteamArtillery, rallyHint);
            RefreshUnitTraining(Slot(9), producer, UnitCatalog.Refueler, rallyHint);
            return;
        }
        if (_display == DisplayMode.Barracks)
        {
            RefreshUnitTraining(Slot(7), producer, UnitCatalog.Knight, rallyHint);
            RefreshUnitTraining(Slot(8), producer, UnitCatalog.Archer, rallyHint);
            RefreshUnitTraining(Slot(9), producer, UnitCatalog.Medic, rallyHint);
            return;
        }
        Slot(7).Detail = "나무 100 · 인구 1 · 생산 9초\n나무 우클릭: 새 일꾼 자동 채집" + rallyHint;
        if (_display == DisplayMode.TownHall)
        {
            var state = Buildings.TeamTier(producer.SideId);
            string reason = Units.TownHallUpgradeBlockReason(producer);
            CommandSlot upgrade = Slot(1);
            if (state.TopTier) upgrade.Set($"{state.Tier}티어", "", Icon("command-upgrade"));
            else upgrade.Set($"{(state.Upgrading ? state.TargetTier : state.Tier + 1)}티어 업그레이드", "Z", Icon("command-upgrade"));
            upgrade.Status = state.Upgrading ? $"{state.Remaining:0}초" : state.TopTier ? "최고 단계" : "";
            upgrade.Disabled = reason != null;
            string terms = state.HasUpgradeTerms ? $"나무 {state.NextWood:N0} · 기름 {state.NextOil:N0} · {state.NextSeconds:0.##}초\n" : "";
            upgrade.Detail = state.TopTier ? "최고 단계" : terms +
                "HQ·요새·감시탑 최대 체력과 공격력: 기본값의 +20%\n팀의 다음 티어 영웅 장비 해금" + (reason == null ? "" : "\n" + reason);
            if (Buildings.IsTownHallUpgrading(producer)) Slot(7).Detail = "HQ 업그레이드 중에는 일꾼을 생산할 수 없습니다.";
        }
        ApplyUnitRequirement(Slot(7), UnitCatalog.Worker);
    }

    // Barracks and workshop buttons: costs and supply come from the server's TRAIN_DEF.
    private void RefreshUnitTraining(CommandSlot slot, Building producer, uint type, string rallyHint)
    {
        string reason = !Units.CommanderControl || Units.Spectating ? "지휘관만 생산할 수 있습니다" :
            producer.ProductionQueue.Count >= ProductionSnapshot.MaxQueue ? "생산 대기열 가득 참" :
            Buildings.UnitRequirementBlockReason(type) ?? Buildings.TrainingTermsBlockReason(producer, type);
        slot.Disabled = reason != null;
        string terms = Buildings.TrainingTerms(type) is TrainingDefinition definition
            ? $"목재 {definition.Wood:N0} · 기름 {definition.Oil:N0} · 인구 {definition.Supply} · {definition.Seconds:0.##}초\n" : "";
        slot.Detail = terms + UnitCatalog.Description(type) + (reason == null ? "" : "\n" + reason) + rallyHint;
    }

    // Terms and progress come from ARMY_UPGRADE; the client copies no research numbers.
    private void RefreshResearch(CommandSlot slot, bool attack)
    {
        Building forge = Buildings.SelectedBuilding;
        if (!GodotObject.IsInstanceValid(forge)) return;
        var state = Buildings.ArmyUpgrade(forge.SideId);
        var research = state.Research(attack);
        int level = state.Level(attack);
        string name = attack ? "공격력" : "체력";
        string reason = Units.ArmyUpgradeBlockReason(forge, attack);
        slot.Disabled = reason != null;
        slot.Status = research.Researching ? $"{research.TargetLevel}단계 {research.Remaining}초" :
            state.MaxLevel > 0 && level >= state.MaxLevel ? "최고 단계" : $"{level + 1}단계";
        string terms = research.NextSeconds > 0 ? $"나무 {research.NextWood:N0} · 기름 {research.NextOil:N0} · {research.NextSeconds}초\n" : "";
        var minionGains = new List<string>();
        foreach (var step in Buildings.MinionUpgradeSteps)
            minionGains.Add($"{MinionShortName(step.UnitType)} +{(attack ? step.Attack : step.Hp):0.##}");
        string minions = minionGains.Count > 0 ? $"미니언 {name} 단계당 {string.Join(" · ", minionGains)}\n" : "";
        slot.Detail = terms + minions + $"용병 기본 {name} +{state.StepPercent}% (일꾼·영웅 제외) · 현재 {level}/{state.MaxLevel}단계" +
            (reason == null ? "" : "\n" + reason);
    }

    private static string MinionShortName(uint type) => type switch
    {
        UnitCatalog.MinionMelee => "근접",
        UnitCatalog.MinionRanged => "원거리",
        UnitCatalog.MinionHealer => "치유",
        UnitCatalog.MinionFurnace => "화로",
        UnitCatalog.MinionWarden => "호위",
        UnitCatalog.MinionArc => "전격",
        _ => UnitCatalog.Name(type)
    };

    private void ApplyUnitRequirement(CommandSlot slot, uint type)
    {
        if (Buildings.UnitRequirementBlockReason(type) is not string reason) return;
        slot.Disabled = true;
        slot.Detail = reason + "\n" + slot.Detail;
    }

    private void RefreshKnightResearch(CommandSlot slot)
    {
        Building lab = Buildings.SelectedBuilding;
        if (!GodotObject.IsInstanceValid(lab)) return;
        string reason = Units.KnightResearchBlockReason(lab);
        slot.Disabled = reason != null;
        var state = Buildings.KnightResearch(lab.SideId);
        slot.Status = state is { Unlocked: true } ? "완료" : state is { Researching: true } research ? $"{research.Remaining:0}초" : "";
        string terms = state is KnightResearchSnapshot s
            ? $"목재 {s.Wood:N0} · 기름 {s.Oil:N0} · {s.ResearchSeconds:0.##}초\n" + KnightResearchDescription(s) : "";
        slot.Detail = terms + (reason == null ? "" : "\n" + reason);
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
        int index = Array.IndexOf(SlotNumbers, number);
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
        else if (_display == DisplayMode.Workshop && number is 7 or 8 or 9)
            Units.RequestTrain(number == 7 ? UnitCatalog.FlameAutomaton : number == 8 ? UnitCatalog.SteamArtillery : UnitCatalog.Refueler);
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
            Key.E when _display == DisplayMode.Workshop => 9,
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
            Key.Escape when _display is DisplayMode.Construction or DisplayMode.TownHall or DisplayMode.Barracks or DisplayMode.Workshop && !Slot(3).Disabled => 3,
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
