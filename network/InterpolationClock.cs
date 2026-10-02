using System;

// 모든 유닛이 공유하는 재생 시계. TICK_END로 완료된 틱까지만 재생합니다.
public sealed class InterpolationClock
{
    public const int TickRate = 30; // 서버 tickRate와 함께 변경합니다.
    public const double DelaySeconds = .05;
    public const double DelayTicks = TickRate * DelaySeconds;
    public bool HasTick { get; private set; }
    public long CurrentTick { get; private set; }
    public long CompletedTick { get; private set; }
    public double RenderTick { get; private set; }
    private uint _wireTick;
    private double _ticksSinceArrival;

    public bool BeginTick(uint tick)
    {
        if (!HasTick)
        {
            HasTick = true;
            CurrentTick = tick;
            CompletedTick = CurrentTick - 1;
            RenderTick = CurrentTick - DelayTicks;
        }
        else
        {
            // uint32 래핑을 허용하고 중복/역행한 틱은 무시합니다.
            int distance = unchecked((int)(tick - _wireTick));
            if (distance <= 0) return false;
            // 다음 틱의 시작도 앞 틱의 완료를 보장합니다.
            CompletedTick = CurrentTick;
            CurrentTick += distance;
            // 긴 멈춤 뒤 밀린 패킷은 재생하지 않고 최근 버퍼로 복귀합니다.
            if (CurrentTick - DelayTicks - RenderTick > 4)
                RenderTick = CurrentTick - DelayTicks;
        }
        _wireTick = tick;
        _ticksSinceArrival = 0;
        return true;
    }

    public bool CompleteTick(uint tick)
    {
        if (!HasTick || tick != _wireTick || CompletedTick == CurrentTick) return false;
        CompletedTick = CurrentTick;
        return true;
    }

    public void Advance(double delta)
    {
        if (!HasTick || !double.IsFinite(delta) || delta <= 0) return;
        double step = delta * TickRate;
        double target = CurrentTick - DelayTicks + _ticksSinceArrival;
        double error = target - RenderTick;
        // 도착 간격의 작은 흔들림은 위치 점프 대신 재생 속도로 보정합니다.
        double speed = error > .1 ? 1.1 : error < -.1 ? .9 : 1;
        _ticksSinceArrival += step;
        RenderTick = Math.Min(CompletedTick, RenderTick + step * speed);
    }

    public void Reset()
    {
        HasTick = false;
        CurrentTick = 0;
        CompletedTick = 0;
        RenderTick = 0;
        _wireTick = 0;
        _ticksSinceArrival = 0;
    }
}
