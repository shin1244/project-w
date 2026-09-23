extends SceneTree
## Creates the reusable 3x3 town hall. All dimensions are in map units.

const OUTPUT := "res://buildings/TownHall.tscn"
var surfaces: Dictionary = {}
var materials: Dictionary = {}

func _initialize() -> void:
	material("stone", Color("778176"))
	material("stone_light", Color("91998c"))
	material("stone_dark", Color("59665e"))
	material("plaster", Color("d5c4a0"))
	material("wood", Color("583d2d"))
	material("wood_light", Color("8c6340"))
	material("dark", Color("25352f"))
	material("roof", Color("274e66"))
	material("roof_light", Color("315971"))
	material("roof_mid", Color("2d546c"))
	material("gold", Color("c49a49"), 0.35)
	material("glass", Color("e7bd70"))
	material("banner", Color("376a94"))
	build_hall()
	save_hall()
	quit()

func material(key: String, color: Color, metallic := 0.0) -> void:
	var mat := StandardMaterial3D.new()
	mat.resource_name = key
	mat.albedo_color = color
	mat.metallic = metallic
	mat.roughness = 0.82 if metallic == 0.0 else 0.48
	# All surfaces are opaque. The flag is a thin, double-sided mesh.
	if key == "banner":
		mat.cull_mode = BaseMaterial3D.CULL_DISABLED
	materials[key] = mat
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	st.set_material(mat)
	surfaces[key] = st

func box(key: String, center: Vector3, size: Vector3, basis := Basis.IDENTITY) -> void:
	var mesh := BoxMesh.new()
	mesh.size = size
	append_mesh(key, mesh, Transform3D(basis, center))

func append_mesh(key: String, mesh: Mesh, transform: Transform3D) -> void:
	# Custom faces and primitives must both be unindexed before joining surfaces.
	var part := SurfaceTool.new()
	part.create_from(mesh, 0)
	part.deindex()
	surfaces[key].append_from(part.commit(), 0, transform)

func cylinder(key: String, center: Vector3, top: float, bottom: float, height: float, basis := Basis.IDENTITY) -> void:
	var mesh := CylinderMesh.new()
	mesh.top_radius = top
	mesh.bottom_radius = bottom
	mesh.height = height
	mesh.radial_segments = 12
	append_mesh(key, mesh, Transform3D(basis, center))

func sphere(key: String, center: Vector3, radius: float) -> void:
	var mesh := SphereMesh.new()
	mesh.radius = radius
	mesh.height = radius * 2.0
	mesh.radial_segments = 10
	mesh.rings = 5
	append_mesh(key, mesh, Transform3D(Basis.IDENTITY, center))

func triangle(key: String, a: Vector3, b: Vector3, c: Vector3) -> void:
	var normal := (b - a).cross(c - a).normalized()
	for point in [a, c, b]: # Godot front faces are clockwise.
		surfaces[key].set_normal(normal)
		surfaces[key].set_uv(Vector2.ZERO)
		surfaces[key].add_vertex(point)

func quad(key: String, a: Vector3, b: Vector3, c: Vector3, d: Vector3) -> void:
	triangle(key, a, b, c)
	triangle(key, a, c, d)

func beam(key: String, a: Vector3, b: Vector3, width: float, depth: float) -> void:
	box(key, (a + b) * 0.5, Vector3(width, a.distance_to(b), depth), Basis(Quaternion(Vector3.UP, (b - a).normalized())))

