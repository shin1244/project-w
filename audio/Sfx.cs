using Godot;
using System;
using System.Collections.Generic;

// Plays the synthesized effects in SoundBank. Like StarCraft, a world sound is heard only near
// the screen: full volume in view, fading out within HearRange half-screens beyond the edge, and
// panned by where it is on screen. Every sound has a voice cap and a minimum gap, and when all
// voices are busy a sound may only cut off one of equal or lower priority, so a large fight
// stays punchy instead of turning to mush.
public partial class Sfx : Node
{
    private const int WorldVoices = 24, InterfaceVoices = 6;
    private const float HearRange = 2.2f;
    private const float QuietestAudible = .03f;
    // Headless runs have no audio output, and their dummy driver never drains stopped
    // playbacks, so the same rules run without starting the players.
    private static readonly bool Silent = DisplayServer.GetName() == "headless";

    public static Sfx Instance { get; private set; }
    // Name and volume of every sound that started.
    public event Action<string, float> Played;

    // A voice counts as busy until its sound would have ended, whatever the audio thread is doing.
    private sealed class Voice
    {
        public Node Player;
        public string Name;
        public int Priority;
        public ulong Started, Until;
        public bool Playing => Time.GetTicksMsec() < Until;
    }

    private readonly Dictionary<string, (SoundDef Def, AudioStream[] Streams)> _sounds = new();
    private readonly Dictionary<string, ulong> _lastStart = new();
    private readonly List<Voice> _world = new(), _interface = new();
    private readonly RandomNumberGenerator _random = new();

    public override void _EnterTree() => Instance = this;

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
        GetTree().NodeAdded -= OnNodeAdded;
        // Stop and let go of every stream so no playback outlives the engine.
        foreach (Voice voice in _world) Release(voice.Player);
        foreach (Voice voice in _interface) Release(voice.Player);
        _sounds.Clear();
    }

    public override void _Ready()
    {
        _random.Randomize();
        foreach (SoundDef def in SoundBank.All)
        {
            var streams = new AudioStream[def.Variants];
            for (int i = 0; i < def.Variants; i++)
                streams[i] = GD.Load<AudioStream>($"res://audio/sfx/{def.Name}-{i + 1}.wav");
            _sounds[def.Name] = (def, streams);
        }
        for (int i = 0; i < WorldVoices; i++)
        {
            var player = new AudioStreamPlayer3D
            {
                Name = $"World{i}",
                Bus = SoundBank.World,
                AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.Disabled,
                PanningStrength = .7f,
                DopplerTracking = AudioStreamPlayer3D.DopplerTrackingEnum.Disabled
            };
            AddChild(player);
            _world.Add(new Voice { Player = player });
        }
        for (int i = 0; i < InterfaceVoices; i++)
        {
            var player = new AudioStreamPlayer { Name = $"Interface{i}", Bus = SoundBank.Interface };
            AddChild(player);
            _interface.Add(new Voice { Player = player });
        }
        // Every button clicks, including ones made in code after this point.
        GetTree().NodeAdded += OnNodeAdded;
    }

    public static void At(string name, Vector3 position) => Instance?.PlayWorld(name, position);

    public static void Interface(string name) => Instance?.PlayInterface(name);

    // 1 inside the view, falling to 0 at HearRange half-screens from the centre.
    public float Audibility(Vector3 position)
    {
        Viewport viewport = GetViewport();
        Camera3D camera = viewport?.GetCamera3D();
        if (camera == null || camera.IsPositionBehind(position)) return 0;
        Vector2 half = viewport.GetVisibleRect().Size / 2;
        if (half.X <= 0 || half.Y <= 0) return 0;
        Vector2 offset = (camera.UnprojectPosition(position) - half) / half;
        float reach = Mathf.Max(Mathf.Abs(offset.X), Mathf.Abs(offset.Y));
        return reach <= 1 ? 1 : Mathf.Clamp(1 - (reach - 1) / (HearRange - 1), 0, 1);
    }

    private void PlayWorld(string name, Vector3 position)
    {
        if (!_sounds.TryGetValue(name, out var sound)) return;
        float audible = Audibility(position);
        if (audible < QuietestAudible || !Admit(name, sound.Def, _world)) return;
        Voice voice = Claim(_world, sound.Def.Priority);
        if (voice == null) return;
        var player = (AudioStreamPlayer3D)voice.Player;
        player.Stop();
        player.GlobalPosition = position;
        player.Stream = Pick(sound.Streams);
        player.VolumeDb = sound.Def.VolumeDb + Mathf.LinearToDb(audible);
        player.PitchScale = Pitch(sound.Def);
        if (!Silent) player.Play();
        Track(voice, name, sound.Def, player.Stream, player.PitchScale, player.VolumeDb);
    }

    private void PlayInterface(string name)
    {
        if (!_sounds.TryGetValue(name, out var sound) || !Admit(name, sound.Def, _interface)) return;
        Voice voice = Claim(_interface, sound.Def.Priority);
        if (voice == null) return;
        var player = (AudioStreamPlayer)voice.Player;
        player.Stop();
        player.Stream = Pick(sound.Streams);
        player.VolumeDb = sound.Def.VolumeDb;
        player.PitchScale = Pitch(sound.Def);
        if (!Silent) player.Play();
        Track(voice, name, sound.Def, player.Stream, player.PitchScale, player.VolumeDb);
    }

    private bool Admit(string name, SoundDef def, List<Voice> pool)
    {
        ulong now = Time.GetTicksMsec();
        if (_lastStart.TryGetValue(name, out ulong last) && now - last < (ulong)(def.MinGap * 1000)) return false;
        int playing = 0;
        foreach (Voice voice in pool)
            if (voice.Name == name && voice.Playing) playing++;
        return playing < def.MaxVoices;
    }

    // A free voice, else the oldest one this sound outranks or equals.
    private static Voice Claim(List<Voice> pool, int priority)
    {
        Voice oldest = null;
        foreach (Voice voice in pool)
        {
            if (!voice.Playing) return voice;
            if (voice.Priority <= priority && (oldest == null || voice.Started < oldest.Started)) oldest = voice;
        }
        return oldest;
    }

    private void Track(Voice voice, string name, SoundDef def, AudioStream stream, float pitch, float volumeDb)
    {
        voice.Name = name;
        voice.Priority = def.Priority;
        voice.Started = Time.GetTicksMsec();
        voice.Until = voice.Started + (ulong)(stream.GetLength() / pitch * 1000);
        _lastStart[name] = voice.Started;
        Played?.Invoke(name, volumeDb);
    }

    private static void Release(Node node)
    {
        if (node is AudioStreamPlayer3D world) { world.Stop(); world.Stream = null; }
        else if (node is AudioStreamPlayer flat) { flat.Stop(); flat.Stream = null; }
    }

    private AudioStream Pick(AudioStream[] streams) => streams[_random.RandiRange(0, streams.Length - 1)];

    private float Pitch(SoundDef def) => 1 + _random.RandfRange(-def.PitchJitter, def.PitchJitter);

    // A button moved to another parent enters the tree again; connect it only once.
    private void OnNodeAdded(Node node)
    {
        if (node is not BaseButton button) return;
        var click = new Callable(this, MethodName.OnButtonPressed);
        if (!button.IsConnected(BaseButton.SignalName.Pressed, click)) button.Connect(BaseButton.SignalName.Pressed, click);
    }

    private void OnButtonPressed() => PlayInterface("ui-click");
}
