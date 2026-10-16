extends "res://tools/build_town_hall.gd"
## The lodgings (숙소). The store, forge, derrick and tier-two builders also extend this script.

const SUPPLY_SCENE := "res://buildings/Supply.tscn"
const SUPPLY_MESH := "res://buildings/meshes/Supply.res"

func _initialize() -> void:
	setup_lodging_materials()
	build_lodging()
	save_supply()
	add_stove_smoke()
	quit()

# ── Lodgings (숙소, server type 3) ──
# A low, ramshackle doss-house row for the company's day labourers: patched brick and plank huts
# under rusted corrugated sheets, a sagging tarp lean-to, boarded windows and a door hanging askew,
# bent stovepipes, a crude painted "beds" board, and lodgers warming their hands at a burning drum.
# Kept low on purpose so it never reads like the tall labour office. Source grid 2x2, scaled to 3x3.

func setup_lodging_materials() -> void:
	material("cobble", Color("4a4742"))
	material("brick", Color("6a3e33"))
	material("plank", Color("6e5e4c"))
	material("wood", Color("4a3a2c"))
	material("rust", Color("8a4f33"), 0.25, 0.75)
	material("tarp", Color("6d6857"))
	material("soot", Color("1e2226"))
	material("iron", Color("2f363d"), 0.4, 0.6)
	material("roof", Color("3d5f77"), 0.15, 0.75) # One painted sheet, company colour.
	material("banner", Color("376a94"))
	material("glass", Color("6e5c3c"), 0.0, 0.5, 0.1)
	material("coat", Color("4a4642"))
	material("skin", Color("b08668"))
	material("fire", Color("ffb257"), 0.0, 0.5, 2.4)

func build_lodging() -> void:
	lodging_yard()
	huts()
	hut_roofs()
	lean_to()
	stovepipes()
	lodging_sign()
	brazier()
	washing_line()
	sleeper()

func lodging_yard() -> void:
	box("cobble", Vector3(0, 0.03, 0), Vector3(2.0, 0.06, 2.0))
	box("soot", Vector3(0.1, 0.062, -0.55), Vector3(1.3, 0.003, 0.5)) # Trodden mud.
	# A broken plank fence down the left side.
	for i in range(6):
		var z := -0.85 + i * 0.3
		if i == 3:
			continue
		box("plank", Vector3(-0.95, 0.22 + (i % 2) * 0.03, z), Vector3(0.04, 0.34 + (i % 3) * 0.05, 0.22), Basis(Vector3.FORWARD, (i % 2) * 0.08))
	box("wood", Vector3(-0.95, 0.3, -0.1), Vector3(0.04, 0.04, 1.6))

func huts() -> void:
	# Two joined single-storey huts: patched brick below, planks above, rough and uneven.
	var x0 := -0.88
	var x1 := 0.5
	var z0 := -0.2
	var z1 := 0.72
	box("brick", Vector3((x0 + x1) * 0.5, 0.3, (z0 + z1) * 0.5), Vector3(x1 - x0, 0.48, z1 - z0))
	box("plank", Vector3((x0 + x1) * 0.5, 0.66, (z0 + z1) * 0.5), Vector3(x1 - x0 - 0.02, 0.26, z1 - z0 - 0.02))
	for i in range(9): # Plank seams on the front.
		box("wood", Vector3(x0 + 0.08 + i * 0.16, 0.66, z0 - 0.012), Vector3(0.012, 0.26, 0.006))
	box("soot", Vector3((x0 + x1) * 0.5, 0.75, z0 - 0.013), Vector3(x1 - x0, 0.08, 0.004))
	# A plank patch nailed over missing brick.
	box("plank", Vector3(-0.55, 0.3, z0 - 0.01), Vector3(0.22, 0.18, 0.02), Basis(Vector3.FORWARD, 0.1))
	# Doors: one shut, one hanging off its top hinge.
	box("wood", Vector3(-0.2, 0.36, z0 - 0.012), Vector3(0.2, 0.56, 0.02))
	box("wood", Vector3(0.26, 0.36, z0 - 0.04), Vector3(0.2, 0.56, 0.02), Basis(Vector3.UP, -0.5) * Basis(Vector3.FORWARD, 0.06))
	box("soot", Vector3(0.26, 0.36, z0 - 0.006), Vector3(0.2, 0.56, 0.004))
	for p in [Vector3(-0.2, 0.08, -0.3), Vector3(0.26, 0.08, -0.3)]: # Crate steps.
		box("wood", p, Vector3(0.18, 0.08, 0.14))
	# Boarded windows.
	for x in [-0.62, 0.02]:
		box("glass", Vector3(x, 0.52, z0 - 0.01), Vector3(0.2, 0.18, 0.006))
		box("plank", Vector3(x, 0.52, z0 - 0.018), Vector3(0.26, 0.04, 0.008), Basis(Vector3.FORWARD, 0.5))
		box("plank", Vector3(x, 0.52, z0 - 0.02), Vector3(0.26, 0.04, 0.008), Basis(Vector3.FORWARD, -0.45))

