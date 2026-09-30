using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

// Runs the real Main message handler without connecting to a server.
public partial class TeamColorChecks : Main
{
    private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    private sealed record Surface(MeshInstance3D Instance, int Index, Mesh Mesh,
        StandardMaterial3D Source, Color SourceColor, Material InitialMaterial);

    public override async void _Ready()
    {
        try
        {
            typeof(Main).GetField("_net", PrivateInstance).SetValue(this, GetNode<NetClient>("/root/Net"));
            foreach (string name in new[] { "Worker", "Knight", "Archer" })
                CheckAsset($"res://units/{name}.tscn");
            foreach (string name in new[] { "TownHall", "Fortress", "Store", "Supply", "Barracks", "Forge", "Tower" })
                CheckAsset($"res://buildings/{name}.tscn");

            BeginWorld();
            Receive("UNIT 0 101 7 -10 0 1");
            Receive("UNIT 1 102 9 -7 0 1");
            Receive("UNIT 2 103 0 -4 0 1");
            Receive("UNIT 1 104 8 2 0 2");
            Receive("BUILDING 0 201 1 -10 8 0");
            Receive("BUILDING 1 202 2 10 8 0");
            Unit own = GetUnit(101);
            Building hall = GetBuilding(201);
            foreach (Node3D node in LiveObjects()) CheckColors(node, null, "Before WELCOME");

            Receive("WELCOME 7 1");
            foreach (uint id in new uint[] { 101, 102, 103 })
                CheckColors(GetUnit(id), TeamMaterials.FriendlyColor, "Own, allied and owner-zero minion teams");
            CheckColors(GetUnit(104), TeamMaterials.EnemyColor, "Enemy unit");
            CheckColors(hall, TeamMaterials.FriendlyColor, "Late WELCOME recolors existing hall");
            CheckColors(GetBuilding(202), TeamMaterials.EnemyColor, "Enemy tower");
            Receive("UNIT 2 105 0 4 0 2");
            CheckColors(GetUnit(105), TeamMaterials.EnemyColor, "New units inherit known local team");
            Receive("BUILDING 1 203 1 -4 8 0");
            CheckColors(GetBuilding(203), TeamMaterials.FriendlyColor, "New buildings inherit known local team");

            Receive("WELCOME 8 2");
            CheckColors(own, TeamMaterials.EnemyColor, "Team two sees team one as enemy");
            CheckColors(GetUnit(104), TeamMaterials.FriendlyColor, "Team two sees its own units as friendly");
            CheckColors(GetUnit(105), TeamMaterials.FriendlyColor, "Team two sees allied minions as friendly");
            CheckColors(hall, TeamMaterials.EnemyColor, "Team two sees team one buildings as enemy");
            CheckColors(GetBuilding(202), TeamMaterials.FriendlyColor, "Team two sees its own buildings as friendly");

            Receive("UNIT 0 101 7 -10 0 2");
            Receive("BUILDING 0 201 2 -10 8 0");
            Check(GetUnit(101) == own && GetBuilding(201) == hall, "Team changes preserve existing instances");
            CheckColors(own, TeamMaterials.FriendlyColor, "Duplicate UNIT updates team color");
            CheckColors(hall, TeamMaterials.FriendlyColor, "Duplicate BUILDING updates team color");
            Material[] beforeUnit = ActiveMaterials(own);
            Material[] beforeHall = ActiveMaterials(hall);
            Receive("WELCOME 8 2");
            Receive("UNIT 0 101 7 -9 0 2");
            Receive("BUILDING 0 201 2 -9 8 0");
            Check(beforeUnit.SequenceEqual(ActiveMaterials(own)) && beforeHall.SequenceEqual(ActiveMaterials(hall)),
                "Repeated identities and snapshots do not allocate replacement materials");

            BeginWorld();
            Check(Units.LiveUnits.Count == 0 && Buildings.LiveBuildings.Count == 0, "Map reset removes old team visuals");
            Receive("UNIT 0 101 7 -10 0 1");
            Receive("BUILDING 0 201 1 -10 8 0");
            CheckColors(GetUnit(101), null, "Reset units do not retain old local team");
            CheckColors(GetBuilding(201), null, "Reset buildings do not retain old local team");
            Receive("WELCOME 7 1");
            CheckColors(GetUnit(101), TeamMaterials.FriendlyColor, "Reconnect applies fresh unit colors");
            CheckColors(GetBuilding(201), TeamMaterials.FriendlyColor, "Reconnect applies fresh building colors");
            InvokeMain("OnConnectionClosed", "Team color test disconnect");
            Check(Units.LiveUnits.Count == 0 && Buildings.LiveBuildings.Count == 0,
                "Disconnect clears both managers and their colored instances");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GD.Print("PASS: all unit/building team materials, untouched shared meshes and non-team surfaces, instance isolation, stable overrides, viewer-relative teams, late WELCOME, duplicate team changes, map reset and reconnect");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private void CheckAsset(string path)
    {
        PackedScene scene = GD.Load<PackedScene>(path);
        var friendly = scene.Instantiate<Node3D>();
        var enemy = scene.Instantiate<Node3D>();
        AddChild(friendly);
        AddChild(enemy);
        Surface[] first = ReadSurfaces(friendly).ToArray();
        Surface[] second = ReadSurfaces(enemy).ToArray();
        Check(first.Any(surface => IsTeamMaterial(surface.Source.ResourceName)), $"{path}: has marked team surfaces");
        Check(first.Any(surface => !IsTeamMaterial(surface.Source.ResourceName)), $"{path}: has non-team surfaces");
        Check(first.Length == second.Length && first.Zip(second).All(pair => pair.First.Mesh == pair.Second.Mesh),
            $"{path}: instances share model geometry");
        SetObjectTeam(friendly, 1);
        SetObjectTeam(enemy, 2);
        CheckColors(friendly, null, path + ": no local identity keeps authored colors");
        SetLocalTeam(friendly, 1);
        SetLocalTeam(enemy, 1);
        CheckColors(friendly, TeamMaterials.FriendlyColor, path);
        CheckColors(enemy, TeamMaterials.EnemyColor, path);
        foreach ((Surface a, Surface b) in first.Zip(second))
        {
            if (IsTeamMaterial(a.Source.ResourceName))
                Check(a.Instance.GetActiveMaterial(a.Index) != b.Instance.GetActiveMaterial(b.Index) &&
                    a.Instance.GetActiveMaterial(a.Index) != a.Source, $"{path}: team materials are instance-local");
            else
                Check(a.Instance.GetActiveMaterial(a.Index) == a.InitialMaterial &&
                    b.Instance.GetActiveMaterial(b.Index) == b.InitialMaterial,
                    $"{path}: skin, wood, metal and other unmarked surfaces remain unchanged");
        }

        Material[] originalOverrides = ActiveMaterials(friendly);
        SetObjectTeam(friendly, 1);
        SetLocalTeam(friendly, 1);
        Check(originalOverrides.SequenceEqual(ActiveMaterials(friendly)), $"{path}: unchanged teams retain material references");
        SetObjectTeam(friendly, 2);
        CheckColors(friendly, TeamMaterials.EnemyColor, path + ": team change");
        SetLocalTeam(friendly, 2);
        CheckColors(friendly, TeamMaterials.FriendlyColor, path + ": viewer change");
        CheckColors(enemy, TeamMaterials.EnemyColor, path + ": other instance remains independent");
        SetLocalTeam(friendly, 0);
        CheckColors(friendly, null, path + ": neutral viewer restores authored colors");
        foreach (Surface surface in first.Concat(second))
            Check(surface.Instance.Mesh == surface.Mesh &&
                surface.Mesh.SurfaceGetMaterial(surface.Index) == surface.Source &&
                surface.Source.AlbedoColor.IsEqualApprox(surface.SourceColor),
                $"{path}: shared mesh resources and original material colors are immutable");
        friendly.Free();
        enemy.Free();
    }

    private static void SetObjectTeam(Node3D node, uint team)
    {
        if (node is Unit unit) unit.Initialize(1, 7, team);
        else ((Building)node).ApplySnapshot(1, team, 0, 0, 0);
    }

    private static void SetLocalTeam(Node3D node, uint team)
    {
        if (node is Unit unit) unit.SetLocalTeam(team);
        else ((Building)node).SetLocalTeam(team);
    }

    private static void CheckColors(Node3D root, Color? palette, string context)
    {
        foreach (Surface surface in ReadSurfaces(root))
        {
            if (!IsTeamMaterial(surface.Source.ResourceName)) continue;
            Color expected = surface.SourceColor;
            if (palette.HasValue)
            {
                float shade = surface.Source.ResourceName switch { "roof" => .80f, "roof_mid" => .91f, _ => 1f };
                Color color = palette.Value;
                expected = new Color(color.R * shade, color.G * shade, color.B * shade, color.A);
            }
            var actual = surface.Instance.GetActiveMaterial(surface.Index) as StandardMaterial3D;
            Check(actual != null && actual.AlbedoColor.IsEqualApprox(expected),
                $"{context}: {root.Name}/{surface.Instance.Name}/{surface.Source.ResourceName} expected {expected}, got {actual?.AlbedoColor}");
        }
    }

    private static bool IsTeamMaterial(string name) => name is "team_cloth" or "banner" or "roof" or "roof_mid" or "roof_light";
    private static Material[] ActiveMaterials(Node3D root) => ReadSurfaces(root)
        .Select(surface => surface.Instance.GetActiveMaterial(surface.Index)).ToArray();

    private static IEnumerable<Surface> ReadSurfaces(Node node)
    {
        if (node is MeshInstance3D { Mesh: not null } mesh)
            for (int index = 0; index < mesh.Mesh.GetSurfaceCount(); index++)
                if (mesh.Mesh.SurfaceGetMaterial(index) is StandardMaterial3D source)
                    yield return new Surface(mesh, index, mesh.Mesh, source, source.AlbedoColor, mesh.GetActiveMaterial(index));
        foreach (Node child in node.GetChildren())
            foreach (Surface surface in ReadSurfaces(child)) yield return surface;
    }

    private IEnumerable<Node3D> LiveObjects() => Units.LiveUnits.Cast<Node3D>().Concat(Buildings.LiveBuildings);
    private Unit GetUnit(uint id) => Units.LiveUnits.Single(unit => unit.UnitId == id);
    private Building GetBuilding(uint id) => Buildings.LiveBuildings.Single(building => building.BuildingId == id);
    private void BeginWorld() { Receive($"MAP 2 {Map.MapHash}"); Receive("WORLD_READY"); }
    private void Receive(string message) => InvokeMain("OnMessage", message);
    private void InvokeMain(string name, params object[] args) => typeof(Main).GetMethod(name, PrivateInstance).Invoke(this, args);
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
