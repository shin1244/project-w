using Godot;

// Server state drives attacks/dashes; the model and effects only present them.
public partial class GolemAnimation : Node3D, ISkillPresentation
{
    private Unit _unit;
    private Node3D _body, _head, _leftArm, _rightArm, _leftFist, _rightFist, _leftLeg, _rightLeg;
    private Vector3 _rest, _lastPosition;
    private bool _positionKnown, _shieldEmpowered;
    private float _time, _stride, _walk, _attack = 1, _slam = 1, _pulse = 1, _radius, _chargeFlash;
    private MeshInstance3D _ring, _shield, _rune;
    private StandardMaterial3D _ringMaterial, _shieldMaterial, _runeMaterial;

    public override void _Ready()
    {
        _unit = GetParent<Unit>();
        var rig = _unit.GetNode<Node3D>("Visual/Rig");
        _body = rig.GetNode<Node3D>("Body");
        _head = _body.GetNode<Node3D>("Head");
        _leftArm = _body.GetNode<Node3D>("LeftArm");
        _rightArm = _body.GetNode<Node3D>("RightArm");
        _leftFist = _leftArm.GetNode<Node3D>("Fist");
        _rightFist = _rightArm.GetNode<Node3D>("Fist");
        _leftLeg = rig.GetNode<Node3D>("LeftLeg");
        _rightLeg = rig.GetNode<Node3D>("RightLeg");
        _rest = _body.Position;
        _ringMaterial = EffectMaterial(new Color("75efd0"));
        _shieldMaterial = EffectMaterial(new Color(.32f, .84f, .77f, .16f));
        _runeMaterial = EffectMaterial(new Color("f3cf7e"));
        _ring = new MeshInstance3D {
            Name = "SlamPulse", Visible = false, TopLevel = true,
            Mesh = new TorusMesh { InnerRadius = .92f, OuterRadius = 1, Rings = 48, RingSegments = 6 },
            MaterialOverride = _ringMaterial, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        _shield = new MeshInstance3D {
            Name = "StoneShield", Visible = false, Position = new Vector3(0, 1.35f, 0),
            Scale = new Vector3(1.5f, 1.55f, 1.1f),
            Mesh = new SphereMesh { Radius = 1, Height = 2, RadialSegments = 12, Rings = 6 },
            MaterialOverride = _shieldMaterial, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        _rune = new MeshInstance3D {
            Name = "EmpowerRune", Visible = false, Position = new Vector3(0, .1f, 0),
            Mesh = new TorusMesh { InnerRadius = .7f, OuterRadius = .82f, Rings = 32, RingSegments = 6 },
            MaterialOverride = _runeMaterial, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        AddChild(_ring);
        AddChild(_shield);
        AddChild(_rune);
        _unit.StateChanged += OnStateChanged;
    }

    private static StandardMaterial3D EffectMaterial(Color color) => new() {
        AlbedoColor = color, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled
    };

    public override void _Process(double delta)
    {
        if (_unit.IsDying) { _ring.Hide(); _shield.Hide(); _rune.Hide(); SetProcess(false); return; }
        float dt = (float)delta;
        _time += dt;
        float distance = _positionKnown ? _unit.GlobalPosition.DistanceTo(_lastPosition) : 0;
        _positionKnown = true;
        _lastPosition = _unit.GlobalPosition;
        bool stepping = dt > 0 && distance < 1 && _unit.State.Activity is UnitActivity.Idle or UnitActivity.Guard;
        _walk = Mathf.MoveToward(_walk, stepping ? Mathf.Clamp(distance / dt / 3, 0, 1) : 0, dt * 6);
        if (distance < 1) _stride += distance * 4;
        _attack = Mathf.Min(1, _attack + dt / .6f);
        _slam = Mathf.Min(1, _slam + dt / .65f);
        float punch = Mathf.Sin(_attack * Mathf.Pi), slam = Mathf.Sin(_slam * Mathf.Pi);
        float gait = Mathf.Sin(_stride) * _walk;
        float dash = _unit.State.Activity == UnitActivity.Dash ? 1 : 0;
        _body.Position = _rest + Vector3.Up * (Mathf.Sin(_time * 1.7f) * .018f + Mathf.Abs(gait) * .045f - slam * .16f);
        _body.Rotation = new Vector3(-slam * .22f - dash * .22f, punch * -.17f, gait * .04f);
        _head.Rotation = new Vector3(slam * .12f + dash * .12f, Mathf.Sin(_time) * .035f, 0);
        _leftLeg.Rotation = new Vector3(gait * .42f, 0, 0);
        _rightLeg.Rotation = new Vector3(-gait * .42f, 0, 0);
        _leftArm.Rotation = new Vector3(-gait * .23f + slam * 1.6f + dash * .85f, 0, .08f);
        _rightArm.Rotation = new Vector3(gait * .23f + punch * 1.6f + slam * 1.6f + dash * .85f, 0, -.08f);
        _leftFist.Rotation = new Vector3(slam * .4f, 0, 0);
        _rightFist.Rotation = new Vector3(punch * .45f + slam * .4f, 0, 0);
        _shield.Visible = _unit.HealthBar?.Shield > 0;
        _shieldMaterial.AlbedoColor = _shieldEmpowered
            ? new Color(.95f, .81f, .49f, .22f) : new Color(.32f, .84f, .77f, .16f);
        _chargeFlash = Mathf.Max(0, _chargeFlash - dt);
        _rune.Visible = _unit.GolemEffects.EmpowerReady || _chargeFlash > 0;
        float runeSize = 1 + Mathf.Sin(_time * 3) * .06f + (_chargeFlash > 0 ? .25f : 0);
        _rune.Scale = new Vector3(runeSize, .5f, runeSize);
        if (_pulse < 1) {
            _pulse = Mathf.Min(1, _pulse + dt / .65f);
            float size = Mathf.Lerp(.4f, _radius, _pulse);
            _ring.Scale = new Vector3(size, .35f, size);
            var color = _ringMaterial.AlbedoColor; color.A = 1 - _pulse;
            _ringMaterial.AlbedoColor = color;
            _ring.Visible = _pulse < 1;
        }
    }

    private void OnStateChanged(UnitState state, bool swung)
    {
        if (swung) _attack = 0;
        if (state.Activity == UnitActivity.Stun) { _attack = _slam = 1; _walk = 0; }
    }

    public void PlaySkill(int slot, bool empowered = false)
    {
        if (_unit.IsDying) return;
        if (slot == 0) {
            _slam = _pulse = 0;
            _radius = empowered ? 5 : 3.5f;
            _ring.GlobalPosition = _unit.GlobalPosition + Vector3.Up * .08f;
            _ringMaterial.AlbedoColor = new Color(empowered ? "f3cf7e" : "75efd0");
            _ring.Show();
        }
        if (slot == 1) { _slam = 0; _shieldEmpowered = empowered; }
        if (slot == 2) { _attack = 0; if (empowered) _chargeFlash = .7f; }
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_unit)) _unit.StateChanged -= OnStateChanged;
        _ringMaterial?.Dispose();
        _shieldMaterial?.Dispose();
        _runeMaterial?.Dispose();
    }
}
