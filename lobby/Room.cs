using Godot;
using System.Collections.Generic;
using System.Linq;

// One room: four seats per team, [역할 선택] to take a role, chat, the host's server choice and
// start. Seats nobody took are played by the AI.
public partial class Room : Control
{
    private static readonly uint[] Heroes = { UnitCatalog.HeroTest, UnitCatalog.HeroGolem, UnitCatalog.HeroMarshal };
    private static readonly Color Muted = LobbyPalette.Muted, Faint = LobbyPalette.Faint, Accent = LobbyPalette.BrassLight, Warn = LobbyPalette.Ember;
    private const int SpectatorItem = 100, StandItem = 101;
    private MatchSession _session;
    private Label _title, _status, _waiting, _regionHint;
    private OptionButton _region;
    private MenuButton _choose;
    private Button _start, _leave, _rejoin, _send;
    private PopupMenu _roles;
    private RichTextLabel _chat;
    private LineEdit _chatInput;
    private readonly List<(Label role, Label name, Label ping)>[] _seatRows = { new(), new() };
    private int _renderedChat;
    private bool _transitioning, _autoPicked, _autoStarted, _updatingRegion;

    public override void _Ready()
    {
        _session = GetNode<MatchSession>("/root/MatchSession");
        GetNode<Label>("Header/Row/PreviewBadge").Text = "3 vs 3";
        var card = GetNode<PanelContainer>("Center/Card");
        card.CustomMinimumSize = new Vector2(1060, 0);
        var content = GetNode<VBoxContainer>("Center/Card/Content");
        foreach (Node child in content.GetChildren()) child.QueueFree(); // This screen lays out its own rows.
        content.AddThemeConstantOverride("separation", 14);

        var top = new HBoxContainer();
        top.AddThemeConstantOverride("separation", 10);
        _title = new Label { SizeFlagsHorizontal = SizeFlags.ExpandFill, ClipText = true, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis };
        _title.AddThemeFontSizeOverride("font_size", 26);
        top.AddChild(_title);
        var regionCaption = LobbyPalette.Caption("서버", 14);
        regionCaption.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        top.AddChild(regionCaption);
        _region = new OptionButton { ThemeTypeVariation = "SecondaryButton", CustomMinimumSize = new Vector2(150, 36), TooltipText = "방장만 바꿀 수 있습니다. 기본은 방장의 핑이 낮은 쪽입니다." };
        _region.ItemSelected += index => { if (!_updatingRegion) _session.ChooseRegion(_region.GetItemMetadata((int)index).AsString()); };
        top.AddChild(_region);
        _regionHint = LobbyPalette.Caption("", 13, Faint);
        _regionHint.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        top.AddChild(_regionHint);
        content.AddChild(top);

        var teams = new HBoxContainer();
        teams.AddThemeConstantOverride("separation", 12);
        for (uint team = 1; team <= 2; team++) teams.AddChild(TeamPanel(team));
        content.AddChild(teams);

        _waiting = LobbyPalette.Caption("", 14);
        _waiting.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        content.AddChild(_waiting);

        var bottom = new HBoxContainer();
        bottom.AddThemeConstantOverride("separation", 16);
        var chatBox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        chatBox.AddThemeConstantOverride("separation", 8);
        var chatPanel = new PanelContainer { ThemeTypeVariation = "FieldPanel" };
        _chat = new RichTextLabel { BbcodeEnabled = false, ScrollFollowing = true, CustomMinimumSize = new Vector2(0, 150), SelectionEnabled = true };
        _chat.AddThemeFontSizeOverride("normal_font_size", 14);
        chatPanel.AddChild(_chat);
        chatBox.AddChild(chatPanel);
        var chatRow = new HBoxContainer();
        chatRow.AddThemeConstantOverride("separation", 8);
        _chatInput = new LineEdit { SizeFlagsHorizontal = SizeFlags.ExpandFill, PlaceholderText = "채팅", MaxLength = MatchSession.ChatMaxLength };
        _chatInput.TextSubmitted += _ => SendChat();
        _send = new Button { Text = "보내기", ThemeTypeVariation = "SecondaryButton", CustomMinimumSize = new Vector2(80, 36) };
        _send.Pressed += SendChat;
        chatRow.AddChild(_chatInput);
        chatRow.AddChild(_send);
        chatBox.AddChild(chatRow);
        bottom.AddChild(chatBox);

        var actions = new VBoxContainer { CustomMinimumSize = new Vector2(240, 0) };
        actions.AddThemeConstantOverride("separation", 8);
        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            CustomMinimumSize = new Vector2(240, 40), SizeFlagsVertical = SizeFlags.ExpandFill };
        _status.AddThemeFontSizeOverride("font_size", 15);
        actions.AddChild(_status);
        _choose = new MenuButton { Text = "역할 선택", ThemeTypeVariation = "BrassButton", CustomMinimumSize = new Vector2(0, 42), Flat = false,
            TooltipText = "고르지 않으면 시작할 때 남은 자리에 앉습니다." };
        _roles = _choose.GetPopup();
        _choose.AboutToPopup += FillRoles;
        _roles.IdPressed += OnRoleChosen;
        _start = new Button { Text = "시작", CustomMinimumSize = new Vector2(0, 46) };
        _start.Pressed += () => { if (_session.Room?.State == "countdown") _session.CancelStart(); else _session.StartRoom(); };
        _rejoin = new Button { Text = "경기로 돌아가기", CustomMinimumSize = new Vector2(0, 46) };
        _rejoin.Pressed += () => _session.Rejoin();
        _leave = new Button { Text = "방 나가기", ThemeTypeVariation = "SecondaryButton", CustomMinimumSize = new Vector2(0, 38) };
        _leave.Pressed += () => _session.LeaveRoom();
        actions.AddChild(_choose);
        actions.AddChild(_start);
        actions.AddChild(_rejoin);
        actions.AddChild(_leave);
        bottom.AddChild(actions);
        content.AddChild(bottom);

