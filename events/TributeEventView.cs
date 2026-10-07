using Godot;
using System;

// One public objective: all animation is cosmetic; the server owns collection and timing.
public partial class TributeEventView : Node3D
{
    public const uint PickLayer = 1u << 4;
    public uint EventId { get; private set; }
    public bool Active => _snapshot?.Active == true;
    public float ChannelProgress => _snapshot?.Progress(_tick) ?? 0;

    private TributeSnapshot _snapshot;
    private uint _tick, _localTeam;
    private Area3D _pickArea;
    private Node3D _relic;
    private Label3D _label;
    private MeshInstance3D _progress;
    private StandardMaterial3D _progressMaterial;
    private int _arcSteps = -1;
    private double _animationTime;
    private static readonly Color Amber = new("e5bc69");

    public override void _Ready()
    {
        var stone = Material("696966");
        var edge = Material("aaa48b");
        AddMesh("Foundation", new CylinderMesh { BottomRadius = 1.35f, TopRadius = 1.25f,
            Height = .19f, RadialSegments = 8, Rings = 1 }, stone, new Vector3(0, .12f, 0));
        AddMesh("Plinth", new CylinderMesh { BottomRadius = .99f, TopRadius = .88f,
            Height = .32f, RadialSegments = 8, Rings = 1 }, stone, new Vector3(0, .37f, 0));
        AddMesh("Coping", new CylinderMesh { BottomRadius = 1.06f, TopRadius = 1.02f,
            Height = .13f, RadialSegments = 8, Rings = 1 }, edge, new Vector3(0, .59f, 0));

        // Flat strips use only a few hundred triangles and do not cast extra shadows.
        AddMesh("NeutralHalo", RingMesh(1.67f, 1.73f, 64), Unlit(new Color("a08b63")),
            new Vector3(0, .045f, 0), false);
        AddMesh("AltarInlay", RingMesh(.71f, .77f, 32, 32), Unlit(Amber),
            new Vector3(0, .666f, 0), false);
        _progressMaterial = Unlit(Amber);
        _progress = AddMesh("ChannelArc", RingMesh(1.78f, 1.93f, 0), _progressMaterial,
            new Vector3(0, .055f, 0), false);
        _progress.Visible = false;

        _relic = new Node3D { Name = "Relic", Position = new Vector3(0, 1.45f, 0) };
        AddChild(_relic);
        var crystal = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true, AlbedoColor = Colors.White,
            Roughness = .48f, Metallic = .12f, EmissionEnabled = true,
            Emission = new Color("32233d"), EmissionEnergyMultiplier = .35f
        };
        var relicMesh = new MeshInstance3D { Name = "FacetedOffering", Mesh = RelicMesh(), MaterialOverride = crystal };
        _relic.AddChild(relicMesh);

