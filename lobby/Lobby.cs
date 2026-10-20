using Godot;
using System.Linq;

// Guest entry, then the room list: make a room, refresh the list or join one.
public partial class Lobby : Control
{
    private MatchSession _session;
    private Label _kicker, _title, _description, _status, _brand;
    private Button _primary, _secondary, _leave, _create;
    private LineEdit _name, _address, _roomName;
    private VBoxContainer _entry, _list, _rows;
    private Control _statusArea, _footnote;
    private HBoxContainer _listActions;
    private Font _brandFont;
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
        _brand = GetNode<Label>("Header/Row/Brand");
        _brandFont = _brand.GetThemeFont("font");
        GetNode<Label>("Header/Row/PreviewBadge").Text = "3 vs 3";
        _footnote = GetNode<Label>("%Footnote");
        _footnote.Hide();
        _kicker.Hide();
        _statusArea = GetNode<Control>("Center/Card/Content/StatusArea");
        GetNode<PanelContainer>("Center/Card").CustomMinimumSize = new Vector2(640, 0);
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        var content = GetNode<VBoxContainer>("Center/Card/Content");

        _entry = new VBoxContainer();
        _entry.AddThemeConstantOverride("separation", 6);
        _name = new LineEdit { Text = _session.PlayerName, PlaceholderText = "이름", MaxLength = MatchSession.NameMaxLength, CustomMinimumSize = new Vector2(0, 42) };
        _address = new LineEdit { Text = _session.LobbyUrl, PlaceholderText = "http://서버주소:8080", TooltipText = "호스트에게 받은 로비 서버 주소", CustomMinimumSize = new Vector2(0, 42) };
        _entry.AddChild(LobbyPalette.Caption("이름"));
        _entry.AddChild(_name);
        _entry.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
        _entry.AddChild(LobbyPalette.Caption("서버"));
        _entry.AddChild(_address);
        _name.TextSubmitted += _ => Enter();
        _address.TextSubmitted += _ => Enter();

        _list = new VBoxContainer();
        _list.AddThemeConstantOverride("separation", 12);
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 220), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _rows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _rows.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(_rows);
        _list.AddChild(scroll);
        var createRow = new HBoxContainer();
        createRow.AddThemeConstantOverride("separation", 8);
        _roomName = new LineEdit { PlaceholderText = "방 이름", MaxLength = MatchSession.RoomNameMaxLength, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _roomName.TextSubmitted += _ => _session.CreateRoom(_roomName.Text);
        _create = new Button { Text = "방 만들기", CustomMinimumSize = new Vector2(132, 42) };
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
        _listActions.AddThemeConstantOverride("separation", 8);
        _secondary.Reparent(_listActions);
        _secondary.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _leave = new Button { Text = "나가기", ThemeTypeVariation = "SecondaryButton", CustomMinimumSize = new Vector2(0, 40), SizeFlagsHorizontal = SizeFlags.ExpandFill };
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
        _brand.SelfModulate = entered ? Colors.White : Colors.Transparent; // The entry card carries the name; keep its place.
        _secondary.Disabled = false;
        _statusArea.CustomMinimumSize = entered ? Vector2.Zero : new Vector2(0, 24);
        GetNode<VBoxContainer>("Center/Card/Content").AddThemeConstantOverride("separation", entered ? 14 : 18);
        string problem = _session.Notice ?? _session.ErrorMessage;
        _status.AddThemeColorOverride("font_color", problem != null ? LobbyPalette.Ember : LobbyPalette.Muted);
        var align = entered ? HorizontalAlignment.Left : HorizontalAlignment.Center;
        _title.HorizontalAlignment = _description.HorizontalAlignment = align;
        if (!entered)
        {
            _title.Text = "BURN QUARTER";
            _title.AddThemeFontOverride("font", _brandFont);
            _title.AddThemeFontSizeOverride("font_size", 34);
            _title.AddThemeColorOverride("font_color", LobbyPalette.Brass);
            _description.Text = "게스트 입장";
            _primary.Text = _session.Connecting ? "입장 중…" : "입장";
            _primary.Disabled = _session.Connecting;
            _name.Editable = _address.Editable = !_session.Connecting;
            _status.Text = problem ?? "";
            _status.Visible = true; // Holds its height so a refusal does not shift the card.
            return;
        }
        _title.Text = "방 목록";
        _title.RemoveThemeFontOverride("font");
        _title.AddThemeFontSizeOverride("font_size", 26);
        _title.RemoveThemeColorOverride("font_color");
        var pings = _session.Lobby.Regions.Select(r => $"{r.Name} {(_session.Pings.TryGetValue(r.Id, out int ms) ? $"{ms}ms" : "-")}");
        _description.Text = $"{_session.Lobby.Name} · {string.Join(" · ", pings)}";
        _secondary.Text = "새로고침";
        _roomName.PlaceholderText = $"{_session.Lobby.Name}의 방";
        _status.Text = problem ?? (!_session.RoomsLoaded ? "불러오는 중…" : _session.Rooms.Count == 0 ? "열린 방 없음" : "");
        _status.Visible = _status.Text != "";
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
        var panel = new PanelContainer { ThemeTypeVariation = "RowPanel" };
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 14);
        var name = new Label { Text = room.Name, SizeFlagsHorizontal = SizeFlags.ExpandFill, ClipText = true, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis };
        name.AddThemeFontSizeOverride("font_size", 17);
        var info = LobbyPalette.Caption($"{room.Host} · {room.RegionName}", 14);
        var count = LobbyPalette.Caption($"{room.Players}/{room.Max}", 14, LobbyPalette.Text);
        var state = LobbyPalette.Caption(room.Playing ? "진행 중" : "대기", 14, room.Playing ? LobbyPalette.Ember : LobbyPalette.Muted);
        state.CustomMinimumSize = new Vector2(48, 0);
        var join = new Button { Text = "입장", CustomMinimumSize = new Vector2(76, 34), Disabled = room.Playing || room.Players >= room.Max };
        join.AddThemeFontSizeOverride("font_size", 15);
        join.Pressed += () => _session.JoinRoom(room.Id);
        row.AddChild(name);
        row.AddChild(info);
        row.AddChild(count);
        row.AddChild(state);
        row.AddChild(join);
        panel.AddChild(row);
        return panel;
    }

    public override void _ExitTree() { if (_session != null) _session.Changed -= Refresh; }
}
