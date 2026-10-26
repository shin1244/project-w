using Godot;
using System.Linq;

// Coal drill gathering and tunnel orders, driven only by injected server lines.
public partial class ConstructionSyncChecks
{
    private void CheckCoalDrill()
    {
        Receive("BUILDING 10 760 1 -2 -2 0");
        Receive("BUILDING 10 761 2 4 4 0");
        Receive("BUILDING_VISION 761 1");
        Building drill = Building(760);
        Invoke("SelectUnit", Worker(101));
        Units.ToggleSelection(Worker(102));
        Units.ToggleSelection(Worker(201));
        Units.RequestContextOrder(drill, drill.GlobalPosition);
        Check(_commands.Last() == "GATHER 760 101 102", "Right-clicking an own coal drill sends GATHER for the selected own workers");
        Check(!Units.RequestGatherAt(Building(761)), "An enemy coal drill cannot be gathered");
        Receive("CONSTRUCTION 760 40 7");
        Units.RequestContextOrder(drill, drill.GlobalPosition);
        Check(_commands.Last() == "CONSTRUCT 760 101" && !Units.RequestGatherAt(drill),
            "An unfinished coal drill continues construction instead of gathering");
        Receive("CONSTRUCTION 760 100 7");
        Receive("STATE 101 GATHER 0 760 3 0");
        Check(Worker().ActionTarget == drill && Worker().State.CarryType == ResourceType.Wood,
            "A worker at the drill carries wood with the drill as its focus");
        Receive("STATE 101 IDLE 0 0 3 0");
        Receive("REMOVE 760");
        Receive("REMOVE 761");
    }

    private void CheckTunnels()
    {
        PlayerInput input = GetNode<PlayerInput>("PlayerInput");
        CommandSlot exitSlot = Commands.GetNode<CommandSlot>("Slots/Slot7");
        Receive("BUILDING 11 770 1 -4 -2 0");
        Receive("BUILDING 11 771 1 6 -2 0");
        Receive("BUILDING 11 772 2 10 4 0");
        Receive("BUILDING_VISION 772 1");
        Building entrance = Building(770), exit = Building(771);

        // An unlinked tunnel offers its exit to the commander; units just walk to it.
        Invoke("SelectBuilding", entrance);
        Check(exitSlot.Caption == "출구 설치(Q)" && !exitSlot.Disabled && Buildings.TunnelExit(entrance) == null &&
            Detail("_extra").Text.Contains("출구 없음"), "A tunnel without an exit offers Q to open one");
        Units.CommanderControl = false;
        Commands.TryHandleShortcut(new InputEventKey { Keycode = Key.Q, PhysicalKeycode = Key.Q, Pressed = true });
        Commands.TryHandleShortcut(new InputEventKey { Keycode = Key.Q, PhysicalKeycode = Key.Q, Pressed = false });
        Check(!Placement.Active && !Units.RequestTunnelExit(entrance, new Vector3(-5, 0, 3)),
            "Only the commander can open a tunnel exit");
        Units.CommanderControl = true;

        input._UnhandledInput(new InputEventKey { Keycode = Key.Q, PhysicalKeycode = Key.Q, Pressed = true });
        input._Input(new InputEventKey { Keycode = Key.Q, PhysicalKeycode = Key.Q, Pressed = false });
        Check(Placement.Active, "Q on the selected tunnel starts placing its exit");
        Invoke("SelectUnit", Worker(101));
        Check(!Placement.Active, "Selecting something else cancels exit placement");
        Invoke("SelectBuilding", entrance);
        input._UnhandledInput(new InputEventKey { Keycode = Key.Q, PhysicalKeycode = Key.Q, Pressed = true });
        input._Input(new InputEventKey { Keycode = Key.Q, PhysicalKeycode = Key.Q, Pressed = false });
        int before = _commands.Count, buildings = Buildings.LiveBuildings.Count;
        long wire = _wire.Length;
        Units.Camera.Position = new Vector3(-8, 32, 22);
        Units.Camera.LookAt(new Vector3(-8, 0, 0));
        Vector2 site = Units.Camera.UnprojectPosition(new Vector3(-5.1f, 0, 3.1f));
        using (var hover = new InputEventMouseMotion { Position = site, GlobalPosition = site })
            GetViewport().PushInput(hover, true); // Placement ignores clicks while the pointer is over the HUD.
        Check(Placement.TryPlace(site) &&
            _commands.Count == before + 1 && _commands.Last() == "TUNNEL_EXIT 770 -5 3" &&
            ReadSince(wire) == "TUNNEL_EXIT 770 -5 3" + System.Environment.NewLine && !Placement.Active &&
            Buildings.LiveBuildings.Count == buildings,
            $"Placing the exit sends one snapped TUNNEL_EXIT and waits for the server's building; " +
            $"commands=[{string.Join(" | ", _commands.Skip(before))}], reason={Placement.BlockReason}");

        Invoke("SelectUnit", Worker(101));
        Units.RequestContextOrder(entrance, entrance.GlobalPosition);
        Check(_commands.Last().StartsWith("MOVE "), "Units walk to a tunnel that has no exit yet");

        // TUNNEL_LINK: only own tunnels take a link; malformed lines change nothing.
        foreach (string invalid in new[] { "TUNNEL_LINK 770", "TUNNEL_LINK 770 x", "TUNNEL_LINK 770 771 1", "TUNNEL_LINK 772 770" })
            Receive(invalid);
        Check(entrance.TunnelPartner == 0 && Building(772).TunnelPartner == 0, "Malformed and enemy tunnel links are ignored");
        Receive("TUNNEL_LINK 770 771");
        Receive("TUNNEL_LINK 771 770");
        Check(Buildings.TunnelExit(entrance) == exit && Buildings.TunnelExit(exit) == entrance, "The server link pairs both ends");
        Invoke("SelectBuilding", entrance);
        Check(exitSlot.Disabled && exitSlot.Status == "연결됨" && Detail("_extra").Text.Contains("연결됨"),
            "A linked tunnel shows its link and cannot open a second exit");

        Invoke("SelectUnit", Worker(101));
        Units.ToggleSelection(Worker(201));
        Units.ToggleSelection(Worker(301));
        Units.RequestContextOrder(exit, exit.GlobalPosition);
        Check(_commands.Last() == "TUNNEL 771 101 201", "Right-clicking a linked tunnel sends TUNNEL for the selected own units");
        Units.RequestContextOrder(Building(772), Building(772).GlobalPosition);
        Check(_commands.Last() == "ATTACK 772 101 201", "An enemy tunnel is attacked, not entered");
        Receive("CONSTRUCTION 771 50 7");
        Units.RequestContextOrder(entrance, entrance.GlobalPosition);
        Check(_commands.Last().StartsWith("MOVE "), "An unfinished partner cannot be travelled to");
        Receive("CONSTRUCTION 771 100 7");
        Receive("REMOVE 771");
        Units.RequestContextOrder(entrance, entrance.GlobalPosition);
        Check(Buildings.TunnelExit(entrance) == null && _commands.Last().StartsWith("MOVE "),
            "A tunnel whose partner is gone takes no TUNNEL order");
        Receive("REMOVE 770");
        Receive("REMOVE 772");
        Invoke("SelectUnit", Worker(101));
    }
}
