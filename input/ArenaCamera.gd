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

func _process(delta: float) -> void:
	var direction := Vector3.ZERO
	direction.x = float(Input.is_physical_key_pressed(KEY_D) or Input.is_physical_key_pressed(KEY_RIGHT)) - float(Input.is_physical_key_pressed(KEY_A) or Input.is_physical_key_pressed(KEY_LEFT))
	direction.z = float(Input.is_physical_key_pressed(KEY_S) or Input.is_physical_key_pressed(KEY_DOWN)) - float(Input.is_physical_key_pressed(KEY_W) or Input.is_physical_key_pressed(KEY_UP))
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
