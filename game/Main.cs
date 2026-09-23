using Godot;
using System;

// 입력·통신과 각 관리자를 연결합니다. 개별 오브젝트의 상태는 관리자에게 맡깁니다.
public partial class Main : Node3D
{
    [Export] public UnitManager Units;
    [Export] public BuildingManager Buildings;
    [Export] public ResourceManager Resources;
    [Export] public MapWorld Map;
    [Export] public Label SyncStatus;

    private NetClient _net;
    private PlayerInput _playerInput;

    public override void _Ready()
    {
        if (Map.SyncError != null)
        {
            ShowStatus(Map.SyncError);
            return;
        }
        _playerInput = GetNode<PlayerInput>("PlayerInput");
        _playerInput.UnitClicked += Units.SelectSingle;
        _playerInput.SelectionCleared += Units.ClearSelection;
        _playerInput.BoxSelectionRequested += Units.SelectBox;
        _playerInput.ContextClicked += Units.RequestContextOrder;
        _playerInput.AttackTargetClicked += Units.RequestAttack;
        _playerInput.AttackGroundClicked += Units.RequestAttackMove;
        Units.CommandRequested += SendCommand;

        _net = GetNode<NetClient>("/root/Net");
        _net.MessageReceived += OnMessage;
        _net.ConnectionClosed += OnConnectionClosed;
        _net.ConnectToServer("127.0.0.1", 7777);
    }

    private void OnMessage(string message)
    {
        string[] parts = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return;

        switch (parts[0])
        {
            case "MAP":
                if (!Map.AcceptMap(parts)) { RejectMap(); return; }
                Units.Clear();
                Buildings?.Clear();
                _net.Send($"MAP_READY {Map.MapHash}");
                return;
            case "TREE":
                if (!Map.ApplyTree(parts)) RejectMap();
                return;
            case "WORLD_READY":
                if (parts.Length != 1 || !Map.CompleteSync()) { RejectMap(); return; }
                if (SyncStatus != null) SyncStatus.Hide();
                return;
        }
        if (!Map.IsSynchronized)
            return;

        switch (parts[0])
        {
            case "WELCOME":
                if (parts.Length == 2 && uint.TryParse(parts[1], out uint playerId))
                    Units.SetLocalPlayer(playerId);
                break;
            case "UNIT":
                Units.HandleSpawn(parts);
                break;
            case "BUILDING":
                Buildings?.HandleSpawn(parts);
                break;
            case "POS":
                Units.HandlePosition(parts);
                break;
            case "HP":
                if (HealthSnapshot.TryParse(parts, out var health))
                {
                    Units.HandleHealth(health);
                    Buildings?.HandleHealth(health);
                }
                break;
            case "STATE":
                Units.HandleState(parts);
                break;
            case "REMOVE":
                Units.HandleRemove(parts);
                Buildings?.HandleRemove(parts);
                break;
            case "ERR":
                GD.PushWarning(message);
                break;
            default:
                GD.Print($"받음: {message}");
                break;
        }
    }

    private void SendCommand(string command)
    {
        if (!Map.IsSynchronized) return;
        _net.Send(command);
        GD.Print($"전송 요청: {command}");
    }

    private void RejectMap()
    {
        ShowStatus(Map.SyncError ?? "맵 동기화 메시지가 올바르지 않습니다.");
        Map.StopSync();
        _net?.Disconnect();
    }

    private void OnConnectionClosed(string reason)
    {
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
        if (_playerInput != null && GodotObject.IsInstanceValid(Units))
        {
            _playerInput.UnitClicked -= Units.SelectSingle;
            _playerInput.SelectionCleared -= Units.ClearSelection;
            _playerInput.BoxSelectionRequested -= Units.SelectBox;
            _playerInput.ContextClicked -= Units.RequestContextOrder;
            _playerInput.AttackTargetClicked -= Units.RequestAttack;
            _playerInput.AttackGroundClicked -= Units.RequestAttackMove;
        }

        if (GodotObject.IsInstanceValid(Units))
            Units.CommandRequested -= SendCommand;
        if (_net != null)
        {
            _net.MessageReceived -= OnMessage;
            _net.ConnectionClosed -= OnConnectionClosed;
            _net.Disconnect();
        }
    }
}
