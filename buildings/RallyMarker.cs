using Godot;

// 선택한 아군 생산 건물의 확정된 랠리만 표시한다. 물리 충돌/입력 대상은 만들지 않는다.
public partial class RallyMarker : Node3D
{
    private readonly StandardMaterial3D _material = new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        NoDepthTest = true, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha, RenderPriority = 121
    };
    private readonly ImmediateMesh _line = new();
    private Node3D _flag;
    private Vector3 _from, _to;
    private bool _hasPoint;

    public override void _Ready()
    {
        Visible = false;
        AddMesh(this, _line);
        _flag = new Node3D { Name = "Flag" };
        AddChild(_flag);
        AddMesh(_flag, new TorusMesh { InnerRadius = .58f, OuterRadius = .67f, Rings = 32, RingSegments = 6 }, new(0, .08f, 0));
        AddMesh(_flag, new CylinderMesh { TopRadius = .05f, BottomRadius = .05f, Height = 4.2f, RadialSegments = 8 }, new(0, 2.1f, 0));
        var pennant = new ImmediateMesh();
        pennant.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        pennant.SurfaceAddVertex(new(0, 4.2f, 0));
        pennant.SurfaceAddVertex(new(1.5f, 3.65f, 0));
        pennant.SurfaceAddVertex(new(0, 3.15f, 0));
        pennant.SurfaceEnd();
        AddMesh(_flag, pennant);
    }

    public void ShowPoint(Vector3 from, RallySnapshot rally)
    {
        Visible = true;
        Color color = new(rally.ResourceId != 0 ? "72f1ac" : "ffda73");
        _material.AlbedoColor = color;
        _flag.Position = ToLocal(rally.Position);
        if (_hasPoint && _from == from && _to == rally.Position) return;
        _hasPoint = true;
        _from = from;
        _to = rally.Position;
        _line.ClearSurfaces();
        Vector3 start = ToLocal(from) + Vector3.Up * .12f;
        Vector3 end = ToLocal(rally.Position) + Vector3.Up * .12f;
        float distance = start.DistanceTo(end);
        if (!float.IsFinite(distance) || distance < .01f) return;
        Vector3 direction = (end - start) / distance;
        int segments = (int)Mathf.Clamp(Mathf.Ceil(distance / 1.1f), 1, 256);
        float spacing = distance / segments;
        _line.SurfaceBegin(Mesh.PrimitiveType.Lines);
        for (int i = 0; i < segments; i++)
        {
            float d = i * spacing;
            _line.SurfaceAddVertex(start + direction * d);
            _line.SurfaceAddVertex(start + direction * (d + spacing * .5f));
        }
        _line.SurfaceEnd();
    }

    private void AddMesh(Node3D parent, Mesh mesh, Vector3 position = default) => parent.AddChild(new MeshInstance3D
    {
        Mesh = mesh, MaterialOverride = _material, Position = position,
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
    });
}
