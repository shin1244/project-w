using Godot;
using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;

public partial class UnitManager : Node3D
{
    [Export] public Camera3D Camera;
    [Export] public PackedScene WorkerScene;
    [Export] public PackedScene KnightScene;
    [Export] public PackedScene ArcherScene;
    [Export] public PackedScene MinionKnightScene;
    [Export] public PackedScene MinionArcherScene;
    [Export] public PackedScene MinionHealerScene;
    [Export] public PackedScene HeroTestScene;
    [Export] public PackedScene HeroGolemScene;
    [Export] public PackedScene SiegeRamScene;
    [Export] public ResourceManager Resources;
    [Export] public BuildingManager Buildings;
    public IReadOnlyCollection<uint> SelectedUnitIds => _selectedUnitIds;
    public IReadOnlyCollection<Unit> LiveUnits => _units.Values;
    public uint LocalTeam => _localTeam;
    public Func<Vector3, bool> EffectVisibilityCheck { get; set; }
    // 소유하지 않은 유닛은 정보만 살펴보고 명령/부대 지정에는 넣지 않는다.
    public Unit InspectedUnit { get; private set; }
    public bool TryGetUnit(uint id, out Unit unit) => _units.TryGetValue(id, out unit);
    public PackedScene SceneFor(uint type) => type switch
    {
        UnitCatalog.Worker => WorkerScene,
        UnitCatalog.Knight => KnightScene,
        UnitCatalog.Archer => ArcherScene,
        UnitCatalog.MinionMelee => MinionKnightScene ??= GD.Load<PackedScene>("res://units/MinionKnight.tscn"),
        UnitCatalog.MinionRanged => MinionArcherScene ??= GD.Load<PackedScene>("res://units/MinionArcher.tscn"),
        UnitCatalog.MinionHealer => MinionHealerScene ??= GD.Load<PackedScene>("res://units/MinionHealer.tscn"),
        UnitCatalog.HeroTest => HeroTestScene ??= GD.Load<PackedScene>("res://units/HeroTest.tscn"),
        UnitCatalog.HeroGolem => HeroGolemScene ??= GD.Load<PackedScene>("res://units/HeroGolem.tscn"),
        UnitCatalog.SiegeRam => SiegeRamScene ??= GD.Load<PackedScene>("res://units/SiegeRam.tscn"),
        _ => null
    };
    public event Action SelectionChanged;
    public event Action PositionsRendered;
    private readonly HashSet<uint> _selectedUnitIds = new();
    private readonly Dictionary<int, HashSet<uint>> _controlGroups = new();
    private const int MaxSelectedUnits = 64;
    private readonly Dictionary<uint, Unit> _units = new();
    private readonly HashSet<uint> _ramImpacts = new();
    private uint _localPlayerId;
    private uint _localTeam;
    private uint? _controlledHeroType;
    private readonly CommandTargetIndicator _targetIndicator = new();
    private readonly InterpolationClock _interpolation = new();

    public override void _Process(double delta)
    {
        if (_interpolation.HasTick)
        {
            _interpolation.Advance(delta);
            bool moved = false;
            foreach (Unit unit in _units.Values)
                moved |= unit.RenderPosition(_interpolation.RenderTick);
            if (moved) PositionsRendered?.Invoke();
        }
        _targetIndicator.Refresh();
    }
    public override void _ExitTree() => _targetIndicator.Clear();

    public event Action<string> CommandRequested;

    public void Clear()
    {
        _interpolation.Reset();
        _controlledHeroType = null;
        ClearSelectionCore();
        // 사망 연출 중인 유닛은 이미 사전에서 빠졌으므로 씬 자식도 함께 정리합니다.
        foreach (Node child in GetChildren())
        {
            if (child is not Unit && child is not MinionHealPulse && child is not SiegeRamImpact) continue;
            RemoveChild(child);
            child.QueueFree();
        }
        _units.Clear();
        _ramImpacts.Clear();
        _controlGroups.Clear();
        _localPlayerId = 0;
        _localTeam = 0;
        SelectionChanged?.Invoke();
    }

