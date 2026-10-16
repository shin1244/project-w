using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;

// Staged battles for the README GIFs: the real map, buildings and unit scenes, driven by a tiny
// local fight simulation that feeds the client the same messages a server would (UNIT, POS,
// STATE, HP, SKILL, SIEGE_STATE). No server and no game rules; the numbers only shape the shot.
// F6 plays the three scenes in a loop. `-- --capture=DIR` records each scene's frames as JPGs.
public partial class BattleStage : Main
{
    private const int TickRate = 30;
    private const int Fps = 15;
    private static readonly MethodInfo Receive = typeof(Main).GetMethod("OnMessage", BindingFlags.Instance | BindingFlags.NonPublic);

    private sealed class Fighter
    {
        public uint Id, Type, Team, Owner, Focus, Swing;
        public Vector2 Pos;
        public float Hp, MaxHp, Speed, Range, Interval, Damage, Cooldown;
        public string Activity = "IDLE", SentActivity = "";
        public uint SentFocus, SentSwing;
        public float SentHp;
        public bool Moved, Deployed, Anchored;
        public float SkillTimer;
    }

    private readonly List<Fighter> _fighters = new();
    private readonly Dictionary<uint, (Vector2 Pos, uint Team)> _towers = new();
    private readonly RandomNumberGenerator _rng = new() { Seed = 7 };
    private uint _tick, _nextId = 50000;
    private double _accumulator;
    private Camera3D _camera;

    public override async void _Ready()
    {
        if (Map.SyncError != null) { GD.PushError(Map.SyncError); GetTree().Quit(1); return; }
        SyncStatus.Hide();
        // The parts of Main._Ready that do not touch the network.
        Fog?.Configure(Map);
        if (Buildings != null && Fog != null) Buildings.VisibilityCheck = Fog.IsBuildingVisible;
        // The same handshake a server sends: map hash, world ready, then a spectator seat.
        Send($"MAP 2 {Map.MapHash}");
        Send("WORLD_READY");
        Send("WELCOME 99 0 SPECTATOR");
        using var data = JsonDocument.Parse(FileAccess.GetFileAsBytes(Map.MapPath));
        uint id = (uint)(Map.GridSize.X * Map.GridSize.Y + 1);
        uint side = 0;
        foreach (var point in data.RootElement.GetProperty("bases").EnumerateArray())
            Send($"BUILDING 0 {id++} {++side} {point[0].GetRawText()} {point[1].GetRawText()} 0");
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
        string capture = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--capture="))?[10..];
        do
        {
            for (int scene = 1; scene <= 3; scene++)
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
            default: // Bottom lane siege: steam artillery digs in and shells the fortress line.
                Army(1, new Vector2(-17, 31), Vector2.Right, minions: true, mercs: 5, heroes: new uint[] { 202, 200 }, machines: 0);
                Army(2, new Vector2(-1, 32), Vector2.Left, minions: true, mercs: 5, heroes: new uint[] { 201 }, machines: 2);
                foreach (Vector2 at in new[] { new Vector2(6, 30), new Vector2(7, 34) })
                {
                    Fighter gun = Spawn(UnitCatalog.SteamArtillery, 2, at);
                    gun.Anchored = true;
                    Send($"SIEGE_STATE {gun.Id} DEPLOYING {_tick + 1} {_tick + 1 + TickRate * 2}");
                }
                shots = new[] { new Shot(0, new Vector2(5, 31), 12), new Shot(2.6f, new Vector2(1, 31), 18), new Shot(6.8f, new Vector2(-8, 31), 34) };
                length = 6.8f;
                break;
        }
        await Run(0.35f, null, shots); // Let the client place everything before the first frame.
        await Run(length, capture == null ? null : $"{capture}/scene-{scene}", shots);
    }

    private void Army(uint team, Vector2 at, Vector2 forward, bool minions, int mercs, uint[] heroes, int machines)
    {
        Vector2 side = new(-forward.Y, forward.X);
        if (minions)
        {
            uint[] wave = { 100, 100, 100, 102, 101, 101, 101 };
            for (int i = 0; i < wave.Length; i++)
                Spawn(wave[i], team, at + forward * (wave[i] == 100 ? 2.5f : wave[i] == 102 ? 0.5f : -1f) + side * ((i % 3) - 1) * 2f + Jitter());
        }
        uint[] roster = { UnitCatalog.Knight, UnitCatalog.Knight, UnitCatalog.Archer, UnitCatalog.Knight, UnitCatalog.Archer, UnitCatalog.Medic };
        for (int i = 0; i < mercs; i++)
            Spawn(roster[i % roster.Length], team, at - forward * (1.5f + (i / 3) * 1.6f) + side * ((i % 3) - 1) * 2.2f + Jitter());
        for (int i = 0; i < heroes.Length; i++)
            Spawn(heroes[i], team, at - forward * 3.5f + side * (i - (heroes.Length - 1) / 2f) * 3f + Jitter());
        for (int i = 0; i < machines; i++)
            Spawn(i % 2 == 0 ? UnitCatalog.FlameAutomaton : UnitCatalog.SteamArtillery, team, at + forward * 0.5f + side * (i == 0 ? 3.5f : -3.5f) + Jitter());
    }

