using Godot;

// A presentation-only part that lifts along its local Y and drops back, like a steam hammer:
// a slow lift, a short hold at the top, then a fast fall onto its rest position.
// With Swing it tilts up about its local X instead, like a hinged boom pounding the ground.
public partial class ReciprocatingPart : MeshInstance3D
{
    [Export] public float Travel { get; set; } = .3f; // Lift height in the parent's units.
    [Export] public float Period { get; set; } = 1.6f; // Seconds per blow.
    [Export] public float Offset { get; set; } // Seconds into the cycle, so neighbours fall out of step.
    [Export] public float Swing { get; set; } // Radians to tilt about local X; 0 lifts by Travel instead.
    public bool Running { get; private set; } = true;

    private Vector3 _rest;
    private Basis _restBasis;
    private float _time;
    private bool _resting;

    public override void _Ready()
    {
        _rest = Position;
        _restBasis = Basis;
        _time = Offset;
    }

    // A stopped part finishes the blow it is in and waits at rest; instant drops it to rest at once.
    // The next start lifts from rest instead of jumping into the middle of a cycle.
    public void SetRunning(bool running, bool instant = false)
    {
        Running = running;
        if (running) _resting = false;
        else if (instant) Rest();
    }

    public override void _Process(double delta)
    {
        if (_resting || !IsVisibleInTree() || Period <= 0) return;
        _time += (float)delta;
        if (_time >= Period)
        {
            if (!Running)
            {
                Rest();
                return;
            }
            _time %= Period;
        }
        float t = _time / Period;
        float lift = t < .7f ? Mathf.SmoothStep(0, 1, t / .7f) : t < .85f ? 1 : 1 - Mathf.Pow((t - .85f) / .15f, 2);
        if (Swing != 0) Basis = _restBasis * new Basis(Vector3.Right, Swing * lift);
        else Position = _rest + Vector3.Up * (Travel * lift);
    }

    private void Rest()
    {
        _resting = true;
        _time = 0;
        Position = _rest;
        Basis = _restBasis;
    }
}
