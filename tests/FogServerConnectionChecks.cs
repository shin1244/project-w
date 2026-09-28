using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// 별도 검증 서버에 두 인스턴스를 함께 실행합니다. 각 진영의 일꾼 한 기가 정찰 후 귀환/재정찰합니다.
public partial class FogServerConnectionChecks : Node
{
    private uint _player;
    private uint _team;
    private uint _enemy;
    private string _failure;
    private NetClient _net;
    private readonly HashSet<uint> _hidden = new();
    private int _sightDefinitions;

    public override async void _Ready()
    {
        try
        {
            _net = GetNode<NetClient>("/root/Net");
            _net.MessageReceived += Observe;
            _net.ConnectionClosed += OnClosed;
            Main game = GD.Load<PackedScene>("res://game/Main.tscn").Instantiate<Main>();
            AddChild(game);
            Unit scout = null;
            Unit firstEnemy = null;
            int stage = 0;
            ulong stageSince = 0;
            ulong deadline = Time.GetTicksMsec() + 80000;
            while (Time.GetTicksMsec() < deadline)
            {
                await ToSignal(GetTree().CreateTimer(.1), SceneTreeTimer.SignalName.Timeout);
                if (_failure != null || game.Map.SyncError != null) throw new InvalidOperationException(_failure ?? game.Map.SyncError);
                if (!game.Map.IsSynchronized || _player == 0) continue;
                var units = game.Units.LiveUnits;
                if (stage == 0)
                {
                    scout = units.FirstOrDefault(u => u.OwnerId == _player);
                    if (scout == null || _sightDefinitions < 5) continue;
                    Check(units.All(u => u.Team == _team), "Hidden enemy starting workers leaked");
                    game.Fog.RefreshVision();
                    Check(game.Fog.Team == _team && game.Fog.IsVisibleAt(scout.GlobalPosition) && !game.Fog.IsVisibleAt(Vector3.Zero), "Initial server-defined vision mask");
                    if (OS.GetCmdlineUserArgs().Contains("--capture"))
                    {
                        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                        using Image screenshot = GetViewport().GetTexture().GetImage();
                        screenshot.SavePng($"res://docs/images/fog-team-{_team}.png");
                    }
                    game.Units.SelectSingle(scout);
                    game.Units.RequestMove(Vector3.Zero);
                    stage = 1;
                    GD.Print($"Fog team {_team}: initial privacy passed; scouting centre");
                }
                else if (stage == 1)
                {
                    firstEnemy = units.FirstOrDefault(u => u.UnitType == 0 && u.Team != _team && u.HasServerState && u.HealthBar.MaxHP > 0);
                    if (firstEnemy == null) continue;
                    _enemy = firstEnemy.UnitId;
                    Check(firstEnemy.HealthBar.CurrentHP == 50, "Revealed worker full snapshot HP");
                    // 양쪽 클라이언트가 등장 상태를 읽을 시간을 둡니다.
                    stageSince = Time.GetTicksMsec();
                    stage = 2;
                    GD.Print($"Fog team {_team}: enemy {_enemy} revealed");
                }
                else if (stage == 2 && Time.GetTicksMsec() - stageSince >= 700)
                {
                    game.Units.SelectSingle(scout);
                    game.Units.RequestMove(new Vector3(_team == 1 ? -30 : 30, 0, 0));
                    stage = 3;
                }
                else if (stage == 3 && _hidden.Contains(_enemy))
                {
                    Check(game.Units.ResolveFocus(_enemy) == null && !game.Units.HasNode($"Dying_{_enemy}"), "HIDE must not leave target or death effect");
                    if (GodotObject.IsInstanceValid(firstEnemy)) Check(!firstEnemy.IsInsideTree(), "Hidden scene instance detached");
                    // 단순 경계 왕복이 아닌 시야 밖 귀환을 확인한 뒤 재정찰합니다.
                    stageSince = Time.GetTicksMsec();
                    stage = 4;
                    GD.Print($"Fog team {_team}: HIDE {_enemy} applied without death");
                }
                else if (stage == 4 && Time.GetTicksMsec() - stageSince >= 2000)
                {
                    game.Units.SelectSingle(scout);
                    game.Units.RequestMove(Vector3.Zero);
                    stage = 5;
                }
                else if (stage == 5 && game.Units.ResolveFocus(_enemy) is Unit returned && returned.HasServerState && returned.HealthBar.MaxHP > 0)
                {
                    Check(returned != firstEnemy && !returned.IsDying && returned.Team != _team, "Reappearance rebuilt a living enemy from snapshot");
                    Check(game.Buildings.LiveBuildings.Count == 10, "Fog preserves globally known buildings");
                    // 칸 중심 판정과 프레임 시점 때문에 상대편의 재등장이 조금 늦을 수 있습니다.
                    // 먼저 접속을 끊어 정찰병이 사라지면 상대편 검증까지 중단되므로 잠시 유지합니다.
                    await ToSignal(GetTree().CreateTimer(5), SceneTreeTimer.SignalName.Timeout);
                    GD.Print($"PASS: live fog player={_player} team={_team}, initial enemy filtering, shared sight settings, reveal -> HIDE -> reveal, fresh state/HP, known buildings");
                    GetTree().Quit();
                    return;
                }
                if (stage > 0 && (!GodotObject.IsInstanceValid(scout) || scout.IsDying)) throw new InvalidOperationException("Scout died during fog integration check");
            }
            throw new TimeoutException("80초 안에 양쪽 정찰·시야 이탈·재등장을 확인하지 못했습니다. 새 검증 서버에서 두 인스턴스를 함께 실행하세요.");
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private void Observe(string message)
    {
        string[] parts = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 3 && parts[0] == "WELCOME") { _player = uint.Parse(parts[1]); _team = uint.Parse(parts[2]); }
        if (parts.Length == 4 && parts[0] == "SIGHT") _sightDefinitions++;
        if (parts.Length == 2 && parts[0] == "HIDE" && uint.TryParse(parts[1], out uint id)) _hidden.Add(id);
    }
    private void OnClosed(string reason) => _failure = reason;
    public override void _ExitTree()
    {
        if (!GodotObject.IsInstanceValid(_net)) return;
        _net.MessageReceived -= Observe;
        _net.ConnectionClosed -= OnClosed;
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
