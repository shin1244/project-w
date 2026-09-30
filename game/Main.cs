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
    [Export] public StockDisplay Stock;
    [Export] public FogOfWar Fog;
    [Export] public Minimap Minimap;
    [Export] public CommandPanel Commands;
    [Export] public BuildingPlacement Placement;

    private NetClient _net;
    private PlayerInput _playerInput;

    public override void _Ready()
    {
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
        _net.ConnectToServer("127.0.0.1", 7777);
    }

    private void ConnectInput()
    {
        _playerInput = GetNode<PlayerInput>("PlayerInput");
        _playerInput.UnitClicked += SelectUnit;
        _playerInput.BuildingClicked += SelectBuilding;
        _playerInput.SelectionCleared += ClearSelection;
        _playerInput.BoxSelectionRequested += SelectBox;
        _playerInput.ContextClicked += Units.RequestContextOrder;
        _playerInput.AttackTargetClicked += Units.RequestAttack;
        _playerInput.AttackGroundClicked += Units.RequestAttackMove;
        _playerInput.ModifiedUnitSelectionRequested += ModifyUnitSelection;
        _playerInput.ModifiedBoxSelectionRequested += ModifyBoxSelection;
        _playerInput.ControlGroupRequested += HandleControlGroup;
        if (Commands != null) Commands.BuildRequested += BeginPlacement;
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

    private void ModifyUnitSelection(Unit unit, bool shift, bool sameType)
    {
        if (!Units.CanControl(unit)) { SelectUnit(unit); return; }
        Buildings?.ClearSelection();
        if (sameType) Units.SelectSameTypeOnScreen(unit, shift);
        else Units.ToggleSelection(unit);
    }

    private void ModifyBoxSelection(Rect2 rect)
    {
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

    private void SelectUnit(Unit unit)
    {
        Buildings?.ClearSelection();
        Units.SelectSingle(unit);
    }

    private void SelectBuilding(Building building)
    {
        Units.ClearSelection();
        Buildings?.SelectSingle(building);
    }

    private void ClearSelection()
    {
        Units.ClearSelection();
        Buildings?.ClearSelection();
    }

    private void SelectBox(Rect2 rect)
    {
        Buildings?.ClearSelection();
        Units.SelectBox(rect);
    }

    private void OnMessage(string message)
    {
        string[] parts = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return;

        switch (parts[0])
        {
            case "MAP":
                _playerInput?.ResetInteraction();
                Stock?.Clear();
                Fog?.Reset();
                Minimap?.Reset();
                if (!Map.AcceptMap(parts)) { RejectMap(); return; }
                Units.Clear();
                Buildings?.Clear();
                _net.Send($"MAP_READY {Map.MapHash}");
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
            case "TICK":
                Units.HandleTick(parts);
                break;
            case "WELCOME":
                if (parts.Length == 3 && uint.TryParse(parts[1], out uint playerId) && playerId != 0 &&
                    uint.TryParse(parts[2], out uint team) && team != 0)
                {
                    Units.SetLocalPlayer(playerId, team);
                    Buildings?.SetLocalTeam(team);
                    Fog?.SetTeam(team);
                    Minimap?.SetTeam(team);
                }
                break;
            case "SIGHT":
                Fog?.HandleSight(parts);
                break;
            case "BODY":
                Fog?.HandleBody(parts);
                break;
            case "UNIT":
                Units.HandleSpawn(parts);
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
                }
                break;
            case "STATS":
                if (StatsSnapshot.TryParse(parts, out var stats))
                {
                    if (Units.TryGetUnit(stats.Id, out Unit statsUnit)) statsUnit.Stats = stats;
                    if (Buildings != null && Buildings.TryGetBuilding(stats.Id, out Building statsBuilding)) statsBuilding.Stats = stats;
                    Fog?.Invalidate();
                }
                break;
            case "STATE":
                if (StateSnapshot.TryParse(parts, out var state))
                {
                    Units.HandleState(state);
                    Buildings?.HandleState(state);
                }
                break;
            case "STOCK":
                Stock?.HandleStock(parts);
                break;
            case "SUPPLY":
                Stock?.HandleSupply(parts);
                break;
            case "REMOVE":
                Units.HandleRemove(parts);
                Buildings?.HandleRemove(parts);
                Fog?.Invalidate();
                Minimap?.Invalidate();
                break;
            case "HIDE":
                Units.HandleHide(parts);
                Fog?.Invalidate();
                Minimap?.Invalidate();
                break;
            case "ERR":
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
        if (!Map.IsSynchronized) return;
        _net.Send(command);
        GD.Print($"전송 요청: {command}");
    }

    private void OnPositionsRendered()
    {
        Fog?.Invalidate();
        Minimap?.Invalidate();
    }

    private void RejectMap()
    {
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
        if (GodotObject.IsInstanceValid(Commands)) Commands.BuildRequested -= BeginPlacement;
        if (_playerInput != null && GodotObject.IsInstanceValid(Units))
        {
            _playerInput.UnitClicked -= SelectUnit;
            _playerInput.BuildingClicked -= SelectBuilding;
            _playerInput.SelectionCleared -= ClearSelection;
            _playerInput.BoxSelectionRequested -= SelectBox;
            _playerInput.ContextClicked -= Units.RequestContextOrder;
            _playerInput.AttackTargetClicked -= Units.RequestAttack;
            _playerInput.AttackGroundClicked -= Units.RequestAttackMove;
            _playerInput.ModifiedUnitSelectionRequested -= ModifyUnitSelection;
            _playerInput.ModifiedBoxSelectionRequested -= ModifyBoxSelection;
            _playerInput.ControlGroupRequested -= HandleControlGroup;
            if (GodotObject.IsInstanceValid(Minimap))
            {
                Minimap.CameraMoveRequested -= MoveCameraFromMinimap;
                Minimap.MoveRequested -= _playerInput.QueueMinimapMove;
            }
        }

        if (GodotObject.IsInstanceValid(Units))
        {
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
