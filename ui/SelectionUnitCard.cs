using Godot;

// 버튼의 입력/툴팁은 Godot에 맡기고, 작은 초상화 위에 개별 체력을 표시합니다.
public partial class SelectionUnitCard : Button
{
    public uint UnitId { get; private set; }
    private Texture2D _portrait;
    private string _caption;
    private float _current = -1, _maximum = -1;
    private UnitState _state;
    private bool _knownState;

    public void Bind(Unit unit, Texture2D portrait)
    {
        UnitId = unit.UnitId;
        _portrait = portrait;
        _caption = SelectionDetails.UnitName(unit.UnitType);
        Refresh(unit);
        QueueRedraw();
    }

    public void Refresh(Unit unit)
    {
        float current = unit.HealthBar.CurrentHP, maximum = unit.HealthBar.MaxHP;
        if (_current == current && _maximum == maximum && _state == unit.State && _knownState == unit.HasServerState) return;
        _current = current;
        _maximum = maximum;
        _state = unit.State;
        _knownState = unit.HasServerState;
        TooltipText = $"{_caption} · #{UnitId}\n체력 {SelectionDetails.HealthText(unit.HealthBar)}\n" +
            $"{SelectionDetails.ActivityText(unit.State, unit.HasServerState)}\n" +
            "클릭: 단일 선택 / Shift: 선택 제외\nCtrl: 같은 종류 선택 / Ctrl+Shift: 같은 종류 제외";
        QueueRedraw();
    }

    public override void _Draw()
    {
        var portraitRect = new Rect2(3, 3, Size.X - 6, Size.Y - 11);
        if (_portrait != null) DrawTextureRect(_portrait, portraitRect, false);
        DrawRect(new Rect2(3, Size.Y - 27, Size.X - 6, 16), new Color("0d171ddb"));
        Font font = GetThemeFont("font");
        float textWidth = font.GetStringSize(_caption, fontSize: 11).X;
        DrawString(font, new Vector2((Size.X - textWidth) / 2, Size.Y - 15), _caption,
            fontSize: 11, modulate: new Color("e4e9e2"));
        DrawRect(new Rect2(3, Size.Y - 7, Size.X - 6, 4), new Color("36424a"));
        if (_maximum > 0)
        {
            float ratio = Mathf.Clamp(_current / _maximum, 0, 1);
            DrawRect(new Rect2(3, Size.Y - 7, (Size.X - 6) * ratio, 4), SelectionDetails.HealthColor(ratio));
        }
    }
}