        _label = new Label3D
        {
            Name = "CollectionLabel", Position = new Vector3(0, 3.25f, 0),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 38,
            PixelSize = .012f, OutlineSize = 8, Modulate = Amber,
            OutlineModulate = new Color("151821"), NoDepthTest = true,
            Shaded = false
        };
        AddChild(_label);
        _pickArea = new Area3D
        {
            Name = "TributePickArea", CollisionLayer = 0, CollisionMask = 0,
            Monitoring = false, Monitorable = false, InputRayPickable = false
        };
        _pickArea.AddChild(new CollisionShape3D
        {
            Name = "PickShape", Position = new Vector3(0, 1.3f, 0),
            Shape = new CylinderShape3D { Radius = 1.6f, Height = 2.6f }
        });
        AddChild(_pickArea);
        Visible = Active;
        Refresh();
    }

    public void Apply(TributeSnapshot snapshot, uint currentTick, uint localTeam)
    {
        _snapshot = snapshot;
        _tick = currentTick;
        _localTeam = localTeam;
        EventId = snapshot.EventId;
        Position = snapshot.Position;
        Visible = snapshot.Active;
        Refresh();
    }

    public void SetTick(uint tick)
    {
        _tick = tick;
        Refresh();
    }

    public void Reset()
    {
        _snapshot = null; EventId = 0; _tick = 0; _animationTime = 0;
        Visible = false;
        Refresh();
    }

    public override void _Process(double delta)
    {
        if (!Visible || _relic == null) return;
        _animationTime += delta;
        _relic.Position = new Vector3(0, 1.45f + Mathf.Sin((float)_animationTime * 1.6f) * .09f, 0);
        _relic.Rotation = new Vector3(0, Mathf.Sin((float)_animationTime * .45f) * .15f, 0);
    }

    private void Refresh()
    {
        if (_pickArea == null) return;
        _pickArea.CollisionLayer = Active ? PickLayer : 0;
        if (!Active) { _progress.Visible = false; return; }
        bool channeling = _snapshot.Channeling;
        _label.Text = channeling
            ? $"{_snapshot.CapturerTeam}팀 · {_snapshot.RemainingSeconds(_tick):0.0}초"
            : "공물";
        Color color = channeling ? TeamColor(_snapshot.CapturerTeam) : Amber;
        _label.Modulate = color;
        _progressMaterial.AlbedoColor = color;
        _progress.Visible = channeling;
        int steps = channeling ? Math.Clamp((int)Math.Ceiling(ChannelProgress * 64), 0, 64) : 0;
        if (steps != _arcSteps)
        {
            _arcSteps = steps;
            _progress.Mesh = RingMesh(1.78f, 1.93f, steps);
        }
    }

    private Color TeamColor(uint team) => _localTeam is 1 or 2
        ? team == _localTeam ? Minimap.AllyColor : Minimap.EnemyColor
        : team == 1 ? new Color("80bce7") : new Color("efae77");

    private MeshInstance3D AddMesh(string name, Mesh mesh, Material material, Vector3 position, bool shadow = true)
    {
        var instance = new MeshInstance3D { Name = name, Mesh = mesh, MaterialOverride = material, Position = position,
            CastShadow = shadow ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(instance);
        return instance;
    }

    private static StandardMaterial3D Material(string color) => new()
    {
        AlbedoColor = new Color(color), Roughness = .93f
    };

    private static StandardMaterial3D Unlit(Color color) => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = color,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled
    };

    private static ArrayMesh RingMesh(float inner, float outer, int segments, int fullCircleSegments = 64)
    {
        var mesh = new ArrayMesh();
        if (segments <= 0) return mesh;
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i < segments; i++)
        {
            float a = -Mathf.Pi / 2 + Mathf.Tau * i / fullCircleSegments;
            float b = -Mathf.Pi / 2 + Mathf.Tau * (i + 1) / fullCircleSegments;
            Vector3 p = new(Mathf.Cos(a), 0, Mathf.Sin(a)), q = new(Mathf.Cos(b), 0, Mathf.Sin(b));
            Triangle(surface, p * inner, q * inner, q * outer, Colors.White);
            Triangle(surface, p * inner, q * outer, p * outer, Colors.White);
        }
        surface.Commit(mesh);
        return mesh;
    }

    private static ArrayMesh RelicMesh()
    {
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        Shard(surface, Vector3.Zero, new Vector3(.52f, 1.72f, .43f), 0, new Color("a67bba"));
        for (int side = -1; side <= 1; side += 2)
        {
            Shard(surface, new Vector3(side * .62f, -.14f, .02f), new Vector3(.31f, 1.10f, .25f), side * -.62f, new Color("504065"));
            Shard(surface, new Vector3(side * 1.05f, .19f, .03f), new Vector3(.22f, .98f, .19f), side * -.94f, new Color("746080"));
            Shard(surface, new Vector3(side * 1.22f, .51f, .03f), new Vector3(.14f, .63f, .15f), side * -.81f, new Color("ad986d"));
        }
        Shard(surface, new Vector3(0, -.02f, .46f), new Vector3(.10f, .56f, .065f), 0, Amber);
        return surface.Commit();
    }

    private static void Shard(SurfaceTool surface, Vector3 center, Vector3 scale, float tilt, Color color)
    {
        var basis = new Basis(Vector3.Back, tilt);
        Vector3 Point(Vector3 p) => center + basis * (p * scale);
        Vector3 top = Point(new Vector3(0, .65f, 0)), bottom = Point(new Vector3(0, -.50f, 0));
        Vector3[] rim = { Point(Vector3.Right), Point(Vector3.Back), Point(Vector3.Left), Point(Vector3.Forward) };
        for (int i = 0; i < 4; i++)
        {
            Color facet = color * (i % 2 == 0 ? 1.12f : .82f);
            facet.A = 1;
            Triangle(surface, top, rim[(i + 1) % 4], rim[i], facet);
            Triangle(surface, bottom, rim[i], rim[(i + 1) % 4], color.Darkened(.22f));
        }
    }

    private static void Triangle(SurfaceTool surface, Vector3 a, Vector3 b, Vector3 c, Color color)
    {
        // Godot front faces wind clockwise; authored palette colors are sRGB.
        surface.SetNormal((b - a).Cross(c - a).Normalized());
        surface.SetColor(color.SrgbToLinear());
        surface.AddVertex(a); surface.AddVertex(c); surface.AddVertex(b);
    }
}
