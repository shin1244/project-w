using Godot;

// Server-authorized hit presentation only: no damage, collision, lights or particles.
public partial class SiegeRamImpact : Node3D
{
    public uint Team { get; private set; }
    private const float Lifetime = .62f;
    private const int DebrisCount = 9;
    private readonly MeshInstance3D[] _debris = new MeshInstance3D[DebrisCount];
    private readonly Vector3[] _velocities = new Vector3[DebrisCount];
    private MeshInstance3D _ring;
    private StandardMaterial3D _ringMaterial, _iron, _brick;
    private float _age;
    private static BoxMesh _debrisMesh;
    private static TorusMesh _ringMesh;

    public static void Spawn(Node3D parent, Vector3 position, uint team)
    {
        if (!GodotObject.IsInstanceValid(parent) || !parent.IsInsideTree()) return;
        var effect = new SiegeRamImpact { Name = "SiegeRamImpact", Team = team };
        parent.AddChild(effect);
        effect.GlobalPosition = position + Vector3.Up * .055f;
    }

    public override void _Ready()
    {
        _debrisMesh ??= new BoxMesh { Size = new Vector3(.2f, .14f, .26f) };
        _ringMesh ??= new TorusMesh { InnerRadius = .88f, OuterRadius = 1, Rings = 32, RingSegments = 4 };
        _ringMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(.62f, .58f, .52f, .8f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };
        // The walker's ram knocks loose plate iron and brick, with an ash-grey dust ring.
        _iron = new StandardMaterial3D { AlbedoColor = new Color("3b434b"), Metallic = .45f, Roughness = .6f };
        _brick = new StandardMaterial3D { AlbedoColor = new Color("8a4a3a"), Roughness = .9f };
        _ring = new MeshInstance3D
        {
            Name = "ImpactRing", Mesh = _ringMesh, MaterialOverride = _ringMaterial,
            Scale = new Vector3(.35f, .25f, .35f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        AddChild(_ring);
        for (int i = 0; i < DebrisCount; i++)
        {
            float angle = i * Mathf.Tau / DebrisCount + .17f;
            float speed = 1.5f + (i % 3) * .5f;
            _velocities[i] = new Vector3(Mathf.Cos(angle) * speed, 2.4f + (i % 4) * .22f, Mathf.Sin(angle) * speed);
            var chunk = new MeshInstance3D
            {
                Name = "Debris" + i, Mesh = _debrisMesh, MaterialOverride = i % 3 == 0 ? _iron : _brick,
                Position = new Vector3(Mathf.Cos(angle) * .18f, .65f, Mathf.Sin(angle) * .18f),
                Rotation = new Vector3(angle, angle * .5f, angle * 1.7f),
                Scale = Vector3.One * (.7f + (i % 3) * .2f),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            _debris[i] = chunk;
            AddChild(chunk);
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _age += dt;
        float progress = Mathf.Clamp(_age / Lifetime, 0, 1);
        float radius = Mathf.Lerp(.35f, 2.3f, 1 - (1 - progress) * (1 - progress));
        _ring.Scale = new Vector3(radius, .25f, radius);
        _ringMaterial.AlbedoColor = new Color(.62f, .58f, .52f, .8f * (1 - progress));
        for (int i = 0; i < _debris.Length; i++)
        {
            _velocities[i] += Vector3.Down * 9 * dt;
            _debris[i].Position += _velocities[i] * dt;
            _debris[i].RotateObjectLocal(new Vector3(.6f, .8f, 0), dt * (4 + i % 3));
            _debris[i].Transparency = Mathf.Max(0, (progress - .55f) / .45f);
        }
        if (progress >= 1) QueueFree();
    }

    public override void _ExitTree()
    {
        _ringMaterial?.Dispose();
        _iron?.Dispose();
        _brick?.Dispose();
    }
}
