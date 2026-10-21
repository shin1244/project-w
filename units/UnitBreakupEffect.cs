using Godot;
using System.Collections.Generic;

// Presentation only: REMOVE has already taken the unit out of the game, so the server never computes this.
// The blocky rigs come apart at their own part nodes like mass-produced figures, with no blood, wounds or
// physics bodies and nothing to click. A siege shell (DEATH ... BLAST) throws every part: people pop apart
// in a puff of ash, machines out of a fireball. A blade (BLADE) cuts one or two random parts off while the
// rest of the body falls (docs/combat-feedback.md).
public static class UnitBreakupEffect
{
    public const double PersonSeconds = 1.25, MachineSeconds = 1.8;
    private const float StepSeconds = 1 / 60f, Gravity = 16;

    // Pieces are listed children before parents; the last piece is the core that keeps what is left.
    private sealed record Recipe(bool Machine, string[][] Pieces);

    private static readonly Dictionary<uint, Recipe> Recipes = new()
    {
        [UnitCatalog.Worker] = new(false, new[]
        {
            new[] { "Torso/Head" }, new[] { "Torso/Hat", "Torso/HatBrim" }, new[] { "Torso/LeftArm" },
            new[] { "Torso/RightArm" }, new[] { "LeftLeg" }, new[] { "RightLeg" }, new[] { "Torso" }
        }),
        [UnitCatalog.Knight] = new(false, Each("Rig/Head", "Rig/LeftArm", "Rig/RightArm", "Rig/LeftLeg", "Rig/RightLeg",
            "Rig/LeftWinch", "Rig/RightWinch", "Rig/Body")),
        [UnitCatalog.Archer] = new(false, Each("Rig/Head", "Rig/LeftArm", "Rig/RightArm", "Rig/LeftLeg", "Rig/RightLeg",
            "Rig/Rifle", "Rig/Hose", "Rig/Body")),
        [UnitCatalog.FlameAutomaton] = new(true, Each("Rig/Head", "Rig/Nozzle", "Rig/LeftTrack", "Rig/RightTrack",
            "Rig/LeftWheel", "Rig/RightWheel", "Rig/Body"))
    };

    // How hard pieces fly. Severed parts only drop away: the body's own fall carries the death.
    private sealed record Style(float Spread, float Lift, float Spin, float Seconds, float FadeFrom);
    private static readonly Style PersonShatter = new(1.9f, 2.8f, 8.5f, (float)PersonSeconds, (float)PersonSeconds - .45f);
    private static readonly Style MachineShatter = new(4.6f, 5.4f, 4.5f, (float)MachineSeconds, (float)MachineSeconds - .45f);
    private static readonly Style Sever = new(1.1f, 1.9f, 7f, UnitDeathEffect.TotalSeconds,
        (float)(UnitDeathEffect.FallSeconds + UnitDeathEffect.HoldSeconds));

    private static readonly SphereMesh PuffMesh = new() { Radius = .5f, Height = 1, RadialSegments = 8, Rings = 4 };
    private static readonly StandardMaterial3D Ash = Puff(new Color(.49f, .48f, .45f, .7f));
    private static readonly StandardMaterial3D Smoke = Puff(new Color(.24f, .23f, .21f, .75f));
    private static readonly StandardMaterial3D Fire = Glow("ff8a3d");
    private static readonly StandardMaterial3D Flash = Glow("ffd37a");

    // Null means the caller should play the plain fall instead.
    public static Tween TryShatter(Unit unit)
    {
        if (!TryGroups(unit, out Recipe recipe, out List<List<Node3D>> groups)) return null;
        var detached = new List<int>();
        for (int i = 0; i < groups.Count; i++) detached.Add(i);
        var effect = new Breakup(unit, recipe.Machine ? MachineShatter : PersonShatter, recipe.Machine, groups, detached);
        Tween tween = unit.CreateTween();
        using var play = tween.TweenMethod(Callable.From<float>(effect.AdvanceTo), 0f, effect.Seconds, effect.Seconds);
        using var finish = tween.TweenCallback(Callable.From(unit.QueueFree));
        return tween;
    }

    // Cuts parts off before the caller's fall starts, so the fall carries only what is left.
    public static void TrySever(Unit unit)
    {
        if (!TryGroups(unit, out Recipe recipe, out List<List<Node3D>> groups)) return;
        // Never the core, and the same parts every time for the same unit.
        uint seed = unit.UnitId * 2246822519u + 7;
        int Pick(int count) { seed = seed * 1664525u + 1013904223u; return (int)((seed >> 8) % (uint)count); }
        var detached = new List<int> { Pick(groups.Count - 1) };
        if (Pick(3) == 0 && groups.Count > 2)
        {
            int second = Pick(groups.Count - 2);
            detached.Add(second >= detached[0] ? second + 1 : second);
        }
        var effect = new Breakup(unit, Sever, recipe.Machine, groups, detached);
        // Bound to the unit: the fall's tween frees the unit, and with it these pieces.
        Tween tween = unit.CreateTween();
        using var play = tween.TweenMethod(Callable.From<float>(effect.AdvanceTo), 0f, effect.Seconds, effect.Seconds);
    }

