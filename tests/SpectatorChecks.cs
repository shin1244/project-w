using Godot;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

public partial class SpectatorChecks : Main
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private void Invoke(string name, params object[] args) => typeof(Main).GetMethod(name, Private).Invoke(this, args);
    private void Receive(string line) => Invoke("OnMessage", line);
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private async Task Flush() { await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }

    public override async void _Ready()
    {
        try
        {
            Check(WelcomeSnapshot.TryParse("WELCOME 7 0 SPECTATOR".Split(' '), out var welcome) && welcome.Role == PlayerRole.Spectator, "Neutral spectator welcome");
            foreach (string line in new[] { "WELCOME 7 1 SPECTATOR", "WELCOME 7 0 COMMANDER", "WELCOME 7 0 HERO 202", "WELCOME 0 0 SPECTATOR", "WELCOME 7 0 SPECTATOR 202" })
                Check(!WelcomeSnapshot.TryParse(line.Split(' '), out _), "Invalid spectator/playing role accepted");
            var assignment = new MatchAssignment { MatchId = "watch", Host = "localhost", Port = 7777, Ticket = new string('a',32), Role = "SPECTATOR", Team = 0, Players = 8, Bots = 8 };
            Check(assignment.IsValid, "Six hero AI and two commander AI plus spectator is a valid match");
            assignment.Players = assignment.Bots = 6;
            Check(!assignment.IsValid, "Old six-seat matches cannot be mistaken for the complete three-hero teams");
            assignment.Players = assignment.Bots = 8;
            assignment.Team = 1; Check(!assignment.IsValid, "Observer cannot claim a team");
            assignment.Role = "HERO"; assignment.Hero = UnitCatalog.HeroMarshal; assignment.Bots = 7;
            Check(assignment.IsValid, "One human and seven AI fill an eight-seat practice match");
            assignment.Bots = 0;
            Check(assignment.IsValid, "Eight humans can fill the same match");
            assignment.Bots = 8;
            Check(!assignment.IsValid, "A human assignment cannot also fill all eight seats with AI");
            Check(new MatchResultSnapshot(2,"BASE_DESTROYED",500).Title(0) == "2팀 승리", "Observer result is neutral");
            Check(TeamExperienceSnapshot.TryParse("TEAM_EXP 1 2 35 150 13525".Split(' '), out var teamXP) &&
                teamXP == new TeamExperienceSnapshot(1, 2, 35, 150, 13525) && teamXP.TotalExperienceText == "135.25",
                "Team XP retains exact hundredths and displays experience rather than a percentage");
            foreach (string line in new[] { "TEAM_EXP 0 1 0 100 0", "TEAM_EXP 3 1 0 100 0", "TEAM_EXP 1 0 0 100 0",
                "TEAM_EXP 1 1 -1 100 0", "TEAM_EXP 1 1 100 100 0", "TEAM_EXP 1 10 1 0 100", "TEAM_EXP 1 1 0 -1 0",
                "TEAM_EXP 1 1 0 100 -1", "TEAM_EXP 1 1 0 100 NaN", "TEAM_EXP 1 1 0 100 9223372036854775808",
                "TEAM_EXP 1 1 0 100", "TEAM_EXP 1 1 0 100 0 extra" })
                Check(!TeamExperienceSnapshot.TryParse(line.Split(' '), out _), "Malformed team XP rejected");

            var net = GetNode<NetClient>("/root/Net");
            using var wire = new MemoryStream();
            using var writer = new StreamWriter(wire,new UTF8Encoding(false)) { AutoFlush = true };
            typeof(NetClient).GetField("_writer", Private).SetValue(net,writer);
            typeof(Main).GetField("_net", Private).SetValue(this,net);
            Invoke("ConnectInput");
            var guide = GetNode<BattleGuide>("SelectionUI/BattleGuide");
            Label TeamProgress(int team) => (Label)guide.FindChild($"Team{team}Experience", true, false);
            Units.CommandRequested += command => Invoke("SendCommand",command);
            Fog.Configure(Map);
            Receive($"MAP 2 {Map.MapHash}"); Receive("WORLD_READY"); Receive("WELCOME 7 0 SPECTATOR");
            Check(TeamProgress(1).Text.Contains("Lv.—") && TeamProgress(2).Text.Contains("Lv.—"),
                "Observer does not invent team progress before a snapshot");
            Receive("TEAM_EXP 1 2 35 150 13525"); Receive("TEAM_EXP 2 3 40 200 29050");
            Check(TeamProgress(1).Visible && TeamProgress(2).Visible && TeamProgress(1).Text.Contains("Lv.2") &&
                TeamProgress(1).Text.Contains("누적 135.25") && TeamProgress(2).Text.Contains("Lv.3"),
                "Observer sees both teams' shared level and cumulative XP");
            Receive("TEAM_EXP 1 10 0 0 500050"); Receive("TEAM_EXP 1 10 0 0 510075");
            string maxProgress = TeamProgress(1).Text;
            Receive("TEAM_EXP 1 10 1 0 510075");
            Check(maxProgress.Contains("MAX") && maxProgress.Contains("누적 5100.75") && TeamProgress(1).Text == maxProgress,
                "Lifetime XP continues at max level and malformed updates preserve the last valid snapshot");
            Receive("UNIT 200 101 1001 -4 0 1"); Receive("HP 101 700 2200");
            Receive("UNIT 202 102 1002 4 0 2"); Receive("HP 102 1900 3200");
            Receive("STATS 102 200 2 8 5 1.2");
            Receive("BUILDING 4 901 2 12 12 0"); Receive("BUILDING_VISION 901 1");
            Receive("TRIBUTE 0 WAITING 0 0 3600 0 0 0 0 0 0");
            await Flush(); Fog.RefreshVision();
            Check(LocalRole == PlayerRole.Spectator && Units.LocalTeam == 0 && Units.Spectating && Units.DisplayTeam == 1, "Observer identity and rendering perspective");
            Check(!Fog.Visible && Fog.IsVisibleAt(new Vector3(60,0,60)) && Buildings.LiveBuildings.All(Fog.IsBuildingVisible), "Whole map visible for observer");
            Check(Spectator.Visible && Details.Visible && !Commands.Visible && !Minions.Visible && !Stock.Visible && !Shop.Visible, "Read-only observer HUD");
            Check(GetViewport().GetVisibleRect().Encloses(guide.GetGlobalRect()), "Shared team progress stays inside the viewport");
            var tributePanel = GetNode<TributeEventPanel>("SelectionUI/TributeEventPanel");
            Check(tributePanel.IsVisibleInTree() && !guide.GetGlobalRect().Intersects(tributePanel.GetGlobalRect()),
                "Both spectator team progress rows remain separate from the visible tribute countdown");
            Check(Units.LiveUnits.All(u => !Units.CanControl(u) && Units.CanInspect(u)), "Both teams inspectable; neither controllable");
            Units.TryGetUnit(102,out var target);
            Invoke("SelectUnit",target);
            Check(Units.InspectedUnit == target && Units.SelectedUnitIds.Count == 0, "Enemy inspection without command selection");
            long before = wire.Length;
            Invoke("SendCommand","MOVE 0 0 102");
            Invoke("SendCommand","TRAIN 901 3");
            Units.RequestContextOrder(null,new Vector3(20,0,20));
            Units.RequestTrain(3);
            var input=GetNode<PlayerInput>("PlayerInput");
            typeof(PlayerInput).GetMethod("BeginAttackTargeting",Private).Invoke(input,null);
            Check(wire.Length == before && !input.IsAttackTargeting, "Observer must never send gameplay orders or enter attack targeting");
            Spectator.GetNode<Button>("Content/Follow").ButtonPressed = true;
            await Flush();
            Check(Spectator.Following, "Follow selected unit");
            Spectator.FocusTeam(1);
            Check(!Spectator.Following, "Camera shortcut cancels following");
            Receive("HP 102 1700 3200");
            Check(target.HealthBar.CurrentHP == 1700, "Observer receives health updates");
            Spectator.GetNode<Button>("Content/Follow").ButtonPressed = true;
            Receive("REMOVE 102"); await Flush();
            Check(!Spectator.Following, "Removed unit stops follow safely");
            Receive($"MAP 2 {Map.MapHash}");
            Check(!Units.Spectating && !Fog.Spectating && Fog.Visible && !Spectator.Visible, "Map reset clears observer state");
            Check(!guide.Visible && TeamProgress(1).Text.Contains("Lv.—") && TeamProgress(2).Text.Contains("Lv.—"),
                "Map reset clears both team progress snapshots");
            Receive("WORLD_READY"); Receive("WELCOME 7 1 COMMANDER"); Receive("UNIT 0 200 7 0 0 1");
            Units.TryGetUnit(200,out var worker);
            Check(Units.CanControl(worker) && Commands.Visible && !Spectator.Visible && Fog.Visible, "Playing again restores controls and normal fog");
            Receive("TEAM_EXP 1 2 35 150 13525"); Receive("TEAM_EXP 2 9 100 500 999999");
            Check(TeamProgress(1).Visible && TeamProgress(1).Text.Contains("Lv.2") && !TeamProgress(2).Visible &&
                TeamProgress(2).Text.Contains("Lv.—"), "Commander sees only own team's progress");
            Receive("WELCOME 7 1 COMMANDER");
            Check(TeamProgress(1).Text.Contains("누적 135.25"), "Repeated welcome preserves valid shared progress");
            Receive("WELCOME 7 1 HERO 200");
            Check(TeamProgress(1).Text.Contains("Lv.—"), "Role change clears the prior progress snapshot");
            Receive("TEAM_EXP 1 3 40 200 29025"); Receive("EXP 3 40 200");
            Check(TeamProgress(1).Text.Contains("Lv.3") && UnitInfo.Experience == new HeroExperienceSnapshot(3, 40, 200),
                "Hero's owner EXP and team progress HUD agree");
            Receive("WELCOME 7 2 HERO 200"); Receive("TEAM_EXP 1 10 0 0 600000");
            Check(!TeamProgress(1).Visible && TeamProgress(1).Text.Contains("Lv.—") && TeamProgress(2).Visible &&
                TeamProgress(2).Text.Contains("Lv.—"), "Team changes clear old progress and reject other team's updates");
            Receive("TEAM_EXP 2 4 0 250 45000");
            Invoke("OnConnectionClosed", "Team progress test disconnect");
            Check(!guide.Visible && TeamProgress(1).Text.Contains("Lv.—") && TeamProgress(2).Text.Contains("Lv.—"),
                "Disconnect clears shared progress before a new session");
            GD.Print("PASS: spectator admission, full vision, read-only commands, team inspection, follow/death, shared XP validation/privacy/max/lifecycle and role reset");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
