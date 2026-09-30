using Godot;
using System;
using System.Text.Json;

// Uses the real map and resource nodes without connecting to a server.
public partial class ForestPreview : Main
{
    public override async void _Ready()
    {
        if (Map.SyncError != null)
        {
            GD.PushError(Map.SyncError);
            GetTree().Quit(1);
            return;
        }
        SyncStatus.Hide();
        // Show the actual starting structures while inspecting the map offline.
        using var data = JsonDocument.Parse(FileAccess.GetFileAsBytes(Map.MapPath));
        uint id = (uint)(Map.GridSize.X * Map.GridSize.Y + 1);
        uint side = 0;
        Buildings.SetLocalTeam(1);
        foreach (var point in data.RootElement.GetProperty("bases").EnumerateArray())
            Buildings.HandleSpawn(new[] { "BUILDING", "0", (id++).ToString(), (++side).ToString(),
                point[0].GetRawText(), point[1].GetRawText(), "0" });
        if (data.RootElement.TryGetProperty("towers", out var towers))
            foreach (var tower in towers.EnumerateArray())
                Buildings.HandleSpawn(new[] { "BUILDING", "1", (id++).ToString(), tower.GetProperty("side").GetRawText(),
                    tower.GetProperty("pos")[0].GetRawText(), tower.GetProperty("pos")[1].GetRawText(), "0" });
        GD.Print($"Forest preview: {Resources.GetChildCount()} trees");
        bool closeUp = Array.Exists(OS.GetCmdlineUserArgs(), arg => arg == "--capture-close");
        if (!closeUp && !Array.Exists(OS.GetCmdlineUserArgs(), arg => arg == "--capture")) return;
        GetNode<CanvasLayer>("SelectionUI").Hide();
        if (closeUp)
        {
            var camera = GetNode<Camera3D>("Camera3D");
            camera.Size = 22;
            camera.Position += new Vector3(10, 0, -3);
        }
        for (int i = 0; i < 4; i++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        string path = closeUp ? "res://docs/images/terrain-closeup.png" : "res://docs/images/forest-preview.png";
        Error result = GetViewport().GetTexture().GetImage().SavePng(path);
        GetTree().Quit(result == Error.Ok ? 0 : 1);
    }
}
