using Godot;

// The lobby screens' settings: a header button opens a card over the screen with the interface
// language. A change applies at once and Settings keeps it in user://settings.cfg (docs/settings.md).
public partial class SettingsPanel : Control
{
    private Settings _settings;
    private Button _open, _close;
    private Label _title, _languageCaption;
    private OptionButton _language;

    public Button OpenButton => _open;
    public OptionButton LanguagePicker => _language;

    // Adds the button to the screen's header and the hidden panel over the whole screen.
    public static SettingsPanel Attach(Control screen)
    {
        var panel = new SettingsPanel { Name = "SettingsPanel", Visible = false };
        panel._open = new Button { Name = "SettingsButton", ThemeTypeVariation = "SecondaryButton", CustomMinimumSize = new Vector2(0, 30) };
        panel._open.AddThemeFontSizeOverride("font_size", 14);
        panel._open.Pressed += panel.Open;
        var header = screen.GetNode<HBoxContainer>("Header/Row");
        header.AddThemeConstantOverride("separation", 14);
        header.AddChild(panel._open);
        panel.Build(screen.GetNode<PanelContainer>("Center/Card").GetThemeStylebox("panel"));
        screen.AddChild(panel);
        return panel;
    }

    private void Build(StyleBox card)
    {
        _settings = Settings.Instance;
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        var shade = new ColorRect { Color = new Color(0.02f, 0.024f, 0.027f, 0.72f) };
        shade.SetAnchorsPreset(LayoutPreset.FullRect);
        shade.GuiInput += input => { if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) Close(); };
        AddChild(shade);
        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);
        var frame = new PanelContainer { CustomMinimumSize = new Vector2(460, 0) };
        // The screen's card, made opaque so the card under it does not show through.
        if (card.Duplicate() is StyleBoxFlat opaque) { opaque.BgColor = opaque.BgColor with { A = 1 }; card = opaque; }
        frame.AddThemeStyleboxOverride("panel", card);
        center.AddChild(frame);
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 12);
        frame.AddChild(content);

        _title = new Label();
        _title.AddThemeFontSizeOverride("font_size", 26);
        content.AddChild(_title);
        _languageCaption = LobbyPalette.Caption("", 14, LobbyPalette.Brass);
        content.AddChild(_languageCaption);
        _language = new OptionButton { ThemeTypeVariation = "SecondaryButton", CustomMinimumSize = new Vector2(0, 38) };
        foreach (var (_, name) in Lang.Supported) _language.AddItem(name);
        _language.ItemSelected += index => _settings?.SetLanguage(Lang.Supported[(int)index].Code);
        content.AddChild(_language);
        content.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });
        _close = new Button { CustomMinimumSize = new Vector2(0, 42) };
        _close.Pressed += Close;
        content.AddChild(_close);
    }

    public override void _Ready()
    {
        if (_settings != null) _settings.LanguageChanged += ApplyText;
        ApplyText();
    }

    public override void _ExitTree() { if (_settings != null) _settings.LanguageChanged -= ApplyText; }

    private void ApplyText()
    {
        _open.Text = Lang.T("설정");
        _title.Text = Lang.T("설정");
        _languageCaption.Text = Lang.T("언어");
        _close.Text = Lang.T("닫기");
    }

    public void Open()
    {
        int language = System.Array.FindIndex(Lang.Supported, l => l.Code == (_settings?.Language ?? Lang.Default));
        _language.Select(language);
        Show();
        _close.GrabFocus();
    }

    public void Close() => Hide();

    public override void _UnhandledInput(InputEvent input)
    {
        if (!Visible || !input.IsActionPressed("ui_cancel")) return;
        GetViewport().SetInputAsHandled();
        Close();
    }
}
