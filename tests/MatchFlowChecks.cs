using Godot;
using System;
using System.Linq;

// Opt-in integration check. tests/run-match.ps1 supplies a real local lobby and starts one
// client per seat: client 1 makes the room and starts it, the others join from the list.
public partial class MatchFlowChecks : Node
{
    private const string RoomName = "검증 방";
    private MatchSession _session;
    private string _url, _firstMatch;
    private int _cycles, _client = 1, _players = 1;
    private double _elapsed, _played, _retry;
    private bool _entered, _created, _picked, _started;
    private bool _sawLoading;
    private bool _checkCancellation, _cancelled;
    private bool _practice;
    private bool _spectate;
    private bool _requestedEquipment;
    private bool _rosterVerified;
    private bool _capturing, _captured;
    private uint _team, _hero;
    private string _role;

    public override void _Ready()
    {
        _session = GetNode<MatchSession>("/root/MatchSession");
        _session.RememberAddress = false;
        var args = OS.GetCmdlineUserArgs();
        _url = args.FirstOrDefault(a => a.StartsWith("--lobby-url="))?[12..];
        _client = int.Parse(args.FirstOrDefault(a => a.StartsWith("--client="))?[9..] ?? "1");
        _players = int.Parse(args.FirstOrDefault(a => a.StartsWith("--players="))?[10..] ?? "1");
        _checkCancellation = args.Contains("--check-cancellation");
        _practice = args.Contains("--practice");
        _spectate = args.Contains("--spectate");
        if (_url == null) { GD.PushError("Use tests/run-match.ps1"); GetTree().Quit(1); return; }
        Callable.From(() => {
            var lobby = GD.Load<PackedScene>("res://lobby/Lobby.tscn").Instantiate();
            GetTree().Root.AddChild(lobby);
            GetTree().CurrentScene = lobby; // Keep this test driver alive across scene changes.
        }).CallDeferred();
    }

    // Seats in run-match.ps1's order: each team's commander, then its heroes. Alone, the
    // client plays a hero first and the commander in the second match.
    private void ChooseSeat()
    {
        int seat = _practice ? (_cycles == 0 ? 1 : 0) : _client - 1;
        _team = (uint)(seat / 4 + 1);
        _role = seat % 4 == 0 ? "COMMANDER" : "HERO";
        _hero = seat % 4 == 0 ? 0 : new[] { UnitCatalog.HeroMarshal, UnitCatalog.HeroGolem, UnitCatalog.HeroTest }[seat % 4 - 1];
    }

    // Lobby and room steps through the session, as the screens' buttons do.
    private void DriveRoom(double delta)
    {
        if (_session.Match != null || _spectate) return; // --spectate: the screens drive themselves.
        if (_session.Lobby == null)
        {
            if (!_entered && GetTree().CurrentScene is Lobby) { _entered = true; _session.EnterLobby(_url, $"클라{_client}"); }
            return;
        }
        var room = _session.Room;
        if (room == null)
        {
            if (_client == 1)
            {
                if (!_created) { _created = true; _session.CreateRoom(RoomName); }
                return;
            }
            if ((_retry -= delta) > 0) return;
            _retry = 0.5;
            var listed = _session.Rooms.FirstOrDefault(r => r.Name == RoomName && !r.Playing);
            if (listed != null) _session.JoinRoom(listed.Id);
            else _session.RefreshRooms();
            return;
        }
        if (room.State != "waiting" && room.State != "countdown") return; // The last match is still closing.
        var me = room.Me;
        if (me == null) return;
        if (!_picked)
        {
            _picked = true;
            ChooseSeat();
            _session.ChooseRole(_team, _role, _hero);
            return;
        }
        if (!room.IsHost || _started) return;
        bool ready = room.Members.Count == _players && room.Members.All(m => m.Seat >= 0);
        if (!ready) return;
        if (_checkCancellation && !_cancelled)
        {
            // Start, then call it off: the countdown must end without a match.
            if (room.State == "waiting") { _session.StartRoom(); return; }
            _session.CancelStart();
            _cancelled = true;
            return;
        }
        if (room.State == "waiting" && (_retry -= delta) <= 0) { _retry = 1; _session.StartRoom(); _started = true; }
    }

