using Godot;
using System;

// F6: the tier-one melee and ranged minions beside their tier-two upgrades, front and back,
// ally and enemy colors, with one arc bounce lit.
// --capture saves docs/images/tier-two-minions.png.
public partial class TierTwoMinionsPreview : Node3D
{
    public override async void _Ready()
    {
        AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color("141e27"),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color("b8ccd8"), AmbientLightEnergy = .45f,
                TonemapMode = Godot.Environment.ToneMapper.Filmic, GlowEnabled = true
            }
        });
        AddChild(new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-48, -30, 0), LightEnergy = 1.1f, LightColor = new Color("fff0d8"),
            ShadowEnabled = true, DirectionalShadowMaxDistance = 40
        });
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-25, 145, 0), LightEnergy = .5f, LightColor = new Color("9fc8e9") });
        AddChild(new MeshInstance3D
        {
            Position = new Vector3(0, -.05f, 0),
            Mesh = new PlaneMesh { Size = new Vector2(200, 200), Material = new StandardMaterial3D { AlbedoColor = new Color("2b3a33"), Roughness = .95f } }
        });

        string[] scenes = { "MinionKnight", "MinionFurnace", "MinionWarden", "MinionArcher", "MinionArc" };
        string[] names = { "근접 미니언", "화로 미니언", "호위 미니언", "원거리 미니언", "전격 미니언" };
        const float spacing = 2.6f;
        uint id = 1;
        for (int column = 0; column < scenes.Length; column++)
            for (int row = 0; row < 2; row++)
            {
                // Front row: allies facing the camera. Back row: enemies turned to show their backs.
                var unit = GD.Load<PackedScene>($"res://units/{scenes[column]}.tscn").Instantiate<Unit>();
                unit.Initialize(id++, 0, row == 0 ? 1u : 2u);
                unit.SetLocalTeam(1);
                AddChild(unit);
                unit.ApplyServerPosition((column - 2) * spacing + (row == 0 ? -.45f : .45f), row == 0 ? 1.2f : -1.4f);
                unit.GetNode<Node3D>("Visual").RotationDegrees = new Vector3(0, row == 0 ? 180 + 20 : 30, 0);
            }

        var camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = 8.4f, Current = true };
        AddChild(camera);
        camera.LookAtFromPosition(new Vector3(0, 9, 10), new Vector3(0, .9f, 0), Vector3.Up);
        GetViewport().Msaa3D = Viewport.Msaa.Msaa4X;

        var canvas = new CanvasLayer();
        AddChild(canvas);
        for (int column = 0; column < names.Length; column++)
        {
            var label = new Label
            {
                Text = names[column], HorizontalAlignment = HorizontalAlignment.Center,
                Position = new Vector2(column * 230 + 15, 24), Size = new Vector2(200, 40)
            };
            label.AddThemeFontSizeOverride("font_size", 22);
            label.AddThemeColorOverride("font_color", new Color("f0e8d5"));
            canvas.AddChild(label);
        }

        if (!Array.Exists(OS.GetCmdlineUserArgs(), a => a == "--capture")) return;
        for (int i = 0; i < 30; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); // Let embers and smoke rise.
        // One bounce from in front of the arc minion to the side, as MINION_ARC would draw it.
        AddChild(new MinionArcBolt { From = new Vector3(2 * spacing - .4f, 0, 2.6f), To = new Vector3(2 * spacing + 1.6f, 0, 3.6f) });
        for (int i = 0; i < 8; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using Image image = GetViewport().GetTexture().GetImage();
        Error result = image.SavePng("res://docs/images/tier-two-minions.png");
        GD.Print($"Tier-two minions captured: {result}");
        GetTree().Quit(result == Error.Ok ? 0 : 1);
    }
}
