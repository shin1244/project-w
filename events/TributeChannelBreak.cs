using Godot;
using System.Collections.Generic;

// Presentation only: the server cleared a running channel while the stone stayed up, so the progress
// arc that was on screen flashes and breaks into pieces that scatter outward. A completed channel
// hides the stone instead and never breaks.
public partial class TributeChannelBreak : Node3D
{
    private const float Lifetime = .7f, FlashTime = .08f;
    private const int SegmentsPerShard = 4;
    private static readonly Color Flash = new("fff6e0");

    private readonly List<(MeshInstance3D Shard, Vector3 Velocity, Vector3 Axis)> _shards = new();
    private StandardMaterial3D _material;
    private Color _color;
    private float _age;

    // Breaks the first `steps` of `circleSteps` arc segments, drawn like TributeEventView's progress arc.
    public static void Spawn(Node3D parent, Vector3 position, float inner, float outer, int steps, int circleSteps, Color color)
    {
        if (steps <= 0 || !GodotObject.IsInstanceValid(parent) || !parent.IsInsideTree()) return;
        var effect = new TributeChannelBreak { Name = "ChannelBreak", Position = position, _color = color };
        effect._material = new StandardMaterial3D
        {
            AlbedoColor = Flash, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
        for (int first = 0, i = 0; first < steps; first += SegmentsPerShard, i++)
        {
            int count = Mathf.Min(SegmentsPerShard, steps - first);
            float middle = -Mathf.Pi / 2 + Mathf.Tau * (first + count * .5f) / circleSteps;
            Vector3 outward = new(Mathf.Cos(middle), 0, Mathf.Sin(middle));
            Vector3 center = outward * (inner + outer) * .5f;
            var shard = new MeshInstance3D
            {
                Name = "Shard" + i, Mesh = ShardMesh(inner, outer, first, count, circleSteps, center),
                MaterialOverride = effect._material, Position = center,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            effect.AddChild(shard);
            // Fixed spreads instead of random ones keep the README capture the same between runs.
            float speed = 1.5f + (i % 3) * .45f, lift = 1.6f + (i % 4) * .3f;
            Vector3 tangent = new(-outward.Z, 0, outward.X);
            effect._shards.Add((shard, outward * speed + Vector3.Up * lift, (tangent + Vector3.Up * (i % 2 == 0 ? .4f : -.4f)).Normalized()));
        }
        parent.AddChild(effect);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _age += dt;
        float progress = Mathf.Clamp(_age / Lifetime, 0, 1);
        if (_age < FlashTime)
        {
            // The arc holds its place for a moment, lit up like a cracked glass rim.
            _material.AlbedoColor = Flash;
            foreach (var (shard, _, _) in _shards) shard.Scale = Vector3.One * (1 + .25f * _age / FlashTime);
        }
        else
        {
            float fade = (_age - FlashTime) / (Lifetime - FlashTime);
            _material.AlbedoColor = new Color(Flash.Lerp(_color, Mathf.Min(1, fade * 4)), 1 - fade * fade);
            for (int i = 0; i < _shards.Count; i++)
            {
                var (shard, velocity, axis) = _shards[i];
                velocity += Vector3.Down * 7 * dt;
                _shards[i] = (shard, velocity, axis);
                shard.Position += velocity * dt;
                shard.Rotate(axis, dt * (7 + i % 3 * 2));
                shard.Scale = Vector3.One * Mathf.Lerp(1.25f, .6f, fade);
            }
        }
        if (progress >= 1) QueueFree();
    }

    public override void _ExitTree() => _material?.Dispose();

    // One piece of the flat progress ring, centered on its own middle so it can tumble in place.
    private static ArrayMesh ShardMesh(float inner, float outer, int first, int count, int circleSteps, Vector3 center)
    {
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = first; i < first + count; i++)
        {
            float a = -Mathf.Pi / 2 + Mathf.Tau * i / circleSteps;
            float b = -Mathf.Pi / 2 + Mathf.Tau * (i + 1) / circleSteps;
            Vector3 p = new(Mathf.Cos(a), 0, Mathf.Sin(a)), q = new(Mathf.Cos(b), 0, Mathf.Sin(b));
            foreach (Vector3 corner in new[] { p * inner, q * outer, q * inner, p * inner, p * outer, q * outer })
                surface.AddVertex(corner - center);
        }
        return surface.Commit();
    }
}
