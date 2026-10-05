using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

// 서버 없이 HIDE/재등장, 진영 공유 시야, 시야 설정과 초기화를 검증합니다.
public partial class FogSyncChecks : Main
{
    private static readonly MethodInfo Handler = typeof(Main).GetMethod("OnMessage", BindingFlags.Instance | BindingFlags.NonPublic);
    private void Receive(string message) => Handler.Invoke(this, new object[] { message });

    public override async void _Ready()
    {
        try
        {
            var scene = GD.Load<PackedScene>("res://game/Main.tscn").Instantiate<Main>();
            Check(scene.Fog.Units == scene.Units && scene.Fog.Buildings == scene.Buildings && scene.Fog.GetParent() == scene.Units.Camera,
                "Main scene connects shared sight and camera overlay");
            scene.Free();
            Resources = new ResourceManager
            {
                OakScene = GD.Load<PackedScene>("res://resources/trees/Oak.tscn"),
                PineScene = GD.Load<PackedScene>("res://resources/trees/Pine.tscn"),
                BirchScene = GD.Load<PackedScene>("res://resources/trees/Birch.tscn")
            };
            AddChild(Resources);
            Map = new MapWorld { Resources = Resources, MapPath = "res://tests/fixtures/map-fog-open.json" };
            AddChild(Map);
            Units = new UnitManager
            {
                WorkerScene = GD.Load<PackedScene>("res://units/Worker.tscn"),
                KnightScene = GD.Load<PackedScene>("res://units/Knight.tscn"),
                ArcherScene = GD.Load<PackedScene>("res://units/Archer.tscn")
            };
            AddChild(Units);
            Buildings = new BuildingManager
            {
                FortressScene = GD.Load<PackedScene>("res://buildings/Fortress.tscn"),
                TowerScene = GD.Load<PackedScene>("res://buildings/Tower.tscn"),
                TownHallScene = GD.Load<PackedScene>("res://buildings/TownHall.tscn"), Units = Units
            };
            AddChild(Buildings);
            Units.Buildings = Buildings;
            var camera = new Camera3D { Position = new Vector3(0, 30, 20), RotationDegrees = new Vector3(-60, 0, 0), Current = true };
            AddChild(camera);
            Units.Camera = camera;
            Fog = new FogOfWar { Units = Units, Buildings = Buildings };
            camera.AddChild(Fog);
            Fog.Configure(Map);
            Buildings.VisibilityCheck = Fog.IsBuildingVisible;
            var net = new NetClient();
            AddChild(net);
            typeof(Main).GetField("_net", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(this, net);
            Receive("SIGHT UNIT 0 8");
            Check(Fog.Team == 0, "Fog remains closed before handshake");
            Receive($"MAP 2 {Map.MapHash}");
            Receive("WORLD_READY");
            Receive("WELCOME 7 1 COMMANDER");
            foreach (string definition in new[] { "SIGHT UNIT 0 8", "SIGHT UNIT 1 8", "SIGHT UNIT 2 8", "SIGHT UNIT 100 8", "SIGHT UNIT 101 8", "SIGHT BUILDING 0 12", "SIGHT BUILDING 1 12" }) Receive(definition);
            Receive("BODY UNIT 0 0.4");
            Receive("BODY BUILDING 1 4 4");
            Receive("UNIT 0 101 7 0 0 1");
            Receive("UNIT 100 102 0 25 0 1");
            Receive("UNIT 101 201 0 2 0 2");
            Receive("BUILDING 1 301 1 -40 0 0");
            Receive("BUILDING 0 302 2 60 0 0");
            Fog.RefreshVision();
            Check(Fog.Team == 1 && Fog.IsVisibleAt(new Vector3(8.35f, 0, 0)) && !Fog.IsVisibleAt(new Vector3(8.45f, 0, 0)), "Sight expands from the body edge without a fixed visual inset");
            Check(Fog.IsVisibleAt(new Vector3(25, 0, 0)) && Fog.IsVisibleAt(new Vector3(-30, 0, 0)), "Owner-zero allies and same-team buildings share sight");
            Check(!Fog.IsVisibleAt(new Vector3(60, 0, 0)) && Buildings.TryGetBuilding(302, out _), "Known enemy building does not grant vision");
            Check(!Fog.IsVisibleAt(new Vector3(10000, 0, 0)), "No vision outside map bounds");

            Check(Fog.IsVisibleAt(new Vector3(-26.05f, 0, 0)) && !Fog.IsVisibleAt(new Vector3(-25.95f, 0, 0)), "Building sight starts at its wall");
            Vector2 corner = new(-38, 2);
            Vector2 cornerInner = corner + Vector2.One.Normalized() * 11.95f;
            Vector2 cornerOuter = corner + Vector2.One.Normalized() * 12.05f;
            Check(Fog.IsVisibleAt(new(cornerInner.X, 0, cornerInner.Y)) && !Fog.IsVisibleAt(new(cornerOuter.X, 0, cornerOuter.Y)),
                "Building sight has rounded corners at the same surface distance");
            Receive("BODY BUILDING 1 6 2");
            Fog.RefreshVision();
            Check(Fog.IsVisibleAt(new(-25.05f, 0, 0)) && !Fog.IsVisibleAt(new(-40, 0, 13.05f)), "Server rectangular footprint controls range on both axes");
            Receive("BODY BUILDING 1 4 4");
            Receive("BODY UNIT 0 0.9");
            Fog.RefreshVision();
            Check(Fog.IsVisibleAt(new(8.8f, 0, 0)), "Server radius overrides the decorative model size");
            Receive("BODY UNIT 0 0.4");
            foreach (string bad in new[] { "BODY UNIT 0 NaN", "BODY UNIT 0 -2", "BODY UNIT 0 100 extra", "BODY BUILDING 1 4 NaN", "BODY BUILDING 1 0 0", "BODY OTHER 0 100" }) Receive(bad);
            Fog.RefreshVision();
            Check(!Fog.IsVisibleAt(new(8.8f, 0, 0)), "Body changes invalidate cached contours; malformed definitions are ignored");

            Receive("UNIT 0 105 7 0 -20 1");
            Receive("STATS 105 5 0.5 1 5 2");
            Fog.RefreshVision();
            Check(Fog.IsVisibleAt(new(2.3f, 0, -20)) && !Fog.IsVisibleAt(new(2.5f, 0, -20)), "Instance sight overrides the type default");
            Receive("STATS 105 5 0.5 1 5 4");
            Fog.RefreshVision();
            Check(Fog.IsVisibleAt(new(4.3f, 0, -20)), "Instance sight changes rebuild the contour");
            Receive("HIDE 105");
            Receive("UNIT 1 103 7 -20 -20 1");
            Receive("SIGHT UNIT 100 3");
            Fog.RefreshVision();
            Check(!Fog.IsVisibleAt(new Vector3(29, 0, 0)) && Fog.IsVisibleAt(new Vector3(-14, 0, -20)),
                "Changing melee minion sight does not change RTS knight sight");
            Receive("SIGHT UNIT 100 8");
            Receive("HIDE 103");
            Fog.RefreshVision();

            Building knownEnemy = Buildings.GetNode<Building>("Building_302");
            Buildings.SelectSingle(knownEnemy);
            Check(Buildings.SelectedBuilding == null, "Known buildings inside fog cannot be inspected");
            Receive("POS 101 49.2 0");
            Buildings.SelectSingle(knownEnemy); // Checks pending vision without waiting for the render throttle.
            Check(Buildings.SelectedBuilding == knownEnemy && knownEnemy.IsFogRevealed && !Fog.IsVisibleAt(knownEnemy.GlobalPosition),
                "A visible outer wall reveals the whole building without lighting the ground at its center");
            var reveal = knownEnemy.GetNode<MeshInstance3D>("Visual/FogReveal");
            var revealMaterial = (StandardMaterial3D)reveal.GetSurfaceOverrideMaterial(0);
            Check(revealMaterial.RenderPriority == 121 && !revealMaterial.NoDepthTest &&
                revealMaterial.DepthDrawMode == BaseMaterial3D.DepthDrawModeEnum.Disabled,
                "Building-only reveal preserves depth occlusion and does not write the terrain mask");
            if (OS.GetCmdlineUserArgs().Contains("--capture-building-fog")) await CaptureBuildingFog(camera, knownEnemy);
            Receive("BUILDING_VISION 302 0");
            Fog.RefreshVision();
            Check(!knownEnemy.Visible && !knownEnemy.IsFogRevealed && !Buildings.CanInspect(knownEnemy),
                "Server-hidden building has no model, health bar or minimap visibility despite local interpolation");
            foreach (string bad in new[] { "BUILDING_VISION 302 2", "BUILDING_VISION 302 1 extra", "BUILDING_VISION -1 1", "BUILDING_VISION 0 1" }) Receive(bad);
            Fog.RefreshVision();
            Check(!knownEnemy.IsFogRevealed, "Malformed building visibility is ignored");
            Receive("BUILDING_VISION 302 1");
            Fog.RefreshVision();
            Check(knownEnemy.IsFogRevealed && Buildings.CanInspect(knownEnemy), "Server visibility controls both reveal and inspection");
            Receive("POS 101 0 0");
            Receive("BUILDING_VISION 302 0");
            Buildings._Process(0);
            Check(Buildings.SelectedBuilding == null, "Current building inspection closes as the scout leaves");

            Receive("BUILDING_HIDE 302");
            Check(!Buildings.TryGetBuilding(302, out _) && !knownEnemy.IsInsideTree() && !Buildings.CanInspect(knownEnemy),
                "Building hide immediately removes model, selection, collision and minimap registry");
            Receive("HP 302 1 2000");
            Receive("CONSTRUCTION 302 50 8");
            Check(!Buildings.TryGetBuilding(302, out _), "Late hidden-building deltas cannot recreate it");
            Receive("BUILDING 0 302 2 60 0 0");
            Building rediscovered = Buildings.GetNode<Building>("Building_302");
            Check(!rediscovered.Visible, "An unconfirmed enemy spawn never flashes through fog");
            Receive("CONSTRUCTION 302 100 8");
            Receive("HP 302 1700 2000");
            Receive("BUILDING_VISION 302 1");
            Fog.RefreshVision();
            Check(rediscovered.Visible && rediscovered != knownEnemy && rediscovered.HealthBar.CurrentHP == 1700 &&
                !rediscovered.IsUnderConstruction, "Rediscovery restores current state to a fresh building");
            Receive("BUILDING_HIDE 302");
            Receive("BUILDING_HIDE 302");
            Receive("BUILDING_HIDE 301 extra");
            Check(Buildings.TryGetBuilding(301, out _), "Malformed hide cannot remove an allied building");

            // 빈 땅은 반지름 0인 대상이다. 유닛 반지름 0.4 + 시야 8의 경계가 유지되어야 한다.
            Receive("POS 101 0.23 0.37");
            Fog.RefreshVision();
            for (int i = 0; i < 64; i++)
            {
                Vector2 radial = Vector2.FromAngle(Mathf.Tau * i / 64);
                Vector3 inner = new(.23f + radial.X * 8.35f, 0, .37f + radial.Y * 8.35f);
                Vector3 outer = new(.23f + radial.X * 8.45f, 0, .37f + radial.Y * 8.45f);
                Check(Fog.IsVisibleAt(inner) && !Fog.IsVisibleAt(outer), "Surface-based circle is independent of grid phase and angle");
            }
            Receive("POS 101 0 0");
            Fog.RefreshVision();

            Unit own = Units.GetNode<Unit>("Unit_101");
            Unit enemy = Units.GetNode<Unit>("Unit_201");
            var commands = new List<string>();
            Units.CommandRequested += commands.Add;
            Units.SelectSingle(own);
            Units.RequestAttack(enemy);
            Receive("STATE 101 ATTACK 0 201 1");
            Receive("HIDE 201");
            Check(Units.ResolveFocus(201) == null && !enemy.IsInsideTree() && enemy.IsQueuedForDeletion() && !enemy.IsDying && !enemy.Visible,
                "HIDE removes targeting, collision and model immediately without a death animation");
            Check(!enemy.HasNode("CommandTargetRing"), "Hidden target indicator cleared immediately");
            int sent = commands.Count;
            Units.RequestAttack(enemy);
            Units.RequestContextOrder(enemy, Vector3.Zero);
            Check(commands.Count == sent, "Cannot attack a hidden stale reference");
            Receive("HIDE 201");
            Receive("HIDE 301");
            Receive("HIDE 101 extra");
            Receive("HIDE nope");
            Receive("POS 201 50 50");
            Receive("HP 201 1 60");
            Receive("STATE 201 ATTACK 0 101 99");
            Check(Units.ResolveFocus(201) == null && Units.ResolveFocus(101) == own && Buildings.TryGetBuilding(301, out _),
                "Duplicate/invalid HIDE and late deltas do not recreate units or hide buildings");

            Receive("UNIT 101 201 0 4 1 2");
            Unit returned = Units.GetNode<Unit>("Unit_201");
            bool replayed = false;
            returned.StateChanged += (_, fired) => replayed |= fired;
            Receive("STATE 201 ATTACK 0 101 99");
            Receive("HP 201 34 60");
            Check(returned != enemy && returned.HasServerState && returned.HealthBar.CurrentHP == 34 && !replayed,
                "Reappearance uses fresh position/HP/state without replaying unseen attacks");
            own._Process(0);
            Vector3 direction = (returned.GlobalPosition - own.GlobalPosition).Normalized();
            Check((-own.GetNode<Node3D>("Visual").GlobalBasis.Z).Dot(direction) > .999f, "A reappearing focus resolves to its new instance");
            Receive("REMOVE 201");
            Check(returned.IsDying, "REMOVE still uses the normal death effect");

            Receive("SIGHT UNIT 0 3");
            foreach (string bad in new[] { "SIGHT UNIT 0 NaN", "SIGHT UNIT 0 -8", "SIGHT OTHER 0 100", "SIGHT UNIT 0 100 extra" }) Receive(bad);
            Fog.RefreshVision();
            Check(Fog.IsVisibleAt(Vector3.Zero) && !Fog.IsVisibleAt(new Vector3(7, 0, 0)), "Server sight changes apply; invalid settings do not expand vision");
            Receive("POS 101 -20 20");
            Fog.RefreshVision();
            Check(!Fog.IsVisibleAt(Vector3.Zero) && Fog.IsVisibleAt(new Vector3(-20, 0, 20)), "Moving a source closes old vision");
            Receive("REMOVE 301");
            Fog.RefreshVision();
            Check(!Fog.IsVisibleAt(new Vector3(-40, 0, 0)), "Destroyed tower stops granting sight");
            Receive($"MAP 2 {Map.MapHash}");
            Check(Fog.Team == 0 && !Fog.IsVisibleAt(new Vector3(25, 0, 0)) && Units.LiveUnits.Count == 0, "Reconnect resets sight and units");
            Receive("WORLD_READY");
            Receive("WELCOME 8 2 COMMANDER");
            Receive("SIGHT UNIT 0 8");
            Receive("UNIT 0 401 8 0 0 2");
            Fog.RefreshVision();
            Check(Fog.Team == 2 && Fog.IsVisibleAt(Vector3.Zero), "Reconnect can change teams");
            typeof(Main).GetMethod("OnConnectionClosed", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(this, new object[] { "Fog test disconnect" });
            Check(Fog.Team == 0 && !Fog.IsVisibleAt(Vector3.Zero) && Units.LiveUnits.Count == 0 && Buildings.LiveBuildings.Count == 0,
                "Disconnect clears fog data and frozen entities");
            CheckOcclusion();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GD.Print("PASS: body-edge sight, circles/rounded building footprints, server body definitions, instance sight, HIDE/reappearance, shared vision, walls/trees/corners, removal and reconnect");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private void CheckOcclusion()
    {
        Map.MapPath = "res://tests/fixtures/map-fog-occlusion.json";
        Map.LoadLocalMap();
        Fog.Configure(Map);
        Receive($"MAP 2 {Map.MapHash}");
        Receive("WORLD_READY");
        Receive("WELCOME 7 1 COMMANDER");
        Receive("SIGHT UNIT 0 8");
        Receive("SIGHT BUILDING 0 12");
        Receive("UNIT 0 701 7 -3.5 0.5 1");
        Fog.RefreshVision();
        Check(Fog.IsVisibleAt(new Vector3(-1, 0, .5f)) && !Fog.IsVisibleAt(new Vector3(.5f, 0, .5f)) &&
            !Fog.IsVisibleAt(new Vector3(3.5f, 0, .5f)), "2x2 tree and the area behind it stay dark");
        Check(!Fog.IsVisibleAt(new Vector3(2.5f, 0, -2)), "Wall outside tree opening blocks vision");
        Receive("TREE 581 0");
        Fog.RefreshVision();
        Check(Fog.IsVisibleAt(new Vector3(3.5f, 0, .5f)), "Felling a tree reopens its full footprint immediately");
        Receive("BUILDING 0 801 2 0 0 0");
        Fog.RefreshVision();
        Check(Fog.IsVisibleAt(new Vector3(-1, 0, .5f)) && Fog.IsVisibleAt(new Vector3(3.5f, 0, .5f)), "Buildings do not block vision through the gap");
        Receive("BODY BUILDING 0 2 2");
        Fog.RefreshVision();
        Check(Fog.IsVisibleAt(new(-1.5f, 0, .5f)) && Fog.IsVisibleAt(new(3.5f, 0, .5f)), "Building size changes never introduce vision blockers");
        Receive("BODY BUILDING 0 5 5");
        Receive("REMOVE 801");
        Fog.RefreshVision();
        Check(Fog.IsVisibleAt(new Vector3(3.5f, 0, .5f)), "Building destruction preserves already-open vision");
        Receive("BUILDING 0 802 1 -5.5 6.5 0");
        Fog.RefreshVision();
        Check(Fog.IsVisibleAt(new Vector3(-10.5f, 0, 6.5f)), "Building source ignores its own footprint");
        Receive("REMOVE 802");
        Fog.RefreshVision();
        Check(!Fog.IsVisibleAt(new Vector3(-10.5f, 0, 6.5f)), "Destroyed source loses its own sight");
        var grid = new FogOcclusionGrid(Map.GridOrigin, Map.CellSize, Map.GridSize);
        grid.Refresh(Map, Buildings);
        float corner = grid.RayDistance(new Vector2(-14.5f, -10.5f), Vector2.One.Normalized(), 8);
        Check(Math.Abs(corner - MathF.Sqrt(.5f)) < .0001f, "Closed diagonal corner stops a ray at the first cell boundary");
        Receive($"MAP 2 {Map.MapHash}");
        Receive("WORLD_READY");
        Receive("WELCOME 7 1 COMMANDER");
        Receive("SIGHT UNIT 0 8");
        Receive("UNIT 0 701 7 -3.5 0.5 1");
        Fog.RefreshVision();
        Check(!Fog.IsVisibleAt(new Vector3(3.5f, 0, .5f)), "Map reset restores tree occlusion");
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    private async System.Threading.Tasks.Task CaptureBuildingFog(Camera3D camera, Building building)
    {
        Transform3D previous = camera.Transform;
        camera.Projection = Camera3D.ProjectionType.Orthogonal;
        camera.Size = 23;
        camera.Position = new Vector3(56, 22, 18);
        camera.LookAt(new Vector3(56, 0, 0));
        var environment = new WorldEnvironment { Environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color("26323c"),
            AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = Colors.White, AmbientLightEnergy = .65f
        }};
        AddChild(environment);
        var light = new DirectionalLight3D { RotationDegrees = new Vector3(-55, -25, 0), LightEnergy = 1, ShadowEnabled = true };
        AddChild(light);
        foreach (bool enabled in new[] { false, true })
        {
            building.SetFogRevealed(enabled);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using Image capture = GetViewport().GetTexture().GetImage();
            capture.SavePng($"res://.godot/check-logs/building-fog-{(enabled ? "visible" : "dark")}.png");
        }
        light.QueueFree(); environment.QueueFree();
        camera.Projection = Camera3D.ProjectionType.Perspective;
        camera.Transform = previous;
    }
}
