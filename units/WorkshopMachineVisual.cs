using Godot;
using System.Collections.Generic;

// Presentation only: all attack and siege timing comes from authoritative snapshots.
public partial class WorkshopMachineVisual : Node3D
{
    [Export] public bool Artillery { get; set; }
    private Unit _unit;
    private Node3D _rig, _body, _head, _barrel, _muzzle, _leftWheel, _rightWheel;
    private SteamArtilleryPose _artilleryPose;
    private ArtilleryPressureSteam _pressureSteam;
    private Vector3 _bodyRest, _barrelRest, _previousPosition;
    private readonly List<(MeshInstance3D Node, Node3D Vent, float Phase)> _steam = new();
    private readonly List<MeshInstance3D> _fire = new();
    private readonly List<(MultiMesh Belt, Curve3D Path, float Length)> _tracks = new();
    private Node3D _fireRoot, _shotTarget;
    private float _clock, _wheelAngle, _trackTravel, _walk, _gait, _shotAge = 10;
    private bool _ready, _siegeShot;
    private static readonly SphereMesh PuffMesh = new() { Radius = .5f, Height = 1, RadialSegments = 8, Rings = 4 };
    private static readonly StandardMaterial3D SteamMaterial = new()
    {
        AlbedoColor = new Color(.72f, .76f, .73f, .28f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, DisableFog = false
    };
    private static StandardMaterial3D FireMaterial(string color) => new()
    {
        AlbedoColor = new Color(color), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        EmissionEnabled = true, Emission = new Color(color), EmissionEnergyMultiplier = 1.3f
    };
    private static readonly StandardMaterial3D Orange = FireMaterial("f77b28");
    private static readonly StandardMaterial3D Yellow = FireMaterial("ffdf74");

    public override void _Ready()
    {
        _unit = GetParent<Unit>();
        _rig = _unit.GetNode<Node3D>("Visual/Rig");
        if (Artillery)
        {
            _artilleryPose = new SteamArtilleryPose(_rig);
            _pressureSteam = new ArtilleryPressureSteam(this, _rig.GetNode<Node3D>("Hull/PressureVents"));
            _muzzle = _artilleryPose.MobileMuzzle;
            ApplyArtilleryPose(0);
        }
        else
        {
            _body = _rig.GetNode<Node3D>("Body");
            _head = _rig.GetNodeOrNull<Node3D>("Head");
            _barrel = _rig.GetNode<Node3D>("Nozzle");
            _muzzle = _barrel.GetNode<Node3D>("Muzzle");
            _leftWheel = _rig.GetNode<Node3D>("LeftWheel");
            _rightWheel = _rig.GetNode<Node3D>("RightWheel");
            _bodyRest = _body.Position; _barrelRest = _barrel.Position;
        }
        _previousPosition = _unit.GlobalPosition;
        if (!Artillery)
            foreach (string side in new[] { "LeftTrack", "RightTrack" })
            {
                var path = _rig.GetNodeOrNull<Path3D>($"{side}/TrackPath");
                var belt = _rig.GetNodeOrNull<MultiMeshInstance3D>($"{side}/Belt");
                if (path?.Curve == null || belt?.Multimesh == null || path.Curve.GetBakedLength() <= 0) continue;
                // Mutable tread transforms belong to this unit, never to the cached scene resource.
                MultiMesh source = belt.Multimesh;
                var instance = new MultiMesh
                {
                    TransformFormat = source.TransformFormat, UseColors = source.UseColors, UseCustomData = source.UseCustomData,
                    InstanceCount = source.InstanceCount, Mesh = source.Mesh, CustomAabb = source.CustomAabb,
                    VisibleInstanceCount = source.VisibleInstanceCount
                };
                // The headless renderer has no GPU instance buffer to copy.
                float[] buffer = source.Buffer;
                if (buffer.Length > 0) instance.Buffer = buffer;
                belt.Multimesh = instance;
                _tracks.Add((belt.Multimesh, path.Curve, path.Curve.GetBakedLength()));
            }
        string ventRoot = Artillery ? "Hull/Chassis" : "Body";
        for (int side = 0; side < 2; side++)
        {
            Node3D vent = _rig.GetNode<Node3D>($"{ventRoot}/Steam{(side == 0 ? "Left" : "Right")}");
            for (int i = 0; i < 3; i++)
            {
                var puff = new MeshInstance3D { Mesh = PuffMesh, MaterialOverride = SteamMaterial,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
                AddChild(puff);
                _steam.Add((puff, vent, i / 3f + side * .13f));
            }
        }
        _fireRoot = new Node3D { Name = "MuzzleFire", Visible = false, TopLevel = true };
        AddChild(_fireRoot);
        for (int i = 0; i < (Artillery ? 3 : 7); i++)
        {
            var flame = new MeshInstance3D { Mesh = PuffMesh, MaterialOverride = i % 2 == 0 ? Orange : Yellow,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            _fireRoot.AddChild(flame); _fire.Add(flame);
        }
        _unit.StateChanged += OnStateChanged;
        _unit.SiegeChanged += OnSiegeChanged;
        ProcessPriority = 10;
        _ready = true;
    }

    public override void _Process(double delta)
    {
        if (_unit.IsDying) { ClearEffects(); SetProcess(false); return; }
        float dt = (float)delta;
        _clock += dt; _shotAge += dt;
        Vector3 position = _unit.GlobalPosition;
        float distance = position.DistanceTo(_previousPosition);
        _previousPosition = position;
        float move = distance < 1 && dt > 0 ? distance / dt : 0;
        _walk = Mathf.Lerp(_walk, Mathf.Clamp(move / 2f, 0, 1), Mathf.Min(1, dt * 10));
        if (Artillery)
        {
            if (distance < 1 && _unit.Siege?.Mode is null or SiegeMode.Mobile) _gait += distance * 5.2f;
            ApplyArtilleryPose(dt);
            _pressureSteam.Update(_unit);
        }
        else
        {
            if (distance < 1) _wheelAngle += distance / -.19f;
            _leftWheel.Rotation = _rightWheel.Rotation = new Vector3(_wheelAngle, 0, 0);
            _body.Position = _bodyRest + Vector3.Up * Mathf.Sin(_clock * 15) * .015f * _walk;
            if (_head != null) _head.Rotation = new Vector3(0, Mathf.Sin(_clock * .7f) * .035f, 0);
            _barrel.Position = _barrelRest + Vector3.Back * Recoil * .06f;
        }
        if (_tracks.Count > 0 && distance > .00001f && distance < 1)
        {
            _trackTravel += distance;
            foreach (var (belt, path, length) in _tracks)
                for (int i = 0; i < belt.InstanceCount; i++)
                {
                    float offset = Mathf.PosMod(i * length / belt.InstanceCount - _trackTravel, length);
                    Vector3 point = path.SampleBaked(offset);
                    Vector3 tangent = path.SampleBaked(Mathf.PosMod(offset + .025f, length))
                        - path.SampleBaked(Mathf.PosMod(offset - .025f, length));
                    var basis = new Basis(Vector3.Right, -Mathf.Atan2(tangent.Y, tangent.Z));
                    belt.SetInstanceTransform(i, new Transform3D(basis, point));
                }
        }
        foreach (var (puff, vent, phase) in _steam)
        {
            float t = Mathf.PosMod(_clock * .60f + phase, 1);
            puff.Visible = t < .90f && _unit.IsVisibleInTree();
            puff.GlobalPosition = vent.GlobalPosition + new Vector3(Mathf.Sin(t * 5 + phase) * .07f, t * .65f, t * .10f);
            float size = (.07f + t * .25f) * Mathf.Min(1, (1-t)*5);
            puff.Scale = new Vector3(size, size * 1.35f, size);
        }
        UpdateFire();
    }

    private float Recoil => Mathf.Exp(-_shotAge * 14) * Mathf.Sin(Mathf.Min(_shotAge * 32, Mathf.Pi));
    private void ApplyArtilleryPose(float dt)
    {
        Node3D target = _unit.ActionTarget;
        bool mobileAttacking = _unit.State.Activity == UnitActivity.Attack && _unit.Siege?.Mode is null or SiegeMode.Mobile;
        _artilleryPose.Apply(_unit.Siege?.DeploymentAt(_unit.EffectTick) ?? 0, _gait, _walk, Recoil, _siegeShot, dt,
            _unit.State.FocusId == 0 && _walk > .1f, mobileAttacking, VisibleTarget(target) ? target.GlobalPosition : null);
    }

    private static bool VisibleTarget(Node3D target) => target != null && GodotObject.IsInstanceValid(target)
        && target.IsInsideTree() && !target.IsQueuedForDeletion() && target.IsVisibleInTree()
        && (target is not Unit unit || !unit.IsDying);

    private void OnStateChanged(UnitState state, bool swung)
    {
        if (Artillery && !_unit.IsDying) ApplyArtilleryPose(0);
        if (!swung || state.Activity != UnitActivity.Attack || _unit.IsDying) return;
        // A late attack message must not fire a weapon while its carriage is folding or unfolding.
        if (Artillery && _unit.Siege is { Transitioning: true }) { ClearShot(); return; }
        Node3D target = _unit.ActionTarget;
        if (!VisibleTarget(target)) { ClearShot(); return; }
        _shotTarget = target; _shotAge = 0;
        if (Artillery)
        {
            _siegeShot = _unit.Siege?.Mode == SiegeMode.Deployed;
            ApplyArtilleryPose(0); // STATE already turned Aim; launch from this mode's current muzzle immediately.
            _muzzle = _siegeShot ? _artilleryPose.SiegeMuzzle : _artilleryPose.MobileMuzzle;
            var shell = new WorkshopShellFlight { Name = "WorkshopShell" };
            _unit.AddChild(shell);
            shell.Launch(_muzzle.GlobalPosition, TargetCenter(target), target);
        }
        UpdateFire();
    }

    private static Vector3 TargetCenter(Node3D target) => target.GlobalPosition + Vector3.Up *
        (target is Unit unit ? unit.HealthBarHeight * .42f : 1.1f);

    private void UpdateFire()
    {
        float duration = Artillery ? .13f : .30f;
        if (_shotAge >= duration || !VisibleTarget(_shotTarget) || !_unit.IsVisibleInTree()) { ClearShot(); return; }
        Vector3 end = TargetCenter(_shotTarget), start = _muzzle.GlobalPosition;
        Vector3 direction = end - start;
        if (direction.LengthSquared() < .01f) { ClearShot(); return; }
        _fireRoot.Visible = true; _fireRoot.GlobalPosition = start;
        _fireRoot.LookAt(end, Mathf.Abs(direction.Normalized().Dot(Vector3.Up)) > .99f ? Vector3.Right : Vector3.Up);
        float strength = Mathf.Sin(Mathf.Pi * Mathf.Clamp(_shotAge / duration, .05f, .99f));
        float length = Artillery ? (_siegeShot ? 1.05f : .42f) : Mathf.Min(direction.Length(), 2.3f) * (.45f + strength * .55f);
        for (int i = 0; i < _fire.Count; i++)
        {
            float fraction = (i + .5f) / _fire.Count;
            float width = (.12f + fraction * .18f) * (.55f + strength * .45f) * (Artillery && _siegeShot ? 1.55f : 1);
            _fire[i].Position = new Vector3(Mathf.Sin(_clock * 55 + i * 2) * .025f, Mathf.Sin(i * 3 + _clock * 40) * .025f, -length * fraction);
            _fire[i].Scale = new Vector3(width, width, length / _fire.Count * 2.3f);
        }
    }

    private void OnSiegeChanged(Unit unit)
    {
        ClearShot();
        if (Artillery)
        {
            if (!unit.IsDying) ApplyArtilleryPose(0);
            _pressureSteam.Update(unit);
        }
    }
    private void ClearShot() { _shotTarget = null; if (_fireRoot != null) _fireRoot.Visible = false; }
    private void ClearEffects()
    {
        ClearShot(); _pressureSteam?.Clear();
        foreach (var (puff, _, _) in _steam) puff.Visible = false;
    }
    public override void _ExitTree()
    {
        if (!_ready || !GodotObject.IsInstanceValid(_unit)) return;
        _unit.StateChanged -= OnStateChanged; _unit.SiegeChanged -= OnSiegeChanged;
    }
}
