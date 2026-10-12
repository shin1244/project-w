using Godot;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

// Exercise the real Main dispatch and viewport input, without a network server.
public partial class RecoveryClientChecks : Main
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private const string CatalogLine="ITEM_CATALOG [{\"id\":1,\"key\":\"sword\",\"name\":\"강철 장검\",\"description\":\"공격력 +40\",\"cost\":400},{\"id\":3,\"key\":\"boots\",\"name\":\"진군의 장화\",\"description\":\"이동 속도 +0.6\",\"cost\":300},{\"id\":7,\"key\":\"greatsword\",\"name\":\"명장의 대검\",\"description\":\"공격력 +120\",\"cost\":600,\"from\":1}]";
    public override async void _Ready()
    {
        try
        {
            CheckParsers();
            var net=GetNode<NetClient>("/root/Net");
            using var wire=new MemoryStream();
            using var writer=new StreamWriter(wire,new UTF8Encoding(false)) { AutoFlush=true };
            typeof(NetClient).GetField("_writer",Private).SetValue(net,writer);
            typeof(Main).GetField("_net",Private).SetValue(this,net);
            InvokeMain("ConnectInput");
            Units.CommandRequested+=command=>InvokeMain("SendCommand",command);
            Fog.Configure(Map);
            Begin();
            CheckSkillProgression(wire);
            CheckWorkerSkillTargets(wire);
            CheckItemTiers(wire);
            Begin();
            Check(LocalHeroType==202 && Units.TryGetUnit(701,out _) && Skills.HeroUnitId==701 &&
                Skills.Definition(0)?.Id=="marshal-shot", "Marshal must be spawned and controllable with server abilities");
            Receive(CatalogLine);
            Receive("HERO_ITEMS 400 1 0 0 0 0 0 0 0 0 0");
            long before=wire.Length;Shop.RequestBuy(1);
            Check(After(wire,before)=="BUY_ITEM 1\n" && Shop.Inventory.Gold==400 && Shop.Inventory.Items[0]==0,
                "Purchase sends one request, without optimistic gold or equipment changes");
            Receive("HERO_ITEMS 0 1 0 1 0 0 0 0 0 0 0");
            Check(Shop.Inventory.Items[0]==1 && Shop.BuyBlockReason(1)!=null && Shop.BuyBlockReason(7)!=null,
                "Server equipment, duplicate family and team tier restrictions");
            before=wire.Length;Shop.RequestBuy(3);Check(wire.Length==before,"Insufficient gold is blocked");
            Shop.RequestSell(0);Check(After(wire,before)=="SELL_ITEM 0\n","Selling uses zero-based slot");
            Receive("HERO_ITEMS 900 0 0 1 0 0 0 0 0 0 0 2");
            before=wire.Length;Shop.RequestBuy(7);Shop.RequestSell(0);
            Check(After(wire,before)=="BUY_ITEM 7\n","Remote upgrade works; remote selling is blocked");
            Receive("HERO_ITEMS 300 0 1 7 1 0 0 0 0 0 0");
            Receive("ITEM_CRATE 900 7 7 0 0 0");await Flush();
            Check(Crates.Count==1 && Crates[900].Available,"Crate appears only from snapshot");
            before=wire.Length;RightClick(Crates[900]);await Flush();
            Check(After(wire,before)=="PICKUP_CRATE 701 900\n","Actual viewport right click picks up own crate once");
            Receive("ITEM_CRATE 901 8 3 0 3 0");
            before=wire.Length;Units.RequestContextOrder(Crates[901],new Vector3(3,0,0));
            Check(wire.Length==before,"Hero cannot pick up another player's equipment");
            InvokeMain("RequestRecall");Check(After(wire,before)=="RECALL 701\n","Recall command carries own hero ID");
            Receive("RECALL 701 300 540");Receive("TICK 420");
            Check(Shop.RecallText.Contains("4.0"),"Recall remaining time follows server tick");
            Receive("TICK 540");Check(Shop.RecallText.Contains("확인"),"Recall completion is not predicted");
            Receive("RECALL 701 0 0");Check(Shop.RecallText=="귀환 [B]","Server cancels recall channel");
            Receive("MARSHAL 701 660 1440");
            Check(Skills.StatusText.Contains("진군") && Skills.StatusText.Contains("부대 소집"),"Marshal persistent effects use server end ticks");
            Receive("TICK 660");Check(!Skills.StatusText.Contains("진군") && Skills.StatusText.Contains("부대 소집"),"March expires independently of rally");
            Receive("HIDE 701");Check(!Skills.HeroUnitId.HasValue && Skills.StatusText=="영웅 대기 중","Hidden hero drops stale effects and controls");
            before=wire.Length;InvokeMain("RequestRecall");Check(wire.Length==before,"No recall without own hero");
            Receive("WELCOME 7 1 COMMANDER");Receive("UNIT 0 710 7 -5 -3 1");Receive("HP 710 500 500");
            Units.SelectSingle(Units.LiveUnits.Single(u=>u.UnitId==710));await Flush();
            before=wire.Length;RightClick(Crates[900]);await Flush();
            Check(After(wire,before)=="DELIVER_ITEM 900 710\n","Selected worker sends one delivery command via viewport");
            Receive("ITEM_CRATE 900 7 7 710 -3 -2");
            before=wire.Length;Units.RequestContextOrder(Crates[900],Vector3.Zero);
            Check(!Crates[900].Available && wire.Length==before,"Carried crates cannot be assigned again");
            var stale=Crates[900];Receive("ITEM_CRATE_REMOVE 900");
            Units.RequestContextOrder(stale,Vector3.Zero);
            Check(!Crates.ContainsKey(900) && wire.Length==before,"Removed crate cannot issue commands through stale references");
            Receive($"MAP 2 {Map.MapHash}");Check(Crates.Count==0 && !Shop.Visible && Shop.Inventory==null,"Map reset clears economy UI and crates");
            Receive("ITEM_CRATE 999 7 1 0 0 0");Check(Crates.Count==0,"Unsynchronized snapshots are ignored");
            Begin();Receive(CatalogLine);Receive("HERO_ITEMS 900 0 1 1 0 3 1 0 0 0 0");Receive("ITEM_CRATE 902 7 3 0 0 0");
            InvokeMain("OnConnectionClosed","recovery check");Check(Crates.Count==0 && !Shop.Visible && Shop.Catalog.Count==0,"Disconnect clears all new state");
            typeof(NetClient).GetField("_writer",Private).SetValue(net,writer);
            Begin();Receive(CatalogLine);Receive("HERO_ITEMS 900 0 1 1 0 3 1 0 0 0 0");Receive("ITEM_CRATE 903 7 3 0 0 0");
            if (OS.GetCmdlineUserArgs().Contains("--capture")) await CapturePreview();
            Receive("MATCH_END 1 BASE_DESTROYED 120");before=wire.Length;
            Shop.RequestBuy(7);InvokeMain("RequestRecall");
            Check(Crates.Count==0 && !Shop.Visible && wire.Length==before,"Match completion freezes purchases and recall");
            GD.Print("PASS: recovered marshal, catalog/equipment, purchase/upgrade/sell, crate viewport pickup/delivery, recall, effects and lifecycle cleanup");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString());GetTree().Quit(1); }
    }
    private void CheckWorkerSkillTargets(MemoryStream wire)
    {
        Begin();
        Receive("UNIT 0 711 8 0 0 2");Receive("HP 711 500 500");
        Receive("UNIT 0 712 9 -2 2 1");Receive("HP 712 500 500");
        Receive("UNIT 1 713 8 1 2 2");Receive("HP 713 650 650");
        Unit enemy=Units.LiveUnits.Single(u=>u.UnitId==711), ally=Units.LiveUnits.Single(u=>u.UnitId==712);
        long sent=wire.Length;
        Check(!Skills.AcceptsTarget(0,enemy),"Enemy worker cannot be selected for hero Q");
        Units.RequestHeroSkill(Skills.Definition(0).Value,701,new SkillInput(0,enemy));
        Check(wire.Length==sent,"Direct hero skill request cannot bypass enemy worker exclusion");
        Receive("ABILITY UNIT 202 1 blessing ALLY 9 12 0");
        Check(!Skills.AcceptsTarget(1,ally),"Ally worker cannot be selected for hero buffs");
        Units.RequestHeroSkill(Skills.Definition(1).Value,701,new SkillInput(1,ally));
        Check(wire.Length==sent,"Direct hero skill request cannot bypass ally worker exclusion");
        Unit soldier=Units.LiveUnits.Single(u=>u.UnitId==713);
        Check(Skills.AcceptsTarget(0,soldier),"Military units remain selectable hero skill targets");
        Units.RequestHeroSkill(Skills.Definition(0).Value,701,new SkillInput(0,soldier));
        Check(After(wire,sent)=="SKILL 0 713\n","Hero skill commands still target soldiers normally");
        sent=wire.Length;
        Units.RequestContextOrder(enemy,enemy.GlobalPosition);
        Check(After(wire,sent)=="ATTACK 711 701\n","Workers remain valid hero basic attack targets");
        Check(SkillDescriptions.Passive(UnitCatalog.HeroMarshal).Contains("일꾼") &&
            SkillDescriptions.Active(UnitCatalog.HeroTest,3,"wolf-devour",rank:1).Contains("일꾼"),
            "Hero descriptions disclose worker exclusion");
    }
    private void CheckItemTiers(MemoryStream wire)
    {
        Begin();
        string catalog = CatalogLine[..^1] + ",{\"id\":13,\"key\":\"emperor_sword\",\"name\":\"황제의 대검\",\"description\":\"공격력 +200\",\"cost\":900,\"from\":7,\"tier\":3}]";
        Receive(catalog);
        Check(Shop.Catalog.Count == 4 && Shop.Catalog[13].Tier == 3 && Shop.Catalog[7].Tier == 2,
            "Catalog parses three tiers and infers legacy item tiers from ancestry");
        Receive("HERO_ITEMS 1000 1 1 7 0 0 0 0 0 0 0 2");
        long sent = wire.Length;
        Shop.RequestBuy(13);
        Check(wire.Length == sent && Shop.BuyBlockReason(13).Contains("3티어"), "Forge cannot bypass tier-three town hall requirement");
        Receive("HERO_ITEMS 1000 1 0 7 0 0 0 0 0 0 0 3");
        Shop.RequestBuy(13);
        Check(After(wire,sent) == "BUY_ITEM 13\n" && Shop.Inventory.Items[0] == 7 && Shop.Inventory.Gold == 1000,
            "Tier-three purchase needs no forge and awaits server inventory confirmation");
        Receive("HERO_ITEMS 1000 1 0 13 0 0 0 0 0 0 0 3");
        Check(Shop.BuyBlockReason(1) == "보유한 계열" && Shop.BuyBlockReason(7) != null &&
            ((Button)Shop.FindChild("Slot0",true,false)).TooltipText.Contains("950 G"),
            "Tier-three ownership blocks base duplicates and sale value includes both ancestors");
        Receive("HERO_ITEMS 1000 1 0 7 1 0 0 0 0 0 0 3");
        Check(Shop.BuyBlockReason(13).Contains("수령"), "Pending tier-two delivery cannot be upgraded");
        var inventory = Shop.Inventory;
        Receive("HERO_ITEMS 1000 1 0 7 0 0 0 0 0 0 0 4");
        Check(Shop.Inventory == inventory, "Invalid appended team tier cannot replace inventory");
        Receive("HERO_ITEMS 1000 1 1 7 0 0 0 0 0 0 0");
        Check(Shop.Inventory.TeamTier == 1 && Shop.BuyBlockReason(13) != null, "Legacy inventory is parsed conservatively without unlocking tiers");
        Check(!HeroItemDefinition.TryParseCatalog(catalog.Replace("\"from\":7", "\"from\":13"),out _) &&
            !HeroItemDefinition.TryParseCatalog(catalog.Replace("\"tier\":3", "\"tier\":2"),out _),
            "Cyclic ancestry and mismatched tier catalogs are rejected");
    }
    private void CheckSkillProgression(MemoryStream wire)
    {
        Receive("HERO_SKILLS 202 1 0 0 0 0 0 0 0");
        Check(Skills.CanUse(0) && Skills.CanUse(1) && Skills.CanUse(2) && !Skills.CanUse(3),"QWE available, R locked at level 1");
        long start=wire.Length;Skills.RequestUpgrade(3);Check(wire.Length==start,"Level 1 cannot learn R");
        Receive("HERO_SKILLS 202 5 4 0 0 0 0 0 0");
        Check(Skills.CanUpgrade(3),"R can be learned at level 5");
        using var learn=new InputEventKey { Pressed=true,Keycode=Key.R,CtrlPressed=true };
        Check(Skills.TryHandleShortcut(learn) && After(wire,start)=="UPGRADE_SKILL 3\n","Ctrl+R sends exactly one learn command");
        Check(!Skills.CanUse(3) && Skills.Progress?.Points==4,"UI waits for server confirmation");
        start=wire.Length;learn.Echo=true;Skills.TryHandleShortcut(learn);Check(wire.Length==start,"Repeat key does not spend twice");
        Receive("HERO_SKILLS 202 5 3 1 0 0 0 0 0");
        var ultimateButton=Skills.GetNode<Button>("Content/Slots/RSlot/Upgrade");
        for (int level=5;level<10;level++)
        {
            Receive($"HERO_SKILLS 202 {level} {level-2} 1 0 0 0 0 0");
            Check(Skills.CanUse(3) && !Skills.CanUpgrade(3) && ultimateButton.Disabled &&
                ultimateButton.TooltipText.Contains("10레벨"),"Learned R usable but upgrade locked before level 10");
            start=wire.Length;learn.Echo=false;Skills.TryHandleShortcut(learn);ultimateButton.EmitSignal(Button.SignalName.Pressed);
            Check(wire.Length==start,"Locked R upgrade sends no command by shortcut or button");
        }
        Receive("HERO_SKILLS 202 10 8 1 0 0 0 0 0");
        Check(Skills.CanUpgrade(3) && !ultimateButton.Disabled,"R upgrade unlocks at level 10");
        start=wire.Length;Skills.TryHandleShortcut(learn);
        Check(After(wire,start)=="UPGRADE_SKILL 3\n","Ctrl+R upgrades at level 10");
        Receive("HERO_SKILLS 202 10 7 1 0 0 0 1 0");
        Check(!Skills.CanUpgrade(3),"R upgrade cap is one");
        var progress=Skills.Progress;
        Receive("HERO_SKILLS 202 5 1 1 0 0 0 2 0");Receive("HERO_SKILLS 202 5 99 1 0 0 0 1 0");Receive("HERO_SKILLS 200 5 4 0 0 0 0 0 0");
        Check(Skills.Progress==progress,"Malformed and foreign progression rejected");
        Receive("HERO_SKILLS 202 10 1 1 4 2 0 1 0");
        Check(!Skills.CanUpgrade(0) && Skills.CanUpgrade(1),"Basic skill caps at four");
        var button=Skills.GetNode<Button>("Content/Slots/WSlot/Upgrade");
        start=wire.Length;button.EmitSignal(Button.SignalName.Pressed);
        Check(After(wire,start)=="UPGRADE_SKILL 1\n","Upgrade button sends the same authoritative command");
        Receive("SKILL_ACTIVE 701 1 1");Check(!Skills.CanUpgrade(1),"Running skill cannot be upgraded");
        Receive("RESPAWN 900");Receive("REMOVE 701");Receive("UNIT 202 702 7 -4 0 1");Receive("HP 702 3200 3200");
        Check(Skills.Progress?.Q==4 && Skills.Progress?.R==1 && Skills.CanUse(3),"Respawn preserves learned ranks");
        Receive("WELCOME 7 1 HERO 200");Receive("UNIT 200 802 7 -4 0 1");Receive("HP 802 3000 3000");
        Receive("ABILITY UNIT 200 3 wolf-devour ALLY 8 40 0");Receive("HERO_SKILLS 200 10 7 1 0 0 0 1 0");
        Receive("HERO_ABILITY UNIT 200 3 wolf-devour SELF 0 40 0");
        Receive("ABILITY UNIT 200 3 wolf-devour ALLY 8 40 0");
        Check(Skills.Definition(3)?.Target==SkillTargetMode.Self,"Own upgraded definition overrides global catalog");
        Receive("WOLF 802 0 0 3");Check(Skills.StatusText.Contains("3회"),"Remaining devour attacks displayed");
        Check(Skills.CanUpgrade(4),"Passive can spend the same point pool");
        start=wire.Length;
        using var passiveKey=new InputEventKey { Pressed=true,Keycode=Key.P,CtrlPressed=true };
        Check(Skills.TryHandleShortcut(passiveKey) && After(wire,start)=="UPGRADE_SKILL 4\n","Ctrl+P upgrades passive without casting");
        Check(Skills.Progress?.Passive==0 && Skills.Progress?.Points==7,"Passive rank waits for server acknowledgement");
        start=wire.Length;passiveKey.Echo=true;Skills.TryHandleShortcut(passiveKey);Check(wire.Length==start,"Holding Ctrl+P does not repeat spend");
        using var plainP=new InputEventKey { Pressed=true,Keycode=Key.P };
        Check(!Skills.TryHandleShortcut(plainP),"Passive cannot be cast with P");
        Receive("HERO_SKILLS 200 10 6 1 0 0 0 1 1");
        var passivePanel=Skills.GetNode<Control>("Content/Slots/PassiveSlot/Passive");
        Check(passivePanel.TooltipText.Contains("+6%") && passivePanel.TooltipText.Contains("+60%"),"Wolf passive tooltip follows server rank");
        var passiveButton=Skills.GetNode<Button>("Content/Slots/PassiveSlot/Upgrade");
        start=wire.Length;passiveButton.EmitSignal(Button.SignalName.Pressed);
        Check(After(wire,start)=="UPGRADE_SKILL 4\n","Passive button uses same command as shortcut");
        Receive("HERO_SKILLS 200 10 3 1 0 0 0 1 4");
        Check(!Skills.CanUpgrade(4) && passiveButton.Disabled && passivePanel.TooltipText.Contains("+14%") &&
            passivePanel.TooltipText.Contains("10% 추가 피해"),"Passive caps at four and displays full-charge talent");
        progress=Skills.Progress;
        Receive("HERO_SKILLS 200 10 2 1 0 0 0 1 5");Receive("HERO_SKILLS 200 10 4 1 0 0 0 1 4");
        Check(Skills.Progress==progress,"Invalid passive rank/budget rejected");
        Receive("RESPAWN 900");Receive("REMOVE 802");Receive("UNIT 200 803 7 -4 0 1");Receive("HP 803 3000 3000");
        Check(Skills.Progress?.Passive==4,"Passive persists across death and respawn");
        Receive("WELCOME 7 1 COMMANDER");
        Check(Skills.Progress==null && !Skills.CanUpgrade(0),"Role change clears progression");
        Check(!HeroSkillProgressSnapshot.TryParse("HERO_SKILLS 200 4 2 1 0 0 0 0 0".Split(' '),out _) &&
            !HeroSkillProgressSnapshot.TryParse("HERO_SKILLS 200 9 6 1 0 0 0 1 0".Split(' '),out _) &&
            !HeroSkillProgressSnapshot.TryParse("HERO_SKILLS 200 5 -1 0 0 0 0 0 0".Split(' '),out _) &&
            !HeroSkillProgressSnapshot.TryParse($"HERO_SKILLS 200 {HeroSkillProgressSnapshot.MaxHeroLevel+1} {HeroSkillProgressSnapshot.MaxHeroLevel} 0 0 0 0 0 0".Split(' '),out _) &&
            HeroSkillProgressSnapshot.TryParse($"HERO_SKILLS 200 {HeroSkillProgressSnapshot.MaxHeroLevel} {HeroSkillProgressSnapshot.MaxHeroLevel-1} 0 0 0 0 0 0".Split(' '),out _),"Invalid progression levels/points rejected; max level accepted");
    }
    private void Begin()
    {
        Receive($"MAP 2 {Map.MapHash}");Receive("WORLD_READY");Receive("WELCOME 7 1 HERO 202");
        Receive("SIGHT UNIT 202 10");Receive("UNIT 202 701 7 -4 0 1");Receive("HP 701 3200 3200");Receive("TICK 300");
        Receive("ABILITY UNIT 202 0 marshal-shot ENEMY 7 7 0");
        Receive("ABILITY UNIT 202 1 marshal-bulwark SELF 0 12 0");
        Receive("ABILITY UNIT 202 2 marshal-march SELF 0 12 0");
        Receive("ABILITY UNIT 202 3 marshal-command SELF 0 60 0");
        Units.Camera.Size=26;CameraNavigation.FocusGround(Units.Camera,Vector3.Zero);
    }
    private async Task CapturePreview()
    {
        Units.Camera.Size=19;CameraNavigation.FocusGround(Units.Camera,new Vector3(-2,0,0));
        Receive("UNIT 202 720 8 -1 -3 2");Receive("HP 720 3200 3200");
        Receive("UNIT 0 721 7 1 3 1");Receive("HP 721 500 500");
        Receive("ITEM_CRATE 904 7 1 721 1 3");Receive("MARSHAL 701 420 1200");
        Receive("HERO_SKILLS 202 10 3 1 1 1 0 0 3");
        Shop.Toggle();await Flush();
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        using Image image=GetViewport().GetTexture().GetImage();
        Check(image.SavePng("res://docs/images/recovered-client.png")==Error.Ok,"Save recovery preview");
    }
    private static void CheckParsers()
    {
        Check(HeroItemDefinition.TryParseCatalog(CatalogLine,out var defs) && defs.Length==3 && defs[2].From==1,"Catalog with Korean names and upgrade lineage");
        Check(!HeroItemDefinition.TryParseCatalog("ITEM_CATALOG [null]",out _) &&
            !HeroInventorySnapshot.TryParse("HERO_ITEMS 5 0 0 0 1 0 0 0 0 0 0".Split(' '),out _) &&
            !HeroInventorySnapshot.TryParse("HERO_ITEMS -1 0 0 0 0 0 0 0 0 0 0".Split(' '),out _) &&
            !ItemCrateSnapshot.TryParse("ITEM_CRATE 9 7 1 0 NaN 0".Split(' '),out _) &&
            !RecallSnapshot.TryParse("RECALL 7 300 200".Split(' '),out _) &&
            !MarshalEffectSnapshot.TryParse("MARSHAL 0 0 0".Split(' '),out _),"Reject invalid wire data");
    }
    private static string After(MemoryStream stream,long start) => Encoding.UTF8.GetString(stream.ToArray()[(int)start..]).Replace("\r\n","\n");
    private void Receive(string line) => InvokeMain("OnMessage",line);
    private void InvokeMain(string method,params object[] args) => typeof(Main).GetMethod(method,Private).Invoke(this,args);
    private static void Check(bool condition,string message) { if (!condition) throw new InvalidOperationException(message); }
    private async Task Flush()
    { await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame); }
    private void RightClick(Node3D target)
    {
        Vector2 point=Units.Camera.UnprojectPosition(target.GlobalPosition+Vector3.Up*.4f);
        using var motion=new InputEventMouseMotion { Position=point,GlobalPosition=point };GetViewport().PushInput(motion,true);
        using var click=new InputEventMouseButton { Position=point,GlobalPosition=point,Pressed=true,ButtonIndex=MouseButton.Right,ButtonMask=MouseButtonMask.Right };
        GetViewport().PushInput(click,true);click.Pressed=false;click.ButtonMask=0;GetViewport().PushInput(click,true);
    }
}
