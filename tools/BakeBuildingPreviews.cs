using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

// 개발 시 한 번 실행하는 투명 PNG 베이크. 플레이 중에는 SubViewport/건물/재질을 생성하지 않습니다.
public partial class BakeBuildingPreviews : Node
{
    public override async void _Ready()
    {
        try
        {
            const string directory = "res://ui/build-previews";
            const int pixels = 512;
            const float span = 8f;
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(directory));
            var main = GD.Load<PackedScene>("res://game/Main.tscn").Instantiate<Main>();
            Basis view = main.GetNode<Camera3D>("Camera3D").Basis;
            main.Free();
            var catalog = new List<object>();
            var views = new List<(SubViewport Viewport, string Path)>();
            foreach (string name in new[] { "Supply", "Barracks", "Store", "Forge", "Tower" })
            {
                var viewport = new SubViewport
                {
                    Size = new Vector2I(pixels, pixels), OwnWorld3D = true, TransparentBg = true,
                    GuiDisableInput = true, Msaa3D = Viewport.Msaa.Msaa4X,
                    RenderTargetUpdateMode = SubViewport.UpdateMode.Always
                };
                AddChild(viewport);
                var source = GD.Load<PackedScene>($"res://buildings/{name}.tscn").Instantiate<Building>();
                uint type = source.BuildingType;
                Vector2I footprint = source.GetMeta("footprint").AsVector2I();
                foreach (string childName in new[] { "Visual", "Turret" })
                {
                    Node3D child = source.GetNodeOrNull<Node3D>(childName);
                    if (child == null) continue;
                    ClearOwners(child);
                    source.RemoveChild(child);
                    viewport.AddChild(child);
                    child.Rotation = new Vector3(0, Building.ViewYaw(view), 0);
                }
                source.Free();
                viewport.AddChild(new WorldEnvironment
                {
                    Environment = new Godot.Environment
                    {
                        BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color(0, 0, 0, 0),
                        AmbientLightSource = Godot.Environment.AmbientSource.Color,
                        AmbientLightColor = new Color("d3e1eb"), AmbientLightEnergy = .8f
                    }
                });
                viewport.AddChild(new DirectionalLight3D
                {
                    RotationDegrees = new Vector3(-50, -35, 0), LightColor = new Color("fff0d5"), LightEnergy = 1.1f
                });
                var camera = new Camera3D
                {
                    Projection = Camera3D.ProjectionType.Orthogonal, Size = span, Current = true,
                    Transform = new Transform3D(view, Vector3.Up * 1.8f + view.Z * 20)
                };
                viewport.AddChild(camera);
                Vector2 anchor = camera.UnprojectPosition(Vector3.Zero) / pixels;
                string path = $"{directory}/{name.ToLowerInvariant()}.png";
                catalog.Add(new { Type = type, Texture = path, Footprint = new[] { footprint.X, footprint.Y },
                    WorldSpan = span, Anchor = new[] { anchor.X, anchor.Y } });
                views.Add((viewport, path));
            }
            for (int frame = 0; frame < 5; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            foreach (var item in views)
            {
                using Image image = item.Viewport.GetTexture().GetImage();
                if (image == null || image.IsEmpty() || image.SavePng(item.Path) != Error.Ok)
                    throw new InvalidOperationException($"Cannot save {item.Path}");
                GD.Print($"Saved placement image: {item.Path}");
            }
            using var file = Godot.FileAccess.Open($"{directory}/catalog.json", Godot.FileAccess.ModeFlags.Write);
            file.StoreString(JsonSerializer.Serialize(catalog, new JsonSerializerOptions { WriteIndented = true }));
            Callable.From(FinishBake).CallDeferred();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private static void ClearOwners(Node node)
    {
        node.Owner = null;
        foreach (Node child in node.GetChildren()) ClearOwners(child);
    }

    private void FinishBake()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GetTree().Quit();
    }
}
