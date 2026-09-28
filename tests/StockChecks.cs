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
            Label label = Stock.GetChild<Label>(0);
            Check(label.Text.Contains("목재   0"), "Empty stock starts at zero");
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

            Receive("STOCK 0 99");
            Check(label.Text.Contains("목재   0"), "Stock follows world sync gate");
            Receive($"MAP 2 {Map.MapHash}");
            Receive("WORLD_READY");
            Receive("STOCK 0 100");
            Receive("STOCK 0 120");
            Receive("STOCK 0 120");
            Check(label.Text.Contains("목재   120"), "Absolute totals and duplicate delivery");
            Receive("STOCK 0 20");
            Check(label.Text.Contains("목재   20"), "Spending decreases stock");
            Receive("STOCK 0 0");
            Check(label.Text.Contains("목재   0"), "Zero total is applied");
            Receive("STOCK 0 3000000000");
            Check(label.Text.Contains("3,000,000,000"), "64-bit Go int total and readable grouping");
            Receive("STOCK 7 55");
            string before = label.Text;
            foreach (string invalid in new[] { "STOCK", "STOCK 0", "STOCK 0 -1", "STOCK -1 5", "STOCK 0 1.5", "STOCK x 5", "STOCK 0 NaN", "STOCK 0 9223372036854775808", "STOCK 4294967296 2", "STOCK 0 5 extra" })
                Receive(invalid);
            Check(label.Text == before && label.Text.Contains("자원 7   55"), "Malformed messages ignored; unknown types remain separate");

            Receive($"MAP 2 {Map.MapHash}");
            Receive("WORLD_READY");
            Check(label.Text.Contains("목재   0") && !label.Text.Contains("자원 7"), "Reconnect clears omitted entries");
            Receive("STOCK 0 35");
            typeof(Main).GetMethod("OnConnectionClosed", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(this, new object[] { "Stock test disconnect" });
            Receive("STOCK 0 999");
            Check(label.Text.Contains("목재   0") && !Map.IsSynchronized, "Disconnect clears stale totals and blocks updates");
            GD.Print("PASS: stock scene wiring, sync gate, totals, spending, zero, validation, multiple types, reconnect and disconnect");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private void Receive(string message) => Handler.Invoke(this, new object[] { message });
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
