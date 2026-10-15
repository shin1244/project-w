using Godot;
using System.Collections.Generic;

// The server owns the dash path, speed, target and cooldown. This rig only presents its snapshot.
public partial class SteamKnightAnimation : Node
{
    private Unit _unit;
    private Node3D _rig, _leftLeg, _rightLeg, _leftArm, _rightArm, _body, _needle, _vent;
    private readonly List<Node3D> _gears = new(), _exits = new();
    private readonly List<MeshInstance3D> _wires = new(), _anchors = new(), _idlePuffs = new(), _burstPuffs = new();
    private Node3D _cables, _steam;
    private AudioStreamPlayer3D _releaseSound;
    private Vector3 _rigRest, _lastPosition, _releaseOrigin;
    private Basis _releaseBasis;
    private float _stride, _walk, _swing = 1, _clock, _chargeBlend, _burstAge = 10, _winchAngle;
    private uint _seenStart;
    private bool _ready, _observedInactive, _hadActive, _hasSeenStart;

    private static readonly SphereMesh PuffMesh = new() { Radius = .5f, Height = 1, RadialSegments = 8, Rings = 4 };
    private static readonly CylinderMesh CableMesh = new() { TopRadius = .014f, BottomRadius = .014f, Height = 1, RadialSegments = 5 };
    private static readonly StandardMaterial3D SteamMaterial = new()
    {
        AlbedoColor = new Color(.88f, .91f, .88f, .68f),
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
    };
    private static readonly StandardMaterial3D CableMaterial = new() { AlbedoColor = new Color("667680"), Metallic = .6f, Roughness = .6f };
    private static readonly StandardMaterial3D AnchorMaterial = new() { AlbedoColor = new Color("bbc1b6"), Metallic = .7f, Roughness = .5f };
    private static AudioStreamWav _releaseClip;

