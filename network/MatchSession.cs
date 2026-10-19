using Godot;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

public sealed class MatchAssignment
{
    public string AI { get; set; }
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
        Port is > 0 and <= 65535 && Ticket?.Length == 32 && Players == 8 &&
        (Role == "SPECTATOR" && Team == 0 && Hero == 0 && Bots == Players ||
         Team is 1 or 2 && Bots >= 0 && Bots < Players &&
         (Role == "COMMANDER" && Hero == 0 || Role == "HERO" && UnitCatalog.IsHero(Hero)));
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
    // The server keeps a dropped seat for the AI until the match ends, so the same ticket can
    // come back: in place while the game scene is open, or from the lobby (even after a restart).
    public bool Reconnecting { get; private set; }
    public bool Rejoining { get; private set; }
    public MatchAssignment Rejoinable { get; private set; }
    private const double SilenceLimit = 5, ReconnectWindow = 120, RetryInterval = 2;
    private string _rejoinUrl;
    private double _silence, _reconnectElapsed, _retryIn;
    private bool _attempting;
    private CanvasLayer _reconnectOverlay;
    private Label _reconnectLabel;
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

    // Godot names the user folder after config/name, so an address saved before the rename to
    // Burn Quarter is still in the Project W folder beside it until the next queue saves a new one.
    private static string LegacyNetworkConfig() => OS.GetUserDataDir().GetBaseDir().PathJoin("Project W").PathJoin("network.cfg");

    public override void _Ready()
    {
        var config = new ConfigFile();
        if (config.Load("user://network.cfg") == Error.Ok || config.Load(LegacyNetworkConfig()) == Error.Ok)
            LobbyUrl = config.GetValue("network", "lobby_url", LobbyUrl).AsString();
        foreach (string argument in OS.GetCmdlineUserArgs())
        {
            if (argument == "--spectate") PracticeRole = "SPECTATOR";
            if (argument.StartsWith("--lobby-url=", StringComparison.Ordinal))
            {
                LobbyUrl = argument[12..];
                RememberAddress = false; // A local launcher must not replace the saved remote address.
            }
        }
        _net = GetNode<NetClient>("/root/Net");
        _net.MessageReceived += OnMessage;
        _net.ConnectionClosed += OnClosed;
    }

    public void StartQueue(string url)
    {
        if (IsQueued || Match != null) return;
        if (!UnitCatalog.IsHero(SelectedHero)) { Fail("선택할 수 없는 영웅입니다."); return; }
        if (PracticeRole is not ("" or "HERO" or "COMMANDER" or "SPECTATOR")) { Fail("선택할 수 없는 연습 모드입니다."); return; }
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https") || uri.AbsolutePath != "/" || uri.Query != "" || uri.Fragment != "" || uri.UserInfo != "")
        { Fail("서버 주소를 http://주소:8080 형식으로 입력해 주세요."); return; }
        LobbyUrl = uri.GetLeftPart(UriPartial.Authority);
        if (RememberAddress) SaveSetting("network", "lobby_url", LobbyUrl);
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
        Rejoining = false;
        EndReconnect();
        string url = _queueUrl;
        _queueUrl = null;
        // A started match's entry stays until its game exits: it is how this seat comes back.
        if (url != null && url != _rejoinUrl) _cleanupTask = ReleaseQueue(url, _pollTask);
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
        // Leaving a running match hands the seat to the AI; the lobby can bring it back.
        if (InGame && Started && Result == null && !Reconnecting) _net.Leave("LEAVE");
        else _net.Disconnect();
        if (Result != null) ForgetRejoin();
        InGame = false;
        _results?.QueueFree();
        _results = null;
        Result = null;
        _gameMenu?.QueueFree();
        _gameMenu = null;
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
        _silence = 0;
        if (message == "MATCH_START")
        {
            Started = true;
            Rejoining = false;
            RememberRejoin(_queueUrl);
            EndReconnect();
            Changed?.Invoke();
        }
        else if (!Started && message.StartsWith("ERR ", StringComparison.Ordinal)) Fail(message[4..]);
    }

    private void OnClosed(string reason)
    {
        if (Match == null || Result != null) return;
        _attempting = false;
        if (InGame && Started) BeginReconnect();
        else Fail(reason);
    }

    public override void _Process(double delta)
    {
        if (Match == null || Result != null) { _heartbeat = 0; return; }
        if (Reconnecting) { ContinueReconnect(delta); return; }
        _heartbeat += delta;
        if (_heartbeat >= 10) { _heartbeat = 0; _net.Send("PING"); }
        // A running server sends TICK every frame. Silence means a dead link the OS has not
        // reported yet; a long local stall must not look like one, hence the per-frame cap.
        if (!InGame || !Started) return;
        _silence += Math.Min(delta, 0.5);
        if (_silence < SilenceLimit) return;
        _net.Disconnect();
        BeginReconnect();
    }

    private void BeginReconnect()
    {
        if (Reconnecting) return;
        Reconnecting = true;
        _reconnectElapsed = 0;
        _retryIn = 0.5;
        _attempting = false;
        ShowReconnect();
        Changed?.Invoke();
    }

