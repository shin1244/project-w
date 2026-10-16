extends "res://previews/ProductionBuildings.gd"
## Model-only comparison; there is no server or simulated research in this preview.

func _ready() -> void:
	zoom = 6.3
	var background := ColorRect.new()
	background.color = Color("111d24")
	background.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	background.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(background)
	var heading := label("PROJECT W  /  2티어 건물", 30, Color("edf0e4"))
	heading.position = Vector2(38, 24)
	add_child(heading)
	var layout := HBoxContainer.new()
	layout.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	layout.offset_left = 28
	layout.offset_right = -28
	layout.offset_top = 84
	layout.offset_bottom = -28
	layout.add_theme_constant_override("separation", 22)
	add_child(layout)
	for entry in [
		["res://previews/Workshop.tscn", "공방", "회관 2티어", "4 × 4", "c7a36a"],
		["res://previews/ResearchLab.tscn", "기술연구소", "회관 2티어", "4 × 4", "9abbb7"]
	]:
		add_card(layout, entry)
	update_cameras()
	if "--capture" in OS.get_cmdline_user_args():
		capture.call_deferred()

func capture() -> void:
	for i in range(10):
		await get_tree().process_frame
	await RenderingServer.frame_post_draw
	var error := get_viewport().get_texture().get_image().save_png("res://docs/images/tier-two-buildings-preview.png")
	get_tree().quit(error)
