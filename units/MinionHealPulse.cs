using Godot;
using System;

// A short server-authorized effect independent of the dying model; it never changes HP.
public partial class MinionHealPulse : Node3D
{
    public float Radius { get; set; } = 4;
    public Func<Vector3, bool> VisibilityCheck { get; set; }
    private MeshInstance3D _ring;
    private StandardMaterial3D _material;
    private float _age;

    public override void _Ready()
    {
        _material = new StandardMaterial3D {
            AlbedoColor = new Color(.4f, 1, .63f, .85f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
        _ring = new MeshInstance3D {
            Name = "HealingWave",
            Mesh = new TorusMesh { InnerRadius = .965f, OuterRadius = 1, Rings = 64, RingSegments = 6 },
            MaterialOverride = _material, Scale = new Vector3(.35f, .35f, .35f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        AddChild(_ring);
    }

    public override void _Process(double delta)
    {
        _age += (float)delta;
        float progress = Mathf.Clamp(_age / .6f, 0, 1);
        float radius = Mathf.Lerp(.35f, Radius, 1 - (1 - progress) * (1 - progress));
        _ring.Scale = new Vector3(radius, .35f, radius);
        _material.AlbedoColor = new Color(.4f, 1, .63f, (1 - progress) * .85f);
        Visible = VisibilityCheck?.Invoke(GlobalPosition) ?? true;
        if (progress >= 1) QueueFree();
    }

    public override void _ExitTree()
    {
        VisibilityCheck = null;
        _material?.Dispose();
    }
}