func plinth() -> void:
	# Chamfered corners make the exact 3x3 footprint read as a masonry foundation.
	var outline := [Vector2(-1.38, -1.5), Vector2(1.38, -1.5), Vector2(1.5, -1.38), Vector2(1.5, 1.38),
		Vector2(1.38, 1.5), Vector2(-1.38, 1.5), Vector2(-1.5, 1.38), Vector2(-1.5, -1.38)]
	for i in range(outline.size()):
		var a: Vector2 = outline[i]
		var b: Vector2 = outline[(i + 1) % outline.size()]
		triangle("stone_light", Vector3(0, 0.18, 0), Vector3(b.x, 0.18, b.y), Vector3(a.x, 0.18, a.y))
		quad("stone_dark", Vector3(a.x, 0, a.y), Vector3(a.x, 0.18, a.y), Vector3(b.x, 0.18, b.y), Vector3(b.x, 0, b.y))
	box("stone", Vector3(0, 0.34, 0.07), Vector3(2.48, 0.32, 2.25))
	box("stone_light", Vector3(0, 0.51, 0.07), Vector3(2.53, 0.10, 2.30))
	for side in [-1.0, 1.0]:
		for i in range(6):
			box("stone_light" if i % 2 == 0 else "stone", Vector3(side * 1.245, 0.345, -0.85 + i * 0.365), Vector3(0.035, 0.235, 0.342))
		for i in range(7):
			box("stone_light" if i % 2 == 0 else "stone", Vector3(-1.035 + i * 0.345, 0.345, 0.07 + side * 1.13), Vector3(0.325, 0.235, 0.035))
	box("stone_light", Vector3(0, 0.24, -1.34), Vector3(1.62, 0.12, 0.29))
	box("stone", Vector3(0, 0.36, -1.18), Vector3(1.38, 0.12, 0.28))

func roof(half_width: float, front: float, back: float, eave: float, ridge: float, rows: int, columns: int) -> void:
	# A solid dark shell beneath overlapping blue slate courses.
	for side in [-1.0, 1.0]:
		var x0 := minf(0.0, side * half_width)
		var x1 := maxf(0.0, side * half_width)
		quad("roof", Vector3(x0, roof_y(x0, half_width, eave, ridge), front),
			Vector3(x0, roof_y(x0, half_width, eave, ridge), back),
			Vector3(x1, roof_y(x1, half_width, eave, ridge), back),
			Vector3(x1, roof_y(x1, half_width, eave, ridge), front))
		for row in range(rows):
			for col in range(columns):
				var lo := half_width * row / rows + 0.012
				var hi := half_width * (row + 1) / rows - 0.008
				var xa := minf(side * lo, side * hi)
				var xb := maxf(side * lo, side * hi)
				var z0 := lerpf(front, back, float(col) / columns) + 0.009
				var z1 := lerpf(front, back, float(col + 1) / columns) - 0.009
				var y0 := roof_y(xa, half_width, eave, ridge) + 0.024
				var y1 := roof_y(xb, half_width, eave, ridge) + 0.024
				var color: String = ["roof", "roof_mid", "roof_light"][(row * 7 + col * 11) % 3]
				quad(color, Vector3(xa, y0, z0), Vector3(xa, y0, z1), Vector3(xb, y1, z1), Vector3(xb, y1, z0))
		beam("wood", Vector3(side * half_width, eave - 0.025, front), Vector3(side * half_width, eave - 0.025, back), 0.085, 0.085)
	for z in [front, back]:
		beam("wood", Vector3(-half_width, eave, z), Vector3(0, ridge, z), 0.08, 0.08)
		beam("wood", Vector3(0, ridge, z), Vector3(half_width, eave, z), 0.08, 0.08)
	box("gold", Vector3(0, ridge + 0.03, (front + back) * 0.5), Vector3(0.10, 0.075, back - front + 0.04))

func roof_y(x: float, half_width: float, eave: float, ridge: float) -> float:
	return lerpf(ridge, eave, absf(x) / half_width)

func window(side: float, z: float) -> void:
	box("wood", Vector3(side * 1.17, 1.22, z), Vector3(0.10, 0.64, 0.47))
	box("glass", Vector3(side * 1.227, 1.23, z), Vector3(0.025, 0.49, 0.34))
	box("wood_light", Vector3(side * 1.25, 1.23, z), Vector3(0.025, 0.50, 0.032))
	box("wood_light", Vector3(side * 1.25, 1.23, z), Vector3(0.025, 0.038, 0.36))
	box("stone_light", Vector3(side * 1.23, 0.87, z), Vector3(0.22, 0.085, 0.55))