    public void SetLocalPlayer(uint playerId, uint team)
    {
        bool playerChanged = _localPlayerId != playerId || _localTeam != team;
        bool teamChanged = _localTeam != team;
        if (playerChanged) _controlGroups.Clear();
        _localPlayerId = playerId;
        _localTeam = team;
        if (teamChanged)
            foreach (Node child in GetChildren())
                if (child is Unit unit) unit.SetLocalTeam(team);
        Buildings?.SetLocalTeam(team);
        _targetIndicator.Clear();
        bool selectionChanged = ValidateSelection(notify: false);
        if (playerChanged || selectionChanged) SelectionChanged?.Invoke();
    }

    // AOS는 선택 입력과 관계없이 서버가 지정한 내 영웅 한 기를 조종한다.
    public void SetHeroControl(uint? heroType)
    {
        if (_controlledHeroType == heroType)
        {
            if (heroType.HasValue) ValidateSelection();
            return;
        }
        _controlledHeroType = heroType;
        _controlGroups.Clear();
        ClearSelectionCore();
        if (heroType.HasValue) ValidateSelection(notify: false);
        SelectionChanged?.Invoke();
    }

    public void RequestTrainWorker() => RequestTrain(0);

    public void RequestHeroSkill(SkillDefinitionSnapshot definition, uint heroId, SkillInput input)
    {
        if (!_controlledHeroType.HasValue || definition.UnitType != _controlledHeroType || input.Slot != definition.Slot ||
            !TryGetUnit(heroId, out Unit hero) || !CanControl(hero) || hero.State.Activity is UnitActivity.Stun or UnitActivity.Dash) return;
        switch (definition.Target)
        {
            case SkillTargetMode.Self:
                CommandRequested?.Invoke(Protocol.BuildSelfSkill(input.Slot));
                break;
            case SkillTargetMode.Point when input.Point is Vector3 point && float.IsFinite(point.X) && float.IsFinite(point.Z):
                CommandRequested?.Invoke(Protocol.BuildPointSkill(input.Slot, point.X, point.Z));
                break;
            case SkillTargetMode.Enemy:
            case SkillTargetMode.Ally:
                uint id, team;
                if (input.Target is Unit unit && CanInspect(unit)) { id = unit.UnitId; team = unit.Team; }
                else if (input.Target is Building building && GodotObject.IsInstanceValid(Buildings) && Buildings.CanInspect(building))
                { id = building.BuildingId; team = building.SideId; }
                else return;
                if ((team == _localTeam) != (definition.Target == SkillTargetMode.Ally)) return;
                CommandRequested?.Invoke(Protocol.BuildTargetSkill(input.Slot, id));
                break;
        }
    }

    private bool CanUseSelectedBuilding(Building building) => _localPlayerId != 0 && _localTeam != 0 &&
        _selectedUnitIds.Count == 0 && InspectedUnit == null && GodotObject.IsInstanceValid(Buildings) &&
        GodotObject.IsInstanceValid(building) && building == Buildings.SelectedBuilding && Buildings.CanInspect(building) &&
        building.SideId == _localTeam;

    public bool CanCancelTraining(Building building, uint jobId) => jobId != 0 && CanUseSelectedBuilding(building) &&
        !building.IsUnderConstruction && building.ProductionJobs.Any(job => job.Id == jobId && job.OwnerId == _localPlayerId);

    public void RequestCancelTraining(Building building, uint jobId)
    {
        if (CanCancelTraining(building, jobId)) CommandRequested?.Invoke(Protocol.BuildCancelTrain(building.BuildingId, jobId));
    }

    public bool CanCancelConstruction(Building building) => CanUseSelectedBuilding(building) &&
        building.IsUnderConstruction && building.ConstructionOwnerId == _localPlayerId;

    public void RequestCancelConstruction(Building building)
    {
        if (CanCancelConstruction(building)) CommandRequested?.Invoke(Protocol.BuildCancelConstruction(building.BuildingId));
    }

    public void RequestTrain(uint unitType)
    {
        if (_localPlayerId == 0 || _localTeam == 0 || _selectedUnitIds.Count > 0 || InspectedUnit != null ||
            !GodotObject.IsInstanceValid(Buildings)) return;

        Building producer = Buildings.SelectedBuilding;
        if (!GodotObject.IsInstanceValid(producer) || producer.IsQueuedForDeletion() || !producer.IsInsideTree() ||
            !producer.CanTrain(unitType) || producer.IsUnderConstruction || producer.SideId != _localTeam ||
            Buildings.UnitRequirementBlockReason(unitType) != null ||
            producer.ProductionQueue.Count >= ProductionSnapshot.MaxQueue ||
            !Buildings.TryGetBuilding(producer.BuildingId, out Building registered) || registered != producer) return;

        // 생성 가능 여부와 비용은 서버에서 결정하고, UNIT 응답을 받은 뒤 표시합니다.
        CommandRequested?.Invoke(Protocol.BuildTrain(producer.BuildingId, unitType));
    }

