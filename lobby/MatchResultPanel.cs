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
        screen.GetNode<Label>("Header/Row/PreviewBadge").Text = Lang.T("경기 종료");
        screen.GetNode<Label>("%Kicker").Text = LocalTeam == 0 ? Lang.T("관전 · 최종 결과") : Lang.F("{0}팀 · 최종 결과", LocalTeam);
        var title = screen.GetNode<Label>("%Title");
        title.Text = Result.Title(LocalTeam);
        title.AddThemeFontSizeOverride("font_size", 48);
        title.AddThemeColorOverride("font_color", Result.Winner == 0 ? LobbyPalette.Text :
            Result.Winner == LocalTeam ? LobbyPalette.BrassLight : LobbyPalette.Ember);
        screen.GetNode<Label>("%Description").Text = Result.Description;
        screen.GetNode<Label>("%Status").Text = Lang.F("플레이 시간  {0:00}:{1:00}", Result.Seconds / 60, Result.Seconds % 60);
        screen.GetNode<Label>("%QueueTime").Hide();
        screen.GetNode<Button>("%SecondaryButton").Hide();
        screen.GetNode<Label>("%Footnote").Text = Lang.T("방으로 돌아가 같은 멤버와 다시 시작할 수 있습니다.");
        var back = screen.GetNode<Button>("%PrimaryButton");
        back.Text = Lang.T("방으로 돌아가기");
        back.Pressed += () => BackRequested?.Invoke();
        back.GrabFocus();
    }
}
