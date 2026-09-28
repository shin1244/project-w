using Godot;
using System;
using System.Runtime.InteropServices;

// 작은 원을 장애물 경계에서 자른 삼각형 팬. 격자 칸의 사각형 윤곽을 출력하지 않습니다.
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
    private Vector2 _center;
    private float _radius = -1;
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

    public bool Update(Vector2 center, float radius, int ignore, FogOcclusionGrid grid, Vector2 origin, float pixelsPerWorldUnit, float cell)
    {
        if (_center == center && _radius == radius && _ignore == ignore && _revision == grid.Revision) return false;
        _center = center; _radius = radius; _ignore = ignore; _revision = grid.Revision;
        _node.Position = (center - origin) * pixelsPerWorldUnit;
        _node.Visible = radius > 0;
        if (radius <= 0) return true;
        for (int i = 0; i < Segments; i++)
            _distances[i] = grid.RayDistance(center, Directions[i], radius, ignore);
        for (int i = 0; i < Segments; i++)
        {
            // 차폐 모서리를 잇는 삼각형이 장애물 뒤로 삐져나오지 않도록 이웃 각도도 보수적으로 적용합니다.
            float distance = Math.Min(_distances[i], Math.Min(_distances[(i + Segments - 1) % Segments], _distances[(i + 1) % Segments]));
            if (distance < radius) distance = Math.Max(0, distance - .02f * cell);
            _polygon[i] = Directions[i] * distance;
            Vector2 vertex = _polygon[i] * pixelsPerWorldUnit;
            _vertices[i + 1] = new Vector3(vertex.X, vertex.Y, 0);
        }
        float pixelRadius = radius * pixelsPerWorldUnit;
        _mesh.CustomAabb = new Aabb(new Vector3(-pixelRadius, -pixelRadius, -.1f), new Vector3(pixelRadius * 2, pixelRadius * 2, .2f));
        _mesh.SurfaceUpdateVertexRegion(0, 0, MemoryMarshal.AsBytes(_vertices.AsSpan()));
        _material.SetShaderParameter("radius", pixelRadius);
        _material.SetShaderParameter("feather", Math.Min(radius, .35f * cell) * pixelsPerWorldUnit);
        return true;
    }

    public bool Contains(Vector2 point) => _radius > 0 && Geometry2D.IsPointInPolygon(point - _center, _polygon);
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
