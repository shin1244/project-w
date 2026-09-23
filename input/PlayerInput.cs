using Godot;
using System;
using System.Collections.Generic;

public partial class PlayerInput : Node
{
    [Export] public Camera3D Camera;
    [Export(PropertyHint.Layers3DPhysics)] public uint SelectionMask = 1u << 1;
    [Export(PropertyHint.Layers3DPhysics)] public uint ResourceMask = 1u << 2;
    [Export] public SelectionBox SelectionBox;

    public event Action<Unit> UnitClicked;
    public event Action SelectionCleared;
    // target: Unit / ResourceNode / null(땅). 어떤 명령인지는 UnitManager가 결정합니다.
    public event Action<Node3D, Vector3> ContextClicked;
    public event Action<Unit> AttackTargetClicked;
    public event Action<Vector3> AttackGroundClicked;
    public bool IsAttackTargeting { get; private set; }

    private const float RayLength = 1000f;
    private static readonly Plane GroundPlane = new(Vector3.Up, 0);

    private bool _leftPressed;
    private bool _dragging;
    private Vector2 _dragStart;

    private const float DragThreshold = 6f;

    public event Action<Rect2> BoxSelectionRequested;

    // 광선 조회는 물리 프레임에서 실행하고, 박스 선택과 이동도 같은 순서를 지킵니다.
    private readonly Queue<Action> _actions = new();

    public override void _Ready() => GetWindow().FocusExited += CancelInput;

    // UI 위에서 버튼을 떼어도, 게임 화면에서 시작한 드래그를 끝냅니다.
    public override void _Input(InputEvent @event)
    {
        if (IsAttackTargeting && @event is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            SetAttackTargeting(false);
            GetViewport().SetInputAsHandled();
            return;
        }
        if (_leftPressed)
            HandleDragEvent(@event);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed &&
            (key.PhysicalKeycode == Key.A || key.Keycode == Key.A))
        {
            if (!key.Echo && !_leftPressed && GodotObject.IsInstanceValid(Camera))
                SetAttackTargeting(true);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_leftPressed)
        {
            HandleDragEvent(@event);
            return;
        }

        if (@event is not InputEventMouseButton mouse || !mouse.Pressed)
            return;

        if (mouse.ButtonIndex != MouseButton.Left && mouse.ButtonIndex != MouseButton.Right)
            return;

        if (!GodotObject.IsInstanceValid(Camera))
            return;

        if (mouse.ButtonIndex == MouseButton.Left)
        {
            if (IsAttackTargeting)
            {
                // 공격 클릭은 선택을 바꾸지 않으며, 한 번 클릭하면 모드가 종료됩니다.
                QueueAttack(mouse.Position);
                SetAttackTargeting(false);
            }
            else
            {
                _leftPressed = true;
                _dragging = false;
                _dragStart = mouse.Position;
            }
        }
        else
        {
            SetAttackTargeting(false);
            QueueClick(mouse.ButtonIndex, mouse.Position);
        }
        GetViewport().SetInputAsHandled();
    }

    private void HandleDragEvent(InputEvent @event)
    {
        if (!GodotObject.IsInstanceValid(Camera))
        {
            CancelDrag();
            return;
        }

        if (@event is InputEventKey key && key.Pressed && key.Keycode == Key.Escape ||
            @event is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true })
        {
            CancelDrag();
            GetViewport().SetInputAsHandled();
        }
        else if (@event is InputEventMouseMotion motion)
        {
            UpdateDrag(motion.Position);
            GetViewport().SetInputAsHandled();
        }
        else if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } mouse)
        {
            UpdateDrag(mouse.Position);
            if (_dragging)
            {
                Rect2 rect = new Rect2(_dragStart, mouse.Position - _dragStart).Abs();
                _actions.Enqueue(() => BoxSelectionRequested?.Invoke(rect));
            }
            else
                QueueClick(MouseButton.Left, mouse.Position);

            CancelDrag();
            GetViewport().SetInputAsHandled();
        }
    }

    private void UpdateDrag(Vector2 position)
    {
        _dragging |= _dragStart.DistanceSquaredTo(position) >= DragThreshold * DragThreshold;
        if (_dragging)
            SelectionBox?.ShowRect(new Rect2(_dragStart, position - _dragStart).Abs());
    }

    private void CancelDrag()
    {
        _leftPressed = false;
        _dragging = false;
        SelectionBox?.Hide();
    }

    private void SetAttackTargeting(bool active)
    {
        if (IsAttackTargeting == active)
            return;
        IsAttackTargeting = active;
        Input.SetDefaultCursorShape(active ? Input.CursorShape.Cross : Input.CursorShape.Arrow);
    }

    private void CancelInput()
    {
        CancelDrag();
        SetAttackTargeting(false);
    }

    private void QueueAttack(Vector2 position)
    {
        Vector3 origin = Camera.ProjectRayOrigin(position);
        Vector3 direction = Camera.ProjectRayNormal(position);
        _actions.Enqueue(() =>
        {
            Unit target = FindUnit(origin, direction);
            if (target != null)
                AttackTargetClicked?.Invoke(target);
            else if (GroundPlane.IntersectsRay(origin, direction) is Vector3 point)
                AttackGroundClicked?.Invoke(point);
        });
    }

    private void QueueClick(MouseButton button, Vector2 position)
    {
        Vector3 origin = Camera.ProjectRayOrigin(position);
        Vector3 direction = Camera.ProjectRayNormal(position);
        _actions.Enqueue(() =>
        {
            if (button == MouseButton.Left)
                HandleSelection(origin, direction);
            else
                HandleRightClick(origin, direction);
        });
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!GodotObject.IsInstanceValid(Camera))
        {
            _actions.Clear();
            CancelInput();
            return;
        }

        while (_actions.TryDequeue(out var action))
            action();
    }

    private void HandleSelection(Vector3 origin, Vector3 direction)
    {
        Unit unit = FindUnit(origin, direction);
        if (unit != null)
            UnitClicked?.Invoke(unit);
        else
            SelectionCleared?.Invoke();
    }

    private Unit FindUnit(Vector3 origin, Vector3 direction)
        => FindTarget(origin, direction, SelectionMask) as Unit;

    private Node3D FindTarget(Vector3 origin, Vector3 direction, uint mask)
    {
        var query = PhysicsRayQueryParameters3D.Create(
            origin, origin + direction * RayLength);
        query.CollisionMask = mask;
        query.CollideWithAreas = true;
        query.CollideWithBodies = false;

        var hit = Camera.GetWorld3D().DirectSpaceState.IntersectRay(query);

        // 두 종류를 한 번에 조회하므로 광선에 먼저 닿은 대상을 고릅니다.
        if (hit.Count > 0 &&
            hit["collider"].AsGodotObject() is Area3D area &&
            area.GetParent() is Node3D target && (target is Unit or ResourceNode) &&
            !target.IsQueuedForDeletion() && target is not Unit { IsDying: true })
            return target;
        return null;
    }

    private void HandleRightClick(Vector3 origin, Vector3 direction)
    {
        // 이동 목적지는 Y=0 평면, 대상 판별은 유닛/자원 Area3D를 사용합니다.
        if (GroundPlane.IntersectsRay(origin, direction) is Vector3 point)
        {
            Node3D target = FindTarget(origin, direction, SelectionMask | ResourceMask);
            ContextClicked?.Invoke(target, point);
        }
    }

    public override void _ExitTree()
    {
        GetWindow().FocusExited -= CancelInput;
        SetAttackTargeting(false);
        _actions.Clear();
    }
}
