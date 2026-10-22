using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;

// Staged battles for the README GIFs: the real map, buildings and unit scenes, driven by a tiny
// local fight simulation that feeds the client the same messages a server would (UNIT, POS,
// STATE, HP, SKILL, SIEGE_STATE). No server and no game rules; the numbers only shape the shot.
// Movement copies the server's shape of a fight (project-w-server attack_slots.go, move.go): armies
// march in formation, melee units split the ring around their target into attack slots, wait one
// ring further out or switch to an enemy with room, allies push apart and enemies block.
// Scenes 1-3 are battles; 4-6 add a short script each (an item crate carried to a hero, a magic stone
// channel broken by fire, a siege on an HQ) on top of the same simulation.
// F6 plays the six scenes in a loop. `-- --capture=DIR` records each scene's frames as JPGs, and
// `--scenes=4,5,6` limits either to those scenes.
public partial class BattleStage : Main
{
    private const int TickRate = 30;
    private const int Fps = 15;
    // The server tuning the staged movement follows (attack_move.go, attack_slots.go, move.go).
    private const float ScanRadius = 10, ChaseRadius = 12, MeleeSlotMaxRange = 1.5f, SlotEngage = 2, SlotArcStep = .3f;
    private const float AvoidanceMargin = .3f, SeparationWeight = 2, SidestepWeight = .6f, SlowRadius = .5f;
    private const float SlotJitter = .5f, StuckTime = 1, SlotStall = 2;
    private const int SlotSweep = 48;
    private static readonly MethodInfo Receive = typeof(Main).GetMethod("OnMessage", BindingFlags.Instance | BindingFlags.NonPublic);

    private enum SlotKind { None, Fixed, Ring, Wait, Hold }

    // A melee unit's place around its target: where it stands (Fixed: already in range), the ring
    // spot it walks to, the waiting spot one ring further out, or where it stands when both are full.
    private struct Slot
    {
        public uint Target;
        public SlotKind Kind;
        public Vector2 Pos, Route;
        public float Angle;
    }

    private sealed class Fighter
    {
        public uint Id, Type, Team, Owner, Focus, Swing;
        public Vector2 Pos, March, Velocity, SlotAnchor;
        public float Hp, MaxHp, Speed, MarchSpeed, Range, Radius, Interval, Damage, Cooldown, Stride, SlotStill;
        public Fighter Target;
        public Slot Slot;
        public uint StalledUntil;
        public string Activity = "IDLE", SentActivity = "";
        public uint SentFocus, SentSwing;
        public float SentHp;
        public bool Moved, Deployed, Anchored, Blocks;
        // Scripted parts: a passive unit picks no target and only walks to March; a pose is the
        // STATE it shows while it has nothing to fight (a worker chopping, a hero channelling).
        public bool Passive, Building;
        public string Pose;
        public uint PoseFocus;
        public float SkillTimer;
        public bool Alive => Hp > 0;
        public bool Melee => Range <= MeleeSlotMaxRange;
        public bool ToSlot => Slot.Kind is SlotKind.Ring or SlotKind.Wait;
    }

    private readonly record struct Body(Vector2 Pos, float Radius, uint Team, bool Blocks);

    // Bodies around one target this tick: units that took a spot, and everyone else close by.
    private sealed class Ring
    {
        public Fighter Victim;
        public readonly List<Body> Placed = new(), Obstacles = new();
    }

    private readonly List<Fighter> _fighters = new();
    private readonly Dictionary<uint, (Vector2 Pos, uint Team)> _towers = new();
    private readonly Dictionary<uint, Ring> _rings = new();
    private readonly Dictionary<uint, (uint Id, Vector2 Pos)> _bases = new();
    private readonly RandomNumberGenerator _rng = new() { Seed = 7 };
    private uint _tick, _nextId = 50000, _sceneTick, _crate;
    private double _accumulator;
    private Camera3D _camera;
    private Action<float> _script; // runs every tick of a scene with the seconds since it started

