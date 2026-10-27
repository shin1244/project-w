using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

// Keeps the guest session, the room and the match admission across scenes: the lobby
// (room list), the room, loading and the game. Gameplay state remains in Main.
public partial class MatchSession : Node
{
    public const int LobbyProtocol = 1;
    public const int NameMaxLength = 16, RoomNameMaxLength = 24, ChatMaxLength = 200;
    private const string LobbyScene = "res://lobby/Lobby.tscn", RoomScene = "res://lobby/Room.tscn";
    private const int ChatKeep = 100;

    public event Action Changed;
    // The demo's lobby (project-w-server/docs/deploy-gcp.md). Exported builds always use it;
    // editor runs may type and remember another address, and --lobby-url= overrides both.
    public const string OfficialLobbyUrl = "https://burn-quarter.duckdns.org";
    public static bool AddressEditable => OS.IsDebugBuild();
    public string LobbyUrl { get; private set; } = OfficialLobbyUrl;
    public bool RememberAddress { get; set; } = true;
    public string PlayerName { get; private set; } = "";
    // --spectate: enter, open a room, watch an all-AI match (tools/Watch-Latest-AI.ps1).
    public bool AutoSpectate { get; private set; }
    public LobbySession Lobby { get; private set; }
    public bool Connecting { get; private set; }
    public string Notice { get; private set; } // The lobby's last refusal or a lost connection.
    public List<RoomSummary> Rooms { get; private set; } = new();
    public bool RoomsLoaded { get; private set; }
    public RoomState Room { get; private set; }
    public List<ChatLine> Chat { get; } = new();
    public IReadOnlyDictionary<string, int> Pings => _pings;
    public MatchResultSnapshot Result { get; private set; }
    public string ErrorMessage { get; private set; }
    public bool Started { get; private set; }
    public bool InGame { get; private set; }
    public MatchAssignment Match { get; private set; }
    // The server keeps a seat for the AI until the match ends, so the same ticket can come
    // back: in place while the game scene is open, or from the room (even after a restart).
    public bool Reconnecting { get; private set; }
    public bool Rejoining { get; private set; }
    public MatchAssignment Rejoinable { get; private set; }

    private const double SilenceLimit = 5, ReconnectWindow = 120, RetryInterval = 2;
    private double _silence, _reconnectElapsed, _retryIn;
    private bool _attempting;
    private CanvasLayer _reconnectOverlay;
    private Label _reconnectLabel;
    private readonly System.Net.Http.HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private NetClient _net;
    private int _generation, _lobbyGeneration, _chatSeq;
    private double _heartbeat;
    private CanvasLayer _gameMenu;
    private MatchResultPanel _results;
    private RegionPinger _pinger;
    private Dictionary<string, int> _pings = new(), _reportedPings;
    private string _token, _leftMatchId, _finishedMatchId;

    private readonly record struct Reply<T>(bool Ok, HttpStatusCode? Status, T Value, LobbyError Error);

