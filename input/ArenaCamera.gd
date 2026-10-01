extends Camera3D
## Orthographic map inspection. Does not consume right-click unit orders.

@export var pan_speed := 55.0
@export var minimum_size := 22.0
@export var maximum_size := 125.0
var home_position: Vector3
var home_size: float
var dragging := false

func _ready() -> void:
	var viewport_size := get_viewport().get_visible_rect().size
	size = maxf(size, 184.0 * viewport_size.y / maxf(viewport_size.x, 1.0))
	home_position = position
	home_size = size
	get_window().focus_exited.connect(_cancel_drag)

func _input(event: InputEvent) -> void:
	# UI가 놓기 입력을 소비해도 전장에서 시작한 카메라 드래그는 끝낸다.
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_MIDDLE and not event.pressed:
		_cancel_drag()

func _cancel_drag() -> void:
	dragging = false

func _process(delta: float) -> void:
	var direction := Vector3.ZERO
	var menu_open: bool = get_meta("command_menu_open", false)
	var wait_for_release: bool = get_meta("command_menu_wait_for_release", false)
	if wait_for_release and not (Input.is_physical_key_pressed(KEY_Q) or Input.is_physical_key_pressed(KEY_W) or Input.is_physical_key_pressed(KEY_S) or Input.is_physical_key_pressed(KEY_D)):
		wait_for_release = false
		set_meta("command_menu_wait_for_release", false)
	var letters_enabled := not menu_open and not wait_for_release
	var unit_orders: bool = get_meta("unit_orders_selected", false)
	var hero_q: bool = get_meta("hero_q_shortcut", false)
	# A는 공격 대상 지정에 사용합니다. 왼쪽 이동은 Q 또는 왼쪽 방향키입니다.
	# 건설 메뉴는 Q/W/A/S를 사용하므로 메뉴가 닫힌 뒤 해당 키를 놓을 때까지 문자 이동을 쉽니다.
	# 내 유닛을 선택했으면 S/D는 정지/홀드에 사용합니다. 방향키는 항상 카메라를 이동합니다.
	# 늑대 영웅의 Q는 스킬 전용이며 사망·쿨다운 중에도 카메라를 움직이지 않는다.
	direction.x = float((letters_enabled and not unit_orders and Input.is_physical_key_pressed(KEY_D)) or Input.is_physical_key_pressed(KEY_RIGHT)) - float((letters_enabled and not hero_q and Input.is_physical_key_pressed(KEY_Q)) or Input.is_physical_key_pressed(KEY_LEFT))
	direction.z = float((letters_enabled and not unit_orders and Input.is_physical_key_pressed(KEY_S)) or Input.is_physical_key_pressed(KEY_DOWN)) - float((letters_enabled and Input.is_physical_key_pressed(KEY_W)) or Input.is_physical_key_pressed(KEY_UP))
	if direction.length_squared() > 0:
		position += direction.normalized() * pan_speed * (size / home_size) * delta
		clamp_position()

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventMouseButton:
		if event.button_index == MOUSE_BUTTON_MIDDLE:
			dragging = event.pressed
		if event.pressed and event.button_index == MOUSE_BUTTON_WHEEL_UP:
			size = maxf(minimum_size, size / 1.12)
		elif event.pressed and event.button_index == MOUSE_BUTTON_WHEEL_DOWN:
			size = minf(maximum_size, size * 1.12)
	elif event is InputEventMouseMotion and dragging:
		var units_per_pixel := size / get_viewport().get_visible_rect().size.y
		position.x -= event.relative.x * units_per_pixel
		position.z -= event.relative.y * units_per_pixel / maxf(0.1, -sin(rotation.x))
		clamp_position()
	elif event is InputEventKey and event.pressed and event.keycode == KEY_HOME:
		position = home_position
		size = home_size

func clamp_position() -> void:
	position.x = clampf(position.x, -80, 80)
	position.z = clampf(position.z, home_position.z - 44, home_position.z + 44)