    public override async void _Ready()
    {
        if (Map.SyncError != null) { GD.PushError(Map.SyncError); GetTree().Quit(1); return; }
        SyncStatus.Hide();
        // The parts of Main._Ready that do not touch the network, and the magic stone view it makes with the input.
        Fog?.Configure(Map);
        if (Buildings != null && Fog != null) Buildings.VisibilityCheck = Fog.IsBuildingVisible;
        var stone = new TributeEventView { Name = "TributeEvent" };
        AddChild(stone);
        typeof(Main).GetField("_tributeView", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(this, stone);
        // The same handshake a server sends: map hash, world ready, then a spectator seat.
        Send($"MAP 2 {Map.MapHash}");
        Send("WORLD_READY");
        Send("WELCOME 99 0 SPECTATOR");
        using var data = JsonDocument.Parse(FileAccess.GetFileAsBytes(Map.MapPath));
        uint id = (uint)(Map.GridSize.X * Map.GridSize.Y + 1);
        uint side = 0;
        foreach (var point in data.RootElement.GetProperty("bases").EnumerateArray())
        {
            _bases[++side] = (id, new Vector2(point[0].GetSingle(), point[1].GetSingle()));
            Send($"BUILDING 0 {id++} {side} {point[0].GetRawText()} {point[1].GetRawText()} 0");
        }
        foreach (var tower in data.RootElement.GetProperty("towers").EnumerateArray())
        {
            var pos = tower.GetProperty("pos");
            uint team = tower.GetProperty("side").GetUInt32();
            _towers[id] = (new Vector2(pos[0].GetSingle(), pos[1].GetSingle()), team);
            Send($"BUILDING 1 {id++} {team} {pos[0].GetRawText()} {pos[1].GetRawText()} 0");
        }
        _camera = GetNode<Camera3D>("Camera3D");
        // ArenaCamera edge-pans and clamps every frame; the shots drive the camera instead.
        _camera.SetProcess(false);
        _camera.SetProcessInput(false);
        _camera.SetProcessUnhandledInput(false);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        HideInterface();
        string[] args = OS.GetCmdlineUserArgs();
        string capture = args.FirstOrDefault(a => a.StartsWith("--capture="))?[10..];
        int[] scenes = args.FirstOrDefault(a => a.StartsWith("--scenes="))?[9..].Split(',').Select(int.Parse).ToArray()
            ?? new[] { 1, 2, 3, 4, 5, 6 };
        do
        {
            foreach (int scene in scenes)
                await PlayScene(scene, capture);
        } while (capture == null);
        GetTree().Quit();
    }

    private void Send(string line)
    {
        try { Receive.Invoke(this, new object[] { line }); }
        catch (Exception error) { GD.PrintErr($"{line}: {error.InnerException ?? error}"); }
    }

    private void HideInterface()
    {
        foreach (Node node in FindChildren("*", "CanvasLayer", true, false))
            if (node is CanvasLayer layer) layer.Visible = false;
    }

    // ── Scenes ──

    private record struct Shot(float Time, Vector2 Focus, float Size);

    private async System.Threading.Tasks.Task PlayScene(int scene, string capture)
    {
        Clear();
        Shot[] shots;
        float length;
        switch (scene)
        {
            case 1: // Top lane: two full waves with mercenaries and heroes meet head on.
                Army(1, new Vector2(-11, -31), Vector2.Right, minions: true, mercs: 6, heroes: new uint[] { 200, 202 }, machines: 0);
                Army(2, new Vector2(11, -31), Vector2.Left, minions: true, mercs: 6, heroes: new uint[] { 201, 200 }, machines: 0);
                shots = new[] { new Shot(0, new Vector2(0, -30), 34), new Shot(2.2f, new Vector2(0, -31), 24), new Shot(6.5f, new Vector2(-1, -31), 15) };
                length = 6.5f;
                break;
            case 2: // Centre plaza: three heroes a side and the workshop machines, skills flying.
                Army(1, new Vector2(-9, 1), Vector2.Right, minions: false, mercs: 5, heroes: new uint[] { 200, 201, 202 }, machines: 2);
                Army(2, new Vector2(9, -1), Vector2.Left, minions: false, mercs: 5, heroes: new uint[] { 201, 202, 200 }, machines: 2);
                shots = new[] { new Shot(0, new Vector2(-2, 0), 13), new Shot(2.5f, new Vector2(0, 0), 16), new Shot(6.5f, new Vector2(1, 0), 30) };
                length = 6.5f;
                break;
            case 3: // Bottom lane siege: steam artillery digs in and shells the fortress line.
                Army(1, new Vector2(-17, 31), Vector2.Right, minions: true, mercs: 5, heroes: new uint[] { 202, 200 }, machines: 0);
                Army(2, new Vector2(-1, 32), Vector2.Left, minions: true, mercs: 5, heroes: new uint[] { 201 }, machines: 2);
                foreach (Vector2 at in new[] { new Vector2(6, 30), new Vector2(7, 34) })
                {
                    Fighter gun = Spawn(UnitCatalog.SteamArtillery, 2, at);
                    gun.Anchored = gun.Blocks = true;
                    gun.Range = 14;
                    Send($"SIEGE_STATE {gun.Id} DEPLOYING {_tick + 1} {_tick + 1 + TickRate * 2}");
                }
                shots = new[] { new Shot(0, new Vector2(5, 31), 12), new Shot(2.6f, new Vector2(1, 31), 18), new Shot(6.8f, new Vector2(-8, 31), 34) };
                length = 6.8f;
                break;
            case 4:
                (shots, length) = ItemDelivery();
                break;
            case 5:
                (shots, length) = BrokenChannel();
                break;
            default:
                (shots, length) = HeadquartersSiege();
                break;
        }
        await Run(0.35f, null, shots); // Let the client place everything before the first frame.
        _sceneTick = _tick;
        await Run(length, capture == null ? null : $"{capture}/scene-{scene}", shots);
    }

    // The blue hero buys an item mid-fight: the crate drops at the blue spawn, a worker carries it
    // across the base to the hero, and the hero, armed with it, finishes the red minions.
    private (Shot[], float) ItemDelivery()
    {
        Fighter hero = Spawn(UnitCatalog.HeroTest, 1, new Vector2(-65f, -9.5f));
        hero.Damage = 15;
        foreach ((uint type, Vector2 at) in new[] { (100u, new Vector2(-63.5f, -12.3f)), (100u, new Vector2(-66.6f, -12.1f)) })
            Spawn(type, 2, at).Hp = 520;
        foreach ((Vector2 at, uint tree) in new[] { (new Vector2(-55.1f, -3f), 10111u), (new Vector2(-55.1f, -.9f), 10447u) })
        {
            Fighter chopper = Spawn(UnitCatalog.Worker, 1, at);
            (chopper.Passive, chopper.Pose, chopper.PoseFocus) = (true, "GATHER", tree);
        }
        Fighter porter = Spawn(UnitCatalog.Worker, 1, new Vector2(-72.5f, -5f));
        porter.Passive = true;
        Vector2 drop = new(-76, -2); // 2m from the hero spawn point, one of the server's eight drop spots
        float pickedUp = -1;
        _script = time =>
        {
            if (_crate == 0 && time >= .3f)
            {
                _crate = _nextId++;
                Send($"ITEM_CRATE {_crate} {hero.Owner} 1 0 {Num(drop.X)} {Num(drop.Y)}");
                porter.March = drop;
            }
            else if (_crate != 0 && pickedUp < 0 && porter.Pos.DistanceTo(drop) < .2f)
                pickedUp = time;
            else if (pickedUp >= 0 && time >= pickedUp + .3f && hero.Damage < 100)
            {
                porter.March = hero.Pos;
                Send($"ITEM_CRATE {_crate} {hero.Owner} 1 {porter.Id} {Num(porter.Pos.X)} {Num(porter.Pos.Y)}");
                if (Gap(porter, hero) < .3f)
                {
                    Send($"ITEM_CRATE_REMOVE {_crate}");
                    hero.Damage = 600;
                    hero.SkillTimer = 0;
                    porter.March = new Vector2(-72.5f, -5f);
                }
            }
        };
        return (new[] { new Shot(0, new Vector2(-68f, -6.5f), 18) }, 6f);
    }

    // A red golem channels on the magic stone; blue pneumatic gunners walk up and fire, the first
    // real hit breaks the channel (the progress ring is gone) and the golem runs.
    private (Shot[], float) BrokenChannel()
    {
        const uint stone = 7;
        Fighter golem = Spawn(UnitCatalog.HeroGolem, 2, new Vector2(2.9f, .2f));
        (golem.Passive, golem.Pose, golem.PoseFocus) = (true, "CHANNEL", stone);
        uint start = _tick > TickRate ? _tick - TickRate : 0; // channelling for a second already
        Send($"TRIBUTE {stone} ACTIVE 0 0 0 {golem.Id} 2 {start} {start + TickRate * 6} 1 1");
        var gunners = new List<(Fighter Unit, Vector2 Post)>();
        foreach ((Vector2 at, Vector2 post) in new[] { (new Vector2(-16.5f, -1.8f), new Vector2(-1.2f, -2.3f)),
            (new Vector2(-17f, .9f), new Vector2(-2.4f, .4f)), (new Vector2(-18.5f, -.4f), new Vector2(-.6f, 2.6f)) })
        {
            Fighter gunner = Spawn(UnitCatalog.Archer, 1, at);
            (gunner.March, gunner.MarchSpeed, gunner.Cooldown) = (at + Vector2.Right * 30, gunner.Speed, .2f);
            gunners.Add((gunner, post));
        }
        bool broken = false;
        _script = time =>
        {
            if (!broken && golem.Hp < golem.MaxHp)
            {
                broken = true;
                Send($"TRIBUTE {stone} ACTIVE 0 0 0 0 0 0 0 1 1");
                (golem.Pose, golem.March, golem.MarchSpeed) = (null, new Vector2(24, -1), 6);
            }
            // The gunners fire while the golem is in reach, then take the stone instead of chasing it.
            if (broken)
                foreach (var (gunner, post) in gunners)
                    if (Gap(gunner, golem) > gunner.Range + 1)
                        (gunner.Passive, gunner.March) = (true, post);
        };
        return (new[] { new Shot(0, new Vector2(1.5f, 0), 15) }, 6f);
    }

    // Blue waves come down both lane entrances of the red base (a forest closes its front) and close on
    // the HQ: the melee units wrap its walls, the gunners and machines fire over them, the HQ shoots back.
    private (Shot[], float) HeadquartersSiege()
    {
        Headquarters(2, 3200, 4500);
        const uint K = UnitCatalog.Knight, A = UnitCatalog.Archer;
        Column(new Vector2(65, -7.5f), Vector2.Down, new uint[] { 100, K, 100, K, 100 }, new uint[] { K, 200, 202, K },
            new uint[] { 101, A, UnitCatalog.FlameAutomaton, A, 101 });
        Column(new Vector2(65, 7.5f), Vector2.Up, new uint[] { 100, K, 100, K, 100 }, new uint[] { K, 201, K, K },
            new uint[] { 101, A, UnitCatalog.SteamArtillery, A, 101 });
        return (new[] { new Shot(0, new Vector2(65, 0), 21) }, 6.5f);
    }

    // A blue wave in rows, front row first, walking in formation like Army.
    private void Column(Vector2 at, Vector2 forward, params uint[][] rows)
    {
        Vector2 side = new(-forward.Y, forward.X);
        var wave = new List<Fighter>();
        for (int row = 0; row < rows.Length; row++)
            for (int i = 0; i < rows[row].Length; i++)
                wave.Add(Spawn(rows[row][i], 1, at - forward * row * 2f + side * (i - (rows[row].Length - 1) / 2f) * 2.1f + Jitter()));
        float pace = wave.Min(f => f.Speed);
        foreach (Fighter f in wave)
            (f.March, f.MarchSpeed) = (f.Pos + forward * 30, pace);
    }

    // A base HQ as a target: it stands, blocks, has a body as wide as its walls and shoots back.
    private Fighter Headquarters(uint team, float hp, float maxHp)
    {
        var (id, pos) = _bases[team];
        var hq = new Fighter
        {
            Id = id, Type = uint.MaxValue, Team = team, Pos = pos, March = pos, Hp = hp, MaxHp = maxHp,
            Range = 6, Interval = 1.5f, Damage = 40, Radius = 2.9f,
            Building = true, Anchored = true, Deployed = true, Blocks = true
        };
        _fighters.Add(hq);
        Send($"HP {id} {Num(hp)} {Num(maxHp)}");
        hq.SentHp = hp;
        return hq;
    }

    // One army as an attack-move in formation: every unit heads for its own spot of the same block,
    // shifted toward the enemy, at the pace of the slowest so the block keeps its shape.
    private void Army(uint team, Vector2 at, Vector2 forward, bool minions, int mercs, uint[] heroes, int machines)
    {
        Vector2 side = new(-forward.Y, forward.X);
        var army = new List<Fighter>();
        if (minions)
        {
            uint[] wave = { 100, 100, 100, 102, 101, 101, 101 };
            for (int i = 0; i < wave.Length; i++)
                army.Add(Spawn(wave[i], team, at + forward * (wave[i] == 100 ? 2.5f : wave[i] == 102 ? 0.5f : -1f) + side * ((i % 3) - 1) * 2f + Jitter()));
        }
        uint[] roster = { UnitCatalog.Knight, UnitCatalog.Knight, UnitCatalog.Archer, UnitCatalog.Knight, UnitCatalog.Archer, UnitCatalog.Archer };
        for (int i = 0; i < mercs; i++)
            army.Add(Spawn(roster[i % roster.Length], team, at - forward * (1.5f + (i / 3) * 1.6f) + side * ((i % 3) - 1) * 2.2f + Jitter()));
        for (int i = 0; i < heroes.Length; i++)
            army.Add(Spawn(heroes[i], team, at - forward * 3.5f + side * (i - (heroes.Length - 1) / 2f) * 3f + Jitter()));
        for (int i = 0; i < machines; i++)
            army.Add(Spawn(i % 2 == 0 ? UnitCatalog.FlameAutomaton : UnitCatalog.SteamArtillery, team, at + forward * 0.5f + side * (i == 0 ? 3.5f : -3.5f) + Jitter()));
        float pace = army.Min(f => f.Speed);
        foreach (Fighter f in army)
        {
            f.March = f.Pos + forward * 30;
            f.MarchSpeed = pace;
        }
    }

    private Vector2 Jitter() => new(_rng.RandfRange(-.5f, .5f), _rng.RandfRange(-.5f, .5f));

    private Fighter Spawn(uint type, uint team, Vector2 pos)
    {
        var f = new Fighter { Id = _nextId++, Type = type, Team = team, Pos = pos, March = pos, SkillTimer = _rng.RandfRange(.4f, 1.8f) };
        bool autonomous = UnitCatalog.IsAutonomous(type);
        f.Owner = autonomous ? 0 : team * 10 + (uint)_fighters.Count(o => o.Team == team) % 4 + 1;
        // Ranges are gaps between bodies like the server's; bodies are a little wider than the server's
        // so the larger models stand shoulder to shoulder instead of overlapping on screen.
        (f.MaxHp, f.Speed, f.Range, f.Interval, f.Damage, f.Radius) = type switch
        {
            100 => (900f, 5f, 1f, 1f, 15f, .55f),
            101 => (420f, 5f, 6f, 1.2f, 8f, .5f),
            102 => (420f, 5f, 3f, 1.1f, 6f, .5f),
            UnitCatalog.Knight => (1100f, 5.5f, 1f, 1.1f, 16f, .7f),
            UnitCatalog.Archer => (600f, 5.5f, 5.5f, 1.4f, 19f, .6f),
            UnitCatalog.FlameAutomaton => (1800f, 4.5f, 3f, .8f, 14f, .9f),
            UnitCatalog.SteamArtillery => (2000f, 4f, 6f, 3f, 60f, 1.3f),
            UnitCatalog.Worker => (300f, 5f, 1f, 1f, 5f, .45f),
            UnitCatalog.HeroGolem => (3000f, 7f, 1f, .9f, 40f, 1f),
            >= 200 and <= 203 => (3000f, 7f, 1f, .9f, 40f, .85f),
            _ => (500f, 5f, 1f, 1f, 10f, .6f)
        };
        f.MarchSpeed = f.Speed;
        f.Hp = f.MaxHp;
        f.Cooldown = _rng.RandfRange(0, f.Interval);
        _fighters.Add(f);
        Send($"UNIT {type} {f.Id} {f.Owner} {Num(pos.X)} {Num(pos.Y)} {team}");
        Send($"HP {f.Id} {Num(f.Hp)} {Num(f.MaxHp)}");
        f.SentHp = f.Hp;
        return f;
    }

    private void Clear()
    {
        foreach (Fighter f in _fighters) Send(f.Building ? $"STATE {f.Id} IDLE 0 0 {f.Swing}" : $"REMOVE {f.Id}");
        _fighters.Clear();
        _rings.Clear();
        if (_crate != 0) Send($"ITEM_CRATE_REMOVE {_crate}");
        _crate = 0;
        Send("TRIBUTE 0 WAITING 0 0 1 0 0 0 0 0 0");
        (_script, _sceneTick) = (null, 0);
    }

    // Floating 3D text (crate and spawn-zone labels) stays out of the shot like the interface.
    private void HideLabels()
    {
        foreach (Node node in FindChildren("*", "Label3D", true, false))
            if (node is Label3D label) label.Visible = false;
    }

    // ── Simulation and recording ──

    private async System.Threading.Tasks.Task Run(float seconds, string folder, Shot[] shots)
    {
        if (folder != null) DirAccess.MakeDirRecursiveAbsolute(folder);
        double clock = 0;
        int frame = 0;
        while (clock < seconds)
        {
            double delta = folder != null ? 1.0 / Fps : GetProcessDeltaTime();
            _accumulator += delta;
            while (_accumulator >= 1.0 / TickRate)
            {
                _accumulator -= 1.0 / TickRate;
                Step();
            }
            if (folder != null) AimCamera(shots, (float)clock);
            HideLabels();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (folder != null)
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using Image image = GetViewport().GetTexture().GetImage();
                image.SaveJpg($"{folder}/frame-{frame++:D4}.jpg", .92f);
            }
            else AimCamera(shots, (float)clock);
            clock += delta;
        }
    }

