using System;

// 모든 유닛이 공유하는 재생 시계. TICK N은 N의 시작이므로 N-1까지만 완성된 상태입니다.
public sealed class InterpolationClock
{
    public const int TickRate = 20;
    public const int DelayTicks = 2; // 100ms. 서버 tickRate와 함께 변경합니다.
    public bool HasTick { get; private set; }
    public long CurrentTick { get; private set; }
    public double RenderTick { get; private set; }
    private uint _wireTick;
    private double _ticksSinceArrival;

    public bool BeginTick(uint tick)
    {
        if (!HasTick)
        {
            HasTick = true;
            CurrentTick = tick;
            RenderTick = CurrentTick - DelayTicks;
        }
        else
        {
            // uint32 래핑을 허용하고 중복/역행한 틱은 무시합니다.
            int distance = unchecked((int)(tick - _wireTick));
            if (distance <= 0) return false;
            CurrentTick += distance;
            // 긴 멈춤 뒤 밀린 패킷은 재생하지 않고 최근 버퍼로 복귀합니다.
            if (CurrentTick - DelayTicks - RenderTick > 4)
                RenderTick = CurrentTick - DelayTicks;
        }
        _wireTick = tick;
        _ticksSinceArrival = 0;
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
        RenderTick = Math.Min(CurrentTick - 1, RenderTick + step * speed);
    }

    public void Reset()
    {
        HasTick = false;
        CurrentTick = 0;
        RenderTick = 0;
        _wireTick = 0;
        _ticksSinceArrival = 0;
    }
}
