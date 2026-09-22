extends SceneTree
## Rebuild with: Godot --headless --path . --script tools/build_arena.gd
## Geometry and terrain colors use abs(x), abs(z): both axes are exact mirrors.

const OUTPUT := "res://maps/SymmetricArena.tscn"
const CELL := 0.5
const HALF_X := 168
const HALF_Z := 92
const BASE_X := 65.0
const LANE_WIDTH := 8.0
const TOP_LANE := [
	Vector2(-65, 0), Vector2(-58, -14), Vector2(-44, -25),
	Vector2(-24, -29), Vector2(0, -30), Vector2(24, -29),
	Vector2(44, -25), Vector2(58, -14), Vector2(65, 0),
]

var arena: Node3D

func _initialize() -> void:
	arena = Node3D.new()
	arena.name = "SymmetricArena"
	arena.set_meta("dimensions", Vector2(168, 92))
	arena.set_meta("symmetry", "Reflection across X=0 and Z=0")
	arena.set_meta("lane_width", LANE_WIDTH)
	arena.set_meta("note", "Terrain blockout. Paths are guides, not movement/navigation logic.")
	build_terrain()
	build_guides()
	var scene := PackedScene.new()
	assert(scene.pack(arena) == OK)
	assert(ResourceSaver.save(scene, OUTPUT) == OK)
	print("Arena saved: ", OUTPUT)
	arena.free()
	quit()

func attach(node: Node, parent: Node = null) -> void:
	if parent == null:
		parent = arena
	parent.add_child(node)
	node.owner = arena

func lane_distance(p: Vector2) -> float:
	var q := Vector2(p.x, -absf(p.y))
	var distance := INF
	for i in range(TOP_LANE.size() - 1):
		distance = minf(distance, q.distance_to(Geometry2D.get_closest_point_to_segment(q, TOP_LANE[i], TOP_LANE[i + 1])))
	return distance

func segment_distance(p: Vector2, a: Vector2, b: Vector2) -> float:
	return p.distance_to(Geometry2D.get_closest_point_to_segment(p, a, b))

func trail_distance(p: Vector2) -> float:
	var q := p.abs()
	return minf(
		minf(minf(segment_distance(q, Vector2(0, 0), Vector2(13, 2)),
		segment_distance(q, Vector2(13, 2), Vector2(27, 0))),
		minf(segment_distance(q, Vector2(27, 0), Vector2(25, 13)),
		segment_distance(q, Vector2(25, 13), Vector2(31, 28)))),
		minf(minf(segment_distance(q, Vector2(27, 0), Vector2(39, 6)),
		segment_distance(q, Vector2(39, 6), Vector2(55, 4))),
		segment_distance(q, Vector2(0, 7), Vector2(0, 30))))

func boundary(p: Vector2) -> float:
	var q := p.abs()
	var angle := atan2(q.y / 44.0, q.x / 82.0)
	var irregularity := 1.0 + 0.017 * cos(angle * 12.0) + 0.008 * cos(angle * 26.0)
	return (pow(q.x / 82.0, 2.6) + pow(q.y / 44.0, 2.6)) / irregularity

func inside(p: Vector2) -> bool:
	return boundary(p) < 1.0

func mound(q: Vector2, center: Vector2, radius: Vector2, peak: float) -> float:
	var d := ((q - center) / radius).length()
	var wobble := 0.05 * sin(q.x * 0.6) * cos(q.y * 0.7)
	return peak * (1.0 - smoothstep(0.68, 1.05, d + wobble))

func height_at(p: Vector2) -> float:
	var q := p.abs()
	var hill := maxf(mound(q, Vector2(14, 15), Vector2(9, 7), 3.4),
		mound(q, Vector2(39, 13), Vector2(8, 6), 2.7))
	hill = maxf(hill, mound(q, Vector2(48, 0), Vector2(5, 5), 2.1))
	var rim := smoothstep(0.84, 1.02, boundary(p)) * (3.6 + 0.8 * sin(q.x * 0.38) * cos(q.y * 0.41))
	var base_distance := q.distance_to(Vector2(BASE_X, 0))
	var clear_lane := smoothstep(LANE_WIDTH * 0.5 + 0.8, LANE_WIDTH * 0.5 + 2.6, lane_distance(p))
	var clear_base := smoothstep(11.5, 14.0, base_distance)
	var clear_trail := smoothstep(2.0, 3.5, trail_distance(p))
	return maxf(hill, rim) * clear_lane * clear_base * clear_trail

