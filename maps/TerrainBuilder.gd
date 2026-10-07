extends RefCounted
## Geometry and the visible road mask both follow the authoritative JSON cells.

const SUBDIVISIONS := 2
const BASE_X := 65.0
const LANE_WIDTH := 12.0
const OUTER_BUILD_WIDTH := 3.0
const WALL_THICKNESS := 2
const BASE_OUTER_RADIUS := 18.0
const BASE_MEADOW_RADIUS := 18.0 # Grass kept around each town hall for the opening build.
const TOP_LANE := [
	Vector2(-65, 0), Vector2(-58, -14), Vector2(-47, -24),
	Vector2(-20, -29), Vector2(0, -30), Vector2(20, -29),
	Vector2(47, -24), Vector2(58, -14), Vector2(65, 0),
]

const TRAIL_SEGMENTS := [
	Vector4(0, 0, 15, 0), Vector4(15, 0, 28, 0),
	Vector4(28, 0, 28, 14), Vector4(28, 14, 32, 28), Vector4(0, 0, 0, 30),
]
const TRAIL_WIDTH := 6.0
const EVENT_RADIUS := 9.0
const JUNGLE_CENTER := Vector2(15, 13)
const JUNGLE_RADII := Vector2(7, 6)
const JUNGLE_ENTRANCE_WIDTH := 4.0
const MAP_ORIGIN := Vector2(-84, -64)
const MAP_SIZE := Vector2i(168, 128)
const EXPANSION_PAD_X := -34.0
# The perimeter swells gently toward each expansion instead of adding a separate bastion.
# Raised-cosine profile: zero slope where it meets the original rim; 428 extra cells per side.
const EXPANSION_SWELL_HEIGHT := 10.0
const EXPANSION_SWELL_HALF_WIDTH := 41.5
# A dome-shaped grass bay standing on the lane's road edge: its whole base opens onto
# the lane for easy access and harassment, and its top curves smoothly into the forest.
const EXPANSION_BAY_SHIFT := 0.5 # Along the lane, toward the map center.
const EXPANSION_BAY_HALF_WIDTH := 9.5
const EXPANSION_BAY_HEIGHT := 7.5
const EXPANSION_BAY_ROUNDNESS := 2.5 # 2 is a half-ellipse; larger values square the shoulders.
const EXPANSION_BAY_Z := 37.0 # Site marker inside the bay; a store fits around it.
const EXPANSION_BUILD_SIZE := 3.0

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
			assert(tile in [".", "#", "T", "R", "W"])
	arena = Node3D.new()
	arena.name = "SymmetricArena"
	arena.set_meta("dimensions", Vector2(rows[0].length(), rows.size()) * cell_size)
	arena.set_meta("origin", map_origin)
	arena.set_meta("symmetry", "180-degree rotation around the map center")
	arena.set_meta("lane_width", LANE_WIDTH)
	arena.set_meta("note", "R: walkable stone road, no construction. .: buildable grass. T: harvestable forest.")
	build_terrain()
	build_walls()
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

func activity_areas() -> Array:
	var areas: Array = [{"id": "center_event", "kind": "event", "center": [0.0, 0.0],
		"radii": [EVENT_RADIUS, EVENT_RADIUS], "entrances": [], "entranceWidth": 0.0}]
	for side in [Vector2(-1, -1), Vector2(1, -1), Vector2(-1, 1), Vector2(1, 1)]:
		var center: Vector2 = JUNGLE_CENTER * side
		var label := ("n" if side.y < 0 else "s") + ("w" if side.x < 0 else "e")
		areas.append({"id": "jungle_" + label, "kind": "jungle", "center": [center.x, center.y],
			"radii": [JUNGLE_RADII.x, JUNGLE_RADII.y],
			"entrances": [[6.0 * side.x, 5.0 * side.y], [28.0 * side.x, 13.0 * side.y]],
			"entranceWidth": JUNGLE_ENTRANCE_WIDTH})
	return areas

func area_contains(area: Dictionary, p: Vector2) -> bool:
	var center := Vector2(area["center"][0], area["center"][1])
	var radii := Vector2(area["radii"][0], area["radii"][1])
	if ((p - center) / radii).length_squared() <= 1.0:
		return true
	for entry in area.get("entrances", []):
		if segment_distance(p, center, Vector2(entry[0], entry[1])) <= float(area["entranceWidth"]) * 0.5:
			return true
	return false