func build_hall() -> void:
	plinth()
	box("plaster", Vector3(0, 1.175, 0.06), Vector3(2.30, 1.25, 2.04))
	for z in [-0.96, 1.08]:
		var a := Vector3(-1.15, 1.80, z)
		var b := Vector3(1.15, 1.80, z)
		var c := Vector3(0, 2.70, z)
		if z < 0: triangle("plaster", a, c, b)
		else: triangle("plaster", a, b, c)
		box("wood", Vector3(0, 1.79, z), Vector3(2.38, 0.11, 0.11))
		beam("wood", Vector3(-1.13, 1.81, z), Vector3(0, 2.70, z), 0.10, 0.12)
		beam("wood", Vector3(0, 2.70, z), Vector3(1.13, 1.81, z), 0.10, 0.12)
		beam("wood", Vector3(0, 1.78, z), Vector3(0, 2.70, z), 0.10, 0.12)
	for x in [-1.13, 1.13]:
		for z in [-0.95, 1.07]:
			box("wood", Vector3(x, 1.18, z), Vector3(0.15, 1.30, 0.15))
		box("wood", Vector3(x, 1.79, 0.06), Vector3(0.12, 0.12, 2.16))
		box("wood", Vector3(x, 0.61, 0.06), Vector3(0.13, 0.12, 2.16))
		box("wood", Vector3(x, 1.20, 0.06), Vector3(0.13, 1.20, 0.13))
		window(signf(x), -0.46)
		window(signf(x), 0.58)
		for z in [-0.95, 1.07]:
			box("gold", Vector3(x, 0.71, z), Vector3(0.165, 0.055, 0.165))
	roof(1.39, -1.22, 1.31, 1.91, 2.90, 6, 10)
	entrance()
	bell_tower()

func entrance() -> void:
	box("dark", Vector3(0, 0.96, -1.012), Vector3(0.87, 1.02, 0.075))
	for i in range(7):
		var x := (i - 3) * 0.102
		var top := 1.13 + sqrt(maxf(0, 0.36 * 0.36 - x * x))
		box("wood_light" if i % 2 == 0 else "wood", Vector3(x, (0.44 + top) * 0.5, -1.058), Vector3(0.094, top - 0.44, 0.05))
	for x in [-0.435, 0.435]:
		box("wood", Vector3(x, 0.79, -1.07), Vector3(0.12, 0.72, 0.14))
	for i in range(9):
		var angle := PI * (i + 0.5) / 9.0
		box("wood_light", Vector3(cos(angle) * 0.435, 1.13 + sin(angle) * 0.435, -1.07),
			Vector3(0.165, 0.12, 0.14), Basis(Vector3.FORWARD, -angle - PI * 0.5))
	for y in [0.66, 1.04]:
		box("dark", Vector3(0, y, -1.097), Vector3(0.64, 0.045, 0.025))
	for x in [-0.085, 0.085]:
		sphere("gold", Vector3(x, 0.86, -1.13), 0.032)
	# A small porch keeps the entrance legible under the main roof.
	for x in [-0.57, 0.57]:
		box("wood", Vector3(x, 0.98, -1.32), Vector3(0.085, 1.1, 0.085))
		box("stone_light", Vector3(x, 0.46, -1.32), Vector3(0.17, 0.14, 0.17))
	roof(0.66, -1.43, -0.91, 1.58, 1.99, 3, 3)
	# Round civic seal above the porch, facing -Z.
	cylinder("gold", Vector3(0, 2.31, -1.04), 0.17, 0.17, 0.065, Basis(Vector3.RIGHT, PI * 0.5))
	cylinder("banner", Vector3(0, 2.31, -1.08), 0.12, 0.12, 0.028, Basis(Vector3.RIGHT, PI * 0.5))
	box("gold", Vector3(0, 2.265, -1.103), Vector3(0.15, 0.03, 0.02))
	for x in [-0.05, 0.0, 0.05]:
		box("gold", Vector3(x, 2.31, -1.103), Vector3(0.027, 0.115 if x == 0 else 0.08, 0.02))

