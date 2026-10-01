using Godot;

// 표시 위치와 서버 타격 번호만 사용한다. 이동·공격 판정은 바꾸지 않는다.
public partial class BeastAnimation : Node
{
    private Unit _unit;
    private Node3D _body, _head, _jaw, _arm, _forearm, _tail;
    private readonly Node3D[] _legs = new Node3D[4];
    private readonly Node3D[] _lower = new Node3D[4];
    private Vector3 _bodyRest, _lastPosition;
    private float _time, _stride, _walk, _attack = 1, _skill = 1;
    private UnitActivity _lastActivity;
    private bool _positionKnown;

    public override void _Ready()
    {
        _unit = GetParent<Unit>();
        Node3D rig = _unit.GetNode<Node3D>("Visual/Rig");
        _body = rig.GetNode<Node3D>("Body");
        _head = _body.GetNode<Node3D>("Head");
        _jaw = _head.GetNode<Node3D>("Jaw");
        _arm = _body.GetNode<Node3D>("ExtraArm");
        _forearm = _arm.GetNode<Node3D>("Forearm");
        _tail = _body.GetNode<Node3D>("Tail");
        _bodyRest = _body.Position;
        string[] names = { "FrontLeft", "FrontRight", "HindLeft", "HindRight" };
        for (int i = 0; i < names.Length; i++)
        {
            _legs[i] = rig.GetNode<Node3D>(names[i]);
            _lower[i] = _legs[i].GetNode<Node3D>("Lower");
        }
        _unit.StateChanged += OnStateChanged;
    }

    public override void _Process(double delta)
    {
        if (_unit.IsDying) { SetProcess(false); return; }
        float dt = (float)delta;
        _time += dt;
        Vector3 position = _unit.GlobalPosition;
        float distance = _positionKnown ? position.DistanceTo(_lastPosition) : 0;
        _positionKnown = true;
        _lastPosition = position;
        bool stepping = dt > 0 && distance < 1 && _unit.State.Activity is UnitActivity.Idle or UnitActivity.Guard;
        _walk = Mathf.MoveToward(_walk, stepping ? Mathf.Clamp(distance / dt / 4, 0, 1) : 0, dt * 8);
        if (distance < 1) _stride += distance * 4.2f;
        _attack = Mathf.Min(1, _attack + dt / .55f);
        _skill = Mathf.Min(1, _skill + dt / .55f);
        float bite = Mathf.Sin(_attack * Mathf.Pi);
        float skill = Mathf.Sin(_skill * Mathf.Pi);
        float dash = _unit.State.Activity == UnitActivity.Dash ? 1 : 0;
        for (int i = 0; i < _legs.Length; i++)
        {
            // 대각선 앞·뒷발이 함께 나가는 네발 보행. 뒷발은 꺾인 발목을 유지한다.
            float phase = Mathf.Sin(_stride + (i is 0 or 3 ? 0 : Mathf.Pi)) * _walk;
            _legs[i].Rotation = new Vector3(phase * (i < 2 ? .42f : .36f) + dash * (i < 2 ? .65f : -.5f), 0, 0);
            _lower[i].Rotation = new Vector3(Mathf.Max(0, -phase) * (i < 2 ? .5f : -.4f) + dash * .35f, 0, 0);
        }
        _body.Position = _bodyRest + Vector3.Up * (Mathf.Sin(_time * 2.4f) * .012f + Mathf.Abs(Mathf.Sin(_stride)) * .035f * _walk - bite * .045f + skill * .12f);
        _body.Rotation = new Vector3(bite * .06f - dash * .16f, 0, Mathf.Sin(_stride) * .014f * _walk);
        _head.Rotation = new Vector3(-bite * .26f - skill * .22f + Mathf.Sin(_time * 1.4f) * .02f, Mathf.Sin(_time * .7f) * .028f, 0);
        _jaw.Rotation = new Vector3(-.05f - Mathf.Max(bite * .42f, skill * .65f + dash * .18f), 0, 0);
        _arm.Rotation = new Vector3(Mathf.Sin(_time * 2.7f) * .09f - bite * .30f, bite * .18f, -.06f + Mathf.Sin(_time * 1.9f) * .07f);
        _forearm.Rotation = new Vector3(-bite * .35f, Mathf.Sin(_time * 2.2f) * .10f, 0);
        _tail.Rotation = new Vector3(Mathf.Sin(_time * 1.6f) * .05f, Mathf.Sin(_time * 2.2f) * .13f - Mathf.Sin(_stride) * .10f * _walk, 0);
    }

    private void OnStateChanged(UnitState state, bool swung)
    {
        if (swung) _attack = 0;
        if (state.Activity == UnitActivity.Dash && _lastActivity != UnitActivity.Dash && _skill >= 1) _skill = 0;
        if (state.Activity == UnitActivity.Stun) { _skill = _attack = 1; _walk = 0; }
        _lastActivity = state.Activity;
    }

    // Only an accepted server SKILL broadcast starts this pose. Movement remains server POS.
    public void PlaySkill(int slot)
    {
        if (slot == 0 && !_unit.IsDying) _skill = 0;
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_unit)) _unit.StateChanged -= OnStateChanged;
    }
}