func ground_color(p: Vector2, height: float, slope: float) -> Color:
	var q := p.abs()
	var variation := (sin(q.x * 0.72) * cos(q.y * 0.63) + sin(q.x * 0.21 + q.y * 0.3)) * 0.017
	var grass := Color(0.28, 0.34, 0.24)
	var earth := Color(0.37, 0.33, 0.25)
	var color := grass.lerp(earth, 0.45 + 0.3 * sin(q.x * 0.19) * cos(q.y * 0.23))
	if height > 0.3:
		color = Color(0.32, 0.38, 0.26)
	if slope > 0.35:
		color = Color(0.32, 0.31, 0.28).lerp(Color(0.43, 0.40, 0.33), clampf(height / 4.0, 0, 1))
	var lane := lane_distance(p)
	if lane < LANE_WIDTH * 0.5 + 1.2:
		var road := Color(0.49, 0.46, 0.38)
		color = road.lerp(color, smoothstep(LANE_WIDTH * 0.5 - 0.6, LANE_WIDTH * 0.5 + 1.2, lane))
	elif trail_distance(p) < 2.7 and height < 0.2:
		color = Color(0.43, 0.39, 0.29).lerp(color, smoothstep(1.4, 2.7, trail_distance(p)))
	var base_d := q.distance_to(Vector2(BASE_X, 0))
	if base_d < 11.0:
		color = Color(0.45, 0.45, 0.39)
		if base_d > 9.7:
			color = Color(0.55, 0.52, 0.43)
	if q.length() < 7.6:
		color = Color(0.47, 0.44, 0.34)
		if q.length() > 6.8:
			color = Color(0.55, 0.50, 0.38)
	return Color(color.r + variation, color.g + variation, color.b + variation)

func vertex(x: float, z: float) -> Vector3:
	return Vector3(x, height_at(Vector2(x, z)), z)

func triangle(st: SurfaceTool, a: Vector3, b: Vector3, c: Vector3, cliff := false) -> void:
	var normal := (b - a).cross(c - a).normalized()
	var center := (a + b + c) / 3.0
	var color := ground_color(Vector2(center.x, center.z), center.y, 1.0 - absf(normal.y))
	if cliff:
		color = Color(0.25, 0.26, 0.24).lerp(Color(0.36, 0.34, 0.29), clampf((center.y + 4.5) / 8.0, 0, 1))
	# Godot front faces use clockwise winding; the geometric normal above
	# points outward, so submit the last two vertices in reverse order.
	for point in [a, c, b]:
		if cliff:
			st.set_normal(normal)
			st.set_color(color)
		else:
			var p := Vector2(point.x, point.z)
			var dx := height_at(p + Vector2(0.25, 0)) - height_at(p - Vector2(0.25, 0))
			var dz := height_at(p + Vector2(0, 0.25)) - height_at(p - Vector2(0, 0.25))
			var smooth_normal := Vector3(-dx, 0.5, -dz).normalized()
			st.set_normal(smooth_normal)
			st.set_color(ground_color(p, point.y, 1.0 - smooth_normal.y))
		st.add_vertex(point)

