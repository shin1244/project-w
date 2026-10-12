using Godot;
using System.Collections.Generic;

// 서버가 보낸 활성 상태만 표시하므로 실패한 입력·안개 재등장도 같은 상태를 보인다.
public partial class WolfEffectsVisual : Node3D, ISkillPresentation
{
    private Unit _unit;
    private MeshInstance3D _drain, _aura, _fill, _auraPulse, _devourRing;
    private readonly MeshInstance3D[] _fangs = new MeshInstance3D[2];
    private readonly MeshInstance3D[] _mist = new MeshInstance3D[6];
    private readonly MeshInstance3D[] _motes = new MeshInstance3D[5];
    private readonly List<StandardMaterial3D> _materials = new();
    private GradientTexture2D _softTexture;
    private float _time, _auraTime, _devour = 1;

    public override void _Ready()
    {
        _unit = GetParent<Unit>();
        _drain = Ring("DrainReady", .9f, new Color(1, .3f, .48f, .75f));
        _aura = Ring("DamageAura", 3.5f, new Color(.95f, .18f, .22f, .65f));
        _auraPulse = Ring("AuraPulse", 1, new Color(.9f, .13f, .2f, .35f));
        _devourRing = Ring("DevourPulse", 1, new Color(1, .38f, .3f, .65f));
        _fill = new MeshInstance3D
        {
            Name = "AuraFill", Visible = false, Position = new Vector3(0, .045f, 0),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Mesh = new CylinderMesh { TopRadius = 3.5f, BottomRadius = 3.5f, Height = .015f, RadialSegments = 64 },
            MaterialOverride = Material(new Color(.7f, .08f, .12f, .065f))
        };
        AddChild(_fill);
        _softTexture = new GradientTexture2D
        {
            Width = 32, Height = 32, Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(.5f, .5f), FillTo = new Vector2(1, .5f),
            Gradient = new Gradient
            {
                Colors = new[] { Colors.White, new Color(1, 1, 1, 0) },
                Offsets = new[] { 0f, 1f }
            }
        };
        Node3D head = _unit.GetNode<Node3D>("Visual/Rig/Body/Head");
        for (int i = 0; i < _fangs.Length; i++)
        {
            _fangs[i] = Wisp("FangGlow" + i, new Color(1, .16f, .25f, .65f), head);
            _fangs[i].Position = new Vector3(i == 0 ? -.2f : .2f, -.22f, -.87f);
            _fangs[i].Scale = new Vector3(.24f, .4f, .24f);
        }
        for (int i = 0; i < _mist.Length; i++)
            _mist[i] = Wisp("BloodMist" + i, new Color(.72f, .06f, .14f, .2f), this);
        for (int i = 0; i < _motes.Length; i++)
            _motes[i] = Wisp("DevourMote" + i, new Color(1, .23f, .2f, .7f), this);
    }

    private MeshInstance3D Ring(string name, float radius, Color color)
    {
        var ring = new MeshInstance3D
        {
            Name = name, Visible = false, Position = new Vector3(0, .09f, 0),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Mesh = new TorusMesh { InnerRadius = radius - .055f, OuterRadius = radius + .055f, Rings = 96, RingSegments = 6 },
            MaterialOverride = Material(color)
        };
        AddChild(ring);
        return ring;
    }

