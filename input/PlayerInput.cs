using Godot;
using System;
using System.Collections.Generic;

public partial class PlayerInput : Node
{
	[Export] public Camera3D Camera;
	[Export(PropertyHint.Layers3DPhysics)] public uint SelectionMask = 1u << 1;
	[Export(PropertyHint.Layers3DPhysics)] public uint ResourceMask = 1u << 2;
	[Export(PropertyHint.Layers3DPhysics)] public uint BuildingMask = 1u << 3;
	[Export(PropertyHint.Layers3DPhysics)] public uint ObjectiveMask = TributeEventView.PickLayer;
	[Export] public SelectionBox SelectionBox;
	[Export] public BuildingPlacement Placement;
	[Export] public CommandPanel Commands;
	[Export] public HeroSkillPanel Skills;

	public event Action<Unit> UnitClicked;
	public event Action<Unit, bool, bool> ModifiedUnitSelectionRequested;
	public event Action<Rect2> ModifiedBoxSelectionRequested;
	public event Action<int, bool, bool> ControlGroupRequested;
	public event Action CameraHomeRequested;
	public event Action<Building> BuildingClicked;
	public event Action SelectionCleared;
	// target: Unit / Building / ResourceNode / TributeEventView / null(땅).
	public event Action<Node3D, Vector3> ContextClicked;
	public event Action<Node3D> AttackTargetClicked;
	public event Action<Vector3> AttackGroundClicked;
	public event Action StopRequested;
	public event Action HoldRequested;
	public event Action<SkillInput> SkillRequested;
	public event Action<int?> SkillTargetingChanged;
	public event Action<bool> AttackTargetingChanged;
	public bool IsAttackTargeting { get; private set; }
	public bool IsSkillTargeting => _skillSlot.HasValue;
	private int? _skillSlot;
	private int _skillGeneration;

	private const float RayLength = 1000f;
	private static readonly Plane GroundPlane = new(Vector3.Up, 0);

	private bool _leftPressed;
	private bool _dragging;
	private Vector2 _dragStart;
	private bool _shiftSelection, _doubleClick;
	private int _lastGroup;
	private ulong _lastGroupTime;

	private const float DragThreshold = 6f;

	public event Action<Rect2> BoxSelectionRequested;

	// 광선 조회는 물리 프레임에서 실행하고, 박스 선택과 이동도 같은 순서를 지킵니다.
	private readonly Queue<Action> _actions = new();

	public override void _Ready()
	{
		GetWindow().FocusExited += CancelInput;
		if (GodotObject.IsInstanceValid(Skills))
		{
			Skills.TargetingRequested += BeginSkillTargeting;
			Skills.AvailabilityChanged += OnSkillAvailabilityChanged;
		}
		if (GodotObject.IsInstanceValid(Commands))
		{
			Commands.BuildMenuOpened += OnBuildMenuOpened;
			Commands.AttackRequested += BeginAttackTargeting;
			Commands.StopRequested += QueueStop;
			Commands.HoldRequested += QueueHold;
		}
	}

	private void BeginAttackTargeting()
	{
		if (!GodotObject.IsInstanceValid(Camera)) return;
		SetSkillTargeting(null);
		Placement?.Cancel();
		CancelDrag();
		_lastGroup = 0;
		SetAttackTargeting(true);
	}

	private void BeginSkillTargeting(int slot)
	{
		if (Skills?.CanUse(slot) != true || !GodotObject.IsInstanceValid(Camera)) return;
		Placement?.Cancel();
		Commands?.CancelMenu();
		CancelDrag();
		SetAttackTargeting(false);
		_lastGroup = 0;
		if (Skills.Definition(slot)?.Target == SkillTargetMode.Self)
		{
			SetSkillTargeting(null);
			SkillRequested?.Invoke(new SkillInput(slot));
		}
		else SetSkillTargeting(slot);
	}

	private void OnSkillAvailabilityChanged()
	{
		if (_skillSlot is int slot && Skills?.CanUse(slot) != true) SetSkillTargeting(null);
	}

	private void QueueStop()
	{
		CancelTargeting();
		CancelDrag();
		_actions.Enqueue(() => StopRequested?.Invoke());
	}

	private void QueueHold()
	{
		CancelTargeting();
		CancelDrag();
		_actions.Enqueue(() => HoldRequested?.Invoke());
	}

	private void OnBuildMenuOpened()
	{
		Placement?.Cancel();
		CancelDrag();
		SetAttackTargeting(false);
		SetSkillTargeting(null);
		_lastGroup = 0;
	}