    private static bool TryGroups(Unit unit, out Recipe recipe, out List<List<Node3D>> groups)
    {
        groups = new List<List<Node3D>>();
        if (!Recipes.TryGetValue(unit.UnitType, out recipe) || !unit.IsVisibleInTree()) return false;
        Node3D visual = unit.GetNode<Node3D>("Visual");
        foreach (string[] paths in recipe.Pieces)
        {
            var nodes = new List<Node3D>();
            foreach (string path in paths)
                if (visual.GetNodeOrNull<Node3D>(path) is { } node) nodes.Add(node);
            if (nodes.Count > 0) groups.Add(nodes);
        }
        return groups.Count >= 2 && UnitDeathEffect.TryReserve(unit);
    }

    private sealed class Piece
    {
        public Node3D Pivot;
        public Vector3 Velocity, Spin;
        public float Radius;
        public bool Resting;
    }

    private sealed class Breakup
    {
        private readonly List<Piece> _pieces = new();
        private readonly List<MeshInstance3D> _meshes = new();
        private readonly List<(MeshInstance3D Mesh, Vector3 Start, Vector3 Drift, float From, float To, float Life)> _puffs = new();
        private readonly Style _style;
        private float _time;
        public float Seconds => _style.Seconds;

        public Breakup(Unit unit, Style style, bool machine, List<List<Node3D>> groups, List<int> detached)
        {
            _style = style;
            uint seed = unit.UnitId * 2654435761u + 1;
            float Next() { seed = seed * 1664525u + 1013904223u; return (seed >> 8) / 16777216f; }
            var bounds = new List<Aabb>();
            Vector3 origin = Vector3.Zero;
            foreach (List<Node3D> nodes in groups)
            {
                Aabb box = Bounds(nodes);
                bounds.Add(box);
                origin += box.GetCenter();
            }
            // Burst from just below the middle of the figure so the pieces fly up and out.
            origin /= groups.Count;
            origin.Y -= machine ? .15f : .25f;
            bool sever = style == Sever;
            foreach (int i in detached)
            {
                Aabb box = bounds[i];
                var pivot = new Node3D { Name = "BreakupPiece" + i };
                unit.AddChild(pivot);
                pivot.GlobalPosition = box.GetCenter();
                foreach (Node3D node in groups[i]) node.Reparent(pivot);
                CollectMeshes(pivot, _meshes);
                bool core = i == groups.Count - 1;
                Vector3 away = box.GetCenter() - origin;
                away.Y = 0;
                if (away.LengthSquared() < .0025f)
                {
                    float angle = Next() * Mathf.Tau;
                    away = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                }
                float spread = style.Spread * (.7f + .6f * Next());
                // Heads and hats pop highest; the core slumps instead of flying.
                float lift = style.Lift * (.8f + .4f * Next()) + Mathf.Max(0, box.GetCenter().Y - origin.Y) * (sever ? .5f : 1.2f);
                if (core) { spread *= .3f; lift *= .45f; }
                Vector3 axis = new Vector3(Next() - .5f, Next() - .5f, Next() - .5f).Normalized();
                _pieces.Add(new Piece
                {
                    Pivot = pivot,
                    Velocity = away.Normalized() * spread + Vector3.Up * lift,
                    Spin = axis * style.Spin * (.6f + .8f * Next()) * (core ? machine ? .8f : .4f : 1),
                    Radius = Mathf.Clamp(Mathf.Min(box.Size.X, Mathf.Min(box.Size.Y, box.Size.Z)) / 2, .05f, .6f)
                });
                // A clean cut shows a spark, never a wound.
                if (sever) AddPuff(unit, Flash, unit.ToLocal(box.GetCenter()), Vector3.Zero, .08f, .32f, .18f);
            }
            if (sever) return;
            Vector3 local = unit.ToLocal(origin);
            if (machine)
            {
                // The fireball sits above the hull so the hull cannot hide it from the RTS camera.
                for (int i = 0; i < 5; i++)
                    AddPuff(unit, i % 2 == 0 ? Flash : Fire, local + Scatter(Next, .6f) + Vector3.Up * 1.3f, new Vector3(0, 1.2f, 0), .5f, 1.9f, .4f);
                for (int i = 0; i < 6; i++)
                    AddPuff(unit, Smoke, local + Scatter(Next, .6f) + Vector3.Up * .9f, new Vector3(0, 1.1f, 0) + Scatter(Next, .4f), .5f, 1.6f, 1.6f);
            }
            else
                for (int i = 0; i < 3; i++)
                    AddPuff(unit, Ash, local + Scatter(Next, .2f) + Vector3.Up * .25f, Scatter(Next, .4f) + Vector3.Up * .4f, .12f, .55f, .4f);
        }

