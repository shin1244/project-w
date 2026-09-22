extends SceneTree

func _initialize() -> void:
	call_deferred("check")

func check() -> void:
	var arena: Node3D = load("res://maps/SymmetricArena.tscn").instantiate()
	root.add_child(arena)
	await physics_frame
	await physics_frame
	var mesh: ArrayMesh = arena.get_node("TerrainSurface").mesh
	var vertices: PackedVector3Array = mesh.surface_get_arrays(0)[Mesh.ARRAY_VERTEX]
	var positions := {}
	for v in vertices:
		positions[Vector3i(roundi(v.x * 2), roundi(v.y * 1000), roundi(v.z * 2))] = true
	for v: Vector3i in positions:
		assert(positions.has(Vector3i(-v.x, v.y, v.z)), "X reflection missing")
		assert(positions.has(Vector3i(v.x, v.y, -v.z)), "Z reflection missing")
	for i in range(0, vertices.size(), 3):
		var normal := (vertices[i + 2] - vertices[i]).cross(vertices[i + 1] - vertices[i])
		assert(normal.y > 0.0, "Terrain face winding must point up")
	var space := arena.get_world_3d().direct_space_state
	for path_name in ["TopLane", "BottomLane"]:
		var path: Path3D = arena.get_node("LayoutGuides/" + path_name)
		for i in range(201):
			var point := path.curve.sample_baked(path.curve.get_baked_length() * i / 200.0)
			var query := PhysicsRayQueryParameters3D.create(point + Vector3.UP * 10, point + Vector3.DOWN * 10)
			var hit := space.intersect_ray(query)
			assert(not hit.is_empty(), "Lane collision missing")
			assert(absf(hit.position.y) < 0.01, "Lane should remain flat")
			assert(hit.normal.y > 0.99, "Lane collision should face up")
	print("PASS: baked vertex symmetry, upward faces, 402 lane collision samples")
	quit()
