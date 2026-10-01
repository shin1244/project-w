using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

// 실제 입력/메시지 경로를 사용하고 네트워크 전송만 메모리에 기록한다.
public partial class RallyChecks : Main
{
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<string> _commands = new();
    private PlayerInput _input;

    public override async void _Ready()
    {
        try
        {
            CheckProtocol();
            var net = GetNode<NetClient>("/root/Net");
            using var wire = new MemoryStream();
            using var writer = new StreamWriter(wire, new UTF8Encoding(false)) { AutoFlush = true };
            typeof(NetClient).GetField("_writer", Private).SetValue(net, writer);
            typeof(Main).GetField("_net", Private).SetValue(this, net);
            Invoke("ConnectInput");
            Units.CommandRequested += command => { _commands.Add(command); Invoke("SendCommand", command); };
            _input = GetNode<PlayerInput>("PlayerInput");
            Fog.Configure(Map);
            Receive($"MAP 2 {Map.MapHash}");
            Receive("WORLD_READY");
            Receive("WELCOME 7 1 COMMANDER");
            Receive("SIGHT BUILDING 0 12");
            Receive("SIGHT BUILDING 4 8");
            Receive("BUILDING 0 201 1 -10 0 0");
            Receive("BUILDING 4 202 1 10 0 0");
            Receive("BUILDING 4 203 1 18 0 0");
            Receive("BUILDING 0 204 2 25 0 0");
            Receive("BUILDING 1 205 1 -25 0 0");
            Receive("UNIT 0 101 7 -10 5 1");
            Receive("STATS 201 0 0 0 0 12");
            Receive("HP 201 1000 1000");
            ResourceNode tree = Resources.SpawnOrUpdate(1000000, 0, new Vector3(0, 0, -3));
            tree.SetAmount(320);
            Units.Camera.Size = 45;
            CameraNavigation.FocusGround(Units.Camera, Vector3.Zero);
            await Flush();

            Building hall = Select(201);
            Vector3 originalPosition = Units.LiveUnits.Single().GlobalPosition;
            int before = _commands.Count;
            Click(tree.GlobalPosition + Vector3.Up * 2);
            await Flush();
            Check(_commands.Count == before + 1 && _commands[^1] == "RALLY 201 GATHER 1000000", "Actual tree right-click sends a hall resource rally");
            Check(hall.Rally == null && Buildings.GetNodeOrNull<RallyMarker>("RallyMarker") == null,
                "No predicted rally marker appears before server acknowledgement");
            Check(Units.LiveUnits.Count == 1 && Units.LiveUnits.Single().GlobalPosition == originalPosition,
                "Rally requests neither create nor move client units");
            Receive("RALLY 201 GATHER 0 -3 1000000");
            RallyMarker marker = Buildings.GetNode<RallyMarker>("RallyMarker");
            Check(marker.Visible && marker.GetNode<Node3D>("Flag").GlobalPosition == tree.GlobalPosition && hall.Rally?.ResourceId == tree.ResourceId,
                "Authoritative resource rally displays a flag at the resource position");
            Receive("QUEUE 201 48 800:0:7");

            RallySnapshot? accepted = hall.Rally;
            foreach (string invalid in new[] { "RALLY 201 MOVE NaN 0", "RALLY 201 GATHER 1 2 0", "RALLY 201 CLEAR junk", "RALLY 999 MOVE 1 2" }) Receive(invalid);
            Check(hall.Rally == accepted && marker.Visible, "Malformed or unknown rally updates cannot replace valid state");
            Building a = Select(202);
            Check(!marker.Visible, "Unconfigured producer selection hides the prior flag");
            Units.RequestContextOrder(tree, tree.GlobalPosition);
            Check(_commands[^1] == "RALLY 202 MOVE 0 -3", "Barracks resource click creates movement, not gathering");
            Receive("RALLY 202 GATHER 0 -3 1000000");
            Check(a.Rally == null, "Barracks rejects a resource-mode snapshot");
            Receive("RALLY 202 MOVE 4 6");
            Check(marker.Visible && a.Rally?.Position == new Vector3(4, 0, 6), "Barracks has its own rally");
            Select(203);
            Receive("RALLY 203 MOVE 20 8");
            Check(a.Rally?.Position == new Vector3(4, 0, 6), "Changing a different barracks does not overwrite the first");
            Select(201);
            Check(marker.Visible && marker.GetNode<Node3D>("Flag").GlobalPosition == tree.GlobalPosition, "Reselecting hall restores its resource flag");

            before = _commands.Count;
            _input.QueueMinimapMove(new Vector3(-12.5f, 0, 9.25f));
            await Flush();
            Check(_commands.Count == before + 1 && _commands[^1] == "RALLY 201 MOVE -12.5 9.25", "Minimap right-click uses selected producer");
            Receive("RALLY 201 MOVE -12.5 9.25");
            Check(hall.Rally?.ResourceId == 0, "Ground acknowledgement replaces gathering mode");
            Click(hall.GlobalPosition + Vector3.Up * 2);
            await Flush();
            Check(_commands[^1] == "RALLY 201 CLEAR" && hall.Rally != null, "Right-clicking producer itself requests clear but waits for server");
            Receive("RALLY 201 CLEAR");
            Check(hall.Rally == null && !marker.Visible, "Server clear removes marker and remembered rally");

            before = _commands.Count;
            foreach (uint id in new uint[] { 204, 205 })
            {
                Building invalid = Select(id);
                Units.RequestContextOrder(null, Vector3.Zero);
                Receive($"RALLY {id} MOVE 1 2");
                Check(invalid.Rally == null && !marker.Visible, "Enemy and nonproducer rallies remain unavailable");
            }
            Check(_commands.Count == before, "Invalid producers send no rally command");
            Select(202);
            Receive("CONSTRUCTION 202 35 7");
            Units.RequestContextOrder(null, new Vector3(2, 0, 4));
            Check(_commands[^1] == "RALLY 202 MOVE 2 4", "Can prepare rally during construction");
            Receive("CONSTRUCTION 202 100 7");
            Invoke("SelectUnit", Units.LiveUnits.Single());
            Check(!marker.Visible, "Unit selection hides producer rally");
            Units.RequestContextOrder(tree, tree.GlobalPosition);
            Check(_commands[^1] == "GATHER 1000000 101", "Existing worker right-click still gathers normally");
            Units.RequestContextOrder(null, new Vector3(3, 0, 4));
            Check(_commands[^1] == "MOVE 3 4 101", "Existing unit movement is unchanged");

            Select(202);
            Receive("BUILDING 4 202 2 10 0 0");
            Check(a.Rally == null && !marker.Visible, "Producer team change clears rally");
            Select(203);
            Check(marker.Visible, "Other producer rally remains intact");
            Receive("REMOVE 203");
            Check(Buildings.SelectedBuilding == null && !marker.Visible, "Producer destruction removes its flag");
            Select(201);
            Receive("RALLY 201 GATHER 0 -3 1000000");
            Resources.Remove(tree.ResourceId);
            Check(marker.Visible && hall.Rally?.ResourceId == 1000000, "Depletion preserves rally area for server retargeting");
            long wireBefore = wire.Length;
            Map.StopSync();
            Units.RequestContextOrder(null, Vector3.Zero);
            Check(wire.Length == wireBefore, "Unsynchronized world blocks wire transmission");
            Receive($"MAP 2 {Map.MapHash}");
            Check(!marker.Visible && Buildings.SelectedBuilding == null, "Resynchronization clears rally display");
            Receive("WORLD_READY");
            Receive("WELCOME 7 1 COMMANDER");
            Receive("BUILDING 0 201 1 -10 0 0");
            Receive("RALLY 201 GATHER 0 -3 1000000");
            Select(201);
            Check(marker.Visible, "Join snapshot restores the server's rally");
            Invoke("OnConnectionClosed", "rally check disconnect");
            Check(!marker.Visible && Buildings.SelectedBuilding == null, "Disconnect clears rally and selection");
            string sent = Encoding.UTF8.GetString(wire.ToArray()).Replace("\r\n", "\n");
            Check(sent.Contains("RALLY 201 GATHER 1000000\n") && sent.Contains("RALLY 201 CLEAR\n"), "Rally commands use normal framed network output");
            GD.Print("PASS: rally right-click input, auto-gather requests, server acknowledgement, independent building flags, minimap, clear, invalid messages, ownership, construction, depletion and reconnect");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private static void CheckProtocol()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Check(Protocol.BuildRallyMove(9, 1.25f, -2.5f) == "RALLY 9 MOVE 1.25 -2.5", "Rally coordinates use invariant culture");
            Check(RallySnapshot.TryParse("RALLY 9 GATHER 1.25 -2.5 8".Split(' '), out var r) && r.Position == new Vector3(1.25f, 0, -2.5f) && r.ResourceId == 8,
                "Resource snapshot uses server coordinates and ID");
            foreach (string line in new[] { "RALLY", "RALLY 0 CLEAR", "RALLY -1 CLEAR", "RALLY 9 MOVE 1", "RALLY 9 MOVE Inf 1", "RALLY 9 GATHER 1 2", "RALLY 9 GATHER 1 2 -1", "RALLY 9 UNKNOWN" })
                Check(!RallySnapshot.TryParse(line.Split(' '), out _), "Reject malformed rally: " + line);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    private Building Select(uint id)
    {
        Check(Buildings.TryGetBuilding(id, out var building), "Building exists");
        Invoke("SelectBuilding", building);
        return building;
    }

    private void Click(Vector3 position)
    {
        Vector2 screen = Units.Camera.UnprojectPosition(position);
        foreach (bool pressed in new[] { true, false })
        {
            using var input = new InputEventMouseButton
            {
                Position = screen, GlobalPosition = screen, ButtonIndex = MouseButton.Right,
                Pressed = pressed, ButtonMask = pressed ? MouseButtonMask.Right : 0
            };
            GetViewport().PushInput(input, true);
        }
    }

    private async Task Flush()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private void Receive(string message) => Invoke("OnMessage", message);
    private void Invoke(string name, params object[] args) => typeof(Main).GetMethod(name, Private).Invoke(this, args);
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
