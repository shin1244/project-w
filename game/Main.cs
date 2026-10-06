using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// 입력·통신과 각 관리자를 연결합니다. 개별 오브젝트의 상태는 관리자에게 맡깁니다.
public partial class Main : Node3D
{
    [Export] public UnitManager Units;
    [Export] public BuildingManager Buildings;
    [Export] public ResourceManager Resources;
    [Export] public MapWorld Map;
    [Export] public Label SyncStatus;
    [Export] public StockDisplay Stock;
    [Export] public FogOfWar Fog;
    [Export] public Minimap Minimap;
    [Export] public CommandPanel Commands;
    [Export] public BuildingPlacement Placement;
    [Export] public SelectionDetails Details;
    [Export] public HeroSkillPanel Skills;
    [Export] public UnitInfoPanel UnitInfo;
    [Export] public MinionPanel Minions;
    // 접속 요청값. 실제 역할과 진영은 서버의 WELCOME으로 확정한다.
    [Export] public PlayerRole RequestedRole { get; set; } = PlayerRole.Commander;
    [Export] public uint RequestedHeroType { get; set; } = UnitCatalog.HeroTest;
    public PlayerRole LocalRole { get; private set; }
    public uint? LocalHeroType { get; private set; }
    public MatchResultSnapshot Result { get; private set; }

    private NetClient _net;
    private MatchSession _matchSession;
    private PlayerInput _playerInput;
    private BattleGuide _battleGuide;
    private bool _autoFrameStart, _startingViewPlaced;
    private readonly RangeIndicator _skillRange = new("SkillRangeRing", new Color(.22f, .95f, .78f, .95f));
    private readonly List<RangeIndicator> _attackRanges = new();

    public override void _Ready()
    {
        _autoFrameStart = true;
        SetPlayerRole(PlayerRole.None);
        if (Map.SyncError != null)
        {
            ShowStatus(Map.SyncError);
            return;
        }
        Fog?.Configure(Map);
        if (Buildings != null && Fog != null) Buildings.VisibilityCheck = Fog.IsBuildingVisible;
        ConnectInput();
        Units.CommandRequested += SendCommand;
        Units.PositionsRendered += OnPositionsRendered;

        _net = GetNode<NetClient>("/root/Net");
        _net.MessageReceived += OnMessage;
        _net.ConnectionClosed += OnConnectionClosed;
        _matchSession = GetNodeOrNull<MatchSession>("/root/MatchSession");
        var match = _matchSession?.Match;
        if (match != null)
        {
            RequestedRole = match.Role == "HERO" ? PlayerRole.Hero : PlayerRole.Commander;
            RequestedHeroType = match.Hero;
        }
        _net.ConnectToServer(match?.Host ?? "127.0.0.1", match?.Port ?? 7777);
    }

    private void ConnectInput()
    {
        if (Units != null && Fog != null) Units.EffectVisibilityCheck = Fog.IsVisibleAt;
        if (GetNodeOrNull<CanvasLayer>("SelectionUI") is CanvasLayer ui)
        {
            _battleGuide = new BattleGuide { Name = "BattleGuide", Skills = Skills };
            ui.AddChild(_battleGuide);
        }
        _playerInput = GetNode<PlayerInput>("PlayerInput");
        _playerInput.UnitClicked += SelectUnit;
        _playerInput.BuildingClicked += SelectBuilding;
        _playerInput.SelectionCleared += ClearSelection;
        _playerInput.BoxSelectionRequested += SelectBox;
        _playerInput.ContextClicked += Units.RequestContextOrder;
        _playerInput.AttackTargetClicked += Units.RequestAttack;
        _playerInput.AttackGroundClicked += Units.RequestAttackMove;
        _playerInput.StopRequested += Units.RequestStop;
        _playerInput.HoldRequested += Units.RequestHold;
        _playerInput.SkillRequested += CastHeroSkill;
        _playerInput.SkillTargetingChanged += ShowSkillRange;
        _playerInput.AttackTargetingChanged += OnAttackTargetingChanged;
        Units.SelectionChanged += RefreshAttackRanges;
        _playerInput.ModifiedUnitSelectionRequested += ModifyUnitSelection;
        _playerInput.ModifiedBoxSelectionRequested += ModifyBoxSelection;
        _playerInput.ControlGroupRequested += HandleControlGroup;
        _playerInput.CameraHomeRequested += FocusHome;
        if (Commands != null) Commands.BuildRequested += BeginPlacement;
        if (Minions != null)
        {
            Minions.CommandRequested += SendMinionCommand;
            Minions.ViewChanged += OnMinionViewChanged;
        }
        if (Minimap != null)
        {
            Minimap.CameraMoveRequested += MoveCameraFromMinimap;
            Minimap.MoveRequested += _playerInput.QueueMinimapMove;
        }
    }

