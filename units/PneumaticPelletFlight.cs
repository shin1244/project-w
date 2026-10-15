using Godot;

// A small metal pellet, never an arrow. The server already resolved damage before this cue.
public partial class PneumaticPelletFlight : Node3D
{
    private Vector3 _start, _end;
    private Node3D _target;
    private float _age, _duration;
    private static readonly SphereMesh PelletMesh = new() { Radius = .035f, Height = .07f, RadialSegments = 8, Rings = 4 };
    private static readonly StandardMaterial3D PelletMaterial = new()
    {
        AlbedoColor = new Color("d8d1ad"), Metallic = .7f, Roughness = .35f,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
    };

    public void Launch(Vector3 start, Vector3 end, Node3D target)
    {
        TopLevel = true; _start = start; _end = end; _target = target;
        _duration = Mathf.Clamp(start.DistanceTo(end) / 42, .075f, .24f);
        GlobalPosition = start;
        if (start.DistanceSquaredTo(end) > .0001f)
            LookAt(end, Mathf.Abs((end - start).Normalized().Dot(Vector3.Up)) > .98f ? Vector3.Right : Vector3.Up);
        AddChild(new MeshInstance3D { Mesh = PelletMesh, MaterialOverride = PelletMaterial,
            Scale = new Vector3(1, 1, 2.4f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    public override void _Process(double delta)
    {
        if (_target == null || !GodotObject.IsInstanceValid(_target) || !_target.IsInsideTree()
            || !_target.IsVisibleInTree() || _target.IsQueuedForDeletion() || _target is Unit { IsDying: true }
            || GetParent() is not Unit shooter || shooter.IsDying || !shooter.IsVisibleInTree())
        { QueueFree(); return; }
        _age += (float)delta;
        float t = Mathf.Min(1, _age / _duration);
        GlobalPosition = _start.Lerp(_end, t);
        if (t >= 1) QueueFree();
    }
}
