using Godot;
using System.Linq;

// Guest entry, then the room list: make a room, refresh the list or join one.
public partial class Lobby : Control
{
    private MatchSession _session;
    private Label _kicker, _title, _description, _status, _brand, _nameCaption, _addressCaption;
    private Button _primary, _secondary, _leave, _create;
    private LineEdit _name, _address, _roomName;
    private VBoxContainer _entry, _list, _rows;
    private Control _statusArea, _footnote;
    private HBoxContainer _listActions;
    private Font _brandFont;
    private Settings _settings;
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
        _name = new LineEdit { Text = _session.PlayerName, MaxLength = MatchSession.NameMaxLength, CustomMinimumSize = new Vector2(0, 42) };
        _nameCaption = LobbyPalette.Caption("");
        _entry.AddChild(_nameCaption);
        _entry.AddChild(_name);
        _name.TextSubmitted += _ => Enter();
        // Players go to the official lobby; only editor runs can point at another server.
        if (MatchSession.AddressEditable)
        {
            _address = new LineEdit { Text = _session.LobbyUrl, CustomMinimumSize = new Vector2(0, 42) };
            _addressCaption = LobbyPalette.Caption("");
            _entry.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
            _entry.AddChild(_addressCaption);
            _entry.AddChild(_address);
            _address.TextSubmitted += _ => Enter();
        }

        _list = new VBoxContainer();
        _list.AddThemeConstantOverride("separation", 12);
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 220), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _rows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _rows.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(_rows);
        _list.AddChild(scroll);
        var createRow = new HBoxContainer();
        createRow.AddThemeConstantOverride("separation", 8);
        _roomName = new LineEdit { MaxLength = MatchSession.RoomNameMaxLength, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _roomName.TextSubmitted += _ => CreateRoom();
        _create = new Button { CustomMinimumSize = new Vector2(132, 42) };
        _create.Pressed += CreateRoom;
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
        _leave = new Button { ThemeTypeVariation = "SecondaryButton", CustomMinimumSize = new Vector2(0, 40), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _leave.Pressed += () => _session.LeaveLobby();
        _listActions.AddChild(_leave);
        actions.AddChild(_listActions);

        _primary.Pressed += Enter;
        _secondary.Pressed += () => _session.RefreshRooms();
        SettingsPanel.Attach(this);
        _settings = Settings.Instance;
        if (_settings != null) _settings.LanguageChanged += ApplyText;
        _session.Changed += Refresh;
        ApplyText();
        if (_session.AutoSpectate && _session.Lobby == null) Enter();
        else if (_session.Lobby != null) _session.RefreshRooms();
    }

    private void Enter() => _session.EnterLobby(_address?.Text ?? _session.LobbyUrl, _name.Text);

    // A blank name takes the placeholder, so the room is called what the field showed.
    private void CreateRoom() => _session.CreateRoom(_roomName.Text.Trim() == "" ? _roomName.PlaceholderText : _roomName.Text);

    // The text this screen sets once; Refresh sets the rest.
    private void ApplyText()
    {
        _nameCaption.Text = _name.PlaceholderText = Lang.T("이름");
        if (_address != null)
        {
            _addressCaption.Text = Lang.T("서버 (개발용)");
            _address.PlaceholderText = Lang.T("http://서버주소:8080");
            _address.TooltipText = Lang.T("개발용: 접속할 로비 서버 주소");
        }
        _create.Text = Lang.T("방 만들기");
        _leave.Text = Lang.T("나가기");
        Refresh();
    }

    private void Refresh()
    {
        if (_transitioning) return;
        if (_session.Match != null)
        {
            _transitioning = true;
            if (GetTree().ChangeSceneToFile("res://lobby/Loading.tscn") != Error.Ok)
            { _transitioning = false; _session.Fail(Lang.T("로딩 화면을 열지 못했습니다.")); }
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
            _description.Text = Lang.T("게스트 입장");
            _primary.Text = _session.Connecting ? Lang.T("입장 중…") : Lang.T("입장", "lobby");
            _primary.Disabled = _session.Connecting;
            _name.Editable = !_session.Connecting;
            if (_address != null) _address.Editable = !_session.Connecting;
            _status.Text = problem ?? "";
            _status.Visible = true; // Holds its height so a refusal does not shift the card.
            return;
        }
        _title.Text = Lang.T("방 목록");
        _title.RemoveThemeFontOverride("font");
        _title.AddThemeFontSizeOverride("font_size", 26);
        _title.RemoveThemeColorOverride("font_color");
        var pings = _session.Lobby.Regions.Select(r => $"{Lang.T(r.Name)} {(_session.Pings.TryGetValue(r.Id, out int ms) ? $"{ms}ms" : "-")}");
        _description.Text = $"{_session.Lobby.Name} · {string.Join(" · ", pings)}";
        _secondary.Text = Lang.T("새로고침");
        _roomName.PlaceholderText = Lang.F("{0}의 방", _session.Lobby.Name);
        _status.Text = problem ?? (!_session.RoomsLoaded ? Lang.T("불러오는 중…") : _session.Rooms.Count == 0 ? Lang.T("열린 방 없음") : "");
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
        var info = LobbyPalette.Caption($"{room.Host} · {Lang.T(room.RegionName)}", 14);
        var count = LobbyPalette.Caption($"{room.Players}/{room.Max}", 14, LobbyPalette.Text);
        var state = LobbyPalette.Caption(room.Playing ? Lang.T("진행 중") : Lang.T("대기"), 14, room.Playing ? LobbyPalette.Ember : LobbyPalette.Muted);
        state.CustomMinimumSize = new Vector2(48, 0);
        var join = new Button { Text = Lang.T("입장", "room"), CustomMinimumSize = new Vector2(76, 34), Disabled = room.Playing || room.Players >= room.Max };
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

    public override void _ExitTree()
    {
        if (_session != null) _session.Changed -= Refresh;
        if (_settings != null) _settings.LanguageChanged -= ApplyText;
    }
}
