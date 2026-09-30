using Godot;
using System;
using System.Runtime.InteropServices;

// 몸 가장자리에서 시야만큼 확장한 윤곽을 장애물 경계에서 자르는 삼각형 팬.
public sealed class FogLightMesh
{
    public const int Segments = 256;
    private static readonly Vector2[] Directions = CreateDirections();
    private static readonly int[] Indices = CreateIndices();
    private readonly Vector3[] _vertices = new Vector3[Segments + 1];
    private readonly Vector2[] _polygon = new Vector2[Segments];
    private readonly float[] _distances = new float[Segments];
    private readonly ArrayMesh _mesh = new();
    private readonly MeshInstance2D _node;
    private readonly ShaderMaterial _material;
    private uint _revision = uint.MaxValue;
    private RangeBody _body;
    private float _sight = -1;
    private int _ignore;

    public FogLightMesh(Node parent)
    {
        _material = new ShaderMaterial { Shader = GD.Load<Shader>("res://maps/FogLight.gdshader") };
        _node = new MeshInstance2D { Mesh = _mesh, Material = _material };
        parent.AddChild(_node);
        using var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = _vertices;
        arrays[(int)Mesh.ArrayType.Index] = Indices;
        _mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays, flags: Mesh.ArrayFormat.FlagUseDynamicUpdate);
    }

    public bool Update(RangeBody body, float sight, int ignore, FogOcclusionGrid grid, Vector2 origin, float pixelsPerWorldUnit, float cell)
    {
        if (_body == body && _sight == sight && _ignore == ignore && _revision == grid.Revision) return false;
        _body = body; _sight = sight; _ignore = ignore; _revision = grid.Revision;
        float reach = sight + body.Radius;
        float extent = body.HalfExtents.Length() + reach;
        _node.Position = (body.Center - origin) * pixelsPerWorldUnit;
        _node.Visible = extent > 0;
        if (extent <= 0) return true;
        for (int i = 0; i < Segments; i++)
            _distances[i] = grid.RayDistance(body.Center, Directions[i], body.BoundaryDistance(Directions[i], sight), ignore);
        for (int i = 0; i < Segments; i++)
        {
            // 차폐 모서리를 잇는 삼각형이 장애물 뒤로 삐져나오지 않도록 이웃 각도도 보수적으로 적용합니다.
            float distance = Math.Min(_distances[i], Math.Min(_distances[(i + Segments - 1) % Segments], _distances[(i + 1) % Segments]));
            // 열린 곳의 곡선은 정확한 범위를 유지하고, 차폐 경계만 보수적으로 잘라냅니다.
            float boundary = body.BoundaryDistance(Directions[i], sight);
            bool occluded = _distances[i] < boundary - .001f ||
                _distances[(i + Segments - 1) % Segments] < body.BoundaryDistance(Directions[(i + Segments - 1) % Segments], sight) - .001f ||
                _distances[(i + 1) % Segments] < body.BoundaryDistance(Directions[(i + 1) % Segments], sight) - .001f;
            distance = occluded ? Math.Max(0, Math.Min(boundary, distance) - .02f * cell) : boundary;
            _polygon[i] = Directions[i] * distance;
            Vector2 vertex = _polygon[i] * pixelsPerWorldUnit;
            _vertices[i + 1] = new Vector3(vertex.X, vertex.Y, 0);
        }
        float pixelRadius = extent * pixelsPerWorldUnit;
        _mesh.CustomAabb = new Aabb(new Vector3(-pixelRadius, -pixelRadius, -.1f), new Vector3(pixelRadius * 2, pixelRadius * 2, .2f));
        _mesh.SurfaceUpdateVertexRegion(0, 0, MemoryMarshal.AsBytes(_vertices.AsSpan()));
        _material.SetShaderParameter("half_extents", body.HalfExtents * pixelsPerWorldUnit);
        _material.SetShaderParameter("reach", reach * pixelsPerWorldUnit);
        _material.SetShaderParameter("feather", Math.Min(reach, .35f * cell) * pixelsPerWorldUnit);
        return true;
    }

    public bool Contains(Vector2 point) => _sight >= 0 && _body.DistanceTo(new(point, Vector2.Zero, 0)) <= _sight &&
        Geometry2D.IsPointInPolygon(point - _body.Center, _polygon);
    public void Remove()
    {
        _node.Hide();
        _node.QueueFree();
        _mesh.Dispose();
        _material.Dispose();
    }

    private static Vector2[] CreateDirections()
    {
        var result = new Vector2[Segments];
        for (int i = 0; i < Segments; i++) result[i] = Vector2.FromAngle(Mathf.Tau * i / Segments);
        return result;
    }
    private static int[] CreateIndices()
    {
        var result = new int[Segments * 3];
        for (int i = 0; i < Segments; i++) { result[i * 3] = 0; result[i * 3 + 1] = i + 1; result[i * 3 + 2] = (i + 1) % Segments + 1; }
        return result;
    }
}
