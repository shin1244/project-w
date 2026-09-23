using Godot;
using System;

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
        GD.Print($"Forest preview: {Resources.GetChildCount()} trees");
        bool closeUp = Array.Exists(OS.GetCmdlineUserArgs(), arg => arg == "--capture-close");
        if (!closeUp && !Array.Exists(OS.GetCmdlineUserArgs(), arg => arg == "--capture")) return;
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
