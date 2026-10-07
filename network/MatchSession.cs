using Godot;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

public sealed class MatchAssignment
{
    public string MatchId { get; set; }
    public string Host { get; set; }
    public int Port { get; set; }
    public string Ticket { get; set; }
    public uint Team { get; set; }
    public string Role { get; set; }
    public uint Hero { get; set; }
    public int Players { get; set; }
    public int Bots { get; set; }
    public bool IsValid => !string.IsNullOrWhiteSpace(MatchId) && !string.IsNullOrWhiteSpace(Host) &&
        Port is > 0 and <= 65535 && Ticket?.Length == 32 && Team is 1 or 2 && Players == 6 && Bots >= 0 && Bots < Players &&
        (Role == "COMMANDER" && Hero == 0 || Role == "HERO" && UnitCatalog.IsHero(Hero));
}

public sealed class QueueStatus
{
    public string State { get; set; }
    public int Count { get; set; }
    public int Position { get; set; }
    public int Seconds { get; set; }
    public bool Busy { get; set; }
    public string Message { get; set; }
    public MatchAssignment Match { get; set; }
}

// Persists queue/admission data across scenes. Gameplay state remains in Main.
public partial class MatchSession : Node
{
    public event Action Changed;
    public string LobbyUrl { get; private set; } = "http://127.0.0.1:8080";
    public bool RememberAddress { get; set; } = true;
    public uint SelectedHero { get; set; } = UnitCatalog.HeroTest;
    public string PracticeRole { get; set; } = "";
    public MatchResultSnapshot Result { get; private set; }
    public string ErrorMessage { get; private set; }
    public bool IsQueued { get; private set; }
    public bool Started { get; private set; }
    public bool InGame { get; private set; }
    public QueueStatus Status { get; private set; }
    public MatchAssignment Match { get; private set; }
    private readonly System.Net.Http.HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private NetClient _net;
    private string _queueUrl;
    private int _generation;
    private double _heartbeat;
    private CanvasLayer _gameMenu;
    private MatchResultPanel _results;
    private Task _pollTask = Task.CompletedTask;
    private Task _cleanupTask = Task.CompletedTask;

    public override void _Ready()
    {
        var config = new ConfigFile();
        if (config.Load("user://network.cfg") == Error.Ok)
            LobbyUrl = config.GetValue("network", "lobby_url", LobbyUrl).AsString();
        _net = GetNode<NetClient>("/root/Net");
        _net.MessageReceived += OnMessage;
        _net.ConnectionClosed += OnClosed;
    }

    public void StartQueue(string url)
    {
        if (IsQueued || Match != null) return;
        if (!UnitCatalog.IsHero(SelectedHero)) { Fail("선택할 수 없는 영웅입니다."); return; }
        if (PracticeRole is not ("" or "HERO" or "COMMANDER")) { Fail("선택할 수 없는 연습 모드입니다."); return; }
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https") || uri.AbsolutePath != "/" || uri.Query != "" || uri.Fragment != "" || uri.UserInfo != "")
        { Fail("서버 주소를 http://주소:8080 형식으로 입력해 주세요."); return; }
        LobbyUrl = uri.GetLeftPart(UriPartial.Authority);
        if (RememberAddress)
        {
            var config = new ConfigFile();
            config.SetValue("network", "lobby_url", LobbyUrl);
            config.Save("user://network.cfg");
        }
        ErrorMessage = null;
        Result = null;
        Started = false;
        Status = null;
        IsQueued = true;
        _queueUrl = $"{LobbyUrl}/queue/{Guid.NewGuid():N}?hero={SelectedHero}";
        if (PracticeRole != "") _queueUrl += $"&practice={PracticeRole}";
        int generation = ++_generation;
        Changed?.Invoke();
        _pollTask = PollQueue(_queueUrl, generation, _cleanupTask);
    }