    private void AimCamera(Shot[] shots, float time)
    {
        int next = Array.FindIndex(shots, s => s.Time > time);
        Shot a = next <= 0 ? shots[0] : shots[next - 1], b = next < 0 ? shots[^1] : shots[next];
        float t = b.Time > a.Time ? Mathf.SmoothStep(0, 1, (time - a.Time) / (b.Time - a.Time)) : 1;
        _camera.Size = Mathf.Lerp(a.Size, b.Size, t);
        Vector2 focus = a.Focus.Lerp(b.Focus, t);
        // Put the view axis on the focus point; the play-area clamp would stop short of the lanes.
        Vector3 forward = -_camera.GlobalBasis.Z;
        Vector3 current = _camera.GlobalPosition + forward * (-_camera.GlobalPosition.Y / forward.Y);
        _camera.GlobalPosition += new Vector3(focus.X - current.X, 0, focus.Y - current.Z);
    }

    private void Step()
    {
        _tick++;
        Send($"TICK {_tick}");
        float dt = 1f / TickRate;
        var alive = _fighters.Where(f => f.Alive).ToList();
        if (_script != null && _sceneTick != 0) _script((_tick - _sceneTick) / (float)TickRate);
        foreach (Fighter f in alive)
            if (f.Type == UnitCatalog.SteamArtillery && f.Anchored && !f.Deployed && _tick > TickRate * 2)
            {
                f.Deployed = true;
                Send($"SIEGE_STATE {f.Id} DEPLOYED 0 0");
            }
        Retarget(alive);
        AssignSlots(alive);
        foreach (Fighter f in alive) Steer(f, alive);
        foreach (Fighter f in alive)
        {
            Vector2 old = f.Pos;
            if (f.Velocity != Vector2.Zero) MoveTo(f, f.Pos + f.Velocity.LimitLength(f.Speed) * dt, alive);
            f.Moved = f.Pos.DistanceSquaredTo(old) > 1e-6f;
            if (!f.Moved) f.Pos = old;
            f.Stride = f.Pos.DistanceTo(old);
        }
        foreach (Fighter f in alive) Fight(f, dt);
        // Towers on the fortress line shoot whoever comes close.
        foreach (var (id, tower) in _towers)
        {
            Fighter target = alive.Where(o => o.Team != tower.Team && o.Alive && o.Pos.DistanceTo(tower.Pos) < 9).OrderBy(o => o.Pos.DistanceTo(tower.Pos)).FirstOrDefault();
            if (target != null && _tick % (TickRate * 3 / 2) == 0)
            {
                target.Hp -= 40;
                Send($"STATE {id} ATTACK 0 {target.Id} {_tick}");
            }
        }
        foreach (Fighter f in alive)
        {
            if (f.Moved) Send($"POS {f.Id} {Num(f.Pos.X)} {Num(f.Pos.Y)}");
            if (f.Activity != f.SentActivity || f.Focus != f.SentFocus || f.Swing != f.SentSwing)
            {
                Send($"STATE {f.Id} {f.Activity} 0 {f.Focus} {f.Swing}");
                (f.SentActivity, f.SentFocus, f.SentSwing) = (f.Activity, f.Focus, f.Swing);
            }
            if (f.Hp != f.SentHp)
            {
                Send($"HP {f.Id} {Num(Math.Max(0, f.Hp))} {Num(f.MaxHp)}");
                f.SentHp = f.Hp;
            }
            if (!f.Alive && !f.Building) Send($"REMOVE {f.Id}");
        }
        Send($"TICK_END {_tick}");
    }

