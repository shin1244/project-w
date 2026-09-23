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
    public IReadOnlyCollection<uint> SelectedUnitIds => _selectedUnitIds;
    private readonly HashSet<uint> _selectedUnitIds = new();
    private const int MaxSelectedUnits = 64;
    private readonly Dictionary<uint, Unit> _units = new();
    private uint _localPlayerId;
    private readonly CommandTargetIndicator _targetIndicator = new();

    public override void _Process(double delta) => _targetIndicator.Refresh();
    public override void _ExitTree() => _targetIndicator.Clear();

    public event Action<string> CommandRequested;

    public void Clear()
    {
        ClearSelection();
        // 사망 연출 중인 유닛은 이미 사전에서 빠졌으므로 씬 자식도 함께 정리합니다.
        foreach (Node child in GetChildren())
        {
            if (child is not Unit unit) continue;
            RemoveChild(unit);
            unit.QueueFree();
        }
        _units.Clear();
        _localPlayerId = 0;
    }

    public void SetLocalPlayer(uint playerId)
    {
        _localPlayerId = playerId;
        ValidateSelection();
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

    private bool IsEnemy(Unit unit)
    {
        // 아직 팀 정보가 없으므로 소유자가 다른 유닛을 적으로 취급합니다.
        return _localPlayerId != 0 && GodotObject.IsInstanceValid(unit)
            && !unit.IsQueuedForDeletion() && unit.OwnerId != _localPlayerId
            && _units.TryGetValue(unit.UnitId, out Unit registered) && registered == unit;
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

    public void RequestAttack(Unit target)
    {
        ValidateSelection();
        if (_selectedUnitIds.Count == 0 || !IsEnemy(target))
            return;

        string command = Protocol.BuildAttack(target.UnitId, _selectedUnitIds);
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
            unit.ApplyServerPosition(x, z);
    }

    public void HandleSpawn(string[] parts)
    {
        if (parts.Length != 6)
            return;

        if (!uint.TryParse(parts[1], out uint unitType))
            return;

        if (!uint.TryParse(parts[2], out uint unitId))
            return;

        if (!uint.TryParse(parts[3], out uint ownerID))
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
            existing.Initialize(unitId, ownerID);
            existing.ApplyServerPosition(x, z);
            ValidateSelection();
            return;
        }

        Unit unit = scene.Instantiate<Unit>();

        unit.ResolveFocus = ResolveFocus;

        unit.Initialize(unitId, ownerID);
        unit.Name = $"Unit_{unitId}";

        AddChild(unit);

        unit.ApplyServerPosition(x, z);

        _units.Add(unitId, unit);
    }

    private Node3D ResolveFocus(uint id)
    {
        if (_units.TryGetValue(id, out Unit unit)) return unit;
        if (GodotObject.IsInstanceValid(Resources) && Resources.TryGetResource(id, out ResourceNode resource))
            return resource;
        return null;
    }

    // STATE id IDLE|GATHER|ATTACK carrying focusId swingSequence
    public void HandleState(string[] parts)
    {
        if (parts.Length != 6 || !uint.TryParse(parts[1], out uint id) ||
            !int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out int carrying) ||
            !uint.TryParse(parts[4], out uint focusId) ||
            !uint.TryParse(parts[5], out uint sequence))
            return;

        UnitActivity? activity = parts[2] switch
        {
            "IDLE" => UnitActivity.Idle,
            "GATHER" => UnitActivity.Gather,
            "ATTACK" => UnitActivity.Attack,
            _ => null
        };
        if (activity.HasValue && _units.TryGetValue(id, out Unit unit))
            unit.ApplyServerState(new UnitState(activity.Value, carrying, focusId, sequence));
    }

    public void HandleRemove(string[] parts)
    {
        if (parts.Length != 2 || !uint.TryParse(parts[1], out uint unitId))
            return;

        if (!_units.Remove(unitId, out Unit unit))
            return;

        unit.SetSelected(false);
        _selectedUnitIds.Remove(unitId);
        if (_targetIndicator.Target == unit || _selectedUnitIds.Count == 0) _targetIndicator.Clear();
        unit.Name = $"Dying_{unitId}";
        unit.BeginDeath();
    }

    public void SelectSingle(Unit unit)
    {
        if (!GodotObject.IsInstanceValid(unit) || unit.IsQueuedForDeletion()
            || _localPlayerId == 0 || unit.OwnerId != _localPlayerId
            || !_units.TryGetValue(unit.UnitId, out Unit registered) || registered != unit)
        {
            ClearSelection();
            return;
        }

        ClearSelection();
        SelectUnit(unit);
    }

    private void SelectUnit(Unit unit)
    {
        if (_selectedUnitIds.Count < MaxSelectedUnits && _selectedUnitIds.Add(unit.UnitId))
            unit.SetSelected(true);
    }

    public void ClearSelection()
    {
        _targetIndicator.Clear();
        foreach (uint id in _selectedUnitIds)
            if (_units.TryGetValue(id, out Unit unit))
                unit.SetSelected(false);
        _selectedUnitIds.Clear();
    }

    public void SelectBox(Rect2 rect)
    {
        ClearSelection();
        if (_localPlayerId == 0 || !GodotObject.IsInstanceValid(Camera))
            return;

        foreach (Unit unit in _units.Values)
        {
            if (unit.IsQueuedForDeletion() || unit.OwnerId != _localPlayerId)
                continue;

            Vector3 center = unit.GlobalPosition + Vector3.Up;
            if (!Camera.IsPositionInFrustum(center))
                continue;

            if (rect.HasPoint(Camera.UnprojectPosition(center)))
                SelectUnit(unit);

            if (_selectedUnitIds.Count >= MaxSelectedUnits)
                break;
        }
    }

    private void ValidateSelection()
    {
        _selectedUnitIds.RemoveWhere(id =>
        {
            if (!_units.TryGetValue(id, out Unit unit))
                return true;
            if (_localPlayerId != 0 && unit.OwnerId == _localPlayerId && !unit.IsQueuedForDeletion())
                return false;
            unit.SetSelected(false);
            return true;
        });
        if (_selectedUnitIds.Count == 0) _targetIndicator.Clear();
    }

}