        _session.Changed += Refresh;
        Refresh();
    }

    private Control TeamPanel(uint team)
    {
        var panel = new PanelContainer { ThemeTypeVariation = "PlatePanel", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var rows = new VBoxContainer();
        rows.AddThemeConstantOverride("separation", 8);
        rows.AddChild(LobbyPalette.Caption($"{team}팀", 15, LobbyPalette.Brass));
        for (int i = 0; i < 4; i++)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 10);
            var role = LobbyPalette.Caption("", 15);
            role.CustomMinimumSize = new Vector2(96, 0);
            var name = new Label { SizeFlagsHorizontal = SizeFlags.ExpandFill, ClipText = true, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis };
            var ping = LobbyPalette.Caption("", 14);
            ping.CustomMinimumSize = new Vector2(64, 0);
            ping.HorizontalAlignment = HorizontalAlignment.Right;
            row.AddChild(role);
            row.AddChild(name);
            row.AddChild(ping);
            rows.AddChild(row);
            _seatRows[team - 1].Add((role, name, ping));
        }
        panel.AddChild(rows);
        return panel;
    }

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
        var room = _session.Room;
        if (room == null) return; // The session opens the lobby.
        var me = room.Me;
        bool waiting = room.State == "waiting";
        _title.Text = room.Name;
        RefreshRegions(room);
        foreach (var seat in room.Seats)
        {
            var (role, name, ping) = _seatRows[seat.Team - 1][seat.Index % 4];
            var member = seat.PlayerId != null ? room.Member(seat.PlayerId) : null;
            role.Text = seat.Role == "COMMANDER" ? "지휘관" : member != null ? UnitCatalog.Name(seat.Hero) : "영웅";
            name.Text = member == null ? "AI" : member.Name + (member.Id == room.HostId ? " (방장)" : "");
            name.AddThemeColorOverride("font_color", member == null ? Faint : member.Id == room.You ? Accent : LobbyPalette.Text);
            ping.Text = member == null ? "" : member.Ping < 0 ? "-" : $"{member.Ping}ms";
            ping.AddThemeColorOverride("font_color", member != null && member.Ping >= 150 ? Warn : Muted);
        }
        var unseated = room.Members.Where(m => m.Seat < 0 && !m.Spectator).Select(m => m.Name).ToArray();
        var spectators = room.Members.Where(m => m.Spectator).Select(m => m.Name).ToArray();
        _waiting.Text = $"인원 {room.Members.Count}/8" + (unseated.Length > 0 ? $" · 선택 전: {string.Join(", ", unseated)}" : "") +
            (spectators.Length > 0 ? $" · 관전: {string.Join(", ", spectators)}" : "");
        _choose.Disabled = !waiting;
        _start.Visible = room.IsHost;
        _start.Text = room.State == "countdown" ? "시작 취소" : "시작";
        _start.Disabled = room.State is not ("waiting" or "countdown");
        _rejoin.Visible = _session.Rejoinable != null;
        _status.Text = _session.Notice ?? _session.ErrorMessage ?? room.State switch
        {
            "countdown" => $"{room.Countdown}초 후 시작",
            "launching" => "서버 준비 중",
            "playing" => "경기 진행 중",
            _ when room.IsHost => "대기 중",
            _ => "방장 대기 중",
        };
        _status.AddThemeColorOverride("font_color", _session.Notice != null || _session.ErrorMessage != null ? Warn : room.State == "countdown" ? Accent : Muted);
        RenderChat();
        if (_session.AutoSpectate && waiting && me != null)
        {
            if (!me.Spectator && !_autoPicked) { _autoPicked = true; _session.ChooseRole(0, "SPECTATOR", 0); }
            else if (me.Spectator && !_autoStarted && room.IsHost) { _autoStarted = true; _session.StartRoom(); }
        }
    }

    private void RefreshRegions(RoomState room)
    {
        _updatingRegion = true;
        if (_region.ItemCount != room.Regions.Count)
        {
            _region.Clear();
            foreach (var region in room.Regions)
            {
                _region.AddItem(region.Name);
                _region.SetItemMetadata(_region.ItemCount - 1, region.Id);
            }
        }
        for (int i = 0; i < room.Regions.Count; i++)
        {
            var region = room.Regions[i];
            _region.SetItemText(i, region.Name + (_session.Pings.TryGetValue(region.Id, out int ms) ? $"  {ms}ms" : ""));
            if (region.Id == room.Region) _region.Select(i);
        }
        _region.Disabled = !room.IsHost || room.State != "waiting";
        _regionHint.Text = room.RegionAuto ? "자동" : "방장 선택";
        _updatingRegion = false;
    }

    // [역할 선택]: each team's commander and heroes, then spectating. A role another guest holds
    // is greyed out; the server refuses it anyway. Filled each time the menu opens.
    private void FillRoles()
    {
        _roles.Clear();
        var room = _session.Room;
        if (room == null) return;
        for (uint team = 1; team <= 2; team++)
        {
            _roles.AddSeparator($"{team}팀");
            var seats = room.Seats.Where(s => s.Team == team).ToArray();
            var commander = seats.First(s => s.Role == "COMMANDER");
            AddRole("지휘관", Id(team, 0), commander.PlayerId != null && commander.PlayerId != room.You);
            var heroSeats = seats.Where(s => s.Role == "HERO").ToArray();
            bool mineHere = heroSeats.Any(s => s.PlayerId == room.You);
            bool free = mineHere || heroSeats.Any(s => s.PlayerId == null);
            foreach (uint hero in Heroes)
            {
                bool taken = heroSeats.Any(s => s.PlayerId != null && s.PlayerId != room.You && s.Hero == hero);
                AddRole(UnitCatalog.Name(hero), Id(team, hero), taken || !free);
            }
        }
        _roles.AddSeparator();
        AddRole("관전", SpectatorItem, false);
        AddRole("선택 취소", StandItem, false);
    }

    private void AddRole(string text, int id, bool disabled)
    {
        _roles.AddItem(text, id);
        _roles.SetItemDisabled(_roles.ItemCount - 1, disabled);
    }

    private static int Id(uint team, uint hero) => (int)(team * 1000 + hero);

    private void OnRoleChosen(long id)
    {
        if (id == SpectatorItem) _session.ChooseRole(0, "SPECTATOR", 0);
        else if (id == StandItem) _session.ChooseRole(0, "", 0);
        else
        {
            uint team = (uint)(id / 1000), hero = (uint)(id % 1000);
            _session.ChooseRole(team, hero == 0 ? "COMMANDER" : "HERO", hero);
        }
    }

    private void RenderChat()
    {
        var lines = _session.Chat;
        if (lines.Count == 0)
        {
            if (_renderedChat > 0) { _chat.Clear(); _renderedChat = 0; }
            return;
        }
        foreach (var line in lines.Where(l => l.Seq > _renderedChat))
        {
            if (_renderedChat > 0) _chat.Newline();
            if (line.System)
            {
                _chat.PushColor(Faint);
                _chat.AddText("* " + line.Text);
                _chat.Pop();
            }
            else
            {
                _chat.PushColor(line.PlayerId == _session.Room?.You ? Accent : Muted);
                _chat.AddText(line.Name + ": ");
                _chat.Pop();
                _chat.AddText(line.Text); // Plain text: a guest's brackets are not markup.
            }
            _renderedChat = line.Seq;
        }
    }

    private void SendChat()
    {
        string text = _chatInput.Text;
        _chatInput.Clear();
        _session.SendChat(text);
    }

    public override void _ExitTree() { if (_session != null) _session.Changed -= Refresh; }
}