    public bool TryGetSelectedWorker(out Unit worker)
    {
        worker = null;
        foreach (uint id in _selectedUnitIds)
            if (_units.TryGetValue(id, out Unit candidate) && candidate.UnitType == 0 && CanControl(candidate) &&
                (worker == null || id < worker.UnitId)) worker = candidate;
        return worker != null;
    }

    public bool RequestBuild(uint type, Vector3 position, uint workerId)
    {
        if (!BuildingCatalog.IsPlayerBuildable(type) || !position.IsFinite() || !_selectedUnitIds.Contains(workerId) ||
            !GodotObject.IsInstanceValid(Buildings) || Buildings.BuildingRequirementBlockReason(type) != null ||
            !_units.TryGetValue(workerId, out Unit worker) || worker.UnitType != 0 || !CanControl(worker)) return false;
        CommandRequested?.Invoke(Protocol.BuildConstruction(type, position.X, position.Z, workerId));
        return true;
    }

    public bool RequestConstruct(Building building, uint workerId)
    {
        if (!GodotObject.IsInstanceValid(building) || building.IsQueuedForDeletion() || !building.IsInsideTree() ||
            !building.IsUnderConstruction || building.SideId != _localTeam ||
            !GodotObject.IsInstanceValid(Buildings) || !Buildings.TryGetBuilding(building.BuildingId, out Building registered) ||
            registered != building || !_selectedUnitIds.Contains(workerId) ||
            !_units.TryGetValue(workerId, out Unit worker) || worker.UnitType != 0 || !CanControl(worker)) return false;
        // 접근 중인 일꾼도 서버에서 예약되므로 사용 중 여부는 서버 응답으로만 판단합니다.
        _targetIndicator.Clear();
        CommandRequested?.Invoke(Protocol.BuildConstruct(building.BuildingId, workerId));
        return true;
    }

    public void RequestMove(Vector3 point)
    {
        ValidateSelection();
        if (_selectedUnitIds.Count == 0)
        {
            GD.Print("내 유닛을 좌클릭으로 먼저 선택하세요.");
            return;
        }
        string command = Protocol.BuildMove(
            _selectedUnitIds,
            point.X,
            point.Z
        );
        _targetIndicator.Clear();
        CommandRequested?.Invoke(command);
    }

    public void RequestStop() => RequestStationaryOrder(false);
    public void RequestHold() => RequestStationaryOrder(true);

    private void RequestStationaryOrder(bool hold)
    {
        ValidateSelection();
        if (_selectedUnitIds.Count == 0) return;
        _targetIndicator.Clear();
        // 각 유닛의 현재 위치와 작업 취소는 서버에서 결정합니다.
        CommandRequested?.Invoke(hold ? Protocol.BuildHold(_selectedUnitIds) : Protocol.BuildStop(_selectedUnitIds));
    }

    private bool IsEnemy(Node3D target)
    {
        if (_localPlayerId == 0 || _localTeam == 0 || !GodotObject.IsInstanceValid(target) ||
            target.IsQueuedForDeletion() || !target.IsInsideTree()) return false;
        return target switch
        {
            Unit unit => !unit.IsDying && unit.Team != _localTeam &&
                _units.TryGetValue(unit.UnitId, out Unit registered) && registered == unit,
            Building building => building.SideId != _localTeam && GodotObject.IsInstanceValid(Buildings) &&
                Buildings.CanInspect(building) &&
                Buildings.TryGetBuilding(building.BuildingId, out Building registered) && registered == building,
            _ => false
        };
    }

    public void RequestAttackMove(Vector3 point)
    {
        ValidateSelection();
        if (_selectedUnitIds.Count == 0 || !float.IsFinite(point.X) || !float.IsFinite(point.Z))
            return;

        // 이동 중 적 탐색과 공격 전환은 서버에서 결정합니다.
        _targetIndicator.Clear();
        CommandRequested?.Invoke(Protocol.BuildAttackMove(_selectedUnitIds, point.X, point.Z));
    }

