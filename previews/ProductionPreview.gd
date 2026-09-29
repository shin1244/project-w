extends "res://previews/TownHallPreview.gd"

func _ready() -> void:
	get_viewport().msaa_3d = Viewport.MSAA_4X
	super._ready()
