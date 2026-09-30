using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

public partial class BuildingPlacementChecks : Main
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<string> _commands = new();
    private PlayerInput _input;
    private MemoryStream _wire;
    private bool _capture;
    private void Invoke(string method, params object[] args) => typeof(Main).GetMethod(method, Private).Invoke(this, args);
    private void Receive(string message) => Invoke("OnMessage", message);
    private Unit Unit(uint id = 101) { Check(Units.TryGetUnit(id, out Unit unit), $"Unit {id} exists"); return unit; }
    private Button Slot(int number) => Commands.GetNode<Button>($"Slots/Slot{number}");
    private void ChooseBuilding(int number = 8)
    {
        if (!Commands.IsTierOneMenuOpen) Slot(1).EmitSignal(BaseButton.SignalName.Pressed);
        Check(Commands.IsTierOneMenuOpen, "A selected worker can open the tier-one menu");
        Slot(number).EmitSignal(BaseButton.SignalName.Pressed);
        Check(!Commands.IsTierOneMenuOpen && Placement.Active, "Choosing a building returns to the base panel and starts placement");
    }
    private Vector2 Screen(float x, float z) => Units.Camera.UnprojectPosition(new Vector3(x, 0, z));
    private string Issue(float x, float z, float width = 2, float depth = 2) =>
        PlacementRules.Check(Map, Buildings, Units, new Vector3(x, 0, z), new Vector2(width, depth));

    public override async void _Ready()
    {
        var oldCulture = CultureInfo.CurrentCulture;
        try
        {
            _capture = OS.GetCmdlineUserArgs().Contains("--placement-capture");
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
            _input = GetNode<PlayerInput>("PlayerInput");
            Placement.SetProcess(false);
            Units.SetProcess(false);
            Units.Camera.Position = new Vector3(-8, 32, 22);
            Units.Camera.LookAt(new Vector3(-8, 0, 0));
            Units.Camera.Size = 32;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            BeginSession();
            Receive("SIGHT UNIT 0 20");
            Receive("UNIT 0 101 7 -12 2 1");
            Receive("UNIT 0 102 7 -12 4 1");
            Receive("UNIT 1 201 7 -14 2 1");
            Invoke("SelectUnit", Unit(201));
            Check(Slot(1).Disabled && !Placement.Begin(BuildingCatalog.Store) && !Placement.Begin(BuildingCatalog.Supply), "Combat units cannot start construction");
            Invoke("SelectUnit", Unit());
            Units.ToggleSelection(Unit(102));
            Units.ToggleSelection(Unit(201));
            Check(Slot(1).Text == "1티어(Z)" && !Slot(1).Disabled && Slot(7).Disabled && Slot(8).Disabled && Slot(9).Disabled,
                "Mixed selection with own workers exposes the tier-one menu instead of direct build buttons");
            var activation = System.Diagnostics.Stopwatch.StartNew();
            ChooseBuilding();
            Placement.UpdatePreview(Screen(-10.5f, -3.5f));
            activation.Stop();
            GD.Print($"PERF: first image placement activation {activation.Elapsed.TotalMilliseconds:F3}ms (CPU)");
            Check(Placement.Active && Placement.CanPlace && Placement.PlacementPosition == new Vector3(-10.5f, 0, -3.5f),
                "The supply preview follows the cursor's ground center and snaps to the map grid");
            var ghost = Placement.GetNode<PlacementPreviewImage>("PreviewCanvas/BuildPreview");
            Check(!HasGameplayNode(ghost) && Buildings.LiveBuildings.Count == 0, "Preview is a 2D image with no model, render viewport, collider or live building");
            Texture2D texture = ghost.Texture;
            Check(texture != null && ghost.Tint.A < 1, "Preview has its preloaded image and is translucent");
            using (Image image = texture.GetImage())
                Check(image.GetPixel(0, 0).A == 0 && image.GetUsedRect().HasArea(), "Baked image has a transparent background and visible model pixels");
            float originalSize = ghost.ImageRect.Size.X;
            Units.Camera.Size *= .5f;
            Placement.UpdatePreview(Screen(-10.5f, -3.5f));
            Check(Mathf.IsEqualApprox(ghost.ImageRect.Size.X, originalSize * 2), "Image footprint scales with camera zoom");
            Units.Camera.Size *= 2;
            for (int i = 0; i < 30; i++)
            {
                Placement.Cancel();
                ChooseBuilding();
                Placement.UpdatePreview(Screen(-10.5f, -3.5f));
            }
            Check(ghost == Placement.GetNode<PlacementPreviewImage>("PreviewCanvas/BuildPreview") && ghost.Texture == texture &&
                Placement.GetNode("PreviewCanvas").GetChildCount() == 1, "Repeated clicks reuse one image node and texture without growing the tree");
            await Capture("valid", -10.5f, -3.5f);

            Check(Issue(-20, 0)?.Contains("맵 밖") == true, "Entire footprint must fit inside the map");
            Check(Issue(1, -8)?.Contains("벽이나 나무") == true, "Walls block construction");
            Check(Issue(1, 0)?.Contains("벽이나 나무") == true, "A two-cell tree blocks its whole footprint");
            Receive("TREE 581 0");
            Check(Issue(1, 0) == null, "Server-confirmed tree removal opens its footprint");
            Check(Issue(-12, 2)?.Contains("유닛") == true, "The builder itself also occupies ground");
            Receive("BUILDING 3 501 1 -10.5 -3.5 0");
            Receive("HP 501 150 200");
            long before = _wire.Length;
            Click(-10.5f, -3.5f);
            Check(_wire.Length == before && Placement.Active && !Placement.CanPlace && Placement.Status.Text.Contains("다른 건물"),
                "Blocked click shows a reason, stays in placement mode and sends nothing");
            Check(ghost.Tint.R > ghost.Tint.G, "Blocked image is red");
            await Capture("blocked", -10.5f, -3.5f);
            Receive("REMOVE 501");
            Receive("UNIT 1 303 0 10 5 2");
            Receive("TICK 100");
            Receive("POS 303 -10.5 -3.5");
            Check(Unit(303).GlobalPosition == new Vector3(10, 0, 5) && Issue(-10.5f, -3.5f)?.Contains("유닛") == true,
                "Occupancy uses the latest received position instead of the delayed render position");
            Receive("HIDE 303");
            Placement.UpdatePreview(Screen(-10.5f, -3.5f));
            Check(Placement.CanPlace, "Hidden/removed units no longer occupy the client's known world");
            before = _wire.Length;
            Click(-10.5f, -3.5f);
            Check(!Placement.Active && _commands.Count == 1 && _commands[0] == "BUILD 3 -10.5 -3.5 101", "One click sends one request using one deterministic selected worker");
            Check(Encoding.UTF8.GetString(_wire.ToArray().AsSpan((int)before)) == "BUILD 3 -10.5 -3.5 101" + System.Environment.NewLine,
                "Main and NetClient write the exact requested BUILD wire format");
            Check(Buildings.LiveBuildings.Count == 0 && Units.SelectedUnitIds.Count == 3, "Request creates no speculative building and preserves selection");

            Receive("BUILDING 3 502 1 -10.5 -3.5 0");
            Check(Buildings.TryGetBuilding(502, out Building supply) && supply.BuildingType == BuildingCatalog.Supply, "A server BUILDING response creates quarters");
            Invoke("SelectBuilding", supply);
            var details = GetNode<SelectionDetails>("SelectionUI/SelectionDetails");
            var title = (Label)typeof(SelectionDetails).GetField("_title", Private).GetValue(details);
            Check(title.Text == "합숙소", "New building selection uses its correct name");
            Invoke("SelectUnit", Unit());
            ChooseBuilding(9);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Click(-5.5f, -3.5f);
            Check(_commands.Count == 2 && _commands[1] == "BUILD 4 -5.5 -3.5 101", "Barracks aligns its 5x3 footprint and writes invariant fractional coordinates");
            Receive("BUILDING 4 503 1 8 -4 0");
            Check(Buildings.TryGetBuilding(503, out Building barracks) && barracks.BuildingType == BuildingCatalog.Barracks, "Server responses support both new building types");
            Check(Issue(8, -1.9f, 1, 1) == null, "A 5x3 building does not incorrectly block a 3x3 square");
            Receive("BUILDING 4 503 1 8 -4 1.5707963");
            Check(Issue(8, -1.9f, 1, 1) == null, "Server yaw and visual facing do not rotate the server-aligned footprint");
            Check(Issue(-8, -3.5f) == null && Issue(-8.1f, -3.5f)?.Contains("다른 건물") == true, "Touching edges are allowed while partial footprint overlap is blocked");

            foreach (uint unavailable in new[] { BuildingCatalog.TownHall, BuildingCatalog.Fortress })
                Check(!Placement.Begin(unavailable) && !Units.RequestBuild(unavailable, new Vector3(-4, 0, -8), 101),
                    $"Type {unavailable} is unavailable for player construction in both preview and command paths");
            ChooseBuilding(7);
            Placement.UpdatePreview(Screen(-5.5f, -9.5f));
            Check(Placement.Active && Placement.CanPlace && ghost.Texture != texture && Placement.Status.Text.Contains("저장소 배치"),
                "Tier-one slot 7 selects the cached 3x3 store preview");
            await Capture("store", -5.5f, -9.5f);
            before = _wire.Length;
            int buildingsBefore = Buildings.LiveBuildings.Count;
            Click(-5.5f, -9.5f);
            Check(_commands.Count == 3 && _commands[2] == "BUILD 2 -5.5 -9.5 101" && !Placement.Active &&
                Encoding.UTF8.GetString(_wire.ToArray().AsSpan((int)before)) == "BUILD 2 -5.5 -9.5 101" + System.Environment.NewLine &&
                Buildings.LiveBuildings.Count == buildingsBefore, "Store sends exactly one type 2 BUILD and waits for the server");
            Receive("BUILDING 2 504 1 -5.5 -9.5 0");
            Check(Buildings.TryGetBuilding(504, out Building store) && store.BuildingType == BuildingCatalog.Store &&
                store.GetMeta("footprint").AsVector2I() == new Vector2I(3, 3), "Type 2 response creates the 3x3 store");
            Invoke("SelectBuilding", store);
            Check(title.Text == "저장소", "Store selection displays its synchronized name");
            Invoke("SelectUnit", Unit());
            ChooseBuilding(4);
            Placement.UpdatePreview(Screen(-5.5f, -3.5f));
            Check(Placement.Active && Placement.CanPlace && Placement.Status.Text.Contains("대장간 배치"),
                "Tier-one slot 4 selects the cached 5x3 forge preview");
            await Capture("forge", -5.5f, -3.5f);
            before = _wire.Length;
            buildingsBefore = Buildings.LiveBuildings.Count;
            Click(-5.5f, -3.5f);
            Check(_commands.Count == 4 && _commands[3] == "BUILD 5 -5.5 -3.5 101" && !Placement.Active &&
                Encoding.UTF8.GetString(_wire.ToArray().AsSpan((int)before)) == "BUILD 5 -5.5 -3.5 101" + System.Environment.NewLine &&
                Buildings.LiveBuildings.Count == buildingsBefore, "Forge sends exactly one type 5 BUILD and waits for the server");
            Receive("BUILDING 5 505 1 -5.5 -3.5 0");
            Check(Buildings.TryGetBuilding(505, out Building forge) && forge.BuildingType == BuildingCatalog.Forge &&
                forge.GetMeta("footprint").AsVector2I() == new Vector2I(5, 3), "Type 5 response creates the 5x3 forge");
            Invoke("SelectBuilding", forge);
            Check(title.Text == "대장간", "Forge selection displays its synchronized name");
            Invoke("SelectUnit", Unit());
            ChooseBuilding(5);
            before = _wire.Length;
            Click(-5.5f, 3.5f);
            Check(_commands.Count == 5 && _commands[4] == "BUILD 6 -5.5 3.5 101" && !Placement.Active &&
                Encoding.UTF8.GetString(_wire.ToArray().AsSpan((int)before)) == "BUILD 6 -5.5 3.5 101" + System.Environment.NewLine,
                "Tower uses S menu entry and sends its new 3x3 grid-aligned footprint");
            Receive("BUILDING 6 506 1 6 -8 0");
            Check(Buildings.TryGetBuilding(506, out Building tower) && tower.BuildingType == BuildingCatalog.Tower &&
                tower.GetMeta("footprint").AsVector2I() == new Vector2I(3, 3) && tower.IsDefense,
                "Type 6 response creates the small tower rather than a 3x3 fortress");
            Invoke("SelectBuilding", tower);
            Check(title.Text == "포탑", "Small tower displays its separate name");
            Check(Issue(6, -8)?.Contains("다른 건물") == true, "The confirmed tower blocks overlapping construction");
            Invoke("SelectUnit", Unit());

            ChooseBuilding();
            using (var right = new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = true }) _input._Input(right);
            _input._PhysicsProcess(0);
            Check(!Placement.Active && _commands.Count == 5, "Right click cancels without leaking a MOVE order");
            ChooseBuilding();
            using (var escape = new InputEventKey { Keycode = Key.Escape, Pressed = true }) _input._Input(escape);
            Check(!Placement.Active, "Escape cancels placement");
            ChooseBuilding();
            GetWindow().EmitSignal(Window.SignalName.FocusExited);
            Check(!Placement.Active, "Losing focus removes the ghost");
            ChooseBuilding();
            using (var attack = new InputEventKey { Keycode = Key.A, Pressed = true }) _input._UnhandledInput(attack);
            Check(!Placement.Active && _input.IsAttackTargeting, "Attack targeting replaces construction cleanly");
            ChooseBuilding();
            Check(!_input.IsAttackTargeting && Placement.Active, "Construction replaces attack targeting cleanly");

            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Vector2 buttonPoint = Slot(8).GetGlobalRect().GetCenter();
            using (var motion = new InputEventMouseMotion { Position = buttonPoint, GlobalPosition = buttonPoint })
                GetViewport().PushInput(motion, true);
            Placement.UpdatePreview(buttonPoint);
            Check(!Placement.CanPlace && !ghost.Visible,
                $"UI hover hides the world preview: hovered={GetViewport().GuiGetHoveredControl()}, point={buttonPoint}");
            using (var press = new InputEventMouseButton { Position = buttonPoint, GlobalPosition = buttonPoint, ButtonIndex = MouseButton.Left, Pressed = true })
                GetViewport().PushInput(press, true);
            using (var release = new InputEventMouseButton { Position = buttonPoint, GlobalPosition = buttonPoint, ButtonIndex = MouseButton.Left, Pressed = false })
                GetViewport().PushInput(release, true);
            Check(_commands.Count == 5, "Clicking a UI button never places a building underneath it");
            using (var motion = new InputEventMouseMotion { Position = Screen(-4, -4), GlobalPosition = Screen(-4, -4) })
                GetViewport().PushInput(motion, true);

            Invoke("SelectUnit", Unit(201));
            Check(!Placement.Active && Slot(1).Disabled, "Changing to a combat-only selection cancels placement");
            Invoke("SelectUnit", Unit());
            ChooseBuilding();
            Receive("REMOVE 101");
            Check(!Placement.Active, "Losing the selected worker cancels placement");
            Invoke("SelectUnit", Unit(102));
            ChooseBuilding();
            Map.StopSync();
            Check(!Placement.TryPlace(Screen(-4, -4)) && _commands.Count == 5, "Unsynchronized worlds cannot send construction requests");
            BeginSession();
            Receive("UNIT 0 101 7 -12 2 1");
            Invoke("SelectUnit", Unit());
            ChooseBuilding();
            Receive($"MAP 2 {Map.MapHash}");
            Check(!Placement.Active, "Map resynchronization clears placement and stale worker identity");
            Receive("WORLD_READY"); Receive("WELCOME 7 1"); Receive("UNIT 0 101 7 -12 2 1");
            Invoke("SelectUnit", Unit());
            ChooseBuilding();
            Invoke("OnConnectionClosed", "Placement test disconnect");
            Check(!Placement.Active && Units.LiveUnits.Count == 0, "Disconnect cleans the ghost and selected worker");
            GD.Print("PASS: worker construction menu, translucent ghost, terrain/tree/unit/server-aligned building occupancy, latest server positions, exact BUILD framing, one request per click, server-only creation, cancel/UI/sync/reconnect gates");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
        finally { CultureInfo.CurrentCulture = oldCulture; }
    }

    private void BeginSession()
    {
        Receive($"MAP 2 {Map.MapHash}"); Receive("WORLD_READY"); Receive("WELCOME 7 1");
    }

    private void Click(float x, float z)
    {
        using var press = new InputEventMouseButton { Position = Screen(x, z), ButtonIndex = MouseButton.Left, Pressed = true };
        _input._UnhandledInput(press);
        using var release = new InputEventMouseButton { Position = Screen(x, z), ButtonIndex = MouseButton.Left, Pressed = false };
        _input._UnhandledInput(release);
        _input._PhysicsProcess(0);
    }

    private static bool HasGameplayNode(Node node)
    {
        if (node is Building or CollisionObject3D or CollisionShape3D or HealthBar or MeshInstance3D or Camera3D or SubViewport) return true;
        foreach (Node child in node.GetChildren()) if (HasGameplayNode(child)) return true;
        return false;
    }

    private async System.Threading.Tasks.Task Capture(string name, float x, float z)
    {
        if (!_capture) return;
        Fog.RefreshVision();
        Placement.UpdatePreview(Screen(x, z));
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using Image image = GetViewport().GetTexture().GetImage();
        image.SavePng($"res://.godot/build-placement-{name}.png");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
