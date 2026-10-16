using Godot;
using System.Collections.Generic;

// One fixed multimesh pool per machine. Every cloud is reconstructed from the current server transition tick.
internal sealed class ArtilleryPressureSteam
{
    private const int CloudsPerVent = 10;
    private readonly Node3D _owner;
    private readonly List<Node3D> _vents = new();
    private readonly MultiMesh _clouds;
    private readonly MultiMeshInstance3D _display;
    private static readonly QuadMesh CloudMesh = new() { Size = Vector2.One };
    private static readonly ShaderMaterial CloudMaterial = new()
    {
        Shader = new Shader { Code = @"
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled;
varying vec2 cloud_seed;
float hash21(vec2 p) { return fract(sin(dot(p, vec2(127.1,311.7))) * 43758.5453); }
float cloud_noise(vec2 p) {
    vec2 i = floor(p), f = fract(p); f = f*f*(3.0-2.0*f);
    return mix(mix(hash21(i),hash21(i+vec2(1,0)),f.x),mix(hash21(i+vec2(0,1)),hash21(i+vec2(1,1)),f.x),f.y);
}
void vertex() {
    cloud_seed = INSTANCE_CUSTOM.xy;
    mat4 billboard = mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
    billboard[0] *= length(MODEL_MATRIX[0].xyz);
    billboard[1] *= length(MODEL_MATRIX[1].xyz);
    billboard[2] *= length(MODEL_MATRIX[2].xyz);
    MODELVIEW_MATRIX = VIEW_MATRIX * billboard;
}
void fragment() {
    vec2 p = UV * 2.0 - 1.0;
    float noise = cloud_noise(p * 3.1 + cloud_seed.x * 17.0 + vec2(cloud_seed.y * .7, 0));
    float edge = 1.0 - smoothstep(.32, 1.0, length(p) + (noise-.5)*.22);
    ALBEDO = COLOR.rgb;
    ALPHA = COLOR.a * edge * (.52 + noise*.48);
}" }
    };

    public ArtilleryPressureSteam(Node3D owner, Node3D vents)
    {
        _owner = owner;
        foreach (Node child in vents.GetChildren()) if (child is Node3D vent) _vents.Add(vent);
        _clouds = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true, UseCustomData = true,
            InstanceCount = _vents.Count * CloudsPerVent, Mesh = CloudMesh,
            CustomAabb = new Aabb(new Vector3(-6, -.2f, -6), new Vector3(12, 6, 12))
        };
        _display = new MultiMeshInstance3D
        {
            Name = "PressureSteam", Multimesh = _clouds, MaterialOverride = CloudMaterial, Visible = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        owner.AddChild(_display);
    }

    public void Update(Unit unit)
    {
        if (unit.IsDying || !unit.IsVisibleInTree() || unit.Siege is not SiegeSnapshot { Transitioning: true } state
            || unit.EffectTick < state.StartTick || unit.EffectTick >= state.EndTick)
        { Clear(); return; }

        float progress = (float)(unit.EffectTick - state.StartTick) / (state.EndTick - state.StartTick);
        bool visible = false;
        for (int ventIndex = 0; ventIndex < _vents.Count; ventIndex++)
        {
            Node3D vent = _vents[ventIndex];
            Vector3 outward = -vent.GlobalBasis.Z.Normalized(), sideways = outward.Cross(Vector3.Up);
            for (int cloud = 0; cloud < CloudsPerVent; cloud++)
            {
                int index = ventIndex * CloudsPerVent + cloud;
                float emission = progress * 4.5f - ventIndex * .043f - (cloud + .5f) / CloudsPerVent;
                float age = Mathf.PosMod(emission, 1), seed = index * .713f;
                float opacity = emission < 0 ? 0 : Mathf.Sin(age * Mathf.Pi) * .62f;
                float size = .30f + age * 2.25f;
                Vector3 position = vent.GlobalPosition + outward * (.10f + age * 3.2f)
                    + sideways * (Mathf.Sin(seed * 7 + age * 3) * age * .60f)
                    + Vector3.Up * (age * .72f);
                position.Y = Mathf.Max(position.Y, unit.GlobalPosition.Y + size * .40f);
                _clouds.SetInstanceTransform(index, new Transform3D(
                    Basis.Identity.Scaled(new Vector3(size * 1.38f, size, 1)), _owner.ToLocal(position)));
                float shade = .72f + .20f * (1 - age);
                _clouds.SetInstanceColor(index, new Color(shade, shade + .025f, shade + .04f, opacity));
                _clouds.SetInstanceCustomData(index, new Color(seed, age, 0, 0));
                visible |= opacity > .001f;
            }
        }
        _display.Visible = visible;
    }

    public void Clear() => _display.Visible = false;
}
