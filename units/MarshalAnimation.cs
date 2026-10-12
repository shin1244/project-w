using Godot;

// Authoritative state starts attacks and sustained command effects; all motion is cosmetic.
public partial class MarshalAnimation : Node3D, ISkillPresentation
{
    private Unit _unit;
    private Node3D _body, _head, _leftArm, _rightArm, _leftLeg, _rightLeg, _leftTail, _rightTail, _banner, _cloth, _pistol, _muzzle;
    private MeshInstance3D _flash, _smoke, _tracer, _slash, _shield, _pulse, _commandRing;
    private readonly MeshInstance3D[] _marchArrows = new MeshInstance3D[3];
    private StandardMaterial3D _flashMat, _smokeMat, _tracerMat, _slashMat, _shieldMat, _pulseMat, _commandMat;
    private Vector3 _lastPosition, _rest;
    private bool _positionKnown;
    private float _time, _stride, _walk, _attack = 1, _shot = 1, _bulwark = 1, _march = 1, _rally = 1, _wave = 1;
    private float _waveRadius;
    private Node3D _shotTarget;
    private Vector3 _shotPoint;
    private bool _hasShotTarget;

    public override void _Ready()
    {
        _unit = GetParent<Unit>();
        Node3D rig = _unit.GetNode<Node3D>("Visual/Rig");
        _body = rig.GetNode<Node3D>("Body"); _head = _body.GetNode<Node3D>("Head");
        _leftArm = _body.GetNode<Node3D>("LeftArm"); _rightArm = _body.GetNode<Node3D>("RightArm");
        _leftLeg = rig.GetNode<Node3D>("LeftLeg"); _rightLeg = rig.GetNode<Node3D>("RightLeg");
        _leftTail = _body.GetNode<Node3D>("LeftTail"); _rightTail = _body.GetNode<Node3D>("RightTail");
        _banner = _body.GetNode<Node3D>("Banner"); _cloth = _banner.GetNode<Node3D>("Cloth");
        _pistol = _leftArm.GetNode<Node3D>("Pistol"); _muzzle = _pistol.GetNode<Node3D>("Muzzle");
        _rest = _body.Position;
        _flashMat = Effect("fff0b0", 1, 1.5f); _smokeMat = Effect("b6bbbc", .25f);
        _tracerMat = Effect("ffe9a1", .75f, .7f); _slashMat = Effect("ecdfaf", .5f, .3f);
        _shieldMat = Effect("94cae3", .14f, .2f); _pulseMat = Effect("e4c27b", .65f, .3f);
        _commandMat = Effect("efd28d", .60f, .25f);
        _flash = Mesh(_muzzle, "MuzzleFlash", new SphereMesh { Radius = .15f, Height = .3f, RadialSegments = 8, Rings = 4 }, _flashMat);
        _smoke = Mesh(this, "PistolSmoke", new SphereMesh { Radius = .17f, Height = .34f, RadialSegments = 10, Rings = 4 }, _smokeMat, true);
        _tracer = Mesh(this, "PistolTracer", new CylinderMesh { TopRadius = .022f, BottomRadius = .008f, Height = 1, RadialSegments = 6 }, _tracerMat, true);
        _slash = Mesh(this, "SabreArc", Arc(), _slashMat, true);
        _shield = Mesh(this, "CommandShield", new SphereMesh { Radius = 1, Height = 2, RadialSegments = 16, Rings = 8 }, _shieldMat);
        _shield.Position = new Vector3(0, 1.35f, 0); _shield.Scale = new Vector3(.92f, 1.34f, .81f);
        _pulse = Mesh(this, "OrderPulse", new TorusMesh { InnerRadius = .96f, OuterRadius = 1, Rings = 64, RingSegments = 4 }, _pulseMat, true);
        _commandRing = Mesh(this, "CommandRing", new TorusMesh { InnerRadius = .89f, OuterRadius = .94f, Rings = 48, RingSegments = 4 }, _commandMat);
        _commandRing.Position = Vector3.Up * .065f;
        for (int i = 0; i < _marchArrows.Length; i++)
        {
            _marchArrows[i] = Mesh(_unit.GetNode<Node3D>("Visual"), "MarchArrow" + i, Chevron(), _commandMat);
            _marchArrows[i].Position = new Vector3(0, .08f, -.6f - i * .48f);
        }
        _unit.StateChanged += OnStateChanged;
    }

