extends SceneTree

func _initialize() -> void:
	call_deferred("check")

func check() -> void:
	var source: Dictionary = JSON.parse_string(FileAccess.get_file_as_string("res://maps/test.json"))
	var builder = load("res://maps/TerrainBuilder.gd").new()
	var arena: Node3D = builder.generate(source)
	root.add_child(arena)
	await physics_frame
	await physics_frame
	var mesh: ArrayMesh = arena.get_node("TerrainSurface").mesh
	var vertices: PackedVector3Array = mesh.surface_get_arrays(0)[Mesh.ARRAY_VERTEX]
	var positions := {}
	for v in vertices:
		assert(is_zero_approx(v.y), "All terrain must be flat at Y=0")
		positions[Vector3i(roundi(v.x * 2), roundi(v.y * 1000), roundi(v.z * 2))] = true
	for v: Vector3i in positions:
		assert(positions.has(Vector3i(-v.x, v.y, v.z)), "X reflection missing")
		assert(positions.has(Vector3i(v.x, v.y, -v.z)), "Z reflection missing")
	for i in range(0, vertices.size(), 3):
		var normal := (vertices[i + 2] - vertices[i]).cross(vertices[i + 1] - vertices[i])
		assert(normal.y > 0.0, "Terrain face winding must point up")
	var space := arena.get_world_3d().direct_space_state
	var data: Dictionary = JSON.parse_string(FileAccess.get_file_as_string("res://maps/test.json"))
	var rows: Array = data["rows"]
	var cell_size: float = data["cellSize"]
	var land_count := 0
	for z in range(rows.size()):
		for x in range(rows[z].length()):
			var is_land: bool = rows[z][x] != "#"
			assert(is_land == (rows[z][rows[z].length() - 1 - x] != "#"), "Grid X symmetry missing")
			assert(is_land == (rows[rows.size() - 1 - z][x] != "#"), "Grid Z symmetry missing")
			if is_land:
				land_count += 1
			# Four samples per 1x1 cell verify both ground and absence of ground outside.
			for offset in [Vector2(0.25, 0.25), Vector2(0.75, 0.25), Vector2(0.25, 0.75), Vector2(0.75, 0.75)]:
				var point := Vector3(data["originX"] + (x + offset.x) * cell_size, 0, data["originZ"] + (z + offset.y) * cell_size)
				var query := PhysicsRayQueryParameters3D.create(point + Vector3.UP * 10, point + Vector3.DOWN * 10)
				var hit := space.intersect_ray(query)
				assert(not hit.is_empty() == is_land, "Terrain/grid mismatch at cell %d,%d" % [x, z])
				if is_land:
					assert(absf(hit.position.y) < 0.01 and hit.normal.y > 0.99, "Grid ground must be flat")
	assert(vertices.size() == land_count * 8 * 3, "Unexpected terrain outside map cells")
	for path_name in ["TopLane", "BottomLane"]:
		var path: Path3D = arena.get_node("LayoutGuides/" + path_name)
		for i in range(201):
			var point := path.curve.sample_baked(path.curve.get_baked_length() * i / 200.0)
			var query := PhysicsRayQueryParameters3D.create(point + Vector3.UP * 10, point + Vector3.DOWN * 10)
			var hit := space.intersect_ray(query)
			assert(not hit.is_empty(), "Lane collision missing")
			assert(absf(hit.position.y) < 0.01, "Lane should remain flat")
			assert(hit.normal.y > 0.99, "Lane collision should face up")
	print("PASS: flat terrain, symmetric grid, ", rows.size() * rows[0].length() * 4, " grid collision samples, 402 lane samples")
	quit()
