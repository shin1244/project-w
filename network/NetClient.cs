using Godot;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;

public partial class NetClient : Node
{
    public event Action<string> MessageReceived;
    public event Action<string> ConnectionClosed;

    private TcpClient _tcp;
    private StreamWriter _writer;
    private int _session;
    private readonly ConcurrentQueue<(int Session, string Message, bool Closed)> _inbox = new();

    public async void ConnectToServer(string host, int port)
    {
        Disconnect();
        int session = _session;
        var tcp = new TcpClient();
        _tcp = tcp;
        try
        {
            await tcp.ConnectAsync(host, port);
            if (session != _session) { tcp.Dispose(); return; }
            var stream = tcp.GetStream();
            _writer = new StreamWriter(stream) { AutoFlush = true };
            _ = Task.Run(() => ReadLoop(new StreamReader(stream), session));
            GD.Print("서버 연결됨");
        }
        catch (Exception e)
        {
            tcp.Dispose();
            _inbox.Enqueue((session, $"연결 실패: {e.Message}", true));
        }
    }

    // 백그라운드 스레드: 받은 줄을 큐에 넣기만 함
    private async Task ReadLoop(StreamReader reader, int session)
    {
        string reason = "서버 연결이 종료되었습니다.";
        try
        {
            using (reader)
            {
                string line;
                while ((line = await reader.ReadLineAsync()) != null)
                    _inbox.Enqueue((session, line, false));
            }
        }
        catch (Exception error) { reason = "서버 연결 종료: " + error.Message; }
        finally { _inbox.Enqueue((session, reason, true)); }
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
        try { _writer?.WriteLine(line); }
        catch (Exception error)
        {
            Disconnect();
            ConnectionClosed?.Invoke("전송 실패: " + error.Message);
        }
    }

    public void Disconnect()
    {
        _session++;
        _tcp?.Close();
        _tcp = null;
        _writer = null;
        _inbox.Clear();
    }

    public override void _ExitTree() => Disconnect();
}
