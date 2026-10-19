using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// 영웅 모드에서 같은 편 다른 영웅의 초상화·이름·체력·마나와 부활까지 남은 시간을 왼쪽 위에 보여 준다.
// 아군은 늘 시야에 있으므로 서버의 UNIT/HP/MANA/REMOVE와 TEAM_RESPAWN만으로 그린다.
public partial class TeamFrame : PanelContainer
{
    private sealed class Mate
    {
        public uint Owner, UnitId, Type;
        public float Hp, MaxHp, Mana, MaxMana;
        public uint? RespawnTick;
        public bool Dead;
        public HBoxContainer Row;
        public TextureRect Portrait;
        public Label Name, HealthText, Status;
        public ProgressBar Health, ManaBar;
    }

    private readonly Dictionary<uint, Mate> _mates = new(); // By owning player.
    private readonly HashSet<uint> _away = new(); // Players whose seat the AI plays (server AWAY).
    private readonly List<StyleBoxFlat> _styles = new();
    private VBoxContainer _rows;
    private bool _active;
    private uint _localPlayer, _team, _tick;
    private bool _hasTick;

    public IReadOnlyCollection<uint> Teammates => _mates.Keys.OrderBy(id => id).ToArray();
    public string NameOf(uint owner) => _mates.TryGetValue(owner, out var m) ? m.Name.Text : null;
    public string HealthTextOf(uint owner) => _mates.TryGetValue(owner, out var m) ? m.HealthText.Text : null;
    public double ManaPercentOf(uint owner) => _mates.TryGetValue(owner, out var m) ? m.ManaBar.Value : -1;
    public string StatusOf(uint owner) => _mates.TryGetValue(owner, out var m) ? m.Status.Text : null;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
        OffsetLeft = 12; OffsetTop = 48;
        AddThemeStyleboxOverride("panel", Style("14222dbf", "526857", 5, 6));
        _rows = new VBoxContainer { Name = "Rows", MouseFilter = MouseFilterEnum.Ignore };
        _rows.AddThemeConstantOverride("separation", 6);
        AddChild(_rows);
        Visible = false;
    }

    public void SetLocal(PlayerRole role, uint playerId, uint team)
    {
        if (role != PlayerRole.Hero || playerId != _localPlayer || team != _team) Clear();
        _active = role == PlayerRole.Hero;
        _localPlayer = playerId; _team = team;
        Refresh();
    }

    public void HandleUnit(string[] parts)
    {
        if (!_active || parts.Length != 7 ||
            !uint.TryParse(parts[1], out uint type) || !uint.TryParse(parts[2], out uint id) || id == 0 ||
            !uint.TryParse(parts[3], out uint owner) || !uint.TryParse(parts[6], out uint team)) return;
        if (!UnitCatalog.IsHero(type) || team != _team || owner == 0 || owner == _localPlayer) return;
        Mate mate = Get(owner);
        if (mate.UnitId == id && mate.Type == type) return;
        mate.UnitId = id; mate.Type = type; mate.Dead = false; mate.RespawnTick = null;
        mate.Hp = mate.MaxHp = mate.Mana = mate.MaxMana = 0;
        Refresh();
    }

    public void HandleHealth(HealthSnapshot health)
    {
        if (Find(health.Id) is not Mate mate) return;
        mate.Hp = Mathf.Min(health.Current, health.Maximum); mate.MaxHp = health.Maximum;
        Refresh();
    }

    public void HandleMana(ManaSnapshot mana)
    {
        if (Find(mana.Id) is not Mate mate) return;
        mate.Mana = mana.Current; mate.MaxMana = mana.Maximum;
        Refresh();
    }

    // REMOVE is a death; allies are never hidden from their own team.
    public void HandleRemove(string[] parts)
    {
        if (parts.Length != 2 || parts[0] != "REMOVE" || !uint.TryParse(parts[1], out uint id) || Find(id) is not Mate mate) return;
        mate.UnitId = 0; mate.Dead = true; mate.Hp = 0;
        Refresh();
    }

    public void HandleRespawn(TeamRespawnSnapshot respawn)
    {
        if (!_active || respawn.PlayerId == _localPlayer) return;
        Mate mate = Get(respawn.PlayerId);
        if (mate.UnitId != 0) return; // Already back: a late timer must not mark it dead.
        mate.Dead = true; mate.RespawnTick = respawn.Tick;
        Refresh();
    }

    public void HandleTick(string[] parts)
    {
        if (parts.Length != 2 || !uint.TryParse(parts[1], out uint tick)) return;
        _tick = tick; _hasTick = true;
        if (_mates.Values.Any(m => m.Dead)) Refresh();
    }

    public void SetAway(uint owner, bool away)
    {
        if (away ? !_away.Add(owner) : !_away.Remove(owner)) return;
        Refresh();
    }

    public void Clear()
    {
        foreach (Mate mate in _mates.Values) mate.Row.QueueFree();
        _mates.Clear();
        _away.Clear();
        _hasTick = false; _tick = 0;
        Visible = false;
    }

    private Mate Find(uint unitId) => unitId == 0 ? null : _mates.Values.FirstOrDefault(m => m.UnitId == unitId);

    private Mate Get(uint owner)
    {
        if (_mates.TryGetValue(owner, out Mate mate)) return mate;
        mate = new Mate { Owner = owner };
        mate.Row = new HBoxContainer { Name = $"Mate{owner}", MouseFilter = MouseFilterEnum.Ignore };
        mate.Row.AddThemeConstantOverride("separation", 6);
        mate.Portrait = new TextureRect
        {
            CustomMinimumSize = new Vector2(36, 36), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore
        };
        mate.Row.AddChild(mate.Portrait);
        var column = new VBoxContainer { CustomMinimumSize = new Vector2(132, 0), MouseFilter = MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", 2);
        mate.Row.AddChild(column);
        var heading = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        column.AddChild(heading);
        mate.Name = Text(heading, 11, "e9eee4");
        mate.Status = Text(heading, 11, "ffc393");
        mate.Status.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        mate.Status.HorizontalAlignment = HorizontalAlignment.Right;
        (mate.Health, mate.HealthText) = Bar(column, "64cf79", 12, true);
        (mate.ManaBar, _) = Bar(column, "5aa0ff", 5, false);
        _rows.AddChild(mate.Row);
        _mates.Add(owner, mate);
        return mate;
    }

    private void Refresh()
    {
        Visible = _active && _mates.Count > 0;
        foreach (Mate mate in _mates.Values)
        {
            mate.Name.Text = mate.Type != 0 ? UnitCatalog.Name(mate.Type) : "팀원";
            Texture2D portrait = Portrait(mate.Type);
            if (mate.Portrait.Texture != portrait) mate.Portrait.Texture = portrait;
            mate.Portrait.Modulate = mate.Dead ? new Color(.45f, .45f, .45f) : Colors.White;
            bool knownHealth = !mate.Dead && mate.MaxHp > 0;
            mate.Health.Value = knownHealth ? 100.0 * mate.Hp / mate.MaxHp : 0;
            mate.HealthText.Text = knownHealth ? HealthDisplay.Whole(mate.Hp) + " / " + HealthDisplay.Whole(mate.MaxHp) : "— / —";
            mate.ManaBar.Value = !mate.Dead && mate.MaxMana > 0 ? 100.0 * mate.Mana / mate.MaxMana : 0;
            string status = !mate.Dead ? "" : mate.RespawnTick is uint at && _hasTick
                ? $"부활 {Math.Ceiling(Math.Max(0, unchecked((int)(at - _tick))) / (double)InterpolationClock.TickRate)}초" : "사망";
            mate.Status.Text = !_away.Contains(mate.Owner) ? status : status == "" ? "AI 대행" : "AI · " + status;
        }
    }

    private static Texture2D Portrait(uint type)
    {
        string path = type switch
        {
            UnitCatalog.HeroTest => "res://ui/portraits/mutant-wolf.png",
            UnitCatalog.HeroGolem => "res://ui/portraits/rune-golem.png",
            UnitCatalog.HeroMarshal => "res://ui/icons/marshal.svg",
            UnitCatalog.HeroRanger => "res://ui/portraits/archer.png",
            _ => null
        };
        return path != null && ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
    }

    private Label Text(Container parent, int size, string color)
    {
        var label = new Label { MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", new Color(color));
        label.AddThemeColorOverride("font_outline_color", new Color("101820"));
        label.AddThemeConstantOverride("outline_size", 3);
        parent.AddChild(label);
        return label;
    }

    private (ProgressBar, Label) Bar(Container parent, string color, int height, bool withText)
    {
        var holder = new Control { CustomMinimumSize = new Vector2(0, height), MouseFilter = MouseFilterEnum.Ignore };
        parent.AddChild(holder);
        var bar = new ProgressBar { ShowPercentage = false, MouseFilter = MouseFilterEnum.Ignore };
        var track = Style("101c26", color, 2, 0);
        track.BgColor = new Color(color).Darkened(0.82f);
        track.BorderColor = new Color(color).Darkened(0.6f);
        bar.AddThemeStyleboxOverride("background", track);
        bar.AddThemeStyleboxOverride("fill", Style(color, color, 2, 0));
        holder.AddChild(bar);
        bar.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Label label = null;
        if (withText)
        {
            label = new Label
            {
                Text = "— / —", HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore
            };
            label.AddThemeFontSizeOverride("font_size", 9);
            label.AddThemeColorOverride("font_color", new Color("f2f4ee"));
            label.AddThemeColorOverride("font_outline_color", new Color("101820"));
            label.AddThemeConstantOverride("outline_size", 2);
            holder.AddChild(label);
            label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }
        return (bar, label);
    }

    private StyleBoxFlat Style(string background, string border, int radius, int margin)
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(background), BorderColor = new Color(border),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
            ContentMarginLeft = margin, ContentMarginRight = margin, ContentMarginTop = margin, ContentMarginBottom = margin
        };
        _styles.Add(style);
        return style;
    }

    public override void _ExitTree()
    {
        foreach (StyleBoxFlat style in _styles) style.Dispose();
        _styles.Clear();
    }
}
