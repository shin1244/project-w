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
    private const double TickSeconds = 1.0 / 30;

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
            GD.Print("PASS: 30Hz/50ms interpolation, tick completion, shared render time, split/batched ticks, sparse POS, bounded history, jitter, stalls, uint wrap, spawn/hide/remove/reconnect");
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
        Receive("TICK_END 100");
        Step(TickSeconds);
        At(own, .5f, "Completed positions render within the first interval at 50ms delay");
        redraws = 0;
        Receive("TICK 101");
        Receive("POS 101 2 0");
        Receive("POS 202 12 0");
        Receive("TICK_END 101");
        Step(TickSeconds / 2);
        At(own, 1, "Half interval reaches the previous tick at 50ms delay");
        At(enemy, 11, "Own units and minions share one render time");
        Check(redraws == 1, "Interpolated movement invalidates dependent displays without a new POS");
        Check(own.GetNode<Area3D>("SelectionArea").GlobalPosition.IsEqualApprox(own.GlobalPosition), "Selection area follows interpolated root");
        Check((-own.GetNode<Node3D>("Visual").GlobalBasis.Z).Dot(Vector3.Right) > .999f, "Movement facing follows rendered displacement");
        Step(TickSeconds / 2);
        At(own, 1.5f, "Second frame keeps moving without waiting for the next TICK");
        Receive("TICK 102");
        Receive("TICK_END 102");
        Step(TickSeconds);
        At(own, 2, "Last movement reaches its endpoint even when no more POS arrive");
        for (uint tick = 103; tick <= 150; tick++)
        {
            Receive($"TICK {tick}");
            Receive($"TICK_END {tick}");
            Step(TickSeconds);
            At(own, 2, "Stationary ticks hold their position");
        }
        Receive("TICK 151");
        Receive("POS 101 3 0");
        Receive("TICK_END 151");
        Step(TickSeconds);
        At(own, 2.5f, "Restart interpolates only its own tick after the idle gap");
        Receive("TICK 152");
        Receive("TICK_END 152");
        Step(TickSeconds);
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
        foreach (string invalid in new[] { "TICK", "TICK -1", "TICK NaN", "TICK 11 extra", "TICK 4294967296", "TICK 10", "TICK 9",
            "TICK_END", "TICK_END -1", "TICK_END NaN", "TICK_END 10 extra", "TICK_END 4294967296", "TICK_END 9", "TICK_END 11" }) Receive(invalid);
        Step(.05);
        At(GetUnit(), 0, "Malformed, duplicate and backwards ticks never seal the current tick");
        Receive("TICK_END 10");
        Step(TickSeconds / 2);
        At(GetUnit(), .55f, "Delayed tick resumes from the frozen endpoint");
        At(GetUnit(202), 10.55f, "Split packets preserve a shared interpolation fraction");
        Receive("TICK 11");
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
        Receive("TICK_END 12");
        Step(TickSeconds);
        At(GetUnit(), 2, "A stale completion cannot expose the open tick");
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
        Check(InterpolationClock.TickRate == 30 && InterpolationClock.DelayTicks == 1.5 &&
            InterpolationClock.DelaySeconds == .05, "30Hz uses an exact 50ms fractional delay");
        Check(!clock.CompleteTick(100), "Completion before the first tick is ignored");
        clock.BeginTick(100);
        for (uint tick = 100; tick < 160; tick++)
        {
            if (tick != 100) Check(clock.BeginTick(tick), "Accept sequential ticks");
            Check(clock.CompleteTick(tick) && !clock.CompleteTick(tick), "Complete each tick once");
            for (int frame = 1; frame <= 2; frame++)
            {
                clock.Advance(1.0 / 60);
                Check(Math.Abs(clock.RenderTick - (tick - 1.5 + frame / 2.0)) < .00001,
                    "60fps advances every frame at 50ms delay against 30Hz arrivals");
            }
        }
        double previous = clock.RenderTick;
        for (uint tick = 160; tick < 200; tick++)
        {
            clock.BeginTick(tick);
            clock.CompleteTick(tick);
            clock.Advance(TickSeconds + (tick % 2 == 0 ? -.005 : .005));
            Check(clock.RenderTick >= previous && clock.RenderTick <= clock.CompletedTick,
                "Jitter never rewinds or runs beyond confirmed time");
            previous = clock.RenderTick;
        }
        Check(clock.CurrentTick - clock.RenderTick < 1.5, "Jitter correction does not accumulate delay");
        clock.Reset();
        clock.BeginTick(uint.MaxValue - 1);
        clock.CompleteTick(uint.MaxValue - 1);
        clock.Advance(TickSeconds);
        Check(clock.BeginTick(uint.MaxValue), "Accept the last uint tick");
        clock.CompleteTick(uint.MaxValue);
        clock.Advance(TickSeconds);
        Check(clock.BeginTick(0) && clock.CurrentTick == (long)uint.MaxValue + 1, "Unwrap the uint32 boundary");
        Check(clock.CompleteTick(0), "Completion survives uint32 wrap");
        previous = clock.RenderTick;
        clock.Advance(TickSeconds / 2);
        Check(Math.Abs(clock.RenderTick - previous - .5) < .00001, "Fractional interpolation survives tick wrap");
    }

    private static void At(Unit unit, float x, string label) => Check(unit.GlobalPosition.IsEqualApprox(new Vector3(x, 0, 0)),
        $"{label}: expected {x}, got {unit.GlobalPosition}");
    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
    }
}
