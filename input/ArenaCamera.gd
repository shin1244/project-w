extends Camera3D
## Orthographic map inspection. Does not consume right-click unit orders.

@export var pan_speed := 55.0
@export var minimum_size := 22.0
@export var maximum_size := 125.0
@export var edge_pan_enabled := false
@export var edge_margin := 18.0
var home_position: Vector3
var home_size: float
var dragging := false
var window_focused := false
var mouse_inside := false
var mouse_position := Vector2.ZERO
var ground_bounds := Rect2(-80, -44, 160, 88)

func _ready() -> void:
	var viewport_size := get_viewport().get_visible_rect().size
	size = maxf(size, 184.0 * viewport_size.y / maxf(viewport_size.x, 1.0))
	home_position = position
	home_size = size
	window_focused = get_window().has_focus()
	get_window().focus_entered.connect(func(): window_focused = true)
	get_window().focus_exited.connect(_focus_lost)
	get_window().mouse_entered.connect(func(): mouse_inside = true)
	get_window().mouse_exited.connect(func(): mouse_inside = false)
	call_deferred("configure_map_bounds")

func configure_map_bounds() -> void:
	var terrain := get_node_or_null("../Ground/SymmetricArena")
	if terrain == null or not terrain.has_meta("origin"):
		return
	var dimensions: Vector2 = terrain.get_meta("dimensions")
	ground_bounds = Rect2(terrain.get_meta("origin"), dimensions).grow(-4.0)
	# Include the north/south bastions when returning to the full-map view.
	var viewport_size := get_viewport().get_visible_rect().size
	var fit_width := (dimensions.x + 12.0) * viewport_size.y / maxf(viewport_size.x, 1.0)
	var fit_depth := dimensions.y * absf(sin(rotation.x)) + 12.0
	var using_home_size := is_equal_approx(size, home_size)
	home_size = maxf(home_size, maxf(fit_width, fit_depth))
	maximum_size = maxf(maximum_size, home_size)
	if using_home_size:
		size = home_size

func _input(event: InputEvent) -> void:
	if event is InputEventMouseMotion:
		mouse_position = event.position
		mouse_inside = get_viewport().get_visible_rect().has_point(mouse_position)
	# UI가 놓기 입력을 소비해도 전장에서 시작한 카메라 드래그는 끝낸다.
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_MIDDLE and not event.pressed:
		_cancel_drag()

func _cancel_drag() -> void:
	dragging = false

func _focus_lost() -> void:
	window_focused = false
	_cancel_drag()

func _edge_direction() -> Vector3:
	# 미니맵·선택 드래그 및 가운데 버튼 이동과 가장자리 이동이 겹치지 않게 한다.
	if not edge_pan_enabled or not window_focused or not mouse_inside or dragging or Input.is_mouse_button_pressed(MOUSE_BUTTON_LEFT):
		return Vector3.ZERO
	var rect := get_viewport().get_visible_rect()
	var mouse := mouse_position
	if not rect.has_point(mouse):
		return Vector3.ZERO
	return Vector3(
		float(mouse.x >= rect.end.x - edge_margin) - float(mouse.x < rect.position.x + edge_margin),
		0,
		float(mouse.y >= rect.end.y - edge_margin) - float(mouse.y < rect.position.y + edge_margin))

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
	var hero_w: bool = get_meta("hero_w_shortcut", false)
	# A는 공격 대상 지정에 사용합니다. 왼쪽 이동은 Q 또는 왼쪽 방향키입니다.
	# 건설 메뉴는 Q/W/A/S를 사용하므로 메뉴가 닫힌 뒤 해당 키를 놓을 때까지 문자 이동을 쉽니다.
	# 내 유닛을 선택했으면 S/D는 정지/홀드에 사용합니다. 방향키는 항상 카메라를 이동합니다.
	# 영웅의 Q/W는 사망·쿨다운 중에도 스킬 전용이다.
	direction.x = float((letters_enabled and not unit_orders and Input.is_physical_key_pressed(KEY_D)) or Input.is_physical_key_pressed(KEY_RIGHT)) - float((letters_enabled and not hero_q and Input.is_physical_key_pressed(KEY_Q)) or Input.is_physical_key_pressed(KEY_LEFT))
	direction.z = float((letters_enabled and not unit_orders and Input.is_physical_key_pressed(KEY_S)) or Input.is_physical_key_pressed(KEY_DOWN)) - float((letters_enabled and not hero_w and Input.is_physical_key_pressed(KEY_W)) or Input.is_physical_key_pressed(KEY_UP))
	direction += _edge_direction()
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
	position.x = clampf(position.x, ground_bounds.position.x, ground_bounds.end.x)
	position.z = clampf(position.z, home_position.z + ground_bounds.position.y, home_position.z + ground_bounds.end.y)
