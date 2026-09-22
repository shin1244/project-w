using Godot;

// 화면 좌표를 그대로 사용하는 전체 화면 Control입니다. 마우스 입력은 받지 않습니다.
public partial class SelectionBox : Control
{
    private Rect2 _rect;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Hide();
    }

    public void ShowRect(Rect2 rect)
    {
        _rect = rect;
        Show();
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawRect(_rect, new Color(0.25f, 0.9f, 0.5f, 0.12f));
        DrawRect(_rect, new Color(0.4f, 1f, 0.65f, 0.95f), false, 1.5f);
    }
}
