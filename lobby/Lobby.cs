using Godot;
using System;

// 현재는 화면 미리보기만 실행한다. 매칭 서버는 QueueRequested/QueueCancelled와 ConfirmMatch에 연결한다.
public partial class Lobby : Control
{
    [Export] public bool PreviewMatching { get; set; } = true;
    [Export(PropertyHint.Range, "0.1,60,0.1")] public double PreviewMatchDelaySeconds { get; set; } = 5;
    public event Action QueueRequested;
    public event Action QueueCancelled;
    public bool IsQueued { get; private set; }
    public double QueueElapsedSeconds { get; private set; }
    private bool _transitioning;
    private Button _start, _cancel;
    private Label _status, _elapsed;

    public override void _Ready()
    {
        _start = GetNode<Button>("%PrimaryButton");
        _cancel = GetNode<Button>("%SecondaryButton");
        _status = GetNode<Label>("%Status");
        _elapsed = GetNode<Label>("%QueueTime");
        _start.Pressed += StartQueue;
        _cancel.Pressed += CancelQueue;
        Refresh();
        _start.GrabFocus();
    }

    public void StartQueue()
    {
        if (IsQueued || _transitioning) return;
        IsQueued = true;
        QueueElapsedSeconds = 0;
        Refresh();
        _cancel.GrabFocus();
        QueueRequested?.Invoke();
    }

    public void CancelQueue()
    {
        if (!IsQueued || _transitioning) return;
        IsQueued = false;
        QueueElapsedSeconds = 0;
        Refresh();
        _start.GrabFocus();
        QueueCancelled?.Invoke();
    }

    public override void _Process(double delta)
    {
        if (!IsQueued || _transitioning) return;
        QueueElapsedSeconds += delta;
        int seconds = (int)QueueElapsedSeconds;
        _elapsed.Text = $"대기 시간  {seconds / 60:00}:{seconds % 60:00}";
        if (PreviewMatching && QueueElapsedSeconds >= Math.Max(0.1, PreviewMatchDelaySeconds)) ConfirmMatch();
    }

    // 별도 매칭 서버가 매칭 확정을 알려주면 호출할 진입점. 취소 후 도착한 응답은 무시한다.
    public void ConfirmMatch()
    {
        if (!IsQueued || _transitioning) return;
        _transitioning = true;
        Error error = GetTree().ChangeSceneToFile("res://lobby/Loading.tscn");
        if (error == Error.Ok) return;
        _transitioning = false;
        IsQueued = false;
        QueueElapsedSeconds = 0;
        Refresh();
        _status.Text = "로딩 화면을 열지 못했어요. 다시 시도해 주세요.";
        _start.GrabFocus();
    }

    private void Refresh()
    {
        _start.Text = IsQueued ? "매칭 중…" : "매칭 시작";
        _start.Disabled = IsQueued;
        _cancel.Disabled = !IsQueued;
        _status.Text = IsQueued ? "함께할 상대를 찾고 있어요" : "매칭할 준비가 되었어요";
        _elapsed.Text = "대기 시간  00:00";
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!IsQueued || !@event.IsActionPressed("ui_cancel")) return;
        CancelQueue();
        GetViewport().SetInputAsHandled();
    }
}
