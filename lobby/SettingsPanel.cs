using Godot;
using System.Collections.Generic;

// The lobby screens' settings: a header button opens a card over the screen with the master,
// effect and interface volume and the interface language. Every change applies at once and
// Settings keeps it in user://settings.cfg (docs/settings.md).
public partial class SettingsPanel : Control
{
    private static readonly (string Bus, string Name)[] Volumes =
        { ("Master", "전체 음량"), (SoundBank.World, "효과음"), (SoundBank.Interface, "인터페이스") };

    private sealed class VolumeRow
    {
        public string Bus, Name;
        public Label Label, Value;
        public HSlider Slider;
        public bool Unsaved;
    }

    private Settings _settings;
    private Button _open, _close;
    private Label _title, _volumeCaption, _languageCaption;
    private OptionButton _language;
    private readonly List<VolumeRow> _rows = new();

    public Button OpenButton => _open;
    public OptionButton LanguagePicker => _language;
    public HSlider Slider(string bus) => _rows.Find(row => row.Bus == bus)?.Slider;

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
        _volumeCaption = LobbyPalette.Caption("", 14, LobbyPalette.Brass);
        content.AddChild(_volumeCaption);
        foreach (var (bus, name) in Volumes) content.AddChild(VolumeLine(bus, name));
        content.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });
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

    private Control VolumeLine(string bus, string name)
    {
        var row = new VolumeRow { Bus = bus, Name = name };
        var line = new HBoxContainer();
        line.AddThemeConstantOverride("separation", 12);
        row.Label = new Label { CustomMinimumSize = new Vector2(132, 0), SizeFlagsVertical = SizeFlags.ShrinkCenter };
        row.Label.AddThemeFontSizeOverride("font_size", 15);
        row.Slider = new HSlider
        {
            Name = bus, MinValue = 0, MaxValue = 100, Step = 1, SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkCenter, CustomMinimumSize = new Vector2(0, 24)
        };
        row.Slider.AddThemeStyleboxOverride("slider", Track(new Color(0.137f, 0.153f, 0.169f)));
        row.Slider.AddThemeStyleboxOverride("grabber_area", Track(LobbyPalette.Brass));
        row.Slider.AddThemeStyleboxOverride("grabber_area_highlight", Track(LobbyPalette.BrassLight));
        row.Value = LobbyPalette.Caption("", 14, LobbyPalette.Text);
        row.Value.CustomMinimumSize = new Vector2(48, 0);
        row.Value.HorizontalAlignment = HorizontalAlignment.Right;
        row.Value.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        // The level follows the mouse; the file is written once the button is let go. Keys and
        // the wheel move the slider without a drag, so those changes save at once.
        row.Slider.DragEnded += _ => { if (row.Unsaved) Released(row); };
        row.Slider.ValueChanged += value =>
        {
            _settings?.SetVolume(bus, (float)value / 100);
            row.Value.Text = $"{(int)value}%";
            row.Unsaved = true;
            if (!Input.IsMouseButtonPressed(MouseButton.Left)) Released(row);
        };
        line.AddChild(row.Label);
        line.AddChild(row.Slider);
        line.AddChild(row.Value);
        _rows.Add(row);
        return line;
    }

    // Saves once the slider settles and plays a click on the bus it set, so the level is heard.
    // World effects need a place on the map, so the effects slider stays silent here.
    private void Released(VolumeRow row)
    {
        row.Unsaved = false;
        _settings?.Save();
        if (row.Bus != SoundBank.World) Sfx.Interface("ui-click");
    }

    private static StyleBoxFlat Track(Color color) => new()
    {
        BgColor = color, ContentMarginTop = 3, ContentMarginBottom = 3,
        CornerRadiusTopLeft = 2, CornerRadiusTopRight = 2, CornerRadiusBottomLeft = 2, CornerRadiusBottomRight = 2
    };

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
        _volumeCaption.Text = Lang.T("음량");
        _languageCaption.Text = Lang.T("언어");
        foreach (var row in _rows) row.Label.Text = Lang.T(row.Name);
        _close.Text = Lang.T("닫기");
    }

    public void Open()
    {
        foreach (var row in _rows)
        {
            float volume = _settings?.Volume(row.Bus) ?? 1;
            row.Slider.SetValueNoSignal(Mathf.Round(volume * 100));
            row.Value.Text = $"{(int)row.Slider.Value}%";
        }
        int language = System.Array.FindIndex(Lang.Supported, l => l.Code == (_settings?.Language ?? Lang.Default));
        _language.Select(language);
        Show();
        _close.GrabFocus();
    }

    public void Close()
    {
        if (_rows.Exists(row => row.Unsaved))
        {
            foreach (var row in _rows) row.Unsaved = false;
            _settings?.Save();
        }
        Hide();
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (!Visible || !input.IsActionPressed("ui_cancel")) return;
        GetViewport().SetInputAsHandled();
        Close();
    }
}