func hut_roofs() -> void:
	# Rusted corrugated sheets laid unevenly as a shed roof, one tar patch, one company-painted sheet.
	var sheets := [[-0.72, 0.0, "rust"], [-0.4, 0.06, "rust"], [-0.08, -0.04, "roof"], [0.24, 0.05, "rust"]]
	for s in sheets:
		var x: float = s[0]
		var tilt: float = s[1]
		var basis := Basis(Vector3.RIGHT, -0.16) * Basis(Vector3.FORWARD, tilt)
		box(s[2], Vector3(x + 0.08, 0.86 + tilt * 0.3, 0.26), Vector3(0.36, 0.025, 1.08), basis)
		for k in range(4): # Corrugation ridges.
			box("iron" if s[2] == "rust" else "roof", Vector3(x - 0.04 + k * 0.08, 0.875 + tilt * 0.3, 0.26), Vector3(0.012, 0.02, 1.06), basis)
	box("soot", Vector3(-0.3, 0.9, 0.05), Vector3(0.3, 0.012, 0.28), Basis(Vector3.RIGHT, -0.16))
	# Stones weighing the sheets down.
	for p in [Vector3(-0.6, 0.93, -0.12), Vector3(0.1, 0.93, 0.5), Vector3(-0.25, 0.86, 0.62)]:
		sphere("cobble", p, 0.045, 0.6)

func lean_to() -> void:
	# A plank lean-to on the right under a sagging tarp.
	var x0 := 0.5
	var x1 := 0.95
	for p in [Vector3(x1, 0, -0.15), Vector3(x1, 0, 0.6)]:
		box("wood", p + Vector3(0, 0.3, 0), Vector3(0.05, 0.6, 0.05))
	box("plank", Vector3(x1 - 0.01, 0.25, 0.22), Vector3(0.03, 0.4, 0.7))
	face("tarp", Vector3(x0, 0.7, -0.2), Vector3(x1 + 0.03, 0.58, -0.2), Vector3(x1 + 0.03, 0.58, 0.65), Vector3(x0, 0.7, 0.65))
	face("tarp", Vector3(x0 + 0.2, 0.6, 0.1), Vector3(x0 + 0.3, 0.6, 0.1), Vector3(x0 + 0.3, 0.6, 0.35), Vector3(x0 + 0.2, 0.6, 0.35))
	box("wood", Vector3(0.75, 0.12, 0.3), Vector3(0.3, 0.12, 0.4)) # A bunk of crates inside.
	box("tarp", Vector3(0.75, 0.2, 0.3), Vector3(0.28, 0.03, 0.38))