    // Attack-move targeting: keep the target until it dies or runs off, else take the nearest enemy.
    // Melee units take an enemy with room on its ring first, and leave a full ring for one with room.
    private void Retarget(List<Fighter> alive)
    {
        foreach (Fighter u in alive)
        {
            if (u.Target != null && (u.Passive || !u.Target.Alive || Gap(u, u.Target) > Math.Max(ChaseRadius, u.Range)))
                u.Target = null;
            if (u.Passive) continue;
            if (u.Target == null)
                u.Target = NearestEnemy(u, alive, u.Melee ? t => HasRoom(u, t, false) : null) ?? NearestEnemy(u, alive, null);
            else if (u.Melee && u.Slot.Target == u.Target.Id && u.Slot.Kind is SlotKind.Wait or SlotKind.Hold)
            {
                Fighter current = u.Target;
                Fighter roomier = NearestEnemy(u, alive, t => t != current && HasRoom(u, t, false));
                if (roomier != null && HasRoom(u, roomier, true)) u.Target = roomier;
            }
        }
    }

    private static Fighter NearestEnemy(Fighter u, List<Fighter> alive, Func<Fighter, bool> accept)
    {
        float scan = Math.Max(ScanRadius, u.Range);
        return alive.Where(o => o.Team != u.Team && o.Alive && Gap(u, o) <= scan && (accept == null || accept(o)))
            .OrderBy(o => Gap(u, o)).ThenBy(o => o.Id).FirstOrDefault();
    }

