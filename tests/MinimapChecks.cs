using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

// 실제 Main 씬을 사용하되 서버 대신 메시지를 넣어 미니맵의 수명과 진영 표시를 확인합니다.
public partial class MinimapChecks : Main
{
    private static readonly MethodInfo MessageHandler = typeof(Main).GetMethod("OnMessage", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly MethodInfo DisconnectHandler = typeof(Main).GetMethod("OnConnectionClosed", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly Color Green = new("63e88a");
    private static readonly Color Red = new("ff635f");
    private bool _capture;

    public override async void _Ready()
    {
        try
        {
            _capture = OS.GetCmdlineUserArgs().Contains("--minimap-capture");
            typeof(Main).GetField("_net", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(this, GetNode<NetClient>("/root/Net"));
            Fog.Configure(Map);
            Check(Map.HasMap && Map.SyncError == null, "Main loads its terrain");
            Check(Minimap == GetNode<Minimap>("SelectionUI/Minimap") && Minimap.Map == Map &&
                Minimap.Units == Units && Minimap.Buildings == Buildings, "Main scene wires the minimap to live world managers");
            Check(Minimap.MouseFilter == Control.MouseFilterEnum.Stop, "Minimap absorbs clicks so they do not issue world orders");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Rect2 viewport = GetViewport().GetVisibleRect();
            Rect2 panel = Minimap.GetGlobalRect();
            Check(Mathf.IsEqualApprox(panel.Position.X, viewport.Position.X) &&
                Mathf.IsEqualApprox(panel.End.Y, viewport.End.Y) && panel.Size.IsEqualApprox(new Vector2(300, 168)),
                "Minimap is 300 by 168 and sits flush against the bottom-left viewport edges");
            CheckCameraDragRelease(viewport);
            Check(Mathf.IsEqualApprox(Minimap.MapRect.Size.X / Minimap.MapRect.Size.Y,
                (float)Map.GridSize.X / Map.GridSize.Y), "Rectangular terrain keeps its aspect ratio");
            Vector3 mapCenter = new(Map.GridOrigin.X + Map.GridSize.X * Map.CellSize / 2, 0,
                Map.GridOrigin.Y + Map.GridSize.Y * Map.CellSize / 2);
            Check(Minimap.TryWorldToMap(mapCenter, out Vector2 center) && center.DistanceTo(Minimap.MapRect.GetCenter()) < .01f,
                "World center maps to the center of the fitted terrain");

            Receive("WELCOME 7 1");
            Receive("UNIT 0 101 7 -40 -12 1");
            Check(Minimap.Team == 0 && Units.LiveUnits.Count == 0, "No team or markers before map synchronization");
            BeginSession(7, 1);
            Receive("UNIT 0 101 7 -40 -12 1");
            Receive("UNIT 1 102 0 -20 -12 1");
            Receive("UNIT 2 103 9 0 -12 1");
            Receive("UNIT 1 201 0 20 -12 2");
            Receive("BUILDING 0 301 1 -40 12 0");
            Receive("BUILDING 1 302 2 20 12 0");
            Check(Minimap.Team == 1 && Units.LiveUnits.Count == 4 && Buildings.LiveBuildings.Count == 2,
                "WELCOME uses the team rather than the player id, including owner-zero minions");
            using (Image snapshot = await Snapshot())
            {
                AssertMarker(snapshot, new Vector3(-40, 0, -12), Green, true, "Own unit is green");
                AssertMarker(snapshot, new Vector3(-20, 0, -12), Green, true, "Owner-zero allied minion is green");
                AssertMarker(snapshot, new Vector3(0, 0, -12), Green, true, "Another player's same-team unit is green");
                AssertMarker(snapshot, new Vector3(20, 0, -12), Red, true, "Enemy minion is red");
                AssertMarker(snapshot, new Vector3(-40, 0, 12), Green, true, "Allied building is green");
                AssertMarker(snapshot, new Vector3(20, 0, 12), Red, true, "Enemy building is red");
            }

            Receive("POS 101 -40 -25");
            Receive("HIDE 201");
            Receive("REMOVE 102");
            Receive("REMOVE 302");
            Check(Units.LiveUnits.Count == 2 && Buildings.LiveBuildings.Count == 1,
                "Hidden, dying and removed entities immediately leave the minimap's live data");
            using (Image snapshot = await Snapshot())
            {
                AssertMarker(snapshot, new Vector3(-40, 0, -25), Green, true, "Position update moves the marker");
                AssertMarker(snapshot, new Vector3(-40, 0, -12), Green, false, "No stale marker remains at the old position");
                AssertMarker(snapshot, new Vector3(20, 0, -12), Red, false, "HIDE removes the enemy marker");
                AssertMarker(snapshot, new Vector3(-20, 0, -12), Green, false, "Death animation does not keep a minimap marker");
                AssertMarker(snapshot, new Vector3(20, 0, 12), Red, false, "Destroyed building loses its marker");
            }

            Receive($"MAP 2 {Map.MapHash}");
            Check(Minimap.Team == 0 && Units.LiveUnits.Count == 0 && Buildings.LiveBuildings.Count == 0,
                "MAP clears the old team and all markers during reconnect");
            Receive("WELCOME 8 2");
            Check(Minimap.Team == 0, "WELCOME cannot reveal markers before WORLD_READY");
            Receive("WORLD_READY");
            Receive("WELCOME 8 2");
            Receive("UNIT 1 401 0 20 -12 2");
            Receive("UNIT 1 402 0 -20 -12 1");
            Check(Minimap.Team == 2, "Reconnecting on the other team updates minimap allegiance");
            using (Image snapshot = await Snapshot())
            {
                AssertMarker(snapshot, new Vector3(20, 0, -12), Green, true, "New team is green after reconnect");
                AssertMarker(snapshot, new Vector3(-20, 0, -12), Red, true, "Former team is red after reconnect");
                AssertMarker(snapshot, new Vector3(-40, 0, 12), Green, false, "Old buildings cannot survive reconnect on the minimap");
            }
            DisconnectHandler.Invoke(this, new object[] { "Minimap test disconnect" });
            Check(Minimap.Team == 0 && Units.LiveUnits.Count == 0 && Buildings.LiveBuildings.Count == 0,
                "Disconnect clears the team and frozen world markers");
            using (Image snapshot = await Snapshot())
                AssertMarker(snapshot, new Vector3(20, 0, -12), Green, false, "Disconnect leaves no frozen allied marker");

            if (_capture)
            {
                ArrangePreview();
                using Image preview = await Snapshot();
                string path = "res://.godot/minimap-preview.png";
                Check(preview.SavePng(path) == Error.Ok, "Save rendered minimap preview");
                GD.Print("Minimap preview: " + ProjectSettings.GlobalizePath(path));
            }
            GD.Print("PASS: minimap scene layout, camera drag release over UI, aspect ratio, team identity, shared minions, movement, HIDE/REMOVE, reconnect and disconnect" +
                (_capture ? ", including rendered marker colors and cleanup" : " (pixel checks require --minimap-capture)"));
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private void Receive(string message) => MessageHandler.Invoke(this, new object[] { message });

    private void CheckCameraDragRelease(Rect2 viewport)
    {
        Vector2 start = viewport.GetCenter();
        Vector2 finish = Minimap.GetGlobalTransformWithCanvas() * (Minimap.Size / 2);
        using var press = new InputEventMouseButton
        {
            Position = start, GlobalPosition = start, ButtonIndex = MouseButton.Middle,
            ButtonMask = MouseButtonMask.Middle, Pressed = true
        };
        GetViewport().PushInput(press, true);
        Check(Units.Camera.Get("dragging").AsBool(), "Middle press in the world starts camera dragging");
        using var release = new InputEventMouseButton
        {
            Position = finish, GlobalPosition = finish, ButtonIndex = MouseButton.Middle, Pressed = false
        };
        GetViewport().PushInput(release, true);
        Check(!Units.Camera.Get("dragging").AsBool(), "Releasing over the minimap ends camera dragging even when UI consumes the release");
    }

    private void BeginSession(uint player, uint team)
    {
        Receive($"MAP 2 {Map.MapHash}");
        Receive("WORLD_READY");
        Receive($"WELCOME {player} {team}");
    }

    private async Task<Image> Snapshot()
    {
        if (!_capture) return null;
        Fog.RefreshVision();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        return GetViewport().GetTexture().GetImage();
    }

    private void AssertMarker(Image screenshot, Vector3 world, Color expected, bool present, string message)
    {
        if (screenshot == null) return;
        Check(Minimap.TryWorldToMap(world, out Vector2 local), "Marker sample lies in the map");
        Vector2 screen = Minimap.GetGlobalTransformWithCanvas() * local;
        int x = Mathf.RoundToInt(screen.X), y = Mathf.RoundToInt(screen.Y);
        bool found = false;
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                Color actual = screenshot.GetPixel(x + dx, y + dy);
                found |= Mathf.Abs(actual.R - expected.R) < .08f && Mathf.Abs(actual.G - expected.G) < .08f &&
                    Mathf.Abs(actual.B - expected.B) < .08f;
            }
        Check(found == present, message);
    }

    private void ArrangePreview()
    {
        BeginSession(7, 1);
        foreach (string sight in new[] { "SIGHT UNIT 0 8", "SIGHT UNIT 1 8", "SIGHT UNIT 2 8", "SIGHT BUILDING 0 12", "SIGHT BUILDING 1 12" })
            Receive(sight);
        Receive("BUILDING 0 501 1 -65 0 0");
        Receive("BUILDING 0 502 2 65 0 0");
        Receive("BUILDING 1 503 1 -45 -22 0");
        Receive("BUILDING 1 504 1 -17 -22 0");
        Receive("BUILDING 1 505 1 -45 22 0");
        Receive("BUILDING 1 506 1 -17 22 0");
        Receive("BUILDING 1 507 2 17 -22 0");
        Receive("BUILDING 1 508 2 45 -22 0");
        Receive("BUILDING 1 509 2 17 22 0");
        Receive("BUILDING 1 510 2 45 22 0");
        Receive("UNIT 0 601 7 -60 3 1");
        Receive("UNIT 0 602 7 -63 -5 1");
        Receive("UNIT 1 603 0 -8 -22 1");
        Receive("UNIT 1 604 0 -5 -19 1");
        Receive("UNIT 2 605 0 -14 -21 1");
        Receive("UNIT 1 606 0 -8 22 1");
        Receive("UNIT 2 607 0 -14 22 1");
        Receive("UNIT 1 608 0 1 -22 2");
        Receive("UNIT 1 609 0 4 -19 2");
        Receive("UNIT 2 610 0 8 -21 2");
        Receive("UNIT 1 611 0 1 22 2");
        Receive("UNIT 2 612 0 8 22 2");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
