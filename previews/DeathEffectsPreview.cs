using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// F6: the same three units die side by side, the old way (every death falls over) in the back row and by
// the server's DEATH cause in the front row, cycling blade, shot, fire and siege shell.
// `-- --capture=DIR` records one pass of the four causes as JPGs in DIR/scene-7 for
// tools/pack-battle-gif.py (run with --fixed-fps 15, as the battle GIFs are).
public partial class DeathEffectsPreview : Node3D
{
    private static readonly string[] Victims = { "Worker", "Knight", "FlameAutomaton" };
    // Each pass lasts until its slowest death has finished, plus a short pause.
    private static readonly (DeathCause Cause, string Title, string Killers, float Seconds)[] Causes =
    {
        (DeathCause.Blade, "베기 · 부위 절단", "일꾼 · 스팀돌격병 · 근접 미니언\n늑대 · 사령관", 1.75f),
        (DeathCause.Shot, "사격 · 쓰러짐", "공압사수 · 포탑 · 치유사\n이동 모드 증기포격차", 1.75f),
        (DeathCause.Fire, "화염 · 재", "화염기계", 2.3f),
        (DeathCause.Blast, "포격 · 산산조각", "전개한 증기포격차 · 룬 골렘", 2.4f)
    };
    private const float RowGap = 3.2f, AliveSeconds = .5f;
    private readonly List<Unit> _units = new();
    private Label _currentTitle, _currentKillers;
    private float _clock;
    private int _cycle = -1;
    private bool _dead;
    private string _capture;
    private int _frame;

    public override void _Ready()
    {
        _capture = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--capture="))?[10..];
        AddChild(new WorldEnvironment { Environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color("1d262e"),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color("bacbd4"), AmbientLightEnergy = .55f,
            TonemapMode = Godot.Environment.ToneMapper.Filmic
        }});
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-55, -30, 0), LightEnergy = 1.25f,
            LightColor = new Color("ffe6c1"), ShadowEnabled = true, DirectionalShadowMaxDistance = 40 });
        AddChild(new MeshInstance3D { Position = new Vector3(0, -.01f, 0), Mesh = new PlaneMesh
            { Size = new Vector2(200, 200), Material = new StandardMaterial3D { AlbedoColor = new Color("3a4650") } }});
        var camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = 9.4f, Current = true };
        AddChild(camera);
        Vector3 focus = new(.6f, .9f, .4f);
        camera.Position = focus + new Basis(Vector3.Right, Mathf.DegToRad(-42)) * new Vector3(0, 0, 30);
        camera.LookAt(focus, Vector3.Up);
        GetViewport().Msaa3D = Viewport.Msaa.Msaa4X;

        var canvas = new CanvasLayer();
        AddChild(canvas);
        var layout = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Size = new Vector2(1280, 800) };
        canvas.AddChild(layout);
        Text(layout, "이전", 48, 40, 40, "f0e7d4");
        Text(layout, "어떤 공격에 죽어도\n옆으로 넘어짐", 48, 96, 20, "a5b4bb");
        _currentTitle = Text(layout, "", 48, 420, 40, "f0e7d4");
        _currentKillers = Text(layout, "", 48, 476, 20, "bd9c65");
        layout.AddChild(new ColorRect { Position = new Vector2(48, 392), Size = new Vector2(1184, 1), Color = new Color("4c5a61") });
    }

    private static Label Text(Control parent, string text, float x, float y, int size, string color)
    {
        var label = new Label { Text = text, Position = new Vector2(x, y) };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", new Color(color));
        parent.AddChild(label);
        return label;
    }

    public override void _Process(double delta)
    {
        _clock += (float)delta;
        if (_cycle < 0 || _clock >= Causes[_cycle % Causes.Length].Seconds) StartCycle();
        if (!_dead && _clock >= AliveSeconds)
        {
            _dead = true;
            DeathCause cause = Causes[_cycle % Causes.Length].Cause;
            for (int i = 0; i < _units.Count; i++)
            {
                // Back row: no DEATH line, as before the server named the weapon.
                if (i >= Victims.Length) _units[i].ApplyDeathCause(cause);
                _units[i].BeginDeath();
            }
        }
        if (_capture == null) return;
        if (_cycle >= Causes.Length) { GetTree().Quit(); return; }
        string folder = $"{_capture}/scene-7";
        DirAccess.MakeDirRecursiveAbsolute(folder);
        GetViewport().GetTexture().GetImage().SaveJpg($"{folder}/{_frame++:D4}.jpg", .92f);
    }

    private void StartCycle()
    {
        foreach (Unit unit in _units)
            if (IsInstanceValid(unit)) unit.QueueFree();
        _units.Clear();
        _cycle++;
        _clock = 0;
        _dead = false;
        var (_, title, killers, _) = Causes[_cycle % Causes.Length];
        _currentTitle.Text = $"현재 · {title}";
        _currentKillers.Text = killers;
        for (int row = 0; row < 2; row++)
            for (int i = 0; i < Victims.Length; i++)
            {
                var unit = GD.Load<PackedScene>($"res://units/{Victims[i]}.tscn").Instantiate<Unit>();
                // New IDs each pass, so the cut parts and flying directions vary from cycle to cycle.
                unit.Initialize((uint)(100 + _cycle * 10 + row * 5 + i), 7, 1);
                unit.SetLocalTeam(1);
                AddChild(unit);
                unit.ApplyServerPosition(-.6f + i * 3f, row == 0 ? -RowGap : RowGap);
                unit.GetNode<Node3D>("Visual").RotationDegrees = new Vector3(0, -25, 0);
                unit.ApplyServerState(new UnitState(UnitActivity.Idle, 0, 0, 0));
                _units.Add(unit);
            }
    }
}
