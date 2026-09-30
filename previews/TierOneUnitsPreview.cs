using Godot;
using System;
using System.Collections.Generic;

// F6: actual game assets at one camera scale. 1/2/3 views, T team, R turntable.
public partial class TierOneUnitsPreview : Node3D
{
    private readonly List<Unit> _models = new();
    private Camera3D _camera;
    private Control _layout;
    private bool _rotate, _enemy;

    public override async void _Ready()
    {
        AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color("141e27"),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color("b8ccd8"), AmbientLightEnergy = .4f,
                TonemapMode = Godot.Environment.ToneMapper.Filmic
            }
        });
        AddChild(new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-45, -32, 0), LightEnergy = 1.1f,
            LightColor = new Color("fff0d8"), ShadowEnabled = true,
            DirectionalShadowMaxDistance = 40
        });
        AddChild(new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-25, 145, 0), LightEnergy = .5f, LightColor = new Color("9fc8e9")
        });
        AddChild(new MeshInstance3D
        {
            Position = new Vector3(0, -.05f, 0),
            Mesh = new PlaneMesh { Size = new Vector2(200, 200), Material = Material("09131d") }
        });
        string[] paths = { "MinionKnight", "Knight", "Archer", "MinionArcher" };
        float[] xs = { 4.6f, 1.55f, -1.55f, -4.6f };
        for (int i = 0; i < paths.Length; i++)
        {
            bool rts = i is 1 or 2;
            AddChild(new MeshInstance3D
            {
                Position = new Vector3(xs[i], -.03f, 0),
                Mesh = new CylinderMesh
                {
                    TopRadius = rts ? 1.12f : .85f, BottomRadius = rts ? 1.15f : .88f,
                    Height = .06f, RadialSegments = 64, Material = Material(rts ? "586569" : "37464e")
                }
            });
            var unit = GD.Load<PackedScene>($"res://units/{paths[i]}.tscn").Instantiate<Unit>();
            unit.Initialize((uint)(i + 1), rts ? 7u : 0u, 1);
            unit.SetLocalTeam(1);
            AddChild(unit);
            unit.ApplyServerPosition(xs[i], 0);
            unit.GetNode<Node3D>("Visual").RotationDegrees = new Vector3(0, i < 2 ? -16 : 16, 0);
            _models.Add(unit);
        }
        _camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = 8.0f, Current = true };
        GetViewport().Msaa3D = Viewport.Msaa.Msaa4X;
        AddChild(_camera);
        SetView(2);
        CreateLayout();
        if (!Array.Exists(OS.GetCmdlineUserArgs(), a => a == "--capture")) return;
        for (int i = 0; i < 6; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using Image image = GetViewport().GetTexture().GetImage();
        Error result = image.SavePng("res://docs/images/tier-one-units.png");
        GD.Print($"Tier-one comparison captured: {result}");
        GetTree().Quit(result == Error.Ok ? 0 : 1);
    }

    private static StandardMaterial3D Material(string color) => new() { AlbedoColor = new Color(color), Roughness = .9f };

    private void CreateLayout()
    {
        var canvas = new CanvasLayer();
        AddChild(canvas);
        _layout = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        canvas.AddChild(_layout);
        Text("PROJECT W     /     BARRACKS", 64, 42, 650, 19, "9faeaf");
        Text("1티어 정규군", 60, 76, 1000, 48, "f0e8d5");
        Text("검방병과 궁수  ·  실제 게임 모델 / 동일한 카메라 배율", 64, 143, 1200, 20, "abb9bd");
        Line(64, 202, 1472, "4c5e65");
        Text("01 / SILHOUETTE & SCALE", 1190, 90, 346, 17, "c5aa79", HorizontalAlignment.Right);

        string[] names = { "근접 미니언", "검방병", "궁수", "원거리 미니언" };
        string[] roles = { "기존 라인 유닛", "검 · 방패 / 전열", "장궁 · 가죽 갑옷 / 후열", "기존 라인 유닛" };
        for (int i = 0; i < 4; i++)
        {
            float x = 64 + i * 368;
            bool rts = i is 1 or 2;
            Text(rts ? "RTS  /  TIER 01" : "MINION", x, 700, 368, 17, rts ? "c5aa79" : "8a9fa9", HorizontalAlignment.Center);
            Text(names[i], x, 730, 368, rts ? 32 : 25, rts ? "f0e8d5" : "b0bec3", HorizontalAlignment.Center);
            Text(roles[i], x, 779, 368, 18, "a8b9c0", HorizontalAlignment.Center);
        }
        Line(64, 852, 1472, "4c5e65");
        Text("크기 기준", 64, 880, 170, 20, "c5aa79");
        Text("미니언 1.00   →   RTS 약 1.30   →   AOS 영웅 1.65 (예정)", 240, 880, 1230, 24, "e1e6df");
        Text("1 정면   2 기본   3 게임 시점   ·   T 진영 색상   ·   R 회전", 64, 944, 1472, 17, "8da3ae");
    }

    private void Text(string text, float x, float y, float width, int size, string color, HorizontalAlignment alignment = HorizontalAlignment.Left)
    {
        var label = new Label { Text = text, Position = new Vector2(x, y), Size = new Vector2(width, 0), HorizontalAlignment = alignment };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", new Color(color));
        _layout.AddChild(label);
    }

    private void Line(float x, float y, float width, string color) => _layout.AddChild(new ColorRect
    {
        Position = new Vector2(x, y), Size = new Vector2(width, 1), Color = new Color(color)
    });

    public override void _Process(double delta)
    {
        if (_layout != null) _layout.Scale = GetViewport().GetVisibleRect().Size / new Vector2(1600, 1000);
        if (_rotate)
            foreach (Unit unit in _models) unit.GetNode<Node3D>("Visual").RotateY((float)delta * .55f);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.Keycode == Key.R) _rotate = !_rotate;
        if (key.Keycode is Key.Key1 or Key.Key2 or Key.Key3) SetView((int)key.Keycode - (int)Key.Key0);
        if (key.Keycode != Key.T) return;
        _enemy = !_enemy;
        foreach (Unit unit in _models) unit.Initialize(unit.UnitId, unit.OwnerId, _enemy ? 2u : 1u);
    }

    private void SetView(int view)
    {
        _camera.Position = new Vector3(0, view == 1 ? 3.7f : view == 3 ? 17 : 7, -16);
        _camera.LookAt(new Vector3(0, 1.25f, 0));
    }
}
