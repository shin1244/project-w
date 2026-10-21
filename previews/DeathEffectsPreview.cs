using Godot;
using System.Collections.Generic;
using System.Linq;

// F6: the same four units die over and over, alternating the plain fall (no DEATH cause, as every death
// looked before) and a siege-shell blast (DEATH ... BLAST). No text, for the README GIFs.
// `-- --capture=DIR` records one fall as DIR/scene-7 and one blast as DIR/scene-8 as JPGs for
// tools/pack-battle-gif.py (run with --fixed-fps 15, as the battle GIFs are).
public partial class DeathEffectsPreview : Node3D
{
    private static readonly string[] Victims = { "Worker", "Knight", "Archer", "FlameAutomaton" };
    // Each pass lasts until its slowest death has finished, plus a short pause.
    private static readonly (DeathCause Cause, float Seconds)[] Passes = { (DeathCause.None, 1.9f), (DeathCause.Blast, 2.6f) };
    private const float AliveSeconds = .5f;
    private readonly List<Unit> _units = new();
    private float _clock;
    private int _pass = -1, _frame;
    private bool _dead;
    private string _capture;

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
        var camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = 6.2f, Current = true };
        AddChild(camera);
        Vector3 focus = new(0, 1.25f, 0);
        camera.Position = focus + new Basis(Vector3.Right, Mathf.DegToRad(-40)) * new Vector3(0, 0, 30);
        camera.LookAt(focus, Vector3.Up);
        GetViewport().Msaa3D = Viewport.Msaa.Msaa4X;
    }

    public override void _Process(double delta)
    {
        _clock += (float)delta;
        if (_pass < 0 || _clock >= Passes[_pass % Passes.Length].Seconds) StartPass();
        if (_capture != null && _pass >= Passes.Length) { GetTree().Quit(); return; }
        if (!_dead && _clock >= AliveSeconds)
        {
            _dead = true;
            foreach (Unit unit in _units)
            {
                unit.ApplyDeathCause(Passes[_pass % Passes.Length].Cause);
                unit.BeginDeath();
            }
        }
        if (_capture == null) return;
        string folder = $"{_capture}/scene-{7 + _pass}";
        DirAccess.MakeDirRecursiveAbsolute(folder);
        GetViewport().GetTexture().GetImage().SaveJpg($"{folder}/{_frame++:D4}.jpg", .92f);
    }

    private void StartPass()
    {
        foreach (Unit unit in _units)
            if (IsInstanceValid(unit)) unit.QueueFree();
        _units.Clear();
        _pass++;
        _clock = 0;
        _frame = 0;
        _dead = false;
        for (int i = 0; i < Victims.Length; i++)
        {
            var unit = GD.Load<PackedScene>($"res://units/{Victims[i]}.tscn").Instantiate<Unit>();
            // New IDs each pass, so the flying directions vary from pass to pass.
            unit.Initialize((uint)(100 + _pass * 10 + i), 7, 1);
            unit.SetLocalTeam(1);
            AddChild(unit);
            unit.ApplyServerPosition(-4f + i * 2.3f, 0);
            unit.GetNode<Node3D>("Visual").RotationDegrees = new Vector3(0, -25, 0);
            unit.ApplyServerState(new UnitState(UnitActivity.Idle, 0, 0, 0));
            _units.Add(unit);
        }
    }
}
