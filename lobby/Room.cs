using Godot;
using System.Collections.Generic;
using System.Linq;

// One room: four seats per team. Each row picks its role on the left (대표 = commander, one per
// team; 용병 = hero) and, on hero rows, which hero on the right. Chat, the host's server choice and
// start sit below. Seats nobody took are played by the AI.
public partial class Room : Control
{
    private static readonly uint[] Heroes = { UnitCatalog.HeroTest, UnitCatalog.HeroGolem, UnitCatalog.HeroMarshal };
    private static readonly Color Muted = LobbyPalette.Muted, Faint = LobbyPalette.Faint, Accent = LobbyPalette.BrassLight, Warn = LobbyPalette.Ember;
    private const int CommanderItem = 0, HeroItem = 1, StandItem = 2;
    private MatchSession _session;
    private Label _title, _status, _waiting, _regionHint, _regionCaption;
    private readonly List<Label> _teamCaptions = new();
    private Settings _settings;
    private OptionButton _region;
    private Button _spectate, _start, _leave, _rejoin, _send;
    private RichTextLabel _chat;
    private LineEdit _chatInput;
    private readonly List<SeatRow>[] _seatRows = { new(), new() };
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
        _regionCaption = LobbyPalette.Caption("", 14);
        _regionCaption.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        top.AddChild(_regionCaption);
        _region = new OptionButton { ThemeTypeVariation = "SecondaryButton", CustomMinimumSize = new Vector2(150, 36) };
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
        _chat = new RichTextLabel { BbcodeEnabled = false, ScrollFollowing = true, CustomMinimumSize = new Vector2(0, 120), SelectionEnabled = true };
        _chat.AddThemeFontSizeOverride("normal_font_size", 14);
        chatPanel.AddChild(_chat);
        chatBox.AddChild(chatPanel);
        var chatRow = new HBoxContainer();
        chatRow.AddThemeConstantOverride("separation", 8);
        _chatInput = new LineEdit { SizeFlagsHorizontal = SizeFlags.ExpandFill, MaxLength = MatchSession.ChatMaxLength };
        _chatInput.TextSubmitted += _ => SendChat();
        _send = new Button { ThemeTypeVariation = "SecondaryButton", CustomMinimumSize = new Vector2(80, 36) };
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
        _spectate = new Button { ThemeTypeVariation = "SecondaryButton", CustomMinimumSize = new Vector2(0, 38) };
        _spectate.Pressed += () => _session.ChooseRole(0, _session.Room?.Me?.Spectator == true ? "" : "SPECTATOR", 0);
        _start = new Button { CustomMinimumSize = new Vector2(0, 46) };
        _start.Pressed += () => { if (_session.Room?.State == "countdown") _session.CancelStart(); else _session.StartRoom(); };
        _rejoin = new Button { CustomMinimumSize = new Vector2(0, 46) };
        _rejoin.Pressed += () => _session.Rejoin();
        _leave = new Button { ThemeTypeVariation = "SecondaryButton", CustomMinimumSize = new Vector2(0, 38) };
        _leave.Pressed += () => _session.LeaveRoom();
        actions.AddChild(_start);
        actions.AddChild(_rejoin);
        actions.AddChild(_spectate);
        actions.AddChild(_leave);
        bottom.AddChild(actions);
        content.AddChild(bottom);