    // Whether u fits on the target's ring now. Reserving keeps the spot, so several waiting units
    // do not all switch to the same gap. A target nobody fights yet has room.
    private bool HasRoom(Fighter u, Fighter target, bool reserve)
    {
        if (!reserve && u.Slot.Target == target.Id && u.Slot.Kind is SlotKind.Fixed or SlotKind.Ring) return true;
        if (!_rings.TryGetValue(target.Id, out Ring ring)) _rings[target.Id] = ring = new Ring { Victim = target };
        if (!Find(ring, u, RingRadius(u, target), AngleOf(target, u.Pos), out Vector2 pos, out _)) return false;
        if (reserve) ring.Placed.Add(new Body(pos, u.Radius, u.Team, true));
        return true;
    }

    // ── Melee attack slots (attack_slots.go) ──

    private static float RingRadius(Fighter u, Fighter victim) => victim.Radius + u.Radius + u.Range * .95f;
    private static float LaneRadius(Fighter u, Fighter victim) => RingRadius(u, victim) + u.Radius * 2 + AvoidanceMargin;
    private static float WaitRadius(Fighter u, Fighter victim) => LaneRadius(u, victim) + u.Radius * 2;

    // Melee units close to their target split its ring among themselves: units already in range keep
    // where they stand, then heroes, units that had a spot here, and the nearest take the free spot
    // closest to where they come from. A full ring sends the rest to wait one ring further out.
    // Only a target that stands (fighting, waiting or anchored) is ringed. Two units walking at each
    // other meet head on instead; on the stage, spots around a walking target dragged both sides along.
    private void AssignSlots(List<Fighter> alive)
    {
        _rings.Clear();
        var claims = new SortedDictionary<uint, List<Fighter>>();
        foreach (Fighter u in alive)
        {
            Fighter victim = u.Target;
            bool claimed = victim != null && u.Melee && !u.Anchored && (victim.Blocks || victim.Stride < victim.Speed / TickRate * .3f);
            if (claimed)
            {
                float reach = WaitRadius(u, victim) + SlotEngage * (u.Slot.Target == victim.Id && u.Slot.Kind != SlotKind.None ? 2 : 1);
                claimed = u.Pos.DistanceTo(victim.Pos) <= reach;
            }
            if (!claimed) { u.Slot = default; continue; }
            if (!claims.TryGetValue(victim.Id, out var list)) claims[victim.Id] = list = new List<Fighter>();
            list.Add(u);
        }
        foreach (var (id, claimants) in claims)
        {
            Fighter victim = claimants[0].Target;
            var ring = new Ring { Victim = victim };
            _rings[id] = ring;
            float reach = claimants.Max(u => WaitRadius(u, victim) + u.Radius);
            foreach (Fighter n in alive)
                if (n != victim && !claimants.Contains(n) && n.Pos.DistanceTo(victim.Pos) < reach + n.Radius)
                    ring.Obstacles.Add(new Body(n.Pos, n.Radius, n.Team, n.Blocks));
            var queue = new List<(Fighter Unit, float Desired)>();
            foreach (Fighter u in claimants)
            {
                if (InRange(u, victim)) Take(ring, u, SlotKind.Fixed, u.Pos, AngleOf(victim, u.Pos));
                else if (u.StalledUntil > _tick) u.Slot = new Slot { Target = id, Kind = SlotKind.Hold, Pos = u.Pos, Angle = AngleOf(victim, u.Pos) };
                else queue.Add((u, u.Slot.Target == id && u.Slot.Kind != SlotKind.Hold ? u.Slot.Angle : AngleOf(victim, u.Pos)));
            }
            queue = queue.OrderBy(q => UnitCatalog.IsHero(q.Unit.Type) ? 0 : q.Unit.Slot.Target == id && q.Unit.Slot.Kind is SlotKind.Fixed or SlotKind.Ring ? 1 : 2)
                .ThenBy(q => Gap(q.Unit, victim)).ThenBy(q => q.Unit.Id).ToList();
            foreach (var (u, desired) in queue)
            {
                if (Find(ring, u, RingRadius(u, victim), desired, out Vector2 pos, out float angle)) Take(ring, u, SlotKind.Ring, pos, angle);
                else if (Find(ring, u, WaitRadius(u, victim), desired, out pos, out angle)) Take(ring, u, SlotKind.Wait, pos, angle);
                else u.Slot = new Slot { Target = id, Kind = SlotKind.Hold, Pos = u.Pos, Angle = AngleOf(victim, u.Pos) };
            }
            Untangle(victim, queue.Select(q => q.Unit).ToList());
            var fighting = claimants.Where(u => u.Slot.Kind == SlotKind.Fixed).ToList();
            foreach (Fighter u in claimants)
                u.Slot.Route = u.ToSlot ? Route(ring, u, fighting) : u.Slot.Pos;
        }
    }