    private void BeginPlacement(uint type)
    {
        _playerInput?.ResetInteraction();
        Placement?.Begin(type);
    }

    private void SendMinionCommand(string command)
    {
        if (LocalRole == PlayerRole.Commander) SendCommand(command);
    }

    private void OnMinionViewChanged(bool showMinions)
    {
        if (Details != null) Details.Visible = LocalRole == PlayerRole.Commander && !showMinions;
        _playerInput?.ResetInteraction();
        Placement?.Cancel();
        Commands?.CancelMenu();
    }

    private void ModifyUnitSelection(Unit unit, bool shift, bool sameType)
    {
        if (LocalRole == PlayerRole.Hero) { UnitInfo?.Inspect(unit); return; }
        if (!Units.CanControl(unit))
        {
            // 정보 확인은 일반 클릭으로만 전환하고 Shift 선택은 기존 부대를 유지합니다.
            if (!shift) SelectUnit(unit);
            return;
        }
        Buildings?.ClearSelection();
        if (sameType) Units.SelectSameTypeOnScreen(unit, shift);
        else Units.ToggleSelection(unit);
    }

    private void ModifyBoxSelection(Rect2 rect)
    {
        if (LocalRole == PlayerRole.Hero) { UnitInfo?.Clear(); return; }
        Units.SelectBoxWithMode(rect, true);
        if (Units.SelectedUnitIds.Count > 0) Buildings?.ClearSelection();
    }

    private void HandleControlGroup(int number, bool save, bool focus)
    {
        if (save) { Units.SaveControlGroup(number); return; }
        if (!Units.RecallControlGroup(number)) return;
        Buildings?.ClearSelection();
        if (focus && Units.TryGetSelectionCenter(out Vector3 center)) CameraNavigation.FocusGround(Units.Camera, center);
    }

    private void MoveCameraFromMinimap(Vector3 point)
    {
        _playerInput.CancelTargeting();
        CameraNavigation.FocusGround(Units.Camera, point);
    }

    private void FocusHome()
    {
        if (!Map.IsSynchronized || Result != null) return;
        if (TryGetHomePosition(out Vector3 home)) CameraNavigation.FocusGround(Units.Camera, home);
    }

    private bool TryGetHomePosition(out Vector3 home)
    {
        home = default;
        if (LocalRole == PlayerRole.Hero)
        {
            foreach (Unit unit in Units.LiveUnits)
                if (unit.UnitType == LocalHeroType && Units.CanControl(unit))
                {
                    home = unit.GlobalPosition;
                    return true;
                }
        }
        else if (LocalRole == PlayerRole.Commander) return Map.TryGetBasePosition(Units.LocalTeam, out home);
        return false;
    }

    public override void _Process(double delta)
    {
        if (_autoFrameStart && !_startingViewPlaced) FrameStartingView();
    }

    private void FrameStartingView()
    {
        if (!Map.IsSynchronized || Result != null || !TryGetHomePosition(out Vector3 home)) return;
        Units.Camera.Size = LocalRole == PlayerRole.Hero ? 38 : 52;
        CameraNavigation.FocusGround(Units.Camera, home);
        _startingViewPlaced = true;
    }

    private void SelectUnit(Unit unit)
    {
        if (LocalRole == PlayerRole.Hero) { UnitInfo?.Inspect(unit); return; }
        Buildings?.ClearSelection();
        Units.SelectSingle(unit);
    }