        public void AdvanceTo(float time)
        {
            // Fixed sub-steps keep the motion the same at any frame rate and under Tween.CustomStep.
            while (_time < time)
            {
                float dt = Mathf.Min(StepSeconds, time - _time);
                _time += dt;
                foreach (Piece piece in _pieces) Step(piece, dt);
            }
            float fade = Mathf.Clamp((_time - _style.FadeFrom) / (_style.Seconds - _style.FadeFrom), 0, 1);
            foreach (MeshInstance3D mesh in _meshes)
                if (GodotObject.IsInstanceValid(mesh)) mesh.Transparency = fade;
            foreach (var (mesh, start, drift, from, to, life) in _puffs)
            {
                float t = Mathf.Clamp(_time / life, 0, 1);
                mesh.Visible = t < 1;
                mesh.Scale = Vector3.One * Mathf.Lerp(from, to, 1 - (1 - t) * (1 - t));
                mesh.Position = start + drift * _time;
                mesh.Transparency = Mathf.Lerp(.3f, 1, t);
            }
        }

        private static void Step(Piece piece, float dt)
        {
            if (!GodotObject.IsInstanceValid(piece.Pivot)) return;
            if (!piece.Resting) piece.Velocity += Vector3.Down * Gravity * dt;
            Vector3 position = piece.Pivot.GlobalPosition + piece.Velocity * dt;
            if (position.Y < piece.Radius)
            {
                position.Y = piece.Radius;
                if (piece.Velocity.Y < 0)
                {
                    // A dull bounce, then the piece skids to a stop on the ground.
                    piece.Velocity = new Vector3(piece.Velocity.X * .55f, -piece.Velocity.Y * .3f, piece.Velocity.Z * .55f);
                    piece.Spin *= .5f;
                    if (piece.Velocity.Y < .7f) { piece.Velocity.Y = 0; piece.Resting = true; }
                }
            }
            if (piece.Resting)
            {
                float friction = Mathf.Max(0, 1 - 5 * dt);
                piece.Velocity *= friction;
                piece.Spin *= friction;
            }
            piece.Pivot.GlobalPosition = position;
            float speed = piece.Spin.Length();
            if (speed > .001f) piece.Pivot.GlobalRotate(piece.Spin / speed, speed * dt);
        }

        private void AddPuff(Unit unit, Material material, Vector3 position, Vector3 drift, float from, float to, float life)
        {
            var puff = new MeshInstance3D
            {
                Name = "BreakupPuff" + _puffs.Count, Mesh = PuffMesh, MaterialOverride = material, Position = position,
                Scale = Vector3.One * from, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            unit.AddChild(puff);
            _puffs.Add((puff, position, drift, from, to, life));
        }

        private static Vector3 Scatter(System.Func<float> next, float size) =>
            new Vector3(next() - .5f, (next() - .5f) * .5f, next() - .5f) * size * 2;
    }

    public static Aabb Bounds(List<Node3D> nodes)
    {
        var meshes = new List<MeshInstance3D>();
        foreach (Node3D node in nodes) CollectMeshes(node, meshes);
        Aabb? total = null;
        foreach (MeshInstance3D mesh in meshes)
        {
            if (!mesh.IsVisibleInTree()) continue;
            Aabb box = mesh.GlobalTransform * mesh.GetAabb();
            total = total is { } sum ? sum.Merge(box) : box;
        }
        return total ?? new Aabb(nodes[0].GlobalPosition - Vector3.One * .1f, Vector3.One * .2f);
    }

    public static void CollectMeshes(Node node, List<MeshInstance3D> meshes)
    {
        if (node is MeshInstance3D mesh) meshes.Add(mesh);
        foreach (Node child in node.GetChildren()) CollectMeshes(child, meshes);
    }

    private static string[][] Each(params string[] paths)
    {
        var pieces = new string[paths.Length][];
        for (int i = 0; i < paths.Length; i++) pieces[i] = new[] { paths[i] };
        return pieces;
    }

    private static StandardMaterial3D Puff(Color color) => new()
    {
        AlbedoColor = color, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
    };

    private static StandardMaterial3D Glow(string color) => new()
    {
        AlbedoColor = new Color(color), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        EmissionEnabled = true, Emission = new Color(color), EmissionEnergyMultiplier = 1.4f,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha
    };
}
