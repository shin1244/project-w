using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

// 실제 Main 연결과 Viewport 입력 경로를 사용하며 네트워크 출력만 메모리에 기록합니다.
public partial class RtsControlChecks : Main
{
    [Export] public bool MinionOnly { get; set; }
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<string> _commands = new();
    private PlayerInput _input;

    public override async void _Ready()
    {
        try
        {
            var net = GetNode<NetClient>("/root/Net");
            using var wire = new MemoryStream();
            using var writer = new StreamWriter(wire, new UTF8Encoding(false)) { AutoFlush = true };
            typeof(NetClient).GetField("_writer", Private).SetValue(net, writer);
            typeof(Main).GetField("_net", Private).SetValue(this, net);
            InvokeMain("ConnectInput");
            Units.CommandRequested += command => { _commands.Add(command); InvokeMain("SendCommand", command); };
            _input = GetNode<PlayerInput>("PlayerInput");
            Fog.Configure(Map);
            if (MinionOnly)
            {
                await CheckMinionFormation(wire);
                GD.Print("PASS: minion formation, purchases and drag reordering");
                GetTree().Quit();
                return;
            }
            BeginSession();
            foreach (string message in new[]
            {
                "UNIT 0 101 7 -8 -10 1", "UNIT 0 102 7 0 -10 1", "UNIT 1 103 7 8 -10 1",
                "UNIT 2 104 7 0 0 1", "UNIT 0 105 8 14 -10 2", "UNIT 0 106 9 20 -10 1",
                "UNIT 0 107 7 75 35 1", "BUILDING 0 201 1 -18 -10 0"
            }) Receive(message);
            Units.Camera.Size = 60;
            CameraNavigation.FocusGround(Units.Camera, Vector3.Zero);
            await Flush();

            ClickUnit(101);
            await Flush();
            Expect(101);
            ClickUnit(102, shift: true);
            await Flush();
            Expect(101, 102);
            ClickUnit(101, shift: true);
            await Flush();
            Expect(102);
            ClickUnit(105, shift: true);
            ClickUnit(106, shift: true);
            Click(Units.Camera.UnprojectPosition(new Vector3(40, 0, -15)), MouseButton.Left, shift: true);
            await Flush();
            Expect(102);

            InvokeMain("SelectUnit", Unit(101));
            Rect2 box = UnitBox(101, 102);
            Drag(box.Position, box.End, true);
            await Flush();
            Expect(101, 102);
            Drag(box.End, box.Position, true);
            await Flush();
            Expect();
            InvokeMain("SelectUnit", Unit(104));
            Drag(box.Position, box.End, false);
            await Flush();
            Expect(101, 102);
            ClickUnit(101);
            ClickUnit(101, twice: true);
            await Flush();
            Expect(101, 102); // 다른 소유자와 화면 밖의 같은 종류는 제외합니다.
            ClickUnit(103);
            ClickUnit(101, shift: true);
            ClickUnit(101, shift: true, twice: true);
            await Flush();
            Expect(101, 102, 103);

            // 같은 물리 프레임에 선택 -> 저장 순서를 보존합니다.
            ClickUnit(101);
            ClickUnit(102, shift: true);
            KeyPress(Key.Key1, ctrl: true);
            await Flush();
            ClickUnit(103);
            KeyPress(Key.Key2);
            await Flush();
            Expect(103); // Empty group is a no-op.
            Vector3 beforeRecall = Units.Camera.GlobalPosition;
            KeyPress(Key.Key1);
            await Flush();
            Expect(101, 102);
            Check(Units.Camera.GlobalPosition.IsEqualApprox(beforeRecall), "One group press selects without moving the camera");
            KeyPress(Key.Key1, echo: true);
            await Flush();
            Check(Units.Camera.GlobalPosition.IsEqualApprox(beforeRecall), "Key repeat cannot masquerade as a second tap");
            // 두 키를 한 프레임에 넣어 렌더러/CI의 프레임 시간에 영향받지 않습니다.
            _input.CancelTargeting();
            KeyPress(Key.Key1);
            KeyPress(Key.Key1);
            await Flush();
            Expect(101, 102);
            Check(GroundCenter().DistanceTo(new Vector3(-4, 0, -10)) < .02f, "Double tap focuses the selected group's centroid without changing zoom");
            Check(Mathf.IsEqualApprox(Units.Camera.Size, 60), "Group focus preserves zoom");
            Check(_commands.Count == 0, "Selection and control groups never send network commands");

            // 미니맵 클릭은 선택을 유지하고 공격 대상 지정 모드를 종료합니다.
            KeyPress(Key.A);
            Vector3 panTarget = new(12, 0, 8);
            Click(MiniPoint(panTarget), MouseButton.Left);
            await Flush();
            Expect(101, 102);
            Check(!_input.IsAttackTargeting && GroundCenter().DistanceTo(panTarget) < .03f, "Minimap click pans to map coordinates and cancels A targeting");
            ClickUnit(104);
            await Flush();
            Expect(104);
            Units.RecallControlGroup(1);
            Rect2 footprint = Minimap.CameraRect;
            Check(footprint.HasArea() && Minimap.MapRect.Encloses(footprint), "Camera footprint is clipped to the actual minimap terrain");
            Units.Camera.Size = 30;
            await Flush();
            Check(Minimap.CameraRect.Size.X < footprint.Size.X * .6f, "Zoom shrinks the camera footprint");
            Vector3 dragTarget = new(-20, 0, -12);
            Drag(MiniPoint(panTarget), MiniPoint(dragTarget), false);
            await Flush();
            Check(GroundCenter().DistanceTo(dragTarget) < .03f, "Minimap drag follows the pointer");
            Vector2 outside = new(GetViewport().GetVisibleRect().Size.X - 30, 200);
            Drag(MiniPoint(dragTarget), outside, false);
            await Flush();
            Vector3 afterOutsideRelease = Units.Camera.GlobalPosition;
            Motion(GetViewport().GetVisibleRect().GetCenter());
            await Flush();
            Check(Units.Camera.GlobalPosition.IsEqualApprox(afterOutsideRelease), "Releasing outside the minimap ends panning");
            Expect(101, 102);

            Mouse(MiniPoint(Vector3.Zero), MouseButton.Left, true);
            GetWindow().EmitSignal(Window.SignalName.FocusExited);
            Vector3 afterFocusLoss = Units.Camera.GlobalPosition;
            Motion(MiniPoint(new Vector3(-30, 0, 20)));
            Mouse(MiniPoint(Vector3.Zero), MouseButton.Left, false);
            Check(Units.Camera.GlobalPosition.IsEqualApprox(afterFocusLoss), "Focus loss cancels minimap dragging");
            Mouse(MiniPoint(Vector3.Zero), MouseButton.Left, true);
            KeyPress(Key.Escape);
            Vector3 afterEscape = Units.Camera.GlobalPosition;
            Motion(MiniPoint(new Vector3(-30, 0, 20)));
            Mouse(MiniPoint(Vector3.Zero), MouseButton.Left, false);
            Check(Units.Camera.GlobalPosition.IsEqualApprox(afterEscape), "Escape cancels minimap dragging");

            Vector2 border = Minimap.GetGlobalTransformWithCanvas() * Vector2.One;
            Vector3 beforeBorder = Units.Camera.GlobalPosition;
            Click(border, MouseButton.Left);
            Click(border, MouseButton.Right);
            Click(MiniPoint(Vector3.Zero), MouseButton.WheelUp);
            await Flush();
            Check(_commands.Count == 0 && Units.Camera.GlobalPosition.IsEqualApprox(beforeBorder) && Units.Camera.Size == 30,
                "Minimap padding and wheel input cannot pan, zoom or issue orders");
            Check(!Minimap.TryMapToWorld(new Vector2(float.NaN, 0), out _), "Invalid minimap coordinates are rejected");

            CameraNavigation.FocusGround(Units.Camera, Vector3.Zero);
            await Flush();
            Vector3 destination = new(25, 0, -18);
            // 선택과 미니맵 명령도 같은 입력 큐를 사용합니다.
            ClickUnit(103);
            KeyPress(Key.A);
            Click(MiniPoint(destination), MouseButton.Right);
            await Flush();
            Expect(103);
            Check(!_input.IsAttackTargeting && _commands.Count == 1, "One minimap right-click sends exactly one MOVE and exits A targeting");
            string[] move = _commands.Single().Split(' ');
            Check(move.Length == 4 && move[0] == "MOVE" && move[3] == "103" &&
                Math.Abs(float.Parse(move[1], CultureInfo.InvariantCulture) - destination.X) < .02f &&
                Math.Abs(float.Parse(move[2], CultureInfo.InvariantCulture) - destination.Z) < .02f,
                "Minimap uses world coordinates and the existing type/coordinate/id MOVE format");
            Check(Encoding.UTF8.GetString(wire.ToArray()).Split('\n').Count(line => line.StartsWith("MOVE ")) == 1,
                "Exactly one MOVE reaches the existing network writer");
            Units.ClearSelection();
            Click(MiniPoint(destination), MouseButton.Right);
            await Flush();
            Check(_commands.Count == 1, "No selection sends no minimap command");

            // 제거, 숨김, 소유권 변경 후 같은 ID가 재사용되어도 이전 부대에 복귀하지 않습니다.
            Units.SelectSingle(Unit(101));
            Units.ToggleSelection(Unit(102));
            Units.ToggleSelection(Unit(103));
            KeyPress(Key.Key3, ctrl: true);
            await Flush();
            Receive("HIDE 101");
            Receive("REMOVE 102");
            Receive("UNIT 1 103 9 8 -10 1");
            Receive("UNIT 0 101 7 -8 -10 1");
            Receive("UNIT 0 102 7 0 -10 1");
            Receive("UNIT 1 103 7 8 -10 1");
            Units.SelectSingle(Unit(104));
            KeyPress(Key.Key3);
            await Flush();
            Expect(104);

            Units.Camera.Size = 60;
            CameraNavigation.FocusGround(Units.Camera, Vector3.Zero);
            for (int i = 0; i < 70; i++) Receive($"UNIT 1 {1000 + i} 7 {i % 10 - 5} {i / 10 - 8} 1");
            Units.SelectSameTypeOnScreen(Unit(1000), false);
            Check(Units.SelectedUnitIds.Count == 64 && Units.SelectedUnitIds.Contains(1000u), "Same-type selection retains the clicked unit and enforces the server's 64-unit limit");
            Units.ToggleSelection(Unit(101));
            Check(Units.SelectedUnitIds.Count == 64, "Shift-add cannot exceed the selection limit");
            KeyPress(Key.Key9, ctrl: true);
            await Flush();
            Units.ClearSelection();
            KeyPress(Key.Key9);
            await Flush();
            Check(Units.SelectedUnitIds.Count == 64, "Control groups recall a complete 64-unit group");
            Receive("WELCOME 7 2 COMMANDER");
            Units.ClearSelection();
            KeyPress(Key.Key9);
            await Flush();
            Expect();

            Units.SelectSingle(Unit(101));
            KeyPress(Key.Key4, ctrl: true);
            await Flush();
            Click(MiniPoint(destination), MouseButton.Right);
            Receive($"MAP 2 {Map.MapHash}");
            Receive("WORLD_READY");
            Receive("WELCOME 7 1 COMMANDER");
            Receive("UNIT 0 101 7 -8 -10 1");
            KeyPress(Key.Key4);
            await Flush();
            Expect();
            Check(_commands.Count == 1, "Resync clears groups and pending minimap commands before a new world can reuse IDs");
            Units.SelectSingle(Unit(101));
            KeyPress(Key.Key4, ctrl: true);
            await Flush();
            InvokeMain("OnConnectionClosed", "RTS controls test disconnect");
            KeyPress(Key.Key4);
            Click(MiniPoint(destination), MouseButton.Right);
            await Flush();
            Expect();
            Check(_commands.Count == 1, "Disconnect disables stale group recalls and minimap commands");

            typeof(NetClient).GetField("_writer", Private).SetValue(net, writer);
            await CheckAosControls();
            await CheckGolemSkills();
            await CheckCameraNavigation();
            await CheckBattleClarity();
            await CheckAttackRangeIndicators();
            await CheckWolfWE();
            await CheckMinionFormation(wire);

            GD.Print("PASS: RTS selection/groups/minimap; edge pan and Space hero/base focus; AOS control/inspection, ownership/death/respawn/resync and role changes");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private async Task CheckWolfWE()
    {
        BeginSession();
        Receive("WELCOME 7 1 HERO 200");
        Receive("ABILITY UNIT 200 1 wolf-drain SELF 0 8 0");
        Receive("ABILITY UNIT 200 2 wolf-aura SELF 0 12 0");
        Receive("UNIT 200 1400 7 -6 -8 1");
        Receive("UNIT 200 1401 9 6 -8 1");
        Receive("TICK 600");
        int before = _commands.Count;
        KeyPress(Key.W); KeyPress(Key.E);
        Check(_commands.Skip(before).SequenceEqual(new[] { "SKILL 1", "SKILL 2" }) && !_input.IsSkillTargeting &&
            Units.Camera.GetMeta("hero_w_shortcut").AsBool(), "Wolf W/E cast immediately and W is reserved for the skill");
        var drainButton = Skills.GetNode<Button>("Content/Slots/W");
        var drainCooldown = drainButton.GetNode<Label>("Cooldown");
        var auraButton = Skills.GetNode<Button>("Content/Slots/E");
        var auraCooldown = auraButton.GetNode<Label>("Cooldown");
        Check(drainButton.TooltipText.Contains("50%") && drainButton.TooltipText.Contains("일반 공격 또는 Q") &&
            Skills.GetNode<Button>("Content/Slots/E").TooltipText.Contains("25%") &&
            Skills.GetNode<Button>("Content/Slots/E").TooltipText.Contains("0.5초마다 10회") &&
            Skills.GetNode<Button>("Content/Slots/E").TooltipText.Contains("총 11회"), "W/E descriptions show their actual effects");
        Receive("COOLDOWN 1400 1 0");
        Receive("SKILL_ACTIVE 1400 1 1");
        Receive("COOLDOWN 1400 2 0");
        Receive("SKILL_ACTIVE 1400 2 1");
        Receive("WOLF 1400 1 750");
        Receive("WOLF 1401 0 0");
        await Flush();
        var effects = Unit(1400).GetNode<Node3D>("WolfEffects");
        Check(effects.GetNode<MeshInstance3D>("DrainReady").Visible && effects.GetNode<MeshInstance3D>("DamageAura").Visible &&
            Skills.StatusText.Contains("50%") && Skills.StatusText.Contains("5초"),
            "Authoritative snapshots show W readiness and the remaining E duration for the owned hero");
        Check(!Skills.CanUse(1) && drainButton.Disabled && Skills.RemainingSeconds(1) == 0 && drainCooldown.Text == "대기",
            "Armed W waits for an attack without starting its cooldown or allowing recasts");
        Check(Skills.IsActive(2) && !Skills.CanUse(2) && auraButton.Disabled &&
            Skills.RemainingSeconds(2) == 0 && auraCooldown.Text == "사용 중",
            "E remains active without counting down or allowing recasts");
        foreach (string invalid in new[] { "SKILL_ACTIVE 1400 2 2", "SKILL_ACTIVE 1400 -1 0", "SKILL_ACTIVE 1400 5 0",
            "SKILL_ACTIVE 0 2 0", "SKILL_ACTIVE 1400 2 0 extra", "SKILL_ACTIVE 1401 2 0" }) Receive(invalid);
        Check(Skills.IsActive(2), "Invalid or other-caster active messages cannot clear E");
        foreach (string invalid in new[] { "WOLF 1400 2 0", "WOLF 1400 0 -1", "WOLF 1400 0 NaN", "WOLF 0 0 0", "WOLF 1400 0 0 extra" }) Receive(invalid);
        Check(Unit(1400).WolfEffects.DrainReady && Unit(1400).WolfEffects.AuraUntil == 750,
            "Malformed effect snapshots cannot erase valid state");
        KeyPress(Key.W); KeyPress(Key.E);
        Check(_commands.Count == before + 2, "Armed W and cooling E block repeat casts");
        Receive("TICK 630");
        Check(Skills.StatusText.Contains("4초"), "E duration uses server ticks");
        Receive("TICK 750");
        Check(Skills.IsActive(2) && !Skills.CanUse(2), "E waits for authoritative completion at its final tick");
        Receive("COOLDOWN 1400 2 1110");
        Receive("SKILL_ACTIVE 1400 2 0");
        Receive("WOLF 1400 1 0");
        await Flush();
        Check(effects.GetNode<MeshInstance3D>("DrainReady").Visible && !effects.GetNode<MeshInstance3D>("DamageAura").Visible,
            "E expiration preserves an unused W");
        Check(!Skills.IsActive(2) && Skills.RemainingSeconds(2) == 12 && auraCooldown.Text == "12",
            "E starts its full twelve-second cooldown after its five-second execution");
        Receive("TICK 900");
        KeyPress(Key.W);
        Check(_commands.Count == before + 2 && !Skills.CanUse(1) && Skills.RemainingSeconds(1) == 0 && drainCooldown.Text == "대기",
            "W remains armed after more than eight seconds without an attack");
        Receive("COOLDOWN 1400 1 1140");
        Receive("SKILL_ACTIVE 1400 1 0");
        Receive("WOLF 1400 0 0");
        await Flush();
        Check(!effects.GetNode<MeshInstance3D>("DrainReady").Visible && !Skills.CanUse(1) &&
            Skills.RemainingSeconds(1) == 8 && drainCooldown.Text == "8",
            "Attack consumption removes W readiness and starts its full eight-second server cooldown");
        Receive("TICK 930");
        Check(drainCooldown.Text == "7", "W cooldown counts down after consumption");
        Receive("WOLF 1400 0 1000");
        Receive("REMOVE 1400");
        await Flush();
        Check(!effects.GetNode<MeshInstance3D>("DamageAura").Visible && !Skills.StatusText.Contains("50%"),
            "Death clears visual and HUD buffs");
        Receive("UNIT 200 1402 7 -6 -8 1");
        Check(!Unit(1402).WolfEffects.DrainReady && Unit(1402).WolfEffects.AuraUntil == 0 && !Skills.CanUse(1),
            "Respawn starts without buffs while retaining the server cooldown");
        Receive("TICK 1140");
        KeyPress(Key.W);
        Check(Skills.CanUse(1) && _commands.Count == before + 3 && _commands.Last() == "SKILL 1",
            "W can be armed again after its post-attack cooldown expires");
        Receive("COOLDOWN 1402 1 0");
        Receive("SKILL_ACTIVE 1402 1 1");
        Check(Skills.IsActive(1) && Skills.RemainingSeconds(1) == 0 && !Skills.CanUse(1),
            "A repeated execution clears the old cooldown and blocks recasting");
    }

    private async Task CheckAttackRangeIndicators()
    {
        BeginSession();
        Units.Camera.Size = 60;
        CameraNavigation.FocusGround(Units.Camera, Vector3.Zero);
        foreach (string message in new[]
        {
            "BODY UNIT 1 0.5", "BODY UNIT 2 0.6",
            "UNIT 1 1300 7 -6 -8 1", "UNIT 2 1301 7 6 -8 1", "UNIT 0 1302 7 0 -8 1",
            "UNIT 2 1303 8 12 -8 2", "UNIT 2 1304 9 18 -8 1",
            "STATS 1300 20 1 1 5 10", "STATS 1302 0 0 0 5 10",
            "STATS 1303 20 8 1 5 10", "STATS 1304 20 8 1 5 10"
        }) Receive(message);
        Units.SelectSingle(Unit(1300));
        Units.ToggleSelection(Unit(1301));
        Units.ToggleSelection(Unit(1302));
        int before = _commands.Count;
        KeyPress(Key.A);
        Check(Ring(1300) is { Visible: true } && Ring(1301) == null && Ring(1302) == null &&
            Ring(1303) == null && Ring(1304) == null && _commands.Count == before,
            "A previews only selected controllable attackers with known stats, without issuing orders");
        Receive("STATS 1301 20 8 1 5 10");
        Check(Mathf.IsEqualApprox(Radius(1300), 1.5f) && Mathf.IsEqualApprox(Radius(1301), 8.6f),
            "Mixed selections use each unit's authoritative range plus collision body, including late stats");
        Receive("STATS 1301 20 10 1 5 10");
        Receive("BODY UNIT 2 0.8");
        Check(Mathf.IsEqualApprox(Radius(1301), 10.8f), "Range and body updates refresh an active preview");
        Unit(1301).Position += Vector3.Right;
        Check(Ring(1301).GlobalPosition.DistanceTo(Unit(1301).GlobalPosition + Vector3.Up * .07f) < .001f,
            "Range preview follows the rendered unit position");
        KeyPress(Key.Escape);
        Check(Ring(1300) == null && Ring(1301) == null, "Esc removes all attack range previews");
        KeyPress(Key.A);
        Click(Units.Camera.UnprojectPosition(new Vector3(12, 0, 3)), MouseButton.Left);
        await Flush();
        Check(!_input.IsAttackTargeting && Ring(1300) == null && Ring(1301) == null &&
            _commands.Count == before + 1 && _commands.Last().StartsWith("ATTACK_MOVE "),
            "Attack-move click removes the previews and retains the existing order");
        KeyPress(Key.A);
        Units.SelectSingle(Unit(1301));
        Check(Ring(1300) == null && Ring(1301) != null, "Changing selection replaces the active previews");
        GetWindow().EmitSignal(Window.SignalName.FocusExited);
        Check(!_input.IsAttackTargeting && Ring(1301) == null, "Focus loss cancels the preview");
        KeyPress(Key.A);
        Unit hidden = Unit(1301);
        Receive("HIDE 1301");
        Check(hidden.GetNodeOrNull<MeshInstance3D>("AttackRangeRing") == null, "HIDE removes a stale preview immediately");

        foreach (uint type in new[] { UnitCatalog.HeroTest, UnitCatalog.HeroGolem })
        {
            BeginSession();
            Receive($"WELCOME 7 1 HERO {type}");
            Receive($"UNIT {type} 1310 7 -6 -8 1");
            Receive($"UNIT {type} 1311 9 6 -8 1");
            Receive("STATS 1310 24 1 1.2 5 10");
            Receive("STATS 1311 24 1 1.2 5 10");
            UnitInfo.Inspect(Unit(1311));
            KeyPress(Key.A);
            Check(Ring(1310) != null && Ring(1311) == null, "Hero preview follows ownership, not the inspected ally");
            Click(Units.Camera.UnprojectPosition(new Vector3(12, 0, 3)), MouseButton.Right);
            await Flush();
            Check(!_input.IsAttackTargeting && Ring(1310) == null, "Right-click exits the attack preview");
            KeyPress(Key.A);
            Unit dead = Unit(1310);
            Receive("REMOVE 1310");
            Check(dead.GetNodeOrNull<MeshInstance3D>("AttackRangeRing") == null, "Death clears the ring before its death animation");
            Receive($"UNIT {type} 1312 7 -6 -8 1");
            Receive("STATS 1312 24 1 1.2 5 10");
            KeyPress(Key.A);
            Check(Ring(1312) != null, "Respawn uses the new hero ID");
            Receive($"ABILITY UNIT {type} 0 test-target ENEMY 7 4 0");
            KeyPress(Key.Q);
            Check(Ring(1312) == null && Unit(1312).GetNodeOrNull<MeshInstance3D>("SkillRangeRing") != null,
                "Skill targeting replaces the attack preview with the skill's own range");
            KeyPress(Key.A);
            Check(Ring(1312) != null && Unit(1312).GetNodeOrNull<MeshInstance3D>("SkillRangeRing") == null,
                "A replaces the skill range preview");
            Unit old = Unit(1312);
            BeginSession();
            Check(old.GetNodeOrNull<MeshInstance3D>("AttackRangeRing") == null && !_input.IsAttackTargeting,
                "Map reset removes active previews and targeting");
        }

        MeshInstance3D Ring(uint id) => Unit(id).GetNodeOrNull<MeshInstance3D>("AttackRangeRing");
        float Radius(uint id) => (((TorusMesh)Ring(id).Mesh).InnerRadius + ((TorusMesh)Ring(id).Mesh).OuterRadius) / 2;
    }

    private async Task CheckAosControls()
    {
        BeginSession();
        Receive("WELCOME 7 1 HERO 200");
        Units.Camera.Size = 60;
        CameraNavigation.FocusGround(Units.Camera, Vector3.Zero);
        await Flush();
        int before = _commands.Count;
        Vector3 destination = new(12, 0, 3);
        Vector2 ground = Units.Camera.UnprojectPosition(destination);
        Click(ground, MouseButton.Right);
        await Flush();
        Check(_commands.Count == before, "AOS waits for its own hero before sending orders");

        Receive("UNIT 200 501 8 8 -8 2");
        Receive("UNIT 200 502 9 -8 -8 1");
        Receive("UNIT 0 503 7 -12 -15 1");
        Receive("BUILDING 0 201 1 -20 -20 0");
        Expect();
        Receive("UNIT 200 500 7 0 -8 1");
        await Flush();
        Expect(500);
        Check(!Commands.Visible && UnitInfo.Visible && UnitInfo.DisplayedUnit == Unit(500),
            "AOS shows the own hero in the compact information panel without clicking");
        Check(Unit(500).GetNode<MeshInstance3D>("SelectionRing").Visible && !Units.CanControl(Unit(503)),
            "AOS automatically selects only the matching owned hero, even when other owned units exist");
        Click(ground, MouseButton.Right);
        await Flush();
        Check(_commands.Count == before + 1 && _commands.Last().StartsWith("MOVE ") && _commands.Last().EndsWith(" 500"),
            "First world right-click moves the hero without a prior selection click");

        ClickUnit(501);
        ClickUnit(502);
        ClickUnit(503, shift: true);
        ClickUnit(500, shift: true, twice: true);
        Click(ground, MouseButton.Left);
        Drag(UnitBox(501, 502).Position, UnitBox(501, 502).End, false);
        Click(Units.Camera.UnprojectPosition(new Vector3(-20, 1, -20)), MouseButton.Left);
        await Flush();
        Units.ClearSelection();
        Units.SelectFromPortrait(500, true, false);
        KeyPress(Key.Key1, ctrl: true);
        KeyPress(Key.Key1);
        await Flush();
        Expect(500);
        Check(Units.InspectedUnit == null && Buildings.SelectedBuilding == null && _commands.Count == before + 1 &&
            UnitInfo.DisplayedUnit == Unit(500),
            "Empty clicks, other units, buildings, drag, Shift and group inputs cannot replace or clear the AOS hero");

        ClickUnit(501);
        await Flush();
        Expect(500);
        Check(UnitInfo.InspectedUnit == Unit(501), "AOS left click inspects the enemy while the own hero stays controlled");
        Click(UnitPoint(501), MouseButton.Right);
        KeyPress(Key.A);
        Click(ground, MouseButton.Left);
        KeyPress(Key.S);
        KeyPress(Key.D);
        Click(MiniPoint(destination), MouseButton.Right);
        await Flush();
        string[] orders = _commands.Skip(before + 1).ToArray();
        Check(orders.Length == 5 && orders[0] == "ATTACK 501 500" && orders[1].StartsWith("ATTACK_MOVE ") &&
            orders[2] == "STOP 500" && orders[3] == "HOLD 500" && orders[4].StartsWith("MOVE ") &&
            orders.All(order => order.EndsWith(" 500")) && UnitInfo.InspectedUnit == Unit(501),
            "World, A/S/D and minimap orders address the hero while preserving the inspected enemy");
        Click(UnitInfo.GetGlobalRect().GetCenter(), MouseButton.Right);
        await Flush();
        Check(_commands.Count == before + 6, "Read-only information panel consumes right clicks without issuing orders");

        Receive("UNIT 200 500 9 0 -8 1");
        Expect();
        before = _commands.Count;
        Click(MiniPoint(destination), MouseButton.Right);
        await Flush();
        Check(_commands.Count == before, "Losing ownership immediately disables hero commands");
        Receive("UNIT 200 504 7 0 -8 1");
        Expect(504);
        Receive("REMOVE 504");
        Expect();
        Units.RequestMove(destination);
        Units.RequestStop();
        Check(_commands.Count == before, "A dead hero cannot receive commands");
        Receive("UNIT 200 505 7 0 -8 1");
        Expect(505);
        Receive("HIDE 505");
        Expect();
        Receive("UNIT 200 505 7 0 -8 1");
        Expect(505);
        Click(MiniPoint(destination), MouseButton.Right);
        Receive($"MAP 2 {Map.MapHash}");
        Receive("WORLD_READY");
        Receive("WELCOME 7 1 HERO 200");
        Receive("UNIT 200 506 7 0 -8 1");
        await Flush();
        Expect(506);
        Check(_commands.Count == before, "Resync binds the new hero and drops pending commands for the old session");

        await CheckWolfSkill();
        await CheckSkillDefinitions();
        before = _commands.Count;
        Receive("WELCOME 7 1 COMMANDER");
        KeyPress(Key.Q);
        Check(!_input.IsSkillTargeting && !Skills.CanUse(0), "Returning to RTS removes the hero Q shortcut");
        Receive("UNIT 0 507 7 -8 -8 1");
        Expect();
        await Flush();
        ClickUnit(507);
        await Flush();
        Expect(507);
        Click(ground, MouseButton.Left);
        await Flush();
        Expect();
        Check(_commands.Count == before, "Returning to RTS restores manual selection and deselection");
    }

    private async Task CheckWolfSkill()
    {
        Receive("ABILITY UNIT 200 0 wolf-bite ENEMY 6 6 1");
        Receive("UNIT 1 950 8 3 -8 2");
        Receive("UNIT 1 951 9 -4 -8 1");
        Receive("TICK 100");
        await Flush();
        var button = Skills.GetNode<Button>("Content/Slots/Q");
        var cooldown = (Label)button.FindChild("Cooldown", true, false);
        int before = _commands.Count;
        Check(Skills.CanUse(0) && !button.Disabled, "A live owned wolf can arm Q without selecting itself");
        ClickUnit(950);
        await Flush();
        KeyPress(Key.Q, ctrl: true);
        KeyPress(Key.Q, echo: true);
        Check(!_input.IsSkillTargeting, "Modified and repeated Q presses do not arm the skill");
        KeyPress(Key.Q);
        Check(_input.IsSkillTargeting && !_input.IsAttackTargeting, "Q arms enemy targeting without issuing a command");
        Check(Unit(506).GetNodeOrNull<MeshInstance3D>("SkillRangeRing") is { Visible: true } &&
            Unit(950).GetNodeOrNull<MeshInstance3D>("SkillRangeRing") == null,
            "Q shows range around the controlled hero, not the inspected enemy");
        if (OS.GetCmdlineUserArgs().Contains("--capture-skill-range") || OS.GetCmdlineUserArgs().Contains("--capture-shield-tooltip"))
        {
            Receive("SIGHT UNIT 200 10");
            Receive("HP 506 300 300");
            Fog.RefreshVision();
            await Flush();
            if (OS.GetCmdlineUserArgs().Contains("--capture-shield-tooltip"))
            {
                GetTree().Root.GuiEmbedSubwindows = true;
                Receive("SHIELD 506 160");
                Motion(Skills.GetNode<Control>("Content/Slots/R").GetGlobalRect().GetCenter());
                await ToSignal(GetTree().CreateTimer(1), SceneTreeTimer.SignalName.Timeout);
            }
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using Image image = GetViewport().GetTexture().GetImage();
            image.SavePng(OS.GetCmdlineUserArgs().Contains("--capture-shield-tooltip")
                ? "res://.godot/shield-tooltip.png" : "res://.godot/wolf-q-range.png");
        }
        ClickUnit(951);
        Click(Units.Camera.UnprojectPosition(new Vector3(15, 0, -8)), MouseButton.Left);
        await Flush();
        Check(_commands.Count == before && _input.IsSkillTargeting, "Allies and ground cannot consume a unit-targeted skill");
        ClickUnit(950);
        ClickUnit(950);
        await Flush();
        Check(_commands.Count == before + 1 && _commands.Last() == "SKILL 0 950" && !_input.IsSkillTargeting &&
            Skills.CanUse(0) && UnitInfo.InspectedUnit == Unit(950) && Units.SelectedUnitIds.SequenceEqual(new uint[] { 506 }) &&
            Unit(506).GetNodeOrNull<MeshInstance3D>("SkillRangeRing") == null,
            "Q sends exactly slot and enemy ID, preserves hero control/inspection, and waits for server cooldown");
        Receive("SKILL 506 0 950");
        Receive("STATE 506 DASH 0 950 0");
        Check(Unit(506).State.Activity == UnitActivity.Dash, "Server DASH is accepted for facing and wolf presentation");
        Receive("COOLDOWN 506 0 220");
        Check(!Skills.CanUse(0) && button.Disabled && cooldown.Text == "4", "COOLDOWN 506 uses 30Hz server ticks, not a copied skill duration");
        KeyPress(Key.Q);
        Click(button.GetGlobalRect().GetCenter(), MouseButton.Left);
        await Flush();
        Check(!_input.IsSkillTargeting && _commands.Count == before + 1, "Cooldown blocks both keyboard and button casts");
        Receive("TICK 160");
        foreach (string invalid in new[] { "COOLDOWN 506 -1 220", "COOLDOWN 506 5 220", "COOLDOWN 506 0 -1", "COOLDOWN 506 0 NaN",
            "COOLDOWN 506 0 4294967296", "COOLDOWN 506 0 220 extra", "COOLDOWN 506 1 500", "COOLDOWN 506 0 180", "TICK 120" }) Receive(invalid);
        Check(cooldown.Text == "2" && Skills.RemainingSeconds(0) == 2, "Malformed, foreign-slot and stale timing messages cannot alter Q cooldown");
        Receive("TICK 220");
        Receive("STATE 506 IDLE 0 0 0");
        Check(Skills.CanUse(0) && cooldown.Text == "", "The server ready tick re-enables Q");

        Click(button.GetGlobalRect().GetCenter(), MouseButton.Left);
        Check(_input.IsSkillTargeting && Unit(506).GetNodeOrNull<MeshInstance3D>("SkillRangeRing") is { Visible: true },
            "The Q button arms targeting and shows the same range as the key");
        ClickUnit(950);
        KeyPress(Key.Escape);
        await Flush();
        Check(_commands.Count == before + 1 && !_input.IsSkillTargeting &&
            Unit(506).GetNodeOrNull<MeshInstance3D>("SkillRangeRing") == null,
            "Escape removes the range and cancels even a skill click awaiting the physics frame");
        KeyPress(Key.Q);
        Click(UnitPoint(950), MouseButton.Right);
        await Flush();
        Check(_commands.Count == before + 1 && !_input.IsSkillTargeting, "Right click cancels Q without issuing an accidental attack/move");
        KeyPress(Key.Q);
        GetWindow().EmitSignal(Window.SignalName.FocusExited);
        Check(!_input.IsSkillTargeting, "Window focus loss cancels skill targeting");
        KeyPress(Key.Q);
        KeyPress(Key.A);
        Check(!_input.IsSkillTargeting && _input.IsAttackTargeting, "Attack targeting replaces Q targeting");
        KeyPress(Key.Q);
        Check(_input.IsSkillTargeting && !_input.IsAttackTargeting, "Q replaces attack targeting");
        Receive("STATE 506 STUN 0 0 0");
        Check(Unit(506).State.Activity == UnitActivity.Stun && !Skills.CanUse(0) && !_input.IsSkillTargeting,
            "Server stun immediately cancels Q and disables the skill");
        Receive("STATE 506 IDLE 0 0 0");
        Click(button.GetGlobalRect().GetCenter(), MouseButton.Left);
        ClickUnit(950);
        await Flush();
        Check(_commands.Count == before + 2 && _commands.Last() == "SKILL 0 950", "Button targeting sends the same skill request after stun ends");
        Receive("COOLDOWN 506 0 340");
        Receive("REMOVE 506");
        KeyPress(Key.Q);
        Receive("UNIT 200 952 7 0 -8 1");
        Check(!Skills.CanUse(0) && Skills.HeroUnitId == 952 && Skills.RemainingSeconds(0) == 4,
            "Cooldown belongs to the player and survives hero death and a new unit ID");
        Receive("TICK 340");
        KeyPress(Key.Q);
        ClickUnit(950);
        Receive($"MAP 2 {Map.MapHash}");
        Receive("WORLD_READY");
        Receive("WELCOME 7 1 HERO 200");
        Receive("ABILITY UNIT 200 0 wolf-bite ENEMY 6 6 1");
        Receive("UNIT 200 953 7 0 -8 1");
        await Flush();
        Check(_commands.Count == before + 2 && !_input.IsSkillTargeting && Skills.CanUse(0) && cooldown.Text == "",
            "Resync drops queued skill requests and previous cooldowns before binding the new hero");
    }

    private async Task CheckSkillDefinitions()
    {
        Receive("ABILITY UNIT 200 1 blessing ALLY 9 12 0");
        Receive("ABILITY UNIT 200 2 ground-burst POINT 8 4 0");
        Receive("ABILITY UNIT 200 3 haste SELF 0 10 0");
        Receive("UNIT 1 960 8 3 -8 2");
        Receive("UNIT 1 961 9 -4 -8 1");
        await Flush();
        int before = _commands.Count;
        KeyPress(Key.W);
        ClickUnit(960);
        await Flush();
        Check(_input.IsSkillTargeting && _commands.Count == before, "Ally skills reject enemy targets");
        ClickUnit(961);
        await Flush();
        Check(_commands.Count == before + 1 && _commands.Last() == "SKILL 1 961", "W targets another player's ally using its server definition");
        KeyPress(Key.E);
        Click(Units.Camera.UnprojectPosition(new Vector3(10, 0, -8)), MouseButton.Left);
        await Flush();
        Check(_commands.Count == before + 2 && _commands.Last().StartsWith("SKILL 2 ") && _commands.Last().Split(' ').Length == 4,
            "Point skills send coordinates through the same targeting flow");
        KeyPress(Key.R);
        Check(_commands.Count == before + 3 && _commands.Last() == "SKILL 3" && !_input.IsSkillTargeting,
            "Self skills cast without a target click");
        KeyPress(Key.W);
        Receive("CONTROL 953 4");
        Check(!_input.IsSkillTargeting && !Skills.CanUse(1), "Silence cancels targeting independently of displayed activity");
        Receive("CONTROL 953 2");
        Check(!Skills.CanUse(0) && Skills.CanUse(1), "Root blocks movement skills and permits stationary skills");
        Receive("CONTROL 953 8");
        Check(Skills.CanUse(0) && Skills.CanUse(1), "Disarm leaves skills available");
        Receive("CONTROL 953 0");
        Receive("COOLDOWN 960 1 900");
        foreach (string invalid in new[] { "ABILITY UNIT 200 1 blessing ALLY NaN 12 0", "ABILITY UNIT 200 1 ../bad ALLY 9 12 0",
            "ABILITY UNIT 200 1 blessing ALLY 9 12 2", "CONTROL 953 16", "UNIT 200 962 7 NaN -8 1" }) Receive(invalid);
        Check(Skills.Definition(1)?.Range == 9 && Skills.CanUse(1) && Skills.HeroUnitId == 953,
            "Malformed definitions, controls, unit positions and another caster's cooldown do not change the skill state");
        foreach (string valid in new[] { "SKILL 953 1 961", "SKILL 953 2 10 -8", "SKILL 953 3" })
            Check(SkillActivationSnapshot.TryParse(valid.Split(' '), out _), "All activation forms support presentation");
        Check(!SkillActivationSnapshot.TryParse("SKILL 953 2 NaN 0".Split(' '), out _), "Invalid activation coordinates are rejected");
    }

    private async Task CheckGolemSkills()
    {
        BeginSession();
        Receive("WELCOME 7 1 HERO 201");
        foreach (string message in new[] {
            "ABILITY UNIT 201 0 golem-slam SELF 0 6 0", "ABILITY UNIT 201 1 golem-shell SELF 0 12 0",
            "ABILITY UNIT 201 2 golem-charge ENEMY 7 10 1", "ABILITY UNIT 201 3 golem-quake SELF 0 30 0",
            "EXP 1 0 100", "UNIT 201 970 7 0 -8 1", "HP 970 420 420", "UNIT 1 971 8 3 -8 2" }) Receive(message);
        await Flush();
        Expect(970);
        Check(Unit(970).GetNodeOrNull<GolemAnimation>("GolemAnimation") != null &&
            UnitInfo.Experience is { Level: 1, Current: 0 }, "Golem binds its model and initial experience");
        Check(Units.Camera.GetMeta("hero_w_shortcut").AsBool(), "Golem reserves W for its shield, including cooldown");
        int before = _commands.Count;
        KeyPress(Key.Q); KeyPress(Key.W); KeyPress(Key.R);
        Check(_commands.Skip(before).SequenceEqual(new[] { "SKILL 0", "SKILL 1", "SKILL 3" }),
            "Golem Q/W/R cast immediately without a target click");
        KeyPress(Key.E); ClickUnit(971);
        await Flush();
        Check(_commands.Last() == "SKILL 2 971", "Golem E selects an enemy through the normal input path");
        Receive("SKILL 970 0"); Receive("SKILL 970 1"); Receive("SHIELD 970 105"); Receive("COOLDOWN 970 1 360");
        Receive("SKILL 970 3");
        await Flush();
        Check(Unit(970).GetNode<MeshInstance3D>("GolemAnimation/StoneShield").Visible && !Skills.CanUse(1),
            "Server shield activates the effect and cooldown blocks W");
        Receive("SHIELD 970 0"); Receive("CONTROL 970 2");
        await Flush();
        Check(!Unit(970).GetNode<MeshInstance3D>("GolemAnimation/StoneShield").Visible &&
            !Skills.CanUse(2) && Skills.CanUse(0), "Shield depletion hides the effect; root only blocks the dash");

        var golem = Unit(970).GetNode<GolemAnimation>("GolemAnimation");
        golem._Process(1); // 이전 Q 충격파가 끝난 뒤 R만의 연출을 확인한다.
        Receive("CONTROL 970 0");
        Receive("SKILL 970 3");
        Receive("GOLEM 970 1");
        Receive("COOLDOWN 970 3 900");
        await Flush();
        Check(golem.GetNode<MeshInstance3D>("EmpowerRune").Visible &&
            !golem.GetNode<MeshInstance3D>("SlamPulse").Visible && Skills.StatusText.Contains("강화 대기"),
            "R arms the next basic skill without playing an area attack");
        Check(Skills.GetNode<Button>("Content/Slots/Q").TooltipText.Contains("250%") &&
            !Skills.GetNode<Button>("Content/Slots/Q").TooltipText.Contains("35%") &&
            Skills.GetNode<Button>("Content/Slots/W").TooltipText.Contains("40%") &&
            Skills.GetNode<Button>("Content/Slots/E").TooltipText.Contains("1.5초"),
            "Ready tooltips show empowered Q without its old slow, W shield and E stun");
        foreach (string invalid in new[] { "GOLEM 970 2", "GOLEM 0 0", "GOLEM 970 0 extra", "GOLEM 970", "GOLEM 971 1", "GOLEM 9999 1" }) Receive(invalid);
        Check(Unit(970).GolemEffects.EmpowerReady && !Unit(971).GolemEffects.EmpowerReady,
            "Malformed, unknown and non-golem snapshots cannot change readiness");
        KeyPress(Key.E); KeyPress(Key.Escape);
        Receive("STATE 970 ATTACK 0 971 1");
        Receive("TICK 900");
        Check(Unit(970).GolemEffects.EmpowerReady && Skills.CanUse(3),
            "Canceled targeting, basic attacks and elapsed R cooldown preserve the server's ready state");

        foreach (string activation in new[] { "SKILL 970 0 EMPOWERED", "SKILL 970 1 EMPOWERED", "SKILL 970 2 971 EMPOWERED" })
            Check(SkillActivationSnapshot.TryParse(activation.Split(' '), out var empowered) && empowered.Empowered,
                "Empowered self and targeted activations preserve their explicit flag");
        foreach (string invalid in new[] { "SKILL 970 3 EMPOWERED", "SKILL 970 2 0 EMPOWERED", "SKILL 970 0 EMPOWERED extra", "SKILL 970 0 EMPOWERED EMPOWERED" })
            Check(!SkillActivationSnapshot.TryParse(invalid.Split(' '), out _), "Malformed empowered activations are rejected");
        Receive("GOLEM 970 0");
        Receive("SKILL 970 0 EMPOWERED");
        await Flush();
        Check(!golem.GetNode<MeshInstance3D>("EmpowerRune").Visible &&
            golem.GetNode<MeshInstance3D>("SlamPulse").Visible && !Skills.StatusText.Contains("강화 대기") &&
            Skills.GetNode<Button>("Content/Slots/Q").TooltipText.StartsWith("Q · 내려찍기"),
            "Consumption clears readiness while an explicit empowered Q still plays after that update");
        Receive("GOLEM 970 1");
        Receive("SKILL 970 1 EMPOWERED");
        Receive("GOLEM 970 0");
        Receive("SHIELD 970 168");
        await Flush();
        Check(Unit(970).HealthBar.Shield == 168 && golem.GetNode<MeshInstance3D>("StoneShield").Visible &&
            !Unit(970).GolemEffects.EmpowerReady, "Empowered W displays the server's shield and consumed state");

        Receive("GOLEM 970 1");
        Receive("HIDE 970");
        Receive("GOLEM 970 1"); // 시야에서 사라진 객체에 대한 늦은 메시지는 무시한다.
        Check(!Skills.StatusText.Contains("강화 대기"), "Hiding the hero clears HUD readiness");
        Receive("UNIT 201 970 7 0 -8 1");
        Receive("GOLEM 970 1");
        await Flush();
        golem = Unit(970).GetNode<GolemAnimation>("GolemAnimation");
        Check(Unit(970).GolemEffects.EmpowerReady && golem.GetNode<MeshInstance3D>("EmpowerRune").Visible &&
            Skills.StatusText.Contains("강화 대기"), "A visibility snapshot restores the armed ultimate without a new R cast");
        Receive("REMOVE 970");
        await Flush();
        Check(!golem.GetNode<MeshInstance3D>("EmpowerRune").Visible && !Skills.StatusText.Contains("강화 대기"),
            "Death clears the rune and HUD readiness");
        Receive("UNIT 201 972 7 0 -8 1");
        Receive("GOLEM 972 0");
        Check(!Unit(972).GolemEffects.EmpowerReady && !Skills.StatusText.Contains("강화 대기"),
            "A respawn starts with no empowered skill");
    }

    private async Task CheckCameraNavigation()
    {
        Camera3D camera = Units.Camera;
        Vector2 screen = GetViewport().GetVisibleRect().Size;
        camera.Size = 60;
        camera.SetProcess(false); // 가장자리 이동은 일정한 delta로 검증한다.
        GetWindow().EmitSignal(Window.SignalName.FocusEntered);
        GetWindow().EmitSignal(Window.SignalName.MouseEntered);
        try
        {
            float step = 55 * camera.Size / camera.Get("home_size").AsSingle() * .1f;
            foreach ((Vector2 point, Vector3 direction) in new[]
            {
                (new Vector2(1, screen.Y / 2), Vector3.Left),
                (new Vector2(screen.X - 1, screen.Y / 2), Vector3.Right),
                (new Vector2(screen.X / 2, 1), Vector3.Forward),
                (new Vector2(screen.X / 2, screen.Y - 1), Vector3.Back),
                (screen - Vector2.One, new Vector3(1, 0, 1).Normalized()),
                (screen / 2, Vector3.Zero),
                (new Vector2(-1, screen.Y / 2), Vector3.Zero)
            })
            {
                CameraNavigation.FocusGround(camera, Vector3.Zero);
                Motion(point);
                Vector3 before = camera.Position;
                camera.Call("_process", .1);
                Check((camera.Position - before).DistanceTo(direction * step) < .001f,
                    $"Edge pan follows {point}, with equal straight/diagonal speed and no motion outside the viewport");
            }

            Motion(new Vector2(screen.X - 1, screen.Y / 2));
            Vector3 stopped = camera.Position;
            using (var press = new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = screen / 2 })
            {
                Input.ParseInputEvent(press);
                Input.FlushBufferedEvents();
                camera.Call("_process", .1);
                Check(camera.Position.IsEqualApprox(stopped), "Left-button dragging suppresses edge pan");
                using var release = new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = screen / 2 };
                Input.ParseInputEvent(release);
                Input.FlushBufferedEvents();
            }
            using (var press = new InputEventMouseButton { ButtonIndex = MouseButton.Middle, Pressed = true })
            {
                camera.Call("_unhandled_input", press);
                camera.Call("_process", .1);
                Check(camera.Position.IsEqualApprox(stopped), "Middle-button dragging suppresses edge pan");
                press.Pressed = false;
                camera.Call("_input", press);
            }
            GetWindow().EmitSignal(Window.SignalName.MouseExited);
            camera.Call("_process", .1);
            Check(camera.Position.IsEqualApprox(stopped), "Leaving the window stops edge pan even with a stale edge position");
            GetWindow().EmitSignal(Window.SignalName.MouseEntered);
            GetWindow().EmitSignal(Window.SignalName.FocusExited);
            camera.Call("_process", .1);
            Check(camera.Position.IsEqualApprox(stopped), "Unfocused window cannot edge pan");
            GetWindow().EmitSignal(Window.SignalName.FocusEntered);
            camera.Call("_process", 100);
            Check(camera.Position.X <= 80 && camera.Position.X > stopped.X, "Edge pan resumes on focus and respects map bounds");
        }
        finally
        {
            Motion(screen / 2);
            GetWindow().EmitSignal(Window.SignalName.FocusExited);
            camera.SetProcess(true);
        }

        int commandsBefore = _commands.Count;
        foreach (uint team in new uint[] { 1, 2 })
        {
            BeginSession();
            Receive($"WELCOME 7 {team} COMMANDER");
            Receive($"UNIT 0 1000 7 0 -8 {team}");
            Units.SelectSingle(Unit(1000));
            CameraNavigation.FocusGround(camera, Vector3.Zero);
            KeyPress(Key.Space);
            await Flush();
            Check(GroundCenter().DistanceTo(new Vector3(team == 1 ? -65 : 65, 0, 0)) < .02f,
                "Commander Space focuses its own base even before building snapshots arrive");
            Expect(1000);
            Check(Mathf.IsEqualApprox(camera.Size, 60), "Space preserves camera zoom");
        }
        foreach (uint type in new uint[] { UnitCatalog.HeroTest, UnitCatalog.HeroGolem })
        {
            BeginSession();
            Receive($"WELCOME 7 2 HERO {type}");
            Receive($"UNIT {type} 1001 8 12 8 2"); // 같은 편 다른 플레이어의 영웅.
            Receive($"UNIT {type} 1002 7 -12 -8 2");
            UnitInfo.Inspect(Unit(1001));
            KeyPress(Key.Space);
            await Flush();
            Check(GroundCenter().DistanceTo(Unit(1002).GlobalPosition) < .02f && UnitInfo.DisplayedUnit == Unit(1001),
                "Space focuses the owned hero without changing independent inspection");
            CameraNavigation.FocusGround(camera, Vector3.Zero);
            KeyPress(Key.Space, echo: true);
            await Flush();
            Check(GroundCenter().Length() < .02f, "Space key repeat does not lock the camera");
            Receive("REMOVE 1002");
            KeyPress(Key.Space);
            await Flush();
            Check(GroundCenter().Length() < .02f, "A dead hero cannot redirect Space to another player's hero");
            Receive($"UNIT {type} 1003 7 -6 -4 2");
            KeyPress(Key.Space);
            await Flush();
            Check(GroundCenter().DistanceTo(Unit(1003).GlobalPosition) < .02f, "Space follows the respawned hero's new ID");
        }
        Check(_commands.Count == commandsBefore, "Camera navigation sends no unit orders");
    }

