using Godot;
using System;
using System.Globalization;
using System.Reflection;

public partial class HealthBarChecks : Main
{
    private static readonly MethodInfo Handler = typeof(Main).GetMethod("OnMessage", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo Synced = typeof(MapWorld).GetField("<IsSynchronized>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

    public override async void _Ready()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            Map = new MapWorld();
            Synced.SetValue(Map, true);
            Units = new UnitManager
            {
                WorkerScene = GD.Load<PackedScene>("res://units/Worker.tscn"),
                KnightScene = GD.Load<PackedScene>("res://units/Knight.tscn"),
                ArcherScene = GD.Load<PackedScene>("res://units/Archer.tscn")
            };
            Buildings = new BuildingManager { TownHallScene = GD.Load<PackedScene>("res://buildings/TownHall.tscn") };
            AddChild(Units);
            AddChild(Buildings);
            var camera = new Camera3D { Position = new Vector3(0, 8, 12), Current = true };
            AddChild(camera);
            camera.LookAt(Vector3.Zero);
            for (int i = 0; i < 3; i++) Receive($"UNIT {i} {101 + i} 1 0 0");
            Receive("BUILDING 0 201 1 0 0 1.5707963");
            Unit worker = Units.GetNode<Unit>("Unit_101");
            Building hall = Buildings.GetNode<Building>("Building_201");
            Check(!worker.HealthBar.Visible && !hall.HealthBar.Visible, "No invented HP before snapshot");
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Receive("HP 101 25.5 50");
            Receive("HP 102 100 100");
            Receive("HP 103 12 60");
            Receive("HP 201 700 1000");
            Check(worker.HealthBar.Visible && Mathf.IsEqualApprox(worker.HealthBar.Ratio, .51f), "Invariant HP routes to worker");
            Check(hall.HealthBar.Visible && hall.HealthBar.Ratio == .7f, "Building uses same HP message");
            Check(worker.HealthBar.MouseFilter == Control.MouseFilterEnum.Ignore, "Health bar does not intercept selection");
            Synced.SetValue(Map, false);
            Receive("HP 101 1 50");
            Synced.SetValue(Map, true);
            Check(worker.HealthBar.CurrentHP == 25.5f, "HP respects world sync gate");
            foreach (string invalid in new[] { "HP 101 NaN 50", "HP 101 2 Infinity", "HP 101 -1 50", "HP 101 5 0", "HP 101 5 -1", "HP 101 5", "HP 101 5 50 extra", "HP broken 5 50", "HP 999 5 50" }) Receive(invalid);
            Check(worker.HealthBar.CurrentHP == 25.5f, "Invalid and unknown HP ignored");
            Vector2 oldPosition = worker.HealthBar.Position;
            Receive("POS 101 2 0");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(worker.HealthBar.Position != oldPosition, "Bar follows unit movement");
            Vector2 barSize = worker.HealthBar.Size;
            camera.Position *= 2;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(worker.HealthBar.Size == barSize, "Bar stays readable at different camera distances");
            Receive("HP 101 70 50");
            Check(worker.HealthBar.Ratio == 1, "Overheal cannot overflow bar");
            Receive("HP 101 0 50");
            Check(worker.HealthBar.Ratio == 0 && !worker.IsDying, "HP alone does not remove server entity");
            Receive("REMOVE 101");
            Receive("HP 101 50 50");
            Check(worker.IsDying && !worker.HealthBar.Visible && !worker.HealthBar.IsProcessing(), "Death hides bar and late HP cannot revive it");
            Receive("REMOVE 201");
            Check(!Buildings.HasNode("Building_201"), "Building bar removed with building");
            Units.Clear();
            Buildings.Clear();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(Units.GetChildCount() == 0, "Map reset leaves no orphan health bars");
            GD.Print("PASS: health dispatch, initial visibility, fractions, validation, camera/movement tracking, input pass-through, death/reset cleanup");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
        finally { CultureInfo.CurrentCulture = original; Map?.Free(); }
    }

    private void Receive(string message) => Handler.Invoke(this, new object[] { message });
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
