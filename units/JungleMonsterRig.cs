using Godot;
using System.Collections.Generic;

// 정글몹 전용 저폴리 모델. 영웅 모델을 빌리지 않고 코드로 조립하며, 이동은 서버 위치 변화,
// 공격 자세는 STATE의 타격 번호로만 움직인다. 모델 정면은 -Z.
public abstract partial class JungleMonsterRig : Node3D
{
    private Unit _unit;
    private Vector3 _lastPosition;
    private bool _positionKnown;
    private float _time, _stride, _walk, _strike = 1;
    private readonly Dictionary<Color, StandardMaterial3D> _materials = new();

    protected abstract float StrideScale { get; }
    protected abstract float StrikeSeconds { get; }
    protected abstract void Build();
    protected abstract void Pose(float time, float stride, float walk, float strike);

    public override void _Ready()
    {
        Build();
        _unit = GetParent()?.GetParent() as Unit;
        if (_unit != null) _unit.StateChanged += OnStateChanged;
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_unit)) _unit.StateChanged -= OnStateChanged;
    }

    public override void _Process(double delta)
    {
        if (_unit == null) return;
        if (_unit.IsDying) { SetProcess(false); return; }
        float dt = (float)delta;
        _time += dt;
        Vector3 position = _unit.GlobalPosition;
        float distance = _positionKnown ? position.DistanceTo(_lastPosition) : 0;
        _positionKnown = true;
        _lastPosition = position;
        bool stepping = dt > 0 && distance < 1;
        _walk = Mathf.MoveToward(_walk, stepping ? Mathf.Clamp(distance / dt / 4, 0, 1) : 0, dt * 8);
        if (distance < 1) _stride += distance * StrideScale;
        _strike = Mathf.Min(1, _strike + dt / StrikeSeconds);
        Pose(_time, _stride, _walk, Mathf.Sin(_strike * Mathf.Pi));
    }

    private void OnStateChanged(UnitState state, bool swung)
    {
        if (swung) _strike = 0;
        if (state.Activity == UnitActivity.Stun) { _strike = 1; _walk = 0; }
    }

    protected Node3D Joint(Node3D parent, string name, Vector3 position)
    {
        var joint = new Node3D { Name = name, Position = position };
        parent.AddChild(joint);
        return joint;
    }

    protected MeshInstance3D Part(Node3D parent, Mesh mesh, string color, Vector3 position, Vector3 rotation = default,
        float glow = 0)
    {
        var part = new MeshInstance3D { Mesh = mesh, MaterialOverride = Material(new Color(color), glow),
            Position = position, Rotation = rotation };
        parent.AddChild(part);
        return part;
    }

    protected static BoxMesh Box(float x, float y, float z) => new() { Size = new Vector3(x, y, z) };
    protected static CylinderMesh Cylinder(float top, float bottom, float height, int sides = 6) =>
        new() { TopRadius = top, BottomRadius = bottom, Height = height, RadialSegments = sides, Rings = 1 };
    protected static CylinderMesh Spike(float radius, float height) => Cylinder(0, radius, height, 4);
    protected static SphereMesh Blob(float radius, float height) =>
        new() { Radius = radius, Height = height, RadialSegments = 7, Rings = 4 };

    private StandardMaterial3D Material(Color color, float glow)
    {
        if (glow <= 0 && _materials.TryGetValue(color, out var cached)) return cached;
        var material = new StandardMaterial3D { AlbedoColor = color, Roughness = .9f };
        if (glow > 0)
        {
            material.EmissionEnabled = true;
            material.Emission = color;
            material.EmissionEnergyMultiplier = glow;
            return material;
        }
        _materials[color] = material;
        return material;
    }
}