func expansion_sites() -> Array:
	return [
		{"id": "team1_expansion", "center": [EXPANSION_PAD_X, -EXPANSION_BAY_Z], "buildSize": [EXPANSION_BUILD_SIZE, EXPANSION_BUILD_SIZE]},
		{"id": "team2_expansion", "center": [-EXPANSION_PAD_X, EXPANSION_BAY_Z], "buildSize": [EXPANSION_BUILD_SIZE, EXPANSION_BUILD_SIZE]},
	]

func expansion_local(p: Vector2) -> Vector2:
	# Team 1's upper site and team 2's lower site are rotational counterparts.
	var q := p if p.y < 0.0 else -p
	return Vector2(q.x - EXPANSION_PAD_X, -q.y)

func is_expansion_clearing(p: Vector2) -> bool:
	# Height is measured from the road edge, so the base follows the lane's curve.
	var height := lane_distance(p) - LANE_WIDTH * 0.5
	if not outside_lanes(p) or height < 0.0:
		return false
	var q := expansion_local(p)
	return pow(absf(q.x - EXPANSION_BAY_SHIFT) / EXPANSION_BAY_HALF_WIDTH, EXPANSION_BAY_ROUNDNESS) \
		+ pow(height / EXPANSION_BAY_HEIGHT, EXPANSION_BAY_ROUNDNESS) <= 1.0

func is_base_meadow(p: Vector2) -> bool:
	return p.abs().distance_to(Vector2(BASE_X, 0)) <= BASE_MEADOW_RADIUS

# Extra rim depth along the lane, measured from the expansion pad's X.
func expansion_swell(p: Vector2) -> float:
	var q := expansion_local(p)
	if absf(q.x) >= EXPANSION_SWELL_HALF_WIDTH:
		return 0.0
	return EXPANSION_SWELL_HEIGHT * 0.5 * (1.0 + cos(PI * q.x / EXPANSION_SWELL_HALF_WIDTH))

func outside_lanes(p: Vector2) -> bool:
	return not Geometry2D.is_point_in_polygon(Vector2(p.x, -absf(p.y)), PackedVector2Array(TOP_LANE))

func is_expansion_land(p: Vector2) -> bool:
	# Widen the lane-side rim band itself, so the wall keeps one continuous curve.
	return outside_lanes(p) and lane_distance(p) <= \
		LANE_WIDTH * 0.5 + OUTER_BUILD_WIDTH + WALL_THICKNESS + expansion_swell(p)

# Outside the lane loop everything up to the wall is forest, except the town-hall
# meadows and the expansion bays. Lane and wall cells are excluded by their tiles.
func is_outer_forest(p: Vector2) -> bool:
	return outside_lanes(p) and not is_base_meadow(p) and not is_expansion_clearing(p)

# Used only when authoring the map. Gameplay and rendering read its saved R cells.
func is_road(p: Vector2) -> bool:
	for area in activity_areas():
		if area_contains(area, p):
			return true
	return lane_distance(p) <= LANE_WIDTH * 0.5 \
		or trail_distance(p) <= TRAIL_WIDTH * 0.5 \
		or p.abs().distance_to(Vector2(BASE_X, 0)) <= 11.0 \
		or p.length() <= 5.0

func is_initial_forest(p: Vector2) -> bool:
	# Retain the forest boundary; the road mask carves clearings inside the parcels.
	# Mirroring into the upper half lets the lane polyline enclose the full interior.
	# The planting tool excludes every road/wall cell in each complete 2x2 footprint.
	return not outside_lanes(p) or is_outer_forest(p)

