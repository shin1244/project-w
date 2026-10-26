using Godot;

// One command panel cell: a picture, its hotkey in the top-left corner and a short status (remaining seconds,
// research step) along the bottom. The name and the details are in the tooltip.
public partial class CommandSlot : Button
{
    private static readonly Color DisabledTint = new(1, 1, 1, .35f);
    private readonly TextureRect _icon;
    private readonly Label _key, _status;

    public string CommandName { get; private set; } = "";
    public string Hotkey { get; private set; } = "";
    public string Status { get; set; } = "";
    public string Detail { get; set; } = "";
    // One line for checks and logs, e.g. "공격(A)" or "공격력 연구(Q) 1단계 30초".
    public string Caption => CommandName + (Hotkey.Length > 0 ? $"({Hotkey})" : "") + (Status.Length > 0 ? " " + Status : "");

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
        Status = Detail = "";
        Disabled = name.Length == 0;
    }

    public void Paint()
    {
        _icon.SelfModulate = Disabled ? DisabledTint : Colors.White;
        _key.Text = Hotkey;
        _key.Visible = Hotkey.Length > 0;
        _status.Text = Status;
        _status.Visible = Status.Length > 0;
        TooltipText = CommandName.Length == 0 ? "" :
            CommandName + (Hotkey.Length > 0 ? $" ({Hotkey})" : "") + (Status.Length > 0 ? $" · {Status}" : "") +
            (Detail.Length > 0 ? "\n" + Detail : "");
    }
}
