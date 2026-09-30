using Godot;
using System;
using System.Globalization;
using System.Collections.Generic;

public partial class UnitManager : Node3D
{
    [Export] public Camera3D Camera;
    [Export] public PackedScene WorkerScene;
    [Export] public PackedScene KnightScene;
    [Export] public PackedScene ArcherScene;
    [Export] public ResourceManager Resources;
    [Export] public BuildingManager Buildings;
    public IReadOnlyCollection<uint> SelectedUnitIds => _selectedUnitIds;
    public IReadOnlyCollection<Unit> LiveUnits => _units.Values;
    public uint LocalTeam => _localTeam;
    public bool TryGetUnit(uint id, out Unit unit) => _units.TryGetValue(id, out unit);
    public event Action SelectionChanged;
    public event Action PositionsRendered;
    private readonly HashSet<uint> _selectedUnitIds = new();
    private readonly Dictionary<int, HashSet<uint>> _controlGroups = new();
    private const int MaxSelectedUnits = 64;
    private readonly Dictionary<uint, Unit> _units = new();
    private uint _localPlayerId;
    private uint _localTeam;
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
        ClearSelectionCore();
        // 사망 연출 중인 유닛은 이미 사전에서 빠졌으므로 씬 자식도 함께 정리합니다.
        foreach (Node child in GetChildren())
        {
            if (child is not Unit unit) continue;
            RemoveChild(unit);
            unit.QueueFree();
        }
        _units.Clear();
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

    public void RequestTrainWorker()
    {
        if (_localPlayerId == 0 || _localTeam == 0 || _selectedUnitIds.Count > 0 ||
            !GodotObject.IsInstanceValid(Buildings)) return;

        Building hall = Buildings.SelectedBuilding;
        if (!GodotObject.IsInstanceValid(hall) || hall.IsQueuedForDeletion() || !hall.IsInsideTree() ||
            hall.BuildingType != 0 || hall.SideId != _localTeam ||
            !Buildings.TryGetBuilding(hall.BuildingId, out Building registered) || registered != hall) return;

        // 생성 가능 여부와 비용은 서버에서 결정하고, UNIT 응답을 받은 뒤 표시합니다.
        CommandRequested?.Invoke(Protocol.BuildTrain(0));
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

    private bool IsEnemy(Node3D target)
    {
        if (_localPlayerId == 0 || _localTeam == 0 || !GodotObject.IsInstanceValid(target) ||
            target.IsQueuedForDeletion() || !target.IsInsideTree()) return false;
        return target switch
        {
            Unit unit => !unit.IsDying && unit.Team != _localTeam &&
                _units.TryGetValue(unit.UnitId, out Unit registered) && registered == unit,
            Building building => building.SideId != _localTeam && GodotObject.IsInstanceValid(Buildings) &&
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
                RequestGather(resource);
                break;
            case Building building:
                if (!GodotObject.IsInstanceValid(building) || building.IsQueuedForDeletion() ||
                    !GodotObject.IsInstanceValid(Buildings) ||
                    !Buildings.TryGetBuilding(building.BuildingId, out Building registeredBuilding) || registeredBuilding != building)
                    return;
                if (IsEnemy(building)) RequestAttack(building);
                else RequestMove(point);
                break;
            case null:
                RequestMove(point);
                break;
        }
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
        // 다음 TICK이 와야 앞 틱의 POS가 모두 수신되었습니다. 큐가 잠깐 빈 것은 경계가 아닙니다.
        foreach (Unit unit in _units.Values)
            unit.CapturePosition(hadTick ? completedTick : _interpolation.CurrentTick - 1);
    }

    public void HandleSpawn(string[] parts)
    {
        // UNIT type id owner x z team. 미니언은 기존 기사/궁수 씬으로 표시합니다.
        if (parts.Length != 7)
            return;

        if (!uint.TryParse(parts[1], out uint unitType))
            return;

        if (!uint.TryParse(parts[2], out uint unitId) || unitId == 0)
            return;

        if (!uint.TryParse(parts[3], out uint ownerID))
            return;

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

        PackedScene scene = unitType switch
        {
            0 => WorkerScene,
            1 => KnightScene,
            2 => ArcherScene,
            _ => null
        };

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
        ForgetGroupUnit(unitId);
        if (_targetIndicator.Target == unit || _selectedUnitIds.Count == 0) _targetIndicator.Clear();
        unit.Name = $"Dying_{unitId}";
        unit.BeginDeath();
        if (selectionChanged) SelectionChanged?.Invoke();
    }

    // HIDE는 사망이 아닙니다. 대상 조회·입력에서 즉시 빼고, 재등장 시 새 스냅샷을 받습니다.
    public void HandleHide(string[] parts)
    {
        if (parts.Length != 2 || !uint.TryParse(parts[1], out uint id) ||
            !_units.Remove(id, out Unit unit)) return;
        unit.SetSelected(false);
        bool selectionChanged = _selectedUnitIds.Remove(id);
        ForgetGroupUnit(id);
        if (_targetIndicator.Target == unit || _selectedUnitIds.Count == 0) _targetIndicator.Clear();
        unit.Hide();
        RemoveChild(unit);
        unit.QueueFree();
        if (selectionChanged) SelectionChanged?.Invoke();
    }

    public void SelectSingle(Unit unit)
    {
        if (!CanControl(unit))
        {
            ClearSelection();
            return;
        }

        bool selectionChanged = _selectedUnitIds.Count != 1 || !_selectedUnitIds.Contains(unit.UnitId);
        ClearSelectionCore();
        SelectUnit(unit);
        if (selectionChanged) SelectionChanged?.Invoke();
    }

    private void SelectUnit(Unit unit)
    {
        if (_selectedUnitIds.Count < MaxSelectedUnits && _selectedUnitIds.Add(unit.UnitId))
            unit.SetSelected(true);
    }

    public bool CanControl(Unit unit) => GodotObject.IsInstanceValid(unit) && unit.IsInsideTree() &&
        !unit.IsQueuedForDeletion() && !unit.IsDying && _localPlayerId != 0 && unit.OwnerId == _localPlayerId &&
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
        bool selectionChanged = _selectedUnitIds.Count > 0;
        ClearSelectionCore();
        if (selectionChanged) SelectionChanged?.Invoke();
    }

    private void ClearSelectionCore()
    {
        _targetIndicator.Clear();
        foreach (uint id in _selectedUnitIds)
            if (_units.TryGetValue(id, out Unit unit))
                unit.SetSelected(false);
        _selectedUnitIds.Clear();
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
        if (!previousSelection.SetEquals(_selectedUnitIds)) SelectionChanged?.Invoke();
    }

    private bool ValidateSelection(bool notify = true)
    {
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
        if (notify && removed > 0) SelectionChanged?.Invoke();
        return removed > 0;
    }

}
