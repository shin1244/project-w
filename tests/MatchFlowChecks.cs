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

    public override void _Ready()
    {
        _session = GetNode<MatchSession>("/root/MatchSession");
        _session.RememberAddress = false;
        _url = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--lobby-url="))?[12..];
        _checkCancellation = OS.GetCmdlineUserArgs().Contains("--check-cancellation");
        _practice = OS.GetCmdlineUserArgs().Contains("--practice");
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
                _session.SelectedHero = _cycles == 0 ? UnitCatalog.HeroTest : UnitCatalog.HeroGolem;
                _session.PracticeRole = _practice ? (_cycles == 0 ? "HERO" : "COMMANDER") : "";
                // Check rapid cancel/requeue in the two-client run. With four clients,
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
            _played += delta;
            if (_played < 1.5) return;
            if (match.Players != 4 || (_practice && (match.Bots != 3 || match.Role != _session.PracticeRole)))
                throw new Exception("Invalid bot-filled match");
            if (match.Role == "HERO" && (match.Hero != _session.SelectedHero ||
                !game.Units.LiveUnits.Any(unit => unit.UnitType == match.Hero && game.Units.CanControl(unit)) ||
                game.UnitInfo.Experience is not { Level: 1, Current: 0 }))
                throw new Exception("Selected hero or initial experience missing");
            GD.Print($"PASS: cycle {_cycles+1}, {match.Players - match.Bots} players, team {match.Team}, {match.Role}, AI {match.Bots}, lobby/queue/loading/game");
            _firstMatch = match.MatchId;
            _cycles++;
            _played = 0;
            _sawLoading = false;
            _session.ReturnToLobby();
            if (_cycles == 2) { GetTree().Quit(); return; }
            _waitingToQueue = true;
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
