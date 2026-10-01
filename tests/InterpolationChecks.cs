using Godot;
using System;
using System.Reflection;

// 실제 Main 수신 분기에 메시지를 넣고 렌더 시간을 직접 진행해 네트워크/프레임 변동을 재현합니다.
public partial class InterpolationChecks : Main
{
    private static readonly MethodInfo Handler = typeof(Main).GetMethod("OnMessage", BindingFlags.Instance | BindingFlags.NonPublic);
    private void Receive(string message) => Handler.Invoke(this, new object[] { message });
    private Unit GetUnit(uint id = 101) => Units.GetNode<Unit>($"Unit_{id}");
    private void Step(double seconds) => Units._Process(seconds);

    public override void _Ready()
    {
        try
        {
            Resources = new ResourceManager();
            AddChild(Resources);
            Map = new MapWorld { Resources = Resources, MapPath = "res://tests/fixtures/map-fog-open.json" };
            AddChild(Map);
            Check(Map.SyncError == null, "Fixture map loads");
            Units = new UnitManager { KnightScene = GD.Load<PackedScene>("res://units/Knight.tscn") };
            AddChild(Units);
            Units.SetProcess(false);
            var net = new NetClient();
            AddChild(net);
            typeof(Main).GetField("_net", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(this, net);
            Receive("TICK 900"); // 핸드셰이크 전에는 무시합니다.
            BeginSession();
            CheckSteadyAndSparseMovement();
            CheckSplitAndBatchedTicks();
            CheckLifecycle();
            CheckHistoryAndClock();
            GD.Print("PASS: 2-tick/100ms interpolation, shared render time, split/batched ticks, sparse POS, bounded history, jitter, stalls, uint wrap, spawn/hide/remove/reconnect");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private void BeginSession()
    {
        Receive($"MAP 2 {Map.MapHash}");
        Receive("WORLD_READY");
        Receive("WELCOME 7 1 COMMANDER");
    }

    private void CheckSteadyAndSparseMovement()
    {
        Receive("UNIT 1 101 7 0 0 1");
        Receive("UNIT 100 202 0 10 0 2");
        Unit own = GetUnit(), enemy = GetUnit(202);
        Units.SelectSingle(own);
        Units.RequestAttack(enemy);
        int commands = 0, redraws = 0;
        Units.CommandRequested += _ => commands++;
        Units.PositionsRendered += () => redraws++;
        Receive("TICK 100");
        Receive("POS 101 1 0");
        Receive("POS 202 11 0");
        At(own, 0, "POS reception does not jump the display");
        Units.RequestMove(new Vector3(20, 0, 0));
        Check(commands == 1, "Commands are sent immediately during the interpolation delay");
        Step(.05);
        At(own, 0, "First interval holds the initial snapshot");
        Receive("TICK 101");
        Receive("POS 101 2 0");
        Receive("POS 202 12 0");
        Step(.025);
        At(own, .5f, "Half tick blends the completed positions, two ticks behind");
        At(enemy, 10.5f, "Own units and minions share one render time");
        Check(redraws == 1, "Interpolated movement invalidates dependent displays without a new POS");
        Check(own.GetNode<Area3D>("SelectionArea").GlobalPosition.IsEqualApprox(own.GlobalPosition), "Selection area follows interpolated root");
        Check((-own.GetNode<Node3D>("Visual").GlobalBasis.Z).Dot(Vector3.Right) > .999f, "Movement facing follows rendered displacement");
        Step(.025);
        At(own, 1, "Full interval reaches the exact endpoint");
        Receive("TICK 102");
        Step(.05);
        At(own, 2, "Last movement reaches its endpoint even when no more POS arrive");
        for (uint tick = 103; tick <= 150; tick++)
        {
            Receive($"TICK {tick}");
            Step(.05);
            At(own, 2, "Stationary ticks hold their position");
        }
        Receive("TICK 151");
        Receive("POS 101 3 0");
        Step(.05);
        At(own, 2, "Restart does not interpolate across the long idle gap");
        Receive("TICK 152");
        Step(.025);
        At(own, 2.5f, "Restart interpolates only its own tick");
        Step(.025);
        At(own, 3, "Restart reaches the exact endpoint");
        Receive("STATE 101 ATTACK 0 202 1");
        own._Process(0);
        Check(own.State.FocusId == 202 && (-own.GetNode<Node3D>("Visual").GlobalBasis.Z).Dot(Vector3.Right) > .999f,
            "Action facing resolves the displayed target position");
    }

    private void CheckSplitAndBatchedTicks()
    {
        BeginSession();
        Receive("UNIT 1 101 7 0 0 1");
        Receive("UNIT 100 202 0 10 0 2");
        Receive("TICK 10");
        Receive("POS 101 1 0");
        Step(.15); // TCP의 같은 틱이 여러 렌더 프레임에 나뉘어 도착합니다.
        At(GetUnit(), 0, "An empty receive queue is not a completed tick");
        Receive("POS 202 11 0");
        Step(.05);
        At(GetUnit(202), 10, "Late lines of the open tick stay buffered");
        foreach (string invalid in new[] { "TICK", "TICK -1", "TICK NaN", "TICK 11 extra", "TICK 4294967296", "TICK 10", "TICK 9" }) Receive(invalid);
        Step(.05);
        At(GetUnit(), 0, "Malformed, duplicate and backwards ticks never seal the current tick");
        Receive("TICK 11");
        Step(.025);
        At(GetUnit(), .5f, "Delayed tick resumes from the frozen endpoint");
        At(GetUnit(202), 10.5f, "Split packets preserve a shared interpolation fraction");
        Receive("POS 101 NaN 0");
        Receive("POS 101 5 Infinity");
        Receive("TICK 12");
        Receive("POS 101 2 0");
        Receive("TICK 13");
        Receive("POS 101 999 0"); // 현재 틱의 미완성 위치는 사용하면 안 됩니다.
        Step(1);
        At(GetUnit(), 2, "Batched ticks remain distinct and never extrapolate into an unfinished tick");
        Step(1);
        At(GetUnit(), 2, "Network stalls hold the last completed position");
        for (uint tick = 14; tick <= 100; tick++)
        {
            Receive($"TICK {tick}");
            Receive($"POS 101 {tick} 0");
        }
        Step(.05);
        Check(GetUnit().GlobalPosition.X >= 94 && GetUnit().GlobalPosition.X <= 99,
            "Large backlogs return to recent bounded history without using the open tick");
    }

    private void CheckLifecycle()
    {
        BeginSession();
        Receive("UNIT 1 101 7 0 0 1");
        Receive("TICK 1");
        Receive("POS 101 10 0");
        Receive("TICK 2");
        Receive("UNIT 1 101 7 50 0 1");
        Step(.01);
        At(GetUnit(), 50, "Duplicate UNIT snapshots discard old paths immediately");
        Receive("UNIT 100 202 0 20 0 2");
        Step(.01);
        At(GetUnit(202), 20, "New units never interpolate from the origin");
        Unit hidden = GetUnit(202);
        Receive("POS 202 30 0");
        Receive("HIDE 202");
        Receive("POS 202 40 0");
        Check(!Units.TryGetUnit(202, out _) && !hidden.IsInsideTree(), "Hide removes pending movement immediately");
        Receive("UNIT 100 202 0 80 0 2");
        Step(.01);
        At(GetUnit(202), 80, "Reappearance starts a new history");
        Unit dying = GetUnit();
        Receive("REMOVE 101");
        Receive("TICK 3");
        Receive("POS 101 90 0");
        Step(.05);
        Check(dying.IsDying && !Units.TryGetUnit(101, out _), "Removal excludes a unit from interpolation");
        At(dying, 50, "Death stays at the last displayed position");
        BeginSession();
        Receive("UNIT 1 101 7 0 0 1");
        Receive("POS 101 4 0");
        At(GetUnit(), 4, "Map reset clears the clock and accepts an initial snapshot");
        Receive("TICK 1");
        Receive("POS 101 5 0");
        Step(.05);
        At(GetUnit(), 4, "A restarted server may begin at a lower tick");
        typeof(Main).GetMethod("OnConnectionClosed", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(this, new object[] { "Interpolation test disconnect" });
        Check(Units.LiveUnits.Count == 0 && !Map.IsSynchronized, "Disconnect clears all buffered entities");
        BeginSession();
        Receive("UNIT 1 101 7 0 0 1");
        Receive("TICK 0");
        Receive("POS 101 1 0");
        Step(.05);
        At(GetUnit(), 0, "Reconnect starts a fresh interpolation clock");
    }

    private static void CheckHistoryAndClock()
    {
        var history = new PositionHistory();
        for (int i = 0; i < 1000; i++) history.Add(i, new Vector3(i, 0, 0));
        Check(history.Sample(-1, Vector3.Zero).X == 992, "History retains only the last eight ticks");
        Check(Math.Abs(history.Sample(995.5, Vector3.Zero).X - 995.5) < .001, "History retains exact interpolation endpoints");
        Check(history.Sample(2000, Vector3.Zero).X == 999, "History cannot extrapolate");
        history.Clear();
        Check(history.Sample(0, Vector3.One) == Vector3.One, "Empty history uses its supplied snapshot");

        var clock = new InterpolationClock();
        clock.BeginTick(100);
        for (uint tick = 100; tick < 160; tick++)
        {
            if (tick != 100) Check(clock.BeginTick(tick), "Accept sequential ticks");
            for (int frame = 1; frame <= 3; frame++)
            {
                clock.Advance(1.0 / 60);
                Check(Math.Abs(clock.RenderTick - (tick - 2 + frame / 3.0)) < .00001,
                    "60fps maintains a two-tick delay against 20Hz arrivals");
            }
        }
        double previous = clock.RenderTick;
        for (uint tick = 160; tick < 200; tick++)
        {
            clock.BeginTick(tick);
            clock.Advance(tick % 2 == 0 ? .04 : .06);
            Check(clock.RenderTick >= previous && clock.RenderTick <= clock.CurrentTick - 1,
                "Jitter never rewinds or runs beyond confirmed time");
            previous = clock.RenderTick;
        }
        Check(clock.CurrentTick - clock.RenderTick < 2.5, "Jitter correction does not accumulate delay");
        clock.Reset();
        clock.BeginTick(uint.MaxValue - 1);
        clock.Advance(.05);
        Check(clock.BeginTick(uint.MaxValue), "Accept the last uint tick");
        clock.Advance(.05);
        Check(clock.BeginTick(0) && clock.CurrentTick == (long)uint.MaxValue + 1, "Unwrap the uint32 boundary");
        previous = clock.RenderTick;
        clock.Advance(.025);
        Check(Math.Abs(clock.RenderTick - previous - .5) < .00001, "Fractional interpolation survives tick wrap");
    }

    private static void At(Unit unit, float x, string label) => Check(unit.GlobalPosition.IsEqualApprox(new Vector3(x, 0, 0)),
        $"{label}: expected {x}, got {unit.GlobalPosition}");
    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
    }
}
