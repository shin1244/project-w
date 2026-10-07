extends Node3D

# Keep a repeatable close view of all three resource silhouettes alongside map captures.
func _ready() -> void:
	if not "--capture" in OS.get_cmdline_user_args():
		return
	for frame in 5:
		await get_tree().process_frame
	await RenderingServer.frame_post_draw
	var result := get_viewport().get_texture().get_image().save_png("res://docs/images/trees-preview.png")
	get_tree().quit(0 if result == OK else 1)
