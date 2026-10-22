using Godot;

// Server state drives attacks/dashes; the model and effects only present them.
public partial class GolemAnimation : Node3D, ISkillPresentation
{
    private Unit _unit;
    private Node3D _body, _head, _leftArm, _rightArm, _leftFist, _rightFist, _leftLeg, _rightLeg;
    private Vector3 _rest, _lastPosition;
    private bool _positionKnown, _shieldEmpowered, _leftPunch;
    private float _time, _stride, _walk, _dash, _attack = 1, _slam = 1, _armor = 1, _awaken = 1;
    private float _pulse = 1, _radius, _chargeFlash, _debris = 1, _debrisStrength;
    private MeshInstance3D _ring, _shield, _rune;
    private Node3D _debrisRoot;
    private readonly MeshInstance3D[] _stones = new MeshInstance3D[8];
    private readonly MeshInstance3D[] _dust = new MeshInstance3D[4];
    private readonly MeshInstance3D[] _shieldPlates = new MeshInstance3D[6];
    private readonly MeshInstance3D[] _runeMarks = new MeshInstance3D[6];
    private readonly MeshInstance3D[] _smoke = new MeshInstance3D[6];
    private StandardMaterial3D _ringMaterial, _shieldMaterial, _runeMaterial, _stoneMaterial, _dustMaterial, _plateMaterial, _smokeMaterial;

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
        _stoneMaterial = EffectMaterial(new Color("879b98"));
        _dustMaterial = EffectMaterial(new Color(.53f, .63f, .6f, .2f));
        _plateMaterial = EffectMaterial(new Color(.4f, .85f, .77f, .5f));
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
        for (int i = 0; i < _shieldPlates.Length; i++) {
            _shieldPlates[i] = EffectMesh("ArmorPlate" + i,
                new BoxMesh { Size = new Vector3(.48f, .62f, .055f) }, _plateMaterial);
            AddChild(_shieldPlates[i]);
            _runeMarks[i] = EffectMesh("RuneMark" + i,
                new BoxMesh { Size = new Vector3(.11f, .018f, .22f) }, _runeMaterial);
            AddChild(_runeMarks[i]);
        }
        // Small reusable bursts keep the silhouette readable without persistent debris.
        _debrisRoot = new Node3D { Name = "StoneBurst", TopLevel = true };
        AddChild(_debrisRoot);
        var stoneMesh = new BoxMesh { Size = new Vector3(.15f, .2f, .13f) };
        for (int i = 0; i < _stones.Length; i++) {
            _stones[i] = EffectMesh("Chip" + i, stoneMesh, _stoneMaterial);
            _debrisRoot.AddChild(_stones[i]);
        }
        var dustMesh = new SphereMesh { Radius = 1, Height = 2, RadialSegments = 8, Rings = 4 };
        for (int i = 0; i < _dust.Length; i++) {
            _dust[i] = EffectMesh("Dust" + i, dustMesh, _dustMaterial);
            _debrisRoot.AddChild(_dust[i]);
        }
        // Murky smoke from the two chimneys shows the boiler on the back is running.
        _smokeMaterial = new StandardMaterial3D { AlbedoColor = new Color("8a8580"), Roughness = 1 };
        var smokeMesh = new SphereMesh { Radius = .5f, Height = 1, RadialSegments = 8, Rings = 4 };
        for (int i = 0; i < _smoke.Length; i++) {
            _smoke[i] = EffectMesh("ChimneySmoke" + i, smokeMesh, _smokeMaterial);
            _smoke[i].TopLevel = true;
            AddChild(_smoke[i]);
        }
        _unit.StateChanged += OnStateChanged;
    }

    private static MeshInstance3D EffectMesh(string name, Mesh mesh, Material material) => new() {
        Name = name, Visible = false, Mesh = mesh, MaterialOverride = material,
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
    };

    private static StandardMaterial3D EffectMaterial(Color color) => new() {
        AlbedoColor = color, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled
    };

    public override void _Process(double delta)
    {
        if (_unit.IsDying) {
            _ring.Hide(); _shield.Hide(); _rune.Hide(); _debrisRoot.Hide();
            foreach (var plate in _shieldPlates) plate.Hide();
            foreach (var mark in _runeMarks) mark.Hide();
            foreach (var puff in _smoke) puff.Hide();
            SetProcess(false);
            return;
        }
        float dt = (float)delta;
        _time += dt;
        float distance = _positionKnown ? _unit.GlobalPosition.DistanceTo(_lastPosition) : 0;
        _positionKnown = true;
        _lastPosition = _unit.GlobalPosition;
        bool stepping = dt > 0 && distance < 1 && _unit.State.Activity is UnitActivity.Idle or UnitActivity.Guard;
        _walk = Mathf.MoveToward(_walk, stepping ? Mathf.Clamp(distance / dt / 3, 0, 1) : 0, dt * 6);
        if (distance < 1) _stride += distance * 4;
        _attack = Mathf.Min(1, _attack + dt / .62f);
        _slam = Mathf.Min(1, _slam + dt / .72f);
        _armor = Mathf.Min(1, _armor + dt / .65f);
        _awaken = Mathf.Min(1, _awaken + dt / .8f);
        _dash = Mathf.MoveToward(_dash, _unit.State.Activity == UnitActivity.Dash ? 1 : 0, dt * 12);
        // A short load, quick strike and longer settle make stone feel heavy.
        float load = Envelope(_attack, .2f, .36f);
        float punch = Strike(_attack, .2f, .4f);
        float raise = Envelope(_slam, .22f, .46f);
        float slam = Strike(_slam, .26f, .44f);
        float armor = Envelope(_armor, .26f, 1);
        float awaken = Envelope(_awaken, .3f, 1);
        float hand = _leftPunch ? -1 : 1;
        float gait = Mathf.Sin(_stride) * _walk;
        float brace = Mathf.Max(slam, Mathf.Max(punch * .65f, armor * .5f));
        _body.Position = _rest + new Vector3(hand * (punch * .045f - load * .025f),
            Mathf.Sin(_time * 1.7f) * .018f + Mathf.Abs(gait) * .055f + raise * .08f
                - slam * .2f - punch * .065f - armor * .07f + awaken * .075f,
            load * .065f - punch * .15f - slam * .075f - _dash * .09f);
        _body.Rotation = new Vector3(load * .07f - punch * .13f + raise * .12f - slam * .3f - _dash * .3f - awaken * .065f,
            hand * (load * .19f - punch * .29f), gait * .05f - hand * punch * .045f);
        _head.Rotation = new Vector3(punch * .1f - raise * .1f + slam * .15f + _dash * .18f - awaken * .1f,
            Mathf.Sin(_time) * .025f + hand * punch * .12f, 0);
        _leftLeg.Rotation = new Vector3(gait * .46f - brace * .12f + _dash * .2f, 0, -brace * .075f);
        _rightLeg.Rotation = new Vector3(-gait * .46f + brace * .13f - _dash * .22f, 0, brace * .075f);
        float leftPunch = _leftPunch ? punch : punch * .12f;
        float rightPunch = _leftPunch ? punch * .12f : punch;
        float leftLoad = _leftPunch ? load : 0, rightLoad = _leftPunch ? 0 : load;
        float skillArm = raise * 2.3f + slam * .52f + armor * .78f + _dash * .95f + awaken * .25f;
        _leftArm.Rotation = new Vector3(-gait * .26f - leftLoad * .38f + leftPunch * 1.7f + skillArm,
            armor * -.28f, .08f + armor * .43f - awaken * .27f);
        _rightArm.Rotation = new Vector3(gait * .26f - rightLoad * .38f + rightPunch * 1.7f + skillArm,
            armor * .28f, -.08f - armor * .43f + awaken * .27f);
        _leftFist.Rotation = new Vector3(leftPunch * .38f + raise * .3f + armor * .35f, 0, 0);
        _rightFist.Rotation = new Vector3(rightPunch * .38f + raise * .3f + armor * .35f, 0, 0);
        _shield.Visible = _unit.HealthBar?.Shield > 0;
        _shieldMaterial.AlbedoColor = _shieldEmpowered
            ? new Color(.95f, .81f, .49f, .13f) : new Color(.32f, .84f, .77f, .09f);
        _plateMaterial.AlbedoColor = _shieldEmpowered
            ? new Color(.95f, .81f, .49f, .5f) : new Color(.4f, .85f, .77f, .42f);
        _chargeFlash = Mathf.Max(0, _chargeFlash - dt);
        _rune.Visible = _unit.GolemEffects.EmpowerReady || _chargeFlash > 0;
        float runeSize = 1 + Mathf.Sin(_time * 3) * .04f + awaken * .3f + (_chargeFlash > 0 ? .16f : 0);
        _rune.Scale = new Vector3(runeSize, .5f, runeSize);
        for (int i = 0; i < _shieldPlates.Length; i++) {
            float angle = Mathf.Tau * i / _shieldPlates.Length + _time * .22f;
            _shieldPlates[i].Visible = _shield.Visible;
            _shieldPlates[i].Position = new Vector3(Mathf.Sin(angle) * 1.23f,
                1.25f + Mathf.Sin(_time * 2 + i) * .035f, Mathf.Cos(angle) * 1.05f);
            _shieldPlates[i].Rotation = new Vector3(0, angle, .18f);
            float markAngle = Mathf.Tau * i / _runeMarks.Length - _time * .3f;
            _runeMarks[i].Visible = _rune.Visible;
            _runeMarks[i].Position = new Vector3(Mathf.Sin(markAngle), .105f, Mathf.Cos(markAngle)) * runeSize;
            _runeMarks[i].Rotation = new Vector3(0, markAngle + Mathf.Pi / 4, 0);
        }
        if (_pulse < 1) {
            _pulse = Mathf.Min(1, _pulse + dt / .65f);
            float size = Mathf.Lerp(.4f, _radius, _pulse);
            _ring.Scale = new Vector3(size, .35f, size);
            var color = _ringMaterial.AlbedoColor; color.A = 1 - _pulse;
            _ringMaterial.AlbedoColor = color;
            _ring.Visible = _pulse < 1;
        }
        UpdateDebris(dt);
        UpdateSmoke();
    }

    private void UpdateSmoke()
    {
        for (int i = 0; i < _smoke.Length; i++) {
            float side = i % 2 == 0 ? -1 : 1;
            float age = Mathf.PosMod(_time * .45f + (float)i / _smoke.Length, 1);
            // Chimney tops in Body space, matching tools/build_rune_golem.gd.
            Vector3 top = _body.ToGlobal(new Vector3(side * .21f, 1.37f, .64f));
            _smoke[i].GlobalPosition = top + new Vector3(age * .25f, age * 1.1f, age * .2f);
            _smoke[i].Scale = Vector3.One * (.18f + age * .45f);
            _smoke[i].Transparency = Mathf.Lerp(.35f, 1, age);
            _smoke[i].Visible = true;
        }
    }

    private static float Smooth(float value) { value = Mathf.Clamp(value, 0, 1); return value * value * (3 - 2 * value); }
    private static float Envelope(float time, float peak, float end) => time < peak
        ? Smooth(time / peak) : 1 - Smooth((time - peak) / (end - peak));
    private static float Strike(float time, float start, float peak) => time < start ? 0
        : time < peak ? Smooth((time - start) / (peak - start)) : 1 - Smooth((time - peak) / (1 - peak));

    private void Burst(float strength)
    {
        _debris = 0;
        _debrisStrength = strength;
        _debrisRoot.GlobalPosition = _unit.GlobalPosition + Vector3.Up * .06f;
    }

    private void UpdateDebris(float dt)
    {
        if (_debris >= 1) return;
        _debris = Mathf.Min(1, _debris + dt / .65f);
        float age = _debris;
        var stoneColor = _stoneMaterial.AlbedoColor; stoneColor.A = 1 - Smooth((age - .5f) * 2);
        _stoneMaterial.AlbedoColor = stoneColor;
        var dustColor = _dustMaterial.AlbedoColor; dustColor.A = .2f * (1 - age);
        _dustMaterial.AlbedoColor = dustColor;
        for (int i = 0; i < _stones.Length; i++) {
            float angle = Mathf.Tau * i / _stones.Length + .3f;
            float reach = (.4f + age * (1.15f + i % 3 * .17f)) * _debrisStrength;
            _stones[i].Position = new Vector3(Mathf.Sin(angle) * reach,
                Mathf.Sin(age * Mathf.Pi) * (.3f + i % 3 * .12f) * _debrisStrength, Mathf.Cos(angle) * reach);
            _stones[i].Rotation = new Vector3(age * 3 + i, i, age * 4);
            _stones[i].Scale = Vector3.One * (.65f + i % 3 * .16f);
            _stones[i].Visible = age < 1;
        }
        for (int i = 0; i < _dust.Length; i++) {
            float angle = Mathf.Tau * i / _dust.Length + .65f;
            float reach = (.45f + age * .9f) * _debrisStrength;
            _dust[i].Position = new Vector3(Mathf.Sin(angle) * reach, .1f + age * .12f, Mathf.Cos(angle) * reach);
            _dust[i].Scale = new Vector3(.45f + age * .48f, .13f + age * .12f, .4f + age * .42f) * _debrisStrength;
            _dust[i].Visible = age < 1;
        }
    }

    private void OnStateChanged(UnitState state, bool swung)
    {
        if (swung) { _attack = 0; _leftPunch = (state.SwingSequence & 1) == 0; }
        if (state.Activity == UnitActivity.Stun) { _attack = _slam = _armor = _awaken = 1; _walk = 0; }
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
            Burst(empowered ? 1.3f : 1);
        }
        if (slot == 1) { _armor = 0; _shieldEmpowered = empowered; }
        if (slot == 2) { Burst(.65f); if (empowered) _chargeFlash = .7f; }
        if (slot == 3) _awaken = 0;
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_unit)) _unit.StateChanged -= OnStateChanged;
        _ringMaterial?.Dispose();
        _shieldMaterial?.Dispose();
        _runeMaterial?.Dispose();
        _stoneMaterial?.Dispose();
        _dustMaterial?.Dispose();
        _plateMaterial?.Dispose();
        _smokeMaterial?.Dispose();
    }
}
