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
                game.Buildings.TownHallScene != null, "Main scene connects its separate building manager");
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
            Buildings = new BuildingManager { TownHallScene = GD.Load<PackedScene>("res://buildings/TownHall.tscn") };
            AddChild(Buildings);

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
            Receive("WELCOME 7");
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
                (-left.GlobalBasis.Z).IsEqualApprox(Vector3.Right) &&
                (-right.GlobalBasis.Z).IsEqualApprox(Vector3.Left), "Both entrances face the centre");
            Check(left.GetMeta("footprint").AsVector2I() == new Vector2I(3, 3) &&
                left.GetNode<Area3D>("SelectionArea").CollisionLayer == 8, "3x3 building uses separate selection layer");
            CheckBannerIsolation(left, right);

            Receive("BUILDING 0 16001 1 -64.5 2.25 0.25");
            Check(Buildings.GetChildCount() == 2 && Buildings.GetNode<Building>("Building_16001") == left &&
                left.GlobalPosition.IsEqualApprox(new Vector3(-64.5f, 0, 2.25f)) &&
                Mathf.IsEqualApprox(left.GlobalRotation.Y, 0.25f), "Duplicate snapshot updates pose using invariant decimals");
            foreach (string invalid in new[] {
                "BUILDING 1 16001 1 0 0 0", "BUILDING 999 16003 1 0 0 0",
                "BUILDING 0 broken 1 0 0 0", "BUILDING 0 0 1 0 0 0", "BUILDING 0 16001 0 0 0 0",
                "BUILDING 0 16001 1 NaN 0 0", "BUILDING 0 16001 1 0 Infinity 0",
                "BUILDING 0 16001 1 0 0 NaN", "BUILDING 0 16001 1 0 0",
                "BUILDING 0 16001 1 0 0 0 extra" }) Receive(invalid);
            Check(Buildings.GetChildCount() == 2 && left.SideId == 1 &&
                left.GlobalPosition.IsEqualApprox(new Vector3(-64.5f, 0, 2.25f)), "Invalid payload cannot create or mutate buildings");

            Receive("UNIT 1 16003 7 0 0");
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
            GD.Print("PASS: building scene wiring, WORLD_READY gate, server identities, mirrored entrances, independent banners, duplicate and malformed snapshots, removal, reconnect");
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
                blue.AlbedoColor.IsEqualApprox(new Color("376a94")) &&
                red.AlbedoColor.IsEqualApprox(new Color("a33c37")) &&
                source.AlbedoColor.IsEqualApprox(new Color("376a94")), "Side tint does not alter shared mesh material");
            return;
        }
        throw new InvalidOperationException("TownHall must contain its banner material");
    }

    private void Receive(string message) => MessageHandler.Invoke(this, new object[] { message });
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
