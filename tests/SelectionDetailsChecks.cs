using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

// 실제 HUD와 GUI 입력을 사용합니다. 서버 대신 Main에 스냅샷을 주입합니다.
public partial class SelectionDetailsChecks : Main
{
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private SelectionDetails _details;
    private GridContainer Grid => _details.GetNode<GridContainer>("Content/Body/UnitGrid");
    private Control Single => _details.GetNode<Control>("Content/Body/Single");
    private int _commands;

    public override async void _Ready()
    {
        try
        {
            typeof(Main).GetField("_net", Private).SetValue(this, GetNode<NetClient>("/root/Net"));
            _details = GetNode<SelectionDetails>("SelectionUI/SelectionDetails");
            Units.CommandRequested += _ => _commands++;
            Fog.Configure(Map);
            await Layout();
            Check(!Single.Visible && !Grid.Visible, "Empty selection has no stale unit/building contents");
            Receive($"MAP 2 {Map.MapHash}");
            Receive("WORLD_READY");
            Receive("WELCOME 7 1 COMMANDER");
            Receive("SIGHT UNIT 0 8");
            Receive("SIGHT UNIT 1 8");
            Receive("SIGHT UNIT 2 8");
            Receive("SIGHT UNIT 100 8");
            Receive("SIGHT UNIT 101 8");
            Receive("SIGHT BUILDING 0 12");
            Receive("SIGHT BUILDING 1 9");
            Receive("STOCK 0 240");
            for (int i = 0; i < 64; i++)
            {
                Receive($"UNIT {i % 3} {101 + i} 7 {-18 + i % 8 * 3} {-15 + i / 8 * 3} 1");
                if (i > 0) Receive($"HP {101 + i} {20 + i % 4 * 20} 100");
                Receive($"STATE {101 + i} IDLE 0 0 0");
            }
            Receive("UNIT 1 999 8 12 0 2");
            Receive("BUILDING 0 201 1 -24 8 0");
            Receive("BUILDING 1 202 2 14 8 0");
            Receive("HP 201 740 1000");
            Receive("HP 202 220 500");
            await CheckRolePanels();

            InvokeMain("SelectUnit", Unit(101));
            await Layout();
            Check(Single.Visible && !Grid.Visible && Labels().Any(label => label.Text == "체력   — / —"), "Single selection does not invent HP before a server snapshot");
            var unitPortrait = Single.FindChildren("Portrait", "TextureRect", true, false).OfType<TextureRect>().Single();
            Check(unitPortrait.Texture is CompressedTexture2D && unitPortrait.Texture.ResourcePath == "res://ui/portraits/worker.png",
                "Selection loads a pre-imported PNG instead of rendering a 3D portrait");
            Receive("HP 101 32.5 50");
            Receive("STATE 101 GATHER 7 0 1");
            await Layout();
            Check(Labels().Any(label => label.Text == "체력   32.5 / 50") &&
                Labels().Any(label => label.Text == "운반 중인 목재   7") &&
                Labels().Any(label => label.Text == "상태   채집 중"), "HP, activity and carrying update without reselection");
            await CheckInspection();
            foreach (uint id in new uint[] { 102, 103 })
            {
                InvokeMain("SelectUnit", Unit(id));
                await Layout();
                Check(!Labels().Any(label => label.IsVisibleInTree() && label.Text.StartsWith("운반 중인 목재")), "Combat units do not retain worker-only details");
            }

            SelectAll();
            for (uint id = 113; id <= 164; id++) Units.SelectFromPortrait(id, true, false);
            await Layout();
            Check(Units.SelectedUnitIds.Count == 12, "A mixed twelve-unit group retains each selected type");

            SelectAll();
            await Layout();
            Check(Units.SelectedUnitIds.Count == 64 && Grid.Visible && !Single.Visible, "All 64 owned units use multi-selection; enemy is excluded");
            Check(Grid.GetChildCount() == _details.PageSize && _details.PageCount > 1, "Multiple pages retain the full selection");
            Check(Grid.GetChild<SelectionUnitCard>(0).UnitId == 101 && Grid.GetChild<SelectionUnitCard>(1).UnitId == 104, "Portraits sort by unit type and stable id");
            Receive("HP 101 5 50");
            await Layout();
            Check(Grid.GetChild<SelectionUnitCard>(0).TooltipText.Contains("5 / 50"), "Each portrait reflects changing health");
            await CheckHudInput();
            var seen = new HashSet<uint>();
            var pages = _details.GetNode<HBoxContainer>("Content/Pages");
            Button next = pages.GetChild<Button>(2);
            do
            {
                foreach (SelectionUnitCard card in Grid.GetChildren()) seen.Add(card.UnitId);
                if (next.Disabled) break;
                next.EmitSignal(BaseButton.SignalName.Pressed);
                await Layout();
            } while (true);
            Check(seen.SetEquals(Units.SelectedUnitIds), "Every selected unit is reachable through the pages");
            Check(Units.SelectedUnitIds.Count == 64, "Page navigation never narrows command selection");
            uint lastPageId = Grid.GetChild<SelectionUnitCard>(0).UnitId;
            await ClickCard(lastPageId);
            Check(Units.SelectedUnitIds.SequenceEqual(new[] { lastPageId }) && Single.Visible,
                $"A real GUI click selects only that portrait (expected {lastPageId}, got {string.Join(',', Units.SelectedUnitIds)}, single={Single.Visible})");

            SelectAll();
            await Layout();
            await ClickCard(101, shift: true);
            Check(Units.SelectedUnitIds.Count == 63 && !Units.SelectedUnitIds.Contains(101u), "Shift-click excludes exactly one unit");
            SelectAll();
            await Layout();
            await ClickCard(101, ctrl: true);
            Check(Units.SelectedUnitIds.Count == 22 && Units.SelectedUnitIds.All(id => Unit(id).UnitType == 0), "Ctrl-click retains only the clicked type in the selection");
            await ClickCard(101, shift: true, ctrl: true);
            Check(Units.SelectedUnitIds.Count == 0 && !Single.Visible && !Grid.Visible, "Ctrl+Shift can exclude an entire type and return to the empty state");
            SelectAll();
            Units.SelectFromPortrait(999, false, false);
            Check(Units.SelectedUnitIds.Count == 64, "An unselected or enemy id cannot enter the portrait selection path");
            await Layout();
            next.EmitSignal(BaseButton.SignalName.Pressed);
            for (uint id = 102; id <= 164; id++) Receive($"REMOVE {id}");
            await Layout();
            Check(Single.Visible && Units.SelectedUnitIds.SequenceEqual(new uint[] { 101 }), "Deaths on other pages collapse to the surviving single unit");
            Receive("HIDE 101");
            Check(!Single.Visible && !Grid.Visible, "HIDE immediately clears selection details");

            InvokeMain("SelectBuilding", Buildings.LiveBuildings.Single(building => building.BuildingId == 201));
            await Layout();
            Check(Single.Visible && Labels().Any(label => label.Text.StartsWith("회관")) && Labels().Any(label => label.Text == "체력   740 / 1000"), "Building displays its name and current HP");
            InvokeMain("SelectBuilding", Buildings.LiveBuildings.Single(building => building.BuildingId == 202));
            Receive("STATE 202 ATTACK 0 201 1");
            await Layout();
            Check(Labels().Any(label => label.Text == "요새") && Labels().Any(label => label.Text == "상태   공격 중"), "Type 1 fortress displays its name and server activity");
            var portrait = Single.FindChildren("Portrait", "TextureRect", true, false).OfType<TextureRect>().Single();
            Texture2D enemyPortrait = portrait.Texture;
            Receive("WELCOME 7 2 COMMANDER");
            Check(portrait.Texture != enemyPortrait, "Team changes still update the selected building's portrait colors");
            Receive("REMOVE 202");
            Check(!Single.Visible, "Building destruction clears details");
            InvokeMain("SelectBuilding", Buildings.LiveBuildings.Single());
            Receive("WELCOME 7 2 HERO 200");
            Receive("EXP 3 40 200");
            Receive($"MAP 2 {Map.MapHash}");
            Check(!Single.Visible && !Grid.Visible, "Map resynchronization clears details");
            Check(LocalRole == PlayerRole.None && !Skills.Visible && Skills.HeroType == null && UnitInfo.Experience == null,
                "Map resynchronization clears the previous hero role and skill panel");
            Receive("WORLD_READY");
            Receive("WELCOME 7 1 COMMANDER");
            Receive("UNIT 0 301 7 0 0 1");
            InvokeMain("SelectUnit", Unit(301));
            Receive("UNIT 0 301 9 0 0 1");
            Check(!Single.Visible, "Ownership changes clear the previous unit details");
            Receive("UNIT 0 301 7 0 0 1");
            InvokeMain("SelectUnit", Unit(301));
            Receive("WELCOME 7 1 HERO 201");
            Receive("EXP 2 10 150");
            InvokeMain("OnConnectionClosed", "Selection details test disconnect");
            Check(!Single.Visible && !Grid.Visible, "Disconnect clears details");
            Check(LocalRole == PlayerRole.None && LocalHeroType == null && !Skills.Visible && UnitInfo.Experience == null,
                "Disconnect cannot retain a hero HUD for the next session");
            Check(_commands == 0, "All inspection and selection gestures send no server commands");
            Check(_details.FindChildren("*", "SubViewport", true, false).Count == 0 &&
                _details.FindChildren("*", "Node3D", true, false).Count == 0,
                "Unit and building selections never create portrait viewports, models, lights or cameras");
            GD.Print("PASS: selection details, live HP/STATE, 64-unit paging, real GUI click/Shift/Ctrl, input isolation, building details, ownership/team/removal/HIDE/reset/disconnect");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private async Task CheckRolePanels()
    {
        Check(LocalRole == PlayerRole.Commander && _details.Visible && !Skills.Visible,
            "COMMANDER welcome retains the RTS information panel");
        InvokeMain("SelectUnit", Unit(101));
        Rect2 commandsRect = Commands.GetGlobalRect();
        Receive("WELCOME 7 1 HERO 200");
        await Layout();
        Check(LocalRole == PlayerRole.Hero && LocalHeroType == 200 && Skills.HeroType == 200 && Skills.Visible && !_details.Visible,
            "HERO welcome stores the hero type and replaces only the middle panel");
        Check(Skills.GetNode("Content/Slots").GetChildren().OfType<Control>().Count(slot => slot.Visible) == 5 &&
            Skills.GetNode("Content/Slots").FindChildren("Key", "Label", true, false).OfType<Label>().Where(label => label.IsVisibleInTree()).Select(label => label.Text).SequenceEqual(new[] { "패시브", "Q", "W", "E", "R" }) &&
            Skills.GetNode("Content/Slots/Q") is Button,
            "Hero HUD keeps five minimal slots and exposes the wolf Q button");
        Check(!Commands.Visible && UnitInfo.Visible && UnitInfo.Size == new Vector2(208, 160) &&
            UnitInfo.GetGlobalRect().End == commandsRect.End && UnitInfo.FindChildren("*", "Button", true, false).Count == 0,
            "AOS replaces commands with a smaller read-only information panel at the same bottom-right corner");
        foreach (string invalid in new[] { "WELCOME 8 2", "WELCOME 8 2 HERO", "WELCOME 8 2 HERO nope", "WELCOME 8 2 HERO -1", "WELCOME 8 2 HERO 4294967296", "WELCOME 8 2 COMMANDER 200", "WELCOME 8 2 OTHER", "WELCOME 0 2 HERO 200", "WELCOME 8 0 HERO 200" }) Receive(invalid);
        Check(LocalRole == PlayerRole.Hero && LocalHeroType == 200 && Units.LocalTeam == 1 && Skills.Visible,
            "Invalid welcome messages cannot partially change team, ownership or HUD");
        CheckHeroVitals();
        await CheckShieldSync();
        await CheckCompactInfo();
        InvokeMain("SelectUnit", Unit(999));
        Check(Skills.Visible && !_details.Visible, "Inspecting another unit does not replace the hero skill bar");
        var input = GetNode<PlayerInput>("PlayerInput");
        int clicks = 0;
        void OnContext(Node3D _, Vector3 __) => clicks++;
        input.ContextClicked += OnContext;
        float cameraSize = Units.Camera.Size;
        foreach (Vector2 position in new[] { Skills.GetGlobalRect().GetCenter(), UnitInfo.GetGlobalRect().GetCenter() })
        {
            PushMouse(position, MouseButton.Right, true);
            PushMouse(position, MouseButton.Right, false);
            PushMouse(position, MouseButton.WheelUp, true);
            PushMouse(position, MouseButton.WheelUp, false);
        }
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        input.ContextClicked -= OnContext;
        Check(clicks == 0 && Mathf.IsEqualApprox(cameraSize, Units.Camera.Size) && _commands == 0,
            "Skill and unit information panels consume mouse input without orders, casts or camera zoom");
        Receive("WELCOME 7 1 COMMANDER");
        Receive("EXP 9 100 500");
        Check(_details.Visible && Commands.Visible && Commands.GetGlobalRect() == commandsRect && !UnitInfo.Visible &&
            UnitInfo.InspectedUnit == null && UnitInfo.Experience == null && !Skills.Visible && Skills.HeroType == null && LocalHeroType == null && Skills.HeroUnitId == null,
            "COMMANDER restores the original RTS panels and clears AOS inspection");
    }

    private async Task CheckCompactInfo()
    {
        Label Info(string name) => (Label)UnitInfo.FindChild(name, true, false);
        var experience = (ProgressBar)UnitInfo.FindChild("Experience", true, false);
        Check(Info("Level").Text == "Lv.—" && Info("ExperienceText").Text == "— / —",
            "Missing initial EXP never invents a level or threshold");
        Receive("EXP 1 35 100"); // Player progress can precede UNIT.
        Receive("UNIT 200 8005 7 0 -5 1");
        Receive("HP 8005 185 300");
        Receive("STATS 8005 20 0.7 1.25 6 10");
        await Layout();
        Check(UnitInfo.InspectedUnit == null && UnitInfo.DisplayedUnit == Unit(8005) && Info("HealthText").Text == "185 / 300" &&
            Info("Level").Text == "Lv.1" && Info("ExperienceText").Text == "35 / 100" && experience.Value == 35 &&
            Info("Damage").Text == "공격 20" && Info("AttackSpeed").Text == "공속 0.8/초" &&
            Info("Range").Text == "사거리 0.7" && Info("Sight").Text == "시야 10" && Info("Speed").Text == "이속 6",
            "Without a click the own hero shows server vitals, all five stats and owner-only progress");
        Receive("WELCOME 7 1 HERO 200");
        foreach (string invalid in new[] { "EXP 0 0 100", "EXP 1 -1 100", "EXP 1 100 100", "EXP 1 1 0", "EXP 1 0 -1",
            "EXP 1 NaN 100", "EXP 1 2", "EXP 1 2 100 extra", "EXP 2147483648 0 100", "EXP 1 9223372036854775808 100" }) Receive(invalid);
        Check(UnitInfo.Experience == new HeroExperienceSnapshot(1, 35, 100), "Repeated WELCOME and malformed EXP preserve valid progress");
        Receive("EXP 2 25 150");
        Receive("HP 8005 203.5 330");
        Receive("STATS 8005 22 0.7 0.8 6 10");
        await Layout();
        Check(Info("Level").Text == "Lv.2" && Info("ExperienceText").Text == "25 / 150" &&
            Math.Abs(experience.Value - 100.0 / 6) < .01 && Info("HealthText").Text == "203.5 / 330" &&
            Info("Damage").Text == "공격 22" && Info("AttackSpeed").Text == "공속 1.25/초",
            "Level-up EXP, HP and STATS update independently without reselection");
        if (OS.GetCmdlineUserArgs().Contains("--capture-aos-info"))
        {
            if (OS.GetCmdlineUserArgs().Contains("--capture-wolf-q"))
            {
                Receive("TICK 100");
                Receive("COOLDOWN 0 220");
                Receive("TICK 160");
                await Layout();
            }
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using Image image = GetViewport().GetTexture().GetImage();
            image.SavePng("res://.godot/aos-info.png");
        }
        InvokeMain("SelectUnit", Unit(999));
        await Layout();
        Check(UnitInfo.InspectedUnit == Unit(999) && Units.SelectedUnitIds.SequenceEqual(new uint[] { 8005 }) &&
            Info("UnitName").Text == "검방병" && Info("HealthText").Text == "— / —",
            "Compact inspection keeps the own hero controlled and leaves unknown enemy HP empty");
        Receive("HP 999 61 100");
        Receive("STATE 999 ATTACK 0 8005 1");
        Receive("STATS 999 12 0.7 1 6 8");
        await Layout();
        Check(Info("HealthText").Text == "61 / 100" && Info("Activity").Text == "공격 중" &&
            Info("Damage").Text == "공격 12" && Info("Range").Text == "사거리 0.7" &&
            !Info("Level").Visible && !experience.IsVisibleInTree() &&
            Skills.GetNode<Label>("Content/Vitals/Health/Value").Text == "203.5 / 330",
            "Inspected HP, action and stats update without changing the hero vitals");
        Check(((TextureRect)UnitInfo.FindChild("Portrait", true, false)).Texture.ResourcePath == "res://ui/portraits/knight-enemy.png",
            "Compact inspection uses the existing enemy portrait");
        Receive("HP 999 40 100");
        Receive("STATE 999 HOLD 0 0 1");
        await Layout();
        Check(Info("HealthText").Text == "40 / 100" && Info("Activity").Text == "위치 사수", "Inspected values stay live");
        Receive("EXP 2 75 150");
        Check(!experience.IsVisibleInTree() && experience.Value == 0, "Own EXP is retained but never attributed to the inspected enemy");
        Receive("UNIT 100 8810 0 8 0 1");
        InvokeMain("SelectUnit", Unit(8810));
        await Layout();
        Check(Info("UnitName").Text == "근접 미니언" && Info("HealthText").Text == "— / —" &&
            Info("Damage").Text == "공격 —" && Info("AttackSpeed").Text == "공속 —", "Switching to an allied minion clears the previous unit's values");
        Receive("STATS 8810 0 0 0 4 8");
        await Layout();
        Check(Info("AttackSpeed").Text == "공속 —", "Zero attack interval does not divide by zero or invent an attack speed");
        Receive("HIDE 8810");
        Receive("HP 8810 60 60");
        await Layout();
        Check(UnitInfo.InspectedUnit == null && UnitInfo.DisplayedUnit == Unit(8005) && Info("HealthText").Text == "203.5 / 330" &&
            experience.Value == 50, "HIDE returns to the own hero and ignores late HP for the former inspection");
        Receive("UNIT 100 8810 0 8 0 1");
        await Layout();
        Check(UnitInfo.InspectedUnit == null, "A reappearing ID does not silently restore the old inspection");
        InvokeMain("SelectUnit", Unit(8810));
        Receive("REMOVE 8810");
        await Layout();
        Check(UnitInfo.InspectedUnit == null && UnitInfo.DisplayedUnit == Unit(8005), "An inspected unit's death returns to the own hero");
        InvokeMain("SelectUnit", Unit(999));
        InvokeMain("ClearSelection");
        Check(UnitInfo.InspectedUnit == null && UnitInfo.DisplayedUnit == Unit(8005) && Units.SelectedUnitIds.SequenceEqual(new uint[] { 8005 }),
            "Clearing inspection leaves the own hero controlled");
        InvokeMain("SelectUnit", Unit(8005));
        Check(Info("Level").Visible && experience.Value == 50, "Explicitly inspecting the own hero still shows owner progress");
        Receive("REMOVE 8005");
        Receive("HP 8005 300 300");
        await Layout();
        Check(UnitInfo.DisplayedUnit == null && Info("HealthText").Text == "— / —" && Info("Level").Text == "Lv.2" && experience.Value == 50,
            "Hero death clears instance data but retains player level and XP");
        Receive("UNIT 200 8006 7 0 -5 1");
        Receive("HP 8006 330 330");
        await Layout();
        Check(UnitInfo.DisplayedUnit == Unit(8006) && experience.Value == 50 && Info("Damage").Text == "공격 —",
            "Respawn with a new ID automatically rebinds the hero without old instance stats");
        Receive("EXP 10 0 0");
        Check(Info("Level").Text == "Lv.10" && Info("ExperienceText").Text == "MAX" && experience.Value == 100,
            "Server zero threshold displays a full max-level bar without hardcoding a cap");
        Receive("WELCOME 7 2 HERO 200");
        Check(UnitInfo.Experience == null && UnitInfo.DisplayedUnit == null, "Changing team resets owner progress and the displayed hero");
        Receive("WELCOME 7 1 HERO 200");
        Receive("HIDE 8006");
    }

    private void CheckHeroVitals()
    {
        var health = Skills.GetNode<ProgressBar>("Content/Vitals/Health/Bar");
        var mana = Skills.GetNode<ProgressBar>("Content/Vitals/Mana/Bar");
        var hpText = Skills.GetNode<Label>("Content/Vitals/Health/Value");
        var mpText = Skills.GetNode<Label>("Content/Vitals/Mana/Value");
        Check(hpText.Text == "— / —" && mpText.Text == "— / —", "Vitals remain unknown before authoritative values arrive");
        Receive("UNIT 200 8001 9 0 0 1");
        Receive("UNIT 200 8002 7 0 0 2");
        Receive("HP 8001 300 600");
        Check(Skills.HeroUnitId == null && health.Value == 0, "Other players and other teams do not bind the hero HUD");
        Receive("UNIT 200 8001 7 0 0 1");
        Receive("HP 8001 300 600");
        Check(Skills.HeroUnitId == 8001 && health.Value == 50 && hpText.Text == "300 / 600" && mpText.Text == "— / —",
            "UNIT ownership identifies the hero independently of selection; HP changes only the health bar");
        Check(Units.TryGetUnit(8001, out Unit hero) && hero.UnitType == UnitCatalog.HeroTest && hero.HealthBar.CurrentHP == 300,
            "The registered hero model and the AOS health bar consume the same HP snapshot");
        Skills.ApplyMana(8001, 45, 90);
        Receive("HP 8001 450 600");
        Receive("HP 8002 1 600");
        Receive("HP 8001 NaN 600");
        Skills.ApplyMana(8002, 0, 90);
        Skills.ApplyMana(8001, float.NaN, 90);
        Check(health.Value == 75 && mana.Value == 50 && hpText.Text == "450 / 600" && mpText.Text == "45 / 90",
            "Health and the future mana input update independently and ignore invalid/foreign values");
        Receive("UNIT 200 8001 7 1 0 1");
        Receive("WELCOME 7 1 HERO 200");
        Check(health.Value == 75 && mana.Value == 50, "Repeated snapshots retain current vitals");
        Receive("REMOVE 8001");
        Receive("HP 8001 600 600");
        Check(Skills.HeroUnitId == null && health.Value == 0 && mana.Value == 0 && hpText.Text == "— / —" && mpText.Text == "— / —",
            "Removing the hero clears both bars and ignores late updates");
        Receive("UNIT 200 8003 7 0 0 1");
        Receive("HP 8003 600 600");
        Check(health.Value == 100 && mpText.Text == "— / —", "A replacement hero starts with fresh values");
        Receive("UNIT 200 8003 9 0 0 1");
        Check(Skills.HeroUnitId == null && hpText.Text == "— / —", "Losing ownership clears the former hero's vitals");
        Receive("HIDE 8002");
        Receive("HIDE 8003");
    }

    private async Task CheckShieldSync()
    {
        Receive("UNIT 200 8020 7 0 0 1");
        Receive("SHIELD 8020 150"); // A shield may arrive before HP.
        Receive("HP 8020 300 300");
        await Layout();
        var fill = Skills.GetNode<ColorRect>("Content/Vitals/Health/Bar/Shield");
        var hpText = Skills.GetNode<Label>("Content/Vitals/Health/Value");
        Check(Unit(8020).HealthBar.Shield == 150 && fill.Visible && hpText.Text == "300 / 300  (+150)" &&
            UnitInfo.GetNode<Label>("Body/Content/HealthRow/HealthText").Text.Contains("(+150)"),
            "Shield snapshot reaches the world bar, hero HUD and inspected health");
        Check(Math.Abs(fill.AnchorLeft - 2f / 3) < .001 && fill.AnchorRight == 1,
            "Shield exceeding maximum HP is shown within the bar");
        foreach (string invalid in new[] { "SHIELD 8020 -1", "SHIELD 8020 NaN", "SHIELD 8020 Infinity", "SHIELD 0 50", "SHIELD 8020 10 extra" }) Receive(invalid);
        Receive("SHIELD 999 50");
        Check(Unit(8020).HealthBar.Shield == 150 && hpText.Text.Contains("(+150)"), "Invalid and foreign shields do not change the hero HUD");
        Receive("SHIELD 8020 70");
        Check(Unit(8020).HealthBar.Shield == 70 && hpText.Text.Contains("(+70)"), "Absorbed shield updates without an HP message");
        Receive("SHIELD 8020 0");
        Check(!fill.Visible && hpText.Text == "300 / 300", "Zero shield clears the overlay and amount");
        Receive("SHIELD 201 40");
        Check(Buildings.TryGetBuilding(201, out Building building) && building.HealthBar.Shield == 40,
            "Building shields use the same snapshot");
        Receive("SHIELD 201 0");
        Receive("SHIELD 8020 80");
        Receive("HIDE 8020");
        Receive("SHIELD 8020 999");
        Receive("UNIT 200 8020 7 0 0 1");
        Receive("HP 8020 300 300");
        Check(Unit(8020).HealthBar.Shield == 0 && !fill.Visible, "HIDE/reappearance discards old and late shields");
        Receive("SHIELD 8020 90");
        Receive("REMOVE 8020");
        Check(!fill.Visible && hpText.Text == "— / —", "Death clears the hero shield display");
        Receive("SHIELD 999 0");
    }

    private async Task CheckInspection()
    {
        Receive("STATS 101 5 0.5 1 5 8");
        await Layout();
        Check(Labels().Any(label => label.Text.Contains("공격력 5") && label.Text.Contains("사거리 0.5") && label.Text.Contains("이동 속도 5")),
            "Info panel uses server damage, range, attack interval, speed and sight");
        foreach (string invalid in new[] { "STATS 101 NaN 1 1 1 1", "STATS 101 -1 1 1 1 1", "STATS 0 1 1 1 1 1", "STATS 101 1 1 1 1", "STATS 101 1 1 1 Infinity 1" }) Receive(invalid);
        Check(Unit(101).Stats?.Damage == 5, "Malformed stats do not overwrite the last known server values");
        Receive("STATS 101 7 0.75 1.2 6 9");
        await Layout();
        Check(Labels().Any(label => label.Text.Contains("공격력 7") && label.Text.Contains("사거리 0.75")), "Stats update without reselection");
        Receive("HP 999 61 100");
        Receive("STATS 999 12 0.5 1 5 8");
        Receive("STATE 999 GUARD 0 0 0");
        Receive("UNIT 2 998 9 10 0 1");
        Receive("UNIT 100 997 0 8 0 2");
        Receive("STATS 997 4 0.4 1.4 4 6");
        foreach (uint id in new uint[] { 999, 998, 997 })
        {
            InvokeMain("SelectUnit", Unit(id));
            await Layout();
            Check(Units.InspectedUnit == Unit(id) && Units.SelectedUnitIds.Count == 0 && Single.Visible,
                "Enemy, allied other-owner and minion clicks all inspect without entering the command selection");
            Check(Commands.FindChildren("*", "Button", true, false).OfType<Button>().All(button => button.Disabled), "Foreign inspection exposes no unit commands");
            Units.RequestMove(Vector3.Zero);
            Units.RequestAttackMove(Vector3.Zero);
            Units.RequestAttack(Unit(999));
            Units.SaveControlGroup(8);
            Check(!Units.RecallControlGroup(8), "Inspected foreign units cannot enter a control group");
            if (id == 999)
            {
                Check(Labels().Any(label => label.Text == "체력   61 / 100") && Labels().Any(label => label.Text == "상태   경계 중"),
                    "Enemy inspection shows current server health and guarding state");
                Check(Single.FindChildren("Portrait", "TextureRect", true, false).OfType<TextureRect>().Single().Texture.ResourcePath == "res://ui/portraits/knight-enemy.png",
                    "Enemy inspection uses the opposing team's portrait colors");
            }
            if (id == 997)
            {
                Check(Single.FindChildren("Portrait", "TextureRect", true, false).OfType<TextureRect>().Single().Texture.ResourcePath == "res://ui/portraits/minion-knight-enemy.png",
                    "Inspected minions retain their own model portrait");
                Check(Labels().Any(label => label.Text == "근접 미니언") && Labels().Any(label => label.Text.Contains("공격력 4") && label.Text.Contains("시야 6")),
                    "Minion inspection shows its independent name and combat stats");
            }
        }
        InvokeMain("SelectUnit", Unit(999));
        Receive("HIDE 999");
        Check(Units.InspectedUnit == null && !Single.Visible, "Losing vision immediately clears enemy inspection");
        Receive("UNIT 1 999 8 12 0 2");
        InvokeMain("SelectUnit", Unit(997));
        Receive("REMOVE 997");
        Check(!Single.Visible && Units.InspectedUnit == null, "Inspected minion death clears the info panel");

        Building enemy = Buildings.LiveBuildings.Single(building => building.BuildingId == 202);
        Buildings.VisibilityCheck = Fog.IsBuildingVisible;
        Receive("BUILDING_VISION 202 0");
        InvokeMain("SelectBuilding", enemy);
        Check(Buildings.SelectedBuilding == null, "A building hidden by fog cannot be inspected");
        Receive("BUILDING_VISION 202 1");
        Receive("STATS 202 15 7 1.5 0 11");
        InvokeMain("SelectBuilding", enemy);
        await Layout();
        Check(Single.Visible && Labels().Any(label => label.Text.Contains("공격력 15") && label.Text.Contains("사거리 7")), "Visible enemy buildings show combat stats");
        Receive("BUILDING_VISION 202 0");
        await Layout();
        Check(Buildings.SelectedBuilding == null && !Single.Visible, "Losing building vision closes its current-status panel");
        Receive("BUILDING_VISION 202 1");
        Fog.RefreshVision();
        Buildings.VisibilityCheck = null;
        InvokeMain("SelectUnit", Unit(101));
    }

    private async Task ClickCard(uint id, bool shift = false, bool ctrl = false)
    {
        var card = Grid.GetChildren().OfType<SelectionUnitCard>().Single(item => item.UnitId == id);
        Vector2 position = card.GetGlobalRect().GetCenter();
        using var motion = new InputEventMouseMotion { Position = position, GlobalPosition = position };
        GetViewport().PushInput(motion, true);
        PushMouse(position, MouseButton.Left, true, shift, ctrl);
        PushMouse(position, MouseButton.Left, false, shift, ctrl);
        await Layout();
    }

    private async Task CheckHudInput()
    {
        var input = GetNode<PlayerInput>("PlayerInput");
        int worldClicks = 0;
        input.ContextClicked += (_, _) => worldClicks++;
        float cameraSize = Units.Camera.Size;
        Vector2 blank = _details.GetGlobalRect().Position + new Vector2(_details.Size.X - 12, 10);
        PushMouse(blank, MouseButton.Right, true);
        PushMouse(blank, MouseButton.Right, false);
        PushMouse(blank, MouseButton.WheelUp, true);
        PushMouse(blank, MouseButton.WheelUp, false);
        PushMouse(Grid.GetChild<SelectionUnitCard>(0).GetGlobalRect().GetCenter(), MouseButton.Right, true);
        PushMouse(Grid.GetChild<SelectionUnitCard>(0).GetGlobalRect().GetCenter(), MouseButton.Right, false);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        Check(worldClicks == 0 && Mathf.IsEqualApprox(cameraSize, Units.Camera.Size), "Right clicks and wheels over the HUD never reach world orders or camera zoom");
    }

    private void PushMouse(Vector2 position, MouseButton button, bool pressed, bool shift = false, bool ctrl = false)
    {
        using var input = new InputEventMouseButton
        {
            Position = position, GlobalPosition = position, ButtonIndex = button, Pressed = pressed,
            ShiftPressed = shift, CtrlPressed = ctrl,
            ButtonMask = pressed ? (MouseButtonMask)(1 << ((int)button - 1)) : 0
        };
        GetViewport().PushInput(input, true);
    }

    private IEnumerable<Label> Labels() => _details.FindChildren("*", "Label", true, false).OfType<Label>();
    private Unit Unit(uint id) => Units.LiveUnits.Single(unit => unit.UnitId == id);
    private void SelectAll() => InvokeMain("SelectBox", GetViewport().GetVisibleRect());
    private void Receive(string message) => InvokeMain("OnMessage", message);
    private void InvokeMain(string method, params object[] args) => typeof(Main).GetMethod(method, Private).Invoke(this, args);
    private async Task Layout()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
