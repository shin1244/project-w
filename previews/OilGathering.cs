using Godot;
using System;
using System.Linq;
using System.Reflection;

// Real models and HUD with fixture messages; no server or economy simulation runs here.
public partial class OilGathering : Main
{
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public override async void _Ready()
    {
        try
        {
            typeof(Main).GetField("_net", Private).SetValue(this, GetNode<NetClient>("/root/Net"));
            typeof(Main).GetMethod("ConnectInput", Private).Invoke(this, null);
            Fog.Configure(Map);
            Receive($"MAP 2 {Map.MapHash}");
            Receive("WORLD_READY");
            Receive("WELCOME 7 1 COMMANDER");
            Receive("SIGHT UNIT 0 12");
            Receive("SIGHT BUILDING 7 12");
            Receive("SIGHT BUILDING 2 12");
            Receive("BUILDING 7 90001 1 -39 0 0");
            Receive("BUILDING 2 90002 1 -34 5 0");
            Receive("UNIT 0 90011 7 -36.4 0 1");
            Receive("UNIT 0 90012 7 -32 0 1");
            Receive("HP 90011 40 40");
            Receive("HP 90012 40 40");
            Receive("STATE 90011 GATHER 0 90001 0 0");
            Receive("STATE 90012 IDLE 5 0 0 2");
            Receive("POS 90012 -32 1");
            Receive("STOCK 0 260");
            Receive("STOCK 2 40");
            Receive("SUPPLY 12 28");
            Units.TryGetUnit(90012, out Unit carrying);
            Units.SelectSingle(carrying);
            Units.Camera.Size = 24;
            CameraNavigation.FocusGround(Units.Camera, new Vector3(-36, 0, 1));
            for (int i = 0; i < 8; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!OS.GetCmdlineUserArgs().Contains("--capture")) return;
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var picture = GetViewport().GetTexture().GetImage();
            if (picture.SavePng("res://docs/images/oil-gathering-preview.png") != Error.Ok)
                throw new Exception("Oil gathering preview capture failed");
            GD.Print("PASS: oil gathering visual preview captured (fixture messages)");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private void Receive(string message) => typeof(Main).GetMethod("OnMessage", Private).Invoke(this, new object[] { message });
}