    private static void Take(Ring ring, Fighter u, SlotKind kind, Vector2 pos, float angle)
    {
        u.Slot = new Slot { Target = ring.Victim.Id, Kind = kind, Pos = pos, Angle = angle };
        ring.Placed.Add(new Body(pos, u.Radius, u.Team, true));
    }

    // The free spot on the circle closest to the desired direction. Candidates are that direction,
    // the directions that just touch a body already there (so no gaps are left), and an even sweep.
    private bool Find(Ring ring, Fighter u, float radius, float desired, out Vector2 pos, out float angle)
    {
        Vector2 center = ring.Victim.Pos;
        var candidates = new List<float> { desired };
        foreach (Body b in ring.Placed.Concat(ring.Obstacles))
        {
            Vector2 q = b.Pos - center;
            float d = q.Length(), touch = u.Radius + b.Radius;
            float c = (radius * radius + d * d - touch * touch) / (2 * radius * d);
            if (d == 0 || c < -1 || c > 1) continue;
            candidates.Add(q.Angle() + Mathf.Acos(c));
            candidates.Add(q.Angle() - Mathf.Acos(c));
        }
        for (int k = 1; k < SlotSweep; k++) candidates.Add(desired + Mathf.Tau * k / SlotSweep);
        foreach (float a in candidates.OrderBy(a => Mathf.Abs(Delta(a, desired))).ThenBy(a => a))
        {
            pos = center + Vector2.FromAngle(a) * radius;
            if (Fits(ring, u, pos)) { angle = a; return true; }
        }
        pos = default;
        angle = 0;
        return false;
    }

    // Spots on trees or walls, or over another taken spot, an enemy or an ally that holds its ground are out.
    // Moving allies step aside on their own.
    private bool Fits(Ring ring, Fighter u, Vector2 pos)
    {
        foreach (Body b in ring.Placed)
            if (pos.DistanceTo(b.Pos) < u.Radius + b.Radius - .001f) return false;
        foreach (Body b in ring.Obstacles)
            if ((b.Team != u.Team || b.Blocks) && pos.DistanceTo(b.Pos) < u.Radius + b.Radius - .001f) return false;
        return !TerrainBlocked(pos, u.Radius);
    }