    // 모든 일반 우클릭의 진입점. 새 대상의 기본 행동은 이곳에 추가합니다.
    public void RequestContextOrder(Node3D target, Vector3 point)
    {
        // 공물은 영웅의 전용 상호작용이다. RTS 선택/집결 명령으로 흘려보내지 않는다.
        if (target is TributeEventView tribute)
        {
            if (!_controlledHeroType.HasValue || !GodotObject.IsInstanceValid(tribute) ||
                tribute.IsQueuedForDeletion() || !tribute.IsVisibleInTree() || !tribute.Active || tribute.EventId == 0) return;
            ValidateSelection();
            Unit hero = _units.Values.FirstOrDefault(unit => CanControl(unit) && UnitCatalog.IsHero(unit.UnitType) &&
                (unit.HealthBar.MaxHP == 0 || unit.HealthBar.CurrentHP > 0));
            if (hero == null) return;
            _targetIndicator.Clear();
            CommandRequested?.Invoke(Protocol.BuildTribute(hero.UnitId, tribute.EventId));
            return;
        }
        if (_controlledHeroType.HasValue)
        {
            ValidateSelection();
            if (_selectedUnitIds.Count == 0) return;
        }
        if (GodotObject.IsInstanceValid(Buildings) && Buildings.SelectedBuilding is Building producer && _selectedUnitIds.Count == 0)
        {
            RequestRally(producer, target, point);
            return;
        }
        switch (target)
        {
            case Unit unit:
                if (!GodotObject.IsInstanceValid(unit) || unit.IsQueuedForDeletion() ||
                    !_units.TryGetValue(unit.UnitId, out Unit registered) || registered != unit)
                    return;
                if (IsEnemy(unit)) RequestAttack(unit);
                else RequestMove(point);
                break;
            case ResourceNode resource:
                if (_controlledHeroType.HasValue) RequestMove(point);
                else RequestGather(resource);
                break;
            case Building building:
                if (!GodotObject.IsInstanceValid(building) || building.IsQueuedForDeletion() ||
                    !GodotObject.IsInstanceValid(Buildings) ||
                    !Buildings.TryGetBuilding(building.BuildingId, out Building registeredBuilding) || registeredBuilding != building)
                    return;
                if (IsEnemy(building)) RequestAttack(building);
                else if (building.IsUnderConstruction && TryGetSelectedWorker(out Unit builder)) RequestConstruct(building, builder.UnitId);
                else RequestMove(point);
                break;
            case null:
                RequestMove(point);
                break;
        }
    }

    private void RequestRally(Building producer, Node3D target, Vector3 point)
    {
        if (!CanUseSelectedBuilding(producer) || !producer.IsProducer || !point.IsFinite()) return;
        if (target != null && (!GodotObject.IsInstanceValid(target) || !target.IsInsideTree() || target.IsQueuedForDeletion())) return;
        if (target == producer)
        {
            CommandRequested?.Invoke(Protocol.BuildRallyClear(producer.BuildingId));
            return;
        }
        if (target is ResourceNode resource)
        {
            if (!GodotObject.IsInstanceValid(Resources) || resource.Amount <= 0 ||
                !Resources.TryGetResource(resource.ResourceId, out var registered) || registered != resource) return;
            if (producer.BuildingType == BuildingCatalog.TownHall)
            {
                CommandRequested?.Invoke(Protocol.BuildRallyGather(producer.BuildingId, resource.ResourceId));
                return;
            }
            point = resource.GlobalPosition;
        }
        // 위치 표시는 RALLY 응답이 온 뒤에만 변경한다. 이동/채집 결정도 서버에서 처리한다.
        CommandRequested?.Invoke(Protocol.BuildRallyMove(producer.BuildingId, point.X, point.Z));
    }

    public void RequestGather(ResourceNode target)
    {
        ValidateSelection();
        if (_selectedUnitIds.Count == 0 || !GodotObject.IsInstanceValid(target) ||
            target.IsQueuedForDeletion() || !target.IsInsideTree() || target.ResourceId == 0 || target.Amount <= 0)
            return;

        // 혼합 선택도 그대로 전달합니다. 서버가 소유권과 채집 능력을 최종 검사합니다.
        _targetIndicator.Show(target);
        CommandRequested?.Invoke(Protocol.BuildGather(target.ResourceId, _selectedUnitIds));
    }

