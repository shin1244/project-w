using Godot;
using System.Collections.Generic;
using System.Linq;

// Presentation only: the server removed an allied crate right next to its owner's hero, so the hero
// took it. The crate flies from where it was (a worker's head or the ground) onto the hero, opens with
// a few gas-lamp glints and sinks in. No message is predicted; nothing here changes the inventory.
public partial class ItemCrateHandoff : Node3D
{
    // How close the owner's hero must stand to the crate's last spot. Pickup, delivery and the spawn
    // point all hand over within a few metres; a crate removed by a sale or a new purchase is usually
    // far from the hero, and then the crate just disappears as before.
    public const float ReceiveRadius = 4.5f;
    private const float Lifetime = .9f, FlightTime = .24f, OpenTime = .16f, SinkStart = .56f;
    private const int GlintCount = 8;
    private static readonly Color GasLamp = new("ffd37a");
    private static BoxMesh _bodyMesh, _lidMesh, _bodyBandMesh, _lidBandMesh, _latchMesh, _glowMesh;
    private static TorusMesh _ringMesh;
    private static ArrayMesh _glintMesh;

    private readonly MeshInstance3D[] _glints = new MeshInstance3D[GlintCount];
    private readonly Vector3[] _glintVelocities = new Vector3[GlintCount];
    private readonly List<MeshInstance3D> _chestParts = new();
    private Unit _hero;
    private Vector3 _start, _top;
    private Node3D _crate, _lid;
    private MeshInstance3D _ring, _flash;
    private StandardMaterial3D _wood, _metal, _glow, _billboard, _ringMaterial;
    private float _age;
    private bool _burst;

    // The owner's visible hero next to where the crate was, or null when nobody took it.
    public static Unit Receiver(ItemCrateView crate, IEnumerable<Unit> units)
    {
        Vector3 at = crate.GlobalPosition;
        Unit best = null;
        float bestDistance = ReceiveRadius * ReceiveRadius;
        foreach (Unit unit in units)
        {
            if (unit.OwnerId != crate.Snapshot.PlayerId || !UnitCatalog.IsHero(unit.UnitType) ||
                unit.IsDying || !unit.IsVisibleInTree()) continue;
            Vector3 gap = unit.GlobalPosition - at;
            float distance = gap.X * gap.X + gap.Z * gap.Z;
            if (distance <= bestDistance) (best, bestDistance) = (unit, distance);
        }
        return best;
    }

    public static void Spawn(Node3D parent, ItemCrateView crate, Unit hero)
    {
        if (!GodotObject.IsInstanceValid(parent) || !parent.IsInsideTree()) return;
        var effect = new ItemCrateHandoff { Name = "ItemCrateHandoff", _hero = hero };
        parent.AddChild(effect);
        effect.GlobalPosition = hero.GlobalPosition;
        effect._start = crate.ChestPosition - hero.GlobalPosition;
        // Over the hero's back or head, clear of the health bar.
        effect._top = Vector3.Up * (hero.HealthBarHeight - 1.25f);
        effect._crate.Position = effect._start;
    }