    private async Task PollQueue(string queueUrl, int generation, Task previousCleanup)
    {
        // Finish the old PUT and its DELETE before a fast cancel/requeue can add
        // a second seat for this client.
        await previousCleanup;
        bool joined = false;
        int failures = 0;
        while (generation == _generation && IsQueued)
        {
            try
            {
                using var request = new HttpRequestMessage(joined ? HttpMethod.Get : HttpMethod.Put, queueUrl);
                using var response = await _http.SendAsync(request);
                if (generation != _generation) { await DeleteQueue(queueUrl); return; }
                response.EnsureSuccessStatusCode();
                string body = await response.Content.ReadAsStringAsync();
                if (generation != _generation) { await DeleteQueue(queueUrl); return; }
                var status = JsonSerializer.Deserialize<QueueStatus>(body, JsonOptions);
                if (status == null) throw new InvalidOperationException("비어 있는 로비 응답");
                joined = true;
                Status = status;
                if (status.State == "error") { Fail(status.Message ?? "매칭이 종료되었습니다."); return; }
                if (status.State == "matched")
                {
                    if (status.Match?.IsValid != true) throw new InvalidOperationException("잘못된 매칭 정보");
                    Match = status.Match;
                    IsQueued = false;
                    Changed?.Invoke();
                    return;
                }
                if (status.State is not ("queued" or "starting")) throw new InvalidOperationException("지원하지 않는 로비 응답");
                failures = 0;
                Changed?.Invoke();
            }
            catch (Exception error)
            {
                if (generation != _generation) { await DeleteQueue(queueUrl); return; }
                if (++failures >= 3) { Fail("로비에 연결할 수 없습니다. 서버 주소와 실행 상태를 확인해 주세요. " + error.Message); return; }
            }
            await Task.Delay(500);
        }
    }

    public void CancelQueue()
    {
        ++_generation;
        IsQueued = false;
        Status = null;
        Match = null;
        Started = false;
        string url = _queueUrl;
        _queueUrl = null;
        if (url != null) _cleanupTask = ReleaseQueue(url, _pollTask);
        Changed?.Invoke();
    }

    private async Task ReleaseQueue(string url, Task previousPoll)
    {
        try { await previousPoll; }
        catch (Exception) { }
        await DeleteQueue(url);
    }

    private async Task DeleteQueue(string url)
    {
        try { using var response = await _http.DeleteAsync(url); }
        catch (Exception) { /* A lost queue lease expires on the server. */ }
    }

    public void Fail(string message)
    {
        ErrorMessage = message;
        CancelQueue();
        _net?.Disconnect();
        if (InGame) Callable.From(ReturnToLobby).CallDeferred();
    }

    public void EnterGame()
    {
        InGame = true;
        _gameMenu = new CanvasLayer { Layer = 50 };
        var back = new Button { Text = "로비로 나가기", Position = new Vector2(12, 58) };
        back.Pressed += ReturnToLobby;
        _gameMenu.AddChild(back);
        AddChild(_gameMenu);
    }

    public void ReturnToLobby()
    {
        InGame = false;
        _results?.QueueFree();
        _results = null;
        Result = null;
        _gameMenu?.QueueFree();
        _gameMenu = null;
        _net.Disconnect();
        CancelQueue();
        GetTree().ChangeSceneToFile("res://lobby/Lobby.tscn");
    }

    public void Finish(MatchResultSnapshot result, uint localTeam)
    {
        if (Result != null) return;
        Result = result;
        _gameMenu?.Hide();
        _results = new MatchResultPanel { Result = result, LocalTeam = localTeam };
        _results.BackRequested += ReturnToLobby;
        AddChild(_results);
        Changed?.Invoke();
    }

    private void OnMessage(string message)
    {
        if (Match == null) return;
        if (message == "MATCH_START") { Started = true; Changed?.Invoke(); }
        else if (!Started && message.StartsWith("ERR ", StringComparison.Ordinal)) Fail(message[4..]);
    }

    private void OnClosed(string reason) { if (Match != null && Result == null) Fail(reason); }

    public override void _Process(double delta)
    {
        if (Match == null || Result != null) { _heartbeat = 0; return; }
        _heartbeat += delta;
        if (_heartbeat >= 10) { _heartbeat = 0; _net.Send("PING"); }
    }

    public override void _ExitTree()
    {
        ++_generation;
        _net.MessageReceived -= OnMessage;
        _net.ConnectionClosed -= OnClosed;
        _http.Dispose();
    }
}