    public override void _Process(double delta)
    {
        try
        {
            _elapsed += delta;
            if (_elapsed > 70) throw new Exception("Match flow timeout");
            if (_session.ErrorMessage != null) throw new Exception(_session.ErrorMessage);
            DriveRoom(delta);
            if (GetTree().CurrentScene is Loading) _sawLoading = true;
            if (!_session.InGame || GetTree().CurrentScene is not Main game) return;
            var match = _session.Match;
            if (!_sawLoading || !game.Map.IsSynchronized || !_session.Started ||
                game.Units.LocalTeam != match.Team ||
                (game.LocalRole == PlayerRole.Hero) != (match.Role == "HERO")) throw new Exception("Invalid game transition");
            if (_cycles == 1 && _firstMatch == match.MatchId) throw new Exception("Reused previous world");
            // MATCH_START follows every admitted client's completed snapshot. Keep
            // this initial roster proof after faster test clients return to the lobby.
            if (!_rosterVerified)
                _rosterVerified = (_spectate ? new uint[] { 1, 2 } : new[] { match.Team }).All(team =>
                {
                    var heroes = game.Units.LiveUnits.Where(unit => unit.Team == team && UnitCatalog.IsHero(unit.UnitType)).ToArray();
                    // Three different heroes; spectator matches draw them at random.
                    return heroes.Length == 3 && heroes.Select(unit => unit.UnitType).Distinct().Count() == 3;
                });
            _played += delta;
            if (match.Role=="HERO" && !_requestedEquipment && game.Shop?.Inventory!=null && game.Skills.Definition(1).HasValue)
            {
                var tiers = game.Shop.Catalog.Values.GroupBy(item => item.Tier).OrderBy(group => group.Key).ToArray();
                if (tiers.Length == 0 || tiers.Any(group => group.Count() != 6) || !tiers.Select(group => group.Key).SequenceEqual(Enumerable.Range(1, tiers.Length)))
                    throw new Exception("Server item catalog must contain six items for each tier up to its last HQ tier");
                if (game.Skills.Progress is not HeroSkillProgressSnapshot progression || progression.Level != 1 ||
                    progression.Points != 0 || progression.UltimateLearned || game.Skills.CanUse(3))
                    throw new Exception("Initial skill progression or ultimate lock missing from live server");
                game.Shop.RequestBuy(1);
                game.Units.RequestHeroSkill(game.Skills.Definition(1).Value,game.Skills.HeroUnitId.Value,new SkillInput(1));
                _requestedEquipment=true;
            }
            if (_played < 1.5) return;
            if (_spectate)
            {
                if (match.Role != "SPECTATOR" || match.Bots != 8 || game.LocalRole != PlayerRole.Spectator ||
                    game.Units.LocalTeam != 0 || game.Fog.Visible || !game.Spectator.Visible ||
                    game.Units.LiveUnits.Any(game.Units.CanControl) ||
                    game.Units.LiveUnits.Count(u => UnitCatalog.IsHero(u.UnitType)) != 6 ||
                    !game.Units.LiveUnits.Any(u => u.Team == 1) || !game.Units.LiveUnits.Any(u => u.Team == 2) ||
                    game.Buildings.LiveBuildings.Any(b => !b.Visible)) throw new Exception("Incomplete AI spectator world");
                if (_cycles == 0 && OS.GetCmdlineUserArgs().Contains("--capture-spectator"))
                {
                    if (!_capturing) { _capturing = true; CaptureSpectator(game); }
                    if (!_captured) return;
                }
            }
            if (match.Players != 8 || match.Bots != 8 - (_spectate ? 0 : _players) || (!_spectate && (match.Team != _team || match.Role != _role)))
                throw new Exception("Invalid bot-filled match");
            if (!_rosterVerified) throw new Exception("Initial team roster did not contain exactly one wolf, golem and marshal");
            if (match.Role == "HERO" && (match.Hero != _hero ||
                !game.Units.LiveUnits.Any(unit => unit.UnitType == match.Hero && game.Units.CanControl(unit)) ||
                game.UnitInfo.Experience is not { Level: 1, Current: 0 }))
                throw new Exception("Selected hero or initial experience missing");
            if (match.Role=="HERO" && (!_requestedEquipment || !game.Shop.Inventory.Items.Contains(1u) || game.Shop.Inventory.Pending.Any(p=>p)))
                throw new Exception("Real server did not equip purchased item");
            GD.Print($"PASS: cycle {_cycles+1}, {match.Players - match.Bots} players, team {match.Team}, {match.Role}, AI {match.Bots}, lobby/room/loading/game");
            _firstMatch = match.MatchId;
            _cycles++;
            _played = 0;
            _requestedEquipment=false;
            _rosterVerified=false;
            _sawLoading = false;
            _session.ReturnToLobby();
            if (_cycles == 2) { GetTree().Quit(); return; }
            _picked = !_practice; // Alone, the second match switches to the commander.
            _started = false;
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private async void CaptureSpectator(Main game)
    {
        try
        {
            game.Spectator.FocusTeam(1);
            game.Units.Camera.Size = 48;
            var hero = game.Units.LiveUnits.First(u => u.Team == 1 && UnitCatalog.IsHero(u.UnitType));
            game.Units.SelectSingle(hero);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var picture = GetViewport().GetTexture().GetImage();
            if (picture.SavePng("res://docs/images/spectator-trained-ai.png") != Error.Ok) throw new Exception("Preview save failed");
            _captured = true;
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
