using Godot;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;

public partial class NetClient : Node
{
    public event Action<string> MessageReceived;

    private TcpClient _tcp;
    private StreamWriter _writer;
    private readonly ConcurrentQueue<string> _inbox = new();

    public async void ConnectToServer(string host, int port)
    {
        try
        {
            _tcp = new TcpClient();
            await _tcp.ConnectAsync(host, port);
            var stream = _tcp.GetStream();
            _writer = new StreamWriter(stream) { AutoFlush = true };
            _ = Task.Run(() => ReadLoop(new StreamReader(stream)));
            GD.Print("서버 연결됨");
        }
        catch (Exception e)
        {
            GD.PrintErr($"연결 실패: {e.Message}");
        }
    }

    // 백그라운드 스레드: 받은 줄을 큐에 넣기만 함
    private async Task ReadLoop(StreamReader reader)
    {
        string line;
        while ((line = await reader.ReadLineAsync()) != null)
            _inbox.Enqueue(line);
    }

    // 메인 스레드: 큐에서 꺼내 게임 코드에 전달
    public override void _Process(double delta)
    {
        while (_inbox.TryDequeue(out var msg))
            MessageReceived?.Invoke(msg);
    }

    public void Send(string line) => _writer?.WriteLine(line);

    public override void _ExitTree() => _tcp?.Close();
}