func is_arena_land(p: Vector2) -> bool:
	# Follow the lanes closely: retain a narrow forest strip before the wall.
	# Rounded base ends preserve the east/west extent while the north/south rim shrinks.
	return not outside_lanes(p) \
		or is_expansion_land(p) \
		or lane_distance(p) <= LANE_WIDTH * 0.5 + OUTER_BUILD_WIDTH + WALL_THICKNESS \
		or p.abs().distance_to(Vector2(BASE_X, 0)) <= BASE_OUTER_RADIUS

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
	var road_image := Image.create(rows[0].length(), rows.size(), false, Image.FORMAT_R8)
	for z in range(rows.size()):
		for x in range(rows[z].length()):
			road_image.set_pixel(x, z, Color.WHITE if rows[z][x] == "R" else Color.BLACK)
	ground_material.set_shader_parameter("road_mask", ImageTexture.create_from_image(road_image))
	ground_material.set_shader_parameter("map_origin", map_origin)
	ground_material.set_shader_parameter("map_size", Vector2(rows[0].length(), rows.size()) * cell_size)
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

func wall_face(st: SurfaceTool, a: Vector3, b: Vector3, c: Vector3, d: Vector3) -> void:
	st.set_normal((b - a).cross(c - a).normalized())
	for point in [a, c, b, a, d, c]:
		st.add_vertex(point)

func wall_box(st: SurfaceTool, lo: Vector3, size: Vector3) -> void:
	var hi := lo + size
	wall_face(st, Vector3(lo.x, hi.y, lo.z), Vector3(lo.x, hi.y, hi.z), hi, Vector3(hi.x, hi.y, lo.z))
	wall_face(st, lo, Vector3(lo.x, hi.y, lo.z), Vector3(hi.x, hi.y, lo.z), Vector3(hi.x, lo.y, lo.z))
	wall_face(st, Vector3(hi.x, lo.y, lo.z), Vector3(hi.x, hi.y, lo.z), hi, Vector3(hi.x, lo.y, hi.z))
	wall_face(st, Vector3(hi.x, lo.y, hi.z), hi, Vector3(lo.x, hi.y, hi.z), Vector3(lo.x, lo.y, hi.z))
	wall_face(st, Vector3(lo.x, lo.y, hi.z), Vector3(lo.x, hi.y, hi.z), Vector3(lo.x, hi.y, lo.z), lo)

func build_walls() -> void:
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	var count := 0
	for z in range(rows.size()):
		for x in range(rows[z].length()):
			if rows[z][x] != "W":
				continue
			var p := map_origin + Vector2(x, z) * cell_size
			wall_box(st, Vector3(p.x, 0, p.y), Vector3(cell_size, 2.4, cell_size))
			wall_box(st, Vector3(p.x, 2.4, p.y), Vector3(cell_size, 0.2, cell_size))
			var center := p + Vector2.ONE * cell_size * 0.5
			if (floori(absf(center.x)) + floori(absf(center.y))) % 3 == 0:
				wall_box(st, Vector3(p.x + cell_size * 0.08, 2.6, p.y + cell_size * 0.08), Vector3(cell_size * 0.84, 0.65, cell_size * 0.84))
			count += 1
	if count == 0:
		return
	var material := ShaderMaterial.new()
	material.shader = load("res://maps/materials/Wall.gdshader")
	st.set_material(material)
	var wall := MeshInstance3D.new()
	wall.name = "OuterWalls"
	wall.mesh = st.commit()
	attach(wall)
	var body := StaticBody3D.new()
	body.name = "WallCollision"
	attach(body)
	var shape := CollisionShape3D.new()
	shape.name = "WallShape"
	shape.shape = wall.mesh.create_trimesh_shape()
	attach(shape, body)
	print("Outer wall cells: ", count)

func build_guides() -> void:
	var guides := Node3D.new()
	guides.name = "LayoutGuides"
	attach(guides)
	# Future event/monster spawns have named anchors without adding collision props.
	for area in map_data.get("activityAreas", []):
		var marker := Marker3D.new()
		marker.name = area["id"]
		marker.position = Vector3(area["center"][0], 0, area["center"][1])
		marker.set_meta("kind", area["kind"])
		marker.set_meta("radii", Vector2(area["radii"][0], area["radii"][1]))
		attach(marker, guides)
	for site in map_data.get("expansionSites", []):
		var marker := Marker3D.new()
		marker.name = site["id"]
		marker.position = Vector3(site["center"][0], 0, site["center"][1])
		marker.set_meta("kind", "expansion")
		attach(marker, guides)
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
