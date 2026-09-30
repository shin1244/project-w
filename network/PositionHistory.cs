using Godot;

// 매 완료 틱의 위치를 보관합니다. POS가 없는 틱도 기록해야 정지 후 출발이 늘어지지 않습니다.
public sealed class PositionHistory
{
    private const int Capacity = 8;
    private readonly (long Tick, Vector3 Position)[] _samples = new (long, Vector3)[Capacity];
    private int _start;
    private int _count;

    public void Clear() { _start = 0; _count = 0; }

    public void Add(long tick, Vector3 position)
    {
        if (_count > 0)
        {
            int last = (_start + _count - 1) % Capacity;
            if (tick < _samples[last].Tick) return;
            if (tick == _samples[last].Tick) { _samples[last] = (tick, position); return; }
        }
        if (_count == Capacity) { _start = (_start + 1) % Capacity; _count--; }
        _samples[(_start + _count++) % Capacity] = (tick, position);
    }

    public Vector3 Sample(double tick, Vector3 fallback)
    {
        if (_count == 0) return fallback;
        var before = _samples[_start];
        if (tick <= before.Tick) return before.Position;
        for (int i = 1; i < _count; i++)
        {
            var after = _samples[(_start + i) % Capacity];
            if (tick <= after.Tick)
                return before.Position.Lerp(after.Position, (float)((tick - before.Tick) / (after.Tick - before.Tick)));
            before = after;
        }
        // 아직 받지 않은 위치를 예측하지 않고 마지막 확정 위치에서 기다립니다.
        return before.Position;
    }
}
