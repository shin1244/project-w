using Godot;
using System;
using System.Collections.Generic;

public partial class PlayerInput : Node
{
    [Export] public Camera3D Camera;
    [Export(PropertyHint.Layers3DPhysics)] public uint SelectionMask = 1u << 1;

    public event Action<Unit> UnitClicked;
    public event Action SelectionCleared;
    public event Action<Vector3> GroundRightClicked;

    private const float RayLength = 1000f;
    private static readonly Plane GroundPlane = new(Vector3.Up, 0);

    // 클릭 순간의 광선을 순서대로 저장합니다. 선택 직후 이동 클릭도 순서가 유지됩니다.
    private readonly Queue<(MouseButton Button, Vector3 Origin, Vector3 Direction)> _clicks = new();

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton mouse || !mouse.Pressed)
            return;

        if (mouse.ButtonIndex != MouseButton.Left && mouse.ButtonIndex != MouseButton.Right)
            return;

        if (!GodotObject.IsInstanceValid(Camera))
            return;

        _clicks.Enqueue((
            mouse.ButtonIndex,
            Camera.ProjectRayOrigin(mouse.Position),
            Camera.ProjectRayNormal(mouse.Position)
        ));
        GetViewport().SetInputAsHandled();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!GodotObject.IsInstanceValid(Camera))
        {
            _clicks.Clear();
            return;
        }

        while (_clicks.TryDequeue(out var click))
        {
            if (click.Button == MouseButton.Left)
                HandleSelection(click.Origin, click.Direction);
            else
                HandleGroundClick(click.Origin, click.Direction);
        }
    }

    private void HandleSelection(Vector3 origin, Vector3 direction)
    {
        var query = PhysicsRayQueryParameters3D.Create(
            origin, origin + direction * RayLength);
        query.CollisionMask = SelectionMask;
        query.CollideWithAreas = true;
        query.CollideWithBodies = false;

        var hit = Camera.GetWorld3D().DirectSpaceState.IntersectRay(query);

        // Unit.tscn의 SelectionArea는 Unit 바로 아래에 있습니다.
        if (hit.Count > 0 &&
            hit["collider"].AsGodotObject() is Area3D area &&
            area.GetParent() is Unit unit && !unit.IsQueuedForDeletion())
        {
            UnitClicked?.Invoke(unit);
            return;
        }

        SelectionCleared?.Invoke();
    }

    private void HandleGroundClick(Vector3 origin, Vector3 direction)
    {
        // 현재 서버는 평면 X/Z 좌표를 사용합니다. 통행 여부 검증은 서버에 별도로 구현합니다.
        if (GroundPlane.IntersectsRay(origin, direction) is Vector3 point)
            GroundRightClicked?.Invoke(point);
    }

    public override void _ExitTree() => _clicks.Clear();
}