    public override void _Ready()
    {
        var config = LoadConfig();
        if (AddressEditable) LobbyUrl = config.GetValue("network", "lobby_url", LobbyUrl).AsString();
        PlayerName = config.GetValue("player", "name", "").AsString();
        if (PlayerName == "") PlayerName = Lang.F("게스트{0}", Random.Shared.Next(1000, 10000));
        foreach (string argument in OS.GetCmdlineUserArgs())
        {
            if (argument == "--spectate") AutoSpectate = true;
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

    // Godot names the user folder after config/name, so an address saved before the rename to
    // Burn Quarter is still in the Project W folder beside it until the next save writes a new one.
    private static ConfigFile LoadConfig()
    {
        var config = new ConfigFile();
        if (config.Load("user://network.cfg") != Error.Ok)
            config.Load(OS.GetUserDataDir().GetBaseDir().PathJoin("Project W").PathJoin("network.cfg"));
        return config;
    }

    // ---------------------------------------------------------------- lobby HTTP

    private async Task<Reply<T>> Call<T>(HttpMethod method, string path, object body = null, string token = null, bool quiet = false)
    {
        int generation = _lobbyGeneration;
        token ??= Lobby?.Token;
        try
        {
            using var request = new HttpRequestMessage(method, LobbyUrl + path);
            if (token != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (body != null) request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            using var response = await _http.SendAsync(request);
            string text = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
                return new Reply<T>(true, response.StatusCode, text == "" ? default : JsonSerializer.Deserialize<T>(text, JsonOptions), null);
            LobbyError error = null;
            try { error = JsonSerializer.Deserialize<LobbyError>(text, JsonOptions); } catch (JsonException) { }
            string message = Describe(error) ?? Lang.F("로비가 요청을 거절했습니다 ({0}).", (int)response.StatusCode);
            if (response.StatusCode == HttpStatusCode.Unauthorized && token == Lobby?.Token && generation == _lobbyGeneration && !quiet)
                LoseLobby(message);
            else if (!quiet) SetNotice(message);
            return new Reply<T>(false, response.StatusCode, default, error);
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or JsonException)
        {
            if (!quiet) SetNotice(AddressEditable ? Lang.T("로비에 연결할 수 없습니다. 서버 주소와 실행 상태를 확인해 주세요.")
                : Lang.T("서버에 연결할 수 없습니다. 인터넷 연결을 확인하고 잠시 후 다시 시도해 주세요."));
            return new Reply<T>(false, null, default, null);
        }
    }

    // The lobby's refusal in this client's language, by its code (project-w-server
    // internal/lobby); a code this build does not know keeps the lobby's own words.
    private static string Describe(LobbyError error) => error?.Code switch
    {
        null => error?.Message,
        "BAD_REQUEST" => Lang.T("잘못된 요청입니다"),
        "NO_ROOM" => Lang.T("방이 없어졌습니다"),
        "NOT_IN_ROOM" => Lang.T("이 방에 들어가 있지 않습니다"),
        "NOT_HOST" => Lang.T("방장만 할 수 있습니다"),
        "ROOM_BUSY" => Lang.T("지금은 바꿀 수 없습니다"),
        "NO_SESSION" => Lang.T("로비 연결이 끊겼습니다. 다시 입장해 주세요"),
        "UPDATE_REQUIRED" => Lang.T("게임을 최신 버전으로 업데이트해 주세요"),
        "BAD_NAME" => Lang.F("이름은 1~{0}자로 정해 주세요.", NameMaxLength),
        "LOBBY_FULL" => Lang.T("로비가 가득 찼습니다. 잠시 뒤 다시 시도해 주세요"),
        "BAD_ROOM_NAME" => Lang.F("방 이름은 1~{0}자로 정해 주세요", RoomNameMaxLength),
        "TOO_MANY_ROOMS" => Lang.T("방이 너무 많습니다. 다른 방에 들어가 주세요"),
        "ROOM_PLAYING" => Lang.T("게임 중인 방에는 들어갈 수 없습니다"),
        "ROOM_FULL" => Lang.T("방이 가득 찼습니다"),
        "BAD_CHAT" => Lang.F("채팅은 1~{0}자까지 보낼 수 있습니다", ChatMaxLength),
        "CHAT_TOO_FAST" => Lang.T("채팅을 너무 빨리 보내고 있습니다"),
        "ROLE_TAKEN" => Lang.T("이미 다른 사람이 고른 역할입니다"),
        "TEAM_FULL" => Lang.T("이 팀의 영웅 자리가 모두 찼습니다"),
        _ => error.Message
    };

    private void SetNotice(string message) { Notice = message; Changed?.Invoke(); }

    public void ClearNotice() { if (Notice == null) return; Notice = null; Changed?.Invoke(); }

    // Enters the lobby as a guest. A session this client saved earlier is reused while the
    // lobby still knows it: it may hold a room or a running match to return to.
    public async void EnterLobby(string url, string name)
    {
        if (Connecting || Lobby != null) return;
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https") || uri.AbsolutePath != "/" || uri.Query != "" || uri.Fragment != "" || uri.UserInfo != "")
        { SetNotice(Lang.T("서버 주소를 https://주소 또는 http://주소:8080 형식으로 입력해 주세요.")); return; }
        name = name.Trim();
        if (name.Length is 0 or > NameMaxLength) { SetNotice(Lang.F("이름은 1~{0}자로 정해 주세요.", NameMaxLength)); return; }
        LobbyUrl = uri.GetLeftPart(UriPartial.Authority);
        PlayerName = name;
        var config = LoadConfig();
        string saved = RememberAddress && config.GetValue("lobby", "url", "").AsString() == LobbyUrl ? config.GetValue("lobby", "token", "").AsString() : _token;
        if (RememberAddress) { SaveSetting("network", "lobby_url", LobbyUrl); SaveSetting("player", "name", name); }
        Connecting = true;
        Notice = null;
        ErrorMessage = null;
        Changed?.Invoke();
        LobbySession session = null;
        LobbyMe me = null;
        if (!string.IsNullOrEmpty(saved))
        {
            var known = await Call<LobbyMe>(HttpMethod.Get, "/me", token: saved, quiet: true);
            var regions = known.Ok ? await Call<List<RegionInfo>>(HttpMethod.Get, "/regions", token: saved, quiet: true) : default;
            if (known.Ok && regions.Ok)
            {
                me = known.Value;
                session = new LobbySession { Id = me.Id, Token = saved, Name = me.Name, Regions = regions.Value ?? new() };
            }
        }
        if (session == null)
        {
            var created = await Call<LobbySession>(HttpMethod.Post, "/session", new { name, protocol = LobbyProtocol });
            session = created.Value;
        }
        Connecting = false;
        if (session == null) { Changed?.Invoke(); return; }
        StartSession(session);
        if (me?.Room != null)
        {
            var room = await Call<RoomState>(HttpMethod.Get, $"/rooms/{me.Room}", quiet: true);
            if (room.Ok) { EnterRoom(room.Value, me.Match?.MatchId); return; }
        }
        RefreshRooms();
    }

    private void StartSession(LobbySession session)
    {
        Lobby = session;
        _token = session.Token;
        if (RememberAddress) { SaveSetting("lobby", "url", LobbyUrl); SaveSetting("lobby", "token", session.Token); }
        _pinger?.Dispose();
        _pinger = new RegionPinger(session.Regions);
        _pings = new();
        _reportedPings = null;
        _ = LobbyLoop(++_lobbyGeneration);
        Changed?.Invoke();
    }

    // Back to the guest entry. The room is left; a running match stays reachable by ticket.
    public void LeaveLobby()
    {
        if (Room != null) _ = Call<object>(HttpMethod.Post, $"/rooms/{Room.Id}/leave", quiet: true);
        EndSession();
        _token = null;
        if (RememberAddress) SaveSetting("lobby", "token", null);
        Changed?.Invoke();
        ShowLobbyScreen();
    }

    private void LoseLobby(string message)
    {
        EndSession();
        Notice = message;
        Changed?.Invoke();
        if (!InGame && Match == null) ShowLobbyScreen();
    }

    private void EndSession()
    {
        ++_lobbyGeneration;
        _pinger?.Dispose();
        _pinger = null;
        _pings = new();
        Lobby = null;
        Room = null;
        Rooms = new();
        RoomsLoaded = false;
        Rejoinable = null;
        Chat.Clear();
    }

    public async void RefreshRooms()
    {
        if (Lobby == null) return;
        var reply = await Call<List<RoomSummary>>(HttpMethod.Get, "/rooms");
        if (!reply.Ok) return;
        Rooms = reply.Value ?? new();
        RoomsLoaded = true;
        Changed?.Invoke();
    }

    public async void CreateRoom(string name)
    {
        if (Lobby == null || Room != null) return;
        var reply = await Call<RoomState>(HttpMethod.Post, "/rooms", new { name = name.Trim() });
        if (reply.Ok) EnterRoom(reply.Value, null);
    }

    public async void JoinRoom(string id)
    {
        if (Lobby == null || Room != null) return;
        var reply = await Call<RoomState>(HttpMethod.Post, $"/rooms/{id}/join");
        if (reply.Ok) EnterRoom(reply.Value, null);
        else RefreshRooms(); // The room filled up, started or closed since the list was read.
    }

    private void EnterRoom(RoomState state, string runningMatch)
    {
        Room = null;
        Chat.Clear();
        _chatSeq = 0;
        _leftMatchId = runningMatch; // A match running when this client came back is offered, not forced.
        _finishedMatchId = null;
        Notice = null;
        ApplyRoom(state);
        ShowLobbyScreen();
    }

    public async void LeaveRoom()
    {
        if (Room == null) return;
        string id = Room.Id;
        Room = null;
        Rejoinable = null;
        Chat.Clear();
        Changed?.Invoke();
        ShowLobbyScreen();
        await Call<object>(HttpMethod.Post, $"/rooms/{id}/leave", quiet: true);
        RefreshRooms();
    }

    public void ChooseRole(uint team, string role, uint hero) => RoomAction(HttpMethod.Put, "role", new { team, role, hero });
    public void ChooseRegion(string region) => RoomAction(HttpMethod.Put, "region", new { region });
    public void StartRoom() => RoomAction(HttpMethod.Post, "start");
    public void CancelStart() => RoomAction(HttpMethod.Post, "cancel");

    private async void RoomAction(HttpMethod method, string action, object body = null)
    {
        if (Room == null) return;
        string id = Room.Id;
        Notice = null;
        var reply = await Call<RoomState>(method, $"/rooms/{id}/{action}", body);
        if (reply.Ok && reply.Value != null && Room?.Id == id) ApplyRoom(reply.Value);
    }

    public async void SendChat(string text)
    {
        text = text.Trim();
        if (Room == null || text.Length == 0) return;
        if (text.Length > ChatMaxLength) text = text[..ChatMaxLength];
        await Call<object>(HttpMethod.Post, $"/rooms/{Room.Id}/chat", new { text });
    }

    // Polls the room twice a second while this client is not inside a running match, keeps
    // the session alive outside a room, and reports region pings when they change.
    private async Task LobbyLoop(int generation)
    {
        double sinceHeartbeat = 0;
        int failures = 0;
        while (generation == _lobbyGeneration && Lobby != null)
        {
            bool playing = Match != null && Result == null;
            if (!playing) await ReportPings();
            if (generation != _lobbyGeneration) return;
            if (Room != null && !playing)
            {
                string id = Room.Id;
                var reply = await Call<RoomState>(HttpMethod.Get, $"/rooms/{id}?chat={_chatSeq}", quiet: true);
                if (generation != _lobbyGeneration || Room?.Id != id) { }
                else if (reply.Ok) { failures = 0; ApplyRoom(reply.Value); }
                else if (reply.Status == HttpStatusCode.Unauthorized) { LoseLobby(Describe(reply.Error) ?? Lang.T("로비 연결이 끊겼습니다.")); return; }
                else if (reply.Status is HttpStatusCode.NotFound or HttpStatusCode.Forbidden) LostRoom(Describe(reply.Error) ?? Lang.T("방에서 나왔습니다."));
                else if (reply.Status == null && ++failures >= 6) { LoseLobby(Lang.T("로비와 연결이 끊겼습니다. 다시 입장해 주세요.")); return; }
            }
            else if (Room == null && !playing && (sinceHeartbeat += 0.5) >= 10)
            {
                sinceHeartbeat = 0;
                var me = await Call<LobbyMe>(HttpMethod.Get, "/me", quiet: true);
                if (me.Status == HttpStatusCode.Unauthorized && generation == _lobbyGeneration) { LoseLobby(Describe(me.Error) ?? Lang.T("로비 연결이 끊겼습니다.")); return; }
            }
            await Task.Delay(500);
        }
    }

    private void LostRoom(string message)
    {
        Room = null;
        Rejoinable = null;
        Chat.Clear();
        Notice = message;
        Changed?.Invoke();
        if (!InGame && Match == null) ShowLobbyScreen();
        RefreshRooms();
    }

    private async Task ReportPings()
    {
        if (_pinger == null) return;
        var pings = _pinger.Snapshot();
        bool changed = !pings.Keys.ToHashSet().SetEquals(_pings.Keys) || pings.Any(p => _pings[p.Key] != p.Value);
        _pings = pings;
        if (changed) Changed?.Invoke();
        if (pings.Count == 0 || (_reportedPings != null && pings.Keys.ToHashSet().SetEquals(_reportedPings.Keys) &&
            pings.All(p => Math.Abs(_reportedPings[p.Key] - p.Value) < 10))) return;
        _reportedPings = pings;
        await Call<object>(HttpMethod.Put, "/me/pings", pings, quiet: true);
    }

    private void ApplyRoom(RoomState state)
    {
        Room = state;
        foreach (var line in state.Chat.Where(line => line.Seq > _chatSeq).OrderBy(line => line.Seq))
        {
            Chat.Add(line);
            _chatSeq = line.Seq;
        }
        if (Chat.Count > ChatKeep) Chat.RemoveRange(0, Chat.Count - ChatKeep);
        if (state.State == "playing" && state.Match?.IsValid == true)
        {
            if (Match == null && state.Match.MatchId != _finishedMatchId)
            {
                if (state.Match.MatchId == _leftMatchId) Rejoinable = state.Match;
                else BeginMatch(state.Match, false);
            }
        }
        else Rejoinable = null;
        Changed?.Invoke();
    }

    private void BeginMatch(MatchAssignment match, bool rejoining)
    {
        ++_generation;
        ErrorMessage = null;
        Notice = null;
        Result = null;
        Started = false;
        Rejoining = rejoining;
        Rejoinable = null;
        _leftMatchId = null;
        Match = match;
        Changed?.Invoke();
    }

    public void Rejoin()
    {
        if (Rejoinable == null || Match != null) return;
        BeginMatch(Rejoinable, true);
    }

    public void ForgetRejoin()
    {
        Rejoinable = null;
        Changed?.Invoke();
    }

    private void ShowLobbyScreen()
    {
        if (InGame) return;
        string scene = Room != null ? RoomScene : LobbyScene;
        if (GetTree().CurrentScene?.SceneFilePath == scene) return;
        Callable.From(() => GetTree().ChangeSceneToFile(scene)).CallDeferred();
    }

    // ---------------------------------------------------------------- match

    public void Fail(string message)
    {
        ErrorMessage = message;
        if (Match != null && Result == null) _leftMatchId = Match.MatchId; // It may go on with the AI.
        ClearMatch();
        _net?.Disconnect();
        if (InGame) Callable.From(ReturnToLobby).CallDeferred();
    }

    private void ClearMatch()
    {
        ++_generation;
        Match = null;
        Started = false;
        Rejoining = false;
        EndReconnect();
        Changed?.Invoke();
    }

    public void EnterGame()
    {
        InGame = true;
        _gameMenu = new CanvasLayer { Layer = 50 };
        var back = new Button { Text = Lang.T("방으로 나가기"), Position = new Vector2(12, 58) };
        back.Pressed += ReturnToLobby;
        _gameMenu.AddChild(back);
        AddChild(_gameMenu);
    }

    // Leaves the loading screen, the game or its result for the room (or the lobby when the
    // room is gone). Leaving a running match hands the seat to the AI; the room offers it back.
    public void ReturnToLobby()
    {
        bool running = Match != null && Result == null;
        if (running && !Reconnecting) _net.Leave("LEAVE");
        else _net.Disconnect();
        if (running) _leftMatchId = Match.MatchId;
        InGame = false;
        _results?.QueueFree();
        _results = null;
        Result = null;
        _gameMenu?.QueueFree();
        _gameMenu = null;
        ClearMatch();
        if (Room != null) Callable.From(() => GetTree().ChangeSceneToFile(RoomScene)).CallDeferred();
        else Callable.From(() => GetTree().ChangeSceneToFile(LobbyScene)).CallDeferred();
    }

    public void Finish(MatchResultSnapshot result, uint localTeam)
    {
        if (Result != null) return;
        Result = result;
        _finishedMatchId = Match?.MatchId;
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
            Fail(Lang.T("서버에 다시 연결하지 못했습니다. 경기가 계속되고 있으면 방에서 돌아갈 수 있습니다."));
            return;
        }
        _reconnectLabel.Text = Lang.F("서버와 연결이 끊겼습니다. 다시 연결하는 중… {0}초\n그동안 AI가 대신 조종합니다.", (int)_reconnectElapsed);
        _retryIn -= delta;
        if (_attempting || _retryIn > 0) return;
        _retryIn = RetryInterval;
        _attempting = true;
        _ = Reconnect(_generation);
    }

    // The lobby knows whether the match is still running; the game socket alone cannot tell
    // a finished match from a network problem. An unreachable lobby does not stop a retry.
    private async Task Reconnect(int generation)
    {
        string id = Match?.MatchId;
        var me = await Call<LobbyMe>(HttpMethod.Get, "/me", quiet: true);
        if (generation != _generation || !Reconnecting || Match == null) return;
        if (me.Ok && me.Value?.Match?.MatchId != id)
        {
            _finishedMatchId = id;
            Fail(Lang.T("경기가 끝났습니다."));
            return;
        }
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
        var leave = new Button { Text = Lang.T("방으로 나가기"), SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        leave.Pressed += ReturnToLobby;
        box.AddChild(leave);
        AddChild(_reconnectOverlay);
        _reconnectLabel.Text = Lang.T("서버와 연결이 끊겼습니다. 다시 연결하는 중…\n그동안 AI가 대신 조종합니다.");
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
        ++_lobbyGeneration;
        _pinger?.Dispose();
        _net.MessageReceived -= OnMessage;
        _net.ConnectionClosed -= OnClosed;
        _http.Dispose();
    }
}
