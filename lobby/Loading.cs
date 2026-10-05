using Godot;

// Loads the real game under an overlay, keeping its input paused until every
// admitted client has synchronized the map. Main owns its existing connection.
public partial class Loading : Control
{
    private const string GamePath = "res://game/Main.tscn";
    private MatchSession _session;
    private Main _game;
    private ProgressBar _progress;
    private Label _status, _percent;
    private bool _finished, _failed, _instantiating, _loadedSent;
    private double _elapsed;

    public override void _Ready()
    {
        _session = GetNode<MatchSession>("/root/MatchSession");
        _progress = GetNode<ProgressBar>("%Progress");
        _status = GetNode<Label>("%Status");
        _percent = GetNode<Label>("%ProgressValue");
        GetNode<Button>("%PrimaryButton").Pressed += ReturnToLobby;
        GetNode<Label>("%Footnote").Text = "모든 참가자의 맵 준비가 끝나면 함께 시작합니다.";
        GetNode<Label>("Header/Row/PreviewBadge").Text = "지인 테스트 · 전장 준비";
        // The overlay sits above Main's in-game CanvasLayers.
        var overlay = new CanvasLayer { Layer = 100 };
        AddChild(overlay);
        foreach (string name in new[] { "Background", "Header", "Center" }) GetNode(name).Reparent(overlay);
        if (_session.Match == null) { Fail("매칭 정보가 없습니다. 로비에서 다시 시작해 주세요."); return; }
        var match = _session.Match;
        GetNode<Label>("%Description").Text = $"{match.Team}팀 · {(match.Role == "HERO" ? "영웅" : "지휘관")} · 플레이어 {match.Players - match.Bots}명 + AI {match.Bots}명";
        if (ResourceLoader.LoadThreadedRequest(GamePath) != Error.Ok) Fail("전장 리소스를 불러오지 못했습니다.");
    }

    public override void _Process(double delta)
    {
        if (_finished || _failed) return;
        _elapsed += delta;
        if (_session.ErrorMessage != null) { Fail(_session.ErrorMessage); return; }
        if (_elapsed > 100) { Fail("전장 준비 시간이 초과되었습니다. 참가자 모두 다시 매칭해 주세요."); return; }
        if (_game == null)
        {
            if (_instantiating) return;
            var progress = new Godot.Collections.Array();
            var state = ResourceLoader.LoadThreadedGetStatus(GamePath, progress);
            SetProgress(progress.Count > 0 ? progress[0].AsDouble() * 65 : 0, "전장 리소스를 불러오고 있어요");
            if (state is ResourceLoader.ThreadLoadStatus.Failed or ResourceLoader.ThreadLoadStatus.InvalidResource) { Fail("전장을 불러오지 못했습니다."); return; }
            if (state != ResourceLoader.ThreadLoadStatus.Loaded) return;
            _instantiating = true;
            Callable.From(CreateGame).CallDeferred();
            return;
        }
        if (_game.Map.SyncError != null) { Fail(_game.Map.SyncError); return; }
        SetProgress(_game.Map.IsSynchronized ? 90 : 75,
            _game.Map.IsSynchronized ? "준비 완료 · 다른 참가자를 기다리고 있어요" : "게임 서버 연결 및 맵 확인 중…");
        if (!_loadedSent && _game.Map.IsSynchronized && _game.LocalRole != PlayerRole.None)
        {
            _loadedSent = true;
            GetNode<NetClient>("/root/Net").Send("LOADED");
        }
        if (!_session.Started || !_game.Map.IsSynchronized || _game.LocalRole == PlayerRole.None) return;
        _finished = true;
        _game.ProcessMode = ProcessModeEnum.Inherit;
        GetTree().CurrentScene = _game;
        _session.EnterGame();
        QueueFree();
    }

    private void CreateGame()
    {
        if (_finished || _failed) return;
        _game = ((PackedScene)ResourceLoader.LoadThreadedGet(GamePath)).Instantiate<Main>();
        _game.ProcessMode = ProcessModeEnum.Disabled;
        GetTree().Root.AddChild(_game);
    }

    private void SetProgress(double value, string status)
    { _progress.Value = value; _percent.Text = $"{(int)value}%"; _status.Text = status; }

    private void Fail(string reason)
    {
        _failed = true;
        _session.Fail(reason);
        SetProgress(_progress.Value, reason);
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        if (GodotObject.IsInstanceValid(_game)) { _game.QueueFree(); _game = null; }
    }

    public void ReturnToLobby()
    {
        if (_finished) return;
        _finished = true;
        if (GodotObject.IsInstanceValid(_game)) { _game.QueueFree(); _game = null; }
        _session.ReturnToLobby();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!@event.IsActionPressed("ui_cancel")) return;
        GetViewport().SetInputAsHandled();
        ReturnToLobby();
    }

    public override void _ExitTree()
    {
        if (!_session.InGame && GodotObject.IsInstanceValid(_game)) _game.QueueFree();
    }
}
