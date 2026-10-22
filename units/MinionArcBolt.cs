using Godot;
using System;

// A short server-authorized lightning arc for one bounce (MINION_ARC); it never changes HP.
// It waits until the arrow that started the chain would land, then flickers and fades.
public partial class MinionArcBolt : Node3D
{
    private const int Segments = 6;
    private const float Height = 1.1f, Jitter = .22f, Lifetime = .28f;

    public Vector3 From { get; set; }
    public Vector3 To { get; set; }
    public float Delay { get; set; }
    public Func<Vector3, bool> VisibilityCheck { get; set; }
    private readonly MeshInstance3D[] _pieces = new MeshInstance3D[Segments];
    private StandardMaterial3D _material;
    private CylinderMesh _mesh;
    private float _age, _reshape;

    public override void _Ready()
    {
        TopLevel = true;
        _material = new StandardMaterial3D {
            AlbedoColor = new Color(.65f, .95f, 1, .95f), EmissionEnabled = true, Emission = new Color(.35f, .85f, 1),
            EmissionEnergyMultiplier = 2.5f, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };
        _mesh = new CylinderMesh { TopRadius = .035f, BottomRadius = .035f, Height = 1, RadialSegments = 5, Rings = 1, Material = _material };
        for (int i = 0; i < Segments; i++)
        {
            _pieces[i] = new MeshInstance3D { Name = $"Segment{i}", Mesh = _mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            AddChild(_pieces[i]);
        }
        Visible = false;
    }

    public override void _Process(double delta)
    {
        _age += (float)delta;
        float lit = _age - Delay;
        if (lit < 0) return;
        if (lit >= Lifetime) { QueueFree(); return; }
        _reshape -= (float)delta;
        if (_reshape <= 0) { Shape(); _reshape = .05f; }
        _material.AlbedoColor = new Color(.65f, .95f, 1, .95f * (1 - lit / Lifetime));
        Vector3 middle = (From + To) * .5f;
        Visible = VisibilityCheck?.Invoke(middle) ?? true;
    }

    // A jagged path between the two units, redrawn a few times so the arc flickers.
    private void Shape()
    {
        Vector3 start = From + Vector3.Up * Height, end = To + Vector3.Up * Height;
        Vector3 previous = start;
        for (int i = 1; i <= Segments; i++)
        {
            Vector3 point = start.Lerp(end, (float)i / Segments);
            if (i < Segments)
                point += new Vector3((float)GD.RandRange(-Jitter, Jitter), (float)GD.RandRange(-Jitter, Jitter), (float)GD.RandRange(-Jitter, Jitter));
            Vector3 span = point - previous;
            var piece = _pieces[i - 1];
            float length = span.Length();
            if (length < .001f) { piece.Visible = false; previous = point; continue; }
            piece.Visible = true;
            Vector3 up = span / length;
            Vector3 side = Mathf.Abs(up.Dot(Vector3.Up)) > .99f ? Vector3.Right : Vector3.Up.Cross(up).Normalized();
            piece.GlobalTransform = new Transform3D(new Basis(side, up * length, up.Cross(side)), previous + span * .5f);
            previous = point;
        }
    }

    public override void _ExitTree()
    {
        VisibilityCheck = null;
        _material?.Dispose();
        _mesh?.Dispose();
    }
}
