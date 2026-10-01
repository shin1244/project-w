using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

// Main의 실제 메시지 라우팅/입력 연결/송신 경로를 메모리 스트림으로 검증합니다.
public partial class ConstructionSyncChecks : Main
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<string> _commands = new();
    private MemoryStream _wire;
    private SelectionDetails _details;
    private void Invoke(string method, params object[] args) => typeof(Main).GetMethod(method, Private).Invoke(this, args);
    private void Receive(string message) => Invoke("OnMessage", message);
    private Label Detail(string name) => (Label)typeof(SelectionDetails).GetField(name, Private).GetValue(_details);
    private Unit Worker(uint id = 101)
    {
        Check(Units.TryGetUnit(id, out Unit unit), $"Unit {id} exists");
        return unit;
    }
    private Building Building(uint id)
    {
        Check(Buildings.TryGetBuilding(id, out Building building), $"Building {id} exists");
        return building;
    }

    public override async void _Ready()
    {
        try
        {
            Map.MapPath = "res://tests/fixtures/map-fog-occlusion.json";
            Map.LoadLocalMap();
            Fog.Configure(Map);
            var net = GetNode<NetClient>("/root/Net");
            typeof(Main).GetField("_net", Private).SetValue(this, net);
            using var wire = new MemoryStream();
            _wire = wire;
            using var writer = new StreamWriter(wire, new UTF8Encoding(false)) { AutoFlush = true };
            typeof(NetClient).GetField("_writer", Private).SetValue(net, writer);
            Invoke("ConnectInput");
            Units.CommandRequested += _commands.Add;
            Units.CommandRequested += command => Invoke("SendCommand", command);
            Placement.SetProcess(false);
            Units.SetProcess(false);
            Units.Camera.Position = new Vector3(-8, 32, 22);
            Units.Camera.LookAt(new Vector3(-8, 0, 0));
            Units.Camera.Size = 32;
            _details = GetNode<SelectionDetails>("SelectionUI/SelectionDetails");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            BeginSession();
            Receive("SIGHT UNIT 0 20");
            Receive("UNIT 0 101 7 -12 2 1");
            Receive("UNIT 0 102 7 -12 4 1");
            Receive("UNIT 1 201 7 -14 2 1");
            Receive("UNIT 0 301 8 -14 4 1");
            string[] sceneNames = { "TownHall", "Fortress", "Store", "Supply", "Barracks", "Forge", "Tower" };
            for (uint type = 0; type <= 6; type++)
            {
                Check(Buildings.SceneFor(type)?.ResourcePath == $"res://buildings/{sceneNames[type]}.tscn",
                    $"Actual Main scene maps synchronized type {type} to {sceneNames[type]}");
                Receive($"BUILDING {type} {500 + type} 1 {-12 + (int)type * 4} -7 0");
                Check(Building(500 + type).BuildingType == type, $"Main BUILDING branch creates type {type}");
            }
            Building site = Building(502);
            Receive("CONSTRUCTION 502 0");
            Check(site.IsUnderConstruction && site.ConstructionPercent == 0 && site.GetNode<ConstructionSite>("ConstructionSite").Visible,
                "Main CONSTRUCTION branch activates the zero-percent common site");
            Receive("HP 502 60 400");
            Invoke("SelectBuilding", site);
            Check(Detail("_title").Text == "저장소" && Detail("_activity").Text.Contains("공사 중") &&
                Detail("_activity").Text.Contains("0%") && Detail("_extra").Text.Contains("이어 짓기"),
                "Selected store shows its construction percentage and continuation hint");
            Receive("CONSTRUCTION 502 37");
            Check(Detail("_activity").Text.Contains("37%") && site.GetNode<ConstructionSite>("ConstructionSite").Percent == 37,
                "Server percentage refreshes selected details and construction state without requiring a world-space label");

            Receive("STATE 101 IDLE 17 0 10");
            Invoke("SelectUnit", Worker(102));
            Units.ToggleSelection(Worker(201));
            Units.ToggleSelection(Worker(101));
            UnitState beforeState = Worker().State;
            int beforeCount = _commands.Count;
            long beforeWire = _wire.Length;
            Units.RequestContextOrder(site, site.GlobalPosition);
            Check(_commands.Count == beforeCount + 1 && _commands.Last() == "CONSTRUCT 502 101" &&
                ReadSince(beforeWire) == "CONSTRUCT 502 101" + System.Environment.NewLine,
                "Friendly site right-click sends exactly one CONSTRUCT through Main/Net using the lowest selected owned worker ID");
            Check(Worker().State == beforeState && site.ConstructionPercent == 37 && Units.SelectedUnitIds.Count == 3,
                "A continuation request preserves selection and waits for authoritative worker/progress snapshots");
            Receive("ERR 이미 짓는 중");
            Check(Placement.Status.Visible && Placement.Status.Text == "이미 짓는 중" &&
                Worker().State == beforeState && site.ConstructionPercent == 37 && _commands.Count == beforeCount + 1,
                "Busy-builder error displays the server notice without optimistic construction state or a retry");

            int swings = 0;
            Worker().StateChanged += (_, swung) => { if (swung) swings++; };
            Receive("STATE 101 BUILD 17 502 11");
            Check(Worker().State == new UnitState(UnitActivity.Build, 17, 502, 11) && swings == 1 && Worker().IsProcessing(),
                "Worker BUILD state preserves carried wood, building focus and the server swing sequence");
            Receive("STATE 101 BUILD 17 502 11");
            Receive("STATE 101 BUILD 17 502 12");
            Check(swings == 2 && Worker().State.SwingSequence == 12, "Repeated BUILD sequence does not replay a swing; the next sequence is accepted");
            Invoke("SelectUnit", Worker());
            _details._Process(0);
            Check(Detail("_activity").Text.Contains("건설 중") && Detail("_extra").Text.Contains("17"),
                "Selected worker displays construction activity and retains its carried resource display");

            beforeCount = _commands.Count;
            beforeWire = _wire.Length;
            Units.ToggleSelection(Worker(301));
            Check(!Units.SelectedUnitIds.Contains(301u) && !Units.RequestConstruct(site, 301) && !Units.RequestConstruct(site, 102),
                "Another player's allied worker and an unselected owned worker cannot be sent to construct");
            Invoke("SelectUnit", Worker(301));
            Units.RequestContextOrder(site, site.GlobalPosition);
            Check(Units.SelectedUnitIds.Count == 0 && _commands.Count == beforeCount && _wire.Length == beforeWire,
                "An allied worker owned by someone else cannot issue continuation commands");
            Invoke("SelectUnit", Worker(201));
            Check(!Units.RequestConstruct(site, 201), "A selected combat unit cannot be used as a construction worker");

            Invoke("SelectUnit", Worker());
            Receive("BUILDING 2 602 2 8 6 0");
            Receive("CONSTRUCTION 602 24");
            Building enemy = Building(602);
            Check(!Units.RequestConstruct(enemy, 101), "An enemy site cannot receive a direct construct request");
            beforeWire = _wire.Length;
            Units.RequestContextOrder(enemy, enemy.GlobalPosition);
            Check(_commands.Last() == "ATTACK 602 101" && ReadSince(beforeWire) == "ATTACK 602 101" + System.Environment.NewLine,
                "Enemy construction sites retain the normal attack context action");

            Receive("CONSTRUCTION 502 100");
            Invoke("SelectBuilding", site);
            Check(!site.IsUnderConstruction && site.GetNode<MeshInstance3D>("Visual").Visible &&
                !Detail("_activity").Text.Contains("공사 중") && Detail("_extra").Text == "",
                "Completion restores the real building and removes construction-only selection text");
            Invoke("SelectUnit", Worker());
            beforeWire = _wire.Length;
            Units.RequestContextOrder(site, site.GlobalPosition);
            Check(_commands.Last() == "MOVE -4 -7 101" && ReadSince(beforeWire) == "MOVE -4 -7 101" + System.Environment.NewLine &&
                !Units.RequestConstruct(site, 101), "Completed friendly buildings keep the normal movement context action");

            Receive("CONSTRUCTION 502 45");
            Receive("STATE 101 BUILD 17 502 13");
            Invoke("SelectBuilding", site);
            Receive("REMOVE 502");
            Check(!Buildings.TryGetBuilding(502, out _) && Buildings.SelectedBuilding == null && Units.ResolveFocus(502) == null,
                "A destroyed site is immediately unselectable and stops resolving as a worker target");
            Worker()._Process(0); // REMOVE may precede the server's matching IDLE snapshot.
            Receive("STATE 101 IDLE 17 0 13");
            Invoke("SelectUnit", Worker());
            _details._Process(0);
            Check(Worker().State.Activity == UnitActivity.Idle && Worker().State.FocusId == 0 && Worker().State.Carrying == 17 &&
                !Worker().IsProcessing() && !Detail("_activity").Text.Contains("건설 중"),
                "Server IDLE after destruction releases the worker while preserving its carried resources");
            beforeCount = _commands.Count;
            Units.RequestContextOrder(site, Vector3.Zero);
            Receive("CONSTRUCTION 502 100");
            Check(_commands.Count == beforeCount && !Buildings.TryGetBuilding(502, out _), "Stale clicks/progress cannot recreate or command a removed site");

            Receive("BUILDING 5 605 1 -7 -5 0");
            Receive("CONSTRUCTION 605 23");
            Building forge = Building(605);
            Map.StopSync();
            beforeWire = _wire.Length;
            Units.RequestContextOrder(forge, forge.GlobalPosition);
            Check(_wire.Length == beforeWire, "Main does not send continuation requests while map synchronization is unavailable");
            BeginSession();
            Check(Buildings.LiveBuildings.Count == 0 && Units.LiveUnits.Count == 0 && Units.SelectedUnitIds.Count == 0,
                "Map resynchronization clears construction sites and stale worker identities");
            Receive("BUILDING 5 605 1 -7 -5 0");
            Receive("CONSTRUCTION 605 64");
            Receive("UNIT 0 101 7 -12 2 1");
            bool historicalSwing = false;
            Worker().StateChanged += (_, swung) => historicalSwing |= swung;
            Receive("STATE 101 BUILD 5 605 90");
            Check(Building(605).IsUnderConstruction && Building(605).ConstructionPercent == 64 &&
                Worker().State.Activity == UnitActivity.Build && !historicalSwing,
                "Reconnect snapshots restore authoritative construction without replaying previous worker swings");
            Invoke("OnConnectionClosed", "Construction synchronization test disconnect");
            Check(Buildings.LiveBuildings.Count == 0 && Units.LiveUnits.Count == 0 && !Map.IsSynchronized,
                "Disconnect clears construction state along with the synchronized world");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GD.Print("PASS: actual Main type mappings, CONSTRUCTION/BUILD state routing, construction selection details, exact CONSTRUCT wire format, ownership/selection checks, busy error, context actions, destruction IDLE, synchronization and reconnect");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private void BeginSession()
    {
        Receive($"MAP 2 {Map.MapHash}"); Receive("WORLD_READY"); Receive("WELCOME 7 1 COMMANDER");
    }
    private string ReadSince(long offset) => Encoding.UTF8.GetString(_wire.ToArray().AsSpan((int)offset));
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
