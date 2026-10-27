using Godot;
using System;
using System.Collections.Generic;

// The player's own preferences, kept in user://settings.cfg across restarts: the volume of the
// buses the lobby's settings panel shows and the interface language. A first run plays at full
// volume in English, whatever the system language (docs/settings.md). Autoloaded first, so the
// other autoloads already see the chosen language.
public partial class Settings : Node
{
    public const string DefaultPath = "user://settings.cfg";
    // Music has a bus but nothing plays on it yet, so it has no slider.
    public static readonly string[] Buses = { "Master", SoundBank.World, SoundBank.Interface };

    public static Settings Instance { get; private set; }
    public event Action LanguageChanged;
    public string Language { get; private set; } = Lang.Default;

    private readonly Dictionary<string, float> _volumes = new(), _layoutDb = new();
    private string _path = DefaultPath;

    public override void _EnterTree() => Instance = this;

    public override void _ExitTree() { if (Instance == this) Instance = null; }

    public override void _Ready()
    {
        GetTree().Root.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        // A slider scales its bus from the level default_bus_layout.tres gives it.
        foreach (string bus in Buses)
        {
            int index = AudioServer.GetBusIndex(bus);
            _layoutDb[bus] = index >= 0 ? AudioServer.GetBusVolumeDb(index) : 0;
        }
        Load(DefaultPath);
    }

    // Reads and applies a settings file; a missing or unreadable one gives the defaults.
    // Later saves go to the same file (checks use their own instead of the player's).
    public void Load(string path)
    {
        _path = path;
        var config = new ConfigFile();
        config.Load(path);
        foreach (string bus in Buses) ApplyVolume(bus, (float)config.GetValue("audio", bus, 1.0).AsDouble());
        ApplyLanguage(config.GetValue("interface", "language", Lang.Default).AsString());
    }

    public void Save()
    {
        var config = new ConfigFile();
        foreach (string bus in Buses) config.SetValue("audio", bus, Volume(bus));
        config.SetValue("interface", "language", Language);
        if (config.Save(_path) != Error.Ok) GD.PushWarning($"Settings: could not save {_path}");
    }

    // 0 (muted) to 1 (the layout's level).
    public float Volume(string bus) => _volumes.TryGetValue(bus, out float volume) ? volume : 1;

    // Applies at once; the caller saves, so a dragged slider writes the file once.
    public void SetVolume(string bus, float volume) => ApplyVolume(bus, volume);

    public void SetLanguage(string language)
    {
        string before = Language;
        ApplyLanguage(language);
        Save();
        if (Language != before) LanguageChanged?.Invoke();
    }

    private void ApplyVolume(string bus, float volume)
    {
        volume = Mathf.Clamp(float.IsFinite(volume) ? volume : 1, 0, 1);
        _volumes[bus] = volume;
        int index = AudioServer.GetBusIndex(bus);
        if (index < 0) return;
        AudioServer.SetBusMute(index, volume == 0);
        if (volume > 0) AudioServer.SetBusVolumeDb(index, _layoutDb[bus] + Mathf.LinearToDb(volume));
    }

    private void ApplyLanguage(string language)
    {
        Language = Lang.IsSupported(language) ? language : Lang.Default;
        TranslationServer.SetLocale(Language);
    }
}