    public override void _Process(double delta)
    {
        if (_unit.IsDying)
        {
            foreach (MeshInstance3D mesh in new[] { _flash, _smoke, _tracer, _slash, _shield, _pulse, _commandRing }) mesh.Hide();
            foreach (MeshInstance3D arrow in _marchArrows) arrow.Hide();
            SetProcess(false); return;
        }
        float dt = (float)delta; _time += dt;
        float distance = _positionKnown ? _unit.GlobalPosition.DistanceTo(_lastPosition) : 0;
        _positionKnown = true; _lastPosition = _unit.GlobalPosition;
        bool moving = dt > 0 && distance < 1 && _unit.State.Activity != UnitActivity.Stun;
        _walk = Mathf.MoveToward(_walk, moving ? Mathf.Clamp(distance / dt / 3.2f, 0, 1) : 0, dt * 7);
        if (distance < 1) _stride += distance * 4.5f;
        _attack = Mathf.Min(1, _attack + dt / .63f); _shot = Mathf.Min(1, _shot + dt / .55f);
        _bulwark = Mathf.Min(1, _bulwark + dt / .8f); _march = Mathf.Min(1, _march + dt / .7f); _rally = Mathf.Min(1, _rally + dt / 1.1f);
        if (_shot < .8f && _hasShotTarget)
        {
            if (CanAimAt(_shotTarget)) _shotPoint = _shotTarget.GlobalPosition + Vector3.Up;
            else _shotTarget = null;
            Node3D visual = _unit.GetNode<Node3D>("Visual");
            Vector3 facing = _shotPoint; facing.Y = visual.GlobalPosition.Y;
            if (facing.DistanceSquaredTo(visual.GlobalPosition) > .0001f) visual.LookAt(facing, Vector3.Up);
        }
        float gait = Mathf.Sin(_stride) * _walk;
        float load = Window(_attack, 0, .19f, .34f), cut = Window(_attack, .12f, .38f, 1);
        float aim = Window(_shot, 0, .09f, 1), recoil = Window(_shot, .08f, .18f, .5f);
        float guard = Window(_bulwark, 0, .3f, 1), order = Window(_march, 0, .25f, 1), rally = Window(_rally, 0, .3f, 1);
        float rifleArm = aim * 1.07f - recoil * .20f;
        _body.Position = _rest + new Vector3(cut * .06f, Mathf.Abs(gait) * .065f + Mathf.Sin(_time * 1.7f) * .012f - cut * .05f + rally * .045f,
            load * .045f - cut * .13f + recoil * .095f);
        _body.Rotation = new Vector3(-_walk * .065f - cut * .10f + recoil * .06f - rally * .045f,
            load * -.28f + cut * .45f - aim * .14f, gait * .025f - guard * .02f);
        _head.Rotation = new Vector3(recoil * .035f - rally * .10f, -cut * .17f + aim * .11f + Mathf.Sin(_time * .65f) * .028f, 0);
        _leftLeg.Rotation = new Vector3(gait * .52f - cut * .18f, 0, -.035f - guard * .04f);
        _rightLeg.Rotation = new Vector3(-gait * .52f + cut * .15f, 0, .035f + guard * .04f);
        _rightArm.Rotation = new Vector3(-gait * .25f - load * .8f + cut * 1.8f + guard * .55f + order * .95f + rally * 2.8f,
            load * -.35f - cut * .65f, -.12f - load * .65f - cut * .18f - guard * .45f - rally * .19f);
        _leftArm.Rotation = new Vector3(gait * .24f + rifleArm + guard * .65f + order * .35f,
            aim * -.12f, .13f + guard * .30f + recoil * .045f);
        // Counter-rotate the pistol so raising the elbow still aims the barrel at the target.
        _pistol.Rotation = new Vector3(-rifleArm + recoil * .13f, aim * .12f, -aim * .11f);
        _leftTail.Rotation = new Vector3(-.07f - Mathf.Max(0, gait) * .35f - _walk * .10f, -.035f, .035f);
        _rightTail.Rotation = new Vector3(-.07f - Mathf.Max(0, -gait) * .35f - _walk * .10f, .035f, -.035f);
        _banner.Rotation = new Vector3(Mathf.Sin(_stride - .45f) * _walk * .045f - recoil * .055f, 0, Mathf.Sin(_time * 1.8f) * .025f - cut * .08f);
        _cloth.Rotation = new Vector3(Mathf.Sin(_time * 3.7f) * .035f + _walk * .06f, Mathf.Sin(_time * 3.2f) * .08f, Mathf.Sin(_time * 2.5f) * .018f);

        _flash.Visible = _shot is > .08f and < .29f;
        _flash.Scale = new Vector3(.8f, .8f, 2.5f) * (1 - Mathf.Clamp((_shot - .08f) / .23f, 0, 1) * .75f);
        _smoke.Visible = _shot is > .13f and < 1;
        if (_smoke.Visible)
        {
            _smoke.GlobalPosition = _muzzle.GlobalPosition - _pistol.GlobalBasis.Z * .17f + Vector3.Up * _shot * .24f;
            _smoke.Scale = Vector3.One * (.4f + _shot * 1.6f); Alpha(_smokeMat, (1 - _shot) * .28f);
        }
        _tracer.Visible = _shot is > .10f and < .26f;
        if (_tracer.Visible)
        {
            Vector3 from = _muzzle.GlobalPosition;
            float range = _hasShotTarget ? Mathf.Clamp(from.DistanceTo(_shotPoint), .6f, 8) : 4.5f;
            Vector3 direction = _hasShotTarget ? (_shotPoint - from).Normalized() : -_unit.GetNode<Node3D>("Visual").GlobalBasis.Z;
            _tracer.GlobalPosition = from + direction * (range * .5f);
            Vector3 axis = Vector3.Up.Cross(direction);
            _tracer.Quaternion = new Quaternion(axis.LengthSquared() > .00001f ? axis.Normalized() : Vector3.Right,
                Mathf.Acos(Mathf.Clamp(Vector3.Up.Dot(direction), -1, 1)));
            _tracer.Scale = new Vector3(1, range, 1);
        }
        _slash.Visible = _attack is > .2f and < .6f;
        if (_slash.Visible)
        {
            _slash.GlobalTransform = _unit.GetNode<Node3D>("Visual").GlobalTransform;
            _slash.GlobalPosition += Vector3.Up * 1.05f;
            _slash.RotateObjectLocal(Vector3.Forward, -.20f); Alpha(_slashMat, Mathf.Sin((_attack - .2f) / .4f * Mathf.Pi) * .55f);
        }
        _shield.Visible = _unit.HealthBar?.Shield > 0;
        _shield.Scale = new Vector3(.92f, 1.34f, .81f) * (1 + Mathf.Sin(_time * 2.7f) * .015f);
        bool marching = _unit.MarshalEffects.Marching(_unit.EffectTick);
        bool commanding = _unit.MarshalEffects.Rallying(_unit.EffectTick);
        _commandRing.Visible = commanding;
        _commandRing.Scale = Vector3.One * (1 + Mathf.Sin(_time * 2.4f) * .05f);
        for (int i = 0; i < _marchArrows.Length; i++)
        {
            _marchArrows[i].Visible = marching;
            _marchArrows[i].Scale = Vector3.One * (.8f + Mathf.Sin(_time * 6 - i * .9f) * .13f);
        }
        _wave = Mathf.Min(1, _wave + dt / .8f);
        _pulse.Visible = _wave < 1;
        if (_pulse.Visible) { _pulse.Scale = Vector3.One * (.35f + _wave * _waveRadius); Alpha(_pulseMat, (1 - _wave) * .6f); }
    }

