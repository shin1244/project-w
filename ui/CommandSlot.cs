using Godot;
using System;

// What a command costs, from the server: resources, the seconds it takes and, for units, the supply it uses.
public readonly record struct CommandTerms(long Wood, long Oil, double Seconds, long? Supply = null)
{
    // One line for checks and logs; the tooltip shows the same values as icons.
    public string Text => Supply is long supply
        ? Lang.F("목재 {0:N0} · 기름 {1:N0} · 인구 {2} · {3:0.##}초", Wood, Oil, supply, Seconds)
        : Lang.F("목재 {0:N0} · 기름 {1:N0} · {2:0.##}초", Wood, Oil, Seconds);
}

// One command panel cell: a picture, its hotkey in the top-left corner and a short status (remaining seconds,
// research step) along the bottom. The name and the details are in the tooltip: the cost as icons, then a short
// list instead of a paragraph (Heading and Points), then other notes (Detail) and why it is locked (Warning).
public partial class CommandSlot : Button
{
    private static readonly Color DisabledTint = new(1, 1, 1, .35f);
    private static readonly Color TipText = new("eef2e6"), TipMuted = new("a8b4a6"), TipWarning = new("f0a16a");
    private readonly TextureRect _icon;
    private readonly Label _key, _status;

    public string CommandName { get; private set; } = "";
    public string Hotkey { get; private set; } = "";
    public string Status { get; set; } = "";
    public string Detail { get; set; } = "";
    public CommandTerms? Terms { get; set; }
    public string Heading { get; set; } = "";
    public string[] Points { get; set; } = Array.Empty<string>();
    public string Warning { get; set; } = "";
    // One line for checks and logs, e.g. "공격(A)" or "공격력 연구(Q) 1단계 30초".
    public string Caption => CommandName + (Hotkey.Length > 0 ? $"({Hotkey})" : "") + (Status.Length > 0 ? " " + Status : "");
    private string Title => CommandName + (Hotkey.Length > 0 ? $" ({Hotkey})" : "") + (Status.Length > 0 ? $" · {Status}" : "");

    public CommandSlot()
    {
        _icon = new TextureRect
        {
            Name = "Icon", ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(_icon);
        _icon.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _icon.OffsetLeft = _icon.OffsetTop = 4;
        _icon.OffsetRight = _icon.OffsetBottom = -4;
        _key = new Label { Name = "Key", MouseFilter = MouseFilterEnum.Ignore, Position = new Vector2(4, 1) };
        _key.AddThemeFontSizeOverride("font_size", 12);
        _key.AddThemeColorOverride("font_color", new Color("f2e8cb"));
        _key.AddThemeColorOverride("font_outline_color", new Color("101820"));
        _key.AddThemeConstantOverride("outline_size", 4);
        AddChild(_key);
        _status = new Label
        {
            Name = "Status", MouseFilter = MouseFilterEnum.Ignore, HorizontalAlignment = HorizontalAlignment.Center,
            Visible = false
        };
        _status.AddThemeFontSizeOverride("font_size", 11);
        _status.AddThemeColorOverride("font_color", new Color("f2e8cb"));
        _status.AddThemeStyleboxOverride("normal", new StyleBoxFlat { BgColor = new Color("101820d0") });
        AddChild(_status);
        _status.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
        _status.OffsetLeft = 1;
        _status.OffsetRight = -1;
        _status.OffsetTop = -16;
        _status.OffsetBottom = -1;
    }

    // An empty name clears the cell and disables it; callers enable it again once its command is allowed.
    public void Set(string name, string hotkey = "", Texture2D icon = null)
    {
        CommandName = name;
        Hotkey = name.Length > 0 ? hotkey : "";
        _icon.Texture = name.Length > 0 ? icon : null;
        Status = Detail = Heading = Warning = "";
        Terms = null;
        Points = Array.Empty<string>();
        Disabled = name.Length == 0;
    }

    public void Paint()
    {
        _icon.SelfModulate = Disabled ? DisabledTint : Colors.White;
        _key.Text = Hotkey;
        _key.Visible = Hotkey.Length > 0;
        _status.Text = Status;
        _status.Visible = Status.Length > 0;
        if (CommandName.Length == 0)
        {
            TooltipText = "";
            return;
        }
        string text = Title;
        if (Terms is CommandTerms terms) text += "\n" + terms.Text;
        if (Heading.Length > 0) text += "\n" + Heading;
        foreach (string point in Points) text += "\n• " + point;
        if (Detail.Length > 0) text += "\n" + Detail;
        if (Warning.Length > 0) text += "\n" + Warning;
        TooltipText = text;
    }

    // Commands with a cost or a short list get the laid-out tooltip; the rest keep the plain one.
    public override GodotObject _MakeCustomTooltip(string forText)
    {
        if (Terms == null && Points.Length == 0) return null;
        var box = new VBoxContainer { Name = "CommandTooltip" };
        box.AddThemeConstantOverride("separation", 4);
        box.AddChild(TipLine(Title, 15, TipText));
        if (Terms is CommandTerms terms) box.AddChild(TermsRow(terms));
        if (Heading.Length > 0) box.AddChild(TipLine(Heading, 13, TipMuted));
        foreach (string point in Points) box.AddChild(TipLine("• " + point, 14, TipText));
        if (Detail.Length > 0) box.AddChild(TipLine(Detail, 12, TipMuted));
        if (Warning.Length > 0) box.AddChild(TipLine(Warning, 13, TipWarning));
        return box;
    }

    private static Label TipLine(string text, int size, Color color)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    // Wood and oil only when the command needs them, then supply for units, then the seconds it takes.
    private static HBoxContainer TermsRow(CommandTerms terms)
    {
        var row = new HBoxContainer { Name = "Terms" };
        row.AddThemeConstantOverride("separation", 10);
        void Add(string icon, string value)
        {
            var item = new HBoxContainer { Name = icon };
            item.AddThemeConstantOverride("separation", 3);
            item.AddChild(new TextureRect
            {
                Texture = GD.Load<Texture2D>($"res://ui/icons/{icon}.svg"), CustomMinimumSize = new Vector2(22, 20),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
            });
            item.AddChild(TipLine(value, 14, TipText));
            row.AddChild(item);
        }
        if (terms.Wood > 0) Add("wood", terms.Wood.ToString("N0", System.Globalization.CultureInfo.InvariantCulture));
        if (terms.Oil > 0) Add("oil", terms.Oil.ToString("N0", System.Globalization.CultureInfo.InvariantCulture));
        if (terms.Supply is long supply) Add("population", supply.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Add("time", Lang.F("{0:0.##}초", terms.Seconds));
        return row;
    }
}