    public override void _Ready()
    {
        _bodyMesh ??= new BoxMesh { Size = new Vector3(.85f, .44f, .65f) };
        _lidMesh ??= new BoxMesh { Size = new Vector3(.85f, .16f, .65f) };
        _bodyBandMesh ??= new BoxMesh { Size = new Vector3(.09f, .46f, .68f) };
        _lidBandMesh ??= new BoxMesh { Size = new Vector3(.09f, .17f, .68f) };
        _latchMesh ??= new BoxMesh { Size = new Vector3(.17f, .2f, .06f) };
        _glowMesh ??= new BoxMesh { Size = new Vector3(.72f, .02f, .52f) };
        _ringMesh ??= new TorusMesh { InnerRadius = .9f, OuterRadius = 1, Rings = 32, RingSegments = 4 };
        _glintMesh ??= GlintMesh();
        _wood = new StandardMaterial3D { AlbedoColor = new Color("94704a"), Roughness = .9f };
        _metal = new StandardMaterial3D { AlbedoColor = new Color("e5bc69"), Metallic = .45f, Roughness = .65f };
        _glow = new StandardMaterial3D { AlbedoColor = GasLamp, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
        _billboard = new StandardMaterial3D
        {
            AlbedoColor = GasLamp, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled, BillboardKeepScale = true,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
        _ringMaterial = new StandardMaterial3D
        {
            AlbedoColor = GasLamp, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };

        // Same chest as ItemCrateView, cut in two: the lid hinges on the side away from the camera.
        _crate = new Node3D { Name = "Chest" };
        AddChild(_crate);
        Add(_crate, "Body", _bodyMesh, _wood, new Vector3(0, -.08f, 0));
        Add(_crate, "Glow", _glowMesh, _glow, new Vector3(0, .13f, 0));
        _lid = new Node3D { Name = "LidHinge", Position = new Vector3(0, .14f, -.325f) };
        _crate.AddChild(_lid);
        Add(_lid, "Lid", _lidMesh, _wood, new Vector3(0, .08f, .325f));
        Add(_lid, "Latch", _latchMesh, _metal, new Vector3(0, .02f, .68f));
        foreach (float x in new[] { -.27f, .27f })
        {
            Add(_crate, "BodyBand", _bodyBandMesh, _metal, new Vector3(x, -.08f, 0));
            Add(_lid, "LidBand", _lidBandMesh, _metal, new Vector3(x, .08f, .325f));
        }

        _chestParts.AddRange(_crate.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>());
        _flash = Add(this, "Flash", _glintMesh, _billboard, Vector3.Zero);
        _flash.Visible = false;
        for (int i = 0; i < GlintCount; i++)
        {
            float angle = i * Mathf.Tau / GlintCount + .3f;
            float speed = .9f + (i % 3) * .3f;
            _glintVelocities[i] = new Vector3(Mathf.Cos(angle) * speed, 2.6f + (i % 4) * .25f, Mathf.Sin(angle) * speed);
            _glints[i] = Add(this, "Glint" + i, _glintMesh, _billboard, Vector3.Zero);
            _glints[i].Visible = false;
        }
        _ring = Add(this, "ReceiveRing", _ringMesh, _ringMaterial, new Vector3(0, .06f, 0));
        _ring.Visible = false;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _age += dt;
        if (GodotObject.IsInstanceValid(_hero) && _hero.IsInsideTree() && !_hero.IsDying)
            GlobalPosition = _hero.GlobalPosition;

        if (_age < FlightTime)
        {
            float t = _age / FlightTime;
            float eased = 1 - (1 - t) * (1 - t);
            _crate.Position = _start.Lerp(_top, eased) + Vector3.Up * (.6f * 4 * t * (1 - t));
        }
        else if (_age < SinkStart)
        {
            float open = Mathf.Clamp((_age - FlightTime) / OpenTime, 0, 1);
            _crate.Position = _top;
            // A quick squash as it lands on the hero, then the lid springs past open and settles.
            _crate.Scale = new Vector3(1, 1 - .14f * Mathf.Sin(open * Mathf.Pi), 1);
            _lid.Rotation = new Vector3(-Mathf.DegToRad(118) * EaseOutBack(open), 0, 0);
        }
        else
        {
            float sink = Mathf.Clamp((_age - SinkStart) / (Lifetime - SinkStart), 0, 1);
            _crate.Position = _top.Lerp(_top * .55f, sink);
            _crate.Scale = Vector3.One * Mathf.Lerp(1, .3f, sink);
            foreach (MeshInstance3D part in _chestParts) part.Transparency = sink;
        }

        float burst = (_age - FlightTime) / (Lifetime - FlightTime);
        if (burst >= 0)
        {
            if (!_burst) Burst();
            float flash = Mathf.Clamp(burst / .4f, 0, 1);
            _flash.Position = _top + Vector3.Up * .3f;
            _flash.Scale = Vector3.One * Mathf.Max(.01f, 2.2f * Mathf.Sin(flash * Mathf.Pi));
            _flash.Visible = flash < 1;
            for (int i = 0; i < GlintCount; i++)
            {
                _glintVelocities[i] += Vector3.Down * 4.5f * dt;
                _glints[i].Position += _glintVelocities[i] * dt;
                _glints[i].Scale = Vector3.One * Mathf.Lerp(1.15f, .35f, burst);
                _glints[i].Transparency = Mathf.Clamp((burst - .45f) / .55f, 0, 1);
            }
            // The ring closes on the hero's feet: something was received, not struck.
            float radius = Mathf.Lerp(1.7f, .55f, 1 - (1 - burst) * (1 - burst));
            _ring.Scale = new Vector3(radius, .2f, radius);
            _ringMaterial.AlbedoColor = new Color(GasLamp, .85f * (1 - burst));
        }
        if (_age >= Lifetime) QueueFree();
    }

    public override void _ExitTree()
    {
        _wood?.Dispose();
        _metal?.Dispose();
        _glow?.Dispose();
        _billboard?.Dispose();
        _ringMaterial?.Dispose();
    }

    private void Burst()
    {
        _burst = _flash.Visible = _ring.Visible = true;
        for (int i = 0; i < GlintCount; i++)
        {
            _glints[i].Position = _top + new Vector3(0, .2f, 0);
            _glints[i].Visible = true;
        }
    }

    private static float EaseOutBack(float t)
    {
        const float c = 1.7f;
        float u = t - 1;
        return 1 + (c + 1) * u * u * u + c * u * u;
    }

    private static MeshInstance3D Add(Node3D parent, string name, Mesh mesh, Material material, Vector3 position)
    {
        var instance = new MeshInstance3D
        {
            Name = name, Mesh = mesh, MaterialOverride = material, Position = position,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        parent.AddChild(instance);
        return instance;
    }

    // A flat four-point star; the billboard material turns it to the camera.
    private static ArrayMesh GlintMesh()
    {
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.Tau / 8, b = (i + 1) * Mathf.Tau / 8;
            float ra = i % 2 == 0 ? .32f : .06f, rb = i % 2 == 0 ? .06f : .32f;
            surface.AddVertex(Vector3.Zero);
            surface.AddVertex(new Vector3(Mathf.Cos(a) * ra, Mathf.Sin(a) * ra, 0));
            surface.AddVertex(new Vector3(Mathf.Cos(b) * rb, Mathf.Sin(b) * rb, 0));
        }
        return surface.Commit();
    }
}