    // Swap spots on the same circle so nobody walks around past another unit going the other way.
    private static void Untangle(Fighter victim, List<Fighter> units)
    {
        float Travel(Fighter u, Slot s) => Mathf.Abs(Delta(s.Angle, AngleOf(victim, u.Pos)));
        for (int pass = 0; pass < units.Count; pass++)
        {
            bool swapped = false;
            for (int i = 0; i < units.Count; i++)
                for (int j = i + 1; j < units.Count; j++)
                {
                    Fighter a = units[i], b = units[j];
                    Slot sa = a.Slot, sb = b.Slot;
                    if (sa.Kind != sb.Kind || !a.ToSlot || a.Radius != b.Radius || a.Range != b.Range) continue;
                    if (Travel(a, sb) + Travel(b, sa) < Travel(a, sa) + Travel(b, sb) - 1e-3f)
                    {
                        (a.Slot.Pos, a.Slot.Angle, b.Slot.Pos, b.Slot.Angle) = (sb.Pos, sb.Angle, sa.Pos, sa.Angle);
                        swapped = true;
                    }
                }
            if (!swapped) return;
        }
    }

    // Walk to the entry just outside the spot and then straight in. If the target or the units
    // fighting it stand in the way, walk around the outer lane toward the spot, the short way unless
    // that way is blocked.
    private Vector2 Route(Ring ring, Fighter u, List<Fighter> fighting)
    {
        Fighter victim = ring.Victim;
        Slot slot = u.Slot;
        Vector2 outward = Vector2.FromAngle(slot.Angle), rel = u.Pos - victim.Pos;
        float outer = LaneRadius(u, victim);
        Vector2 goal = slot.Pos;
        if (slot.Kind == SlotKind.Ring)
        {
            if (Mathf.Abs(outward.Cross(rel)) < u.Radius * .5f && rel.Dot(outward) > slot.Pos.DistanceTo(victim.Pos))
                return slot.Pos;
            goal = victim.Pos + outward * outer;
        }
        bool Crosses(Vector2 at, float radius) => DistanceToSegment(at, u.Pos, goal) < u.Radius + radius - .001f;
        bool clear = !Crosses(victim.Pos, victim.Radius) && !fighting.Any(f => f != u && Crosses(f.Pos, f.Radius))
            && !ring.Obstacles.Any(b => (b.Team != u.Team || b.Blocks) && Crosses(b.Pos, b.Radius));
        float current = rel.Angle(), gap = Delta(slot.Angle, current);
        if (clear || Mathf.Abs(gap) * rel.Length() < u.Radius) return goal;
        if (rel.Length() < outer - u.Radius * .5f) return victim.Pos + Vector2.FromAngle(current) * outer;
        float radius = Mathf.Max(outer, rel.Length());
        if (ArcBlocked(ring, u, current, gap, radius))
        {
            // The server stands here and gives the spot up after a while; without its path finding the
            // stage would leave whole back rows standing, so it walks on and slides along the crowd.
            float other = gap - Mathf.Sign(gap) * Mathf.Tau;
            if (ArcBlocked(ring, u, current, other, radius)) return goal;
            gap = other;
        }
        return victim.Pos + Vector2.FromAngle(current + Mathf.Clamp(gap, -SlotArcStep, SlotArcStep)) * radius;
    }

    private bool ArcBlocked(Ring ring, Fighter u, float current, float delta, float radius)
    {
        int steps = (int)(Mathf.Abs(delta) * radius / u.Radius) + 1;
        for (int k = 1; k <= steps; k++)
        {
            Vector2 at = ring.Victim.Pos + Vector2.FromAngle(current + delta * k / steps) * radius;
            if (TerrainBlocked(at, u.Radius)) return true;
            foreach (Body b in ring.Obstacles)
                if ((b.Team != u.Team || b.Blocks) && at.DistanceTo(b.Pos) < u.Radius + b.Radius - .001f) return true;
        }
        return false;
    }

    // ── Steering and fighting (move.go, attack.go) ──

    // What the order wants plus a push away from overlapping allies. Units fighting in range hold
    // their ground; the others walk around them, sidestepping when an ally blocks them head on.
    private void Steer(Fighter u, List<Fighter> alive)
    {
        u.Velocity = Vector2.Zero;
        if (u.Anchored) return;
        Fighter t = u.Target;
        Vector2 desired;
        if (t == null) desired = Arrive(u, u.March, 0, u.MarchSpeed);
        else if (InRange(u, t) || u.Slot.Kind == SlotKind.Hold || (u.Slot.Kind == SlotKind.Wait && u.Pos.DistanceTo(u.Slot.Pos) < .1f)) desired = Vector2.Zero;
        else if (u.ToSlot)
        {
            // A unit that only jitters in the crowd gives its spot up for a while (attack.go toSlot).
            if (u.Pos.DistanceTo(u.SlotAnchor) > SlotJitter) (u.SlotAnchor, u.SlotStill) = (u.Pos, 0);
            else if ((u.SlotStill += 1f / TickRate) > StuckTime) (u.SlotStill, u.StalledUntil) = (0, _tick + (uint)(SlotStall * TickRate));
            desired = Arrive(u, u.Slot.Route, 0, u.Speed, .01f);
        }
        else desired = Arrive(u, t.Pos, t.Radius + u.Radius + u.Range * .9f, u.Speed);
        if (u.Blocks && t != null && InRange(u, t)) return;
        Vector2 push = Vector2.Zero;
        foreach (Fighter n in alive)
        {
            if (n == u || n.Team != u.Team) continue;
            float min = u.Radius + n.Radius + (n.Blocks && !u.ToSlot ? AvoidanceMargin : 0);
            Vector2 d = u.Pos - n.Pos;
            if (d.LengthSquared() >= min * min) continue;
            float distance = d.Length();
            push += (distance > 0 ? d / distance : Vector2.FromAngle(u.Id * 2.399963f)) * ((min - distance) / min);
        }
        push *= u.Speed * SeparationWeight;
        u.Velocity = desired + push;
        bool slowing = t == null && desired.LengthSquared() < u.MarchSpeed * u.MarchSpeed * .99f;
        if (desired != Vector2.Zero && push != Vector2.Zero && !slowing && desired.Normalized().Dot(push.Normalized()) <= -.7f)
        {
            Vector2 dir = desired.Normalized(), side = new(-dir.Y, dir.X);
            if (u.Id % 2 == 0) side = -side;
            if (TerrainBlocked(u.Pos + side * u.Radius, u.Radius)) side = -side;
            u.Velocity += side * u.Speed * SidestepWeight;
        }
    }

