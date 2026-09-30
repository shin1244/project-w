extends Control
## Reusable building assets in a server-free review scene.

var cameras: Array[Camera3D] = []
var yaw := 0.56
var dragging := false
var zoom := 7.5

func _ready() -> void:
	var background := ColorRect.new()
	background.color = Color("111d24")
	background.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	background.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(background)
	var heading := label("PROJECT W  /  생활 · 군수 시설", 30, Color("edf0e4"))
	heading.position = Vector2(38, 24)
	add_child(heading)
	var subtitle := label("합숙소 · 자원 창고 · 병영 · 화덕과 모루가 있는 대장간", 17, Color("9eaca8"))
	subtitle.position = Vector2(40, 66)
	add_child(subtitle)
	var layout := HBoxContainer.new()
	layout.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	layout.offset_left = 22
	layout.offset_right = -22
	layout.offset_top = 110
	layout.offset_bottom = -56
	layout.add_theme_constant_override("separation", 14)
	add_child(layout)
	for entry in [
		["res://previews/Store.tscn", "저장소", "목재 · 자원 창고", "3 × 3", "9eb581"],
		["res://previews/Supply.tscn", "합숙소", "병사들의 생활 공간", "3 × 3", "c5aa72"],
		["res://previews/Barracks.tscn", "병영", "1티어 유닛 훈련", "5 × 3", "7fa4b9"],
		["res://previews/Forge.tscn", "대장간", "화덕 · 모루 · 철제 장비", "5 × 3", "c9956f"]
	]:
		add_card(layout, entry)
	update_cameras()
	var caption := label("드래그로 회전 · 휠로 확대/축소    |    금색 외곽선: 점유 영역 · 격자: 1 × 1", 16, Color("9eaca8"))
	add_child(caption)
	caption.anchor_top = 1.0
	caption.anchor_bottom = 1.0
	caption.offset_left = 38
	caption.offset_top = -37
	caption.offset_right = 1500
	caption.offset_bottom = -12
	if "--capture" in OS.get_cmdline_user_args():
		capture.call_deferred()

func label(text: String, size: int, color: Color) -> Label:
	var result := Label.new()
	result.text = text
	result.add_theme_font_size_override("font_size", size)
	result.add_theme_color_override("font_color", color)
	result.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return result

func add_card(layout: HBoxContainer, entry: Array) -> void:
	var card := VBoxContainer.new()
	card.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	card.add_theme_constant_override("separation", 0)
	layout.add_child(card)
	var header := PanelContainer.new()
	header.custom_minimum_size.y = 95
	var style := StyleBoxFlat.new()
	style.bg_color = Color("1c2b32")
	style.border_color = Color(entry[4])
	style.border_width_top = 3
	style.content_margin_left = 24
	style.content_margin_right = 24
	style.content_margin_top = 12
	style.content_margin_bottom = 12
	header.add_theme_stylebox_override("panel", style)
	card.add_child(header)
	var row := HBoxContainer.new()
	header.add_child(row)
	var names := VBoxContainer.new()
	names.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	names.add_theme_constant_override("separation", 3)
	row.add_child(names)
	names.add_child(label(entry[1], 28, Color("f2e7cc")))
	names.add_child(label(entry[2], 16, Color("b8c2bd")))
	var size_label := label(entry[3], 25, Color(entry[4]))
	size_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	row.add_child(size_label)
	var panel := SubViewportContainer.new()
	panel.size_flags_vertical = Control.SIZE_EXPAND_FILL
	panel.stretch = true
	panel.mouse_filter = Control.MOUSE_FILTER_IGNORE
	card.add_child(panel)
	var viewport := SubViewport.new()
	viewport.own_world_3d = true
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	viewport.msaa_3d = Viewport.MSAA_4X
	panel.add_child(viewport)
	var scene: Node3D = load(entry[0]).instantiate()
	scene.set_script(null) # This combined preview owns camera controls and capture.
	viewport.add_child(scene)
	cameras.append(scene.get_node("Camera3D"))

func update_cameras() -> void:
	for camera in cameras:
		camera.size = zoom
		camera.position = Vector3(sin(yaw) * 12.0, 7.5, -cos(yaw) * 12.0)
		camera.look_at(Vector3(0, 1.8, 0))

func _input(event: InputEvent) -> void:
	if event is InputEventMouseButton:
		if event.button_index == MOUSE_BUTTON_LEFT:
			dragging = event.pressed
		if event.pressed and event.button_index == MOUSE_BUTTON_WHEEL_UP:
			zoom = maxf(4.8, zoom / 1.1)
			update_cameras()
		if event.pressed and event.button_index == MOUSE_BUTTON_WHEEL_DOWN:
			zoom = minf(12.0, zoom * 1.1)
			update_cameras()
	elif event is InputEventMouseMotion and dragging:
		yaw -= event.relative.x * 0.008
		update_cameras()

func capture() -> void:
	for i in range(10): await get_tree().process_frame
	await RenderingServer.frame_post_draw
	var error := get_viewport().get_texture().get_image().save_png("res://docs/images/production-buildings-preview.png")
	get_tree().quit(error)