    private void CastHeroSkill(SkillInput input)
    {
        if (Map.IsSynchronized && LocalRole == PlayerRole.Hero && Skills?.CanUse(input.Slot) == true &&
            Skills.HeroUnitId is uint id && Skills.Definition(input.Slot) is SkillDefinitionSnapshot definition)
            Units.RequestHeroSkill(definition, id, input);
    }

    private void ShowSkillRange(int? slot)
    {
        _skillRange.Clear();
        if (!slot.HasValue || !Map.IsSynchronized || LocalRole != PlayerRole.Hero ||
            Skills?.CanUse(slot.Value) != true || Skills.HeroUnitId is not uint id ||
            Skills.Definition(slot.Value) is not SkillDefinitionSnapshot definition ||
            !Units.TryGetUnit(id, out Unit hero) || !Units.CanControl(hero)) return;
        _skillRange.Show(hero, definition.Range, Fog?.UnitBodyRadius(hero) ?? hero.PlacementRadius);
    }

    private void SelectBuilding(Building building)
    {
        if (LocalRole == PlayerRole.Hero) { UnitInfo?.Clear(); return; }
        Units.ClearSelection();
        Buildings?.SelectSingle(building);
    }

    private void OnAttackTargetingChanged(bool active) => RefreshAttackRanges();

    private void ClearAttackRanges()
    {
        foreach (RangeIndicator ring in _attackRanges) ring.Clear();
        _attackRanges.Clear();
    }

    private void RefreshAttackRanges()
    {
        ClearAttackRanges();
        if (_playerInput?.IsAttackTargeting != true || !Map.IsSynchronized || Result != null ||
            LocalRole is not (PlayerRole.Hero or PlayerRole.Commander)) return;
        foreach (uint id in Units.SelectedUnitIds)
        {
            if (!Units.TryGetUnit(id, out Unit unit) || !Units.CanControl(unit) ||
                !unit.IsVisibleInTree() || unit.Stats is not { Interval: > 0 } stats) continue;
            var ring = new RangeIndicator("AttackRangeRing", new Color(1f, .76f, .25f, .95f));
            ring.Show(unit, stats.Range, Fog?.UnitBodyRadius(unit) ?? unit.PlacementRadius);
            _attackRanges.Add(ring);
        }
    }

    private void ClearSelection()
    {
        UnitInfo?.Clear();
        Units.ClearSelection();
        Buildings?.ClearSelection();
    }

    private void SelectBox(Rect2 rect)
    {
        if (LocalRole == PlayerRole.Hero) { UnitInfo?.Clear(); return; }
        Buildings?.ClearSelection();
        Units.SelectBox(rect);
    }

