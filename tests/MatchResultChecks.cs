using Godot;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

public partial class MatchResultChecks : Main
{
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private void InvokeMain(string name, params object[] args) => typeof(Main).GetMethod(name, Private).Invoke(this, args);

    public override async void _Ready()
    {
        try
        {
            foreach (string invalid in new[] { "MATCH_END", "MATCH_END 3 BASE_DESTROYED 1", "MATCH_END 0 BASE_DESTROYED 1",
                "MATCH_END 1 TIME_LIMIT 1", "MATCH_END 0 TIME_LIMIT -1", "MATCH_END 1 UNKNOWN 3", "MATCH_END 1 BASE_DESTROYED 1 extra" })
                Check(!MatchResultSnapshot.TryParse(invalid.Split(' '), out _), "invalid result accepted");
            Check(new MatchResultSnapshot(1, "BASE_DESTROYED", 123).Title(1) == "승리" &&
                new MatchResultSnapshot(2, "BASE_DESTROYED", 123).Title(1) == "패배" &&
                new MatchResultSnapshot(0, "BOTH_BASES", 123).Title(1) == "무승부", "wrong perspective");
            var net = GetNode<NetClient>("/root/Net");
            var session = GetNode<MatchSession>("/root/MatchSession");
            session.RememberAddress = false;
            using var wire = new MemoryStream();
            using var writer = new StreamWriter(wire, new UTF8Encoding(false)) { AutoFlush = true };
            typeof(NetClient).GetField("_writer", Private).SetValue(net, writer);
            typeof(Main).GetField("_net", Private).SetValue(this, net);
            typeof(Main).GetField("_matchSession", Private).SetValue(this, session);
            InvokeMain("ConnectInput");
            foreach (string line in new[] { $"MAP 2 {Map.MapHash}", "WORLD_READY", "WELCOME 7 1 COMMANDER", "UNIT 1 101 7 0 -8 1" })
                InvokeMain("OnMessage", line);
            InvokeMain("SendCommand", "MOVE 1 1 101");
            long before = wire.Length;
            InvokeMain("OnMessage", "MATCH_END 1 BASE_DESTROYED 558");
            InvokeMain("SendCommand", "MOVE 5 5 101");
            InvokeMain("OnMessage", "MATCH_END 2 BASE_DESTROYED 559");
            InvokeMain("OnConnectionClosed", "server finished");
            Check(wire.Length == before && Result?.Winner == 1 && session.Result == Result &&
                ProcessMode == ProcessModeEnum.Disabled && Units.LiveUnits.Any(u => u.UnitId == 101),
                "result must freeze commands and survive disconnect without clearing the battlefield");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (OS.GetCmdlineUserArgs().Contains("--capture"))
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using Image image = GetViewport().GetTexture().GetImage();
                image.SavePng("res://.godot/match-result.png");
            }
            // Retain the driver, hiding its HUD as a normal scene exit would.
            foreach (Node node in FindChildren("*", "CanvasLayer", true, false)) ((CanvasLayer)node).Hide();
            GetTree().CurrentScene = null;
            var panel = session.GetChildren().OfType<MatchResultPanel>().Single();
            panel.GetChild<Control>(0).GetNode<Button>("%PrimaryButton").EmitSignal(Button.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(session.Result == null && session.Match == null && !session.InGame && GetTree().CurrentScene is Lobby,
                "result return did not reset session");
            if (OS.GetCmdlineUserArgs().Contains("--capture"))
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using Image image = GetViewport().GetTexture().GetImage();
                image.SavePng("res://.godot/bot-lobby.png");
            }
            GD.Print("PASS: result parsing, winner perspective, frozen commands, disconnect and return to lobby");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
