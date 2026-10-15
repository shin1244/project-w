using Godot;
using System;
using System.Linq;
using System.Reflection;

// Visual fixture using the real HUD and server messages; this is not a live match.
public partial class CompactHud : Main
{
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public override async void _Ready()
    {
        try
        {
            typeof(Main).GetField("_net", Private).SetValue(this, GetNode<NetClient>("/root/Net"));
            typeof(Main).GetMethod("ConnectInput", Private).Invoke(this, null);
            Fog.Configure(Map);
            Receive($"MAP 2 {Map.MapHash}"); Receive("WORLD_READY"); Receive("WELCOME 7 1 HERO 201");
            Receive("SIGHT UNIT 201 10"); Receive("SIGHT UNIT 1 8"); Receive("SIGHT UNIT 2 9");
            Receive("UNIT 201 701 7 -2 0 1"); Receive("HP 701 730 980"); Receive("MANA 701 180 360");
            Receive("STATS 701 68 0.8 1.2 5.5 10"); Receive("STATE 701 IDLE 0 0 0");
            Receive("ABILITY UNIT 201 0 golem-slam SELF 0 10 0");
            Receive("ABILITY UNIT 201 1 golem-shell SELF 0 13 0");
            Receive("ABILITY UNIT 201 2 golem-charge ENEMY 7 12 1");
            Receive("ABILITY UNIT 201 3 golem-up SELF 0 40 0");
            for (int i = 0; i < 4; i++) Receive($"MANA_COST 201 {i} {(i == 3 ? 90 : 40)}");
            Receive("HERO_SKILLS 201 7 0 1 2 1 1 0 1");
            Receive("TEAM_LEVEL 1 7"); Receive("TEAM_LEVEL 2 8");
            Receive("TEAM_EXP 1 7 150 500 160000"); Receive("EXP 7 150 500"); Receive("TICK 10320");
            Receive("TRIBUTE 1 ACTIVE 0 0 0 701 1 10230 10440 0 0");
            for (int i = 0; i < 6; i++)
            {
                uint id = (uint)(710 + i);
                Receive($"UNIT {(i % 2 == 0 ? 1 : 2)} {id} 8 {3 + i % 3 * 2} {-4 + i / 3 * 3} 2");
                Receive($"HP {id} 240 300");
            }
            for (int i = 0; i < 4; i++)
            {
                uint id = (uint)(720 + i);
                Receive($"UNIT {(i % 2 == 0 ? 1 : 2)} {id} 9 {-6 + i % 2 * 2} {-4 + i / 2 * 4} 1");
                Receive($"HP {id} 280 300");
            }
            Units.Camera.Size = 26;
            CameraNavigation.FocusGround(Units.Camera, Vector3.Zero);
            for (int i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (OS.GetCmdlineUserArgs().Contains("--capture"))
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using var picture = GetViewport().GetTexture().GetImage();
                if (picture.SavePng("res://.godot/compact-hud.png") != Error.Ok) throw new Exception("HUD preview capture failed");
                GD.Print("PASS: compact HUD preview captured (fixture messages)");
                GetTree().Quit();
            }
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private void Receive(string message) => typeof(Main).GetMethod("OnMessage", Private).Invoke(this, new object[] { message });
}
