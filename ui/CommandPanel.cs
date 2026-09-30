using Godot;
using System;
using System.Collections.Generic;

// 선택한 일꾼의 건설 메뉴와 회관의 일꾼 생성 요청을 표시합니다.
public partial class CommandPanel : PanelContainer
{
    [Export] public UnitManager Units;
    [Export] public BuildingManager Buildings;

    public event Action<uint> BuildRequested;
    public event Action BuildMenuOpened;
    public bool IsTierOneMenuOpen => _tierOne;
    public const string CameraMenuMeta = "command_menu_open";
    public const string CameraReleaseMeta = "command_menu_wait_for_release";
    private enum DisplayMode { Unset, Empty, Units, Workers, TierOne, TownHall }
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
            int number = NumpadOrder[i];
            slot.Pressed += () => RequestSlot(number);
            grid.AddChild(slot);
            _slots[i] = slot;
        }
        if (GodotObject.IsInstanceValid(Units)) Units.SelectionChanged += OnSelectionChanged;
        if (GodotObject.IsInstanceValid(Buildings)) Buildings.SelectionChanged += OnSelectionChanged;
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
            else if (GodotObject.IsInstanceValid(Buildings))
            {
                Building selected = Buildings.SelectedBuilding;
                if (GodotObject.IsInstanceValid(selected) && !selected.IsQueuedForDeletion() &&
                    selected.BuildingType == BuildingCatalog.TownHall && selected.SideId == Units.LocalTeam)
                    next = DisplayMode.TownHall;
            }
        }
        if (next != DisplayMode.Workers) SetMenuOpen(false);
        else if (_tierOne) next = DisplayMode.TierOne;
        if (next == _display) return;
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
                _ => ""
            };
            _slots[i].Text = text;
            _slots[i].Disabled = text.Length == 0;
            _slots[i].TooltipText = "";
        }
    }

    private void RequestSlot(int number)
    {
        RefreshSelection();
        if (_display == DisplayMode.TownHall && number == 7) Units.RequestTrainWorker();
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
            Key.Escape when _tierOne => 1,
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
        if (GodotObject.IsInstanceValid(Buildings)) Buildings.SelectionChanged -= OnSelectionChanged;
        GetWindow().FocusExited -= OnFocusExited;
        SetMenuOpen(false);
        foreach (StyleBoxFlat style in _styles) style.Dispose();
        _styles.Clear();
    }
}
