using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

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
    public event Action<Building> TunnelExitRequested;
    public bool IsBuildMenuOpen => _buildTier != 0;
    private enum DisplayMode { Unset, Empty, Units, Workers, BuildTierOne, BuildTierTwo, TownHall, Barracks, Workshop, Forge, ResearchLab, Tunnel, Construction }
    // Slots keep their old numpad numbers (7 8 9 / 4 5 6 / 1 2 3, plus 10 11 12) as identities for commands and
    // hotkeys; ArrangeSlots decides where each one sits in the 4x3 grid.
    private static readonly int[] SlotNumbers = { 7, 8, 9, 10, 4, 5, 6, 11, 1, 2, 3, 12 };
    private readonly CommandSlot[] _slots = new CommandSlot[12];
    private readonly List<StyleBoxFlat> _styles = new();
    private readonly HashSet<Key> _heldMenuKeys = new();
    private GridContainer _grid;
    private SelectionPortraits _portraits;
    private DisplayMode _display;
    private int _buildTier; // The worker's open construction menu: 0 none, 1 (Z) or 2 (X).
    private bool BuildMenu => _display is DisplayMode.BuildTierOne or DisplayMode.BuildTierTwo;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        MouseForcePassScrollEvents = false;
        Theme = GD.Load<Theme>("res://ui/SkillTooltipTheme.tres"); // Tooltip frame shared with skills and unit info
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
            Buildings.TunnelLinkChanged += RefreshProduction;
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

    // Each command sits on its hotkey's cell (Q W E R / A S D F / Z X C V), which the slot numbers already follow,
    // and that key runs it (TryHandleShortcut). Cancel (Esc) has no cell of its own and takes the free bottom-right one.
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
                        BuildingCatalog.Tunnel => DisplayMode.Tunnel,
                        _ => DisplayMode.Empty
                    };
            }
        }
        if (next != DisplayMode.Workers) SetMenuOpen(0);
        else if (_buildTier != 0) next = _buildTier == 1 ? DisplayMode.BuildTierOne : DisplayMode.BuildTierTwo;
        if (next == _display) { RefreshProduction(); return; }
        _display = next;
        ArrangeSlots(next);
        for (int i = 0; i < _slots.Length; i++)
        {
            (string name, string key, Texture2D icon) = (next, SlotNumbers[i]) switch
            {
                (DisplayMode.Units or DisplayMode.Workers, 4) => (Lang.T("공격"), "A", Icon("command-attack")),
                (DisplayMode.Units or DisplayMode.Workers, 5) => (Lang.T("정지"), "S", Icon("command-stop")),
                (DisplayMode.Units or DisplayMode.Workers, 6) => (Lang.T("홀드"), "D", Icon("command-hold")),
                (DisplayMode.Workers, 1) => (Lang.T("1티어 건설"), "Z", Icon("command-build")),
                (DisplayMode.Workers, 2) => (Lang.T("2티어 건설"), "X", Icon("command-build-advanced")),
                (DisplayMode.BuildTierOne or DisplayMode.BuildTierTwo, 3) => (Lang.T("뒤로"), "C", Icon("command-back")),
                (DisplayMode.BuildTierOne or DisplayMode.BuildTierTwo, int number) when BuildingCatalog.IsPlayerBuildable(BuildingTypeForSlot(next, number)) =>
                    (BuildingCatalog.Name(BuildingTypeForSlot(next, number)), CellKey(number), BuildingIcon(BuildingTypeForSlot(next, number))),
                (DisplayMode.TownHall, 7) => (UnitCatalog.Name(UnitCatalog.Worker), "Q", UnitIcon(UnitCatalog.Worker)),
                (DisplayMode.TownHall, 1) => (Lang.T("업그레이드"), "Z", Icon("command-upgrade")),
                (DisplayMode.Barracks, 7) => (UnitCatalog.Name(UnitCatalog.Knight), "Q", UnitIcon(UnitCatalog.Knight)),
                (DisplayMode.Barracks, 8) => (UnitCatalog.Name(UnitCatalog.Archer), "W", UnitIcon(UnitCatalog.Archer)),
                (DisplayMode.Barracks, 9) => (UnitCatalog.Name(UnitCatalog.Medic), "E", UnitIcon(UnitCatalog.Medic)),
                (DisplayMode.Workshop, 7) => (UnitCatalog.Name(UnitCatalog.FlameAutomaton), "Q", UnitIcon(UnitCatalog.FlameAutomaton)),
                (DisplayMode.Workshop, 8) => (UnitCatalog.Name(UnitCatalog.SteamArtillery), "W", UnitIcon(UnitCatalog.SteamArtillery)),
                (DisplayMode.Workshop, 9) => (UnitCatalog.Name(UnitCatalog.Refueler), "E", UnitIcon(UnitCatalog.Refueler)),
                (DisplayMode.Forge, 7) => (Lang.T("공격력 연구"), "Q", Icon("research-attack")),
                (DisplayMode.Forge, 8) => (Lang.T("체력 연구"), "W", Icon("research-armor")),
                (DisplayMode.ResearchLab, 7) => (Lang.T("와이어 돌진"), "Q", Icon("research-wire")),
                (DisplayMode.Tunnel, 7) => (Lang.T("출구 설치"), "Q", BuildingIcon(BuildingCatalog.Tunnel)),
                (DisplayMode.Construction, 3) => (Lang.T("건설 취소"), "Esc", Icon("command-cancel")),
                _ => ("", "", null)
            };
            _slots[i].Set(name, key, icon);
        }
        if (next is DisplayMode.Units or DisplayMode.Workers)
        {
            Slot(4).Detail = Lang.T("A: 공격 사거리 표시 · 적 클릭: 공격 · 지형 클릭: 공격 이동 · Esc: 취소");
            Slot(5).Detail = Lang.T("현재 작업 중단 · 주변 적을 추격하고 제자리로 복귀");
            Slot(6).Detail = Lang.T("현재 작업 중단 · 자리를 지키며 사거리 안의 적만 공격");
        }
        if (next == DisplayMode.Workers)
        {
            Slot(1).Detail = BuildingList(BuildingCatalog.Store, BuildingCatalog.Supply, BuildingCatalog.OilDerrick, BuildingCatalog.Tower,
                BuildingCatalog.Barracks, BuildingCatalog.Forge);
            Slot(2).Detail = BuildingList(BuildingCatalog.CoalDrill, BuildingCatalog.Tunnel, BuildingCatalog.Workshop, BuildingCatalog.ResearchLab);
        }
        RefreshProduction();
    }

    private static string BuildingList(params uint[] types) => string.Join(" · ", types.Select(BuildingCatalog.Name));

    // The key printed on each cell of the 4x3 grid, by slot number.
    private static string CellKey(int number) => number switch
    {
        7 => "Q", 8 => "W", 9 => "E", 10 => "R", 4 => "A", 5 => "S", 6 => "D", 11 => "F", 1 => "Z", 2 => "X", 3 => "C", 12 => "V", _ => ""
    };

    private static int CellOfKey(Key key) => key switch
    {
        Key.Q => 7, Key.W => 8, Key.E => 9, Key.R => 10, Key.A => 4, Key.S => 5, Key.D => 6, Key.F => 11,
        Key.Z => 1, Key.X => 2, Key.C => 3, Key.V => 12, _ => 0
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
            else Slot(1).Set(allowed ? deploy ? Lang.T("공성 전개") : Lang.T("전개 해제") : Lang.T("공성 전환"), "Z",
                Icon(allowed && !deploy ? "siege-pack" : "siege-deploy"));
            Slot(1).Disabled = !allowed;
            Slot(1).Detail = available ? reason ?? Lang.T("선택한 내 증기포격차만 전환 · 전환 중 이동·공격 불가") : "";
            return;
        }
        if (BuildMenu)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                uint type = BuildingTypeForSlot(_display, SlotNumbers[i]);
                if (!BuildingCatalog.IsPlayerBuildable(type)) continue;
                string reason = Buildings.BuildingRequirementBlockReason(type);
                CommandSlot slot = _slots[i];
                slot.Disabled = reason != null;
                BuildingDefinition? terms = Buildings.BuildingTerms(type);
                slot.Terms = terms is BuildingDefinition t ? new CommandTerms(t.Wood, t.Oil, t.Seconds) : null;
                (slot.Heading, slot.Points) = BuildingPoints(type, terms?.Supply ?? 0);
                slot.Warning = reason ?? "";
            }
            return;
        }
        if (_display == DisplayMode.Construction)
        {
            Slot(3).Disabled = !Units.CanCancelConstruction(Buildings.SelectedBuilding);
            Slot(3).Detail = Lang.T("공사를 취소하고 지불 비용의 75% 반환 (소수점 버림)");
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
        if (_display == DisplayMode.Tunnel)
        {
            Building tunnel = Buildings.SelectedBuilding;
            bool linked = Buildings.TunnelPartner(tunnel) != null;
            Slot(7).Disabled = !Units.CanOpenTunnelExit(tunnel);
            Slot(7).Status = linked ? Lang.T("연결됨") : "";
            Slot(7).Detail = (linked ? Lang.T("이미 출구가 있습니다") + "\n" : !Units.CommanderControl ? Lang.T("지휘관만 출구를 낼 수 있습니다") + "\n" : "") +
                Lang.T("보이는 빈 땅에 출구를 바로 설치 (추가 비용 없음)\n한쪽이 무너지면 다른 쪽도 함께 무너짐");
            return;
        }
        if (_display is not (DisplayMode.TownHall or DisplayMode.Barracks or DisplayMode.Workshop)) return;
        Building producer = Buildings.SelectedBuilding;
        if (!GodotObject.IsInstanceValid(producer)) return;
        int count = producer.ProductionQueue.Count;
        bool canCancel = producer.ProductionJobs.Count > 0 && Units.CanCancelTraining(producer, producer.ProductionJobs[0].Id);
        if (canCancel) Slot(3).Set(Lang.T("생산 취소"), "Esc", Icon("command-cancel"));
        else Slot(3).Set("");
        Slot(3).Detail = canCancel ? Lang.T("현재 생산을 취소하고 지불 비용의 75% 반환 · 대기 항목은 정보 패널에서 클릭") : "";
        Slot(7).Disabled = count >= ProductionSnapshot.MaxQueue || Buildings.IsTownHallUpgrading(producer);
        string rallyHint = Lang.T("지형 우클릭: 랠리 지정 · 선택한 건물 우클릭: 랠리 해제");
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
        Slot(7).Terms = TrainingTermsOf(UnitCatalog.Worker);
        Slot(7).Points = UnitPoints(UnitCatalog.Worker);
        Slot(7).Detail = Lang.T("나무 우클릭: 새 일꾼 자동 채집") + "\n" + rallyHint;
        if (_display == DisplayMode.TownHall)
        {
            var state = Buildings.TeamTier(producer.SideId);
            string reason = Units.TownHallUpgradeBlockReason(producer);
            CommandSlot upgrade = Slot(1);
            if (state.TopTier) upgrade.Set(Lang.F("{0}티어", state.Tier), "", Icon("command-upgrade"));
            else upgrade.Set(Lang.F("{0}티어 업그레이드", state.Upgrading ? state.TargetTier : state.Tier + 1), "Z", Icon("command-upgrade"));
            upgrade.Status = state.Upgrading ? Lang.F("{0:0}초", state.Remaining) : state.TopTier ? Lang.T("최고 단계") : "";
            upgrade.Disabled = reason != null;
            upgrade.Terms = state.HasUpgradeTerms && !state.TopTier ? new CommandTerms(state.NextWood, state.NextOil, state.NextSeconds) : null;
            upgrade.Points = state.TopTier ? Array.Empty<string>() :
                new[] { Lang.T("HQ·요새·감시탑 최대 체력과 공격력: 기본값의 +20%"), Lang.T("팀의 다음 티어 영웅 장비 해금") };
            upgrade.Detail = state.TopTier ? Lang.T("최고 단계") : "";
            upgrade.Warning = state.TopTier ? "" : reason ?? "";
            if (Buildings.IsTownHallUpgrading(producer)) Slot(7).Warning = Lang.T("HQ 업그레이드 중에는 일꾼을 생산할 수 없습니다.");
        }
        ApplyUnitRequirement(Slot(7), UnitCatalog.Worker);
    }

    // Barracks and workshop buttons: costs and supply come from the server's TRAIN_DEF.
    private void RefreshUnitTraining(CommandSlot slot, Building producer, uint type, string rallyHint)
    {
        string reason = !Units.CommanderControl || Units.Spectating ? Lang.T("지휘관만 생산할 수 있습니다") :
            producer.ProductionQueue.Count >= ProductionSnapshot.MaxQueue ? Lang.T("생산 대기열 가득 참") :
            Buildings.UnitRequirementBlockReason(type) ?? Buildings.TrainingTermsBlockReason(producer, type);
        slot.Disabled = reason != null;
        slot.Terms = TrainingTermsOf(type);
        slot.Points = UnitPoints(type);
        slot.Detail = rallyHint;
        slot.Warning = reason ?? "";
    }

    private CommandTerms? TrainingTermsOf(uint type) =>
        Buildings.TrainingTerms(type) is TrainingDefinition d ? new CommandTerms(d.Wood, d.Oil, d.Seconds, d.Supply) : null;

    // A few words on what each unit does, listed under its cost instead of a paragraph.
    private static string[] UnitPoints(uint type) => type switch
    {
        UnitCatalog.Worker => new[] { Lang.T("자원 채집"), Lang.T("건물 건설") },
        UnitCatalog.Knight => new[] { Lang.T("근접 보병"), Lang.T("와이어 돌진 연구 시 자동 돌진") },
        UnitCatalog.Archer => new[] { Lang.T("원거리 보병") },
        UnitCatalog.Medic => new[] { Lang.T("아군 치유 (마나 사용)"), Lang.T("공격도 함") },
        UnitCatalog.FlameAutomaton => new[] { Lang.T("전방 부채꼴 화염") },
        UnitCatalog.SteamArtillery => new[] { Lang.T("전개 시 장거리 범위 포격"), Lang.T("전개 중 이동 불가") },
        UnitCatalog.Refueler => new[] { Lang.T("아군 기계 수리"), Lang.T("공격도 함") },
        _ => Array.Empty<string>()
    };

    // The same for buildings: what it produces or researches, or what it is for.
    private static (string Heading, string[] Points) BuildingPoints(uint type, long supply) => type switch
    {
        BuildingCatalog.Store => ("", new[] { Lang.T("일꾼 자원 반납") }),
        BuildingCatalog.Supply => ("", new[] { supply > 0 ? Lang.F("인구 +{0}", supply) : Lang.T("인구 증가") }),
        BuildingCatalog.OilDerrick => ("", new[] { Lang.T("유전 지대에만 건설"), Lang.T("유전마다 1개") }),
        BuildingCatalog.Tower => ("", new[] { Lang.T("사거리 안 적 자동 공격") }),
        BuildingCatalog.Barracks => (Lang.T("생산 가능"), Produces(UnitCatalog.Knight, UnitCatalog.Archer, UnitCatalog.Medic)),
        BuildingCatalog.Forge => (Lang.T("연구"), new[] { Lang.T("공격력"), Lang.T("체력") }),
        BuildingCatalog.CoalDrill => ("", new[] { Lang.T("나무 없이 목재 채집") }),
        BuildingCatalog.Tunnel => ("", new[] { Lang.T("Q: 다른 곳에 출구 설치"), Lang.T("아군 땅굴 우클릭: 반대쪽으로 이동") }),
        BuildingCatalog.Workshop => (Lang.T("생산 가능"), Produces(UnitCatalog.FlameAutomaton, UnitCatalog.SteamArtillery, UnitCatalog.Refueler)),
        BuildingCatalog.ResearchLab => (Lang.T("연구"), new[] { Lang.T("와이어 돌진") }),
        _ => ("", Array.Empty<string>())
    };

    private static string[] Produces(params uint[] units) => units.Select(type => $"{UnitCatalog.Name(type)} · {UnitRole(type)}").ToArray();

    private static string UnitRole(uint type) => type switch
    {
        UnitCatalog.Knight => Lang.T("근접"),
        UnitCatalog.Archer => Lang.T("원거리"),
        UnitCatalog.Medic => Lang.T("치유"),
        UnitCatalog.FlameAutomaton => Lang.T("화염"),
        UnitCatalog.SteamArtillery => Lang.T("포격"),
        UnitCatalog.Refueler => Lang.T("수리"),
        _ => ""
    };

    // Terms and progress come from ARMY_UPGRADE; the client copies no research numbers.
    private void RefreshResearch(CommandSlot slot, bool attack)
    {
        Building forge = Buildings.SelectedBuilding;
        if (!GodotObject.IsInstanceValid(forge)) return;
        var state = Buildings.ArmyUpgrade(forge.SideId);
        var research = state.Research(attack);
        int level = state.Level(attack);
        string name = attack ? Lang.T("공격력") : Lang.T("체력");
        string reason = Units.ArmyUpgradeBlockReason(forge, attack);
        slot.Disabled = reason != null;
        slot.Status = research.Researching ? Lang.F("{0}단계 {1}초", research.TargetLevel, research.Remaining) :
            state.MaxLevel > 0 && level >= state.MaxLevel ? Lang.T("최고 단계") : Lang.F("{0}단계", level + 1);
        slot.Terms = research.NextSeconds > 0 ? new CommandTerms(research.NextWood, research.NextOil, research.NextSeconds) : null;
        var minionGains = new List<string>();
        foreach (var step in Buildings.MinionUpgradeSteps)
            minionGains.Add($"{MinionShortName(step.UnitType)} +{(attack ? step.Attack : step.Hp):0.##}");
        var points = new List<string>();
        if (minionGains.Count > 0) points.Add(Lang.F("미니언 {0} 단계당 {1}", name, string.Join(" · ", minionGains)));
        points.Add(Lang.F("용병 기본 {0} +{1}% (일꾼·영웅 제외) · 현재 {2}/{3}단계", name, state.StepPercent, level, state.MaxLevel));
        slot.Points = points.ToArray();
        slot.Warning = reason ?? "";
    }

    private static string MinionShortName(uint type) => type switch
    {
        UnitCatalog.MinionMelee => Lang.T("근접"),
        UnitCatalog.MinionRanged => Lang.T("원거리"),
        UnitCatalog.MinionHealer => Lang.T("치유"),
        UnitCatalog.MinionFurnace => Lang.T("화로"),
        UnitCatalog.MinionWarden => Lang.T("호위"),
        UnitCatalog.MinionArc => Lang.T("전격"),
        _ => UnitCatalog.Name(type)
    };

    private void ApplyUnitRequirement(CommandSlot slot, uint type)
    {
        if (Buildings.UnitRequirementBlockReason(type) is not string reason) return;
        slot.Disabled = true;
        slot.Warning = slot.Warning.Length > 0 ? reason + "\n" + slot.Warning : reason;
    }

    private void RefreshKnightResearch(CommandSlot slot)
    {
        Building lab = Buildings.SelectedBuilding;
        if (!GodotObject.IsInstanceValid(lab)) return;
        string reason = Units.KnightResearchBlockReason(lab);
        slot.Disabled = reason != null;
        var state = Buildings.KnightResearch(lab.SideId);
        slot.Status = state is { Unlocked: true } ? Lang.T("완료") : state is { Researching: true } research ? Lang.F("{0:0}초", research.Remaining) : "";
        slot.Terms = state is KnightResearchSnapshot s ? new CommandTerms(s.Wood, s.Oil, s.ResearchSeconds) : null;
        slot.Points = state is KnightResearchSnapshot known ? KnightResearchDescription(known).Split('\n') : Array.Empty<string>();
        slot.Warning = reason ?? "";
    }

    public static string KnightResearchDescription(KnightResearchSnapshot state) =>
        Lang.F("아군 {0} 전체에 자동 돌진 해금 · 이후 생산한 병력에도 적용", UnitCatalog.Name(UnitCatalog.Knight)) + "\n" +
        Lang.F("적 유닛 추격 중 {0:0.##}m 이내에서 자동 발동 · 이동 속도 +{1:0.##}% · 재사용 {2:0.##}초",
            state.TriggerRange, (state.SpeedMultiplier - 1) * 100, state.CooldownSeconds);

    private static uint BuildingTypeForSlot(DisplayMode menu, int number) => (menu, number) switch
    {
        (DisplayMode.BuildTierOne, 7) => BuildingCatalog.Store,
        (DisplayMode.BuildTierOne, 8) => BuildingCatalog.Supply,
        (DisplayMode.BuildTierOne, 9) => BuildingCatalog.OilDerrick,
        (DisplayMode.BuildTierOne, 10) => BuildingCatalog.Tower,
        (DisplayMode.BuildTierOne, 4) => BuildingCatalog.Barracks,
        (DisplayMode.BuildTierOne, 5) => BuildingCatalog.Forge,
        (DisplayMode.BuildTierTwo, 7) => BuildingCatalog.CoalDrill,
        (DisplayMode.BuildTierTwo, 8) => BuildingCatalog.Tunnel,
        (DisplayMode.BuildTierTwo, 4) => BuildingCatalog.Workshop,
        (DisplayMode.BuildTierTwo, 5) => BuildingCatalog.ResearchLab,
        _ => uint.MaxValue
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
        else if (_display == DisplayMode.Tunnel && number == 7) TunnelExitRequested?.Invoke(Buildings.SelectedBuilding);
        else if (_display == DisplayMode.Barracks && number is 7 or 8 or 9)
            Units.RequestTrain(number == 7 ? UnitCatalog.Knight : number == 8 ? UnitCatalog.Archer : UnitCatalog.Medic);
        else if (_display == DisplayMode.Workshop && number is 7 or 8 or 9)
            Units.RequestTrain(number == 7 ? UnitCatalog.FlameAutomaton : number == 8 ? UnitCatalog.SteamArtillery : UnitCatalog.Refueler);
        else if (_display == DisplayMode.Workers && number is 1 or 2)
        {
            SetMenuOpen(number);
            RefreshSelection();
            BuildMenuOpened?.Invoke();
        }
        else if (BuildMenu)
        {
            if (number == 3) { CancelMenu(); return; }
            uint type = BuildingTypeForSlot(_display, number);
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
        int cell = CellOfKey(code);
        int slot = code switch
        {
            Key.Escape when BuildMenu => 3,
            // A grid key runs the command on its cell when that command shows the key; the cancel moved to V shows Esc.
            _ when cell > 0 && Slot(cell).Hotkey == CellKey(cell) => cell,
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
        if (_buildTier == 0) return false;
        SetMenuOpen(0);
        RefreshSelection();
        return true;
    }

    private void SetMenuOpen(int tier) => _buildTier = tier;

    private void OnSelectionChanged()
    {
        SetMenuOpen(0);
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
            Buildings.TunnelLinkChanged -= RefreshProduction;
        }
        GetWindow().FocusExited -= OnFocusExited;
        SetMenuOpen(0);
        foreach (StyleBoxFlat style in _styles) style.Dispose();
        _styles.Clear();
    }
}
