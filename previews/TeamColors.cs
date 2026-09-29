using Godot;
using System;
using System.Linq;

// 실제 유닛과 건물에 같은 서버 진영 정보를 적용하는 오프라인 재질 미리보기.
public partial class TeamColors : Control
{
    public override async void _Ready()
    {
        try
        {
            var background = new ColorRect { Color = new Color("132029"), MouseFilter = MouseFilterEnum.Ignore };
            AddChild(background);
            background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(Label("진영별 색상", 32, new Color("edf0e4"), new Vector2(34, 22)));
            AddChild(Label("의상 · 방패 · 지붕 · 깃발", 18, new Color("aab9bd"), new Vector2(36, 67)));
            var row = new HBoxContainer();
            AddChild(row);
            row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            row.OffsetLeft = 22;
            row.OffsetRight = -22;
            row.OffsetTop = 112;
            row.OffsetBottom = -52;
            row.AddThemeConstantOverride("separation", 16);
            AddTeam(row, 2, "아군", "옅은 파란색", TeamMaterials.FriendlyColor);
            AddTeam(row, 1, "적군", "옅은 붉은색", TeamMaterials.EnemyColor);
            var footer = Label("같은 모델에서 팀 표시용 재질의 색상만 변경합니다.", 17, new Color("aab9bd"), Vector2.Zero);
            AddChild(footer);
            footer.AnchorTop = footer.AnchorBottom = 1;
            footer.OffsetLeft = 34;
            footer.OffsetTop = -36;
            footer.OffsetRight = 1500;
            footer.OffsetBottom = -10;
            if (!OS.GetCmdlineUserArgs().Contains("--capture")) return;
            for (int i = 0; i < 10; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using Image screenshot = GetViewport().GetTexture().GetImage();
            Error error = screenshot.SavePng("res://docs/images/team-colors-preview.png");
            GetTree().Quit((int)error);
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private void AddTeam(HBoxContainer row, uint team, string title, string subtitle, Color color)
    {
        var card = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        card.AddThemeConstantOverride("separation", 0);
        row.AddChild(card);
        var header = new PanelContainer { CustomMinimumSize = new Vector2(0, 84) };
        header.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("20313c"), BorderColor = color, BorderWidthTop = 3,
            ContentMarginLeft = 24, ContentMarginRight = 24, ContentMarginTop = 14, ContentMarginBottom = 14
        });
        card.AddChild(header);
        var names = new HBoxContainer();
        header.AddChild(names);
        var name = Label(title, 29, color, Vector2.Zero);
        name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        names.AddChild(name);
        var note = Label(subtitle, 18, new Color("d3dce0"), Vector2.Zero);
        note.VerticalAlignment = VerticalAlignment.Center;
        names.AddChild(note);
        var container = new SubViewportContainer { Stretch = true, SizeFlagsVertical = SizeFlags.ExpandFill };
        card.AddChild(container);
        var viewport = new SubViewport
        {
            OwnWorld3D = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always, Msaa3D = Viewport.Msaa.Msaa4X
        };
        container.AddChild(viewport);
        var world = new Node3D();
        viewport.AddChild(world);
        world.AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color("202b2c"),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color("ccddeb"), AmbientLightEnergy = .55f,
                AmbientLightSkyContribution = 0, TonemapMode = Godot.Environment.ToneMapper.Filmic
            }
        });
        world.AddChild(new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(100, 100) }, Position = new Vector3(0, -.015f, 0),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("37454a"), Roughness = 1 }
        });
        world.AddChild(new DirectionalLight3D
        {
            Rotation = new Vector3(-.9f, -.55f, 0), LightColor = new Color("fff0d9"), LightEnergy = 1.1f,
            ShadowEnabled = true, DirectionalShadowMaxDistance = 30,
            DirectionalShadowMode = DirectionalLight3D.ShadowMode.Orthogonal
        });
        world.AddChild(new DirectionalLight3D
        {
            Rotation = new Vector3(-.45f, 2, 0), LightColor = new Color("b8d6ff"), LightEnergy = .3f
        });
        var camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = 8.0f, Current = true, Far = 80 };
        world.AddChild(camera);
        camera.Position = new Vector3(5.8f, 8.2f, -13.5f);
        camera.LookAt(new Vector3(0, 1.0f, -.45f));
        AddBuilding(world, "Supply", new Vector3(-1.9f, 0, .85f), team);
        AddBuilding(world, "Barracks", new Vector3(1.1f, 0, .85f), team);
        string[] units = { "Worker", "Knight", "Archer" };
        for (int i = 0; i < units.Length; i++)
        {
            Unit unit = GD.Load<PackedScene>($"res://units/{units[i]}.tscn").Instantiate<Unit>();
            unit.Initialize((uint)i + 100, i == 1 ? 0u : 7u, team);
            unit.SetLocalTeam(2);
            world.AddChild(unit);
            unit.Position = new Vector3(-1.7f + i * 1.75f, 0, -2.1f);
            var nameTag = new Label3D
            {
                Text = new[] { "일꾼", "기사", "궁수" }[i], FontSize = 38, PixelSize = .008f,
                Modulate = new Color("e4e9e3"), OutlineSize = 6,
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                Position = unit.Position + new Vector3(0, .06f, -.62f)
            };
            world.AddChild(nameTag);
        }
    }

    private static void AddBuilding(Node3D world, string name, Vector3 position, uint team)
    {
        Building building = GD.Load<PackedScene>($"res://buildings/{name}.tscn").Instantiate<Building>();
        building.SetLocalTeam(2);
        world.AddChild(building);
        building.ApplySnapshot(name == "Supply" ? 201u : 202u, team, position.X, position.Z, 0);
    }

    private static Label Label(string text, int fontSize, Color color, Vector2 position)
    {
        var label = new Label { Text = text, Position = position, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }
}