    private void ContinueReconnect(double delta)
    {
        _reconnectElapsed += delta;
        if (_reconnectElapsed > ReconnectWindow)
        {
            Fail("서버에 다시 연결하지 못했습니다. 경기가 계속되고 있으면 로비에서 돌아갈 수 있습니다.");
            return;
        }
        _reconnectLabel.Text = $"서버와 연결이 끊겼습니다. 다시 연결하는 중… {(int)_reconnectElapsed}초\n그동안 AI가 대신 조종합니다.";
        _retryIn -= delta;
        if (_attempting || _retryIn > 0) return;
        _retryIn = RetryInterval;
        _attempting = true;
        _ = Reconnect(_generation);
    }

    // The lobby knows whether the match is still running; the game socket alone cannot tell
    // a finished match from a network problem.
    private async Task Reconnect(int generation)
    {
        if (await LookUpMatch(_rejoinUrl) is { State: "gone" })
        {
            if (generation != _generation || !Reconnecting) return;
            ForgetRejoin();
            Fail("경기가 끝났습니다.");
            return;
        }
        if (generation != _generation || !Reconnecting || Match == null) return;
        _net.ConnectToServer(Match.Host, Match.Port); // Main resends MAP_READY with the ticket.
    }

    private void EndReconnect()
    {
        Reconnecting = false;
        _attempting = false;
        _silence = 0;
        _reconnectOverlay?.QueueFree();
        _reconnectOverlay = null;
        _reconnectLabel = null;
    }

    private void ShowReconnect()
    {
        _reconnectOverlay = new CanvasLayer { Layer = 110 };
        var shade = new ColorRect { Color = new Color(0.04f, 0.07f, 0.09f, 0.72f) };
        shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _reconnectOverlay.AddChild(shade);
        var box = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        box.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        box.AddThemeConstantOverride("separation", 14);
        shade.AddChild(box);
        _reconnectLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _reconnectLabel.AddThemeFontSizeOverride("font_size", 18);
        box.AddChild(_reconnectLabel);
        var leave = new Button { Text = "로비로 나가기", SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        leave.Pressed += ReturnToLobby;
        box.AddChild(leave);
        AddChild(_reconnectOverlay);
        _reconnectLabel.Text = "서버와 연결이 끊겼습니다. 다시 연결하는 중…\n그동안 AI가 대신 조종합니다.";
    }

    // Offers the lobby a way back into a match this client left or lost while it still runs.
    public async void CheckRejoin()
    {
        if (_rejoinUrl == null && RememberAddress)
        {
            var config = new ConfigFile();
            if (config.Load("user://network.cfg") == Error.Ok) _rejoinUrl = config.GetValue("match", "rejoin_url", "").AsString();
            if (_rejoinUrl == "") _rejoinUrl = null;
        }
        if (_rejoinUrl == null || IsQueued || Match != null) return;
        string url = _rejoinUrl;
        QueueStatus status = await LookUpMatch(url);
        if (url != _rejoinUrl || IsQueued || Match != null) return;
        if (status?.State == "matched" && status.Match?.IsValid == true) Rejoinable = status.Match;
        else if (status != null) ForgetRejoin(); // Gone or failed; null means the lobby is unreachable.
        Changed?.Invoke();
    }

    public void Rejoin()
    {
        if (Rejoinable == null || IsQueued || Match != null) return;
        ++_generation;
        ErrorMessage = null;
        Result = null;
        Started = false;
        Rejoining = true;
        Match = Rejoinable;
        Rejoinable = null;
        _queueUrl = _rejoinUrl;
        Changed?.Invoke();
    }

    public void ForgetRejoin()
    {
        _rejoinUrl = null;
        Rejoinable = null;
        if (RememberAddress) SaveSetting("match", "rejoin_url", null);
    }

    private void RememberRejoin(string url)
    {
        if (url == null || url == _rejoinUrl) return;
        _rejoinUrl = url;
        if (RememberAddress) SaveSetting("match", "rejoin_url", url);
    }

    // State "gone" when the lobby no longer has the entry (the match ended); null if unreachable.
    private async Task<QueueStatus> LookUpMatch(string url)
    {
        if (url == null) return null;
        try
        {
            using var response = await _http.GetAsync(url);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return new QueueStatus { State = "gone" };
            if (!response.IsSuccessStatusCode) return null;
            return JsonSerializer.Deserialize<QueueStatus>(await response.Content.ReadAsStringAsync(), JsonOptions);
        }
        catch (Exception) { return null; }
    }

    private static void SaveSetting(string section, string key, string value)
    {
        var config = new ConfigFile();
        config.Load("user://network.cfg"); // Keep the other saved keys.
        if (value == null) { if (config.HasSectionKey(section, key)) config.EraseSectionKey(section, key); }
        else config.SetValue(section, key, value);
        config.Save("user://network.cfg");
    }

    public override void _ExitTree()
    {
        ++_generation;
        _net.MessageReceived -= OnMessage;
        _net.ConnectionClosed -= OnClosed;
        _http.Dispose();
    }
}
