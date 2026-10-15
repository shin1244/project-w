using Godot;

// A short cosmetic arc to a visible snapshot position; it never follows hidden units or deals damage.
public partial class WorkshopShellFlight : Node3D
{
    private Vector3 _start, _end;
    private Node3D _target;
    private float _elapsed, _duration, _arc;
    private MeshInstance3D _projectile;
    private StandardMaterial3D _material;
    public void Launch(Vector3 start, Vector3 end, Node3D target)
    {
        TopLevel = true; _start = start; _end = end; _target = target;
        float distance = start.DistanceTo(end);
        _duration = Mathf.Clamp(distance / 28, .16f, .48f); _arc = Mathf.Min(1.8f, distance * .12f);
        GlobalPosition = start;
        _material = new StandardMaterial3D { AlbedoColor = new Color("ffdc8a"),
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha };
        _projectile = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = .105f, Height = .21f, RadialSegments = 8, Rings = 4 },
            MaterialOverride = _material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        AddChild(_projectile);
    }
    public override void _Process(double delta)
    {
        if (_target == null || !GodotObject.IsInstanceValid(_target) || !_target.IsInsideTree() ||
            !_target.IsVisibleInTree() || _target.IsQueuedForDeletion() || _target is Unit { IsDying: true } ||
            GetParent() is Unit { IsDying: true }) { QueueFree(); return; }
        _elapsed += (float)delta;
        float t = Mathf.Min(1, _elapsed / _duration);
        GlobalPosition = _start.Lerp(_end, t) + Vector3.Up * (4 * _arc * t * (1-t));
        if (t >= 1)
        {
            float impact = (_elapsed - _duration) / .18f;
            if (impact >= 1) { QueueFree(); return; }
            _projectile.Scale = Vector3.One * (1 + impact * 6);
            _material.AlbedoColor = new Color(.94f - impact*.44f, .65f - impact*.18f, .30f, 1-impact);
        }
    }
}
