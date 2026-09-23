extends SceneTree
## Optional editor preview cache. The game builds directly from JSON at runtime.

func _initialize() -> void:
	var data: Dictionary = JSON.parse_string(FileAccess.get_file_as_string("res://maps/test.json"))
	var builder = load("res://maps/TerrainBuilder.gd").new()
	var arena: Node3D = builder.generate(data)
	for entry in [
		["TerrainSurface", "mesh", "TerrainSurface.res"],
		["TerrainCollision/SurfaceShape", "shape", "TerrainCollision.res"],
		["OuterCliffs", "mesh", "OuterCliffs.res"],
	]:
		var node: Node = arena.get_node(entry[0])
		var path: String = "res://maps/resources/" + entry[2]
		assert(ResourceSaver.save(node.get(entry[1]), path, ResourceSaver.FLAG_COMPRESS) == OK)
		node.set(entry[1], load(path))
	var scene := PackedScene.new()
	assert(scene.pack(arena) == OK)
	assert(ResourceSaver.save(scene, "res://maps/SymmetricArena.tscn") == OK)
	arena.free()
	quit()
