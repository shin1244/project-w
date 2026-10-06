using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

// 실제 게임 HUD를 사용하는 서버 없는 편성 미리보기. 가격/편성 응답만 로컬에서 흉내 낸다.
public partial class MinionFormationPreview : Main
{
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly uint[][] _lanes = {
        new uint[] { 100, 100, 102, 101, 101, 101 },
        new uint[] { 100, 100, 100, 101, 101, 101, 102 }
    };
    private readonly int[] _revisions = new int[2];
    private int _wood = 1200;

    public override async void _Ready()
    {
        try
        {
            typeof(Main).GetField("_net", Private).SetValue(this, GetNode<NetClient>("/root/Net"));
            typeof(Main).GetMethod("ConnectInput", Private).Invoke(this, null);
            Map.AcceptMap(new[] { "MAP", "2", Map.MapHash });
            Map.CompleteSync();
            Fog.Configure(Map);
            Receive("WELCOME 7 1 COMMANDER");
            Receive("MINION_RULES 12 30");
            Receive("MINION_OPTION 100 0 60 460");
            Receive("MINION_OPTION 101 0 80 480");
            Receive("MINION_OPTION 102 0 140 540");
            for (int lane = 0; lane < _lanes.Length; lane++) SendLane(lane);
            Receive($"STOCK 0 {_wood}");
            Receive("SUPPLY 8 20");
            Receive("TICK 390");
            Receive("MINION_WAVE 900");
            Receive("UNIT 0 1800 7 -50 -5 1");
            Receive("UNIT 100 1801 0 -48 2 1");
            Receive("UNIT 101 1802 0 -48 4 1");
            Receive("UNIT 102 1803 0 -46 3 1");
            Receive("HP 1803 50 50");
            Receive("BUILDING 0 1900 1 -65 0 0");
            Units.Camera.Size = 38;
            CameraNavigation.FocusGround(Units.Camera, new Vector3(-50, 0, 0));
            SyncStatus.Hide();
            Minions.CommandRequested += PurchasePreview;
            await Frames();
            Minions.GetNode<Button>("Tabs/MinionTab").EmitSignal(BaseButton.SignalName.Pressed);
            await Frames();
            if (OS.GetCmdlineUserArgs().Contains("--picker"))
            {
                Minions.GetNode<Button>("Body/Content/Lanes/Rows/Lane0/Slot0").EmitSignal(BaseButton.SignalName.Pressed);
                await Frames();
            }
            if (OS.GetCmdlineUserArgs().Contains("--capture"))
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using Image capture = GetViewport().GetTexture().GetImage();
                string path = "res://.godot/minion-formation-preview.png";
                Error saved = capture.SavePng(ProjectSettings.GlobalizePath(path));
                GD.Print($"Minion formation preview: {saved} {ProjectSettings.GlobalizePath(path)}");
                GetTree().Quit(saved == Error.Ok ? 0 : 1);
            }
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private void PurchasePreview(string command)
    {
        string[] parts = command.Split(' ');
        int lane = int.Parse(parts[1]);
        uint type = uint.Parse(parts[^1]);
        int price = type switch { 100 => 60, 101 => 80, _ => 140 };
        if (parts[0] == "MINION_ADD") price += 400;
        _wood -= price;
        if (parts[0] == "MINION_ADD") _lanes[lane] = _lanes[lane].Append(type).ToArray();
        else _lanes[lane][int.Parse(parts[3])] = type;
        _revisions[lane]++;
        Receive($"STOCK 0 {_wood}");
        SendLane(lane);
    }

    private void SendLane(int lane) => Receive($"MINION_LANE {lane} {_revisions[lane]} {_lanes[lane].Length} {string.Join(' ', _lanes[lane])}");
    private void Receive(string line) => typeof(Main).GetMethod("OnMessage", Private).Invoke(this, new object[] { line });
    private async Task Frames()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
}
