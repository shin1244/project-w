using Godot;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

// Drop and reconnect without a server: the session keeps the match and retries in place,
// a fresh snapshot ends the retry, teammates the AI plays are shown, and leaving a running
// match says LEAVE and keeps the way back. Server-side seat handling is checked in Go.
public partial class ReconnectChecks : Main
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private MatchSession _session;
    private void InvokeMain(string name, params object[] args) => typeof(Main).GetMethod(name, Private).Invoke(this, args);
    private void InvokeSession(string name, params object[] args) => typeof(MatchSession).GetMethod(name, Private).Invoke(_session, args);
    private T SessionField<T>(string name) => (T)typeof(MatchSession).GetField(name, Private).GetValue(_session);

    public override async void _Ready()
    {
        try
        {
            var net = GetNode<NetClient>("/root/Net");
            _session = GetNode<MatchSession>("/root/MatchSession");
            _session.RememberAddress = false;
            _session.ProcessMode = ProcessModeEnum.Disabled; // Time is driven by hand below.
            using var wire = new MemoryStream();
            var writer = new StreamWriter(wire, new UTF8Encoding(false), 1024, true) { AutoFlush = true };
            typeof(NetClient).GetField("_writer", Private).SetValue(net, writer);
            typeof(Main).GetField("_net", Private).SetValue(this, net);
            typeof(Main).GetField("_matchSession", Private).SetValue(this, _session);
            var match = new MatchAssignment { MatchId = "m", Host = "127.0.0.1", Port = 9, Ticket = new string('a', 32),
                Team = 1, Role = "HERO", Hero = UnitCatalog.HeroTest, Players = 8, Bots = 6 };
            typeof(MatchSession).GetProperty("Match").SetValue(_session, match);
            string queue = "http://127.0.0.1:9/queue/" + match.Ticket;
            typeof(MatchSession).GetField("_queueUrl", Private).SetValue(_session, queue);
            InvokeMain("ConnectInput");
            Join();
            _session.EnterGame();
            InvokeSession("OnMessage", "MATCH_START");
            Check(_session.Started && SessionField<string>("_rejoinUrl") == queue, "a started match must remember its way back");

            // AWAY: teammates the AI plays are named, listed on the team frame and cleared by a resync.
            InvokeMain("OnMessage", "AWAY 9 1");
            Check(AwayNotice?.Contains(UnitCatalog.Name(UnitCatalog.HeroGolem)) == true && Teammates.StatusOf(9) == "AI 대행",
                "a teammate the AI plays must be shown");
            InvokeMain("OnMessage", "AWAY 9 0");
            Check(AwayNotice == null && Teammates.StatusOf(9) == "", "a returned teammate must be cleared");
            InvokeMain("OnMessage", "AWAY 9 1");
            InvokeMain("OnMessage", "AWAY 9 2");
            InvokeMain("OnMessage", "AWAY x 0");
            Check(AwayNotice != null, "malformed AWAY must be ignored");

            // A lost connection keeps the match and retries in place instead of leaving.
            InvokeMain("OnConnectionClosed", "lost");
            InvokeSession("OnClosed", "lost");
            Check(_session.Reconnecting && _session.Match == match && _session.InGame && _session.ErrorMessage == null &&
                AwayNotice == null && _session.GetChildren().OfType<CanvasLayer>().Count(l => l.Layer == 110) == 1,
                "a dropped running match must reconnect in place");
            Join(); // The server's MAP and snapshot for the same ticket.
            Check(wire.ToArray().Length > 0 && Encoding.UTF8.GetString(wire.ToArray()).Contains($"MAP_READY {Map.MapHash} TICKET {match.Ticket}"),
                "reconnecting must present the same ticket");
            InvokeSession("OnMessage", "MATCH_START");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(!_session.Reconnecting && _session.GetChildren().OfType<CanvasLayer>().All(l => l.Layer != 110) &&
                Units.LiveUnits.Any(u => u.UnitId == 100), "a fresh snapshot must end reconnecting");

            // A link that stops delivering ticks is treated as dropped.
            for (int i = 0; i < 9; i++) _session._Process(0.5);
            Check(!_session.Reconnecting, "a short silence is not a drop");
            InvokeSession("OnMessage", "TICK 1");
            for (int i = 0; i < 9; i++) _session._Process(0.5);
            Check(!_session.Reconnecting, "any server line resets the silence");
            for (int i = 0; i < 3; i++) _session._Process(5);
            Check(_session.Reconnecting, "a silent link must start reconnecting even with long frames");
            InvokeSession("OnMessage", "MATCH_START");

            // Leaving a running match says LEAVE, keeps the lobby entry and offers the way back.
            typeof(NetClient).GetField("_writer", Private).SetValue(net, writer);
            long before = wire.Length;
            foreach (Node node in FindChildren("*", "CanvasLayer", true, false)) ((CanvasLayer)node).Hide();
            GetTree().CurrentScene = null; // Keep this driver alive across the scene change.
            _session.ReturnToLobby();
            for (int i = 0; i < 100 && wire.Length == before; i++) await Task.Delay(10);
            Check(Encoding.UTF8.GetString(wire.ToArray()[(int)before..]).Replace("\r", "") == "LEAVE\n",
                "leaving a running match must tell the server");
            Check(_session.Match == null && !_session.InGame && SessionField<string>("_rejoinUrl") == queue &&
                ReferenceEquals(SessionField<Task>("_cleanupTask"), Task.CompletedTask),
                "leaving a running match must keep its lobby entry");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var lobby = GetTree().CurrentScene as Lobby ?? throw new Exception("lobby did not open");
            typeof(MatchSession).GetProperty("Rejoinable").SetValue(_session, match);
            typeof(Lobby).GetMethod("Refresh", Private).Invoke(lobby, null);
            var start = lobby.GetNode<Button>("%PrimaryButton");
            var cancel = lobby.GetNode<Button>("%SecondaryButton");
            Check(start.Text == "진행 중인 경기로 돌아가기" && !start.Disabled && !cancel.Disabled, "the lobby must offer the running match");
            cancel.EmitSignal(Button.SignalName.Pressed);
            Check(_session.Rejoinable == null && SessionField<string>("_rejoinUrl") == null && start.Text != "진행 중인 경기로 돌아가기",
                "declining must forget the running match");
            await writer.DisposeAsync();
            GD.Print("PASS: in-place reconnect with the same ticket, silent-link detection, AI-played teammates, LEAVE and lobby rejoin offer");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    // Hero 7 on team 1 with golem teammate 9, as a server snapshot would arrive.
    private void Join()
    {
        foreach (string line in new[] { $"MAP 2 {Map.MapHash}", "WORLD_READY", "WELCOME 7 1 HERO 200",
            "UNIT 200 100 7 0 -8 1", "UNIT 201 300 9 2 -8 1" })
            InvokeMain("OnMessage", line);
    }

    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
