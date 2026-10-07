using Godot;

public partial class Lobby : Control
{
    public bool IsQueued => _session?.IsQueued == true;
    public double QueueElapsedSeconds { get; private set; }
    private bool _transitioning;
    private Button _start, _cancel;
    private Label _status, _elapsed;
    private LineEdit _address;
    private OptionButton _heroChoice, _modeChoice;
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
        _heroChoice = new OptionButton { TooltipText = "영웅 역할로 배정되면 선택한 영웅으로 입장합니다." };
        _heroChoice.AddItem("영웅 선택 · 돌연변이 늑대", (int)UnitCatalog.HeroTest);
        _heroChoice.AddItem("영웅 선택 · 룬 골렘", (int)UnitCatalog.HeroGolem);
        _heroChoice.Select(_session.SelectedHero == UnitCatalog.HeroGolem ? 1 : 0);
        _heroChoice.ItemSelected += index => _session.SelectedHero = (uint)_heroChoice.GetItemId((int)index);
        content.AddChild(_heroChoice);
        content.MoveChild(_heroChoice, 4);
        _modeChoice = new OptionButton();
        _modeChoice.AddItem("일반 매칭 · 2~6명 + 빈자리 AI");
        _modeChoice.AddItem("혼자 연습 · 영웅 + AI 5명");
        _modeChoice.AddItem("혼자 연습 · 지휘관 + AI 5명");
        _modeChoice.Select(_session.PracticeRole == "HERO" ? 1 : _session.PracticeRole == "COMMANDER" ? 2 : 0);
        _modeChoice.ItemSelected += index => { _session.PracticeRole = index == 1 ? "HERO" : index == 2 ? "COMMANDER" : ""; Refresh(); };
        content.AddChild(_modeChoice);
        content.MoveChild(_modeChoice, 3);
        content.AddThemeConstantOverride("separation", 12);
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        GetNode<Label>("Header/Row/PreviewBadge").Text = "지인 테스트 · 3 vs 3";
        GetNode<Label>("%Description").Text = "팀마다 지휘관 1명과 영웅 2명이 힘을 합쳐 적 본진을 파괴하세요.";
        GetNode<Label>("%Footnote").Text = "일반 매칭은 같은 팀 지휘관 → 영웅 → 영웅 순서로 배정됩니다.\n빈자리는 AI가 맡습니다. 혼자 연습도 서버 연결이 필요합니다.";
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
        _start.Text = IsQueued ? "매칭 중…" : _session.PracticeRole == "" ? "매칭 시작" : "AI 연습 시작";
        _start.Disabled = IsQueued;
        _cancel.Disabled = !IsQueued;
        _address.Editable = !IsQueued;
        _heroChoice.Disabled = IsQueued;
        _modeChoice.Disabled = IsQueued;
        _elapsed.Visible = IsQueued;
        var status = _session.Status;
        _status.Text = !IsQueued ? _session.ErrorMessage ?? "서버 주소를 확인하고 매칭을 시작하세요" :
            status == null ? "로비 서버에 연결 중…" :
            status.State == "starting" ? "게임 서버를 준비하고 있어요" :
            status.Busy ? $"현재 테스트 경기가 진행 중입니다 · 대기 순서 {status.Position}" :
            status.Count < 2 ? "1 / 6명 · 함께할 팀원 1명을 기다립니다" :
            $"{status.Count} / 6명 · {status.Seconds}초 뒤 출발";
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
