using Godot;

// A presentation-only part that lifts along its local Y and drops back, like a steam hammer:
// a slow lift, a short hold at the top, then a fast fall onto its rest position.
public partial class ReciprocatingPart : MeshInstance3D
{
    [Export] public float Travel { get; set; } = .3f; // Lift height in the parent's units.
    [Export] public float Period { get; set; } = 1.6f; // Seconds per blow.
    [Export] public float Offset { get; set; } // Seconds into the cycle, so neighbours fall out of step.

    private Vector3 _rest;
    private float _time;

    public override void _Ready()
    {
        _rest = Position;
        _time = Offset;
    }

    public override void _Process(double delta)
    {
        if (!IsVisibleInTree() || Period <= 0) return;
        _time = (_time + (float)delta) % Period;
        float t = _time / Period;
        float lift = t < .7f ? Mathf.SmoothStep(0, 1, t / .7f) : t < .85f ? 1 : 1 - Mathf.Pow((t - .85f) / .15f, 2);
        Position = _rest + Vector3.Up * (Travel * lift);
    }
}
