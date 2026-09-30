using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

// 실제 서버 수신 순서와 공통 공사 표시, 완성·파괴·방어 효과 차단을 독립적으로 검증합니다.
public partial class ConstructionSiteChecks : Node3D
{
    private BuildingManager _buildings;
    private Camera3D _camera;

    public override async void _Ready()
    {
        try
        {
            _camera = new Camera3D
            {
                Current = true, Projection = Camera3D.ProjectionType.Orthogonal, Size = 80,
                Position = new Vector3(15, 25, 25)
            };
            AddChild(_camera);
            _camera.LookAt(new Vector3(15, 0, 0));
            _buildings = new BuildingManager
            {
                TownHallScene = Load("TownHall"), FortressScene = Load("Fortress"),
                StoreScene = Load("Store"), SupplyScene = Load("Supply"), BarracksScene = Load("Barracks"),
                ForgeScene = Load("Forge"), TowerScene = Load("Tower")
            };
            AddChild(_buildings);
            CheckProtocolAndVisuals();
            CheckDefense(1, 701);
            CheckDefense(6, 706);
            _buildings.Clear();
            Check(_buildings.LiveBuildings.Count == 0 && _buildings.GetChildCount() == 0 && _buildings.SelectedBuilding == null,
                "Reconnect reset immediately clears sites and selection");
            _buildings.HandleSpawn("BUILDING 6 706 1 0 0 0".Split(' '));
            Building reset = _buildings.GetNode<Building>("Building_706");
            Check(!reset.IsUnderConstruction && !reset.HasServerState && !reset.HasNode("ConstructionSite"),
                "A reused ID begins complete with a fresh state baseline");
            _buildings.Clear();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (OS.GetCmdlineUserArgs().Contains("--capture")) await CapturePreview();
            GD.Print("PASS: common construction sites without world text, exact integer progress, enlarged axis-aligned footprints, immutable shared models, lowered selection/HP anchors, completion/removal/reconnect, and fortress/tower shot suppression");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private void CheckProtocolAndVisuals()
    {
        // 접속 시에는 모든 BUILDING이 먼저 오고 CONSTRUCTION이 이어집니다.
        for (uint type = 0; type <= 6; type++)
        {
            _buildings.HandleSpawn($"BUILDING {type} {100 + type} 1 {type * 5} 0 0".Split(' '));
            Check(_buildings.TryGetBuilding(100 + type, out Building mapped) && mapped.BuildingType == type,
                $"Building type {type} maps to the synchronized scene");
        }
        Building site = _buildings.GetNode<Building>("Building_104");
        var visual = site.GetNode<MeshInstance3D>("Visual");
        Mesh sourceMesh = visual.Mesh;
        var collider = site.GetNode<CollisionShape3D>("SelectionArea/CollisionShape3D");
        var originalShape = (BoxShape3D)collider.Shape;
        Vector3 sourceShapeSize = originalShape.Size;
        Transform3D sourceTransform = collider.Transform;
        int selectedChanges = 0;
        _buildings.SelectionChanged += () => selectedChanges++;
        _buildings.SelectSingle(site);
        site.ApplyHealth(new HealthSnapshot(104, 240, 800));
        int before = selectedChanges;
        uint originalLayout = _buildings.LayoutVersion;
        _buildings.HandleConstruction("CONSTRUCTION 104 0".Split(' '));
        var progress = site.GetNode<ConstructionSite>("ConstructionSite");
        Check(site.IsUnderConstruction && site.ConstructionPercent == 0 && !visual.Visible && progress.Visible && progress.Percent == 0,
            "Zero-percent snapshot replaces the finished visual with the common site");
        Check(selectedChanges == before + 1 && _buildings.LayoutVersion == originalLayout,
            "Construction updates selected details without changing occupied layout");
        Check(collider.Shape != originalShape && originalShape.Size == sourceShapeSize && visual.Mesh == sourceMesh,
            "Construction uses a private selection box without editing the shared finished model or shape");
        var box = (BoxShape3D)collider.Shape;
        Check(box.Size == new Vector3(5, ConstructionSite.SelectionHeight, 3) &&
            collider.Position.IsEqualApprox(Vector3.Up * (ConstructionSite.SelectionHeight * .5f)) && !collider.Disabled,
            "Unfinished 5x3 building keeps a selectable, low, exact footprint");
        Check(progress.HasNode("Sign/Board") && !ContainsWorldText(progress),
            "Construction keeps its center sign and border without any world-space text");
        Check(site.GetNode<MeshInstance3D>("SelectionRing").Visible, "Existing building selection remains visible");
        CheckHealthPosition(site, ConstructionSite.HealthHeight, "Construction HP anchor follows the low sign instead of the hidden completed roof");

        _buildings.HandleConstruction("CONSTRUCTION 102 37".Split(' '));
        var otherSite = _buildings.GetNode<Building>("Building_102").GetNode<ConstructionSite>("ConstructionSite");
        Check(progress.GetNode<MeshInstance3D>("BorderX-1").Mesh == otherSite.GetNode<MeshInstance3D>("BorderX-1").Mesh &&
            progress.GetNode<MeshInstance3D>("BorderX-1").MaterialOverride == otherSite.GetNode<MeshInstance3D>("BorderX-1").MaterialOverride,
            "Different building types reuse common construction geometry and materials");
        Check(progress.Footprint == new Vector2I(5, 3) && otherSite.Footprint == new Vector2I(3, 3),
            "Common site scales to each model footprint");

        foreach (string bad in new[] { "CONSTRUCTION 104 -1", "CONSTRUCTION 104 101", "CONSTRUCTION 104 12.5",
            "CONSTRUCTION 104 +25", "CONSTRUCTION 104 NaN", "CONSTRUCTION 0 30", "CONSTRUCTION 999 30",
            "CONSTRUCTION bad 30", "CONSTRUCTION 104", "CONSTRUCTION 104 30 extra", "CONSTRUCTION 104 0" })
            _buildings.HandleConstruction(bad.Split(' '));
        Check(site.ConstructionPercent == 0 && selectedChanges == before + 1,
            "Invalid and repeated progress do not mutate state or refresh selected details");

        _buildings.HandleConstruction("CONSTRUCTION 104 48".Split(' '));
        _buildings.HandleSpawn("BUILDING 4 104 1 5 7 1.5707963".Split(' '));
        Check(site.GetNode<ConstructionSite>("ConstructionSite") == progress && progress.Percent == 48 &&
            site.ConstructionPercent == 48 && !visual.Visible, "Position/yaw snapshots preserve current construction and reuse its nodes");
        Vector2[] footprint = BuildingFootprint.Corners(site);
        Rect2 bounds = BuildingFootprint.Bounds(footprint);
        Check(bounds.Size.IsEqualApprox(new Vector2(5, 3)) && site.GlobalRotation.IsEqualApprox(Vector3.Zero),
            "Five-by-three construction occupancy remains axis aligned despite server facing yaw");
        Vector3 expectedEdge = site.GlobalPosition + new Vector3(0, .105f, -(3 - .075f) * .5f);
        Check(progress.GetNode<MeshInstance3D>("BorderX-1").GlobalPosition.IsEqualApprox(expectedEdge),
            "Construction boundary follows the same axis-aligned footprint as server collision checks");
        Check(!ContainsWorldText(progress), "Progress updates never recreate removed construction text");
        site.ApplyConstruction(-2);
        site.ApplyConstruction(102);
        Check(site.ConstructionPercent == 48, "Direct invalid construction values are rejected too");
        _buildings.HandleConstruction("CONSTRUCTION 104 100".Split(' '));
        Check(!site.IsUnderConstruction && visual.Visible && !progress.Visible && !progress.IsProcessing() &&
            collider.Shape == originalShape && collider.Transform.IsEqualApprox(sourceTransform),
            "Completion restores the original mesh visibility and exact finished selection shape");
        CheckHealthPosition(site, site.HealthBarHeight, "Completed HP anchor returns above the actual roof");
        _buildings.HandleConstruction("CONSTRUCTION 104 24".Split(' '));
        Check(site.GetNode<ConstructionSite>("ConstructionSite") == progress && progress.Percent == 24,
            "A later authoritative progress value reuses the same site without interpolation");
        _buildings.HandleRemove("REMOVE 104".Split(' '));
        _buildings.HandleConstruction("CONSTRUCTION 104 100".Split(' '));
        Check(!_buildings.TryGetBuilding(104, out _) && _buildings.SelectedBuilding == null && site.IsQueuedForDeletion(),
            "Destroying a site immediately clears lookup/selection and ignores stale progress");
    }

    private void CheckDefense(uint type, uint id)
    {
        _buildings.HandleSpawn($"BUILDING {type} {id} 1 0 0 0".Split(' '));
        Building defense = _buildings.GetNode<Building>($"Building_{id}");
        var target = new Node3D { Position = new Vector3(4, 0, -6) };
        AddChild(target);
        defense.ResolveFocus = _ => target;
        var turret = defense.GetNode<Node3D>("Turret");
        var ballista = defense.GetNode<Node3D>("Turret/Ballista");
        Vector3 rest = ballista.Position;
        int shots = 0;
        defense.StateChanged += (_, fired) => { if (fired) shots++; };
        defense.ApplyServerState(new UnitState(UnitActivity.Attack, 0, 900, 8));
        defense.ApplyServerState(new UnitState(UnitActivity.Attack, 0, 900, 9));
        Check(shots == 1 && defense.HasNode("ShotTrace") && !ballista.Position.IsEqualApprox(rest),
            $"Defense {type} starts with a live shot effect");
        defense.ApplyConstruction(0);
        Check(!turret.Visible && !defense.HasNode("ShotTrace") && ballista.Position.IsEqualApprox(rest),
            $"Defense {type} immediately removes active recoil/trace and stops aim when construction begins");
        Basis facing = turret.GlobalBasis;
        target.Position = new Vector3(-6, 0, 4);
        defense.ApplyServerState(new UnitState(UnitActivity.Attack, 0, 900, 17));
        defense.ApplyServerState(new UnitState(UnitActivity.Build, 0, 900, 200));
        defense._Process(0);
        Check(shots == 1 && defense.State.SwingSequence == 17 && !defense.HasNode("ShotTrace") &&
            turret.GlobalBasis.IsEqualApprox(facing) && ballista.Position.IsEqualApprox(rest),
            $"Defense {type} records construction-time sequence without aiming or firing");
        defense.ApplyConstruction(100);
        defense.ApplyServerState(new UnitState(UnitActivity.Attack, 0, 900, 17));
        Check(shots == 1 && turret.Visible && !defense.HasNode("ShotTrace"),
            $"Defense {type} completion does not replay historical construction-time shots");
        defense.ApplyServerState(new UnitState(UnitActivity.Attack, 0, 900, 18));
        Check(shots == 2 && defense.HasNode("ShotTrace"), $"Defense {type} resumes only on a new server shot");
        defense.ApplyConstruction(50);
        _buildings.HandleRemove($"REMOVE {id}".Split(' '));
        Check(!_buildings.TryGetBuilding(id, out _) && defense.IsQueuedForDeletion(),
            $"Defense {type} removal discards its site and attack state");
        target.QueueFree();
    }

    private void CheckHealthPosition(Building building, float height, string message)
    {
        Vector2 expected = (_camera.UnprojectPosition(building.GlobalPosition + Vector3.Up * height)
            - new Vector2(building.HealthBar.Size.X * .5f, building.HealthBar.Size.Y)).Round();
        Check(building.HealthBar.Visible && building.HealthBar.Position.IsEqualApprox(expected), message);
    }

    private async Task CapturePreview()
    {
        var camera = new Camera3D
        {
                Current = true, Projection = Camera3D.ProjectionType.Orthogonal, Size = 12,
            Position = new Vector3(0, 13, 12)
        };
        AddChild(camera);
        camera.LookAt(new Vector3(0, .5f, 0));
        AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color("182329"),
                AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = Colors.White, AmbientLightEnergy = .65f
            }
        });
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-55, -30, 0), LightEnergy = 1.1f });
        AddChild(new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(30, 20) },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("536356") }
        });
        _buildings.HandleSpawn("BUILDING 2 902 1 -5 0 0".Split(' '));
        _buildings.HandleSpawn("BUILDING 4 904 1 0 0 0.35".Split(' '));
        _buildings.HandleSpawn("BUILDING 6 906 1 5 0 0".Split(' '));
        _buildings.HandleConstruction("CONSTRUCTION 902 0".Split(' '));
        _buildings.HandleConstruction("CONSTRUCTION 904 48".Split(' '));
        _buildings.HandleConstruction("CONSTRUCTION 906 82".Split(' '));
        for (int frame = 0; frame < 4; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng("res://.godot/construction-sites.png");
    }

    private static PackedScene Load(string name) => GD.Load<PackedScene>($"res://buildings/{name}.tscn");
    private static bool ContainsWorldText(Node node)
    {
        foreach (Node child in node.GetChildren())
            if (child is Label3D || ContainsWorldText(child)) return true;
        return false;
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
