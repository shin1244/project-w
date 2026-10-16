using Godot;

// A presentation-only part that turns about its own local Y axis (building gears, fans).
// Meshing gears share one Speed sign convention: a driven gear uses -Speed * driverRadius / radius.
public partial class SpinningPart : MeshInstance3D
{
    [Export] public float Speed { get; set; } = 1f; // Radians per second.

    public override void _Process(double delta)
    {
        // Hidden parts (construction, fog, culled previews) need no motion.
        if (IsVisibleInTree()) RotateObjectLocal(Vector3.Up, Speed * (float)delta);
    }
}
