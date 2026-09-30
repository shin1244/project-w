using Godot;
using System;
using System.Reflection;

public partial class StockChecks : Main
{
    private static readonly MethodInfo Handler = typeof(Main).GetMethod("OnMessage", BindingFlags.Instance | BindingFlags.NonPublic);

    public override void _Ready()
    {
        try
        {
            // 실제 게임 씬의 UI 연결을 검사하되 서버 접속은 시작하지 않습니다.
            var game = GD.Load<PackedScene>("res://game/Main.tscn").Instantiate<Main>();
            Check(game.Stock == game.GetNode<StockDisplay>("SelectionUI/Stock"), "Main scene stock reference");
            game.Free();

            Stock = new StockDisplay();
            AddChild(Stock);
            Label label = Stock.GetNode<Label>("Resources/Resource0/Amount");
            Check(label.Text == "0", "Empty stock starts at zero");
            Check(Stock.MouseFilter == Control.MouseFilterEnum.Ignore && label.MouseFilter == Control.MouseFilterEnum.Ignore,
                "Stock UI does not capture game clicks");

            Resources = new ResourceManager
            {
                OakScene = GD.Load<PackedScene>("res://resources/trees/Oak.tscn"),
                PineScene = GD.Load<PackedScene>("res://resources/trees/Pine.tscn"),
                BirchScene = GD.Load<PackedScene>("res://resources/trees/Birch.tscn")
            };
            AddChild(Resources);
            Map = new MapWorld { Resources = Resources, MapPath = "res://tests/fixtures/map-sync.json" };
            AddChild(Map);
            Units = new UnitManager { WorkerScene = GD.Load<PackedScene>("res://units/Worker.tscn") };
            AddChild(Units);
            typeof(Main).GetField("_net", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(this, GetNode<NetClient>("/root/Net"));

            Label population = Stock.GetNode<Label>("Resources/Supply/Amount");
            Receive("SUPPLY 4 10");
            Check(population.Text == "0 / 0", "Supply follows world sync gate");
            Receive("STOCK 0 99");
            Check(label.Text == "0", "Stock follows world sync gate");
            Receive($"MAP 2 {Map.MapHash}");
            Receive("WORLD_READY");
            Receive("SUPPLY 4 10");
            Check(population.Text == "4 / 10", "Server supply total displayed");
            Receive("SUPPLY 10 10");
            Check(population.GetThemeColor("font_color") == new Color("ff9b82"), "Full supply warning");
            Receive("SUPPLY 12 5");
            Check(population.Text == "12 / 5", "Destroyed supply buildings may leave usage above limit");
            foreach (string invalid in new[] { "SUPPLY", "SUPPLY 1", "SUPPLY -1 10", "SUPPLY 1 -10", "SUPPLY x 10", "SUPPLY 1.5 10", "SUPPLY 1 9223372036854775808", "SUPPLY 1 10 extra" })
                Receive(invalid);
            Check(population.Text == "12 / 5", "Invalid supply messages leave totals unchanged");
            Receive("SUPPLY 3 15");
            Receive("SUPPLY 3 15");
            Check(population.Text == "3 / 15" && population.GetThemeColor("font_color") == new Color("fff3d6"), "Supply recovery and duplicate totals");
            Receive("STOCK 0 100");
            Receive("STOCK 0 120");
            Receive("STOCK 0 120");
            Check(label.Text == "120", "Absolute totals and duplicate delivery");
            Receive("STOCK 0 20");
            Check(label.Text == "20", "Spending decreases stock");
            Receive("STOCK 0 0");
            Check(label.Text == "0", "Zero total is applied");
            Receive("STOCK 0 3000000000");
            Check(label.Text.Contains("3,000,000,000"), "64-bit Go int total and readable grouping");
            Receive("STOCK 7 55");
            string before = label.Text;
            foreach (string invalid in new[] { "STOCK", "STOCK 0", "STOCK 0 -1", "STOCK -1 5", "STOCK 0 1.5", "STOCK x 5", "STOCK 0 NaN", "STOCK 0 9223372036854775808", "STOCK 4294967296 2", "STOCK 0 5 extra" })
                Receive(invalid);
            Check(label.Text == before && Stock.GetNode<Label>("Resources/Resource7/Amount").Text == "55" && Stock.GetNode<Label>("Resources/Resource7/Type").Text == "7", "Malformed messages ignored; unknown types remain separate");

            Receive($"MAP 2 {Map.MapHash}");
            Receive("WORLD_READY");
            Check(population.Text == "0 / 0", "Supply reset on map sync");
            Check(label.Text == "0" && !Stock.HasNode("Resources/Resource7"), "Reconnect clears omitted entries");
            Receive("STOCK 0 35");
            Receive("SUPPLY 7 15");
            typeof(Main).GetMethod("OnConnectionClosed", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(this, new object[] { "Stock test disconnect" });
            Receive("STOCK 0 999");
            Receive("SUPPLY 9 15");
            Check(population.Text == "0 / 0", "Disconnect clears and blocks supply");
            Check(label.Text == "0" && !Map.IsSynchronized, "Disconnect clears stale totals and blocks updates");
            GD.Print("PASS: stock scene wiring, sync gate, totals, spending, zero, validation, multiple types, reconnect and disconnect");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private void Receive(string message) => Handler.Invoke(this, new object[] { message });
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