func stovepipes() -> void:
	# Thin iron stovepipes through the roof, one bent over at the top.
	for p in [Vector3(-0.55, 0, 0.45), Vector3(0.15, 0, 0.35)]:
		cylinder("iron", p + Vector3(0, 1.08, 0), 0.035, 0.035, 0.42, Basis.IDENTITY, 8)
		cylinder("soot", p + Vector3(0, 1.3, 0), 0.037, 0.037, 0.04, Basis.IDENTITY, 8)
	beam("iron", Vector3(0.15, 1.28, 0.35), Vector3(0.28, 1.36, 0.35), 0.06, 0.06)
	cylinder("iron", Vector3(-0.55, 1.33, 0.45), 0.07, 0.0, 0.06, Basis.IDENTITY, 6) # A tin hat.

func lodging_sign() -> void:
	# A crude board painted in the company colour, nailed on crooked over the doors.
	var at := Vector3(0.0, 0.88, -0.25)
	var basis := Basis(Vector3.FORWARD, 0.07)
	box("plank", at, Vector3(0.62, 0.14, 0.02), basis)
	box("banner", at + Vector3(0, 0, -0.012), Vector3(0.58, 0.11, 0.006), basis)
	for i in range(4):
		box("plank", at + basis * Vector3(-0.2 + i * 0.13, 0, -0.016), Vector3(0.07, 0.05, 0.004), basis)

func brazier() -> void:
	# A burning drum in the yard with two lodgers warming their hands.
	var drum := Vector3(0.45, 0.06, -0.62)
	cylinder("rust", drum + Vector3(0, 0.15, 0), 0.08, 0.08, 0.3, Basis.IDENTITY, 10)
	cylinder("fire", drum + Vector3(0, 0.31, 0), 0.07, 0.04, 0.06, Basis.IDENTITY, 8)
	cylinder("fire", drum + Vector3(0, 0.36, 0), 0.035, 0.0, 0.08, Basis.IDENTITY, 6)
	for side in [-1.0, 1.0]:
		var p := drum + Vector3(side * 0.2, 0, 0.05)
		var face_in := Basis(Vector3.UP, side * PI * 0.5)
		for x in [-0.03, 0.03]:
			box("coat", p + face_in * Vector3(x, 0.1, 0), Vector3(0.045, 0.2, 0.05), face_in)
		cylinder("coat", p + Vector3(0, 0.33, 0), 0.055, 0.075, 0.28, Basis(Vector3.FORWARD, -side * 0.12), 8)
		sphere("skin", p + Vector3(-side * 0.02, 0.51, 0), 0.045)
		cylinder("coat", p + Vector3(-side * 0.02, 0.55, 0), 0.05, 0.052, 0.025, Basis.IDENTITY, 10)
		box("coat", p + Vector3(-side * 0.09, 0.36, 0), Vector3(0.12, 0.035, 0.04)) # Arms out to the fire.

func washing_line() -> void:
	# Grey rags strung from the hut corner to a pole.
	box("wood", Vector3(-0.7, 0.42, -0.78), Vector3(0.03, 0.72, 0.03))
	beam("iron", Vector3(-0.7, 0.75, -0.78), Vector3(-0.86, 0.72, -0.21), 0.006, 0.006)
	for i in range(3):
		var t := 0.2 + i * 0.28
		var p := Vector3(-0.7, 0.75, -0.78).lerp(Vector3(-0.86, 0.72, -0.21), t)
		box("tarp" if i % 2 == 0 else "coat", p + Vector3(0, -0.09, 0), Vector3(0.012, 0.16, 0.12))

func sleeper() -> void:
	# A lodger asleep against the wall on a crate, knees up, cap over his eyes.
	var p := Vector3(-0.42, 0.1, -0.34)
	box("wood", p, Vector3(0.2, 0.08, 0.14))
	box("coat", p + Vector3(0, 0.14, 0.04), Vector3(0.13, 0.18, 0.1), Basis(Vector3.RIGHT, -0.3))
	box("coat", p + Vector3(0, 0.09, -0.08), Vector3(0.11, 0.08, 0.14))
	sphere("skin", p + Vector3(0, 0.27, 0.07), 0.042)
	cylinder("coat", p + Vector3(0, 0.29, 0.05), 0.05, 0.052, 0.025, Basis(Vector3.RIGHT, -0.5), 10)

