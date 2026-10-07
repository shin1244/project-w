using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

// Main messages and real viewport input, without a live server or client-side combat.
public partial class SiegeRamChecks : Main
{
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<string> _commands = new();
    private int Impacts => Units.GetChildren().OfType<SiegeRamImpact>().Count();

    public override async void _Ready()
    {
        try
        {
            CheckParser();
            var net = GetNode<NetClient>("/root/Net");
            using var wire = new MemoryStream();
            using var writer = new StreamWriter(wire, new UTF8Encoding(false)) { AutoFlush = true };
            typeof(NetClient).GetField("_writer", Private).SetValue(net, writer);
            typeof(Main).GetField("_net", Private).SetValue(this, net);
            InvokeMain("ConnectInput");
            Units.CommandRequested += command => { _commands.Add(command); InvokeMain("SendCommand", command); };
            Fog.Configure(Map);
            Receive("UNIT 300 801 0 -4 0 1");
            Receive("RAM_IMPACT 801 -4 0 1");
            Check(Units.LiveUnits.Count == 0 && Impacts == 0, "Unsynchronized worlds reject ram snapshots and impacts");
            BeginSession();
            Receive("UNIT 1 101 7 -7 3 1");
            SpawnRam(801, -4, 0, 1);
            SpawnRam(802, 4, 0, 2);
            Unit ally = Ram(801), enemy = Ram(802);
            Receive("UNIT 300 801 0 -3.5 0 1");
            Receive("UNIT 300 803 7 0 3 1");
            Check(Units.LiveUnits.Count == 3 && Ram(801) == ally && ally.OwnerId == 0 &&
                ally.GlobalPosition == new Vector3(-3.5f, 0, 0) && !Units.TryGetUnit(803, out _),
                "An autonomous ram updates by ID and rejects a player-owned spawn");
            Check(UnitCatalog.IsObjective(300) && UnitCatalog.IsAutonomous(300) && !UnitCatalog.IsMinion(300),
                "Ram remains a separate autonomous objective type");
            await Flush();
            Receive("TRIBUTE 1 WAITING 0 0 0 0 0 0 0 1 1");
            Receive("TICK 30");
            var panel = GetNode<TributeEventPanel>("SelectionUI/TributeEventPanel");
            Check(Tribute.RamsMarching && panel.StatusText == "공성추 진군 중",
                "The tribute HUD shows the server's marching state without a respawn countdown");

            Click(ally.GlobalPosition + Vector3.Up, MouseButton.Left);
            await Flush();
            Check(Units.InspectedUnit == ally && Units.SelectedUnitIds.Count == 0 && !Units.CanControl(ally) &&
                Details.FindChildren("*", "Label", true, false).OfType<Label>().Any(label => label.IsVisibleInTree() && label.Text.Contains("공성추")),
                "A viewport click inspects an allied ram without granting control");
            int before = _commands.Count;
            Click(enemy.GlobalPosition + Vector3.Up, MouseButton.Right);
            await Flush();
            Check(_commands.Count == before, "Inspecting an allied ram cannot issue movement or attack orders");
            Units.SelectSingle(Units.LiveUnits.Single(unit => unit.UnitId == 101));
            long offset = wire.Length;
            Click(enemy.GlobalPosition + Vector3.Up, MouseButton.Right);
            await Flush();
            Check(_commands.Skip(before).SequenceEqual(new[] { "ATTACK 802 101" }) &&
                Encoding.UTF8.GetString(wire.ToArray()[(int)offset..]) == "ATTACK 802 101" + System.Environment.NewLine,
                "Enemy ram viewport picking sends one ordinary ATTACK command over the network path");

            Receive("HP 802 680 700");
            Receive("RAM_IMPACT 999 4 0 2");
            Receive("RAM_IMPACT 101 -7 3 1");
            Receive("RAM_IMPACT 802 4 0 1");
            foreach (string malformed in InvalidImpacts()) Receive(malformed);
            Check(Impacts == 0 && !enemy.IsDying, "Damage and invalid IDs, types or teams do not synthesize an impact");
            Receive("RAM_IMPACT 802 4 0 2");
            Receive("RAM_IMPACT 802 4 0 2");
            Check(Impacts == 1 && Ram(802) == enemy && !enemy.IsDying && enemy.HealthBar.CurrentHP == 680,
                "One explicit impact creates one effect without predicting damage or removal");
            Receive("HP 802 0 700");
            Check(Units.TryGetUnit(802, out _) && !enemy.IsDying && Impacts == 1,
                "Even zero HP waits for authoritative REMOVE and does not replay the impact");
            Receive("REMOVE 802");
            Check(!Units.TryGetUnit(802, out _) && enemy.IsDying && Impacts == 1,
                "REMOVE retires the unit while the explicit short impact finishes independently");
            int beforeHide = Impacts;
            Units.SelectSingle(ally);
            Receive("HIDE 801");
            Check(!Units.TryGetUnit(801, out _) && !ally.IsInsideTree() && Units.InspectedUnit == null && Impacts == beforeHide,
                "HIDE removes ram model, inspection and picking without a false collision effect");
            Receive("TICK 90");
            Check(Tribute.RamsMarching && panel.StatusText == "공성추 진군 중",
                "Removing all client ram models cannot start the next tribute timer without a server snapshot");
            Receive("TRIBUTE 1 WAITING 0 0 5490 0 0 0 0 1 1");
            Check(!Tribute.RamsMarching && panel.StatusText.Contains("03:00"),
                "The post-ram server snapshot starts the authoritative three-minute wait");
            SpawnRam(803, 0, 0, 1);
            Receive("RAM_IMPACT 803 0 0 1");
            Units.Clear();
            Check(Units.LiveUnits.Count == 0 && Impacts == 0 && !Units.GetChildren().OfType<Unit>().Any(),
                "Clear immediately removes live, dying and impact nodes");
            BeginSession();
            SpawnRam(803, 0, 0, 1);
            Receive("RAM_IMPACT 803 0 0 1");
            Check(Impacts == 1, "A fresh session resets the impact ID deduplication");
            Receive($"MAP 2 {Map.MapHash}");
            Receive("RAM_IMPACT 803 0 0 1");
            Check(Units.LiveUnits.Count == 0 && Impacts == 0 && Tribute == null && !panel.Visible,
                "Map resynchronization clears models, effects and event HUD and gates stale impacts");
            if (OS.GetCmdlineUserArgs().Contains("--capture")) await CapturePreview();
            GD.Print("PASS: siege ram parsing, autonomous spawn, viewport inspection/ATTACK, server-only impact and cleanup");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private static void CheckParser()
    {
        Check(RamImpactSnapshot.TryParse("RAM_IMPACT 803 -1.25 2.5 2".Split(' '), out var impact) &&
            impact.UnitId == 803 && impact.Team == 2 && impact.Position == new Vector3(-1.25f, 0, 2.5f),
            "Impact parser preserves finite world coordinates and server identity");
        foreach (string invalid in InvalidImpacts())
            Check(!RamImpactSnapshot.TryParse(invalid.Split(' '), out _), $"Invalid impact accepted: {invalid}");
    }

    private static string[] InvalidImpacts() => new[]
    {
        "RAM_IMPACT", "RAM_IMPACT 801 0 0", "RAM_IMPACT 801 0 0 1 extra", "OTHER 801 0 0 1",
        "RAM_IMPACT 0 0 0 1", "RAM_IMPACT -1 0 0 1", "RAM_IMPACT 4294967296 0 0 1",
        "RAM_IMPACT 801 NaN 0 1", "RAM_IMPACT 801 0 Infinity 1", "RAM_IMPACT 801 -Infinity 0 1",
        "RAM_IMPACT 801 0 0 0", "RAM_IMPACT 801 0 0 3", "RAM_IMPACT 801 0 0 -1"
    };

    private void BeginSession()
    {
        Receive($"MAP 2 {Map.MapHash}");
        Receive("WORLD_READY");
        Receive("WELCOME 7 1 COMMANDER");
        Receive("SIGHT UNIT 1 18");
        Receive("SIGHT UNIT 300 18");
        Units.Camera.SetProcess(false);
        Units.Camera.Size = 26;
        CameraNavigation.FocusGround(Units.Camera, Vector3.Zero);
    }

    private void SpawnRam(uint id, int x, int z, uint team)
    {
        Receive($"UNIT 300 {id} 0 {x} {z} {team}");
        Receive($"HP {id} 700 700");
    }

    private Unit Ram(uint id) => Units.LiveUnits.Single(unit => unit.UnitId == id);

    private async Task CapturePreview()
    {
        BeginSession();
        SpawnRam(811, -4, -29, 1);
        SpawnRam(812, 4, -28, 2);
        Receive("UNIT 1 821 7 -6 -32 1");
        Receive("UNIT 1 822 8 6 -31 2");
        Ram(811).GetNode<Node3D>("Visual").RotationDegrees = new Vector3(0, -138, 0);
        Ram(812).GetNode<Node3D>("Visual").RotationDegrees = new Vector3(0, 140, 0);
        foreach (Unit unit in Units.LiveUnits) unit.SetSelected(false);
        Units.Camera.Size = 20;
        CameraNavigation.FocusGround(Units.Camera, new Vector3(0, 0, -29));
        Units.Camera.SetProcess(false);
        GetNode<CanvasLayer>("SelectionUI").Visible = false;
        Fog.Hide();
        await Flush();
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using Image image = GetViewport().GetTexture().GetImage();
        Check(image.SavePng("res://docs/images/siege-ram-preview.png") == Error.Ok, "Ram preview saves the actual lane render");
    }

    private void Click(Vector3 position, MouseButton button)
    {
        Vector2 point = Units.Camera.UnprojectPosition(position);
        using var motion = new InputEventMouseMotion { Position = point, GlobalPosition = point };
        GetViewport().PushInput(motion, true);
        using var click = new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = button,
            Pressed = true, ButtonMask = button == MouseButton.Left ? MouseButtonMask.Left : MouseButtonMask.Right };
        GetViewport().PushInput(click, true);
        click.Pressed = false; click.ButtonMask = 0;
        GetViewport().PushInput(click, true);
    }

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
