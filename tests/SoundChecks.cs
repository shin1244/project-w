using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

// Which server messages make which sound (audio/SoundBank.cs), checked without a server.
public partial class SoundChecks : Main
{
    private static readonly MethodInfo Handler = typeof(Main).GetMethod("OnMessage", BindingFlags.Instance | BindingFlags.NonPublic);
    private readonly List<(string Name, float VolumeDb)> _played = new();

    private void Receive(string message) => Handler.Invoke(this, new object[] { message });

    public override async void _Ready()
    {
        try
        {
            Sfx sfx = Sfx.Instance;
            Check(sfx != null, "Sfx autoload is running");
            foreach (SoundDef def in SoundBank.All)
                for (int i = 1; i <= def.Variants; i++)
                    Check(ResourceLoader.Exists($"res://audio/sfx/{def.Name}-{i}.wav"), $"Imported sound {def.Name}-{i}");
            foreach (string bus in new[] { SoundBank.World, SoundBank.Interface, "Music" })
                Check(AudioServer.GetBusIndex(bus) >= 0, $"Audio bus {bus}");
            sfx.Played += (name, volume) => _played.Add((name, volume));

            Map = new MapWorld();
            typeof(MapWorld).GetField("<IsSynchronized>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(Map, true);
            Units = new UnitManager
            {
                WorkerScene = GD.Load<PackedScene>("res://units/Worker.tscn"),
                KnightScene = GD.Load<PackedScene>("res://units/Knight.tscn")
            };
            Buildings = new BuildingManager
            {
                TownHallScene = GD.Load<PackedScene>("res://buildings/TownHall.tscn"),
                TowerScene = GD.Load<PackedScene>("res://buildings/Tower.tscn"),
                Units = Units
            };
            Units.Buildings = Buildings;
            AddChild(Units);
            AddChild(Buildings);
            var camera = new Camera3D
            {
                Position = new Vector3(0, 25, 0), RotationDegrees = new Vector3(-90, 0, 0),
                Projection = Camera3D.ProjectionType.Orthogonal, Size = 28, Current = true
            };
            AddChild(camera);
            Units.Camera = camera;
            await Flush();

            // Join snapshot: nothing it reports is a live event.
            Receive("WELCOME 7 1 COMMANDER");
            Receive("UNIT 1 101 7 0 0 1");
            Receive("UNIT 1 102 7 1 0 1");
            Receive("STATE 101 ATTACK 0 900 5");
            Receive("BUILDING 0 501 1 6 6 0");
            Receive("CONSTRUCTION 501 40 7");
            Check(_played.Count == 0, "The join snapshot is silent");

            Receive("TICK 1");
            Receive("STATE 101 ATTACK 0 900 6");
            Check(Names() == "sword", "A new swing plays its weapon once");
            Receive("STATE 101 ATTACK 0 900 6");
            Receive("STATE 102 ATTACK 0 900 1"); // First state of this knight: no history to replay.
            Receive("STATE 102 ATTACK 0 900 2");
            Check(Names() == "", "A repeated swing or one inside the minimum gap stays silent");

            // Off screen: quieter just beyond the edge, unheard far away.
            await Gap();
            // Enemy knights 1.3 and 4 half-screens right of the centre.
            Vector2 view = GetViewport().GetVisibleRect().Size;
            float half = camera.Size / 2 * view.X / view.Y;
            string Near(float reach) => (reach * half).ToString(System.Globalization.CultureInfo.InvariantCulture);
            Receive($"UNIT 1 103 8 {Near(1.3f)} 0 2");
            Receive($"UNIT 1 104 8 {Near(4)} 0 2");
            Receive("STATE 103 ATTACK 0 900 1");
            Receive("STATE 104 ATTACK 0 900 1");
            Receive("STATE 103 ATTACK 0 900 2");
            Receive("STATE 104 ATTACK 0 900 2");
            SoundDef sword = SoundBank.All.First(def => def.Name == "sword");
            Check(_played.Count == 1 && _played[0].Name == "sword" && _played[0].VolumeDb < sword.VolumeDb - 1,
                "An attack just off screen is quieter, one far away is not heard");

            // Construction belongs to the player's own team; a cancelled site does not collapse.
            await Gap();
            Receive("CONSTRUCTION 501 100 7");
            Check(Names() == "build-complete", "Own building completion is announced");
            Receive("BUILDING 0 502 1 -6 6 0");
            Receive("CONSTRUCTION 502 0 7");
            Check(Names() == "build-start", "Own construction start is heard");
            Receive("BUILDING 0 503 2 -6 -6 0");
            Receive("CONSTRUCTION 503 0 9");
            Receive("CONSTRUCTION 503 100 9");
            Check(Names() == "", "Enemy construction is not announced");
            Receive("REMOVE 502");
            Check(Names() == "", "A cancelled construction site makes no collapse");
            Receive("REMOVE 501");
            Check(Names() == "building-collapse", "A finished building collapses audibly");

            // Trained units, deaths and siege transitions.
            await Gap();
            Receive("UNIT 1 105 7 2 2 1");
            Receive("UNIT 1 106 8 3 3 2");
            Check(Names() == "unit-ready", "Only this player's newly trained unit reports ready");
            Receive("REMOVE 101");
            Check(Names() == "death-soldier", "A soldier's death");
            Receive("UNIT 5 107 7 -3 -3 1");
            Receive("SIEGE_STATE 107 MOBILE 0 0");
            Receive("SIEGE_STATE 107 DEPLOYING 1 61");
            Receive("SIEGE_STATE 107 DEPLOYING 1 61");
            Check(Names() == "artillery-deploy", "Deploying begins once with its own sound");
            Receive("REMOVE 107");
            Check(Names() == "death-machine", "A machine's death");
            Receive("ERR 자원이 부족합니다");
            Check(Names() == "ui-error", "A refused request buzzes");

            // One sound plays at most MaxVoices times at once, however often it is asked for.
            await Gap();
            SoundDef rifle = SoundBank.All.First(def => def.Name == "rifle");
            for (int i = 0; i < rifle.MaxVoices + 2; i++)
            {
                Sfx.At("rifle", Vector3.Zero);
                await ToSignal(GetTree().CreateTimer(rifle.MinGap + .02), SceneTreeTimer.SignalName.Timeout);
            }
            Check(_played.Count(sound => sound.Name == "rifle") == rifle.MaxVoices, "A sound is capped at its voice count");

            Check(SoundBank.Order("MOVE 1 2 101") == "order-move" && SoundBank.Order("ATTACK_MOVE 1 2 101") == "order-attack" &&
                SoundBank.Order("TRAIN 4 1") == null, "Orders map to acknowledgements by verb");
            Check(SoundBank.Swing(UnitCatalog.SteamArtillery, UnitActivity.Attack) == null,
                "Steam artillery sounds come from its shells, not its swing");

            Buildings.Clear();
            await Flush();
            GD.Print("PASS: sounds follow live swings, screen distance, own construction, training, deaths, sieges and errors");
            Callable.From(FinishChecks).CallDeferred();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
        finally { Map?.Free(); }
    }

    // Names played since the last call, in order.
    private string Names()
    {
        string names = string.Join(",", _played.Select(sound => sound.Name));
        _played.Clear();
        return names;
    }

    // Longer than any sound's minimum gap.
    private async Task Gap()
    {
        await ToSignal(GetTree().CreateTimer(.6), SceneTreeTimer.SignalName.Timeout);
        _played.Clear();
    }

    private void FinishChecks()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GetTree().Quit();
    }

    private async Task Flush()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
