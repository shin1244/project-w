extends Node3D

var yaw := 0.62
var dragging := false
@onready var camera: Camera3D = $Camera3D

func _ready() -> void:
	update_camera()
	if "--capture" in OS.get_cmdline_user_args():
		capture.call_deferred()

func update_camera() -> void:
	camera.position = Vector3(sin(yaw) * 9.0, 6.4, -cos(yaw) * 9.0)
	camera.look_at(Vector3(0, 1.9, 0))

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventMouseButton:
		if event.button_index == MOUSE_BUTTON_LEFT:
			dragging = event.pressed
		if event.pressed and event.button_index == MOUSE_BUTTON_WHEEL_UP:
			camera.size = maxf(3.5, camera.size / 1.1)
		if event.pressed and event.button_index == MOUSE_BUTTON_WHEEL_DOWN:
			camera.size = minf(10.0, camera.size * 1.1)
	elif event is InputEventMouseMotion and dragging:
		yaw -= event.relative.x * 0.008
		update_camera()

func capture() -> void:
	for i in range(5):
		await get_tree().process_frame
	await RenderingServer.frame_post_draw
	var error := get_viewport().get_texture().get_image().save_png("res://docs/images/town-hall-preview.png")
	get_tree().quit(error)
