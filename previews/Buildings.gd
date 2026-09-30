extends Control
## Side-by-side model review at the same world-to-screen scale.

func _ready() -> void:
	var background := ColorRect.new()
	background.color = Color("151f25")
	background.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	background.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(background)
	var layout := HBoxContainer.new()
	layout.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	layout.offset_top = 104
	layout.offset_bottom = -56
	layout.add_theme_constant_override("separation", 2)
	add_child(layout)
	for entry in [
		["res://previews/TownHall.tscn", "회관", "5 × 5", 10.2],
		["res://previews/Fortress.tscn", "요새", "4 × 4", 10.2],
		["res://previews/Tower.tscn", "간이 포탑", "3 × 3", 10.2]
	]:
		var panel := SubViewportContainer.new()
		panel.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		panel.stretch = true
		layout.add_child(panel)
		var viewport := SubViewport.new()
		viewport.own_world_3d = true
		viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
		viewport.msaa_3d = Viewport.MSAA_4X
		panel.add_child(viewport)
		var model: Node3D = load(entry[0]).instantiate()
		# Capture both viewports together, rather than the individual preview callbacks.
		model.set_script(null)
		viewport.add_child(model)
		var camera: Camera3D = model.get_node("Camera3D")
		camera.size = entry[3]
		camera.position = Vector3(6.5, 8.0, -10.4)
		camera.look_at(Vector3(0, 2.75, 0))
		var title := Label.new()
		title.text = entry[1] + "  /  " + entry[2]
		title.position = Vector2(28, 20)
		title.add_theme_font_size_override("font_size", 26)
		title.add_theme_color_override("font_color", Color("f0dfb5"))
		title.mouse_filter = Control.MOUSE_FILTER_IGNORE
		panel.add_child(title)
	var heading := Label.new()
	heading.text = "PROJECT W   /   BUILDINGS"
	heading.position = Vector2(34, 26)
	heading.add_theme_font_size_override("font_size", 30)
	heading.add_theme_color_override("font_color", Color("e5ece7"))
	add_child(heading)
	var caption := Label.new()
	caption.text = "금색 테두리: 건물 점유 영역 · 격자: 1 × 1"
	caption.add_theme_color_override("font_color", Color("a9bab8"))
	caption.add_theme_font_size_override("font_size", 18)
	add_child(caption)
	caption.anchor_top = 1.0
	caption.anchor_bottom = 1.0
	caption.offset_left = 34
	caption.offset_top = -40
	caption.offset_right = 900
	caption.offset_bottom = -10
	if "--capture" in OS.get_cmdline_user_args():
		for i in range(8): await get_tree().process_frame
		await RenderingServer.frame_post_draw
		var error := get_viewport().get_texture().get_image().save_png("res://docs/images/buildings-preview.png")
		get_tree().quit(error)
