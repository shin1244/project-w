using Godot;
using System;
using System.Collections.Generic;

// F6: presentation-only samples using the live hero scenes; no server or game rules.
public partial class HeroVisualsPreview : Control
{
    private readonly List<Unit> _heroes = new();
    private Control _layout;
    private bool _walk, _enemy;
    private double _clock;
    private uint _swing;

    public override async void _Ready()
    {
        AddChild(new ColorRect { Color = new Color("101b28"), Size = new Vector2(1440, 900), MouseFilter = MouseFilterEnum.Ignore });
        _layout = new Control();
        AddChild(_layout);
        Label("PROJECT W / HERO PRESENTATION", new Vector2(28, 22), 26);
        Label("Space walk   A attack   Q/W/E/R skills   T team colors", new Vector2(28, 840), 21);
        // The ranger is left out until this preview supports its rig (the LeftArm/Bow lookup fails here).
        string[] scenes = { "HeroTest", "HeroGolem", "HeroMarshal" };
        string[] names = { "MUTANT WOLF", "RUNE GOLEM", "MARSHAL" };
        for (int i = 0; i < scenes.Length; i++)
        {
            Label(names[i], new Vector2(28 + i * 476, 76), 24);
            AddView(scenes[i], i);
        }
        var args = OS.GetCmdlineUserArgs();
        bool capture = Array.IndexOf(args, "--capture") >= 0;
        if (!capture) return;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        // Deterministic samples show the selected effects without changing game state.
        _heroes[0].WolfEffects = new WolfEffectSnapshot(1, true, 90);
        _heroes[1].GolemEffects = new GolemEffectSnapshot(2, true);
        _heroes[1].HealthBar.ApplyShield(25);
        Play(_heroes[0], 2);
        Play(_heroes[1], 1);
        Play(_heroes[2], 0);
        foreach (Unit hero in _heroes)
            foreach (Node child in hero.GetChildren())
                if (child is ISkillPresentation)
                {
                    child.SetProcess(false);
                    child._Process(.09);
                }
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using Image image = GetViewport().GetTexture().GetImage();
        Error result = image.SavePng("res://docs/images/hero-visuals.png");
        GD.Print($"Hero presentation capture: {result}");
        GetTree().Quit(result == Error.Ok ? 0 : 1);
    }

    private void AddView(string scene, int index)
    {
        var panel = new SubViewportContainer { Position = new Vector2(24 + index * 476, 120), Size = new Vector2(464, 680), Stretch = true };
        _layout.AddChild(panel);
        var view = new SubViewport { Size = new Vector2I(464, 680), OwnWorld3D = true,
            Msaa3D = Viewport.Msaa.Msaa4X, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        panel.AddChild(view);
        view.AddChild(new WorldEnvironment { Environment = new Godot.Environment {
            BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color("1b2b39"),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color("cadce7"), AmbientLightEnergy = .7f } });
        view.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-48, -35, 0),
            LightColor = new Color("fff0d5"), LightEnergy = 1.2f });
        view.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-25, 130, 0),
            LightColor = new Color("9acdea"), LightEnergy = .55f });
        view.AddChild(new MeshInstance3D { Position = Vector3.Down * .035f,
            Mesh = new PlaneMesh { Size = new Vector2(200, 200), Material = new StandardMaterial3D {
                AlbedoColor = new Color("21333e"), Roughness = .9f } } });
        Unit hero = GD.Load<PackedScene>($"res://units/{scene}.tscn").Instantiate<Unit>();
        hero.Initialize((uint)index + 1, 7, 1);
        hero.SetLocalTeam(1);
        view.AddChild(hero);
        hero.ApplyServerPosition(0, 0);
        hero.ApplyServerState(new UnitState(UnitActivity.Idle, 0, 0, 0));
        hero.HealthBar.Apply(100, 100);
        var camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal,
            Size = index == 0 ? 5.8f : 4.8f, Position = new Vector3(5, 4.2f, -7), Current = true };
        view.AddChild(camera);
        camera.LookAt(new Vector3(0, index == 2 ? 1.65f : 1.2f, 0));
        _heroes.Add(hero);
    }

    public override void _Process(double delta)
    {
        if (_layout == null) return;
        _layout.Scale = GetViewportRect().Size / new Vector2(1440, 900);
        _clock += delta;
        foreach (Unit hero in _heroes)
        {
            hero.EffectTick = (uint)(_clock * 30);
            if (_walk) hero.ApplyServerPosition(0, Mathf.Sin((float)_clock * 2) * .6f);
        }
    }

    private static void Play(Unit hero, int slot)
    {
        foreach (Node child in hero.GetChildren())
            if (child is ISkillPresentation presentation) presentation.PlaySkill(slot);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.Keycode == Key.Space) _walk = !_walk;
        if (key.Keycode == Key.T)
        {
            _enemy = !_enemy;
            foreach (Unit hero in _heroes) hero.Initialize(hero.UnitId, 7, _enemy ? 2u : 1u);
        }
        if (key.Keycode == Key.A)
        {
            _swing++;
            foreach (Unit hero in _heroes) hero.ApplyServerState(new UnitState(UnitActivity.Attack, 0, 0, _swing));
        }
        int slot = key.Keycode switch { Key.Q => 0, Key.W => 1, Key.E => 2, Key.R => 3, _ => -1 };
        if (slot < 0) return;
        foreach (Unit hero in _heroes)
        {
            uint until = hero.EffectTick + 90;
            hero.WolfEffects = new WolfEffectSnapshot(hero.UnitId, slot == 1, slot == 2 ? until : 0);
            hero.GolemEffects = new GolemEffectSnapshot(hero.UnitId, slot == 3);
            hero.MarshalEffects = new MarshalEffectSnapshot(hero.UnitId, slot == 2 ? until : 0, slot == 3 ? until : 0);
            hero.HealthBar.ApplyShield(slot == 1 ? 25 : 0);
            Play(hero, slot);
        }
    }

    private void Label(string text, Vector2 position, int size)
    {
        var label = new Godot.Label { Text = text, Position = position };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", new Color("e6dfcf"));
        _layout.AddChild(label);
    }
}
