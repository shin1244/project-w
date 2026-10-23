using Godot;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

public partial class SupportUpdateChecks : Main
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    public override async void _Ready()
    {
        try
        {
            var net=GetNode<NetClient>("/root/Net");
            using var wire=new MemoryStream();
            using var writer=new StreamWriter(wire,new UTF8Encoding(false)) { AutoFlush=true };
            typeof(NetClient).GetField("_writer",Private).SetValue(net,writer);
            typeof(Main).GetField("_net",Private).SetValue(this,net);
            Invoke("ConnectInput");
            Units.CommandRequested+=command=>Invoke("SendCommand",command);
            Fog.Configure(Map);
            Begin();
            Receive("BUILDING 4 901 1 -6 -4 0");Receive("BUILDING 5 902 1 6 -4 0");
            Invoke("SelectBuilding",Buildings.GetChildren().OfType<Building>().Single(b=>b.BuildingId==901));
            var doctorSlot=Commands.GetNode<Button>("Slots/Slot9");
            long before=wire.Length;
            doctorSlot.EmitSignal(BaseButton.SignalName.Pressed);Units.RequestTrain(UnitCatalog.Medic);
            Check(doctorSlot.Text==UnitCatalog.Name(UnitCatalog.Medic) && doctorSlot.Disabled && wire.Length==before &&
                Buildings.SelectedBuilding.CanTrain(UnitCatalog.Medic),"The barracks offers the quack doctor but waits for its server terms");
            // Arbitrary terms: the button follows the wire, not balance constants.
            Receive("REQUIRES UNIT 3 4 5");Receive("TRAIN_DEF 3 4 12 5 1 7");Receive("STOCK 0 12");Receive("STOCK 2 5");Receive("SUPPLY 0 10");
            Check(!doctorSlot.Disabled && doctorSlot.TooltipText.Contains("기름 5") && doctorSlot.TooltipText.Contains("7초"),
                "A standing unlicensed lab and the server's terms open the quack doctor");
            doctorSlot.EmitSignal(BaseButton.SignalName.Pressed);
            Check(After(wire,before)=="TRAIN 901 3\n","The quack doctor trains at the selected barracks");

            Receive("UNIT 3 951 7 -2 2 1");Receive("UNIT 1 952 7 -1 2 1");Receive("UNIT 1 953 7 0 2 1");
            Units.TryGetUnit(951,out Unit doctor);Units.TryGetUnit(952,out Unit knight);
            Receive("HEALING 951 952");
            Check(doctor.HealingTargetId==952 && doctor.HealingTarget==knight,"HEALING names the doctor's patient");
            foreach (string malformed in new[]{"HEALING 951","HEALING 951 953 1","HEALING 951 -1","HEALING 951 951","HEALING x 953"}) Receive(malformed);
            Receive("HEALING 952 953");
            Check(doctor.HealingTargetId==952 && knight.HealingTargetId==0,"Malformed lines and non-healers never move a beam");
            Receive("MANA 951 37 200");
            Check(doctor.Mana is { Current: 37, Maximum: 200 },"The quack doctor keeps its mana for the information panel");
            Receive("HEALING 951 0");
            Check(doctor.HealingTargetId==0 && doctor.HealingTarget==null,"HEALING 0 stops the beam");

            ConfigureMinions();
            CheckLaneExperience(0,100);CheckLaneExperience(1,100);
            foreach (string malformed in new[]{"MINION_XP_RULE 0 5","MINION_XP_RULE 6 -1","MINION_XP_RULE 6 101","MINION_XP_RULE 6 10 extra","MINION_XP_RULE 99999999999 5"}) Receive(malformed);
            Receive("MINION_ADD_COST 3 100");Receive("STOCK 0 0");Receive("STOCK 3 200");
            foreach (string malformed in new[]{"MINION_ADD_COST 3 -1","MINION_ADD_COST 3 100 extra","MINION_ADD_COST x 100","MINION_ADD_COST 3 99999999999999999999"}) Receive(malformed);
            Check(Minions.AddCost==new MinionAddCostSnapshot(3,100) && Minions.MagicStones==200 && Minions.CanAffordAdd,
                "Malformed slot prices cannot replace the server's 마석 price");
            Minions.GetNode<Button>("Tabs/MinionTab").EmitSignal(BaseButton.SignalName.Pressed);await Flush();
            var stoneLabel=Minions.GetNode<Label>("Body/Content/Header/MagicStone");
            Check(stoneLabel.IsVisibleInTree() && stoneLabel.Text.Contains("마석") && stoneLabel.Text.Contains("200") &&
                Minions.GetGlobalRect().Encloses(stoneLabel.GetGlobalRect()),
                "The 마석 balance must remain readable inside the actual HUD");
            Minions.GetNode<Button>("Body/Content/Lanes/Rows/Lane0/Add").EmitSignal(BaseButton.SignalName.Pressed);await Flush();
            var option=Minions.GetNode<Button>("Picker/Content/Options/Option102");
            Check(!option.Disabled && option.FindChildren("Cost","Label",true,false).OfType<Label>().Single().Text.Contains("마석 100"),
                "The + picker shows the server's 마석 price instead of wood");
            before=wire.Length;option.EmitSignal(BaseButton.SignalName.Pressed);
            Check(After(wire,before)=="MINION_ADD 0 0 102\n" && Minions.IsRequestPending && Minions.MagicStones==200,
                "An add sends once and waits for the authoritative balance and lane");
            Receive("STOCK 3 100");Receive("MINION_LANE 0 1 7 100 100 100 101 101 101 102");await Flush();
            Check(!Minions.IsRequestPending && Minions.MagicStones==100,"The server's STOCK spends the 마석");
            CheckLaneExperience(0,95);CheckLaneExperience(1,100);
            var laneLabel=Minions.GetNode<Label>("Body/Content/Lanes/Rows/Lane0/Name");
            Check(laneLabel.Size.X>=80 && laneLabel.GetLineCount()==3 &&
                Minions.GetGlobalRect().Encloses(laneLabel.GetGlobalRect()),"Lane experience label fits inside HUD");
            Minions.GetNode<Button>("Body/Content/Lanes/Rows/Lane0/Slot0").EmitSignal(BaseButton.SignalName.Pressed);
            Check(!Minions.IsPickerOpen,"Slots never change type, so 마석 cannot buy a replacement either");
            Receive("STOCK 3 99");
            Minions.GetNode<Button>("Body/Content/Lanes/Rows/Lane0/Add").EmitSignal(BaseButton.SignalName.Pressed);
            Check(!Minions.CanAffordAdd && Minions.GetNode<Button>("Body/Content/Lanes/Rows/Lane0/Add").Disabled,
                "Less 마석 than one slot locks the + button");
            Receive("MINION_LANE 0 2 8 100 100 100 101 101 101 102 100");CheckLaneExperience(0,90);
            Receive("MINION_LANE 0 3 12 100 100 100 101 101 101 102 100 100 100 100 100");CheckLaneExperience(0,70);
            Receive("STOCK 3 200");Receive($"MAP 2 {Map.MapHash}");
            Check(Minions.AddCost==null && Minions.MagicStones==0 && !Minions.IsPickerOpen,"Map reset drops the old price, 마석 and picker");
            Begin();ConfigureMinions(false);
            Check(!Minions.GetNode<Label>("Body/Content/Lanes/Rows/Lane0/Name").Text.Contains("경험치"),
                "Map reset clears old experience rule; old servers do not display an assumed rate");
            Receive("MINION_XP_RULE 6 5");CheckLaneExperience(0,100);Receive("MINION_ADD_COST 3 100");Receive("STOCK 3 100");
            if (OS.GetCmdlineUserArgs().Contains("--capture-xp")) await Capture();
            Receive("WELCOME 7 1 HERO 202");
            Check(Minions.AddCost==null && Minions.MagicStones==0 && !Minions.Visible,"Hero role cannot keep commander 마석 UI");
            GD.Print("PASS: quack doctor at the barracks with its lab, HEALING and mana; 마석 + choice; lane XP 100/95/90/70%, rule validation, HUD fit and lifecycle reset");
            GetTree().Quit();
        }
        catch(Exception error) { GD.PushError(error.ToString());GetTree().Quit(1); }
    }
    private void Begin() { Receive($"MAP 2 {Map.MapHash}");Receive("WORLD_READY");Receive("WELCOME 7 1 COMMANDER"); }
    private void ConfigureMinions(bool experienceRule=true)
    {
        Receive("MINION_RULES 12 30");
        if (experienceRule) Receive("MINION_XP_RULE 6 5");
        Receive("MINION_OPTION 100 0 60 460");Receive("MINION_OPTION 101 0 80 480");Receive("MINION_OPTION 102 0 140 540");
        Receive("MINION_LANE 0 0 6 100 100 100 101 101 101");
        Receive("MINION_LANE 1 0 6 100 100 100 101 101 101");
    }
    private void CheckLaneExperience(int lane,int percent) => Check(
        Minions.GetNode<Label>($"Body/Content/Lanes/Rows/Lane{lane}/Name").Text.EndsWith($"경험치 {percent}%"),
        $"Lane {lane} must show server-based experience {percent}%");
    private async Task Capture()
    {
        Receive("SIGHT UNIT 1 8");Receive("SIGHT UNIT 202 10");
        Receive("SIGHT BUILDING 4 6");Receive("SIGHT BUILDING 5 6");
        Receive("BUILDING 4 901 1 -5 -4 0");Receive("BUILDING 5 902 1 5 -4 0");
        Receive("UNIT 1 701 7 -3 2 1");Receive("HP 701 600 1100");
        Receive("UNIT 202 702 8 3 2 1");Receive("HP 702 2200 3200");
        Receive("TICK 600");Receive("MINION_WAVE 900");Receive("SUPPLY 4 20");Receive("STOCK 0 500");
        Receive("MINION_LANE 0 1 7 100 100 100 101 101 101 102");
        Units.Camera.Size=23;CameraNavigation.FocusGround(Units.Camera,new Vector3(0,0,-1));
        Invoke("SelectBuilding",Buildings.GetChildren().OfType<Building>().Single(b=>b.BuildingId==901));
        Minions.GetNode<Button>("Tabs/MinionTab").EmitSignal(BaseButton.SignalName.Pressed);await Flush();
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        using Image picture=GetViewport().GetTexture().GetImage();
        Check(picture.SavePng("res://docs/images/minion-experience-update.png")==Error.Ok,"Save update preview");
    }
    private async Task Flush() { await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame); }
    private void Receive(string line)=>Invoke("OnMessage",line);
    private void Invoke(string name,params object[] args)=>typeof(Main).GetMethod(name,Private).Invoke(this,args);
    private static string After(MemoryStream wire,long before)=>Encoding.UTF8.GetString(wire.ToArray()[(int)before..]).Replace("\r\n","\n");
    private static void Check(bool condition,string message) { if(!condition) throw new InvalidOperationException(message); }
}
