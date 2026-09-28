using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

// 명시적으로 실행하는 실제 서버 검사. 127.0.0.1:7777 서버와 동일한 maps/test.json이 필요합니다.
public partial class ServerConnectionChecks : Node
{
    private uint _player;
    private uint _team;
    private string _connectionError;
    private NetClient _net;
    private readonly Dictionary<uint, Vector3> _firstPositions = new();
    private readonly HashSet<uint> _movedTeams = new();
    private uint _probeUnit;
    private uint _probeTower;
    private bool _sawTowerShot;
    private bool _sawProbeDamage;
    private bool _sawProbeRemoval;

    public override async void _Ready()
    {
        try
        {
            _net = GetNode<NetClient>("/root/Net");
            _net.MessageReceived += Observe;
            _net.ConnectionClosed += OnClosed;
            Main game = GD.Load<PackedScene>("res://game/Main.tscn").Instantiate<Main>();
            AddChild(game);
            using var mapData = JsonDocument.Parse(FileAccess.GetFileAsString("res://maps/test.json"));
            var towerSpots = mapData.RootElement.GetProperty("towers").EnumerateArray().Select(spot => (
                Side: spot.GetProperty("side").GetUInt32(),
                Pos: new Vector3(spot.GetProperty("pos")[0].GetSingle(), 0, spot.GetProperty("pos")[1].GetSingle()))).ToArray();
            ulong deadline = Time.GetTicksMsec() + 45000;
            while (Time.GetTicksMsec() < deadline)
            {
                await ToSignal(GetTree().CreateTimer(0.1), SceneTreeTimer.SignalName.Timeout);
                if (_connectionError != null || game.Map.SyncError != null)
                    throw new InvalidOperationException(_connectionError ?? game.Map.SyncError);
                var units = game.Units.GetChildren().OfType<Unit>().Where(u => !u.IsDying).ToArray();
                foreach (Unit minion in units.Where(u => u.OwnerId == 0))
                {
                    if (_firstPositions.TryGetValue(minion.UnitId, out Vector3 start))
                    {
                        if (minion.GlobalPosition.DistanceSquaredTo(start) > .25f) _movedTeams.Add(minion.Team);
                    }
                    else _firstPositions.Add(minion.UnitId, minion.GlobalPosition);
                }
                Unit own = units.FirstOrDefault(u => _player != 0 && u.OwnerId == _player);
                var buildings = game.Buildings.GetChildren().OfType<Building>().ToArray();
                bool buildingsReady = buildings.Count(b => b.BuildingType == 0 && b.HealthBar.MaxHP == 1000) == 2 &&
                    buildings.Count(b => b.BuildingType == 1 && b.HasServerState && b.HealthBar.MaxHP == 1500) == towerSpots.Length &&
                    towerSpots.All(spot => buildings.Any(b => b.BuildingType == 1 && b.SideId == spot.Side && b.GlobalPosition.IsEqualApprox(spot.Pos)));
                if (buildingsReady && own != null && _probeUnit == 0)
                {
                    Building enemyTower = buildings.Where(b => b.BuildingType == 1 && b.SideId != _team)
                        .OrderBy(b => b.GlobalPosition.DistanceSquaredTo(own.GlobalPosition)).First();
                    _probeUnit = own.UnitId;
                    _probeTower = enemyTower.BuildingId;
                    enemyTower.StateChanged += (state, fired) =>
                    {
                        if (fired && state.FocusId == _probeUnit && enemyTower.HasNode("ShotTrace")) _sawTowerShot = true;
                    };
                    // 검증용 일꾼 한 기를 보내 실제 ATTACK 송신과 포탑 발사/HP/REMOVE를 확인합니다.
                    game.Units.SelectSingle(own);
                    game.Units.RequestAttack(enemyTower);
                }
                var minions = units.Where(u => u.OwnerId == 0 && u.HasServerState && u.HealthBar.MaxHP > 0).ToArray();
                // 전장의 안개 이후에는 상대 웨이브가 출생 직후 보이지 않는 것이 정상입니다.
                bool fullWave = new uint[] { 1, 2 }.All(type => minions.Any(u => u.Team == _team && u.UnitType == type));
                if (!game.Map.IsSynchronized || own == null || !fullWave || !_movedTeams.Contains(_team) ||
                    !buildingsReady || !_sawTowerShot || !_sawProbeDamage || !_sawProbeRemoval) continue;
                if (own.Team != _team) throw new InvalidOperationException("WELCOME and own UNIT team mismatch");
                game.Units.SelectSingle(own);
                if (!game.Units.SelectedUnitIds.Contains(own.UnitId)) throw new InvalidOperationException("Own worker cannot be selected");
                foreach (Unit minion in minions)
                {
                    game.Units.SelectSingle(minion);
                    if (game.Units.SelectedUnitIds.Count != 0) throw new InvalidOperationException("Minion became controllable");
                }
                GD.Print($"PASS: live server player={_player} team={_team}, map={game.Map.MapHash}, 2 halls, {towerSpots.Length} towers, tower {_probeTower} firing/HP/REMOVE, allied knight/archer minions, movement, state, HP and selection");
                GetTree().Quit();
                return;
            }
            throw new TimeoutException($"45초 내 검증 미완료: player={_player}, probe={_probeUnit}, tower={_probeTower}, shot={_sawTowerShot}, damage={_sawProbeDamage}, removal={_sawProbeRemoval}, minionTeams={_movedTeams.Count}");
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private void Observe(string message)
    {
        string[] parts = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 3 && parts[0] == "WELCOME")
        {
            _player = uint.Parse(parts[1]);
            _team = uint.Parse(parts[2]);
        }
        if (parts.Length == 4 && parts[0] == "HP" && uint.TryParse(parts[1], out uint healthId) && healthId == _probeUnit &&
            HealthSnapshot.TryParse(parts, out var health) && health.Current < health.Maximum) _sawProbeDamage = true;
        if (parts.Length == 2 && parts[0] == "REMOVE" && uint.TryParse(parts[1], out uint removedId) && removedId == _probeUnit)
            _sawProbeRemoval = true;
    }

    private void OnClosed(string reason) => _connectionError = reason;
    public override void _ExitTree()
    {
        if (!GodotObject.IsInstanceValid(_net)) return;
        _net.MessageReceived -= Observe;
        _net.ConnectionClosed -= OnClosed;
    }
}
