using Godot;
using System;
using System.Collections.Generic;

// 선택한 일꾼의 건설 메뉴와 회관·병영의 생산 버튼을 표시합니다. 생산 진행도는 정보 패널에 표시합니다.
public partial class CommandPanel : PanelContainer
{
    [Export] public UnitManager Units;
    [Export] public BuildingManager Buildings;

    public event Action<uint> BuildRequested;
    public event Action BuildMenuOpened;
    public event Action AttackRequested;
    public event Action StopRequested;
    public event Action HoldRequested;
    public bool IsTierOneMenuOpen => _tierOne;
    public const string CameraMenuMeta = "command_menu_open";
    public const string CameraReleaseMeta = "command_menu_wait_for_release";
    public const string CameraUnitOrdersMeta = "unit_orders_selected";
    private enum DisplayMode { Unset, Empty, Units, Workers, TierOne, TownHall, Barracks, Construction }
    private static readonly int[] NumpadOrder = { 7, 8, 9, 4, 5, 6, 1, 2, 3 };
    private readonly Button[] _slots = new Button[9];
    private readonly List<StyleBoxFlat> _styles = new();
    private readonly HashSet<Key> _heldMenuKeys = new();
    private DisplayMode _display;
    private bool _tierOne;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        MouseForcePassScrollEvents = false;
        var frame = Style("18232ef5", "65776a");
        frame.CornerRadiusTopLeft = 6;
        frame.ContentMarginLeft = frame.ContentMarginTop = frame.ContentMarginRight = frame.ContentMarginBottom = 6;
        AddThemeStyleboxOverride("panel", frame);