	// UI 위에서 버튼을 떼어도, 게임 화면에서 시작한 드래그를 끝냅니다.
	public override void _Input(InputEvent @event)
	{
		Commands?.ObserveKeyRelease(@event);
		if (Placement is { Active: true } &&
			(@event is InputEventKey { Pressed: true, Keycode: Key.Escape } ||
			 @event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right }))
		{
			Placement.Cancel();
			GetViewport().SetInputAsHandled();
			return;
		}
		if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape } &&
			Commands?.TryHandleShortcut(@event) == true)
		{
			GetViewport().SetInputAsHandled();
			return;
		}
		if ((IsAttackTargeting || IsSkillTargeting) && @event is InputEventKey { Pressed: true, Keycode: Key.Escape })
		{
			SetAttackTargeting(false);
			SetSkillTargeting(null);
			GetViewport().SetInputAsHandled();
			return;
		}
		if (_leftPressed)
			HandleDragEvent(@event);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventKey { Pressed: true } homeKey &&
			(homeKey.PhysicalKeycode == Key.Space || homeKey.Keycode == Key.Space))
		{
			if (!homeKey.Echo && !_leftPressed && !homeKey.CtrlPressed && !homeKey.AltPressed && !homeKey.MetaPressed)
				_actions.Enqueue(() => CameraHomeRequested?.Invoke());
			GetViewport().SetInputAsHandled();
			return;
		}
		if (!_leftPressed && Skills?.TryHandleShortcut(@event) == true)
		{
			GetViewport().SetInputAsHandled();
			return;
		}
		if (!_leftPressed && Commands?.TryHandleShortcut(@event) == true)
		{
			_lastGroup = 0;
			if (Commands.IsTierOneMenuOpen) Placement?.Cancel();
			GetViewport().SetInputAsHandled();
			return;
		}
		if (@event is InputEventKey groupKey && groupKey.Pressed && TryGroupNumber(groupKey, out int number))
		{
			if (!groupKey.Echo && !_leftPressed && !groupKey.AltPressed && !groupKey.MetaPressed && !groupKey.ShiftPressed)
			{
				bool save = groupKey.CtrlPressed;
				ulong now = Time.GetTicksMsec();
				bool focus = !save && _lastGroup == number && now - _lastGroupTime <= 350;
				_lastGroup = save || focus ? 0 : number;
				_lastGroupTime = now;
				Placement?.Cancel();
				Commands?.CancelMenu();
				SetAttackTargeting(false);
				SetSkillTargeting(null);
				_actions.Enqueue(() => ControlGroupRequested?.Invoke(number, save, focus));
			}
			GetViewport().SetInputAsHandled();
			return;
		}
		if (@event is InputEventKey key && key.Pressed &&
			(key.PhysicalKeycode == Key.A || key.Keycode == Key.A))
		{
			_lastGroup = 0;
			if (!key.Echo && !_leftPressed && GodotObject.IsInstanceValid(Camera))
			{
				BeginAttackTargeting();
			}
			GetViewport().SetInputAsHandled();
			return;
		}

		if (Placement is { Active: true } && @event is InputEventMouseButton placementClick &&
			placementClick.ButtonIndex is MouseButton.Left or MouseButton.Right)
		{
			if (placementClick.Pressed)
			{
				if (placementClick.ButtonIndex == MouseButton.Right) Placement.Cancel();
				else Placement.TryPlace(placementClick.Position);
			}
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
		_lastGroup = 0;

		if (mouse.ButtonIndex == MouseButton.Left)
		{
			if (IsSkillTargeting)
			{
				QueueSkillTarget(mouse.Position);
			}
			else if (IsAttackTargeting)
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
				_shiftSelection = mouse.ShiftPressed;
				_doubleClick = mouse.DoubleClick;
			}
		}
		else
		{
			if (IsSkillTargeting)
			{
				SetSkillTargeting(null);
				GetViewport().SetInputAsHandled();
				return;
			}
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
				bool shift = _shiftSelection;
				_actions.Enqueue(() =>
				{
					if (shift) ModifiedBoxSelectionRequested?.Invoke(rect);
					else BoxSelectionRequested?.Invoke(rect);
				});
			}
			else
				QueueClick(MouseButton.Left, mouse.Position, _shiftSelection, _doubleClick);

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
		UpdateTargetCursor();
		AttackTargetingChanged?.Invoke(active);
	}

	private void SetSkillTargeting(int? slot)
	{
		_skillSlot = slot;
		_skillGeneration++;
		Skills?.SetTargeting(slot);
		UpdateTargetCursor();
		SkillTargetingChanged?.Invoke(slot);
	}

	private void UpdateTargetCursor() => Input.SetDefaultCursorShape(
		IsAttackTargeting || IsSkillTargeting ? Input.CursorShape.Cross : Input.CursorShape.Arrow);

	private void CancelInput()
	{
		Placement?.Cancel();
		Commands?.CancelMenu();
		CancelDrag();
		SetAttackTargeting(false);
		SetSkillTargeting(null);
		_lastGroup = 0;
		_actions.Clear();
	}

	public void ResetInteraction() => CancelInput();

	public void CancelTargeting()
	{
		Placement?.Cancel();
		Commands?.CancelMenu();
		SetAttackTargeting(false);
		SetSkillTargeting(null);
		_lastGroup = 0;
	}

	public void QueueMinimapMove(Vector3 point)
	{
		CancelTargeting();
		_actions.Enqueue(() => ContextClicked?.Invoke(null, point));
	}

	private static bool TryGroupNumber(InputEventKey key, out int number)
	{
		Key code = key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode;
		number = (int)code - (int)Key.Key0;
		return number >= 1 && number <= 9;
	}

	private void QueueAttack(Vector2 position)
	{
		Vector3 origin = Camera.ProjectRayOrigin(position);
		Vector3 direction = Camera.ProjectRayNormal(position);
		_actions.Enqueue(() =>
		{
			Node3D target = FindTarget(origin, direction, SelectionMask | BuildingMask);
			if (target != null)
				AttackTargetClicked?.Invoke(target);
			else if (GroundPlane.IntersectsRay(origin, direction) is Vector3 point)
				AttackGroundClicked?.Invoke(point);
		});
	}

	private void QueueSkillTarget(Vector2 position)
	{
		int slot = _skillSlot.Value, generation = _skillGeneration;
		uint? hero = Skills.HeroUnitId;
		Vector3 origin = Camera.ProjectRayOrigin(position);
		Vector3 direction = Camera.ProjectRayNormal(position);
		_actions.Enqueue(() =>
		{
			if (_skillSlot != slot || generation != _skillGeneration || !Skills.CanUse(slot) || Skills.HeroUnitId != hero) return;
			if (Skills.Definition(slot)?.Target == SkillTargetMode.Point)
			{
				if (GroundPlane.IntersectsRay(origin, direction) is not Vector3 point) return;
				SetSkillTargeting(null);
				SkillRequested?.Invoke(new SkillInput(slot, Point: point));
				return;
			}
			Node3D target = FindTarget(origin, direction, SelectionMask | BuildingMask);
			if (!Skills.AcceptsTarget(slot, target)) return;
			SetSkillTargeting(null);
			SkillRequested?.Invoke(new SkillInput(slot, target));
		});
	}

	private void QueueClick(MouseButton button, Vector2 position, bool shift = false, bool sameType = false)
	{
		Vector3 origin = Camera.ProjectRayOrigin(position);
		Vector3 direction = Camera.ProjectRayNormal(position);
		_actions.Enqueue(() =>
		{
			if (button == MouseButton.Left)
				HandleSelection(origin, direction, shift, sameType);
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

	private void HandleSelection(Vector3 origin, Vector3 direction, bool shift, bool sameType)
	{
		Node3D target = FindTarget(origin, direction, SelectionMask | BuildingMask);
		if (target is Unit unit)
		{
			if (shift || sameType) ModifiedUnitSelectionRequested?.Invoke(unit, shift, sameType);
			else UnitClicked?.Invoke(unit);
		}
		else if (target is Building building && !shift)
			BuildingClicked?.Invoke(building);
		else if (!shift)
			SelectionCleared?.Invoke();
	}

	private Node3D FindTarget(Vector3 origin, Vector3 direction, uint mask)
	{
		var query = PhysicsRayQueryParameters3D.Create(
			origin, origin + direction * RayLength);
		query.CollisionMask = mask;
		query.CollideWithAreas = true;
		query.CollideWithBodies = false;

		var hit = Camera.GetWorld3D().DirectSpaceState.IntersectRay(query);

		// 같은 광선에서 가장 먼저 닿은 유닛/건물/자원을 고릅니다.
		if (hit.Count > 0 &&
			hit["collider"].AsGodotObject() is Area3D area &&
			area.GetParent() is Node3D target && (target is Unit or Building or ResourceNode or TributeEventView) &&
			!target.IsQueuedForDeletion() && target is not Unit { IsDying: true } &&
			(target is not TributeEventView tribute || tribute.Active && tribute.IsVisibleInTree()))
			return target;
		return null;
	}

	private void HandleRightClick(Vector3 origin, Vector3 direction)
	{
		// 이동 목적지는 Y=0 평면, 대상 판별은 유닛/건물/자원 Area3D를 사용합니다.
		if (GroundPlane.IntersectsRay(origin, direction) is Vector3 point)
		{
			Node3D target = FindTarget(origin, direction, SelectionMask | ResourceMask | BuildingMask | ObjectiveMask);
			ContextClicked?.Invoke(target, point);
		}
	}

	public override void _ExitTree()
	{
		GetWindow().FocusExited -= CancelInput;
		if (GodotObject.IsInstanceValid(Skills))
		{
			Skills.TargetingRequested -= BeginSkillTargeting;
			Skills.AvailabilityChanged -= OnSkillAvailabilityChanged;
		}
		if (GodotObject.IsInstanceValid(Commands))
		{
			Commands.BuildMenuOpened -= OnBuildMenuOpened;
			Commands.AttackRequested -= BeginAttackTargeting;
			Commands.StopRequested -= QueueStop;
			Commands.HoldRequested -= QueueHold;
		}
		SetAttackTargeting(false);
		_skillSlot = null;
		UpdateTargetCursor();
		_actions.Clear();
	}
}
