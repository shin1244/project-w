using Godot;

// 3D 오브젝트의 머리 위치를 화면으로 옮겨 그립니다. 별도 Viewport/텍스처가 필요 없습니다.
public partial class HealthBar : Control
{
    public float CurrentHP { get; private set; }
    public float MaxHP { get; private set; }
    public float Ratio => MaxHP > 0 ? Mathf.Clamp(CurrentHP / MaxHP, 0, 1) : 0;
    private Node3D _owner;
    private float _worldHeight;
    private bool _hasHealth;
    private bool _selected;

    public static HealthBar Attach(Node3D owner, float height, float width = 52)
    {
        var bar = new HealthBar
        {
            Name = "HealthBar", _owner = owner, _worldHeight = height,
            Size = new Vector2(width, 7), MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 100, Visible = false
        };
        owner.AddChild(bar);
        bar.SetProcess(false);
        return bar;
    }

    public void Apply(float current, float maximum)
    {
        if (!float.IsFinite(current) || !float.IsFinite(maximum) || maximum <= 0 || current < 0) return;
        CurrentHP = Mathf.Min(current, maximum);
        MaxHP = maximum;
        _hasHealth = true;
        RefreshVisibility();
        QueueRedraw();
    }

    public void SetSelected(bool selected)
    {
        _selected = selected;
        RefreshVisibility();
    }

    private void RefreshVisibility()
    {
        bool active = _selected && _hasHealth;
        SetProcess(active);
        if (active) UpdatePosition();
        else Hide();
    }

    public void Clear()
    {
        _hasHealth = false;
        SetProcess(false);
        Hide();
    }

    public override void _Process(double delta) => UpdatePosition();

    private void UpdatePosition()
    {
        Camera3D camera = GetViewport().GetCamera3D();
        Vector3 anchor = _owner.GlobalPosition + Vector3.Up * _worldHeight;
        Visible = _selected && _hasHealth && _owner.IsVisibleInTree() && camera != null
            && camera.IsPositionInFrustum(anchor);
        if (Visible)
            Position = (camera.UnprojectPosition(anchor) - new Vector2(Size.X / 2, Size.Y)).Round();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("111820"));
        Vector2 inner = Size - new Vector2(2, 2);
        DrawRect(new Rect2(Vector2.One, inner), new Color("343b42"));
        Color fill = Ratio > .5f ? new Color("64cf79")
            : Ratio > .25f ? new Color("edbf55") : new Color("e96860");
        DrawRect(new Rect2(Vector2.One, new Vector2(inner.X * Ratio, inner.Y)), fill);
    }
}