    private async Task CheckBattleClarity()
    {
        BeginSession();
        Check(Stock.Visible, "Commanders see the economy HUD");
        InvokeMain("FrameStartingView");
        Check(Units.Camera.Size == 52 && GroundCenter().DistanceTo(new Vector3(-65, 0, 0)) < .02f,
            "Commander begins with a readable view of its own base");
        Receive("WELCOME 7 1 HERO 201");
        Check(!Stock.Visible && GetNode<BattleGuide>("SelectionUI/BattleGuide").Visible,
            "Heroes see role/objective guidance without a misleading 0/0 economy");
        Receive("UNIT 201 1200 7 -12 -8 1");
        Receive("HP 1200 420 420");
        InvokeMain("FrameStartingView");
        Check(Units.Camera.Size == 38 && GroundCenter().DistanceTo(Unit(1200).GlobalPosition) < .02f,
            "Hero begins centered at a readable zoom");
        Check(!Skills.GetNode<Control>("Content/Vitals/Mana").Visible,
            "Missing mana is hidden instead of displaying unknown values");
        Receive("UNIT 1 1201 8 -8 -8 2");
        Receive("HP 1201 40 100");
        await Flush();
        Check(Unit(1201).HealthBar.Visible, "An injured enemy's health is readable without selecting it");
        Unit(1201).Hide();
        await Flush();
        Check(!Unit(1201).HealthBar.Visible, "Health bars cannot reveal hidden models");
        Unit(1201).Show();
        Receive("HP 1201 100 100");
        Check(!Unit(1201).HealthBar.Visible, "Full-health unselected units avoid unnecessary bars");

        Receive("TICK 300");
        Receive("RESPAWN 660"); // 실제 서버는 REMOVE보다 먼저 부활 시점을 보낸다.
        Receive("REMOVE 1200");
        Check(Skills.IsRespawning && Skills.StatusText.Contains("12초"), "Death identifies its respawn countdown");
        Receive("RESPAWN invalid");
        Receive("TICK 330");
        Check(Skills.StatusText.Contains("11초"), "Countdown follows server time and ignores malformed messages");
        Receive("TICK 660");
        Check(Skills.StatusText.Contains("부활 위치"), "A blocked respawn is not presented as already alive");
        Receive("UNIT 201 1202 7 -20 -8 1");
        Receive("HP 1202 420 420");
        Check(!Skills.IsRespawning && Skills.StatusText.Contains("내 영웅"), "Respawn clears death feedback for the new unit ID");
        Receive($"MAP 2 {Map.MapHash}");
        Check(!GetNode<BattleGuide>("SelectionUI/BattleGuide").Visible && !Skills.IsRespawning,
            "Resynchronization clears stale guidance and respawn state");
    }