        var empty = Style("101a23", "2a3940");
        var normal = Style("22332d", "728a77");
        var hover = Style("2d463a", "a1b897");
        var pressed = Style("365543", "b6d3aa");
        var grid = new GridContainer { Name = "Slots", Columns = 3 };
        grid.AddThemeConstantOverride("h_separation", 6);
        grid.AddThemeConstantOverride("v_separation", 6);
        AddChild(grid);
        for (int i = 0; i < NumpadOrder.Length; i++)
        {
            var slot = new Button
            {
                Name = $"Slot{NumpadOrder[i]}", Text = "", Disabled = true,
                CustomMinimumSize = new Vector2(64, 64),
                SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill,
                FocusMode = FocusModeEnum.None, MouseFilter = MouseFilterEnum.Stop,
                MouseForcePassScrollEvents = false
            };
            slot.AddThemeStyleboxOverride("normal", normal);
            slot.AddThemeStyleboxOverride("hover", hover);
            slot.AddThemeStyleboxOverride("pressed", pressed);
            slot.AddThemeStyleboxOverride("disabled", empty);
            slot.AddThemeFontSizeOverride("font_size", 14);
            slot.AddThemeColorOverride("font_color", new Color("f2e8cb"));
            slot.AddThemeColorOverride("font_hover_color", new Color("fff4d6"));
            slot.AddThemeColorOverride("font_pressed_color", new Color("fff4d6"));
            slot.AddThemeColorOverride("font_disabled_color", new Color("a8b8bd"));
            int number = NumpadOrder[i];
            slot.Pressed += () => RequestSlot(number);
            grid.AddChild(slot);
            _slots[i] = slot;
        }
        if (GodotObject.IsInstanceValid(Units)) Units.SelectionChanged += OnSelectionChanged;
        if (GodotObject.IsInstanceValid(Buildings))
        {
            Buildings.SelectionChanged += OnSelectionChanged;
            Buildings.ProductionChanged += RefreshProduction;
        }
        GetWindow().FocusExited += OnFocusExited;
        RefreshSelection();
    }

    private StyleBoxFlat Style(string background, string border)
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(background), BorderColor = new Color(border),
            BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1
        };
        _styles.Add(style);
        return style;
    }

    private void RefreshSelection()
    {
        DisplayMode next = DisplayMode.Empty;
        if (GodotObject.IsInstanceValid(Units) && Units.LocalTeam != 0)
        {
            if (Units.SelectedUnitIds.Count > 0) next = Units.TryGetSelectedWorker(out _) ? DisplayMode.Workers : DisplayMode.Units;
            else if (Units.InspectedUnit == null && GodotObject.IsInstanceValid(Buildings))
            {
                Building selected = Buildings.SelectedBuilding;
                if (GodotObject.IsInstanceValid(selected) && !selected.IsQueuedForDeletion() &&
                    selected.SideId == Units.LocalTeam)
                    next = selected.IsUnderConstruction ? Units.CanCancelConstruction(selected) ? DisplayMode.Construction : DisplayMode.Empty : selected.BuildingType switch
                    {
                        BuildingCatalog.TownHall => DisplayMode.TownHall,
                        BuildingCatalog.Barracks => DisplayMode.Barracks,
                        _ => DisplayMode.Empty
                    };
            }
        }
        if (next != DisplayMode.Workers) SetMenuOpen(false);
        else if (_tierOne) next = DisplayMode.TierOne;
        if (GodotObject.IsInstanceValid(Units?.Camera))
        {
            bool selected = next is DisplayMode.Units or DisplayMode.Workers or DisplayMode.TierOne;
            if (!selected && Units.Camera.GetMeta(CameraUnitOrdersMeta, false).AsBool())
                Units.Camera.SetMeta(CameraReleaseMeta, true);
            Units.Camera.SetMeta(CameraUnitOrdersMeta, selected);
        }
        if (next == _display) { RefreshProduction(); return; }
        _display = next;
        for (int i = 0; i < _slots.Length; i++)
        {
            string text = (next, NumpadOrder[i]) switch
            {
                (DisplayMode.Units or DisplayMode.Workers, 4) => "공격(A)",
                (DisplayMode.Units or DisplayMode.Workers, 5) => "정지(S)",
                (DisplayMode.Units or DisplayMode.Workers, 6) => "홀드(D)",
                (DisplayMode.Workers, 1) => "1티어(Z)",
                (DisplayMode.TierOne, 7) => "저장소\n(Q)",
                (DisplayMode.TierOne, 8) => "합숙소\n(W)",
                (DisplayMode.TierOne, 9) => "병영\n(E)",
                (DisplayMode.TierOne, 4) => "대장간\n(A)",
                (DisplayMode.TierOne, 5) => "포탑\n(S)",
                (DisplayMode.TierOne, 1) => "뒤로(Z)",
                (DisplayMode.TownHall, 7) => "일꾼",
                (DisplayMode.Barracks, 7) => "검방병",
                (DisplayMode.Barracks, 8) => "궁수",
                (DisplayMode.Construction, 3) => "건설 취소\n(Esc)",
                _ => ""
            };
            _slots[i].Text = text;
            _slots[i].Disabled = text.Length == 0;
            _slots[i].TooltipText = "";
        }
        RefreshProduction();
        if (next is DisplayMode.Units or DisplayMode.Workers)
        {
            _slots[3].TooltipText = "적 클릭: 공격 · 지형 클릭: 공격 이동";
            _slots[4].TooltipText = "현재 작업 중단 · 주변 적을 추격하고 제자리로 복귀";
            _slots[5].TooltipText = "현재 작업 중단 · 자리를 지키며 사거리 안의 적만 공격";
        }
    }

    private void RefreshProduction()
    {
        if (_display == DisplayMode.Construction)
        {
            _slots[8].Disabled = !Units.CanCancelConstruction(Buildings.SelectedBuilding);
            _slots[8].TooltipText = "공사를 취소하고 지불 비용의 75% 반환 (소수점 버림)";
            return;
        }
        if (_display is not (DisplayMode.TownHall or DisplayMode.Barracks)) return;
        Building producer = Buildings.SelectedBuilding;
        if (!GodotObject.IsInstanceValid(producer)) return;
        int count = producer.ProductionQueue.Count;
        bool canCancel = producer.ProductionJobs.Count > 0 && Units.CanCancelTraining(producer, producer.ProductionJobs[0].Id);
        _slots[8].Text = canCancel ? "생산 취소\n(Esc)" : "";
        _slots[8].Disabled = !canCancel;
        _slots[8].TooltipText = canCancel ? "현재 생산을 취소하고 지불 비용의 75% 반환 · 대기 항목은 정보 패널에서 클릭" : "";
        _slots[0].Disabled = count >= ProductionSnapshot.MaxQueue;
        const string rallyHint = "\n지형 우클릭: 랠리 지정 · 선택한 건물 우클릭: 랠리 해제";
        _slots[0].TooltipText = (_display == DisplayMode.TownHall ? "나무 100 · 인구 1 · 생산 10초\n나무 우클릭: 새 일꾼 자동 채집" : "나무 200 · 인구 2 · 생산 10초") + rallyHint;
        if (_display == DisplayMode.Barracks)
        {
            _slots[1].Disabled = count >= ProductionSnapshot.MaxQueue;
            _slots[1].TooltipText = "나무 200 · 인구 2 · 생산 10초" + rallyHint;
        }
    }

    private void RequestSlot(int number)
    {
        RefreshSelection();
        if (_display is DisplayMode.Units or DisplayMode.Workers && number is 4 or 5 or 6)
        {
            if (number == 4) AttackRequested?.Invoke();
            else if (number == 5) StopRequested?.Invoke();
            else HoldRequested?.Invoke();
        }
        else if (_display == DisplayMode.Construction && number == 3) Units.RequestCancelConstruction(Buildings.SelectedBuilding);
        else if (_display is DisplayMode.TownHall or DisplayMode.Barracks && number == 3)
        {
            Building producer = Buildings.SelectedBuilding;
            if (producer.ProductionJobs.Count > 0) Units.RequestCancelTraining(producer, producer.ProductionJobs[0].Id);
        }
        else if (_display == DisplayMode.TownHall && number == 7) Units.RequestTrainWorker();
        else if (_display == DisplayMode.Barracks && number is 7 or 8) Units.RequestTrain(number == 7 ? 1u : 2u);
        else if (_display == DisplayMode.Workers && number == 1)
        {
            SetMenuOpen(true);
            RefreshSelection();
            BuildMenuOpened?.Invoke();
        }
        else if (_display == DisplayMode.TierOne)
        {
            if (number == 1) { CancelMenu(); return; }
            uint type = number switch
            {
                7 => BuildingCatalog.Store,
                8 => BuildingCatalog.Supply,
                9 => BuildingCatalog.Barracks,
                4 => BuildingCatalog.Forge,
                5 => BuildingCatalog.Tower,
                _ => uint.MaxValue
            };
            if (!BuildingCatalog.IsPlayerBuildable(type)) return;
            // 배치 모드로 들어가기 전에 기본 패널로 돌아갑니다.
            CancelMenu();
            BuildRequested?.Invoke(type);
        }
    }

    public bool TryHandleShortcut(InputEvent @event)
    {
        if (@event is not InputEventKey key) return false;
        Key code = key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode;
        if (!key.Pressed) return _heldMenuKeys.Remove(code);
        // 건물 선택 직후 키 반복으로 A 공격이나 메뉴가 다시 실행되지 않도록 합니다.
        if (_heldMenuKeys.Contains(code)) return true;
        if (key.CtrlPressed || key.AltPressed || key.MetaPressed || key.ShiftPressed) return false;
        RefreshSelection();
        int slot = code switch
        {
            Key.Z when _display is DisplayMode.Workers or DisplayMode.TierOne => 1,
            Key.Q when _tierOne => 7,
            Key.W when _tierOne => 8,
            Key.E when _tierOne => 9,
            Key.A when _tierOne => 4,
            Key.S when _tierOne => 5,
            Key.A when _display is DisplayMode.Units or DisplayMode.Workers => 4,
            Key.S when _display is DisplayMode.Units or DisplayMode.Workers => 5,
            Key.D when _display is DisplayMode.Units or DisplayMode.Workers => 6,
            Key.Escape when _tierOne => 1,
            Key.Escape when _display is DisplayMode.Construction or DisplayMode.TownHall or DisplayMode.Barracks && !_slots[8].Disabled => 3,
            _ => 0
        };
        if (slot == 0) return false;
        if (!key.Echo)
        {
            _heldMenuKeys.Add(code);
            RequestSlot(slot);
        }
        return true;
    }

    // 키 놓기 입력은 UI가 소비하기 전에도 받아야 합니다.
    public void ObserveKeyRelease(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: false } key)
            _heldMenuKeys.Remove(key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode);
    }

    public bool CancelMenu()
    {
        if (!_tierOne) return false;
        SetMenuOpen(false);
        RefreshSelection();
        return true;
    }

    private void SetMenuOpen(bool open)
    {
        if (_tierOne == open) return;
        _tierOne = open;
        if (!GodotObject.IsInstanceValid(Units?.Camera)) return;
        Units.Camera.SetMeta(CameraMenuMeta, open);
        // 메뉴를 닫은 선택 키를 놓을 때까지 카메라가 같은 키로 움직이지 않습니다.
        if (!open) Units.Camera.SetMeta(CameraReleaseMeta, true);
    }

    private void OnSelectionChanged()
    {
        SetMenuOpen(false);
        RefreshSelection();
    }

    private void OnFocusExited()
    {
        CancelMenu();
        _heldMenuKeys.Clear();
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouse) AcceptEvent();
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(Units)) Units.SelectionChanged -= OnSelectionChanged;
        if (GodotObject.IsInstanceValid(Buildings))
        {
            Buildings.SelectionChanged -= OnSelectionChanged;
            Buildings.ProductionChanged -= RefreshProduction;
        }
        GetWindow().FocusExited -= OnFocusExited;
        SetMenuOpen(false);
        if (GodotObject.IsInstanceValid(Units?.Camera)) Units.Camera.SetMeta(CameraUnitOrdersMeta, false);
        foreach (StyleBoxFlat style in _styles) style.Dispose();
        _styles.Clear();
    }
}