    public void PlaySkill(int slot, bool empowered = false)
    {
        if (_unit.IsDying || slot is < 0 or > 3) return;
        if (slot == 0) { _shot = 0; _attack = 1; }
        if (slot == 1) _bulwark = 0;
        if (slot == 2) _march = 0;
        if (slot == 3) _rally = 0;
        if (slot > 0)
        {
            _wave = 0; _waveRadius = slot == 3 ? 6 : 5;
            _pulse.GlobalPosition = _unit.GlobalPosition + Vector3.Up * .075f;
        }
    }
    public void AimShot(Node3D target)
    {
        _hasShotTarget = CanAimAt(target);
        _shotTarget = _hasShotTarget ? target : null;
        if (_hasShotTarget) _shotPoint = target.GlobalPosition + Vector3.Up;
    }
    private static bool CanAimAt(Node3D target) => GodotObject.IsInstanceValid(target)
        && target.IsInsideTree() && target.IsVisibleInTree() && !target.IsQueuedForDeletion()
        && target is not Unit { IsDying: true };
    private void OnStateChanged(UnitState state, bool swung)
    {
        if (swung && _shot >= .8f) _attack = 0;
        if (state.Activity == UnitActivity.Stun) _attack = _shot = _bulwark = _march = _rally = 1;
    }
    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_unit)) _unit.StateChanged -= OnStateChanged;
        _shotTarget = null;
        _hasShotTarget = false;
        // These effects belong to the rig/visual tree rather than this component.
        RemoveExternalEffect(_flash);
        foreach (MeshInstance3D arrow in _marchArrows) RemoveExternalEffect(arrow);
        foreach (Material material in new Material[] { _flashMat, _smokeMat, _tracerMat, _slashMat, _shieldMat, _pulseMat, _commandMat }) material?.Dispose();
    }
    private static void RemoveExternalEffect(MeshInstance3D mesh)
    {
        if (!GodotObject.IsInstanceValid(mesh)) return;
        mesh.Hide();
        mesh.MaterialOverride = null;
        if (!mesh.IsQueuedForDeletion()) mesh.QueueFree();
    }
    private static float Window(float age, float start, float peak, float end)
    {
        if (age <= start || age >= end) return 0;
        float t = age < peak ? (age - start) / (peak - start) : 1 - (age - peak) / (end - peak);
        return t * t * (3 - 2 * t);
    }
    private static StandardMaterial3D Effect(string color, float alpha, float glow = 0)
    {
        var tint = new Color(color); tint.A = alpha;
        return new StandardMaterial3D { AlbedoColor = tint, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            EmissionEnabled = glow > 0, Emission = new Color(color), EmissionEnergyMultiplier = glow, DisableReceiveShadows = true };
    }
    private static void Alpha(StandardMaterial3D material, float alpha)
    { Color color = material.AlbedoColor; color.A = alpha; material.AlbedoColor = color; }
    private static MeshInstance3D Mesh(Node3D parent, string name, Mesh mesh, Material mat, bool topLevel = false)
    {
        var node = new MeshInstance3D { Name = name, Mesh = mesh, MaterialOverride = mat, Visible = false, TopLevel = topLevel,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        parent.AddChild(node); return node;
    }
    private static ArrayMesh Arc()
    {
        using var surface = new SurfaceTool(); surface.Begin(Godot.Mesh.PrimitiveType.Triangles);
        for (int i = 0; i < 20; i++)
        {
            float a = -.9f + i * .1f, b = a + .1f;
            Vector3 outerA = new(Mathf.Sin(a) * 1.6f, 0, -Mathf.Cos(a) * 1.6f), outerB = new(Mathf.Sin(b) * 1.6f, 0, -Mathf.Cos(b) * 1.6f);
            Vector3 innerA = outerA * .89f, innerB = outerB * .89f;
            surface.AddVertex(outerA); surface.AddVertex(outerB); surface.AddVertex(innerA);
            surface.AddVertex(innerA); surface.AddVertex(outerB); surface.AddVertex(innerB);
        }
        surface.GenerateNormals(); return surface.Commit();
    }
    private static ArrayMesh Chevron()
    {
        using var surface = new SurfaceTool(); surface.Begin(Godot.Mesh.PrimitiveType.Triangles);
        Vector3[] points = { new(-.27f,0,.13f),new(0,0,-.16f),new(.27f,0,.13f),new(.21f,0,.20f),new(0,0,-.02f),new(-.21f,0,.20f) };
        foreach (int i in new[] { 0, 1, 4, 0, 4, 5, 1, 2, 3, 1, 3, 4 }) surface.AddVertex(points[i]);
        surface.GenerateNormals(); return surface.Commit();
    }
}