func build_terrain() -> void:
	var top := SurfaceTool.new()
	var sides := SurfaceTool.new()
	top.begin(Mesh.PRIMITIVE_TRIANGLES)
	sides.begin(Mesh.PRIMITIVE_TRIANGLES)
	var count := 0
	for x in range(-HALF_X, HALF_X):
		for z in range(-HALF_Z, HALF_Z):
			var center := Vector2(x + 0.5, z + 0.5) * CELL
			if not inside(center):
				continue
			var a := vertex(x * CELL, z * CELL)
			var b := vertex((x + 1) * CELL, z * CELL)
			var c := vertex((x + 1) * CELL, (z + 1) * CELL)
			var d := vertex(x * CELL, (z + 1) * CELL)
			# Mirror the diagonal too, so the collision mesh is exactly symmetric.
			if (x < 0) == (z < 0):
				triangle(top, a, d, c)
				triangle(top, a, c, b)
			else:
				triangle(top, a, d, b)
				triangle(top, b, d, c)
			count += 2
			var edges := [[a, b, Vector2(0, -1)], [b, c, Vector2(1, 0)], [c, d, Vector2(0, 1)], [d, a, Vector2(-1, 0)]]
			for edge in edges:
				if inside(center + edge[2] * CELL):
					continue
				var low_a := Vector3(edge[0].x, -4.5, edge[0].z)
				var low_b := Vector3(edge[1].x, -4.5, edge[1].z)
				triangle(sides, edge[0], edge[1], low_b, true)
				triangle(sides, edge[0], low_b, low_a, true)
	var material := StandardMaterial3D.new()
	material.vertex_color_use_as_albedo = true
	material.vertex_color_is_srgb = true
	material.roughness = 0.95
	material.cull_mode = BaseMaterial3D.CULL_DISABLED
	top.set_material(material)
	sides.set_material(material)
	var ground := MeshInstance3D.new()
	ground.name = "TerrainSurface"
	var terrain_mesh := top.commit()
	assert(ResourceSaver.save(terrain_mesh, "res://maps/resources/TerrainSurface.res", ResourceSaver.FLAG_COMPRESS) == OK)
	ground.mesh = load("res://maps/resources/TerrainSurface.res")
	attach(ground)
	var body := StaticBody3D.new()
	body.name = "TerrainCollision"
	attach(body)
	var shape := CollisionShape3D.new()
	shape.name = "SurfaceShape"
	var collision := ground.mesh.create_trimesh_shape()
	assert(ResourceSaver.save(collision, "res://maps/resources/TerrainCollision.res", ResourceSaver.FLAG_COMPRESS) == OK)
	shape.shape = load("res://maps/resources/TerrainCollision.res")
	attach(shape, body)
	var edge_mesh := MeshInstance3D.new()
	edge_mesh.name = "OuterCliffs"
	assert(ResourceSaver.save(sides.commit(), "res://maps/resources/OuterCliffs.res", ResourceSaver.FLAG_COMPRESS) == OK)
	edge_mesh.mesh = load("res://maps/resources/OuterCliffs.res")
	attach(edge_mesh)
	print("Terrain triangles: ", count)
	# Verify samples and generated topology assumptions before saving the scene.
	for x in range(HALF_X):
		for z in range(HALF_Z):
			var p := Vector2(x + 0.5, z + 0.5) * CELL
			assert(is_equal_approx(height_at(p), height_at(Vector2(-p.x, p.y))))
			assert(is_equal_approx(height_at(p), height_at(Vector2(p.x, -p.y))))
			assert(inside(p) == inside(Vector2(-p.x, p.y)))
	print("PASS: X/Z symmetry for heights and map bounds")

func build_guides() -> void:
	var guides := Node3D.new()
	guides.name = "LayoutGuides"
	attach(guides)
	for entry in [["BlueSpawn", Vector3(-BASE_X, 0, 0)], ["RedSpawn", Vector3(BASE_X, 0, 0)], ["Center", Vector3.ZERO], ["BlueResourceArea", Vector3(-39, 2.7, 13)], ["RedResourceArea", Vector3(39, 2.7, 13)]]:
		var marker := Marker3D.new()
		marker.name = entry[0]
		marker.position = entry[1]
		attach(marker, guides)
	for entry in [["TopLane", 1.0], ["BottomLane", -1.0]]:
		var path := Path3D.new()
		path.name = entry[0]
		path.curve = Curve3D.new()
		for point in TOP_LANE:
			path.curve.add_point(Vector3(point.x, 0.06, point.y * entry[1]))
		attach(path, guides)
	var length_top: float = guides.get_node("TopLane").curve.get_baked_length()
	var length_bottom: float = guides.get_node("BottomLane").curve.get_baked_length()
	assert(is_equal_approx(length_top, length_bottom))
	print("PASS: equal lane lengths: ", length_top)
