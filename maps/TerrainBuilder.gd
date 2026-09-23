extends RefCounted
## Flat geometry follows maps/test.json; road masks use the same layout as the forest tool.

const SUBDIVISIONS := 2
const BASE_X := 65.0
const LANE_WIDTH := 8.0
const TOP_LANE := [
	Vector2(-65, 0), Vector2(-58, -14), Vector2(-44, -25),
	Vector2(-24, -29), Vector2(0, -30), Vector2(24, -29),
	Vector2(44, -25), Vector2(58, -14), Vector2(65, 0),
]

const TRAIL_SEGMENTS := [
	Vector4(0, 0, 13, 2), Vector4(13, 2, 27, 0),
	Vector4(27, 0, 25, 13), Vector4(25, 13, 31, 28),
	Vector4(27, 0, 39, 6), Vector4(39, 6, 55, 4), Vector4(0, 7, 0, 30),
]

var arena: Node3D
var map_data: Dictionary
var rows: Array
var map_origin: Vector2
var cell_size: float
var mesh_step: float

func generate(data: Dictionary) -> Node3D:
	map_data = data
	rows = map_data["rows"]
	map_origin = Vector2(map_data["originX"], map_data["originZ"])
	cell_size = float(map_data["cellSize"])
	mesh_step = cell_size / SUBDIVISIONS
	assert(not rows.is_empty() and not rows[0].is_empty() and cell_size > 0.0)
	for row: String in rows:
		assert(row.length() == rows[0].length())
		for tile in row:
			assert(tile in [".", "#", "T"])
	arena = Node3D.new()
	arena.name = "SymmetricArena"
	arena.set_meta("dimensions", Vector2(rows[0].length(), rows.size()) * cell_size)
	arena.set_meta("symmetry", "Reflection across X=0 and Z=0")
	arena.set_meta("lane_width", LANE_WIDTH)
	arena.set_meta("note", "Flat terrain from maps/test.json. T is ground occupied by a server resource.")
	build_terrain()
	build_guides()
	return arena

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
	var distance := INF
	for edge: Vector4 in TRAIL_SEGMENTS:
		distance = minf(distance, segment_distance(q, Vector2(edge.x, edge.y), Vector2(edge.z, edge.w)))
	return distance

# The same cells used by the Go server define the visible surface.
# T still has ground underneath; resource blocking is handled by the server.
func inside(p: Vector2) -> bool:
	var cell := Vector2i(floori((p.x - map_origin.x) / cell_size), floori((p.y - map_origin.y) / cell_size))
	return cell.y >= 0 and cell.y < rows.size() and cell.x >= 0 and cell.x < rows[0].length() and rows[cell.y][cell.x] != "#"

func vertex(x: float, z: float) -> Vector3:
	return Vector3(x, 0.0, z)

func triangle(st: SurfaceTool, a: Vector3, b: Vector3, c: Vector3, cliff := false) -> void:
	var normal := (b - a).cross(c - a).normalized()
	var center := (a + b + c) / 3.0
	var color := Color.WHITE
	if cliff:
		color = Color(0.25, 0.26, 0.24).lerp(Color(0.36, 0.34, 0.29), clampf((center.y + 4.5) / 8.0, 0, 1))
	# Godot front faces use clockwise winding; the geometric normal above
	# points outward, so submit the last two vertices in reverse order.
	for point in [a, c, b]:
		if cliff:
			st.set_normal(normal)
			st.set_color(color)
		else:
			st.set_normal(Vector3.UP)
			st.set_color(Color.WHITE)
		st.add_vertex(point)

func build_terrain() -> void:
	var top := SurfaceTool.new()
	var sides := SurfaceTool.new()
	top.begin(Mesh.PRIMITIVE_TRIANGLES)
	sides.begin(Mesh.PRIMITIVE_TRIANGLES)
	var count := 0
	for x in range(rows[0].length() * SUBDIVISIONS):
		for z in range(rows.size() * SUBDIVISIONS):
			var corner := map_origin + Vector2(x, z) * mesh_step
			var center := corner + Vector2.ONE * mesh_step * 0.5
			if not inside(center):
				continue
			var a := vertex(corner.x, corner.y)
			var b := vertex(corner.x + mesh_step, corner.y)
			var c := vertex(corner.x + mesh_step, corner.y + mesh_step)
			var d := vertex(corner.x, corner.y + mesh_step)
			# Mirror the diagonal too, so the collision mesh is exactly symmetric.
			if (center.x < 0) == (center.y < 0):
				triangle(top, a, d, c)
				triangle(top, a, c, b)
			else:
				triangle(top, a, d, b)
				triangle(top, b, d, c)
			count += 2
			var edges := [[a, b, Vector2(0, -1)], [b, c, Vector2(1, 0)], [c, d, Vector2(0, 1)], [d, a, Vector2(-1, 0)]]
			for edge in edges:
				if inside(center + edge[2] * mesh_step):
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
	var ground_material := ShaderMaterial.new()
	ground_material.shader = load("res://maps/materials/Ground.gdshader")
	ground_material.set_shader_parameter("top_lane", PackedVector2Array(TOP_LANE))
	ground_material.set_shader_parameter("trails", TRAIL_SEGMENTS)
	ground_material.set_shader_parameter("lane_width", LANE_WIDTH)
	ground_material.set_shader_parameter("base_x", BASE_X)
	top.set_material(ground_material)
	sides.set_material(material)
	var ground := MeshInstance3D.new()
	ground.name = "TerrainSurface"
	var terrain_mesh := top.commit()
	ground.mesh = terrain_mesh
	attach(ground)
	var body := StaticBody3D.new()
	body.name = "TerrainCollision"
	attach(body)
	var shape := CollisionShape3D.new()
	shape.name = "SurfaceShape"
	var collision := ground.mesh.create_trimesh_shape()
	shape.shape = collision
	attach(shape, body)
	var edge_mesh := MeshInstance3D.new()
	edge_mesh.name = "OuterCliffs"
	edge_mesh.mesh = sides.commit()
	attach(edge_mesh)
	print("Terrain triangles: ", count)
	print("PASS: flat terrain generated from ", rows[0].length(), " x ", rows.size(), " map cells")

func build_guides() -> void:
	var guides := Node3D.new()
	guides.name = "LayoutGuides"
	attach(guides)
	for entry in [["BlueSpawn", Vector3(-BASE_X, 0, 0)], ["RedSpawn", Vector3(BASE_X, 0, 0)], ["Center", Vector3.ZERO], ["BlueResourceArea", Vector3(-39, 0, 13)], ["RedResourceArea", Vector3(39, 0, 13)]]:
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