func add_stove_smoke() -> void:
	# save_compact packs only the mesh; reopen the scene for a thin wisp from the stovepipe.
	var scene: PackedScene = load(SUPPLY_SCENE)
	var root := scene.instantiate()
	var visual := root.get_node("Visual") as Node3D
	var wisp := smoke("Smoke", Vector3(-0.55, 1.36, 0.45))
	wisp.amount = 6
	wisp.scale_amount_min = 0.12
	wisp.scale_amount_max = 0.18
	visual.add_child(wisp)
	wisp.owner = root
	var packed := PackedScene.new()
	assert(packed.pack(root) == OK)
	assert(ResourceSaver.save(packed, SUPPLY_SCENE) == OK)
	root.free()

# ── Helpers shared with the forge, derrick and tier-two builders that extend this script ──

func setup_materials() -> void:
	material("stone", Color("7e8779"))
	material("stone_light", Color("a6ad96"))
	material("stone_dark", Color("526054"))
	material("wood", Color("583b29"))
	material("wood_light", Color("a47947"))
	material("wood_warm", Color("896039"))
	material("dark", Color("28332d"), 0.25)
	material("roof", Color("274e66"))
	material("roof_mid", Color("315971"))
	material("roof_light", Color("3b667d"))
	material("gold", Color("d2aa59"), 0.25)
	material("canvas", Color("cebc85"))
	material("banner", Color("376a94"))
	material("plaster", Color("d8cba9"))
	material("window", Color("f7cf78"))


func crate(bottom: Vector3, size: float) -> void:
	box("wood_warm", bottom + Vector3(0, size * .5, 0), Vector3.ONE * size)
	for side in [-1.0, 1.0]:
		for t in [-.40, .40]:
			box("wood_light", bottom + Vector3(t * size, size * .5, side * (size * .5 + .006)), Vector3(size * .10, size, .018))
			box("wood_light", bottom + Vector3(side * (size * .5 + .006), size * .5, t * size), Vector3(.018, size, size * .10))
		for y in [.12, .88]:
			box("wood", bottom + Vector3(0, size * y, side * (size * .5 + .015)), Vector3(size, .032, .015))
		beam("wood_light", bottom + Vector3(-size * .36, size * .18, side * (size * .5 + .024)), bottom + Vector3(size * .36, size * .82, side * (size * .5 + .024)), .023, .016)
	for i in [-1, 0, 1]:
		box("wood_light", bottom + Vector3(i * size * .31, size + .009, 0), Vector3(size * .27, .018, size))


func barrel(bottom: Vector3, radius: float, height: float) -> void:
	var profile := [Vector2(radius * .84, 0), Vector2(radius, height * .35), Vector2(radius, height * .66), Vector2(radius * .84, height)]
	for segment in range(12):
		var a := TAU * segment / 12.0
		var b := TAU * (segment + 1) / 12.0
		for level in range(3):
			var low: Vector2 = profile[level]
			var high: Vector2 = profile[level + 1]
			quad("wood_light" if segment % 3 == 0 else "wood_warm",
				bottom + Vector3(cos(a) * low.x, low.y, sin(a) * low.x),
				bottom + Vector3(cos(a) * high.x, high.y, sin(a) * high.x),
				bottom + Vector3(cos(b) * high.x, high.y, sin(b) * high.x),
				bottom + Vector3(cos(b) * low.x, low.y, sin(b) * low.x))
	for fraction in [.14, .47, .86]:
		var band_radius := radius * (1.01 if fraction == .47 else .94)
		# Only the thin rim is generated: a full cylinder would hide the barrel lid.
		for segment in range(12):
			var a := TAU * segment / 12.0
			var b := TAU * (segment + 1) / 12.0
			quad("dark", bottom + Vector3(cos(a) * band_radius, height * fraction - .013, sin(a) * band_radius),
				bottom + Vector3(cos(a) * band_radius, height * fraction + .013, sin(a) * band_radius),
				bottom + Vector3(cos(b) * band_radius, height * fraction + .013, sin(b) * band_radius),
				bottom + Vector3(cos(b) * band_radius, height * fraction - .013, sin(b) * band_radius))
	cylinder("wood_light", bottom + Vector3(0, height + .002, 0), radius * .83, radius * .83, .018)
	for offset in [-.05, .05]:
		box("wood", bottom + Vector3(offset, height + .013, 0), Vector3(.008, .008, radius * 1.48))