    private void OnMessage(string message)
    {
        string[] parts = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return;
        if (Result != null) return;

        switch (parts[0])
        {
            case "MAP":
                _startingViewPlaced = false;
                SetPlayerRole(PlayerRole.None);
                _playerInput?.ResetInteraction();
                Stock?.Clear();
                Fog?.Reset();
                Minimap?.Reset();
                if (!Map.AcceptMap(parts)) { RejectMap(); return; }
                Units.Clear();
                Buildings?.Clear();
                if (RequestedRole is not (PlayerRole.Commander or PlayerRole.Hero))
                {
                    ShowStatus("접속 역할을 선택해 주세요.");
                    return;
                }
                _net.Send(_matchSession?.Match is { } match
                    ? $"MAP_READY {Map.MapHash} TICKET {match.Ticket}"
                    : Protocol.BuildMapReady(Map.MapHash, RequestedRole, RequestedHeroType));
                return;
            case "TREE":
                if (!Map.ApplyTree(parts)) RejectMap();
                else Fog?.Invalidate();
                Minimap?.Invalidate();
                return;
            case "WORLD_READY":
                if (parts.Length != 1 || !Map.CompleteSync()) { RejectMap(); return; }
                if (SyncStatus != null) SyncStatus.Hide();
                Minimap?.Invalidate();
                return;
        }
        if (!Map.IsSynchronized)
            return;

        switch (parts[0])
        {
            case "MATCH_END":
                if (MatchResultSnapshot.TryParse(parts, out var result))
                {
                    Result = result;
                    _playerInput?.ResetInteraction();
                    Placement?.Cancel();
                    Minions?.SetRole(false);
                    _skillRange.Clear();
                    ProcessMode = ProcessModeEnum.Disabled;
                    _matchSession?.Finish(result, Units.LocalTeam);
                }
                break;
            case "TICK":
                Units.HandleTick(parts);
                Skills?.HandleTick(parts);
                _battleGuide?.HandleTick(parts);
                if (parts.Length == 2 && uint.TryParse(parts[1], System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out uint minionTick)) Minions?.SetTick(minionTick);
                break;
            case "RESPAWN":
                if (LocalRole == PlayerRole.Hero) Skills?.HandleRespawn(parts);
                break;
            case "TICK_END":
                Units.HandleTickEnd(parts);
                break;
            case "WELCOME":
                if (WelcomeSnapshot.TryParse(parts, out var welcome))
                {
                    Units.SetLocalPlayer(welcome.PlayerId, welcome.Team);
                    Buildings?.SetLocalTeam(welcome.Team);
                    Fog?.SetTeam(welcome.Team);
                    Minimap?.SetTeam(welcome.Team);
                    SetPlayerRole(welcome.Role, welcome.HeroType, welcome.PlayerId, welcome.Team);
                }
                break;
            case "SIGHT":
                Fog?.HandleSight(parts);
                break;
            case "REQUIRES":
                Buildings?.HandleRequirements(parts);
                break;
            case "BODY":
                Fog?.HandleBody(parts);
                RefreshAttackRanges();
                break;
            case "BUILDING_VISION":
                Fog?.HandleBuildingVision(parts);
                Minimap?.Invalidate();
                break;
            case "BUILDING_HIDE":
                if (parts.Length == 2 && uint.TryParse(parts[1], out uint hiddenBuilding)) Fog?.ForgetBuilding(hiddenBuilding);
                Buildings?.HandleRemove(parts);
                Fog?.Invalidate();
                Minimap?.Invalidate();
                break;
            case "UNIT":
                Units.HandleSpawn(parts);
                Skills?.HandleUnit(parts);
                Fog?.Invalidate();
                Minimap?.Invalidate();
                break;
            case "BUILDING":
                Buildings?.HandleSpawn(parts);
                Fog?.Invalidate();
                Minimap?.Invalidate();
                break;
            case "CONSTRUCTION":
                Buildings?.HandleConstruction(parts);
                Fog?.Invalidate();
                break;
            case "QUEUE":
                Buildings?.HandleProduction(parts);
                break;
            case "RALLY":
                Buildings?.HandleRally(parts);
                break;
            case "POS":
                Units.HandlePosition(parts);
                Fog?.Invalidate();
                Minimap?.Invalidate();
                break;
            case "HP":
                if (HealthSnapshot.TryParse(parts, out var health))
                {
                    Units.HandleHealth(health);
                    Buildings?.HandleHealth(health);
                    Skills?.HandleHealth(health);
                }
                break;
            case "SHIELD":
                if (ShieldSnapshot.TryParse(parts, out var shield))
                {
                    Units.HandleShield(shield);
                    Buildings?.HandleShield(shield);
                    Skills?.HandleShield(shield);
                }
                break;
            case "STATS":
                if (StatsSnapshot.TryParse(parts, out var stats))
                {
                    if (Units.TryGetUnit(stats.Id, out Unit statsUnit))
                    {
                        statsUnit.Stats = stats;
                        if (Units.SelectedUnitIds.Contains(stats.Id))
                            RefreshAttackRanges();
                    }
                    if (Buildings != null && Buildings.TryGetBuilding(stats.Id, out Building statsBuilding)) statsBuilding.Stats = stats;
                    Fog?.Invalidate();
                }
                break;
            case "EXP":
                if (LocalRole == PlayerRole.Hero && HeroExperienceSnapshot.TryParse(parts, out var experience))
                    UnitInfo?.ApplyExperience(experience);
                break;
            case "COOLDOWN":
                if (LocalRole == PlayerRole.Hero && SkillCooldownSnapshot.TryParse(parts, out var cooldown))
                    Skills?.ApplyCooldown(cooldown);
                break;
            case "ABILITY":
                if (SkillDefinitionSnapshot.TryParse(parts, out var definition)) Skills?.HandleDefinition(definition);
                break;
            case "CONTROL":
                if (ControlSnapshot.TryParse(parts, out var control)) Skills?.HandleControl(control);
                break;
            case "SKILL":
                if (SkillActivationSnapshot.TryParse(parts, out var skill) && Units.TryGetUnit(skill.CasterId, out Unit caster) &&
                    Units.CanInspect(caster))
                    foreach (Node child in caster.GetChildren())
                        if (child is ISkillPresentation presentation) presentation.PlaySkill(skill.Slot, skill.Empowered);
                break;
            case "WOLF":
                if (WolfEffectSnapshot.TryParse(parts, out var wolf) &&
                    Units.TryGetUnit(wolf.UnitId, out Unit wolfUnit) && wolfUnit.UnitType == UnitCatalog.HeroTest &&
                    Units.CanInspect(wolfUnit))
                {
                    wolfUnit.WolfEffects = wolf;
                    Skills?.HandleWolfEffects(wolf);
                }
                break;
            case "GOLEM":
                if (GolemEffectSnapshot.TryParse(parts, out var golem) &&
                    Units.TryGetUnit(golem.UnitId, out Unit golemUnit) && golemUnit.UnitType == UnitCatalog.HeroGolem &&
                    Units.CanInspect(golemUnit))
                {
                    golemUnit.GolemEffects = golem;
                    Skills?.HandleGolemEffects(golem);
                }
                break;
            case "MINION_RULES":
            case "MINION_OPTION":
            case "MINION_LANE":
            case "MINION_WAVE":
                if (LocalRole == PlayerRole.Commander) Minions?.HandleMessage(parts);
                break;
            case "MINION_HEAL":
                Units.HandleMinionHeal(parts);
                break;
            case "STATE":
                if (StateSnapshot.TryParse(parts, out var state))
                {
                    Units.HandleState(state);
                    Buildings?.HandleState(state);
                    Skills?.HandleState(state);
                }
                break;
            case "STOCK":
                Stock?.HandleStock(parts);
                if (LocalRole == PlayerRole.Commander) Minions?.HandleStock(parts);
                break;
            case "SUPPLY":
                Stock?.HandleSupply(parts);
                break;
            case "REMOVE":
                if (parts.Length == 2 && uint.TryParse(parts[1], out uint removedBuilding)) Fog?.ForgetBuilding(removedBuilding);
                Units.HandleRemove(parts);
                Buildings?.HandleRemove(parts);
                Skills?.HandleRemove(parts);
                Fog?.Invalidate();
                Minimap?.Invalidate();
                break;
            case "HIDE":
                Units.HandleHide(parts);
                Skills?.HandleRemove(parts);
                Fog?.Invalidate();
                Minimap?.Invalidate();
                break;
            case "ERR":
                Minions?.HandleMessage(parts);
                Placement?.Notice(message.Length > 4 ? message[4..] : "요청을 처리할 수 없습니다.", true);
                GD.PushWarning(message);
                break;
            default:
                GD.Print($"받음: {message}");
                break;
        }
    }