    private StandardMaterial3D Material(Color color)
    {
        var material = new StandardMaterial3D
        {
            AlbedoColor = color, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
        _materials.Add(material);
        return material;
    }

    private MeshInstance3D Wisp(string name, Color color, Node3D parent)
    {
        StandardMaterial3D material = Material(color);
        material.AlbedoTexture = _softTexture;
        material.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
        material.BillboardKeepScale = true;
        var wisp = new MeshInstance3D
        {
            Name = name, Visible = false, Mesh = new QuadMesh { Size = Vector2.One },
            MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        parent.AddChild(wisp);
        return wisp;
    }

    private static void Alpha(MeshInstance3D mesh, float alpha)
    {
        var material = (StandardMaterial3D)mesh.MaterialOverride;
        Color color = material.AlbedoColor;
        color.A = alpha;
        material.AlbedoColor = color;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _time += dt;
        _drain.Visible = !_unit.IsDying && _unit.WolfEffects.DrainReady;
        _aura.Visible = _fill.Visible = !_unit.IsDying && _unit.EffectTick < _unit.WolfEffects.AuraUntil;
        float pulse = 1 + Mathf.Sin(_time * 5) * .045f;
        _drain.Scale = new Vector3(pulse, 1, pulse);
        foreach (MeshInstance3D fang in _fangs)
        {
            fang.Visible = _drain.Visible;
            Alpha(fang, .55f + Mathf.Sin(_time * 5) * .12f);
        }

        // 바깥 테두리는 실제 3.5m 범위에 고정한다. 안쪽 안개와 파문만 움직인다.
        _auraTime = _aura.Visible ? _auraTime + dt : 0;
        _auraPulse.Visible = _aura.Visible;
        float ripple = (_auraTime * .85f) % 1;
        _auraPulse.Scale = new Vector3(1, .5f, 1) * Mathf.Lerp(.7f, 3.35f, ripple);
        Alpha(_auraPulse, .32f * Mathf.Sin(ripple * Mathf.Pi));
        Alpha(_aura, .53f + Mathf.Sin(_auraTime * 4) * .1f);
        for (int i = 0; i < _mist.Length; i++)
        {
            float phase = (_auraTime * .45f + i / (float)_mist.Length) % 1;
            float angle = i * Mathf.Tau / _mist.Length + _auraTime * .36f;
            float radius = 1.7f + Mathf.Sin(phase * Mathf.Pi) * .7f;
            _mist[i].Visible = _aura.Visible;
            _mist[i].Position = new Vector3(Mathf.Cos(angle) * radius, .25f + phase * .75f, Mathf.Sin(angle) * radius);
            _mist[i].Scale = new Vector3(1.2f, .6f, 1) * (.65f + phase * .5f);
            Alpha(_mist[i], .25f * Mathf.Sin(phase * Mathf.Pi));
        }

        _devour = Mathf.Min(1, _devour + dt / .7f);
        bool devouring = _devour < 1 && !_unit.IsDying;
        _devourRing.Visible = devouring;
        float size = Mathf.Lerp(1.65f, .3f, _devour);
        _devourRing.Scale = new Vector3(size, .6f, size);
        Alpha(_devourRing, (1 - _devour) * .6f);
        for (int i = 0; i < _motes.Length; i++)
        {
            float angle = i * Mathf.Tau / _motes.Length + _devour * 2;
            float radius = (1 - _devour) * 1.55f;
            _motes[i].Visible = devouring;
            _motes[i].Position = new Vector3(Mathf.Cos(angle) * radius, .25f + _devour * 1.35f, Mathf.Sin(angle) * radius);
            _motes[i].Scale = Vector3.One * (.3f - _devour * .18f);
            Alpha(_motes[i], Mathf.Sin(_devour * Mathf.Pi) * .75f);
        }
        if (_unit.IsDying) SetProcess(false);
    }

    public void PlaySkill(int slot, bool empowered = false)
    {
        if (_unit.IsDying) return;
        if (slot == 2) _auraTime = 0;
        if (slot == 3) _devour = 0;
    }

    public override void _ExitTree()
    {
        // 송곳니 빛은 리그에 붙어 있으므로 이 연출 노드만 제거할 때도 함께 정리한다.
        foreach (MeshInstance3D fang in _fangs)
            if (GodotObject.IsInstanceValid(fang) && !fang.IsQueuedForDeletion()) fang.QueueFree();
        foreach (StandardMaterial3D material in _materials) material.Dispose();
        _softTexture?.Dispose();
    }
}