    private static Vector2 Arrive(Fighter u, Vector2 target, float stop, float speed, float arrived = .06f)
    {
        Vector2 offset = target - u.Pos;
        float remain = offset.Length() - stop;
        if (remain <= arrived) return Vector2.Zero;
        return offset.Normalized() * speed * Mathf.Min(1, remain / SlowRadius);
    }

    // Trees, walls, enemies and allies fighting in place block the way; a glancing hit slides along them.
    // A unit squeezed onto an ally with no way out may pass the allies that block it (the server's
    // push recovery), so two bodies wedged between a fighting ally and the trees come apart.
    private void MoveTo(Fighter u, Vector2 next, List<Fighter> alive)
    {
        bool recovering = false;
        Fighter Blocker(Vector2 p)
        {
            Fighter blocker = null;
            foreach (Fighter n in alive)
            {
                if (n == u || (n.Team == u.Team && (!n.Blocks || recovering)) || (blocker != null && n.Id > blocker.Id)) continue;
                float min = u.Radius + n.Radius - .001f, ahead = p.DistanceSquaredTo(n.Pos);
                if (ahead < min * min && ahead < u.Pos.DistanceSquaredTo(n.Pos)) blocker = n;
            }
            return blocker;
        }
        bool Blocked(Vector2 p) => TerrainBlocked(p, u.Radius) || Blocker(p) != null;
        bool Slide()
        {
            Fighter blocker = TerrainBlocked(next, u.Radius) ? null : Blocker(next);
            if (blocker == null && !TerrainBlocked(next, u.Radius)) { u.Pos = next; return true; }
            if (blocker != null)
            {
                // Slide toward the side the step leans to; the server picks it by ID, which on the stage
                // walked units at the back into the tree line and back.
                Vector2 step = next - u.Pos, normal = (u.Pos - blocker.Pos).Normalized(), side = new(normal.Y, -normal.X);
                if (side.Dot(step) < 0 || (side.Dot(step) == 0 && u.Id % 2 == 0)) side = -side;
                foreach (float sign in new[] { 1f, -1f })
                {
                    Vector2 p = u.Pos + side * step.Length() * sign;
                    if (!Blocked(p)) { u.Pos = p; return true; }
                }
            }
            if (!Blocked(new Vector2(next.X, u.Pos.Y))) u.Pos.X = next.X;
            else if (!Blocked(new Vector2(u.Pos.X, next.Y))) u.Pos.Y = next.Y;
            else return false;
            return true;
        }
        if (TerrainBlocked(u.Pos, u.Radius)) { u.Pos = next; return; }
        if (Slide()) return;
        recovering = alive.Any(n => n != u && n.Team == u.Team && n.Pos.DistanceTo(u.Pos) < u.Radius + n.Radius - .05f);
        if (recovering) Slide();
    }

    private void Fight(Fighter f, float dt)
    {
        Fighter target = f.Target;
        bool ready = !f.Anchored || f.Deployed;
        if (target == null || !target.Alive || !InRange(f, target) || !ready)
        {
            f.Activity = f.Pose ?? "IDLE";
            f.Focus = f.Pose != null ? f.PoseFocus : 0;
            f.Blocks = f.Anchored;
            return;
        }
        f.Activity = "ATTACK";
        f.Focus = target.Id;
        f.Blocks = true;
        f.Cooldown -= dt;
        if (f.Cooldown <= 0)
        {
            f.Cooldown += f.Interval;
            f.Swing++;
            target.Hp -= f.Damage;
        }
        if (UnitCatalog.IsHero(f.Type) && (f.SkillTimer -= dt) <= 0)
        {
            f.SkillTimer = _rng.RandfRange(1.2f, 2.4f);
            Send($"SKILL {f.Id} {_rng.RandiRange(0, 2)} {target.Id}");
        }
    }

    // ── Geometry ──

    private static float Gap(Fighter a, Fighter b) => Mathf.Max(0, a.Pos.DistanceTo(b.Pos) - a.Radius - b.Radius);
    private static bool InRange(Fighter u, Fighter t) => Gap(u, t) <= u.Range;
    private static float AngleOf(Fighter victim, Vector2 pos) => (pos - victim.Pos).Angle();
    private static float Delta(float to, float from) => Mathf.Wrap(to - from, -Mathf.Pi, Mathf.Pi);

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = ab.LengthSquared() > 0 ? Mathf.Clamp((p - a).Dot(ab) / ab.LengthSquared(), 0, 1) : 0;
        return p.DistanceTo(a + ab * t);
    }

    private bool TerrainBlocked(Vector2 pos, float radius)
    {
        Vector2 origin = Map.GridOrigin;
        float cell = Map.CellSize, r = Mathf.Max(0, radius - .05f);
        int x0 = Mathf.FloorToInt((pos.X - r - origin.X) / cell), x1 = Mathf.FloorToInt((pos.X + r - origin.X) / cell);
        int z0 = Mathf.FloorToInt((pos.Y - r - origin.Y) / cell), z1 = Mathf.FloorToInt((pos.Y + r - origin.Y) / cell);
        for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
            {
                if (!Map.IsTerrainBlocked(x, z)) continue;
                Vector2 low = origin + new Vector2(x, z) * cell;
                Vector2 near = new(Mathf.Clamp(pos.X, low.X, low.X + cell), Mathf.Clamp(pos.Y, low.Y, low.Y + cell));
                if (near.DistanceSquaredTo(pos) < r * r) return true;
            }
        return false;
    }

    private static string Num(float value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
}