    private void SendCommand(string command)
    {
        if (!Map.IsSynchronized || Result != null) return;
        _net.Send(command);
        GD.Print($"전송 요청: {command}");
    }

    private void SetPlayerRole(PlayerRole role, uint? heroType = null, uint playerId = 0, uint team = 0)
    {
        LocalRole = role;
        LocalHeroType = role == PlayerRole.Hero ? heroType : null;
        if (role == PlayerRole.Hero)
        {
            Buildings?.ClearSelection();
            Placement?.Cancel();
        }
        Units?.SetHeroControl(LocalHeroType);
        Units?.Camera?.SetMeta("hero_q_shortcut", LocalHeroType.HasValue && UnitCatalog.IsHero(LocalHeroType.Value));
        Units?.Camera?.SetMeta("hero_w_shortcut", LocalHeroType.HasValue && UnitCatalog.IsHero(LocalHeroType.Value));
        if (Details != null) Details.Visible = role == PlayerRole.Commander;
        if (Minions != null)
        {
            Minions.Clear();
            Minions.ConfigureLanes(Map?.LaneNames ?? Array.Empty<string>());
            Minions.SetRole(role == PlayerRole.Commander);
        }
        if (Stock != null) Stock.Visible = role == PlayerRole.Commander;
        _battleGuide?.SetRole(role, team);
        // 숨겨진 명령 패널은 기존 A/S/D 입력 경로를 계속 제공한다.
        if (Commands != null) Commands.Visible = role != PlayerRole.Hero;
        UnitInfo?.SetHero(LocalHeroType, playerId, team);
        Skills?.SetHero(LocalHeroType, playerId, team);
    }

