using Godot;

public partial class Lobby : Control
{
    public bool IsQueued => _session?.IsQueued == true;
    public double QueueElapsedSeconds { get; private set; }
    private bool _transitioning;
    private Button _start, _cancel;
    private Label _status, _elapsed;
    private LineEdit _address;
    private MatchSession _session;

    public override void _Ready()
    {
        _session = GetNode<MatchSession>("/root/MatchSession");
        _start = GetNode<Button>("%PrimaryButton");
        _cancel = GetNode<Button>("%SecondaryButton");
        _status = GetNode<Label>("%Status");
        _elapsed = GetNode<Label>("%QueueTime");
        _address = new LineEdit { Text = _session.LobbyUrl, PlaceholderText = "http://서버주소:8080", TooltipText = "호스트에게 받은 로비 서버 주소" };
        var content = GetNode<VBoxContainer>("Center/Card/Content");
        content.AddChild(_address);
        content.MoveChild(_address, 3);
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        GetNode<Label>("Header/Row/PreviewBadge").Text = "지인 테스트 · 2 vs 2";
        GetNode<Label>("%Description").Text = "한 팀의 지휘관과 영웅부터 함께 배정됩니다.";
        GetNode<Label>("%Footnote").Text = "참가 순서: 1팀 지휘관 → 1팀 영웅 → 2팀 지휘관 → 2팀 영웅\n2명부터 시작할 수 있으며, 빈 자리에 봇은 배치되지 않습니다.";
        _start.Pressed += StartQueue;
        _cancel.Pressed += CancelQueue;
        _session.Changed += Refresh;
        Refresh();
    }

    public void StartQueue() { QueueElapsedSeconds = 0; _session.StartQueue(_address.Text); }
    public void CancelQueue() { _session.CancelQueue(); QueueElapsedSeconds = 0; Refresh(); }

    private void Refresh()
    {
        if (_transitioning) return;
        if (_session.Match != null)
        {
            _transitioning = true;
            if (GetTree().ChangeSceneToFile("res://lobby/Loading.tscn") != Error.Ok)
            { _transitioning = false; _session.Fail("로딩 화면을 열지 못했습니다."); }
            return;
        }
        _start.Text = IsQueued ? "매칭 중…" : "매칭 시작";
        _start.Disabled = IsQueued;
        _cancel.Disabled = !IsQueued;
        _address.Editable = !IsQueued;
        _elapsed.Visible = IsQueued;
        var status = _session.Status;
        _status.Text = !IsQueued ? _session.ErrorMessage ?? "서버 주소를 확인하고 매칭을 시작하세요" :
            status == null ? "로비 서버에 연결 중…" :
            status.State == "starting" ? "게임 서버를 준비하고 있어요" :
            status.Busy ? $"현재 테스트 경기가 진행 중입니다 · 대기 순서 {status.Position}" :
            status.Count < 2 ? "1 / 4명 · 함께할 팀원 1명을 기다립니다" :
            $"{status.Count} / 4명 · {status.Seconds}초 뒤 출발";
    }

    public override void _Process(double delta)
    {
        if (!IsQueued) return;
        QueueElapsedSeconds += delta;
        int seconds = (int)QueueElapsedSeconds;
        _elapsed.Text = $"대기 시간  {seconds / 60:00}:{seconds % 60:00}";
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!IsQueued || !@event.IsActionPressed("ui_cancel")) return;
        CancelQueue();
        GetViewport().SetInputAsHandled();
    }

    public override void _ExitTree() { if (_session != null) _session.Changed -= Refresh; }
}
