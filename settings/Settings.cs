using Godot;
using System;

// The player's own preferences, kept in user://settings.cfg across restarts: the interface
// language the lobby's settings panel picks. A first run is in English, whatever the system
// language (docs/settings.md). Autoloaded first, so the other autoloads already see the chosen
// language.
public partial class Settings : Node
{
    public const string DefaultPath = "user://settings.cfg";

    public static Settings Instance { get; private set; }
    public event Action LanguageChanged;
    public string Language { get; private set; } = Lang.Default;

    private string _path = DefaultPath;

    public override void _EnterTree() => Instance = this;

    public override void _ExitTree() { if (Instance == this) Instance = null; }

    public override void _Ready()
    {
        GetTree().Root.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        Load(DefaultPath);
    }

    // Reads and applies a settings file; a missing or unreadable one gives the defaults.
    // Later saves go to the same file (checks use their own instead of the player's).
    public void Load(string path)
    {
        _path = path;
        var config = new ConfigFile();
        config.Load(path);
        ApplyLanguage(config.GetValue("interface", "language", Lang.Default).AsString());
    }

    public void Save()
    {
        var config = new ConfigFile();
        config.SetValue("interface", "language", Language);
        if (config.Save(_path) != Error.Ok) GD.PushWarning($"Settings: could not save {_path}");
    }

    public void SetLanguage(string language)
    {
        string before = Language;
        ApplyLanguage(language);
        Save();
        if (Language != before) LanguageChanged?.Invoke();
    }

    private void ApplyLanguage(string language)
    {
        Language = Lang.IsSupported(language) ? language : Lang.Default;
        TranslationServer.SetLocale(Language);
    }
}
