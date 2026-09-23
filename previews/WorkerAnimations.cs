using Godot;
using System;

// 서버 없이 F6으로 세 자세를 나란히 확인합니다.
public partial class WorkerAnimations : Node3D
{
    public override async void _Ready()
    {
        PackedScene scene = GD.Load<PackedScene>("res://units/Worker.tscn");
        bool capture = Array.Exists(OS.GetCmdlineUserArgs(), arg => arg == "--capture");
        for (int i = 0; i < 3; i++)
        {
            Unit unit = scene.Instantiate<Unit>();
            AddChild(unit);
            unit.Position = new Vector3((1 - i) * 2.4f, 0, 0);
            unit.ApplyServerState(new UnitState(i == 1 ? UnitActivity.Gather : UnitActivity.Idle,
                i == 2 ? 5 : 0, 0, 0));
            if (capture)
            {
                AnimationPlayer player = unit.GetNode<AnimationPlayer>("AnimationPlayer");
                player.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual;
                player.Advance(i == 1 ? .3 : .2);
                player.Advance(0);
            }
        }
        GetNode<Camera3D>("Camera3D").LookAt(new Vector3(0, 1, 0));
        if (!capture) return;
        for (int i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        Error result = GetViewport().GetTexture().GetImage().SavePng("res://docs/images/worker-animations.png");
        GetTree().Quit(result == Error.Ok ? 0 : 1);
    }
}
