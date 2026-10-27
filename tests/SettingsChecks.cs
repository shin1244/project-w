using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

// The player's settings (settings/Settings.cs, lobby/SettingsPanel.cs): defaults, saving and
// loading again as a restart does, bus levels, the language and its English text. Works on its
// own file so the player's user://settings.cfg is never written.
public partial class SettingsChecks : Node
{
    private const string Path = "user://settings-checks.cfg";
    // Korean reaching Lang.T other than as a literal at the call.
    private static readonly string[] Indirect =
    {
        "전체 음량", "효과음", "인터페이스", "돌연변이 늑대", "룬 골렘", "사령관", "한국", "미국", "독일", "호주",
        "연결 실패: {0}", "서버 연결이 종료되었습니다.", "서버 연결 종료: {0}", "전송 대기열이 가득 찼습니다. 서버 연결을 확인해 주세요.",
        "전송 시간이 초과되었습니다. 서버 연결을 확인해 주세요.", "전송 실패: {0}",
    };

    private Settings _settings;

    public override async void _Ready()
    {
        try
        {
            _settings = Settings.Instance;
            Check(_settings != null && GetTree().Root.AutoTranslateMode == AutoTranslateModeEnum.Disabled,
                "Settings autoload runs and turns label auto-translation off");
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(Path));
            CheckEnglishText();
            CheckDefaultsAndRestart();
            await CheckLobbyPanel();
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(Path));
            _settings.Load(Settings.DefaultPath);
            GD.Print("PASS: settings default to English at full volume, apply at once, save and load back; lobby text follows the language");
            // Let go of the screen's styles and textures before the engine checks for leaks.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    // Every Korean string passed to Lang.T or Lang.F has English with the same placeholders.
    private static void CheckEnglishText()
    {
        var english = GD.Load<Translation>("res://locale/en.po");
        Check(english != null && english.Locale == Lang.English, "locale/en.po loads as the English translation");
        var literal = new Regex(@"Lang\.(T|F)\(\s*""((?:[^""\\]|\\.)*)""(?:\s*,\s*""(\w+)"")?");
        var used = new List<(string Text, string Context)>();
        foreach (string file in Sources("res://"))
            foreach (Match match in literal.Matches(FileAccess.GetFileAsString(file)))
                used.Add((Regex.Unescape(match.Groups[2].Value), match.Groups[1].Value == "T" && match.Groups[3].Success ? match.Groups[3].Value : ""));
        Check(used.Count > 100, $"Found the translated strings in the source ({used.Count})");
        used.AddRange(Indirect.Select(text => (text, "")));
        var placeholder = new Regex(@"\{\d+(:[^}]*)?\}");
        foreach (var (text, context) in used.Distinct())
        {
            string translated = english.GetMessage(text, context);
            Check(translated != "", $"English for \"{text}\"{(context != "" ? $" ({context})" : "")}");
            string Holes(string s) => string.Join(",", placeholder.Matches(s).Select(m => m.Value).OrderBy(v => v));
            Check(Holes(translated) == Holes(text), $"English for \"{text}\" keeps its placeholders");
        }
    }

    private static IEnumerable<string> Sources(string folder)
    {
        foreach (string directory in DirAccess.GetDirectoriesAt(folder))
            if (!directory.StartsWith('.') && directory is not ("tests" or "tools" or "previews" or "bin" or "obj"))
                foreach (string file in Sources(folder.PathJoin(directory))) yield return file;
        foreach (string file in DirAccess.GetFilesAt(folder))
            if (file.EndsWith(".cs")) yield return folder.PathJoin(file);
    }

