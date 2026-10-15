using Godot;

// 3D 오브젝트의 머리 위치를 화면으로 옮겨 그립니다. 별도 Viewport/텍스처가 필요 없습니다.
public partial class HealthBar : Control
{
    public float CurrentHP { get; private set; }
    public float MaxHP { get; private set; }
    public float Shield { get; private set; }
    public float Mana { get; private set; }
    public float MaxMana { get; private set; }
    private const float ManaStripHeight = 3;
    public float Ratio => MaxHP > 0 ? Mathf.Clamp(CurrentHP / MaxHP, 0, 1) : 0;
    private Node3D _owner;
    private float _worldHeight;
    private bool _hasHealth;
    private bool _selected;
    private bool ShouldShow => _hasHealth && (_selected || _owner is Unit unit && CurrentHP > 0 &&
        (unit.AlwaysShowHealth || CurrentHP < MaxHP));

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

    // 영웅만 MANA를 받는다. 받으면 체력바 아래에 파란 줄이 붙는다.
    public void ApplyMana(float current, float maximum)
    {
        if (!float.IsFinite(current) || !float.IsFinite(maximum) || maximum <= 0 || current < 0) return;
        if (MaxMana <= 0) Size = new Vector2(Size.X, Size.Y + ManaStripHeight);
        Mana = Mathf.Min(current, maximum);
        MaxMana = maximum;
        QueueRedraw();
    }

    public void ApplyShield(float amount)
    {
        if (!float.IsFinite(amount) || amount < 0) return;
        Shield = amount;
        QueueRedraw();
    }

    public void SetSelected(bool selected)
    {
        _selected = selected;
        RefreshVisibility();
    }

    public void SetWorldHeight(float height)
    {
        _worldHeight = height;
        if (ShouldShow) UpdatePosition();
    }

    private void RefreshVisibility()
    {
        bool active = ShouldShow;
        SetProcess(active);
        if (active) UpdatePosition();
        else Hide();
    }

    public void Clear()
    {
        _hasHealth = false;
        Shield = 0;
        if (MaxMana > 0) Size = new Vector2(Size.X, Size.Y - ManaStripHeight);
        Mana = MaxMana = 0;
        SetProcess(false);
        Hide();
    }

    public override void _Process(double delta) => UpdatePosition();

    private void UpdatePosition()
    {
        Camera3D camera = GetViewport().GetCamera3D();
        Vector3 anchor = _owner.GlobalPosition + Vector3.Up * _worldHeight;
        Visible = ShouldShow && _owner.IsVisibleInTree() && camera != null
            && camera.IsPositionInFrustum(anchor);
        if (Visible)
            Position = (camera.UnprojectPosition(anchor) - new Vector2(Size.X / 2, Size.Y)).Round();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("111820"));
        float strip = MaxMana > 0 ? ManaStripHeight : 0;
        Vector2 inner = Size - new Vector2(2, 2 + strip);
        DrawRect(new Rect2(Vector2.One, inner), new Color("343b42"));
        Color fill = Ratio > .5f ? new Color("64cf79")
            : Ratio > .25f ? new Color("edbf55") : new Color("e96860");
        float hp = HealthDisplay.HealthFraction(CurrentHP, MaxHP, Shield);
        float shield = HealthDisplay.ShieldFraction(CurrentHP, MaxHP, Shield);
        DrawRect(new Rect2(Vector2.One, new Vector2(inner.X * hp, inner.Y)), fill);
        if (shield > 0)
            DrawRect(new Rect2(new Vector2(1 + inner.X * hp, 1), new Vector2(inner.X * shield, inner.Y)), HealthDisplay.ShieldColor);
        if (strip > 0)
        {
            var track = new Rect2(new Vector2(1, 1 + inner.Y), new Vector2(inner.X, strip - 1));
            DrawRect(track, new Color("1b2a44"));
            DrawRect(new Rect2(track.Position, new Vector2(track.Size.X * Mathf.Clamp(Mana / MaxMana, 0, 1), track.Size.Y)), new Color("5aa0ff"));
        }
    }
}
