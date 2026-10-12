using Godot;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading;

public partial class NetClient : Node
{
    private const int MaxPendingWrites = 256;
    private static readonly TimeSpan WriteTimeout = TimeSpan.FromSeconds(5);
    public event Action<string> MessageReceived;
    public event Action<string> ConnectionClosed;

    private TcpClient _tcp;
    private StreamWriter _writer;
    private CancellationTokenSource _lifetime;
    private SendQueue _sendQueue;
    private int _session;
    private readonly ConcurrentQueue<(int Session, string Message, bool Closed)> _inbox = new();

    private sealed class SendQueue
    {
        public readonly StreamWriter Writer;
        public readonly CancellationToken Cancellation;
        public readonly int Session;
        public Task Tail = Task.CompletedTask;
        public int Pending, Failed;

        public SendQueue(StreamWriter writer, CancellationToken cancellation, int session)
        { Writer = writer; Cancellation = cancellation; Session = session; }
    }

    public async void ConnectToServer(string host, int port)
    {
        Disconnect();
        int session = _session;
        _lifetime = new CancellationTokenSource();
        CancellationToken cancellation = _lifetime.Token;
        var tcp = new TcpClient { NoDelay = true };
        _tcp = tcp;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            await tcp.ConnectAsync(host, port, timeout.Token);
            if (session != _session) { tcp.Dispose(); return; }
            var stream = tcp.GetStream();
            _writer = new StreamWriter(stream) { AutoFlush = true };
            _ = Task.Run(() => ReadLoop(new StreamReader(stream), session, cancellation));
            GD.Print("서버 연결됨");
        }
        catch (Exception e)
        {
            tcp.Dispose();
            if (!cancellation.IsCancellationRequested)
                _inbox.Enqueue((session, $"연결 실패: {e.Message}", true));
        }
    }

    // 백그라운드 스레드: 받은 줄을 큐에 넣기만 함
    private async Task ReadLoop(StreamReader reader, int session, CancellationToken cancellation)
    {
        string reason = "서버 연결이 종료되었습니다.";
        try
        {
            using (reader)
            {
                string line;
                while ((line = await reader.ReadLineAsync(cancellation).ConfigureAwait(false)) != null)
                    _inbox.Enqueue((session, line, false));
            }
        }
        catch (Exception error) { reason = "서버 연결 종료: " + error.Message; }
        finally
        {
            if (!cancellation.IsCancellationRequested)
                _inbox.Enqueue((session, reason, true));
        }
    }

    // 메인 스레드: 큐에서 꺼내 게임 코드에 전달
    public override void _Process(double delta)
    {
        while (_inbox.TryDequeue(out var msg))
        {
            if (msg.Session != _session) continue;
            if (msg.Closed) { Disconnect(); ConnectionClosed?.Invoke(msg.Message); }
            else MessageReceived?.Invoke(msg.Message);
        }
    }

    public void Send(string line)
    {
        if (_writer == null) return;
        _lifetime ??= new CancellationTokenSource();
        if (_sendQueue == null || _sendQueue.Writer != _writer)
            _sendQueue = new SendQueue(_writer, _lifetime.Token, _session);
        SendQueue queue = _sendQueue;
        if (Volatile.Read(ref queue.Failed) != 0 || queue.Cancellation.IsCancellationRequested) return;
        if (Interlocked.Increment(ref queue.Pending) > MaxPendingWrites)
        {
            Interlocked.Decrement(ref queue.Pending);
            FailSend(queue, "전송 대기열이 가득 찼습니다. 서버 연결을 확인해 주세요.");
            return;
        }
        // Each operation captures this connection's writer and waits for the prior
        // write. Network backpressure never blocks the Godot main thread.
        queue.Tail = WriteQueued(queue, queue.Tail, line);
    }

    private async Task WriteQueued(SendQueue queue, Task previous, string line)
    {
        try
        {
            await previous.ConfigureAwait(false);
            if (queue.Cancellation.IsCancellationRequested || Volatile.Read(ref queue.Failed) != 0) return;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(queue.Cancellation);
            timeout.CancelAfter(WriteTimeout);
            await queue.Writer.WriteLineAsync(line.AsMemory(), timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (queue.Cancellation.IsCancellationRequested) { }
        catch (Exception error)
        {
            FailSend(queue, error is OperationCanceledException
                ? "전송 시간이 초과되었습니다. 서버 연결을 확인해 주세요."
                : "전송 실패: " + error.Message);
        }
        finally { Interlocked.Decrement(ref queue.Pending); }
    }

    private void FailSend(SendQueue queue, string reason)
    {
        if (Interlocked.Exchange(ref queue.Failed, 1) == 0)
            _inbox.Enqueue((queue.Session, reason, true));
    }

    public void Disconnect()
    {
        _session++;
        _lifetime?.Cancel();
        _lifetime?.Dispose();
        _lifetime = null;
        _tcp?.Close();
        _tcp = null;
        _writer = null;
        _sendQueue = null;
        _inbox.Clear();
    }

    public override void _ExitTree()
    {
        Disconnect();
        // This autoload exits after the game scene. Finalize temporary native Variants
        // before Godot shuts down its object/Variant runtime (also in headless checks).
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }
}
