using Godot;
using System.Collections.Generic;

// 개발자가 수동 실행하는 PNG 생성 도구입니다. 게임 씬에서는 참조하지 않습니다.
public partial class BakeSelectionPortraits : Node
{
    private readonly Dictionary<(string Path, bool Enemy), SubViewport> _cache = new();
    private readonly List<TeamMaterials> _materials = new();

    public override async void _Ready()
    {
        try
        {
            const string directory = "res://ui/portraits";
            Error result = DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(directory));
            if (result != Error.Ok) throw new System.InvalidOperationException($"Cannot create portrait directory: {result}");
            var portraits = new Dictionary<string, Texture2D>();
            bool soldiersOnly = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--soldiers-only") >= 0;
            bool defensesOnly = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--defenses-only") >= 0;
            bool inspectionOnly = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--inspection-only") >= 0;
            bool heroOnly = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--hero-only") >= 0;
            bool rangedOnly = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--ranged-only") >= 0;
            if (rangedOnly)
                foreach (bool enemy in new[] { false, true })
                {
                    portraits.Add("archer" + (enemy ? "-enemy" : ""), GetPortrait(GD.Load<PackedScene>("res://units/Archer.tscn"), enemy: enemy));
                    portraits.Add("minion-archer" + (enemy ? "-enemy" : ""), GetPortrait(GD.Load<PackedScene>("res://units/MinionArcher.tscn"), enemy: enemy));
                }
            if (!rangedOnly && (heroOnly || (!soldiersOnly && !defensesOnly && !inspectionOnly && System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--buildings-only") < 0)))
                foreach (bool enemy in new[] { false, true })
                    portraits.Add(enemy ? "mutant-wolf-enemy" : "mutant-wolf", GetPortrait(GD.Load<PackedScene>("res://units/HeroTest.tscn"), enemy: enemy));
            if (!rangedOnly && !heroOnly && !defensesOnly && !inspectionOnly && System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--buildings-only") < 0)
                foreach (string unit in soldiersOnly ? new[] { "Knight", "Archer" } : new[] { "Worker", "Knight", "Archer" })
                    portraits.Add(unit.ToLowerInvariant(), GetPortrait(GD.Load<PackedScene>($"res://units/{unit}.tscn")));
            if (!rangedOnly && !heroOnly && (inspectionOnly || (!soldiersOnly && !defensesOnly && System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--buildings-only") < 0)))
            {
                foreach (string unit in new[] { "Worker", "Knight", "Archer" })
                    portraits.Add(unit.ToLowerInvariant() + "-enemy", GetPortrait(GD.Load<PackedScene>($"res://units/{unit}.tscn"), enemy: true));
                foreach (string unit in new[] { "Knight", "Archer" })
                    foreach (bool enemy in new[] { false, true })
                        portraits.Add($"minion-{unit.ToLowerInvariant()}{(enemy ? "-enemy" : "")}", GetPortrait(GD.Load<PackedScene>($"res://units/Minion{unit}.tscn"), enemy: enemy));
            }
            foreach (string building in rangedOnly || heroOnly || soldiersOnly || inspectionOnly ? System.Array.Empty<string>() : defensesOnly ? new[] { "Fortress", "Tower" } : new[] { "TownHall", "Fortress", "Store", "Supply", "Barracks", "Forge", "Tower" })
                foreach (bool enemy in new[] { false, true })
                    portraits.Add($"{building.ToLowerInvariant()}-{(enemy ? "enemy" : "ally")}",
                        GetPortrait(GD.Load<PackedScene>($"res://buildings/{building}.tscn"), true, enemy));

            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            foreach ((string name, Texture2D texture) in portraits)
            {
                using Image image = texture.GetImage();
                string path = $"{directory}/{name}.png";
                if (image == null || image.IsEmpty() || image.SavePng(path) != Error.Ok)
                    throw new System.InvalidOperationException($"Cannot save portrait: {path}");
                GD.Print($"Saved portrait: {path}");
            }
            Callable.From(FinishBake).CallDeferred();
        }
        catch (System.Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    public Texture2D GetPortrait(PackedScene scene, bool building = false, bool enemy = false)
    {
        if (scene == null) return null;
        var key = (scene.ResourcePath, enemy);
        if (_cache.TryGetValue(key, out SubViewport cached)) return cached.GetTexture();

        var viewport = new SubViewport
        {
            Name = $"Portrait{_cache.Count}", Size = new Vector2I(192, 192),
            OwnWorld3D = true, TransparentBg = false,
            GuiDisableInput = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Once
        };
        AddChild(viewport);
        var model = new Node3D();
        viewport.AddChild(model);
        // 루트 스크립트, 충돌, 체력바, 애니메이션은 초상화에서 실행하지 않습니다.
        Node source = scene.Instantiate();
        float buildingHeight = building ? ((Building)source).HealthBarHeight : 0;
        float unitHeight = source is Unit unitSource ? unitSource.HealthBarHeight : 2.4f;
        foreach (string childName in new[] { "Visual", "Turret" })
        {
            Node3D visual = source.GetNodeOrNull<Node3D>(childName);
            if (visual == null) continue;
            ClearOwners(visual);
            source.RemoveChild(visual);
            model.AddChild(visual);
        }
        source.Free();
        var materials = new TeamMaterials(model);
        materials.Apply(enemy ? 2u : 1u, 1);
        _materials.Add(materials);

        viewport.AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color("101c26"),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color("b8d2e2"), AmbientLightEnergy = .75f,
                TonemapMode = Godot.Environment.ToneMapper.Filmic
            }
        });
        viewport.AddChild(new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-40, -35, 0),
            LightColor = new Color("fff0d5"), LightEnergy = 1.6f
        });
        bool beast = scene.ResourcePath == "res://units/HeroTest.tscn";
        var camera = new Camera3D
        {
            Projection = Camera3D.ProjectionType.Orthogonal, Current = true,
            Size = building ? buildingHeight * 1.5f : beast ? 3.6f : unitHeight * .94f,
            Position = building ? new Vector3(7, 6, -10) : beast ? new Vector3(4.8f, 3.1f, -5) : new Vector3(2.4f, 2.5f, -5)
        };
        viewport.AddChild(camera);
        camera.LookAt(beast ? new Vector3(.15f, 1.2f, -.3f) : new Vector3(0, building ? buildingHeight * .42f : unitHeight * .48f, 0));
        _cache.Add(key, viewport);
        return viewport.GetTexture();
    }

    private void FinishBake()
    {
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        GetTree().Quit();
    }

    private static void ClearOwners(Node node)
    {
        node.Owner = null;
        foreach (Node child in node.GetChildren()) ClearOwners(child);
    }

    public override void _ExitTree()
    {
        foreach (TeamMaterials materials in _materials) materials.Dispose();
        _materials.Clear();
        _cache.Clear();
    }
}
