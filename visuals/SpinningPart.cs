using Godot;

// A presentation-only part that turns about its own local Y axis (building gears, fans).
// Meshing gears share one Speed sign convention: a driven gear uses -Speed * driverRadius / radius.
public partial class SpinningPart : MeshInstance3D
{
    [Export] public float Speed { get; set; } = 1f; // Radians per second.
    // Seconds to spin up to Speed after SetRunning(true), and to coast to rest after SetRunning(false).
    [Export] public float RampSeconds { get; set; } = .6f;
    public bool Running { get; private set; } = true;
    private float _throttle = 1;

    // Instant skips the ramp, e.g. for a building that is already idle when it first appears.
    public void SetRunning(bool running, bool instant = false)
    {
        Running = running;
        if (instant) _throttle = running ? 1 : 0;
    }

    public override void _Process(double delta)
    {
        float target = Running ? 1 : 0;
        _throttle = RampSeconds > 0 ? Mathf.MoveToward(_throttle, target, (float)delta / RampSeconds) : target;
        // Hidden parts (construction, fog, culled previews) need no motion.
        if (_throttle > 0 && IsVisibleInTree()) RotateObjectLocal(Vector3.Up, Speed * _throttle * (float)delta);
    }
}
