using Godot;
using System;

public partial class MatchResultPanel : CanvasLayer
{
    public MatchResultSnapshot Result { get; init; }
    public uint LocalTeam { get; init; }
    public event Action BackRequested;

    public override void _Ready()
    {
        Layer = 120;
        var screen = GD.Load<PackedScene>("res://lobby/MatchScreen.tscn").Instantiate<Control>();
        AddChild(screen);
        screen.GetNode<Label>("Header/Row/PreviewBadge").Text = "경기 종료";
        screen.GetNode<Label>("%Kicker").Text = $"{LocalTeam}팀 · 최종 결과";
        var title = screen.GetNode<Label>("%Title");
        title.Text = Result.Title(LocalTeam);
        title.AddThemeFontSizeOverride("font_size", 48);
        title.AddThemeColorOverride("font_color", Result.Winner == 0 ? new Color("c9d5da") :
            Result.Winner == LocalTeam ? new Color("b7dca2") : new Color("e3a997"));
        screen.GetNode<Label>("%Description").Text = Result.Description;
        screen.GetNode<Label>("%Status").Text = $"플레이 시간  {Result.Seconds / 60:00}:{Result.Seconds % 60:00}";
        screen.GetNode<Label>("%QueueTime").Hide();
        screen.GetNode<Button>("%SecondaryButton").Hide();
        screen.GetNode<Label>("%Footnote").Text = "로비로 돌아가 새로운 경기를 시작할 수 있습니다.";
        var back = screen.GetNode<Button>("%PrimaryButton");
        back.Text = "로비로 돌아가기";
        back.Pressed += () => BackRequested?.Invoke();
        back.GrabFocus();
    }
}
