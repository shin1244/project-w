using Godot;

// Compact, font-independent stat glyphs. The whole cell is the tooltip target.
public partial class StatIcon : Control
{
    public enum StatKind { Damage, AttackSpeed, Range, Sight, Speed }

    public StatKind Kind { get; set; }
    public string ValueName { get; set; } = "Value";
    private Label _value;
    private string _text = "—";
    private bool _hovered;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(56, 22);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        MouseFilter = MouseFilterEnum.Stop;
        MouseForcePassScrollEvents = false;
        _value = new Label
        {
            Name = ValueName, Text = _text, MouseFilter = MouseFilterEnum.Ignore,
            VerticalAlignment = VerticalAlignment.Center,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
        };
        _value.AddThemeFontSizeOverride("font_size", 11);
        _value.AddThemeColorOverride("font_color", new Color("dce6df"));
        AddChild(_value);
        _value.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _value.OffsetLeft = 23;
        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;
    }

    public void SetValue(string value, string tooltip)
    {
        if (_text != value)
        {
            _text = value;
            if (_value != null) _value.Text = value;
        }
        if (TooltipText != tooltip) TooltipText = tooltip;
    }

    private void OnMouseEntered() { _hovered = true; QueueRedraw(); }
    private void OnMouseExited() { _hovered = false; QueueRedraw(); }

    public override void _Draw()
    {
        Color ink = new(_hovered ? "ecdfaa" : "a9c7b4");
        const float width = 1.6f;
        void Line(float x1, float y1, float x2, float y2) => DrawLine(new(x1, y1), new(x2, y2), ink, width, true);
        void Shape(params Vector2[] points) => DrawPolyline(points, ink, width, true);
        switch (Kind)
        {
            case StatKind.Damage:
                Shape(new(6, 15), new(15, 4), new(19, 3), new(18, 7), new(8, 16));
                Line(4, 12, 11, 18); Line(7, 15, 3, 19); Line(2, 18, 4, 20);
                break;
            case StatKind.AttackSpeed:
                Shape(new(13, 2), new(6, 12), new(11, 12), new(8, 21), new(18, 9), new(13, 9), new(13, 2));
                Line(2, 5, 7, 5); Line(1, 8, 5, 8); Line(15, 17, 20, 17);
                break;
            case StatKind.Range:
                DrawCircle(new(11, 11), 6, ink, false, width, true);
                DrawCircle(new(11, 11), 2, ink, false, width, true);
                Line(11, 1, 11, 6); Line(11, 16, 11, 21);
                Line(1, 11, 6, 11); Line(16, 11, 21, 11);
                break;
            case StatKind.Sight:
                Shape(new(2, 11), new(6, 6), new(11, 4), new(16, 6), new(20, 11), new(16, 16), new(11, 18), new(6, 16), new(2, 11));
                DrawCircle(new(11, 11), 3, ink, false, width, true);
                break;
            case StatKind.Speed:
                Shape(new(9, 3), new(15, 3), new(14, 11), new(19, 14), new(20, 18), new(5, 18), new(5, 15), new(8, 12), new(9, 3));
                Line(2, 6, 6, 6); Line(1, 10, 5, 10); Line(13, 11, 16, 11);
                break;
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouse) AcceptEvent();
    }

    public override void _ExitTree()
    {
        MouseEntered -= OnMouseEntered;
        MouseExited -= OnMouseExited;
    }
}