    private void CheckDefaultsAndRestart()
    {
        _settings.Load(Path);
        Check(_settings.Language == Lang.English && TranslationServer.GetLocale() == Lang.English, "A first run is in English");
        Check(Lang.T("방 목록") == "Rooms" && Lang.T("입장", "lobby") == "Enter" && Lang.T("입장", "room") == "Join",
            "English text, told apart by context");
        Check(Lang.F("{0}초 후 시작", 5) == "Starting in 5s", "Formatted English");
        Check(Lang.T("아직 번역하지 않은 문장") == "아직 번역하지 않은 문장", "Untranslated text stays Korean");
        foreach (string bus in Settings.Buses)
        {
            int index = AudioServer.GetBusIndex(bus);
            Check(index >= 0 && _settings.Volume(bus) == 1 && !AudioServer.IsBusMute(index), $"{bus} starts at full volume");
        }
        Check(Mathf.IsEqualApprox(AudioServer.GetBusVolumeDb(AudioServer.GetBusIndex(SoundBank.Interface)), -3),
            "Full volume keeps the bus layout's own level");

        int changes = 0;
        void Count() => changes++;
        _settings.LanguageChanged += Count;
        _settings.SetVolume(SoundBank.World, .5f);
        _settings.SetVolume(SoundBank.Interface, 0);
        _settings.SetLanguage(Lang.Korean);
        _settings.SetLanguage(Lang.Korean);
        _settings.LanguageChanged -= Count;
        int effects = AudioServer.GetBusIndex(SoundBank.World), interfaceBus = AudioServer.GetBusIndex(SoundBank.Interface);
        Check(Mathf.IsEqualApprox(AudioServer.GetBusVolumeDb(effects), Mathf.LinearToDb(.5f)) && !AudioServer.IsBusMute(effects),
            "Half volume is applied at once");
        Check(AudioServer.IsBusMute(interfaceBus), "Zero mutes the bus");
        Check(changes == 1 && TranslationServer.GetLocale() == Lang.Korean && Lang.T("방 목록") == "방 목록",
            "Korean applies at once and is announced once");

        var file = new ConfigFile();
        Check(file.Load(Path) == Error.Ok && file.GetValue("interface", "language").AsString() == Lang.Korean &&
            Mathf.IsEqualApprox((float)file.GetValue("audio", SoundBank.World).AsDouble(), .5f), "Choices are saved");
        // A restart reads the file again; values changed since the last save are not kept.
        _settings.SetVolume(SoundBank.World, .9f);
        _settings.Load(Path);
        Check(_settings.Language == Lang.Korean && Mathf.IsEqualApprox(_settings.Volume(SoundBank.World), .5f) &&
            _settings.Volume(SoundBank.Interface) == 0 && AudioServer.IsBusMute(interfaceBus), "Saved choices come back after a restart");

        file.SetValue("interface", "language", "xx");
        file.SetValue("audio", "Master", 7);
        file.Save(Path);
        _settings.Load(Path);
        Check(_settings.Language == Lang.English && _settings.Volume("Master") == 1, "A broken file falls back to valid settings");
    }

    private async Task CheckLobbyPanel()
    {
        DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(Path));
        _settings.Load(Path);
        var lobby = GD.Load<PackedScene>("res://lobby/Lobby.tscn").Instantiate<Lobby>();
        AddChild(lobby);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var panel = lobby.GetNode<SettingsPanel>("SettingsPanel");
        var enter = lobby.GetNode<Button>("%PrimaryButton");
        Check(!panel.Visible && panel.OpenButton.Text == "Settings" && enter.Text == "Enter" &&
            lobby.GetNode<Label>("%Description").Text == "Enter as a guest", "The lobby opens in English with a settings button");

        panel.OpenButton.EmitSignal(BaseButton.SignalName.Pressed);
        Check(panel.Visible && panel.LanguagePicker.Selected == 0 && panel.Slider("Master").Value == 100, "The panel opens on the current settings");
        // Rendered runs only: Godot --path . --resolution 1600x900 res://tests/SettingsChecks.tscn -- --capture
        if (OS.GetCmdlineUserArgs().Contains("--capture"))
        {
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using ViewportTexture texture = GetViewport().GetTexture();
            using Image image = texture.GetImage();
            image.SavePng("res://docs/images/lobby-settings.png");
        }
        panel.LanguagePicker.Select(1);
        panel.LanguagePicker.EmitSignal(OptionButton.SignalName.ItemSelected, 1);
        Check(_settings.Language == Lang.Korean && enter.Text == "입장" && panel.OpenButton.Text == "설정" &&
            lobby.GetNode<Label>("%Description").Text == "게스트 입장", "Choosing Korean changes the open screen at once");

        panel.Slider(SoundBank.World).Value = 40;
        var file = new ConfigFile();
        Check(Mathf.IsEqualApprox(_settings.Volume(SoundBank.World), .4f) && file.Load(Path) == Error.Ok &&
            Mathf.IsEqualApprox((float)file.GetValue("audio", SoundBank.World).AsDouble(), .4f), "A slider sets and saves its bus");

        panel.Close();
        Check(!panel.Visible, "The panel closes");
        panel.OpenButton.EmitSignal(BaseButton.SignalName.Pressed);
        Check(panel.LanguagePicker.Selected == 1 && panel.Slider(SoundBank.World).Value == 40, "Reopening shows the saved choices");
        panel.LanguagePicker.EmitSignal(OptionButton.SignalName.ItemSelected, 0);
        Check(enter.Text == "Enter", "Choosing English changes it back");
        lobby.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
