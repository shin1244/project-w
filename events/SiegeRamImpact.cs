using Godot;

// Server-authorized hit presentation only: no damage, collision, lights or particles.
public partial class SiegeRamImpact : Node3D
{
    public uint Team { get; private set; }
    private const float Lifetime = .62f;
    private const int SplinterCount = 9;
    private readonly MeshInstance3D[] _splinters = new MeshInstance3D[SplinterCount];
    private readonly Vector3[] _velocities = new Vector3[SplinterCount];
    private MeshInstance3D _ring;
    private StandardMaterial3D _ringMaterial, _wood;
    private float _age;
    private static BoxMesh _splinterMesh;
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
        _splinterMesh ??= new BoxMesh { Size = new Vector3(.13f, .12f, .52f) };
        _ringMesh ??= new TorusMesh { InnerRadius = .88f, OuterRadius = 1, Rings = 32, RingSegments = 4 };
        _ringMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(.91f, .76f, .49f, .8f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };
        _wood = new StandardMaterial3D { AlbedoColor = new Color("a77a43"), Roughness = .95f };
        _ring = new MeshInstance3D
        {
            Name = "ImpactRing", Mesh = _ringMesh, MaterialOverride = _ringMaterial,
            Scale = new Vector3(.35f, .25f, .35f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        AddChild(_ring);
        for (int i = 0; i < SplinterCount; i++)
        {
            float angle = i * Mathf.Tau / SplinterCount + .17f;
            float speed = 1.5f + (i % 3) * .5f;
            _velocities[i] = new Vector3(Mathf.Cos(angle) * speed, 2.4f + (i % 4) * .22f, Mathf.Sin(angle) * speed);
            var splinter = new MeshInstance3D
            {
                Name = "WoodSplinter" + i, Mesh = _splinterMesh, MaterialOverride = _wood,
                Position = new Vector3(Mathf.Cos(angle) * .18f, .65f, Mathf.Sin(angle) * .18f),
                Rotation = new Vector3(angle, angle * .5f, angle * 1.7f),
                Scale = Vector3.One * (.7f + (i % 3) * .2f),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            _splinters[i] = splinter;
            AddChild(splinter);
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _age += dt;
        float progress = Mathf.Clamp(_age / Lifetime, 0, 1);
        float radius = Mathf.Lerp(.35f, 2.3f, 1 - (1 - progress) * (1 - progress));
        _ring.Scale = new Vector3(radius, .25f, radius);
        _ringMaterial.AlbedoColor = new Color(.91f, .76f, .49f, .8f * (1 - progress));
        for (int i = 0; i < _splinters.Length; i++)
        {
            _velocities[i] += Vector3.Down * 9 * dt;
            _splinters[i].Position += _velocities[i] * dt;
            _splinters[i].RotateObjectLocal(new Vector3(.6f, .8f, 0), dt * (4 + i % 3));
            _splinters[i].Transparency = Mathf.Max(0, (progress - .55f) / .45f);
        }
        if (progress >= 1) QueueFree();
    }

    public override void _ExitTree()
    {
        _ringMaterial?.Dispose();
        _wood?.Dispose();
    }
}