    public void RequestAttack(Node3D target)
    {
        ValidateSelection();
        if (_selectedUnitIds.Count == 0 || !IsEnemy(target))
            return;

        uint id = target is Unit unit ? unit.UnitId : ((Building)target).BuildingId;
        string command = Protocol.BuildAttack(id, _selectedUnitIds);
        _targetIndicator.Show(target);
        CommandRequested?.Invoke(command);
    }

    public void HandleHealth(HealthSnapshot health)
    {
        if (_units.TryGetValue(health.Id, out Unit unit)) unit.ApplyHealth(health);
    }

    public void HandleShield(ShieldSnapshot shield)
    {
        if (_units.TryGetValue(shield.Id, out Unit unit) && !unit.IsDying) unit.HealthBar.ApplyShield(shield.Amount);
    }

    public void HandleRamImpact(RamImpactSnapshot impact)
    {
        if (!_units.TryGetValue(impact.UnitId, out Unit ram) || ram.UnitType != UnitCatalog.SiegeRam ||
            ram.Team != impact.Team || ram.IsDying || !_ramImpacts.Add(impact.UnitId)) return;
        SiegeRamImpact.Spawn(this, impact.Position, impact.Team);
    }

    // Only the explicit server death-heal event creates this effect. REMOVE can also mean devour.
    public void HandleMinionHeal(string[] parts)
    {
        if (parts.Length != 5 || parts[0] != "MINION_HEAL" ||
            !uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out uint id) || id == 0 ||
            !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
            !float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float z) ||
            !float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float radius) ||
            !float.IsFinite(x) || !float.IsFinite(z) || !float.IsFinite(radius) || radius <= 0 ||
            !_units.TryGetValue(id, out Unit source) || source.UnitType != UnitCatalog.MinionHealer ||
            !CanInspect(source) || HasNode($"MinionHeal_{id}")) return;

        var pulse = new MinionHealPulse {
            Name = $"MinionHeal_{id}", Radius = radius,
            VisibilityCheck = EffectVisibilityCheck
        };
        AddChild(pulse);
        pulse.GlobalPosition = new Vector3(x, .08f, z);
    }

    public void HandlePosition(string[] parts)
    {
        if (parts.Length != 4)
            return;

        if (!uint.TryParse(parts[1], out uint unitId))
            return;

        if (!float.TryParse(
                parts[2], NumberStyles.Float,
                CultureInfo.InvariantCulture, out float x))
            return;

        if (!float.TryParse(
                parts[3], NumberStyles.Float,
                CultureInfo.InvariantCulture, out float z))
            return;

        if (!float.IsFinite(x) || !float.IsFinite(z))
            return;

        if (_units.TryGetValue(unitId, out Unit unit))
        {
            if (_interpolation.HasTick) unit.BufferServerPosition(x, z);
            else unit.ApplyServerPosition(x, z); // 첫 TICK 전의 초기 스냅샷/미리보기
        }
    }

    public void HandleTick(string[] parts)
    {
        if (parts.Length != 2 || !uint.TryParse(parts[1], NumberStyles.None,
                CultureInfo.InvariantCulture, out uint tick)) return;
        bool hadTick = _interpolation.HasTick;
        long completedTick = _interpolation.CurrentTick;
        if (!_interpolation.BeginTick(tick)) return;
        // 다음 TICK도 앞 틱의 완료를 보장합니다. 큐가 잠깐 빈 것은 경계가 아닙니다.
        foreach (Unit unit in _units.Values)
            unit.CapturePosition(hadTick ? completedTick : _interpolation.CurrentTick - 1);
    }

    public void HandleTickEnd(string[] parts)
    {
        if (parts.Length != 2 || !uint.TryParse(parts[1], NumberStyles.None,
                CultureInfo.InvariantCulture, out uint tick) || !_interpolation.CompleteTick(tick)) return;
        foreach (Unit unit in _units.Values)
            unit.CapturePosition(_interpolation.CurrentTick);
    }

    public void HandleSpawn(string[] parts)
    {
        // UNIT type id owner x z team. 하수인·이벤트 유닛은 owner 0으로 자동 행동한다.
        if (parts.Length != 7)
            return;

        if (!uint.TryParse(parts[1], out uint unitType))
            return;

        if (!uint.TryParse(parts[2], out uint unitId) || unitId == 0)
            return;

        if (!uint.TryParse(parts[3], out uint ownerID))
            return;

        if ((ownerID == 0) != UnitCatalog.IsAutonomous(unitType)) return;

        if (!uint.TryParse(parts[6], out uint team) || team == 0)
            return;

        if (!float.TryParse(
                parts[4], NumberStyles.Float,
                CultureInfo.InvariantCulture, out float x))
            return;

        if (!float.TryParse(
                parts[5], NumberStyles.Float,
                CultureInfo.InvariantCulture, out float z))
            return;

        if (!float.IsFinite(x) || !float.IsFinite(z))
            return;

        PackedScene scene = SceneFor(unitType);

        if (scene == null)
        {
            GD.PushWarning($"유닛 리소스를 확인하세요. 타입: {unitType}");
            return;
        }

        // 이미 등록된 유닛이면 중복 생성하지 않고 정보 갱신
        if (_units.TryGetValue(unitId, out Unit existing))
        {
            if (existing.UnitType != unitType)
            {
                GD.PushWarning($"이미 등록된 유닛의 타입이 다릅니다. ID: {unitId}");
                return;
            }
            existing.Initialize(unitId, ownerID, team);
            if (!CanControl(existing)) ForgetGroupUnit(unitId);
            if (_targetIndicator.Target == existing && !IsEnemy(existing)) _targetIndicator.Clear();
            existing.ApplyServerPosition(x, z);
            if (_interpolation.HasTick) existing.CapturePosition(_interpolation.CurrentTick);
            ValidateSelection();
            if (InspectedUnit == existing) SelectionChanged?.Invoke();
            return;
        }

        Unit unit = scene.Instantiate<Unit>();

        unit.ResolveFocus = ResolveFocus;

        unit.Initialize(unitId, ownerID, team);
        unit.SetLocalTeam(_localTeam);
        unit.Name = $"Unit_{unitId}";

        AddChild(unit);

        unit.ApplyServerPosition(x, z);
        if (_interpolation.HasTick) unit.CapturePosition(_interpolation.CurrentTick);

        _units.Add(unitId, unit);
        if (_controlledHeroType.HasValue) ValidateSelection();
    }

    public Node3D ResolveFocus(uint id)
    {
        if (_units.TryGetValue(id, out Unit unit)) return unit;
        if (GodotObject.IsInstanceValid(Buildings) && Buildings.TryGetBuilding(id, out Building building))
            return building;
        if (GodotObject.IsInstanceValid(Resources) && Resources.TryGetResource(id, out ResourceNode resource))
            return resource;
        return null;
    }

    // STATE id IDLE|GATHER|ATTACK carrying focusId swingSequence
    public void HandleState(StateSnapshot snapshot)
    {
        if (_units.TryGetValue(snapshot.Id, out Unit unit)) unit.ApplyServerState(snapshot.State);
    }

    public void HandleRemove(string[] parts)
    {
        if (parts.Length != 2 || !uint.TryParse(parts[1], out uint unitId))
            return;

        if (!_units.Remove(unitId, out Unit unit))
            return;

        unit.SetSelected(false);
        bool selectionChanged = _selectedUnitIds.Remove(unitId);
        if (InspectedUnit == unit) { InspectedUnit = null; selectionChanged = true; }
        ForgetGroupUnit(unitId);
        if (_targetIndicator.Target == unit || _selectedUnitIds.Count == 0) _targetIndicator.Clear();
        unit.Name = $"Dying_{unitId}";
        unit.BeginDeath();
        if (_controlledHeroType.HasValue) selectionChanged |= ValidateSelection(notify: false);
        if (selectionChanged) SelectionChanged?.Invoke();
    }

    // HIDE는 사망이 아닙니다. 대상 조회·입력에서 즉시 빼고, 재등장 시 새 스냅샷을 받습니다.
    public void HandleHide(string[] parts)
    {
        if (parts.Length != 2 || !uint.TryParse(parts[1], out uint id)) return;
        if (GetNodeOrNull<MinionHealPulse>($"MinionHeal_{id}") is MinionHealPulse pulse)
        {
            RemoveChild(pulse);
            pulse.QueueFree();
        }
        if (!_units.Remove(id, out Unit unit)) return;
        unit.SetSelected(false);
        bool selectionChanged = _selectedUnitIds.Remove(id);
        if (InspectedUnit == unit) { InspectedUnit = null; selectionChanged = true; }
        ForgetGroupUnit(id);
        if (_targetIndicator.Target == unit || _selectedUnitIds.Count == 0) _targetIndicator.Clear();
        unit.Hide();
        RemoveChild(unit);
        unit.QueueFree();
        if (_controlledHeroType.HasValue) selectionChanged |= ValidateSelection(notify: false);
        if (selectionChanged) SelectionChanged?.Invoke();
    }

    public void SelectSingle(Unit unit)
    {
        if (_controlledHeroType.HasValue) { ValidateSelection(); return; }
        if (!CanInspect(unit))
        {
            ClearSelection();
            return;
        }

        bool controllable = CanControl(unit);
        bool selectionChanged = controllable ? _selectedUnitIds.Count != 1 || !_selectedUnitIds.Contains(unit.UnitId)
            : InspectedUnit != unit || _selectedUnitIds.Count > 0;
        ClearSelectionCore();
        if (controllable) SelectUnit(unit);
        else { InspectedUnit = unit; unit.SetSelected(true); }
        if (selectionChanged) SelectionChanged?.Invoke();
    }

    private void SelectUnit(Unit unit)
    {
        if (_selectedUnitIds.Count < MaxSelectedUnits && _selectedUnitIds.Add(unit.UnitId))
            unit.SetSelected(true);
    }

    public bool CanControl(Unit unit) => GodotObject.IsInstanceValid(unit) && unit.IsInsideTree() &&
        !unit.IsQueuedForDeletion() && !unit.IsDying && _localPlayerId != 0 && unit.OwnerId == _localPlayerId &&
        (!_controlledHeroType.HasValue || unit.UnitType == _controlledHeroType.Value && unit.Team == _localTeam) &&
        _units.TryGetValue(unit.UnitId, out Unit registered) && registered == unit;

    public bool CanInspect(Unit unit) => GodotObject.IsInstanceValid(unit) && unit.IsInsideTree() && unit.IsVisibleInTree() &&
        !unit.IsQueuedForDeletion() && !unit.IsDying && _localPlayerId != 0 &&
        _units.TryGetValue(unit.UnitId, out Unit registered) && registered == unit;

    public void ToggleSelection(Unit unit)
    {
        if (CanControl(unit)) ApplyCandidates(new List<Unit> { unit }, toggle: true);
    }

    public void SelectSameTypeOnScreen(Unit clicked, bool additive)
    {
        if (!CanControl(clicked) || !GodotObject.IsInstanceValid(Camera)) return;
        var candidates = new List<Unit> { clicked };
        Rect2 screen = GetViewport().GetVisibleRect();
        foreach (Unit unit in _units.Values)
            if (unit != clicked && unit.UnitType == clicked.UnitType && IsOnScreen(unit, screen)) candidates.Add(unit);
        // 더블클릭의 첫 클릭이 이미 선택을 바꿨으므로 Shift+더블클릭은 추가만 합니다.
        ApplyCandidates(candidates, toggle: false, additive: additive);
    }

    public void SaveControlGroup(int number)
    {
        if (number < 1 || number > 9) return;
        ValidateSelection();
        _controlGroups[number] = new HashSet<uint>(_selectedUnitIds);
    }

    public bool RecallControlGroup(int number)
    {
        if (!_controlGroups.TryGetValue(number, out HashSet<uint> ids)) return false;
        var candidates = new List<Unit>();
        foreach (uint id in ids)
            if (_units.TryGetValue(id, out Unit unit) && CanControl(unit)) candidates.Add(unit);
        if (candidates.Count == 0) return false;
        candidates.Sort((left, right) => left.UnitId.CompareTo(right.UnitId));
        ApplyCandidates(candidates, toggle: false);
        return true;
    }

    public bool TryGetSelectionCenter(out Vector3 center)
    {
        center = Vector3.Zero;
        int count = 0;
        foreach (uint id in _selectedUnitIds)
            if (_units.TryGetValue(id, out Unit unit) && CanControl(unit)) { center += unit.GlobalPosition; count++; }
        if (count == 0) return false;
        center /= count;
        return true;
    }

    private void ForgetGroupUnit(uint id)
    {
        foreach (HashSet<uint> group in _controlGroups.Values) group.Remove(id);
    }

    // 초상화는 현재 선택 안에서만 좁힙니다. 다른 소유자의 유닛을 선택에 추가하지 않습니다.
    public void SelectFromPortrait(uint id, bool exclude, bool sameType)
    {
        ValidateSelection();
        if (_controlledHeroType.HasValue) return;
        if (!_selectedUnitIds.Contains(id) || !_units.TryGetValue(id, out Unit clicked)) return;
        if (!exclude && !sameType)
        {
            SelectSingle(clicked);
            return;
        }

        bool changed = false;
        foreach (uint selectedId in new List<uint>(_selectedUnitIds))
        {
            Unit unit = _units[selectedId];
            bool matches = sameType ? unit.UnitType == clicked.UnitType : selectedId == id;
            if (exclude ? !matches : matches) continue;
            _selectedUnitIds.Remove(selectedId);
            unit.SetSelected(false);
            changed = true;
        }
        if (_selectedUnitIds.Count == 0) _targetIndicator.Clear();
        if (changed) SelectionChanged?.Invoke();
    }

    public void ClearSelection()
    {
        if (_controlledHeroType.HasValue) { ValidateSelection(); return; }
        bool selectionChanged = _selectedUnitIds.Count > 0 || InspectedUnit != null;
        ClearSelectionCore();
        if (selectionChanged) SelectionChanged?.Invoke();
    }

    private void ClearSelectionCore()
    {
        ClearInspection();
        _targetIndicator.Clear();
        foreach (uint id in _selectedUnitIds)
            if (_units.TryGetValue(id, out Unit unit))
                unit.SetSelected(false);
        _selectedUnitIds.Clear();
    }

    private void ClearInspection()
    {
        if (GodotObject.IsInstanceValid(InspectedUnit)) InspectedUnit.SetSelected(false);
        InspectedUnit = null;
    }

    public void SelectBox(Rect2 rect) => SelectBoxWithMode(rect, false);

    public void SelectBoxWithMode(Rect2 rect, bool toggle)
    {
        var candidates = new List<Unit>();
        foreach (Unit unit in _units.Values)
            if (IsOnScreen(unit, rect)) candidates.Add(unit);
        ApplyCandidates(candidates, toggle);
    }

    private bool IsOnScreen(Unit unit, Rect2 rect)
    {
        if (!CanControl(unit) || !unit.IsVisibleInTree() || !GodotObject.IsInstanceValid(Camera)) return false;
        Vector3 center = unit.GlobalPosition + Vector3.Up;
        return Camera.IsPositionInFrustum(center) && rect.HasPoint(Camera.UnprojectPosition(center));
    }

    private void ApplyCandidates(List<Unit> candidates, bool toggle, bool additive = false)
    {
        if (_controlledHeroType.HasValue) { ValidateSelection(); return; }
        bool inspected = InspectedUnit != null;
        ClearInspection();
        var previousSelection = new HashSet<uint>(_selectedUnitIds);
        bool remove = toggle && candidates.Count > 0 && candidates.TrueForAll(unit => _selectedUnitIds.Contains(unit.UnitId));
        if (!toggle && !additive) ClearSelectionCore();
        else _targetIndicator.Clear();
        foreach (Unit unit in candidates)
            if (remove)
            {
                _selectedUnitIds.Remove(unit.UnitId);
                unit.SetSelected(false);
            }
            else SelectUnit(unit);
        if (inspected || !previousSelection.SetEquals(_selectedUnitIds)) SelectionChanged?.Invoke();
    }

    private bool ValidateSelection(bool notify = true)
    {
        if (_controlledHeroType.HasValue)
        {
            Unit hero = _units.Values.FirstOrDefault(CanControl);
            bool changed = InspectedUnit != null || (hero == null ? _selectedUnitIds.Count != 0
                : _selectedUnitIds.Count != 1 || !_selectedUnitIds.Contains(hero.UnitId));
            if (!changed) return false;
            ClearSelectionCore();
            if (hero != null) SelectUnit(hero);
            if (notify) SelectionChanged?.Invoke();
            return true;
        }
        bool inspectionRemoved = InspectedUnit != null && !CanInspect(InspectedUnit);
        if (inspectionRemoved) ClearInspection();
        int removed = _selectedUnitIds.RemoveWhere(id =>
        {
            if (!_units.TryGetValue(id, out Unit unit))
                return true;
            if (_localPlayerId != 0 && unit.OwnerId == _localPlayerId && !unit.IsQueuedForDeletion())
                return false;
            unit.SetSelected(false);
            return true;
        });
        if (_selectedUnitIds.Count == 0) _targetIndicator.Clear();
        if (notify && (removed > 0 || inspectionRemoved)) SelectionChanged?.Invoke();
        return removed > 0 || inspectionRemoved;
    }

}
