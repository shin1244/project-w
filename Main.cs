using Godot;

public partial class Main : Node3D
{
    [Export] public Camera3D Camera;
    [Export] public Unit Unit;

	private NetClient _net;
	public override void _Ready()
	{
		_net = GetNode<NetClient>("/root/Net");
		_net.MessageReceived += OnMessage;
		_net.ConnectToServer("127.0.0.1", 7777);
	}

	private void OnMessage(string msg)
	{
		GD.Print($"받음: {msg}"); // 나중에 여기서 유닛 생성/갱신
	}

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.Pressed
            && mb.ButtonIndex == MouseButton.Right)
        {
            Vector3 from = Camera.ProjectRayOrigin(mb.Position);
            Vector3 dir = Camera.ProjectRayNormal(mb.Position);

            var ground = new Plane(Vector3.Up, 0);
            if (ground.IntersectsRay(from, dir) is Vector3 point)
                Unit.MoveTo(point);
        }
    }
}