func bell_tower() -> void:
	var z := 0.40
	box("wood", Vector3(0, 2.925, z), Vector3(0.77, 0.13, 0.77))
	box("gold", Vector3(0, 3.005, z), Vector3(0.79, 0.035, 0.79))
	for x in [-0.28, 0.28]:
		for dz in [-0.28, 0.28]:
			box("wood_light", Vector3(x, 3.25, z + dz), Vector3(0.095, 0.48, 0.095))
	box("wood", Vector3(0, 3.49, z), Vector3(0.76, 0.105, 0.76))
	box("dark", Vector3(0, 3.34, z), Vector3(0.035, 0.23, 0.035))
	cylinder("gold", Vector3(0, 3.24, z), 0.09, 0.19, 0.24)
	cylinder("gold", Vector3(0, 3.105, z), 0.21, 0.21, 0.045)
	sphere("dark", Vector3(0, 3.075, z), 0.05)
	var peak := Vector3(0, 3.92, z)
	var corners := [Vector3(-0.46, 3.52, z - 0.46), Vector3(0.46, 3.52, z - 0.46),
		Vector3(0.46, 3.52, z + 0.46), Vector3(-0.46, 3.52, z + 0.46)]
	for i in range(4):
		triangle("roof_light" if i % 2 == 0 else "roof_mid", corners[(i + 1) % 4], corners[i], peak)
		beam("gold", corners[i], peak, 0.03, 0.03)
	cylinder("gold", Vector3(0, 4.10, z), 0.019, 0.026, 0.38)
	sphere("gold", Vector3(0, 4.30, z), 0.045)
	var a := Vector3(0.02, 4.25, z)
	var b := Vector3(0.72, 4.29, z + 0.035)
	var c := Vector3(0.55, 4.08, z - 0.025)
	var d := Vector3(0.73, 3.94, z + 0.015)
	var e := Vector3(0.02, 3.95, z)
	triangle("banner", a, c, b)
	triangle("banner", a, e, c)
	triangle("banner", e, d, c)
	box("gold", Vector3(0.105, 4.10, z - 0.02), Vector3(0.04, 0.29, 0.022))

func save_hall() -> void:
	DirAccess.make_dir_recursive_absolute("res://buildings/meshes")
	var mesh := ArrayMesh.new()
	for key: String in surfaces:
		surfaces[key].commit(mesh)
	# One visual mesh with material surfaces; dimensions include every roof and flag.
	var bounds := mesh.get_aabb()
	assert(bounds.position.x >= -1.501 and bounds.end.x <= 1.501)
	assert(bounds.position.z >= -1.501 and bounds.end.z <= 1.501)
	assert(bounds.position.y >= -0.001)
	assert(ResourceSaver.save(mesh, "res://buildings/meshes/TownHall.res", ResourceSaver.FLAG_COMPRESS) == OK)
	var hall := Node3D.new()
	hall.set_script(load("res://buildings/Building.cs"))
	hall.name = "TownHall"
	hall.set_meta("footprint", Vector2i(3, 3))
	hall.set_meta("front", "-Z")
	var visual := MeshInstance3D.new()
	visual.name = "Visual"
	visual.mesh = load("res://buildings/meshes/TownHall.res")
	hall.add_child(visual)
	visual.owner = hall
	var selection := Area3D.new()
	selection.name = "SelectionArea"
	selection.collision_layer = 8
	selection.collision_mask = 0
	selection.monitoring = false
	selection.monitorable = false
	hall.add_child(selection)
	selection.owner = hall
	var collision := CollisionShape3D.new()
	collision.name = "CollisionShape3D"
	collision.position.y = 1.65
	var shape := BoxShape3D.new()
	shape.size = Vector3(3, 3.3, 3)
	collision.shape = shape
	selection.add_child(collision)
	collision.owner = hall
	var entrance_marker := Marker3D.new()
	entrance_marker.name = "Entrance"
	entrance_marker.position = Vector3(0, 0, -1.5)
	hall.add_child(entrance_marker)
	entrance_marker.owner = hall
	var scene := PackedScene.new()
	assert(scene.pack(hall) == OK)
	assert(ResourceSaver.save(scene, OUTPUT) == OK)
	print("PASS: TownHall footprint 3x3, mesh bounds ", bounds, ", surfaces ", mesh.get_surface_count())
	hall.free()
