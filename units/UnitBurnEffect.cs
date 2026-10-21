using Godot;
using System.Collections.Generic;

// Presentation only, for a unit the flame automaton killed (DEATH ... FIRE): the whole model chars black
// under rising embers, then slumps into a small heap of ash that fades. It works on any rig because it only
// darkens and squashes the meshes; nothing is clickable and the unit root never moves (docs/combat-feedback.md).
public static class UnitBurnEffect
{
    // The ash has landed by AshLandSeconds, holds for the unit's UnitDeathEffect.Hold, then fades over AshFadeSeconds.
    private const float CharEnd = .55f, CrumbleEnd = 1.15f, AshLandSeconds = 1.2f, AshFadeSeconds = .4f, StepSeconds = 1 / 60f;

    private static readonly SphereMesh EmberMesh = new() { Radius = .05f, Height = .1f, RadialSegments = 6, Rings = 3 };
    private static readonly BoxMesh GrainMesh = new() { Size = new Vector3(.08f, .06f, .08f) };
    private static readonly SphereMesh PuffMesh = new() { Radius = .5f, Height = 1, RadialSegments = 8, Rings = 4 };
    private static readonly StandardMaterial3D Ember = new()
    {
        AlbedoColor = new Color("ff8a3d"), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        EmissionEnabled = true, Emission = new Color("ff8a3d"), EmissionEnergyMultiplier = 1.6f
    };
    private static readonly StandardMaterial3D Soot = new() { AlbedoColor = new Color("2a2622"), Roughness = 1 };
    private static readonly StandardMaterial3D Cinder = new() { AlbedoColor = new Color("7d7a74"), Roughness = 1 };
    private static readonly StandardMaterial3D Dust = new()
    {
        AlbedoColor = new Color(.49f, .48f, .45f, .6f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
    };

    // Null means the caller should play the plain fall instead.
    public static Tween TryPlay(Unit unit)
    {
        if (!unit.IsVisibleInTree() || !UnitDeathEffect.TryReserve(unit)) return null;
        var effect = new Burn(unit);
        Tween tween = unit.CreateTween();
        using var play = tween.TweenMethod(Callable.From<float>(effect.AdvanceTo), 0f, effect.Seconds, effect.Seconds);
        using var finish = tween.TweenCallback(Callable.From(unit.QueueFree));
        return tween;
    }

    private sealed class Grain
    {
        public MeshInstance3D Mesh;
        public Vector3 Velocity;
        public bool Landed;
    }

    private sealed class Burn
    {
        private readonly Node3D _visual;
        private readonly Vector3 _restScale;
        private readonly List<MeshInstance3D> _meshes = new();
        private readonly StandardMaterial3D _char;
        private readonly List<(MeshInstance3D Mesh, Vector3 Start, float Phase)> _embers = new();
        private readonly List<Grain> _grains = new();
        private readonly List<MeshInstance3D> _dust = new();
        private float _time;
        public float Seconds { get; }

        public Burn(Unit unit)
        {
            Seconds = AshLandSeconds + (float)UnitDeathEffect.Hold(unit) + AshFadeSeconds;
            _visual = unit.GetNode<Node3D>("Visual");
            _restScale = _visual.Scale;
            UnitBreakupEffect.CollectMeshes(_visual, _meshes);
            Aabb box = UnitBreakupEffect.Bounds(new List<Node3D> { _visual });
            // One overlay per death: it blends black over every surface without touching team colours.
            _char = new StandardMaterial3D
            {
                AlbedoColor = new Color(.07f, .06f, .05f, 0), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
            };
            foreach (MeshInstance3D mesh in _meshes) mesh.MaterialOverlay = _char;
            uint seed = unit.UnitId * 2654435761u + 3;
            float Next() { seed = seed * 1664525u + 1013904223u; return (seed >> 8) / 16777216f; }
            Vector3 Inside() => unit.ToLocal(box.Position + new Vector3(Next(), Next(), Next()) * box.Size);
            for (int i = 0; i < 6; i++)
            {
                MeshInstance3D ember = Add(unit, "Ember" + i, EmberMesh, Ember, Inside());
                _embers.Add((ember, ember.Position, Next() * Mathf.Tau));
            }
            // Bigger bodies leave more ash.
            int grains = Mathf.Clamp(14 + (int)(box.Size.X * box.Size.Z * 4), 14, 30);
            for (int i = 0; i < grains; i++)
            {
                MeshInstance3D mesh = Add(unit, "Ash" + i, GrainMesh, i % 3 == 0 ? Cinder : Soot, Inside());
                mesh.Visible = false;
                mesh.Rotation = new Vector3(Next(), Next(), Next()) * Mathf.Tau;
                _grains.Add(new Grain { Mesh = mesh, Velocity = new Vector3(Next() - .5f, Next() * .8f, Next() - .5f) * 1.2f });
            }
            for (int i = 0; i < 3; i++)
            {
                MeshInstance3D puff = Add(unit, "AshCloud" + i, PuffMesh, Dust, unit.ToLocal(box.GetCenter()) + new Vector3(Next() - .5f, Next() * .4f, Next() - .5f) * .6f);
                puff.Visible = false;
                _dust.Add(puff);
            }
        }

        public void AdvanceTo(float time)
        {
            while (_time < time)
            {
                float dt = Mathf.Min(StepSeconds, time - _time);
                _time += dt;
                if (_time >= CharEnd)
                    foreach (Grain grain in _grains) Fall(grain, dt);
            }
            float charred = Mathf.SmoothStep(0, 1, Mathf.Clamp(_time / CharEnd, 0, 1));
            _char.AlbedoColor = new Color(.07f, .06f, .05f, .93f * charred);
            // Then the charred body slumps into the ground and gives way to its ash.
            float crumble = Mathf.Clamp((_time - CharEnd) / (CrumbleEnd - CharEnd), 0, 1);
            _visual.Scale = _restScale * new Vector3(1 + .15f * crumble, Mathf.Max(.05f, 1 - .9f * crumble), 1 + .15f * crumble);
            foreach (MeshInstance3D mesh in _meshes)
                if (GodotObject.IsInstanceValid(mesh)) mesh.Transparency = Mathf.Pow(crumble, 1.5f);
            _visual.Visible = crumble < 1;
            foreach (var (mesh, start, phase) in _embers)
            {
                float life = Mathf.Clamp(_time / (CharEnd + .35f), 0, 1);
                mesh.Visible = life < 1;
                mesh.Position = start + new Vector3(Mathf.Sin(_time * 7 + phase) * .08f, _time * .9f, Mathf.Cos(_time * 6 + phase) * .08f);
                mesh.Transparency = life;
            }
            float settle = Mathf.Clamp((_time - Seconds + AshFadeSeconds) / AshFadeSeconds, 0, 1);
            foreach (Grain grain in _grains)
            {
                grain.Mesh.Visible = _time >= CharEnd && settle < 1;
                grain.Mesh.Transparency = settle;
            }
            float cloud = Mathf.Clamp((_time - CharEnd) / .5f, 0, 1);
            foreach (MeshInstance3D puff in _dust)
            {
                puff.Visible = _time >= CharEnd && cloud < 1;
                puff.Scale = Vector3.One * Mathf.Lerp(.2f, .8f, cloud);
                puff.Transparency = Mathf.Lerp(.3f, 1, cloud);
            }
        }

        private static void Fall(Grain grain, float dt)
        {
            if (grain.Landed) return;
            grain.Velocity += Vector3.Down * 9 * dt;
            Vector3 position = grain.Mesh.Position + grain.Velocity * dt;
            if (position.Y <= .03f)
            {
                position.Y = .03f;
                grain.Landed = true;
            }
            grain.Mesh.Position = position;
        }

        private static MeshInstance3D Add(Unit unit, string name, Mesh mesh, Material material, Vector3 position)
        {
            var instance = new MeshInstance3D
            {
                Name = "Burn" + name, Mesh = mesh, MaterialOverride = material, Position = position,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            unit.AddChild(instance);
            return instance;
        }
    }
}
