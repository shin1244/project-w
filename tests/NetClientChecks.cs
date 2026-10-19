using Godot;
using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// Standalone transport regression check: no live server or socket permissions needed.
public partial class NetClientChecks : Node
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public override async void _Ready()
    {
        try
        {
            await CheckOrderedWrites();
            await CheckSessionIsolation();
            await CheckQueueLimit();
            await CheckWriteTimeout();
            await CheckLeaveWritesFarewell();
            GD.Print("PASS: nonblocking ordered writes, session cancellation, bounded queue, write timeout, main-thread errors, farewell before close");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private static StreamWriter Attach(NetClient net, Stream stream)
    {
        var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, true) { AutoFlush = true };
        typeof(NetClient).GetField("_writer", Private).SetValue(net, writer);
        return writer;
    }

    private static object Queue(NetClient net) => typeof(NetClient).GetField("_sendQueue", Private).GetValue(net);
    private static Task Tail(NetClient net) => (Task)Queue(net).GetType().GetField("Tail").GetValue(Queue(net));
    private static void Check(bool value, string reason)
    { if (!value) throw new InvalidOperationException(reason); }

    private static async Task CheckOrderedWrites()
    {
        using var net = new NetClient();
        using var stream = new StalledStream();
        var writer = Attach(net, stream);
        int closed = 0;
        net.ConnectionClosed += _ => closed++;
        net.Send("FIRST");
        net.Send("SECOND");
        Task pending = Tail(net);
        Check(!pending.IsCompleted && stream.WriteStarts == 1 && closed == 0,
            "Send must return while a write is blocked and serialize the next write");
        stream.Release();
        await pending.WaitAsync(TimeSpan.FromSeconds(2));
        Check(stream.Text.Replace("\r", "") == "FIRST\nSECOND\n", "Commands must reach the stream exactly once in order");
        net.Disconnect();
        await writer.DisposeAsync();
    }

    private static async Task CheckSessionIsolation()
    {
        using var net = new NetClient();
        using var oldStream = new StalledStream { IgnoreCancellation = true, FailAfterRelease = true };
        var oldWriter = Attach(net, oldStream);
        int closed = 0;
        net.ConnectionClosed += _ => closed++;
        net.Send("OLD_FIRST");
        net.Send("OLD_QUEUED");
        Task oldPending = Tail(net);
        net.Disconnect();
        using var fresh = new MemoryStream();
        var freshWriter = Attach(net, fresh);
        net.Send("NEW");
        await Tail(net).WaitAsync(TimeSpan.FromSeconds(2));
        oldStream.Release();
        await oldPending.WaitAsync(TimeSpan.FromSeconds(2));
        net._Process(0);
        Check(closed == 0 && oldStream.WriteStarts == 1 && Encoding.UTF8.GetString(fresh.ToArray()).Trim() == "NEW",
            "Cancelled commands and late errors must not affect a replacement connection");
        net.Disconnect();
        try { await oldWriter.DisposeAsync(); } catch (IOException) { }
        await freshWriter.DisposeAsync();
    }

    private static async Task CheckQueueLimit()
    {
        using var net = new NetClient();
        using var stream = new StalledStream();
        var writer = Attach(net, stream);
        int closed = 0, callbackThread = 0;
        net.ConnectionClosed += _ => { closed++; callbackThread = System.Environment.CurrentManagedThreadId; };
        int capacity = (int)typeof(NetClient).GetField("MaxPendingWrites", BindingFlags.Static | BindingFlags.NonPublic).GetRawConstantValue();
        for (int i = 0; i < capacity + 100; i++) net.Send("QUEUED");
        Task pending = Tail(net);
        Check(closed == 0 && (int)Queue(net).GetType().GetField("Pending").GetValue(Queue(net)) <= capacity,
            "Backpressure must cap pending commands and defer error delivery");
        int mainThread = System.Environment.CurrentManagedThreadId;
        net._Process(0);
        await pending.WaitAsync(TimeSpan.FromSeconds(2));
        net._Process(0);
        Check(closed == 1 && callbackThread == mainThread && stream.WriteStarts == 1,
            "Queue overflow must close once on the main thread and cancel queued writes");
        stream.Release();
        await writer.DisposeAsync();
    }

    private static async Task CheckWriteTimeout()
    {
        using var net = new NetClient();
        using var stream = new StalledStream();
        var writer = Attach(net, stream);
        string reason = null;
        net.ConnectionClosed += message => reason = message;
        net.Send("TIMEOUT");
        await Tail(net).WaitAsync(TimeSpan.FromSeconds(8));
        Check(reason == null, "Background write failure must not invoke scene callbacks");
        net._Process(0);
        Check(reason?.Contains("시간이 초과") == true, "A stalled write must time out and close the connection");
        stream.Release();
        await writer.DisposeAsync();
    }

    private static async Task CheckLeaveWritesFarewell()
    {
        using var net = new NetClient();
        using var stream = new StalledStream();
        var writer = Attach(net, stream);
        int closed = 0;
        net.ConnectionClosed += _ => closed++;
        net.Send("LAST_ORDER");
        Task pending = Tail(net);
        net.Leave("LEAVE");
        Check(Queue(net) == null, "Leave must free the client for a new connection at once");
        using var fresh = new MemoryStream();
        var freshWriter = Attach(net, fresh);
        net.Send("NEW");
        await Tail(net).WaitAsync(TimeSpan.FromSeconds(2));
        stream.Release();
        await pending.WaitAsync(TimeSpan.FromSeconds(2));
        for (int i = 0; i < 100 && !stream.Text.Contains("LEAVE"); i++) await Task.Delay(10);
        net._Process(0);
        Check(stream.Text.Replace("\r", "") == "LAST_ORDER\nLEAVE\n" && Encoding.UTF8.GetString(fresh.ToArray()).Trim() == "NEW" && closed == 0,
            "Leave must write its farewell after queued commands without touching the next connection");
        net.Disconnect();
        await writer.DisposeAsync();
        await freshWriter.DisposeAsync();
    }

    private sealed class StalledStream : MemoryStream
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool IgnoreCancellation, FailAfterRelease;
        public int WriteStarts;
        public string Text => Encoding.UTF8.GetString(ToArray());
        public void Release() => _release.TrySetResult();
        public override void Write(byte[] buffer, int offset, int count) => throw new InvalidOperationException("Synchronous network write");
        public override void Write(ReadOnlySpan<byte> buffer) => throw new InvalidOperationException("Synchronous network write");
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellation = default)
        {
            Interlocked.Increment(ref WriteStarts);
            if (IgnoreCancellation) await _release.Task.ConfigureAwait(false);
            else await _release.Task.WaitAsync(cancellation).ConfigureAwait(false);
            if (FailAfterRelease) throw new IOException("Old connection failed");
            byte[] bytes = buffer.ToArray();
            base.Write(bytes, 0, bytes.Length);
        }
    }
}