    public override void _Ready()
    {
        _unit = GetParent<Unit>();
        _rig = _unit.GetNode<Node3D>("Visual/Rig");
        _leftLeg = _rig.GetNode<Node3D>("LeftLeg"); _rightLeg = _rig.GetNode<Node3D>("RightLeg");
        _leftArm = _rig.GetNode<Node3D>("LeftArm"); _rightArm = _rig.GetNode<Node3D>("RightArm");
        _body = _rig.GetNode<Node3D>("Body");
        _needle = _body.GetNode<Node3D>("PressureGauge/Needle"); _vent = _body.GetNode<Node3D>("SteamVent");
        _rigRest = _rig.Position; _lastPosition = _unit.GlobalPosition;
        _cables = new Node3D { Name = "ChargeCables", TopLevel = true, Visible = false };
        _steam = new Node3D { Name = "ChargeSteam", TopLevel = true };
        AddChild(_cables); AddChild(_steam);
        foreach (string side in new[] { "LeftWinch", "RightWinch" })
        {
            _gears.Add(_rig.GetNode<Node3D>($"{side}/Gear"));
            _exits.Add(_rig.GetNode<Node3D>($"{side}/CableExit"));
            var wire = new MeshInstance3D { Name = side + "Wire", Mesh = CableMesh, MaterialOverride = CableMaterial };
            _cables.AddChild(wire); _wires.Add(wire);
            var anchor = new MeshInstance3D { Name = side + "Anchor",
                Mesh = new CylinderMesh { TopRadius = 0, BottomRadius = .095f, Height = .24f, RadialSegments = 5 },
                MaterialOverride = AnchorMaterial };
            _cables.AddChild(anchor); _anchors.Add(anchor);
            for (int i = 0; i < 3; i++)
            {
                var barb = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(.025f, .16f, .035f) }, MaterialOverride = CableMaterial };
                anchor.AddChild(barb);
                float a = i * Mathf.Tau / 3;
                barb.Position = new Vector3(Mathf.Cos(a) * .075f, -.07f, Mathf.Sin(a) * .075f);
                barb.Rotation = new Vector3(Mathf.Sin(a) * -.6f, 0, Mathf.Cos(a) * .6f);
            }
        }
        for (int i = 0; i < 3; i++) _idlePuffs.Add(NewPuff($"ReadySteam{i}"));
        for (int i = 0; i < 16; i++) _burstPuffs.Add(NewPuff($"ReleaseSteam{i}"));
        _releaseSound = new AudioStreamPlayer3D { Name = "PressureRelease", Stream = ReleaseClip(), VolumeDb = -18,
            MaxDistance = 26, UnitSize = 4 };
        AddChild(_releaseSound);
        _unit.StateChanged += OnStateChanged;
        _unit.KnightChargeChanged += OnChargeChanged;
        ProcessPriority = 10;
        _ready = true;
        OnChargeChanged(_unit);
        Pose(0, false);
    }

    private MeshInstance3D NewPuff(string name)
    {
        var puff = new MeshInstance3D { Name = name, Mesh = PuffMesh, MaterialOverride = SteamMaterial, Visible = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        _steam.AddChild(puff);
        return puff;
    }

    public override void _Process(double delta)
    {
        if (!_ready) return;
        float dt = Mathf.Max(0, (float)delta);
        Vector3 position = _unit.GlobalPosition;
        float distance = position.DistanceTo(_lastPosition); _lastPosition = position;
        if (_unit.IsDying || !_unit.IsVisibleInTree())
        {
            ClearEffects(); _observedInactive = false; _hadActive = false; _chargeBlend = 0;
            if (_unit.IsDying) SetProcess(false);
            return;
        }
        _clock += dt; _burstAge = Mathf.Min(10, _burstAge + dt);
        bool active = _unit.KnightCharge is { } charge && charge.ActiveAt(_unit.EffectTick);
        float moving = !active && dt > 0 && distance < 1 ? Mathf.Clamp(distance / dt / 5, 0, 1) : 0;
        _walk = Mathf.MoveToward(_walk, moving, dt * 10);
        if (!active && distance < 1) _stride += distance * 5;
        if (active && distance < 2) _winchAngle += distance / .14f;
        if (_hadActive && !active) ClearBurst();
        _hadActive = active;
        _chargeBlend = Mathf.MoveToward(_chargeBlend, active ? 1 : 0, dt * (active ? 14 : 7));
        Pose(dt, active);
        AnimateCables(active);
        AnimateSteam(active);
    }

    private void Pose(float dt, bool active)
    {
        float step = Mathf.Sin(_stride) * .40f * _walk * (1 - _chargeBlend);
        _swing = Mathf.Min(1, _swing + dt / .48f);
        float attack = Mathf.Sin(_swing * Mathf.Pi) * (1 - _chargeBlend);
        _leftLeg.Rotation = new Vector3(Mathf.Lerp(step, .95f, _chargeBlend), 0, -.06f * _chargeBlend);
        _rightLeg.Rotation = new Vector3(Mathf.Lerp(-step, .60f, _chargeBlend), 0, .06f * _chargeBlend);
        _leftArm.Rotation = new Vector3(Mathf.Lerp(-step * .25f - attack * .15f, .70f, _chargeBlend), 0, -.12f * _chargeBlend);
        _rightArm.Rotation = new Vector3(Mathf.Lerp(step * .5f - attack * 1.5f, -.60f, _chargeBlend), 0, -attack * .3f + .20f * _chargeBlend);
        _body.Rotation = new Vector3(0, step * .08f, 0);
        _rig.Rotation = new Vector3(-.30f * _chargeBlend, 0, 0);
        _rig.Position = _rigRest + Vector3.Up * (Mathf.Abs(Mathf.Cos(_stride)) * .025f * _walk);
        // Keep the rigid boot soles at ground level as the whole assembly leans into the slide.
        if (_chargeBlend > 0)
        {
            Vector3 left = _rig.Basis * (_leftLeg.Transform * new Vector3(0, -1.005f, -.16f));
            Vector3 right = _rig.Basis * (_rightLeg.Transform * new Vector3(0, -1.005f, -.16f));
            _rig.Position += Vector3.Up * (-Mathf.Min(left.Y, right.Y) + .045f) * _chargeBlend;
        }
        float pressure = 0;
        if (_unit.KnightCharge is { Enabled: true } snapshot)
            pressure = snapshot.IsReadyAt(_unit.EffectTick) ? 1 : active ? 1 - Mathf.Clamp(ChargeAge(snapshot) / .18f, 0, 1) : .22f;
        _needle.Rotation = new Vector3(0, 0, Mathf.DegToRad(Mathf.Lerp(120, -105, pressure)));
        if (active)
        {
            _gears[0].Rotation = new Vector3(-_winchAngle, 0, 0);
            _gears[1].Rotation = new Vector3(_winchAngle, 0, 0);
        }
    }

    private void AnimateCables(bool active)
    {
        bool visibleTarget = Available(_unit.KnightChargeTarget);
        _cables.Visible = active && visibleTarget;
        if (!_cables.Visible || _unit.KnightCharge is not { } snapshot) return;
        Vector3 anchor = new(snapshot.Anchor.X, .13f, snapshot.Anchor.Y);
        float launch = Mathf.SmoothStep(0, 1, Mathf.Clamp(ChargeAge(snapshot) / .06f, 0, 1));
        for (int i = 0; i < _wires.Count; i++)
        {
            Vector3 start = _exits[i].GlobalPosition;
            Vector3 end = start.Lerp(anchor + new Vector3((i == 0 ? -1 : 1) * .08f, 0, 0), launch);
            Vector3 direction = end - start;
            float length = direction.Length();
            _wires[i].Visible = length > .01f;
            _anchors[i].Visible = length > .01f;
            if (length <= .01f) continue;
            Quaternion rotation = new(Vector3.Up, direction / length);
            _wires[i].GlobalPosition = (start + end) * .5f;
            _wires[i].Quaternion = rotation;
            _wires[i].Scale = new Vector3(1, length, 1);
            _anchors[i].GlobalPosition = end;
            _anchors[i].Quaternion = rotation;
        }
    }

    private void AnimateSteam(bool active)
    {
        bool ready = _unit.KnightCharge is { } snapshot && snapshot.IsReadyAt(_unit.EffectTick);
        for (int i = 0; i < _idlePuffs.Count; i++)
        {
            MeshInstance3D puff = _idlePuffs[i];
            float t = Mathf.PosMod(_clock * .75f + i / 3f, 1);
            puff.Visible = ready && t < .92f;
            if (!puff.Visible) continue;
            puff.GlobalPosition = _vent.GlobalPosition + Vector3.Up * (.07f + t * .52f)
                + _vent.GlobalBasis.Z.Normalized() * (t * .10f);
            float size = .065f + t * .19f;
            puff.Scale = new Vector3(size, size * 1.4f, size);
            puff.Transparency = Mathf.Clamp(.34f + t * .66f, 0, 1);
        }
        bool blast = active && _burstAge < .62f;
        // Late snapshots show only a small current exhaust trail, never replay the launch cloud.
        for (int i = 0; i < _burstPuffs.Count; i++)
        {
            MeshInstance3D puff = _burstPuffs[i];
            puff.Visible = active && (blast || i < 4);
            if (!puff.Visible) continue;
            float phase = i / (float)_burstPuffs.Count;
            float t = blast ? Mathf.Clamp(_burstAge / .62f, 0, 1) : Mathf.PosMod(_clock * 2 + i / 4f, 1);
            Basis basis = blast ? _releaseBasis : _vent.GlobalBasis.Orthonormalized();
            Vector3 origin = blast ? _releaseOrigin : _vent.GlobalPosition;
            float angle = i * 2.39996f;
            Vector3 spread = new(Mathf.Cos(angle) * (.15f + t * .75f), .20f + t * .8f + phase * .30f,
                .10f + t * (1.25f + phase * .90f));
            puff.GlobalPosition = origin + basis * spread;
            float size = blast ? .20f + t * .83f + phase * .20f : .10f + t * .34f;
            puff.Scale = new Vector3(size, size * 1.15f, size);
            puff.Transparency = Mathf.Clamp((blast ? .05f : .4f) + t * .88f, 0, 1);
        }
    }

    private void OnChargeChanged(Unit unit)
    {
        if (!_ready) return;
        if (unit.IsDying || !unit.IsVisibleInTree()) { ClearEffects(); _observedInactive = false; return; }
        if (unit.KnightCharge is not { } snapshot) { ClearEffects(); return; }
        bool active = snapshot.ActiveAt(unit.EffectTick);
        if (!active)
        {
            _observedInactive = true; _hadActive = false; ClearEffects();
            Pose(0, false);
            return;
        }
        bool unseen = !_hasSeenStart || snapshot.StartTick != _seenStart;
        if (unseen)
        {
            _seenStart = snapshot.StartTick; _hasSeenStart = true;
            if (_observedInactive && unit.IsVisibleInTree() && !unit.IsDying && unit.EffectTick - snapshot.StartTick <= 2)
            {
                _burstAge = 0; _releaseOrigin = _vent.GlobalPosition; _releaseBasis = _vent.GlobalBasis.Orthonormalized();
                _releaseSound.GlobalPosition = _vent.GlobalPosition; _releaseSound.Play();
            }
            else _burstAge = 10;
        }
        _hadActive = true;
        if (unseen && unit.EffectTick > snapshot.StartTick + 2) _chargeBlend = 1;
        Pose(0, true); AnimateCables(true); AnimateSteam(true);
    }

    private void OnStateChanged(UnitState state, bool swung)
    {
        if (_ready && !_unit.IsDying && swung && state.Activity == UnitActivity.Attack
            && !(_unit.KnightCharge?.ActiveAt(_unit.EffectTick) ?? false)) _swing = 0;
    }

    private void ClearBurst() { _burstAge = 10; _releaseSound?.Stop(); foreach (var puff in _burstPuffs) puff.Hide(); }
    private void ClearEffects() { _cables?.Hide(); ClearBurst(); foreach (var puff in _idlePuffs) puff.Hide(); }
    private float ChargeAge(KnightChargeSnapshot snapshot) => _unit.EffectTick <= snapshot.StartTick ? 0
        : (_unit.EffectTick - snapshot.StartTick) / (float)InterpolationClock.TickRate;
    private static bool Available(Node3D target) => target != null && GodotObject.IsInstanceValid(target)
        && target.IsInsideTree() && target.IsVisibleInTree() && !target.IsQueuedForDeletion() && target is not Unit { IsDying: true };

    private static AudioStreamWav ReleaseClip()
    {
        if (_releaseClip != null) return _releaseClip;
        const int rate = 16000, count = 4480;
        byte[] data = new byte[count * 2];
        uint noise = 0x71ce293u;
        float previous = 0;
        for (int i = 0; i < count; i++)
        {
            noise ^= noise << 13; noise ^= noise >> 17; noise ^= noise << 5;
            float n = (noise & 65535) / 32767.5f - 1;
            float t = i / (float)count;
            float envelope = Mathf.Min(1, t * 24) * Mathf.Pow(1 - t, 2.3f);
            float value = (n - previous * .70f) * envelope * .34f;
            previous = n;
            short sample = (short)(Mathf.Clamp(value, -1, 1) * short.MaxValue);
            data[i * 2] = (byte)(sample & 255); data[i * 2 + 1] = (byte)((sample >> 8) & 255);
        }
        _releaseClip = new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = rate, Stereo = false, Data = data };
        return _releaseClip;
    }

    public override void _ExitTree()
    {
        if (!_ready || !GodotObject.IsInstanceValid(_unit)) return;
        ClearEffects();
        _unit.StateChanged -= OnStateChanged; _unit.KnightChargeChanged -= OnChargeChanged;
    }
}