    private Vector3 GroundCenter()
    {
        Check(CameraNavigation.TryGroundPoint(Units.Camera, GetViewport().GetVisibleRect().GetCenter(), out Vector3 point), "Camera sees ground");
        return point;
    }
    private Vector2 MiniPoint(Vector3 world)
    {
        Check(Minimap.TryWorldToMap(world, out Vector2 point), "Test point is inside map");
        return Minimap.GetGlobalTransformWithCanvas() * point;
    }
    private Rect2 UnitBox(uint a, uint b)
    {
        Vector2 from = UnitPoint(a), to = UnitPoint(b);
        return new Rect2(from.Min(to) - Vector2.One * 7, (from - to).Abs() + Vector2.One * 14);
    }
    private Vector2 UnitPoint(uint id) => Units.Camera.UnprojectPosition(Unit(id).GlobalPosition + Vector3.Up);
    private Unit Unit(uint id) => Units.LiveUnits.Single(unit => unit.UnitId == id);
    private void ClickUnit(uint id, bool shift = false, bool twice = false) => Click(UnitPoint(id), MouseButton.Left, shift, twice);
    private void Click(Vector2 point, MouseButton button, bool shift = false, bool twice = false)
    {
        Motion(point);
        Mouse(point, button, true, shift, twice);
        Mouse(point, button, false, shift);
    }
    private void Drag(Vector2 from, Vector2 to, bool shift)
    {
        Motion(from);
        Mouse(from, MouseButton.Left, true, shift);
        Motion(to, true);
        Mouse(to, MouseButton.Left, false); // Press-time modifier survives release of Shift during dragging.
    }
    private void Mouse(Vector2 point, MouseButton button, bool pressed, bool shift = false, bool twice = false)
    {
        using var input = new InputEventMouseButton
        {
            Position = point, GlobalPosition = point, ButtonIndex = button, Pressed = pressed,
            ButtonMask = pressed ? (MouseButtonMask)(1 << ((int)button - 1)) : 0, ShiftPressed = shift, DoubleClick = twice
        };
        GetViewport().PushInput(input, true);
    }
    private void Motion(Vector2 point, bool held = false)
    {
        using var input = new InputEventMouseMotion { Position = point, GlobalPosition = point, ButtonMask = held ? MouseButtonMask.Left : 0 };
        GetViewport().PushInput(input, true);
    }
    private void KeyPress(Key key, bool ctrl = false, bool echo = false)
    {
        using var input = new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true, CtrlPressed = ctrl, Echo = echo };
        GetViewport().PushInput(input, true);
        input.Pressed = false;
        input.Echo = false;
        GetViewport().PushInput(input, true);
    }
    private void Expect(params uint[] ids) => Check(Units.SelectedUnitIds.ToHashSet().SetEquals(ids),
        $"Selection expected [{string.Join(',', ids)}], got [{string.Join(',', Units.SelectedUnitIds)}]");
    private void BeginSession() { Receive($"MAP 2 {Map.MapHash}"); Receive("WORLD_READY"); Receive("WELCOME 7 1 COMMANDER"); }
    private void Receive(string message) => InvokeMain("OnMessage", message);
    private void InvokeMain(string name, params object[] args) => typeof(Main).GetMethod(name, Private).Invoke(this, args);
    private async Task Flush()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
