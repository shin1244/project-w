using Godot;
using System;

// 실제 게임 서버에 접속하지 않는 로딩 화면 미리보기.
public partial class Loading : Control
{
    [Export(PropertyHint.Range, "0.1,30,0.1")] public double PreviewDurationSeconds { get; set; } = 4;
    private double _elapsed;
    private bool _returning;
    private ProgressBar _progress;
    private Label _title, _description, _status, _percent;

    public override void _Ready()
    {
        _progress = GetNode<ProgressBar>("%Progress");
        _title = GetNode<Label>("%Title");
        _description = GetNode<Label>("%Description");
        _status = GetNode<Label>("%Status");
        _percent = GetNode<Label>("%ProgressValue");
        var back = GetNode<Button>("%PrimaryButton");
        back.Pressed += ReturnToLobby;
        back.GrabFocus();
        UpdateProgress();
    }

    public override void _Process(double delta)
    {
        if (_returning || _progress.Value >= 100) return;
        _elapsed += delta;
        UpdateProgress();
    }

    private void UpdateProgress()
    {
        double progress = Math.Clamp(_elapsed / Math.Max(0.1, PreviewDurationSeconds), 0, 1);
        _progress.Value = progress * 100;
        _percent.Text = $"{(int)(progress * 100)}%";
        _status.Text = progress < 0.35 ? "전장을 확인하고 있어요" :
            progress < 0.75 ? "부대를 준비하고 있어요" : progress < 1 ? "곧 준비가 끝나요" : "준비 완료";
        if (progress < 1) return;
        _title.Text = "준비가 끝났어요";
        _description.Text = "로비로 돌아가 매칭을 다시 체험할 수 있어요.";
    }

    public void ReturnToLobby()
    {
        if (_returning) return;
        _returning = true;
        if (GetTree().ChangeSceneToFile("res://lobby/Lobby.tscn") == Error.Ok) return;
        _returning = false;
        _status.Text = "로비를 열지 못했어요. 다시 시도해 주세요.";
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!@event.IsActionPressed("ui_cancel")) return;
        GetViewport().SetInputAsHandled();
        ReturnToLobby();
    }
}
