using Godot;
using System.Collections.Generic;

// 선택에 따른 명령을 표시합니다. 현재는 회관의 일꾼 생성 버튼만 요청에 연결합니다.
public partial class CommandPanel : PanelContainer
{
    [Export] public UnitManager Units;
    [Export] public BuildingManager Buildings;

    private enum DisplayMode { Unset, Empty, Units, TownHall }
    private static readonly int[] NumpadOrder = { 7, 8, 9, 4, 5, 6, 1, 2, 3 };
    private readonly Button[] _slots = new Button[9];
    private readonly List<StyleBoxFlat> _styles = new();
    private DisplayMode _display;

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
            if (NumpadOrder[i] == 7) slot.Pressed += RequestWorker;
            grid.AddChild(slot);
            _slots[i] = slot;
        }
        if (GodotObject.IsInstanceValid(Units)) Units.SelectionChanged += RefreshSelection;
        if (GodotObject.IsInstanceValid(Buildings)) Buildings.SelectionChanged += RefreshSelection;
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
            if (Units.SelectedUnitIds.Count > 0) next = DisplayMode.Units;
            else if (GodotObject.IsInstanceValid(Buildings))
            {
                Building selected = Buildings.SelectedBuilding;
                if (GodotObject.IsInstanceValid(selected) && !selected.IsQueuedForDeletion() &&
                    selected.BuildingType == 0 && selected.SideId == Units.LocalTeam)
                    next = DisplayMode.TownHall;
            }
        }
        if (next == _display) return;
        _display = next;
        for (int i = 0; i < _slots.Length; i++)
        {
            string text = (next, NumpadOrder[i]) switch
            {
                (DisplayMode.Units, 4) => "공격(A)",
                (DisplayMode.Units, 5) => "정지(S)",
                (DisplayMode.Units, 6) => "홀드(D)",
                (DisplayMode.TownHall, 7) => "일꾼",
                _ => ""
            };
            _slots[i].Text = text;
            _slots[i].Disabled = text.Length == 0;
        }
    }

    private void RequestWorker()
    {
        RefreshSelection();
        if (_display == DisplayMode.TownHall) Units.RequestTrainWorker();
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouse) AcceptEvent();
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(Units)) Units.SelectionChanged -= RefreshSelection;
        if (GodotObject.IsInstanceValid(Buildings)) Buildings.SelectionChanged -= RefreshSelection;
        foreach (StyleBoxFlat style in _styles) style.Dispose();
        _styles.Clear();
    }
}
