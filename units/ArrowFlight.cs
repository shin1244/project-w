using Godot;

// Visual only. The server has already resolved the swing; this never deals damage.
public partial class ArrowFlight : Node3D
{
    private Vector3 _start, _end;
    private float _elapsed, _duration, _arc;
    private Node3D _target;

    public void Launch(Vector3 start, Vector3 end, Node3D target)
    {
        TopLevel = true;
        _start = start;
        _end = end; // Never follow a target beyond its last visible position.
        _target = target;
        _duration = Mathf.Clamp(start.DistanceTo(end) / 32, .10f, .30f);
        _arc = Mathf.Min(.25f, start.DistanceTo(end) * .025f);
        GlobalPosition = start;
        Face(end - start);
    }

    public override void _Process(double delta)
    {
        if (_target != null && (!GodotObject.IsInstanceValid(_target) || !_target.IsInsideTree() || !_target.IsVisibleInTree()))
        { QueueFree(); return; }
        _elapsed += (float)delta;
        float t = Mathf.Min(1, _elapsed / _duration);
        GlobalPosition = _start.Lerp(_end, t) + Vector3.Up * (4 * _arc * t * (1 - t));
        Face(_end - _start + Vector3.Up * (4 * _arc * (1 - 2 * t)));
        if (t >= 1) QueueFree();
    }

    private void Face(Vector3 direction)
    {
        if (direction.LengthSquared() < .000001f) return;
        LookAt(GlobalPosition + direction, Mathf.Abs(direction.Normalized().Dot(Vector3.Up)) > .99f ? Vector3.Right : Vector3.Up);
    }
}
