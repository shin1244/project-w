using Godot;
using System;

// F6: 서버 없이 표시만 확인하는 미리보기. 게임에서는 HP 메시지만 사용합니다.
public partial class HealthBars : Node3D
{
    public override async void _Ready()
    {
        var names = new[] { "Worker", "Knight", "Archer" };
        var amounts = new[] { 50f, 45f, 12f };
        var maxima = new[] { 50f, 100f, 60f };
        for (int i = 0; i < 3; i++)
        {
            var unit = GD.Load<PackedScene>($"res://units/{names[i]}.tscn").Instantiate<Unit>();
            AddChild(unit);
            unit.Position = new Vector3(3 - i * 3, 0, -2);
            unit.ApplyHealth(new HealthSnapshot((uint)(i + 1), amounts[i], maxima[i]));
            unit.SetSelected(i < 2); // 선택한 두 유닛만 표시. 궁수와 회관은 숨깁니다.
        }
        var hall = GD.Load<PackedScene>("res://buildings/TownHall.tscn").Instantiate<Building>();
        AddChild(hall);
        hall.ApplySnapshot(4, 1, 0, 3, 0);
        hall.ApplyHealth(new HealthSnapshot(4, 700, 1000));
        GetNode<Camera3D>("Camera3D").LookAt(new Vector3(0, 1.5f, .5f));
        if (!Array.Exists(OS.GetCmdlineUserArgs(), arg => arg == "--capture")) return;
        for (int i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        Error result = GetViewport().GetTexture().GetImage().SavePng("res://docs/images/health-bars.png");
        GetTree().Quit(result == Error.Ok ? 0 : 1);
    }
}
