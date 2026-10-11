using Godot;
using System;
using System.Linq;

// Opt-in integration check. tests/run-match.ps1 supplies a real local lobby.
public partial class MatchFlowChecks : Node
{
    private MatchSession _session;
    private string _url, _firstMatch;
    private int _cycles;
    private double _elapsed, _played;
    private bool _waitingToQueue = true;
    private bool _sawLoading;
    private bool _checkCancellation;
    private bool _practice;
    private bool _spectate;
    private bool _requestedEquipment;
    private bool _rosterVerified;
    private bool _capturing, _captured;

    public override void _Ready()
    {
        _session = GetNode<MatchSession>("/root/MatchSession");
        _session.RememberAddress = false;
        _url = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--lobby-url="))?[12..];
        _checkCancellation = OS.GetCmdlineUserArgs().Contains("--check-cancellation");
        _practice = OS.GetCmdlineUserArgs().Contains("--practice");
        _spectate = OS.GetCmdlineUserArgs().Contains("--spectate");
        if (_url == null) { GD.PushError("Use tests/run-match.ps1"); GetTree().Quit(1); return; }
        Callable.From(() => {
            var lobby = GD.Load<PackedScene>("res://lobby/Lobby.tscn").Instantiate();
            GetTree().Root.AddChild(lobby);
            GetTree().CurrentScene = lobby; // Keep this test driver alive across scene changes.
        }).CallDeferred();
    }

    public override void _Process(double delta)
    {
        try
        {
            _elapsed += delta;
            if (_elapsed > 65) throw new Exception("Match flow timeout");
            if (_session.ErrorMessage != null) throw new Exception(_session.ErrorMessage);
            if (_waitingToQueue && GetTree().CurrentScene is Lobby)
            {
                _waitingToQueue = false;
                _session.SelectedHero = _cycles == 0 ? UnitCatalog.HeroMarshal : UnitCatalog.HeroGolem;
                _session.PracticeRole = _spectate ? "SPECTATOR" : _practice ? (_cycles == 0 ? "HERO" : "COMMANDER") : "";
                // Check rapid cancel/requeue in the two-client run. With eight clients,
                // cancelling an already-full match intentionally cancels that match for everyone.
                if (_checkCancellation)
                {
                    _session.StartQueue(_url);
                    _session.CancelQueue();
                }
                _session.StartQueue(_url);
            }
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
                    return heroes.Length == 3 && heroes.Select(unit => unit.UnitType).OrderBy(type => type)
                        .SequenceEqual(new[] { UnitCatalog.HeroTest, UnitCatalog.HeroGolem, UnitCatalog.HeroMarshal });
                });
            _played += delta;
            if (match.Role=="HERO" && !_requestedEquipment && game.Shop?.Inventory!=null && game.Skills.Definition(1).HasValue)
            {
                if (game.Shop.Catalog.Count!=18 || Enumerable.Range(1,3).Any(tier => game.Shop.Catalog.Values.Count(item => item.Tier==tier)!=6))
                    throw new Exception("Server item catalog must contain six items for each of three tiers");
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
            if (match.Players != 8 || (_practice && (match.Bots != 7 || match.Role != _session.PracticeRole)))
                throw new Exception("Invalid bot-filled match");
            if (!_rosterVerified) throw new Exception("Initial team roster did not contain exactly one wolf, golem and marshal");
            if (match.Role == "HERO" && ((_practice && match.Hero != _session.SelectedHero) ||
                !game.Units.LiveUnits.Any(unit => unit.UnitType == match.Hero && game.Units.CanControl(unit)) ||
                game.UnitInfo.Experience is not { Level: 1, Current: 0 }))
                throw new Exception("Selected hero or initial experience missing");
            if (match.Role=="HERO" && (!_requestedEquipment || !game.Shop.Inventory.Items.Contains(1u) || game.Shop.Inventory.Pending.Any(p=>p)))
                throw new Exception("Real server did not equip purchased item");
            GD.Print($"PASS: cycle {_cycles+1}, {match.Players - match.Bots} players, team {match.Team}, {match.Role}, AI {match.Bots}, lobby/queue/loading/game");
            _firstMatch = match.MatchId;
            _cycles++;
            _played = 0;
            _requestedEquipment=false;
            _rosterVerified=false;
            _sawLoading = false;
            _session.ReturnToLobby();
            if (_cycles == 2) { GetTree().Quit(); return; }
            _waitingToQueue = true;
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
