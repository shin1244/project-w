using Godot;
using System;

// A compact public event HUD. It never consumes mouse input intended for the battlefield.
public partial class TributeEventPanel : Control
{
    public string StatusText => _status?.Text ?? "";
    public float ChannelProgress => _snapshot?.Progress(_tick) ?? 0;
    public string CollectionText => _notice;

    private TributeSnapshot _snapshot;
    private uint _tick, _localTeam, _noticeUntil;
    private string _notice = "";
    private Label _status;
    private ProgressBar _progress;
    private StyleBoxFlat _frame, _fill;
    private static readonly Color Amber = new("e5bc69");

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.CenterTop);
        OffsetLeft = -115; OffsetRight = 115; OffsetTop = 42; OffsetBottom = 72;
        var panel = new PanelContainer { Name = "Frame", MouseFilter = MouseFilterEnum.Ignore };
        AddChild(panel);
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _frame = new StyleBoxFlat
        {
            BgColor = new Color("17212bbf"), BorderColor = new Color("70674f"),
            BorderWidthBottom = 1, CornerRadiusTopLeft = 5, CornerRadiusTopRight = 5,
            CornerRadiusBottomLeft = 5, CornerRadiusBottomRight = 5,
            ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 4, ContentMarginBottom = 4
        };
        panel.AddThemeStyleboxOverride("panel", _frame);
        var content = new VBoxContainer { Name = "Content", MouseFilter = MouseFilterEnum.Ignore };
        content.AddThemeConstantOverride("separation", 1);
        panel.AddChild(content);
        _status = Line(content, "Status", 13, Amber);
        _progress = new ProgressBar
        {
            Name = "ChannelProgress", MouseFilter = MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(0, 3), MaxValue = 1, Step = .001, ShowPercentage = false
        };
        _fill = new StyleBoxFlat { BgColor = Amber, CornerRadiusTopLeft = 2, CornerRadiusTopRight = 2,
            CornerRadiusBottomLeft = 2, CornerRadiusBottomRight = 2 };
        _progress.AddThemeStyleboxOverride("fill", _fill);
        _progress.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color("0d161d"),
            CornerRadiusTopLeft = 2, CornerRadiusTopRight = 2, CornerRadiusBottomLeft = 2, CornerRadiusBottomRight = 2 });
        content.AddChild(_progress);
        Visible = _snapshot != null;
        Refresh();
    }

    public void Apply(TributeSnapshot snapshot, uint currentTick, uint localTeam)
    {
        // Reconnect snapshots establish the baseline; only a newly observed capture announces.
        if (_snapshot != null && (snapshot.Team1Count > _snapshot.Team1Count || snapshot.Team2Count > _snapshot.Team2Count))
        {
            uint team = snapshot.Team1Count > _snapshot.Team1Count ? 1u : 2u;
            _notice = $"{team}팀 공물 획득";
            _noticeUntil = unchecked(currentTick + 3 * (uint)InterpolationClock.TickRate);
        }
        _snapshot = snapshot;
        _tick = currentTick;
        _localTeam = localTeam;
        Visible = true;
        Refresh();
    }

    public void SetTick(uint tick)
    {
        _tick = tick;
        Refresh();
    }

    public void Reset()
    {
        _snapshot = null; _tick = 0; _noticeUntil = 0; _notice = "";
        Visible = false;
        Refresh();
    }

    private void Refresh()
    {
        if (_status == null || _snapshot == null) return;
        if (_notice.Length > 0 && unchecked((int)(_noticeUntil - _tick)) <= 0) _notice = "";
        Color color = Amber;
        if (_notice.Length > 0)
        {
            _status.Text = _notice;
        }
        else if (_snapshot.Channeling)
        {
            _status.Text = $"{_snapshot.CapturerTeam}팀 수집 · {_snapshot.RemainingSeconds(_tick):0.0}초";
            color = TeamColor(_snapshot.CapturerTeam);
        }
        else if (_snapshot.Active)
        {
            _status.Text = "공물 등장";
        }
        else if (_snapshot.RamsMarching)
        {
            _status.Text = "공성추 진군 중";
        }
        else
        {
            _status.Text = $"공물 · {Countdown(_snapshot.NextSpawnTick)}";
            color = new Color("c4c1b0");
        }
        _status.AddThemeColorOverride("font_color", color);
        _frame.BorderColor = color.Darkened(.35f);
        _fill.BgColor = color;
        _progress.Visible = _snapshot.Channeling;
        _progress.Value = ChannelProgress;
    }

    private string Countdown(uint until)
    {
        int remaining = Math.Max(0, unchecked((int)(until - _tick)));
        int seconds = (remaining + InterpolationClock.TickRate - 1) / InterpolationClock.TickRate;
        return $"{seconds / 60:00}:{seconds % 60:00}";
    }

    private Color TeamColor(uint team) => _localTeam is 1 or 2
        ? team == _localTeam ? Minimap.AllyColor : Minimap.EnemyColor
        : team == 1 ? new Color("80bce7") : new Color("efae77");

    private static Label Line(Node parent, string name, int fontSize, Color color)
    {
        var label = new Label
        {
            Name = name, HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        parent.AddChild(label);
        return label;
    }
}
