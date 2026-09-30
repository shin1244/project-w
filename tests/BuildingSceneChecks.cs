using Godot;
using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

// Godot --headless --path . res://tests/BuildingSceneChecks.tscn
public partial class BuildingSceneChecks : Main
{
    private static readonly MethodInfo MessageHandler = typeof(Main).GetMethod(
        "OnMessage", BindingFlags.Instance | BindingFlags.NonPublic);

    public override async void _Ready()
    {
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        try
        {
            // Wire the real game scene without starting a connection.
            var game = GD.Load<PackedScene>("res://game/Main.tscn").Instantiate<Main>();
            Check(game.Buildings == game.GetNode<BuildingManager>("Buildings") &&
                game.Buildings.TownHallScene != null && game.Buildings.FortressScene != null && game.Buildings.TowerScene != null,
                "Main scene connects halls, old type 1 fortresses and new type 6 towers separately");
            game.Free();

            Resources = new ResourceManager
            {
                OakScene = GD.Load<PackedScene>("res://resources/trees/Oak.tscn"),
                PineScene = GD.Load<PackedScene>("res://resources/trees/Pine.tscn"),
                BirchScene = GD.Load<PackedScene>("res://resources/trees/Birch.tscn")
            };
            AddChild(Resources);
            Map = new MapWorld { Resources = Resources, MapPath = "res://tests/fixtures/map-trees-2x2.json" };
            AddChild(Map);
            Units = new UnitManager { KnightScene = GD.Load<PackedScene>("res://units/Knight.tscn") };
            AddChild(Units);
            Buildings = new BuildingManager
            {
                TownHallScene = GD.Load<PackedScene>("res://buildings/TownHall.tscn"),
                FortressScene = GD.Load<PackedScene>("res://buildings/Fortress.tscn"), TowerScene = GD.Load<PackedScene>("res://buildings/Tower.tscn")
            };
            AddChild(Buildings);
            var camera = new Camera3D
            {
                Current = true, Projection = Camera3D.ProjectionType.Orthogonal,
                Position = new Vector3(0, 36, 28), Size = 80
            };
            AddChild(camera);
            camera.LookAt(Vector3.Zero);

            using var sent = new MemoryStream();
            using var writer = new StreamWriter(sent, new UTF8Encoding(false), 1024, true) { AutoFlush = true };
            var net = new NetClient();
            AddChild(net);
            typeof(NetClient).GetField("_writer", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(net, writer);
            typeof(Main).GetField("_net", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(this, net);

            Receive("BUILDING 0 16001 1 -65 0 -1.5707963");
            Check(Buildings.GetChildCount() == 0, "Ignore buildings before map handshake");
            Receive($"MAP 2 {Map.MapHash}");
            Receive("BUILDING 0 16001 1 -65 0 -1.5707963");
            Check(Buildings.GetChildCount() == 0, "Wait for WORLD_READY before spawning");
            Receive("WORLD_READY");
            Receive("WELCOME 7 1");
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Receive("BUILDING 0 16001 1 -65 0 -1.5707963");
            Receive("BUILDING 0 16002 2 65 0 1.5707963");
            Building left = Buildings.GetNode<Building>("Building_16001");
            Building right = Buildings.GetNode<Building>("Building_16002");
            Check(Buildings.GetChildCount() == 2 && left.BuildingType == 0 &&
                left.BuildingId == 16001 && left.SideId == 1 && right.SideId == 2,
                "Two server IDs, base affiliation independent from player 7");
            Check(left.GlobalPosition.IsEqualApprox(new Vector3(-65, 0, 0)) &&
                right.GlobalPosition.IsEqualApprox(new Vector3(65, 0, 0)) &&
                left.GlobalRotation.IsEqualApprox(Vector3.Zero) && right.GlobalRotation.IsEqualApprox(Vector3.Zero),
                "Server yaw never rotates the root or its axis-aligned collision footprint");
            Check(left.GetNode<Marker3D>("Entrance").Position.IsEqualApprox(Vector3.Right * 2.5f) &&
                right.GetNode<Marker3D>("Entrance").Position.IsEqualApprox(Vector3.Left * 2.5f),
                "Gameplay entrance offsets keep the server's mirrored facing independent of visual facing");
            CheckCameraFacing(left, camera);
            CheckCameraFacing(right, camera);
            Check(left.GetMeta("footprint").AsVector2I() == new Vector2I(5, 5) &&
                left.GetNode<Area3D>("SelectionArea").CollisionLayer == 8, "5x5 hall uses separate selection layer");
            CheckDimensions(left, 5);
            CheckBannerIsolation(left, right);

            Receive("BUILDING 1 17001 1 -24 -33 -1.5707963");
            Receive("BUILDING 1 17002 2 24 -33 1.5707963");
            Building tower = Buildings.GetNode<Building>("Building_17001");
            Building enemyTower = Buildings.GetNode<Building>("Building_17002");
            Check(tower.BuildingType == 1 && tower.HasNode("Turret/Ballista") && tower.HasNode("Turret/Muzzle"), "Tower type, rotating weapon and projectile origin");
            CheckDimensions(tower, 4);
            CheckBannerIsolation(tower, enemyTower);
            CheckCameraFacing(tower, camera);
            CheckCameraFacing(enemyTower, camera);
            CheckCameraAndDefenseFacing(camera, left, right, tower, enemyTower);
            Receive("HP 17001 400 500");
            Receive("BUILDING 1 17001 2 -24 -33 0");
            Check(Buildings.GetNode<Building>("Building_17001") == tower && tower.SideId == 2 && tower.HealthBar.CurrentHP == 400,
                "Tower duplicate updates pose/team without replacing health or instance");
            Receive("REMOVE 17001");
            Receive("REMOVE 17002");

            Receive("BUILDING 0 16001 1 -64.5 2.25 0.25");
            Check(Buildings.GetChildCount() == 2 && Buildings.GetNode<Building>("Building_16001") == left &&
                left.GlobalPosition.IsEqualApprox(new Vector3(-64.5f, 0, 2.25f)) &&
                left.GlobalRotation.IsEqualApprox(Vector3.Zero) &&
                left.GetNode<Marker3D>("Entrance").Position.IsEqualApprox(new Vector3(0, 0, -2.5f).Rotated(Vector3.Up, .25f)),
                "Duplicate snapshot updates position and gameplay entrance using invariant decimals without rotating occupancy");
            CheckCameraFacing(left, camera);
            foreach (string invalid in new[] {
                "BUILDING 1 16001 1 0 0 0", "BUILDING 999 16003 1 0 0 0",
                "BUILDING 0 broken 1 0 0 0", "BUILDING 0 0 1 0 0 0", "BUILDING 0 16001 0 0 0 0",
                "BUILDING 0 16001 1 NaN 0 0", "BUILDING 0 16001 1 0 Infinity 0",
                "BUILDING 0 16001 1 0 0 NaN", "BUILDING 0 16001 1 0 0",
                "BUILDING 0 16001 1 0 0 0 extra" }) Receive(invalid);
            Check(Buildings.GetChildCount() == 2 && left.SideId == 1 &&
                left.GlobalPosition.IsEqualApprox(new Vector3(-64.5f, 0, 2.25f)), "Invalid payload cannot create or mutate buildings");

            Receive("UNIT 1 16003 7 0 0 1");
            Receive("REMOVE 16001");
            Receive("REMOVE 16001");
            Receive("REMOVE 99999");
            Check(!Buildings.HasNode("Building_16001") && left.IsQueuedForDeletion() &&
                Buildings.HasNode("Building_16002") && Units.HasNode("Unit_16003"), "Removal affects only matching building and is idempotent");
            Receive("REMOVE 16003");
            Check(!Units.HasNode("Unit_16003") && Buildings.GetChildCount() == 1, "Unit removal leaves buildings untouched");

            Receive($"MAP 2 {Map.MapHash}");
            Check(Buildings.GetChildCount() == 0 && !Map.IsSynchronized, "Map reset removes old buildings");
            Receive("BUILDING 0 16001 1 -65 0 -1.5707963");
            Check(Buildings.GetChildCount() == 0, "Map reset reinstates spawn gate");
            Receive("WORLD_READY");
            Receive("BUILDING 0 16001 1 -65 0 -1.5707963");
            Check(Buildings.GetChildCount() == 1 && Buildings.GetNode<Building>("Building_16001") != left,
                "Snapshot reconstructs base after reconnect");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GD.Print("PASS: building scene wiring, enlarged bounds, camera-facing visuals independent from server entrances/occupancy, preserved visual scale and defense aim, WORLD_READY gate, identities, banners, snapshots, removal and reconnect");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
        finally { CultureInfo.CurrentCulture = previousCulture; }
    }

    private static void CheckBannerIsolation(Building left, Building right)
    {
        var a = left.GetNode<MeshInstance3D>("Visual");
        var b = right.GetNode<MeshInstance3D>("Visual");
        for (int i = 0; i < a.Mesh.GetSurfaceCount(); i++)
        {
            if (a.Mesh.SurfaceGetMaterial(i) is not StandardMaterial3D source || source.ResourceName != "banner") continue;
            var blue = (StandardMaterial3D)a.GetSurfaceOverrideMaterial(i);
            var red = (StandardMaterial3D)b.GetSurfaceOverrideMaterial(i);
            Check(a.Mesh == b.Mesh && blue != red && source != red &&
                blue.AlbedoColor.IsEqualApprox(TeamMaterials.FriendlyColor) &&
                red.AlbedoColor.IsEqualApprox(TeamMaterials.EnemyColor) &&
                source.AlbedoColor.IsEqualApprox(new Color("376a94")), "Side tint does not alter shared mesh material");
            return;
        }
        throw new InvalidOperationException("TownHall must contain its banner material");
    }

    private void Receive(string message) => MessageHandler.Invoke(this, new object[] { message });

    private void CheckCameraAndDefenseFacing(Camera3D camera, params Building[] buildings)
    {
        Vector3[] scales = new Vector3[buildings.Length];
        for (int i = 0; i < buildings.Length; i++) scales[i] = buildings[i].GetNode<Node3D>("Visual").Scale;
        camera.Position = new Vector3(28, 36, 0);
        camera.LookAt(Vector3.Zero);
        for (int i = 0; i < buildings.Length; i++)
        {
            Building building = buildings[i];
            CheckCameraFacing(building, camera);
            Check(building.GetNode<Node3D>("Visual").Scale.IsEqualApprox(scales[i]) &&
                building.GlobalRotation.IsEqualApprox(Vector3.Zero),
                "Camera rotation preserves enlarged visual scale and axis-aligned world occupancy");
            Vector2I footprint = building.GetMeta("footprint").AsVector2I();
            Check(BuildingFootprint.Bounds(BuildingFootprint.Corners(building)).Size.IsEqualApprox((Vector2)footprint),
                "Camera-facing geometry does not change server occupancy dimensions");
        }
        Building defense = buildings[2];
        Node3D turret = defense.GetNode<Node3D>("Turret");
        Vector3 turretScale = turret.Scale;
        Check((-turret.GlobalBasis.Z).Normalized().IsEqualApprox(Vector3.Right),
            "Idle defense weapon faces the same camera direction as its building");
        Vector3 facing = -defense.GetNode<Node3D>("Visual").GlobalBasis.Z.Normalized();
        camera.Position += new Vector3(80, 0, 30);
        foreach (Building building in buildings) CheckCameraFacing(building, camera);
        Check((-defense.GetNode<Node3D>("Visual").GlobalBasis.Z).Normalized().IsEqualApprox(facing),
            "Orthographic camera panning leaves all building facings unchanged");

        var target = new Node3D { Position = defense.GlobalPosition + new Vector3(-5, 0, -5) };
        AddChild(target);
        defense.ResolveFocus = _ => target;
        defense.ApplyServerState(new UnitState(UnitActivity.Attack, 0, 999, 0));
        defense._Process(0);
        Vector3 aim = target.GlobalPosition - turret.GlobalPosition;
        aim.Y = 0;
        Check((-turret.GlobalBasis.Z).Normalized().IsEqualApprox(aim.Normalized()) && turret.Scale.IsEqualApprox(turretScale),
            "Attacking defense still aims at its target and preserves the enlarged weapon scale");
        CheckCameraFacing(defense, camera);
        defense.ApplyServerState(new UnitState(UnitActivity.Idle, 0, 0, 0));
        defense._Process(0);
        Check((-turret.GlobalBasis.Z).Normalized().IsEqualApprox(Vector3.Right),
            "Returning to idle restores the camera-facing weapon pose");
        defense.ResolveFocus = null;
        target.Free();
    }

    private static void CheckCameraFacing(Building building, Camera3D camera)
    {
        building._Process(0);
        Vector3 towardViewer = camera.GlobalBasis.Z;
        towardViewer.Y = 0;
        Check((-building.GetNode<Node3D>("Visual").GlobalBasis.Z).Normalized().IsEqualApprox(towardViewer.Normalized()),
            $"Building {building.BuildingId}: model front faces the player's horizontal viewing direction");
    }

    private static void CheckDimensions(Building building, int size)
    {
        Check(building.Scale == Vector3.One && building.GetMeta("footprint").AsVector2I() == new Vector2I(size, size), "Unscaled root with exact footprint");
        var shape = (BoxShape3D)building.GetNode<CollisionShape3D>("SelectionArea/CollisionShape3D").Shape;
        Check(shape.Size.X == size && shape.Size.Z == size, "Selection bounds match footprint");
        var visual = building.GetNode<MeshInstance3D>("Visual");
        Aabb source = visual.Mesh.GetAabb();
        Transform3D transform = building.GlobalTransform.AffineInverse() * visual.GlobalTransform;
        var bounds = new Aabb(transform * source.GetEndpoint(0), Vector3.Zero);
        for (int i = 1; i < 8; i++) bounds = bounds.Expand(transform * source.GetEndpoint(i));
        Check(Mathf.Abs(bounds.Size.X - size) < .001f && Mathf.Abs(bounds.Size.Z - size) < .001f && bounds.Position.Y >= -.001f,
            "Scaled original mesh fills the enlarged footprint and starts at ground");
        Check(Mathf.Abs(building.GetNode<Marker3D>("Entrance").Position.Length() - size * .5f) < .001f,
            "Gameplay entrance remains at the enlarged ground edge");
        if (building.IsDefense)
        {
            var ballista = building.GetNode<MeshInstance3D>("Turret/Ballista");
            Mesh mesh = ballista.Mesh;
            Basis weaponBasis = building.GlobalBasis.Inverse() * ballista.GlobalBasis;
            for (int surface = 0; surface < mesh.GetSurfaceCount(); surface++)
                foreach (Vector3 vertex in mesh.SurfaceGetArrays(surface)[(int)Mesh.ArrayType.Vertex].AsVector3Array())
                {
                    Vector3 enlarged = weaponBasis * vertex;
                    Check(new Vector2(enlarged.X, enlarged.Z).Length() <= size * .5f + .001f,
                        "Enlarged weapon stays inside footprint at every yaw");
                }
        }
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