    private Vector2 Jitter() => new(_rng.RandfRange(-.5f, .5f), _rng.RandfRange(-.5f, .5f));

    private Fighter Spawn(uint type, uint team, Vector2 pos)
    {
        var f = new Fighter { Id = _nextId++, Type = type, Team = team, Pos = pos, SkillTimer = _rng.RandfRange(.4f, 1.8f) };
        bool autonomous = UnitCatalog.IsAutonomous(type);
        f.Owner = autonomous ? 0 : team * 10 + (uint)_fighters.Count(o => o.Team == team) % 4 + 1;
        (f.MaxHp, f.Speed, f.Range, f.Interval, f.Damage) = type switch
        {
            100 => (900f, 5f, 1.4f, 1f, 15f),
            101 => (420f, 5f, 6f, 1.2f, 8f),
            102 => (420f, 5f, 3f, 1.1f, 6f),
            UnitCatalog.Knight => (1100f, 5.5f, 1.5f, 1.1f, 16f),
            UnitCatalog.Archer => (600f, 5.5f, 5.5f, 1.4f, 19f),
            UnitCatalog.Medic => (600f, 6f, 5f, 1.5f, 5f),
            UnitCatalog.FlameAutomaton => (1800f, 4.5f, 3f, .8f, 14f),
            UnitCatalog.SteamArtillery => (2000f, 4f, 15f, 3f, 60f),
            >= 200 and <= 203 => (3000f, 7f, type is 202 or 203 ? 6f : 1.6f, .9f, 40f),
            _ => (500f, 5f, 1.5f, 1f, 10f)
        };
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
        foreach (Fighter f in _fighters) Send($"REMOVE {f.Id}");
        _fighters.Clear();
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
        var alive = _fighters.Where(f => f.Hp > 0).ToList();
        foreach (Fighter f in alive)
        {
            Fighter target = alive.Where(o => o.Team != f.Team && o.Hp > 0).OrderBy(o => o.Pos.DistanceSquaredTo(f.Pos)).FirstOrDefault();
            f.Moved = false;
            if (target == null) { f.Activity = "IDLE"; continue; }
            float reach = f.Range + .9f;
            float distance = f.Pos.DistanceTo(target.Pos);
            if (f.Type == UnitCatalog.SteamArtillery && f.Anchored && !f.Deployed && _tick > TickRate * 2)
            {
                f.Deployed = true;
                Send($"SIEGE_STATE {f.Id} DEPLOYED 0 0");
            }
            if (distance > reach && !f.Anchored)
            {
                f.Pos += (target.Pos - f.Pos).Normalized() * f.Speed * dt;
                f.Moved = true;
                f.Activity = "IDLE";
                f.Focus = 0;
            }
            else if (!f.Anchored || f.Deployed)
            {
                f.Activity = "ATTACK";
                f.Focus = target.Id;
                f.Cooldown -= dt;
                if (f.Cooldown <= 0 && distance <= reach + (f.Anchored ? 6 : 0))
                {
                    f.Cooldown += f.Interval;
                    f.Swing++;
                    target.Hp -= f.Damage;
                }
            }
            if (f.Type >= 200 && f.Type <= 203 && (f.SkillTimer -= dt) <= 0)
            {
                f.SkillTimer = _rng.RandfRange(1.2f, 2.4f);
                int slot = _rng.RandiRange(0, 2);
                Send($"SKILL {f.Id} {slot} {target.Id}");
            }
        }
        // Bodies keep apart, friend or foe, so the fight reads as two lines instead of one blob.
        for (int pass = 0; pass < 3; pass++)
        foreach (Fighter f in alive)
            foreach (Fighter o in alive)
            {
                if (o.Id <= f.Id) continue;
                float gap = Body(f) + Body(o), distance = f.Pos.DistanceTo(o.Pos);
                if (distance >= gap || distance < .001f) continue;
                Vector2 push = (f.Pos - o.Pos) / distance * (gap - distance) * .5f;
                if (!f.Anchored) { f.Pos += push; f.Moved = true; }
                if (!o.Anchored) { o.Pos -= push; o.Moved = true; }
            }
        // Towers on the fortress line shoot whoever comes close.
        foreach (var (id, tower) in _towers)
        {
            Fighter target = alive.Where(o => o.Team != tower.Team && o.Hp > 0 && o.Pos.DistanceTo(tower.Pos) < 9).OrderBy(o => o.Pos.DistanceTo(tower.Pos)).FirstOrDefault();
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
            if (f.Hp <= 0) Send($"REMOVE {f.Id}");
        }
        Send($"TICK_END {_tick}");
    }

    private static float Body(Fighter f) => f.Type switch
    {
        UnitCatalog.SteamArtillery or UnitCatalog.FlameAutomaton => 1.4f,
        >= 200 and <= 203 => 1.2f,
        >= 100 and <= 102 => .55f,
        _ => .65f
    };

    private static string Num(float value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
}
