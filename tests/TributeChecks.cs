using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

// 서버 연결 대신 메시지를 주입하며 Main의 상태와 실제 Viewport 우클릭 경로를 확인합니다.
public partial class TributeChecks : Main
{
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<string> _commands = new();
    private TributeEventView _tributeView;
    private TributeEventPanel _tributePanel;

    public override async void _Ready()
    {
        try
        {
            CheckSnapshots();
            var net = GetNode<NetClient>("/root/Net");
            using var wire = new MemoryStream();
            using var writer = new StreamWriter(wire, new UTF8Encoding(false)) { AutoFlush = true };
            typeof(NetClient).GetField("_writer", Private).SetValue(net, writer);
            typeof(Main).GetField("_net", Private).SetValue(this, net);
            InvokeMain("ConnectInput");
            Units.CommandRequested += command => { _commands.Add(command); InvokeMain("SendCommand", command); };
            _tributeView = GetNode<TributeEventView>("TributeEvent");
            _tributePanel = GetNode<TributeEventPanel>("SelectionUI/TributeEventPanel");
            Fog.Configure(Map);
            CheckReset("initial world");
            Receive("TRIBUTE 1 ACTIVE 0 0 0 0 0 0 0 0 0");
            Check(Tribute == null, "Unsynchronized world cannot accept tribute snapshots");

            BeginHeroSession();
            Receive("TRIBUTE 0 WAITING 0 0 3600 0 0 0 0 0 0");
            Check(Tribute is { Active: false, NextSpawnTick: 3600 } && !_tributeView.IsVisibleInTree() &&
                _tributePanel.IsVisibleInTree(), "First tribute waits two minutes with a HUD countdown and no world target");
            Receive("TICK 3000");
            Check(Tribute.RemainingSeconds(3000) == 20 && _tributePanel.StatusText.Contains("00:20"),
                "Waiting countdown updates the HUD from server ticks");
            Receive("TICK 3600");
            Check(!Tribute.Active && !_tributeView.IsVisibleInTree(), "Countdown expiry cannot spawn a tribute without a snapshot");
            Receive("TRIBUTE 1 ACTIVE 0 0 0 0 0 0 0 0 0");
            await Flush();
            Check(Tribute is { Active: true, EventId: 1, Channeling: false } &&
                _tributeView.IsVisibleInTree() && _tributeView.EventId == 1,
                "Active snapshot exposes the authoritative event target");
            TributeSnapshot beforeClick = Tribute;
            int before = _commands.Count;
            long beforeWire = wire.Length;
            RightClickTribute();
            await Flush();
            Check(_commands.Skip(before).SequenceEqual(new[] { "TRIBUTE 701 1" }) &&
                WireAfter(wire, beforeWire) == "TRIBUTE 701 1" + System.Environment.NewLine,
                "One actual hero right-click emits exactly one tribute command with hero and event IDs");
            Check(Tribute == beforeClick && !Tribute.Channeling,
                "A click does not start collection or award a score before the server snapshot");

            Receive("TRIBUTE 1 ACTIVE 0 0 0 701 1 3600 3810 0 0");
            Receive("TICK 3705");
            Check(Tribute is { CapturerId: 701, CapturerTeam: 1, Channeling: true } &&
                Mathf.IsEqualApprox(Tribute.Progress(3705), .5f) &&
                Mathf.IsEqualApprox(_tributeView.ChannelProgress, .5f) && Mathf.IsEqualApprox(_tributePanel.ChannelProgress, .5f),
                "Channel state and seven-second world/HUD progress come from the server");
            TributeSnapshot channeling = Tribute;
            foreach (string malformed in InvalidSnapshots()) Receive(malformed);
            Check(Tribute == channeling, "Malformed snapshots cannot replace valid channel state");
            Receive("TICK 3810");
            Check(Tribute is { Active: true, Team1Count: 0, Team2Count: 0 } && _tributeView.IsVisibleInTree(),
                "A completed local progress bar cannot collect or hide the event optimistically");
            Receive("TRIBUTE 1 WAITING 0 0 0 0 0 0 0 1 0");
            Check(Tribute is { RamsMarching: true, Channeling: false, NextSpawnTick: 0, Team1Count: 1, Team2Count: 0 } &&
                !_tributeView.IsVisibleInTree() &&
                _tributePanel.CollectionText.Contains("1팀"),
                "Collection hides the event and waits for the rams without starting the respawn timer");
            Receive("TICK 9210");
            Check(Tribute.RamsMarching && !_tributeView.IsVisibleInTree() &&
                _tributePanel.StatusText == "공성추 진군 중" && _tributePanel.CollectionText == "",
                "Three minutes after capture still shows marching, without a zero countdown or inferred respawn");
            Receive("TRIBUTE 1 WAITING 0 0 14610 0 0 0 0 1 0");
            Check(!Tribute.RamsMarching && Tribute.RemainingSeconds(9210) == 180 &&
                _tributePanel.StatusText.Contains("03:00") && _tributePanel.CollectionText == "",
                "Only the server's post-ram schedule starts a three-minute countdown without a duplicate capture notice");
            Receive("TICK 14610");
            Check(!Tribute.Active, "Respawn countdown also waits for server confirmation");
            Receive("TRIBUTE 2 ACTIVE 0 0 0 0 0 0 0 1 0");
            Receive("TRIBUTE 2 ACTIVE 0 0 0 701 2 14610 14820 1 0");
            Receive("TRIBUTE 2 ACTIVE 0 0 0 0 0 0 0 1 0");
            Check(Tribute is { EventId: 2, Active: true, Channeling: false, Team1Count: 1, Team2Count: 0 },
                "An interrupted channel returns to an available event without awarding collection");

            await CheckRestrictedInput();

            // Re-synchronization must discard a click already queued for the next physics frame.
            Receive("UNIT 200 701 7 -4 0 1");
            await Flush();
            before = _commands.Count;
            RightClickTribute();
            Receive($"MAP 2 {Map.MapHash}");
            await Flush();
            CheckReset("map resynchronization");
            Check(_commands.Count == before, "Map resynchronization clears queued tribute clicks");
            Receive("TRIBUTE 9 ACTIVE 0 0 0 0 0 0 0 9 9");
            Check(Tribute == null, "Snapshots during resynchronization cannot resurrect an old tribute");
            Receive("WORLD_READY");
            Receive("WELCOME 7 1 HERO 200");
            Receive("UNIT 200 701 7 -4 0 1");
            Receive("TRIBUTE 3 ACTIVE 0 0 0 0 0 0 0 1 0");
            InvokeMain("OnConnectionClosed", "tribute test disconnect");
            CheckReset("disconnect");
            beforeWire = wire.Length;
            Units.RequestContextOrder(_tributeView, Vector3.Zero);
            RightClickTribute();
            await Flush();
            Check(wire.Length == beforeWire, "Disconnected world cannot send tribute or fallback movement commands");

            typeof(NetClient).GetField("_writer", Private).SetValue(net, writer);
            BeginHeroSession();
            Receive("TRIBUTE 4 WAITING 0 0 0 0 0 0 0 1 1");
            Check(Tribute.RamsMarching && _tributePanel.CollectionText == "" &&
                _tributePanel.StatusText == "공성추 진군 중" && !_tributeView.IsVisibleInTree(),
                "Reconnecting during a ram march restores the waiting state without a collection announcement");
            Receive("TRIBUTE 4 ACTIVE 0 0 0 701 1 3600 3810 1 1");
            Receive("TICK 3705");
            await Flush();
            if (OS.GetCmdlineUserArgs().Contains("--capture")) await CapturePreview();
            Receive("MATCH_END 1 BASE_DESTROYED 124");
            TributeSnapshot afterResult = Tribute;
            beforeWire = wire.Length;
            Receive("TRIBUTE 4 WAITING 0 0 9210 0 0 0 0 2 1");
            InvokeMain("SendCommand", "TRIBUTE 701 4");
            RightClickTribute();
            await Flush();
            Check(Result?.Winner == 1 && ProcessMode == ProcessModeEnum.Disabled && Tribute == afterResult && wire.Length == beforeWire,
                "Match results freeze tribute state and command output");
            GD.Print("PASS: tribute parsing, ram march/post-ram countdown, server channel/collection, hero viewport input, role restrictions, resync/disconnect and match completion");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private async Task CheckRestrictedInput()
    {
        Receive("WELCOME 7 1 COMMANDER");
        Receive("UNIT 0 702 7 -7 -4 1");
        Receive("UNIT 1 703 7 -7 4 1");
        Receive("UNIT 100 704 0 -9 0 1");
        await Flush();
        foreach (uint id in new uint[] { 701, 702, 703, 704 })
        {
            Units.SelectSingle(Units.LiveUnits.Single(unit => unit.UnitId == id));
            int before = _commands.Count;
            RightClickTribute();
            await Flush();
            Check(_commands.Count == before, $"Commander selection {id} cannot collect tribute or fall back to MOVE");
        }

        Receive("WELCOME 7 1 HERO 200");
        _tributeView.Hide();
        int beforeHidden = _commands.Count;
        Units.RequestContextOrder(_tributeView, Vector3.Zero);
        Check(_commands.Count == beforeHidden, "A stale reference to a hidden tribute cannot issue an order");
        RightClickTribute();
        await Flush();
        Check(!_commands.Skip(beforeHidden).Any(command => command.StartsWith("TRIBUTE ")),
            "An invisible tribute is not a collectible viewport target");
        Receive("TRIBUTE 2 ACTIVE 0 0 0 0 0 0 0 1 0");
        Receive("HP 701 0 300");
        int beforeZeroHealth = _commands.Count;
        RightClickTribute();
        await Flush();
        Check(_commands.Count == beforeZeroHealth, "Zero-health hero cannot collect while waiting for its REMOVE snapshot");
        Receive("REMOVE 701");
        Receive("UNIT 200 705 9 5 2 1");
        await Flush();
        int beforeDead = _commands.Count;
        RightClickTribute();
        await Flush();
        Check(_commands.Count == beforeDead,
            "A dead owned hero cannot be replaced by a worker, mercenary, minion, or another player's hero for collection");
    }

    private static void CheckSnapshots()
    {
        foreach (string malformed in InvalidSnapshots())
            Check(!TributeSnapshot.TryParse(malformed.Split(' '), out _), $"Malformed tribute snapshot accepted: {malformed}");
        Check(TributeSnapshot.TryParse("TRIBUTE 0 WAITING 0 0 3600 0 0 0 0 0 0".Split(' '), out var waiting) &&
            !waiting.Active && !waiting.Channeling && !waiting.RamsMarching && waiting.RemainingSeconds(0) == 120 &&
            waiting.RemainingSeconds(3600) == 0 && waiting.RemainingSeconds(uint.MaxValue) == 0 && waiting.Progress(3600) == 0,
            "Waiting time is nonnegative, uses thirty server ticks per second, and has no channel progress");
        Check(TributeSnapshot.TryParse("TRIBUTE 1 WAITING 0 0 0 0 0 0 0 1 0".Split(' '), out var marching) &&
            marching.RamsMarching && !marching.Channeling && marching.Progress(3810) == 0 &&
            marching.RemainingSeconds(3810) == 0,
            "A zero next-spawn tick is a valid ram march, not an active tribute or an elapsed countdown");
        Check(TributeSnapshot.TryParse("TRIBUTE 1 ACTIVE 0 0 0 701 1 3600 3810 2 3".Split(' '), out var channel) &&
            channel.Position == Vector3.Zero && channel.Channeling && channel.Team1Count == 2 && channel.Team2Count == 3 &&
            channel.Progress(0) == 0 && channel.Progress(3600) == 0 && Mathf.IsEqualApprox(channel.Progress(3705), .5f) &&
            channel.Progress(3810) == 1 && channel.Progress(uint.MaxValue) == 1 &&
            channel.RemainingSeconds(3600) == 7 && channel.RemainingSeconds(3810) == 0,
            "Channel progress clamps before and after the seven-second authoritative interval");
        Check(TributeSnapshot.TryParse("TRIBUTE 1 ACTIVE 0 0 0 0 0 0 0 0 0".Split(' '), out var idle) &&
            !idle.Channeling && idle.Progress(3600) == 0, "Available tribute has no phantom channel");
    }

    private static IEnumerable<string> InvalidSnapshots()
    {
        foreach (string line in new[]
        {
            "TRIBUTE", "TRIBUTE 1 ACTIVE 0 0 0 0 0 0 0 0", "TRIBUTE 1 ACTIVE 0 0 0 0 0 0 0 0 0 extra",
            "OTHER 1 ACTIVE 0 0 0 0 0 0 0 0 0", "TRIBUTE 1 UNKNOWN 0 0 0 0 0 0 0 0 0",
            "TRIBUTE 1 active 0 0 0 0 0 0 0 0 0", "TRIBUTE 0 ACTIVE 0 0 0 0 0 0 0 0 0",
            "TRIBUTE 1 ACTIVE NaN 0 0 0 0 0 0 0 0", "TRIBUTE 1 ACTIVE 0 Infinity 0 0 0 0 0 0 0",
            "TRIBUTE 1 ACTIVE -Infinity 0 0 0 0 0 0 0 0", "TRIBUTE 1 ACTIVE 0 0 1 0 0 0 0 0 0",
            "TRIBUTE 1 WAITING 0 0 3600 701 1 3000 3210 0 0", "TRIBUTE 1 WAITING 0 0 3600 0 1 0 0 0 0",
            "TRIBUTE 1 WAITING 0 0 3600 0 0 1 0 0 0", "TRIBUTE 1 WAITING 0 0 3600 0 0 0 1 0 0",
            "TRIBUTE 1 WAITING 0 0 0 701 1 3600 3810 1 0", "TRIBUTE 1 WAITING 0 0 0 0 1 0 0 1 0",
            "TRIBUTE 1 WAITING 0 0 0 0 0 3600 3810 1 0",
            "TRIBUTE 1 ACTIVE 0 0 0 701 0 3600 3810 0 0", "TRIBUTE 1 ACTIVE 0 0 0 701 3 3600 3810 0 0",
            "TRIBUTE 1 ACTIVE 0 0 0 0 1 3600 3810 0 0", "TRIBUTE 1 ACTIVE 0 0 0 701 1 3600 0 0 0",
            "TRIBUTE 1 ACTIVE 0 0 0 701 1 3600 3600 0 0", "TRIBUTE 1 ACTIVE 0 0 0 701 1 3810 3600 0 0"
        }) yield return line;
        foreach (int index in new[] { 1, 5, 6, 7, 8, 9, 10, 11 })
            foreach (string invalid in new[] { "-1", "4294967296", "NaN" })
            {
                string[] fields = "TRIBUTE 1 ACTIVE 0 0 0 701 1 3600 3810 0 0".Split(' ');
                fields[index] = invalid;
                yield return string.Join(' ', fields);
            }
    }

    private void BeginHeroSession()
    {
        Receive($"MAP 2 {Map.MapHash}");
        Receive("WORLD_READY");
        Receive("WELCOME 7 1 HERO 200");
        Receive("SIGHT UNIT 200 14");
        Receive("UNIT 200 701 7 -4 0 1");
        Receive("HP 701 300 300");
        Units.Camera.Size = 26;
        CameraNavigation.FocusGround(Units.Camera, Vector3.Zero);
    }

    private async Task CapturePreview()
    {
        Units.Camera.Size = 26;
        CameraNavigation.FocusGround(Units.Camera, Vector3.Zero);
        await Flush();
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using Image image = GetViewport().GetTexture().GetImage();
        Error error = image.SavePng("res://docs/images/tribute-preview.png");
        Check(error == Error.Ok, $"Tribute preview could not be saved: {error}");
    }

    private void CheckReset(string context) => Check(Tribute == null && !_tributeView.IsVisibleInTree() &&
        !_tributePanel.IsVisibleInTree(), $"{context} must clear tribute state, target, and HUD");

    private void RightClickTribute()
    {
        Vector2 point = Units.Camera.UnprojectPosition(_tributeView.GlobalPosition + Vector3.Up);
        using var motion = new InputEventMouseMotion { Position = point, GlobalPosition = point };
        GetViewport().PushInput(motion, true);
        using var input = new InputEventMouseButton
        {
            Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Right,
            Pressed = true, ButtonMask = MouseButtonMask.Right
        };
        GetViewport().PushInput(input, true);
        input.Pressed = false;
        input.ButtonMask = 0;
        GetViewport().PushInput(input, true);
    }

    private static string WireAfter(MemoryStream wire, long offset) => Encoding.UTF8.GetString(wire.ToArray()[(int)offset..]);
    private void Receive(string message) => InvokeMain("OnMessage", message);
    private void InvokeMain(string name, params object[] args) => typeof(Main).GetMethod(name, Private).Invoke(this, args);
    private async Task Flush()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