        SettingsPanel.Attach(this);
        _settings = Settings.Instance;
        if (_settings != null) _settings.LanguageChanged += ApplyText;
        _session.Changed += Refresh;
        ApplyText();
    }

    // The text this screen sets once; Refresh sets the rest.
    private void ApplyText()
    {
        _regionCaption.Text = Lang.T("서버");
        _region.TooltipText = Lang.T("방장만 바꿀 수 있습니다. 기본은 방장의 핑이 낮은 쪽입니다.");
        for (int i = 0; i < _teamCaptions.Count; i++) _teamCaptions[i].Text = Lang.F("{0}팀", i + 1);
        foreach (var seat in _seatRows.SelectMany(rows => rows))
        {
            seat.RoleBox.TooltipText = Lang.T("대표는 팀에 한 명입니다. 고르지 않으면 시작할 때 남은 자리에 앉습니다.");
            seat.HeroBox.TooltipText = Lang.T("같은 팀에서 한 용병은 한 명만 고를 수 있습니다.");
        }
        _chatInput.PlaceholderText = Lang.T("채팅");
        _send.Text = Lang.T("보내기");
        _rejoin.Text = Lang.T("경기로 돌아가기");
        _leave.Text = Lang.T("방 나가기");
        Refresh();
    }

    // One seat row: role picker or label, name, ping, and on hero rows the hero picker or label.
    // Pickers show on a row this guest may take; another guest's row shows plain labels.
    private sealed class SeatRow
    {
        public OptionButton RoleBox, HeroBox;
        public Label Role, Name, Ping, Hero;
    }

    private Control TeamPanel(uint team)
    {
        var panel = new PanelContainer { ThemeTypeVariation = "PlatePanel", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var rows = new VBoxContainer();
        rows.AddThemeConstantOverride("separation", 6);
        var caption = LobbyPalette.Caption("", 15, LobbyPalette.Brass);
        _teamCaptions.Add(caption);
        rows.AddChild(caption);
        for (int i = 0; i < 4; i++)
        {
            var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, 30) };
            row.AddThemeConstantOverride("separation", 10);
            var seat = new SeatRow
            {
                RoleBox = Picker(96),
                Role = Cell(96),
                Name = new Label { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter, ClipText = true, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis },
                Ping = LobbyPalette.Caption("", 14),
                HeroBox = Picker(144),
                Hero = Cell(144),
            };
            seat.Ping.CustomMinimumSize = new Vector2(52, 0);
            seat.Ping.HorizontalAlignment = HorizontalAlignment.Right;
            seat.Ping.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            uint rowTeam = team;
            seat.RoleBox.GetPopup().IdPressed += id => OnRoleChosen(rowTeam, (int)id);
            seat.HeroBox.GetPopup().IdPressed += id => _session.ChooseRole(rowTeam, "HERO", (uint)id);
            foreach (var cell in new Control[] { seat.RoleBox, seat.Role, seat.Name, seat.Ping, seat.HeroBox, seat.Hero }) row.AddChild(cell);
            rows.AddChild(row);
            _seatRows[team - 1].Add(seat);
        }
        panel.AddChild(rows);
        return panel;
    }

    private static OptionButton Picker(float width) => new()
    {
        ThemeTypeVariation = "SeatButton", CustomMinimumSize = new Vector2(width, 30),
        SizeFlagsVertical = SizeFlags.ShrinkCenter, FitToLongestItem = false,
    };

    private static Label Cell(float width)
    {
        var label = LobbyPalette.Caption("", 15);
        label.CustomMinimumSize = new Vector2(width, 0);
        label.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        return label;
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
        var room = _session.Room;
        if (room == null) return; // The session opens the lobby.
        var me = room.Me;
        bool waiting = room.State == "waiting";
        _title.Text = room.Name;
        RefreshRegions(room);
        foreach (var seat in room.Seats) RefreshSeat(room, seat, waiting && me != null);
        var unseated = room.Members.Where(m => m.Seat < 0 && !m.Spectator).Select(m => m.Name).ToArray();
        var spectators = room.Members.Where(m => m.Spectator).Select(m => m.Name).ToArray();
        _waiting.Text = Lang.F("인원 {0}/8", room.Members.Count) + (unseated.Length > 0 ? Lang.F(" · 선택 전: {0}", string.Join(", ", unseated)) : "") +
            (spectators.Length > 0 ? Lang.F(" · 관전: {0}", string.Join(", ", spectators)) : "");
        _spectate.Text = me?.Spectator == true ? Lang.T("관전 취소") : Lang.T("관전");
        _spectate.Disabled = !waiting;
        _start.Visible = room.IsHost;
        _start.Text = room.State == "countdown" ? Lang.T("시작 취소") : Lang.T("시작");
        _start.Disabled = room.State is not ("waiting" or "countdown");
        _rejoin.Visible = _session.Rejoinable != null;
        _status.Text = _session.Notice ?? _session.ErrorMessage ?? room.State switch
        {
            "countdown" => Lang.F("{0}초 후 시작", room.Countdown),
            "launching" => Lang.T("서버 준비 중"),
            "playing" => Lang.T("경기 진행 중"),
            _ when room.IsHost => Lang.T("대기 중"),
            _ => Lang.T("방장 대기 중"),
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
                _region.AddItem(Lang.T(region.Name));
                _region.SetItemMetadata(_region.ItemCount - 1, region.Id);
            }
        }
        for (int i = 0; i < room.Regions.Count; i++)
        {
            var region = room.Regions[i];
            _region.SetItemText(i, Lang.T(region.Name) + (_session.Pings.TryGetValue(region.Id, out int ms) ? $"  {ms}ms" : ""));
            if (region.Id == room.Region) _region.Select(i);
        }
        _region.Disabled = !room.IsHost || room.State != "waiting";
        _regionHint.Text = room.RegionAuto ? Lang.T("자동") : Lang.T("방장 선택");
        _updatingRegion = false;
    }

    private void RefreshSeat(RoomState room, RoomSeat seat, bool canChoose)
    {
        var row = _seatRows[seat.Team - 1][seat.Index % 4];
        var member = seat.PlayerId != null ? room.Member(seat.PlayerId) : null;
        bool mine = seat.PlayerId == room.You, hero = seat.Role == "HERO";
        bool open = canChoose && (member == null || mine);
        row.Name.Text = member == null ? "AI" : member.Name + (member.Id == room.HostId ? Lang.T(" (방장)") : "");
        row.Name.AddThemeColorOverride("font_color", member == null ? Faint : mine ? Accent : LobbyPalette.Text);
        row.Ping.Text = member == null ? "" : member.Ping < 0 ? "-" : $"{member.Ping}ms";
        row.Ping.AddThemeColorOverride("font_color", member != null && member.Ping >= 150 ? Warn : Muted);

        row.Role.Text = hero ? Lang.T("용병") : Lang.T("대표");
        row.Role.Visible = !open;
        row.RoleBox.Visible = open;
        if (open && !row.RoleBox.GetPopup().Visible)
        {
            var commander = room.Seats.First(s => s.Team == seat.Team && s.Role == "COMMANDER");
            row.RoleBox.Clear();
            row.RoleBox.AddItem(Lang.T("대표"), CommanderItem);
            row.RoleBox.SetItemDisabled(0, commander.PlayerId != null && commander.PlayerId != room.You);
            row.RoleBox.AddItem(Lang.T("용병"), HeroItem);
            row.RoleBox.SetItemDisabled(1, FreeHero(room, seat.Team) == 0);
            if (mine) row.RoleBox.AddItem(Lang.T("비우기"), StandItem);
            row.RoleBox.Select(hero ? 1 : 0);
        }
        row.RoleBox.AddThemeColorOverride("font_color", mine ? Accent : LobbyPalette.Text);

        row.Hero.Text = hero && member != null ? UnitCatalog.Name(seat.Hero) : "";
        row.Hero.Visible = !(hero && open);
        row.HeroBox.Visible = hero && open;
        if (hero && open && !row.HeroBox.GetPopup().Visible)
        {
            row.HeroBox.Clear();
            for (int i = 0; i < Heroes.Length; i++)
            {
                row.HeroBox.AddItem(UnitCatalog.Name(Heroes[i]), (int)Heroes[i]);
                row.HeroBox.SetItemDisabled(i, TakenByOther(room, seat.Team, Heroes[i]));
            }
            row.HeroBox.Select(mine ? System.Array.IndexOf(Heroes, seat.Hero) : -1);
            if (!mine) row.HeroBox.Text = Lang.T("용병 선택");
        }
        row.HeroBox.AddThemeColorOverride("font_color", mine ? Accent : Muted);
    }

    private static bool TakenByOther(RoomState room, uint team, uint hero) =>
        room.Seats.Any(s => s.Team == team && s.Role == "HERO" && s.PlayerId != null && s.PlayerId != room.You && s.Hero == hero);

    // The hero a guest gets by choosing 용병 on a row: the one they already play in this team,
    // else the first nobody else in the team holds. 0 when the team has no hero seat left for them.
    private static uint FreeHero(RoomState room, uint team)
    {
        var heroSeats = room.Seats.Where(s => s.Team == team && s.Role == "HERO").ToArray();
        var mine = heroSeats.FirstOrDefault(s => s.PlayerId == room.You);
        if (mine != null) return mine.Hero;
        if (heroSeats.All(s => s.PlayerId != null)) return 0;
        return Heroes.FirstOrDefault(h => !TakenByOther(room, team, h));
    }

    private void OnRoleChosen(uint team, int id)
    {
        var room = _session.Room;
        if (room == null) return;
        if (id == StandItem) _session.ChooseRole(0, "", 0);
        else if (id == CommanderItem) _session.ChooseRole(team, "COMMANDER", 0);
        else if (FreeHero(room, team) is uint hero and not 0) _session.ChooseRole(team, "HERO", hero);
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

    public override void _ExitTree()
    {
        if (_session != null) _session.Changed -= Refresh;
        if (_settings != null) _settings.LanguageChanged -= ApplyText;
    }
}