    private void OnPositionsRendered()
    {
        Fog?.Invalidate();
        Minimap?.Invalidate();
    }

    private void RejectMap()
    {
        SetPlayerRole(PlayerRole.None);
        Stock?.Clear();
        Fog?.Reset();
        Minimap?.Reset();
        Units.Clear();
        Buildings?.Clear();
        ShowStatus(Map.SyncError ?? "맵 동기화 메시지가 올바르지 않습니다.");
        Map.StopSync();
        _net?.Disconnect();
    }

    private void OnConnectionClosed(string reason)
    {
        if (Result != null) return;
        SetPlayerRole(PlayerRole.None);
        _playerInput?.ResetInteraction();
        Stock?.Clear();
        Fog?.Reset();
        Minimap?.Reset();
        Units.Clear();
        Buildings?.Clear();
        Map.StopSync();
        ShowStatus(Map.SyncError ?? reason);
    }

    private void ShowStatus(string message)
    {
        GD.PushWarning(message);
        if (SyncStatus != null) { SyncStatus.Text = message; SyncStatus.Show(); }
    }

    public override void _ExitTree()
    {
        _skillRange.Clear();
        ClearAttackRanges();
        if (GodotObject.IsInstanceValid(Commands)) Commands.BuildRequested -= BeginPlacement;
        if (GodotObject.IsInstanceValid(Minions))
        {
            Minions.CommandRequested -= SendMinionCommand;
            Minions.ViewChanged -= OnMinionViewChanged;
        }
        if (_playerInput != null && GodotObject.IsInstanceValid(Units))
        {
            _playerInput.UnitClicked -= SelectUnit;
            _playerInput.BuildingClicked -= SelectBuilding;
            _playerInput.SelectionCleared -= ClearSelection;
            _playerInput.BoxSelectionRequested -= SelectBox;
            _playerInput.ContextClicked -= Units.RequestContextOrder;
            _playerInput.AttackTargetClicked -= Units.RequestAttack;
            _playerInput.AttackGroundClicked -= Units.RequestAttackMove;
            _playerInput.StopRequested -= Units.RequestStop;
            _playerInput.HoldRequested -= Units.RequestHold;
            _playerInput.SkillRequested -= CastHeroSkill;
            _playerInput.SkillTargetingChanged -= ShowSkillRange;
            _playerInput.AttackTargetingChanged -= OnAttackTargetingChanged;
            _playerInput.ModifiedUnitSelectionRequested -= ModifyUnitSelection;
            _playerInput.ModifiedBoxSelectionRequested -= ModifyBoxSelection;
            _playerInput.ControlGroupRequested -= HandleControlGroup;
            _playerInput.CameraHomeRequested -= FocusHome;
            if (GodotObject.IsInstanceValid(Minimap))
            {
                Minimap.CameraMoveRequested -= MoveCameraFromMinimap;
                Minimap.MoveRequested -= _playerInput.QueueMinimapMove;
            }
        }

        if (GodotObject.IsInstanceValid(Units))
        {
            Units.SelectionChanged -= RefreshAttackRanges;
            Units.CommandRequested -= SendCommand;
            Units.PositionsRendered -= OnPositionsRendered;
        }
        if (_net != null)
        {
            _net.MessageReceived -= OnMessage;
            _net.ConnectionClosed -= OnConnectionClosed;
            _net.Disconnect();
        }
    }
}
