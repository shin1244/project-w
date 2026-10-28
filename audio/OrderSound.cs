using Godot;
using System;

// The game's one sound: a short acknowledgement when the local player gives an order that is not
// an attack (docs/sound.md). Like the interface voice it came from, it plays one at a time, so a
// burst of orders does not stack into noise.
public partial class OrderSound : Node
{
    private const float VolumeDb = -14, PitchJitter = .05f;
    // Headless runs have no audio output, so the same rules run without starting the player.
    private static readonly bool Silent = DisplayServer.GetName() == "headless";

    public static OrderSound Instance { get; private set; }
    // Raised whenever the acknowledgement starts.
    public event Action Played;

    private readonly RandomNumberGenerator _random = new();
    private AudioStreamPlayer _player;
    private AudioStream[] _streams;
    private ulong _until;

    public override void _EnterTree() => Instance = this;

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
        // Let go of the stream so no playback outlives the engine.
        if (_player == null) return;
        _player.Stop();
        _player.Stream = null;
    }

    public override void _Ready()
    {
        _random.Randomize();
        _streams = new[] { GD.Load<AudioStream>("res://audio/sfx/order-move-1.wav"), GD.Load<AudioStream>("res://audio/sfx/order-move-2.wav") };
        _player = new AudioStreamPlayer { Name = "Player", VolumeDb = VolumeDb };
        AddChild(_player);
    }

    // Orders that move units or put them to work, by protocol verb; attacks and production stay silent.
    public static bool Acknowledges(string command)
    {
        int space = command.IndexOf(' ');
        return (space < 0 ? command : command[..space]) is "MOVE" or "GATHER" or "RETURN" or "BUILD" or "CONSTRUCT" or
            "STOP" or "HOLD" or "RALLY" or "TUNNEL" or "PICKUP_CRATE" or "DELIVER_ITEM" or "NUGGET" or "TRIBUTE";
    }

    public static void For(string command)
    {
        if (Acknowledges(command)) Instance?.Play();
    }

    private void Play()
    {
        ulong now = Time.GetTicksMsec();
        if (now < _until) return;
        _player.Stop();
        _player.Stream = _streams[_random.RandiRange(0, _streams.Length - 1)];
        _player.PitchScale = 1 + _random.RandfRange(-PitchJitter, PitchJitter);
        if (!Silent) _player.Play();
        _until = now + (ulong)(_player.Stream.GetLength() / _player.PitchScale * 1000);
        Played?.Invoke();
    }
}
