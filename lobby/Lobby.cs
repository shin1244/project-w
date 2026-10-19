using Godot;
using System.Linq;

// Guest entry, then the room list: make a room, refresh the list or join one.
public partial class Lobby : Control
{
    private MatchSession _session;
    private Label _kicker, _title, _description, _status;
    private Button _primary, _secondary, _leave, _create;
    private LineEdit _name, _address, _roomName;
    private VBoxContainer _entry, _list, _rows;
    private Control _statusArea, _footnote;
    private HBoxContainer _listActions;
    private bool _transitioning, _autoCreated;

    public override void _Ready()
    {
        _session = GetNode<MatchSession>("/root/MatchSession");
        _kicker = GetNode<Label>("%Kicker");
        _title = GetNode<Label>("%Title");
        _description = GetNode<Label>("%Description");
        _status = GetNode<Label>("%Status");
        _primary = GetNode<Button>("%PrimaryButton");
        _secondary = GetNode<Button>("%SecondaryButton");
        GetNode<Label>("%QueueTime").Hide();
        GetNode<Label>("Header/Row/PreviewBadge").Text = "영웅 3 vs 3 · 지휘관 각 1명";
        _footnote = GetNode<Label>("%Footnote");
        ((Label)_footnote).Text = "빈자리는 AI가 맡습니다. 방장이 시작하면 5초 뒤 전장으로 이동합니다.";
        _statusArea = GetNode<Control>("Center/Card/Content/StatusArea");
        GetNode<PanelContainer>("Center/Card").CustomMinimumSize = new Vector2(620, 0);
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        var content = GetNode<VBoxContainer>("Center/Card/Content");

        _entry = new VBoxContainer();
        _entry.AddThemeConstantOverride("separation", 10);
        _name = new LineEdit { Text = _session.PlayerName, PlaceholderText = "이름", MaxLength = MatchSession.NameMaxLength, TooltipText = "방과 채팅에 보이는 이름" };
        _address = new LineEdit { Text = _session.LobbyUrl, PlaceholderText = "http://서버주소:8080", TooltipText = "호스트에게 받은 로비 서버 주소" };
        _entry.AddChild(_name);
        _entry.AddChild(_address);
        _name.TextSubmitted += _ => Enter();
        _address.TextSubmitted += _ => Enter();

        _list = new VBoxContainer();
        _list.AddThemeConstantOverride("separation", 10);
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 190), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _rows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _rows.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_rows);
        _list.AddChild(scroll);
        var createRow = new HBoxContainer();
        createRow.AddThemeConstantOverride("separation", 10);
        _roomName = new LineEdit { PlaceholderText = "방 이름 (비우면 \"이름의 방\")", MaxLength = MatchSession.RoomNameMaxLength, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _roomName.TextSubmitted += _ => _session.CreateRoom(_roomName.Text);
        _create = new Button { Text = "방 만들기", CustomMinimumSize = new Vector2(140, 42) };
        _create.Pressed += () => _session.CreateRoom(_roomName.Text);
        createRow.AddChild(_roomName);
        createRow.AddChild(_create);
        _list.AddChild(createRow);

        content.AddChild(_entry);
        content.MoveChild(_entry, 3);
        content.AddChild(_list);
        content.MoveChild(_list, 4);
        // In the list, refresh and back sit side by side under the list to fit the window.
        var actions = GetNode<VBoxContainer>("Center/Card/Content/Actions");
        _listActions = new HBoxContainer();
        _listActions.AddThemeConstantOverride("separation", 10);
        _secondary.Reparent(_listActions);
        _secondary.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _leave = new Button { Text = "입장 화면으로", ThemeTypeVariation = "SecondaryButton", CustomMinimumSize = new Vector2(0, 42), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _leave.Pressed += () => _session.LeaveLobby();
        _listActions.AddChild(_leave);
        actions.AddChild(_listActions);

        _primary.Pressed += Enter;
        _secondary.Pressed += () => _session.RefreshRooms();
        _session.Changed += Refresh;
        Refresh();
        if (_session.AutoSpectate && _session.Lobby == null) Enter();
        else if (_session.Lobby != null) _session.RefreshRooms();
    }

    private void Enter() => _session.EnterLobby(_address.Text, _name.Text);

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
        bool entered = _session.Lobby != null;
        _entry.Visible = !entered;
        _list.Visible = entered;
        _listActions.Visible = entered;
        _primary.Visible = !entered;
        _footnote.Visible = !entered;
        _secondary.Disabled = false;
        _statusArea.CustomMinimumSize = entered ? Vector2.Zero : new Vector2(0, 80);
        GetNode<VBoxContainer>("Center/Card/Content").AddThemeConstantOverride("separation", entered ? 12 : 16);
        if (!entered)
        {
            _kicker.Text = "게스트 입장";
            _title.Text = "전장으로";
            _description.Text = "이름과 서버 주소를 정하고 입장하세요.";
            _primary.Text = _session.Connecting ? "입장하는 중…" : "게스트로 입장";
            _primary.Disabled = _session.Connecting;
            _name.Editable = _address.Editable = !_session.Connecting;
            _status.Text = _session.Notice ?? _session.ErrorMessage ?? "로그인 없이 게스트로 플레이합니다.";
            return;
        }
        _kicker.Text = "로비";
        _title.Text = "방 목록";
        var pings = _session.Lobby.Regions.Select(r => $"{r.Name} {(_session.Pings.TryGetValue(r.Id, out int ms) ? $"{ms}ms" : "측정 중")}");
        _description.Text = $"{_session.Lobby.Name}님 · 핑 {string.Join(" · ", pings)}";
        _secondary.Text = "새로고침";
        _roomName.PlaceholderText = $"방 이름 (비우면 \"{_session.Lobby.Name}의 방\")";
        _status.Text = _session.Notice ?? _session.ErrorMessage ?? (!_session.RoomsLoaded ? "방 목록을 불러오는 중…" :
            _session.Rooms.Count == 0 ? "열린 방이 없습니다. 방을 만들어 보세요." : $"방 {_session.Rooms.Count}개");
        foreach (Node row in _rows.GetChildren()) row.QueueFree();
        foreach (var room in _session.Rooms) _rows.AddChild(RoomRow(room));
        if (_session.AutoSpectate && !_autoCreated && _session.Room == null)
        {
            _autoCreated = true;
            _session.CreateRoom("AI 관전");
        }
    }

    private Control RoomRow(RoomSummary room)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        var name = new Label { Text = room.Name, SizeFlagsHorizontal = SizeFlags.ExpandFill, ClipText = true, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis };
        var info = new Label { Text = $"{room.Host} · {room.Players}/{room.Max} · {room.RegionName}" };
        info.AddThemeColorOverride("font_color", new Color(0.63f, 0.73f, 0.73f));
        info.AddThemeFontSizeOverride("font_size", 14);
        var state = new Label { Text = room.Playing ? "게임 중" : "대기 중", CustomMinimumSize = new Vector2(56, 0) };
        state.AddThemeColorOverride("font_color", room.Playing ? new Color(0.89f, 0.66f, 0.59f) : new Color(0.72f, 0.86f, 0.64f));
        state.AddThemeFontSizeOverride("font_size", 14);
        var join = new Button { Text = "입장", CustomMinimumSize = new Vector2(72, 34), Disabled = room.Playing || room.Players >= room.Max };
        join.AddThemeFontSizeOverride("font_size", 15);
        join.Pressed += () => _session.JoinRoom(room.Id);
        row.AddChild(name);
        row.AddChild(info);
        row.AddChild(state);
        row.AddChild(join);
        return row;
    }

    public override void _ExitTree() { if (_session != null) _session.Changed -= Refresh; }
}
