using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

// 실제 서버 메시지 형식으로 소유권과 진영을 따로 검증합니다. 서버 연결은 필요 없습니다.
public partial class MinionSyncChecks : Main
{
    private static readonly MethodInfo Handler = typeof(Main).GetMethod("OnMessage", BindingFlags.Instance | BindingFlags.NonPublic);

    public override void _Ready()
    {
        try
        {
            Map = new MapWorld();
            typeof(MapWorld).GetField("<IsSynchronized>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(Map, true);
            Units = new UnitManager
            {
                WorkerScene = GD.Load<PackedScene>("res://units/Worker.tscn"),
                KnightScene = GD.Load<PackedScene>("res://units/Knight.tscn"),
                ArcherScene = GD.Load<PackedScene>("res://units/Archer.tscn")
            };
            AddChild(Units);
            var camera = new Camera3D { Position = new Vector3(0, 15, 0), RotationDegrees = new Vector3(-90, 0, 0), Current = true };
            AddChild(camera);
            Units.Camera = camera;
            var sent = new List<string>();
            Units.CommandRequested += sent.Add;

            Receive("WELCOME 7 2 COMMANDER"); // 플레이어 ID로 진영을 추측하면 실패하는 사례
            Receive("UNIT 0 101 7 0 0 2");
            Receive("UNIT 100 201 0 -2 0 2");
            Receive("UNIT 101 202 0 2 0 1");
            Receive("UNIT 0 301 9 0 -2 2");
            Unit own = Units.GetNode<Unit>("Unit_101");
            Unit ally = Units.GetNode<Unit>("Unit_201");
            Unit enemy = Units.GetNode<Unit>("Unit_202");
            Unit allyPlayer = Units.GetNode<Unit>("Unit_301");
            Check(ally.OwnerId == 0 && ally.Team == 2 && ally.UnitType == UnitCatalog.MinionMelee && enemy.Team == 1 && enemy.UnitType == UnitCatalog.MinionRanged,
                "Minions use dedicated melee/ranged types with server team and no owner");
            foreach (Unit other in new[] { ally, enemy, allyPlayer })
            {
                Units.SelectSingle(other);
                Units.RequestMove(Vector3.One);
            }
            Check(Units.SelectedUnitIds.Count == 0 && sent.Count == 0, "Minions and other players cannot be controlled");
            Units.SelectBox(new Rect2(Vector2.Zero, GetViewport().GetVisibleRect().Size));
            Check(Units.SelectedUnitIds.SequenceEqual(new uint[] { 101 }), "Box selects only own units");
            Units.RequestAttack(ally);
            Units.RequestAttack(allyPlayer);
            Check(sent.Count == 0, "A-click cannot attack same-team minions or players");
            Units.RequestContextOrder(ally, Vector3.Zero);
            Check(sent.Count == 1 && sent[0].StartsWith("MOVE "), "Right-click ally uses friendly movement");
            Units.RequestAttack(enemy);
            Check(sent.Last() == "ATTACK 202 101", "Enemy owner-zero minion is attackable by team");

            Receive("POS 202 3 1");
            Receive("STATE 202 ATTACK 0 101 3");
            Receive("HP 202 42 60");
            Check(enemy.GlobalPosition == new Vector3(3, 0, 1) && enemy.HasServerState && enemy.State.FocusId == 101 && enemy.HealthBar.CurrentHP == 42,
                "Existing position, state and health messages apply to minions");
            Receive("UNIT 101 202 0 4 1 2");
            int count = sent.Count;
            Units.RequestAttack(enemy);
            Check(Units.GetNode<Unit>("Unit_202") == enemy && enemy.Team == 2 && sent.Count == count,
                "Duplicate snapshot updates team and stops friendly attacks");

            foreach (string invalid in new[] { "UNIT 100 401 0 0 0", "UNIT 100 401 0 0 0 nope", "UNIT 100 401 0 0 0 0", "UNIT 100 401 0 0 0 2 extra", "UNIT 100 0 0 0 0 2" }) Receive(invalid);
            Check(Units.GetChildCount() == 4, "Missing or invalid team never spawns a unit");
            Receive("WELCOME 8 nope");
            Receive("WELCOME 8");
            Units.SelectSingle(own);
            Check(Units.SelectedUnitIds.Contains(101u), "Malformed welcome does not change local ownership");

            foreach (string invalid in new[] { "UNIT 1 401 0 0 0 2", "UNIT 2 401 0 0 0 2", "UNIT 100 401 7 0 0 2", "UNIT 101 401 7 0 0 2", "UNIT 102 401 7 0 0 2", "UNIT 3 401 0 0 0 2", "UNIT 4 401 0 0 0 2" }) Receive(invalid);
            Check(Units.GetChildCount() == 4, "Old minion IDs are rejected; current mercenary and minion types retain their ownership rules");
            Receive("UNIT 102 402 0 3 -2 2");
            Unit healer = Units.GetNode<Unit>("Unit_402");
            Check(healer.UnitType == UnitCatalog.MinionHealer && healer.OwnerId == 0 && healer.Team == 2 &&
                UnitCatalog.IsMinion(healer.UnitType) && Units.SceneFor(healer.UnitType) != null,
                "Purchased healing minions have their own scene and remain autonomous lane units");
            Units.SelectSingle(healer);
            int beforeHealerOrder = sent.Count;
            Units.RequestMove(Vector3.One);
            Check(Units.SelectedUnitIds.Count == 0 && sent.Count == beforeHealerOrder,
                "Commanders cannot directly order purchased healing minions");
            Receive("HP 402 25 50");
            Check(healer.HealthBar.CurrentHP == 25, "Healing minions use normal server health snapshots");
            foreach (string invalid in new[] { "MINION_HEAL 9999 3 -2 4", "MINION_HEAL 101 3 -2 4",
                "MINION_HEAL 402 NaN -2 4", "MINION_HEAL 402 3 -2 0", "MINION_HEAL 402 3 -2 4 extra" }) Receive(invalid);
            Check(!Units.GetChildren().OfType<MinionHealPulse>().Any(), "Invalid or unknown-source healing events cannot create a pulse");
            Receive("MINION_HEAL 402 3 -2 4");
            Receive("MINION_HEAL 402 3 -2 4");
            Check(Units.GetChildren().OfType<MinionHealPulse>().Count() == 1 && healer.HealthBar.CurrentHP == 25,
                "A server healing event creates one visual pulse without predicting any health changes");
            Receive("REMOVE 402");
            Check(Units.GetChildren().OfType<MinionHealPulse>().Any(), "The healing pulse survives its source's normal death removal");
            Receive("HIDE 402");
            Check(!Units.GetChildren().OfType<MinionHealPulse>().Any(), "Fog hiding cancels the pulse even after source removal");
            Receive("UNIT 0 101 9 0 0 2");
            Check(Units.SelectedUnitIds.Count == 0, "Losing ownership clears selection even when team stays the same");
            Receive("REMOVE 202");
            Check(enemy.IsDying && !Units.HasNode("Unit_202"), "Minion removal uses normal death lifecycle");
            Units.Clear();
            Receive("UNIT 0 101 7 0 0 2");
            Units.SelectSingle(Units.GetNode<Unit>("Unit_101"));
            Check(Units.SelectedUnitIds.Count == 0, "Map reset clears local player and team until next welcome");
            Receive("WELCOME 7 2 COMMANDER");
            Units.SelectSingle(Units.GetNode<Unit>("Unit_101"));
            Check(Units.SelectedUnitIds.Contains(101u), "Reconnect welcome restores control");
            GD.Print("PASS: new WELCOME/UNIT format, minion lifecycle, team relations, ownership, validation and reset");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
        finally { Map?.Free(); }
    }

    private void Receive(string message) => Handler.Invoke(this, new object[] { message });
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