func save_supply() -> void:
	save_compact(SUPPLY_SCENE, SUPPLY_MESH, "Supply", 3, "supply")

func save_compact(scene_path: String, mesh_path: String, model_name: String, type: int, role: String, footprint := Vector2i(2, 2)) -> void:
	DirAccess.make_dir_recursive_absolute("res://buildings/meshes")
	var mesh := ArrayMesh.new()
	for key: String in surfaces:
		var arrays: Array = surfaces[key].commit_to_arrays()
		if arrays[Mesh.ARRAY_VERTEX] != null and arrays[Mesh.ARRAY_VERTEX].size() > 0:
			surfaces[key].commit(mesh)
	var bounds := mesh.get_aabb()
	assert(bounds.position.x >= -footprint.x * .5 - .0001 and bounds.end.x <= footprint.x * .5 + .0001, "%s exceeds footprint width: %s" % [model_name, bounds])
	assert(bounds.position.z >= -footprint.y * .5 - .0001 and bounds.end.z <= footprint.y * .5 + .0001, "%s exceeds footprint depth: %s" % [model_name, bounds])
	assert(bounds.position.y >= -.0001, "%s extends below ground" % model_name)
	var triangles := 0
	for surface in range(mesh.get_surface_count()):
		var arrays := mesh.surface_get_arrays(surface)
		var indices = arrays[Mesh.ARRAY_INDEX]
		var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
		triangles += (indices.size() if indices != null and not indices.is_empty() else vertices.size()) / 3
	assert(triangles < 30000)
	assert(mesh.get_surface_count() <= 15)
	assert(ResourceSaver.save(mesh, mesh_path, ResourceSaver.FLAG_COMPRESS) == OK)
	var supply := Node3D.new()
	supply.set_script(load("res://buildings/Building.cs"))
	supply.name = model_name
	supply.set("BuildingType", type)
	supply.set("HealthBarHeight", bounds.end.y + .30)
	supply.set_meta("role", role)
	supply.set_meta("footprint", footprint)
	supply.set_meta("front", "-Z")
	var visual := MeshInstance3D.new()
	visual.name = "Visual"
	visual.mesh = load(mesh_path)
	supply.add_child(visual)
	visual.owner = supply
	var selection := Area3D.new()
	selection.name = "SelectionArea"
	selection.collision_layer = 8
	selection.collision_mask = 0
	selection.monitoring = false
	selection.monitorable = false
	supply.add_child(selection)
	selection.owner = supply
	var collision := CollisionShape3D.new()
	collision.name = "CollisionShape3D"
	collision.position.y = bounds.end.y * .5
	var shape := BoxShape3D.new()
	shape.size = Vector3(footprint.x, bounds.end.y, footprint.y)
	collision.shape = shape
	selection.add_child(collision)
	collision.owner = supply
	var entrance_marker := Marker3D.new()
	entrance_marker.name = "Entrance"
	entrance_marker.position = Vector3(0, 0, -footprint.y * .5)
	supply.add_child(entrance_marker)
	entrance_marker.owner = supply
	apply_server_footprint(supply)
	var scene := PackedScene.new()
	assert(scene.pack(supply) == OK)
	assert(ResourceSaver.save(scene, scene_path) == OK)
	print("PASS: ", model_name, " footprint ", supply.get_meta("footprint"), ", source bounds ", bounds, ", triangles ", triangles, ", surfaces ", mesh.get_surface_count())
	supply.free()
