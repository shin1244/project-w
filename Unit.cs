using Godot;

public partial class Unit : Node3D
{
	
    [Export] public float Speed = 5f;
	[Export] public UnitDef Def;
    private Vector3? _target;

	public override void _Ready() {

		if (Def?.Model != null)
		{
			var model = Def.Model.Instantiate<Node3D>();
			model.Scale = Vector3.One * Def.Scale;
			AddChild(model);
		}
	}
    public void MoveTo(Vector3 target) => _target = target;

    public override void _Process(double delta)
    {
        if (_target is not Vector3 target) return;

        Vector3 toTarget = target - GlobalPosition;
        toTarget.Y = 0;
        float step = Speed * (float)delta;

        if (toTarget.Length() <= step)
        {
            GlobalPosition = new Vector3(target.X, GlobalPosition.Y, target.Z);
            _target = null;
            return;
        }
        GlobalPosition += toTarget.Normalized() * step;
    }
}