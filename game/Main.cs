using Godot;
using System;
using System.Globalization;
using System.Collections.Generic;

public partial class Main : Node3D
{
    [Export] public Camera3D Camera;
    [Export] public PackedScene KnightScene;
    [Export] public PackedScene ArcherScene;
    public uint SelectedUnitId { get; private set; }
    private readonly Dictionary<uint, Unit> _units = new();
    private uint _localPlayerId;

    private NetClient _net;
    private PlayerInput _playerInput;
    public override void _Ready()
    {
        _playerInput = GetNode<PlayerInput>("PlayerInput");
        _playerInput.UnitClicked += OnUnitClicked;
        _playerInput.SelectionCleared += OnSelectionCleared;
        _playerInput.GroundRightClicked += OnGroundRightClicked;
        _net = GetNode<NetClient>("/root/Net");
        _net.MessageReceived += OnMessage;
        _net.ConnectToServer("127.0.0.1", 7777);
    }

    private void OnMessage(string msg)
    {
        string[] parts = msg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return;

        switch (parts[0])
        {
            case "WELCOME":
                if (parts.Length == 2 && uint.TryParse(parts[1], out uint playerId))
                {
                    _localPlayerId = playerId;
                    ValidateSelection();
                }
                break;
            case "POS":
                HandlePosition(parts);
                break;
            case "UNIT":
                HandleSpawn(parts);
                break;
            case "REMOVE":
                HandleRemove(parts);
                break;
            case "ERR":
                GD.PushWarning(msg);
                break;
            default:
                GD.Print($"받음: {msg}");
                break;
        }
    }

    private void OnGroundRightClicked(Vector3 point)
    {
        if (_localPlayerId == 0 || !_units.TryGetValue(SelectedUnitId, out Unit selected)
            || selected.OwnerId != _localPlayerId)
        {
            GD.Print("내 유닛을 좌클릭으로 먼저 선택하세요.");
            return;
        }
        string command = Protocol.BuildMove(
            new uint[] { SelectedUnitId },
            point.X,
            point.Z
        );
        _net.Send(command);
        GD.Print($"전송 요청: {command}");
    }

    private void HandlePosition(string[] parts)
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

    private void HandleSpawn(string[] parts)
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
            0 => KnightScene,
            1 => ArcherScene,
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

        unit.Initialize(unitId, ownerID);
        unit.Name = $"Unit_{unitId}";

        AddChild(unit);

        unit.ApplyServerPosition(x, z);

        _units.Add(unitId, unit);
    }

    private void HandleRemove(string[] parts)
    {
        if (parts.Length != 2 || !uint.TryParse(parts[1], out uint unitId))
            return;

        if (!_units.Remove(unitId, out Unit unit))
            return;

        unit.SetSelected(false);
        if (SelectedUnitId == unitId)
            SelectedUnitId = 0;
        unit.QueueFree();
    }

    private void OnUnitClicked(Unit unit)
    {
        if (!GodotObject.IsInstanceValid(unit) || unit.IsQueuedForDeletion()
            || _localPlayerId == 0 || unit.OwnerId != _localPlayerId
            || !_units.TryGetValue(unit.UnitId, out Unit registered) || registered != unit)
        {
            SelectUnit(0);
            return;
        }

        SelectUnit(unit.UnitId);
    }

    private void OnSelectionCleared() => SelectUnit(0);

    private void SelectUnit(uint unitId)
    {
        if (_units.TryGetValue(SelectedUnitId, out Unit previous))
            previous.SetSelected(false);

        SelectedUnitId = unitId;

        if (_units.TryGetValue(SelectedUnitId, out Unit selected))
            selected.SetSelected(true);
    }

    private void ValidateSelection()
    {
        if (!_units.TryGetValue(SelectedUnitId, out Unit selected)
            || selected.OwnerId != _localPlayerId)
            SelectUnit(0);
    }

    public override void _ExitTree()
    {
        if (_playerInput != null)
        {
            _playerInput.UnitClicked -= OnUnitClicked;
            _playerInput.SelectionCleared -= OnSelectionCleared;
            _playerInput.GroundRightClicked -= OnGroundRightClicked;
        }

        if (_net != null)
            _net.MessageReceived -= OnMessage;
    }
}
