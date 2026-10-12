using Godot;

// 표시 위치와 서버 타격 번호만 사용한다. 이동·공격 판정은 바꾸지 않는다.
public partial class BeastAnimation : Node, ISkillPresentation
{
    private Unit _unit;
    private Node3D _rig, _body, _head, _jaw, _arm, _forearm, _tail;
    private readonly Node3D[] _legs = new Node3D[4];
    private readonly Node3D[] _lower = new Node3D[4];
    private readonly Vector3[] _legRest = new Vector3[4];
    private Vector3 _rigRest, _bodyRest, _lastPosition;
    private float _time, _stride, _walk, _dash, _attack = 1, _skill = 1;
    private int _skillSlot;
    private float _attackSide = 1;
    private UnitActivity _lastActivity;
    private bool _positionKnown;

    public override void _Ready()
    {
        _unit = GetParent<Unit>();
        _rig = _unit.GetNode<Node3D>("Visual/Rig");
        _rigRest = _rig.Position;
        _body = _rig.GetNode<Node3D>("Body");
        _head = _body.GetNode<Node3D>("Head");
        _jaw = _head.GetNode<Node3D>("Jaw");
        _arm = _body.GetNode<Node3D>("ExtraArm");
        _forearm = _arm.GetNode<Node3D>("Forearm");
        _tail = _body.GetNode<Node3D>("Tail");
        _bodyRest = _body.Position;
        string[] names = { "FrontLeft", "FrontRight", "HindLeft", "HindRight" };
        for (int i = 0; i < names.Length; i++)
        {
            _legs[i] = _rig.GetNode<Node3D>(names[i]);
            _legRest[i] = _legs[i].Position;
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
        _skill = Mathf.Min(1, _skill + dt / (_skillSlot is 2 or 3 ? .72f : .55f));
        _dash = Mathf.MoveToward(_dash, _unit.State.Activity == UnitActivity.Dash ? 1 : 0, dt * 12);

        // 짧게 움츠린 뒤 빠르게 물고, 어깨·꼬리가 늦게 따라오는 반동으로 무게를 보여 준다.
        float windup = Pulse(_attack, 0, .16f, .32f);
        float bite = Pulse(_attack, .14f, .34f, .76f);
        float recoil = Pulse(_attack, .47f, .68f, 1);
        float q = _skillSlot == 0 ? Pulse(_skill, 0, .3f, 1) : 0;
        float drain = _skillSlot == 1 ? Pulse(_skill, 0, .3f, 1) : 0;
        float howl = _skillSlot == 2 ? Pulse(_skill, 0, .35f, 1) : 0;
        float devour = _skillSlot == 3 ? Pulse(_skill, 0, .25f, .8f) : 0;
        float swallow = _skillSlot == 3 ? Pulse(_skill, .35f, .58f, 1) : 0;
        float crouch = windup * .07f + bite * .045f + devour * .085f;
        float twist = _attackSide * (windup * -.035f + bite * .055f - recoil * .025f);
        float pitch = windup * -.07f + bite * .13f - recoil * .035f - _dash * .13f + q * .05f - howl * .12f;
        float gait = Mathf.Sin(_stride) * _walk;

        // 시각 리그만 전진한다. Unit 루트·서버 위치·선택 영역은 건드리지 않는다.
        _rig.Position = _rigRest + Vector3.Forward * (bite * .23f - windup * .075f - recoil * .025f + q * .12f + devour * .1f);
        _body.Position = _bodyRest + Vector3.Up * (Mathf.Sin(_time * 2.4f) * .012f
            + Mathf.Abs(gait) * .04f - crouch + q * .085f + drain * .025f + howl * .09f + swallow * .05f);
        _body.Rotation = new Vector3(pitch, twist, gait * .022f - twist * .45f);
        for (int i = 0; i < _legs.Length; i++)
        {
            // 대각선 앞·뒷발이 함께 나가는 네발 보행. 뒷발은 꺾인 발목을 유지한다.
            float phase = Mathf.Sin(_stride + (i is 0 or 3 ? 0 : Mathf.Pi)) * _walk;
            bool front = i < 2;
            float side = i % 2 == 0 ? -1 : 1;
            float brace = front ? windup * .17f - bite * .22f + recoil * .1f : -windup * .2f + bite * .27f;
            // 다리는 Body의 형제 노드이므로, 몸통의 기울기를 관절 시작점에도 적용한다.
            _legs[i].Position = _body.Position + _body.Basis * (_legRest[i] - _bodyRest);
            _legs[i].Rotation = new Vector3(phase * (front ? .46f : .39f) + brace
                + _dash * (front ? .62f : -.48f) + q * (front ? .18f : -.1f) + howl * (front ? -.08f : .06f),
                twist * .5f, side * (windup * .025f + bite * .035f + devour * .04f));
            _lower[i].Rotation = new Vector3(Mathf.Max(0, -phase) * (front ? .52f : -.42f)
                + _dash * .32f + (front ? windup * .12f + bite * .16f : -windup * .14f - bite * .13f), 0, 0);
        }
        _head.Rotation = new Vector3(windup * .1f - bite * .25f + recoil * .09f - q * .18f
            + drain * .12f + howl * .36f - devour * .24f + swallow * .18f + Mathf.Sin(_time * 1.4f) * .02f,
            Mathf.Sin(_time * .7f) * .028f - twist * .7f, _attackSide * recoil * .045f);
        float mouth = Mathf.Max(Pulse(_attack, 0, .2f, .51f) * .5f,
            q * .62f + _dash * .15f + drain * .2f + howl * .52f + devour * .65f);
        _jaw.Rotation = new Vector3(-.05f - mouth, 0, 0);
        _arm.Rotation = new Vector3(Mathf.Sin(_time * 2.7f) * .07f + windup * .15f - bite * .45f
            - q * .32f - devour * .42f + howl * .16f, bite * .23f, -.06f + Mathf.Sin(_time * 1.9f) * .05f - drain * .14f);
        _forearm.Rotation = new Vector3(-bite * .43f - q * .25f - devour * .4f,
            Mathf.Sin(_time * 2.2f) * .08f + recoil * .16f, 0);
        _tail.Rotation = new Vector3(Mathf.Sin(_time * 1.6f) * .05f + windup * .09f - bite * .14f - q * .15f,
            Mathf.Sin(_time * 2.2f) * .13f - gait * .13f + _attackSide * (bite * -.21f + recoil * .25f), 0);
    }

    private static float Pulse(float t, float start, float peak, float end)
    {
        if (t <= start || t >= end) return 0;
        return t < peak ? Mathf.SmoothStep(0, 1, (t - start) / (peak - start))
            : 1 - Mathf.SmoothStep(0, 1, (t - peak) / (end - peak));
    }

    private void OnStateChanged(UnitState state, bool swung)
    {
        if (swung) { _attack = 0; _attackSide = -_attackSide; }
        if (state.Activity == UnitActivity.Dash && _lastActivity != UnitActivity.Dash && _skill >= 1)
        {
            _skillSlot = 0;
            _skill = 0;
        }
        if (state.Activity == UnitActivity.Stun) { _skill = _attack = 1; _walk = _dash = 0; }
        _lastActivity = state.Activity;
    }

    // Only an accepted server SKILL broadcast starts this pose. Movement remains server POS.
    public void PlaySkill(int slot, bool empowered = false)
    {
        if (_unit.IsDying || slot < 0 || slot > 3) return;
        _skillSlot = slot;
        _skill = 0;
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_unit)) _unit.StateChanged -= OnStateChanged;
    }
}
