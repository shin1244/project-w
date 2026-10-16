using Godot;
using System;

// Heroes keep their original body materials. One persistent ring carries team and selection cues.
public sealed class HeroTeamRing : IDisposable
{
    private static readonly Color FriendlyColor = new("55c7ff");
    private static readonly Color EnemyColor = new("ff7169");
    private readonly MeshInstance3D _ring;
    private readonly Mesh _originalMesh;
    private readonly Material _originalOverride;
    private readonly TorusMesh _mesh;
    private readonly StandardMaterial3D _material;
    private bool _disposed;

    public HeroTeamRing(MeshInstance3D ring)
    {
        _ring = ring;
        _originalMesh = ring.Mesh;
        _originalOverride = ring.MaterialOverride;
        _mesh = new TorusMesh { InnerRadius = .65f, OuterRadius = .75f, Rings = 64, RingSegments = 8 };
        _material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = FriendlyColor
        };
        // Keep the hero scene's footprint and attachment to its visible world unit.
        ring.Mesh = _mesh;
        ring.MaterialOverride = _material;
        ring.Position = new Vector3(ring.Position.X, .08f, ring.Position.Z);
        ring.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
    }

    public void Apply(uint team, uint displayTeam, bool selected, bool dying)
    {
        if (_disposed) return;
        _ring.Visible = team != 0 && displayTeam != 0 && !dying;
        Color color = team == displayTeam ? FriendlyColor : EnemyColor;
        if (_material.AlbedoColor != color) _material.AlbedoColor = color;
        // Strengthen the same ring when selected; do not stack a second selection ring.
        float innerRadius = selected ? .60f : .65f;
        if (!Mathf.IsEqualApprox(_mesh.InnerRadius, innerRadius)) _mesh.InnerRadius = innerRadius;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (GodotObject.IsInstanceValid(_ring))
        {
            _ring.Hide();
            _ring.Mesh = _originalMesh;
            _ring.MaterialOverride = _originalOverride;
        }
        _mesh.Dispose();
        _material.Dispose();
    }
}
