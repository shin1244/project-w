extends "res://tools/build_town_hall.gd"
## The lodging house (주택). The store, forge, derrick and tier-two builders also extend this script.

const SUPPLY_SCENE := "res://buildings/Supply.tscn"
const SUPPLY_MESH := "res://buildings/meshes/Supply.res"

func _initialize() -> void:
	setup_lodging_materials()
	build_lodging()
	save_supply()
	quit()

# ── Lodging house (주택, server type 3) ──
# A soot-blackened 19th-century British common lodging house for the company's day labourers:
# a three-storey brick terrace with two front doors, a hanging "beds" board in the company
# colour, chimney stacks crowded with clay pots, washing strung across the yard, a privy and a
# water pump out the back, and a worn-out lodger slumped on the step. Source grid 2x2, scaled to 3x3.

func setup_lodging_materials() -> void:
	material("brick", Color("6e3f33"))
	material("brick_dark", Color("4d2c24"))
	material("soot", Color("1e2226"))
	material("concrete", Color("858179"))
	material("cobble", Color("4f4c47"))
	material("tar", Color("2b2a29"), 0.2, 0.7)
	material("roof", Color("3d5f77"), 0.3, 0.6) # Painted flashing, company colour.
	material("banner", Color("376a94"))
	material("wood_dark", Color("3b2c22"))
	material("glass", Color("8f7444"), 0.0, 0.4, 0.15)
	material("terracotta", Color("a35a3c"))
	material("iron", Color("2f363d"), 0.5, 0.55)
	material("paper", Color("cfc5aa"))
	material("coat", Color("4a4642"))
	material("skin", Color("b08668"))

func build_lodging() -> void:
	lodging_yard()
	terrace()
	terrace_roof()
	lodging_sign()
	backyard()
	washing_line()
	lodger()

func lodging_yard() -> void:
	box("cobble", Vector3(0, 0.03, 0), Vector3(2.0, 0.06, 2.0))
	for i in range(9): # Setts along the front.
		box("soot", Vector3(-0.88 + i * 0.22, 0.062, -0.88), Vector3(0.01, 0.004, 0.22))
	box("soot", Vector3(0.1, 0.063, -0.62), Vector3(0.5, 0.003, 0.18)) # Coal dust by the doors.

func terrace() -> void:
	var x0 := -0.95
	var x1 := 0.95
	var z0 := -0.45
	var z1 := 0.5
	var top := 2.12
	box("brick", Vector3(0, (0.06 + top) * 0.5, (z0 + z1) * 0.5), Vector3(x1 - x0, top - 0.06, z1 - z0))
	# Soot blackens the upper storeys; a dark band marks each floor.
	box("soot", Vector3(0, top - 0.18, z0 - 0.004), Vector3(x1 - x0, 0.36, 0.006))
	for y in [0.75, 1.42]:
		box("brick_dark", Vector3(0, y, z0 - 0.006), Vector3(x1 - x0, 0.04, 0.012))
	# Party wall down the middle: two houses on one plot.
	box("brick_dark", Vector3(0, (0.06 + top) * 0.5, z0 - 0.012), Vector3(0.06, top - 0.06, 0.02))
	for side in [-1.0, 1.0]:
		var door := Vector3(side * 0.6, 0, z0)
		box("concrete", door + Vector3(0, 0.1, -0.12), Vector3(0.32, 0.08, 0.2))
		box("concrete", door + Vector3(0, 0.05, -0.2), Vector3(0.34, 0.04, 0.12))
		box("wood_dark", door + Vector3(0, 0.42, -0.012), Vector3(0.24, 0.62, 0.02))
		box("banner", door + Vector3(0, 0.42, -0.025), Vector3(0.19, 0.56, 0.006)) # Company paint on the doors.
		box("glass", door + Vector3(0, 0.79, -0.02), Vector3(0.2, 0.07, 0.006)) # Fanlight.
		box("concrete", door + Vector3(0, 0.84, -0.02), Vector3(0.28, 0.03, 0.03))
		window(Vector3(side * 0.22, 0.45, z0), 0.24, 0.36, false)
		for y in [1.08, 1.75]:
			window(Vector3(side * 0.24, y, z0), 0.22, 0.4, y > 1.5 and side < 0)
			window(Vector3(side * 0.7, y, z0), 0.22, 0.4, side > 0)
	# Iron downpipes and a coal hole cover.
	for x in [x0 + 0.03, x1 - 0.03]:
		box("iron", Vector3(x, (0.06 + top) * 0.5, z0 - 0.03), Vector3(0.035, top - 0.06, 0.035))
	cylinder("iron", Vector3(-0.2, 0.065, -0.62), 0.06, 0.06, 0.01, Basis.IDENTITY, 10)
	# Side and back walls: a few small, dirty windows.
	for z in [-0.1, 0.25]:
		box("glass", Vector3(x0 - 0.008, 1.42, z), Vector3(0.008, 0.3, 0.16))
	for x in [-0.6, 0.6]:
		box("glass", Vector3(x, 1.1, z1 + 0.008), Vector3(0.18, 0.3, 0.008))

func window(center: Vector3, width: float, height: float, curtained: bool) -> void:
	box("concrete", center + Vector3(0, -height * 0.5 - 0.02, -0.02), Vector3(width + 0.06, 0.03, 0.05))
	box("wood_dark", center + Vector3(0, 0, -0.008), Vector3(width, height, 0.014))
	box("paper" if curtained else "glass", center + Vector3(0, 0, -0.018), Vector3(width - 0.05, height - 0.05, 0.006))
	box("wood_dark", center + Vector3(0, 0, -0.022), Vector3(width - 0.04, 0.02, 0.004)) # Sash rail.
	box("concrete", center + Vector3(0, height * 0.5 + 0.03, -0.012), Vector3(width + 0.04, 0.04, 0.03)) # Lintel.

func terrace_roof() -> void:
	var eave := 2.12
	var ridge := 2.5
	var z0 := -0.5
	var z1 := 0.55
	var mid := (z0 + z1) * 0.5
	face("tar", Vector3(-0.98, eave, z0), Vector3(0.98, eave, z0), Vector3(0.98, ridge, mid), Vector3(-0.98, ridge, mid))
	face("tar", Vector3(-0.98, eave, z1), Vector3(0.98, eave, z1), Vector3(0.98, ridge, mid), Vector3(-0.98, ridge, mid))
	for x in [-1.0, 1.0]: # Gable ends.
		face("brick_dark", Vector3(x * 0.95, eave, z0 + 0.05), Vector3(x * 0.95, ridge, mid), Vector3(x * 0.95, eave, z1 - 0.05), Vector3(x * 0.95, eave, z0 + 0.05))
	# Company-painted ridge flashing and gutters: the colour that reads from above.
	box("roof", Vector3(0, ridge + 0.02, mid), Vector3(1.98, 0.05, 0.14))
	for z in [z0, z1]:
		box("roof", Vector3(0, eave - 0.01, z), Vector3(1.98, 0.05, 0.06))
	# A patch of iron sheet and a grimy skylight.
	box("iron", Vector3(-0.35, eave + 0.2, mid - 0.25), Vector3(0.28, 0.02, 0.2), Basis(Vector3.RIGHT, 0.63))
	box("glass", Vector3(0.4, eave + 0.17, mid + 0.3), Vector3(0.22, 0.02, 0.18), Basis(Vector3.RIGHT, -0.63))
	# Chimney stacks crowded with clay pots.
	for x in [-0.62, 0.0, 0.62]:
		box("brick_dark", Vector3(x, ridge + 0.17, mid), Vector3(0.3, 0.4, 0.2))
		box("concrete", Vector3(x, ridge + 0.38, mid), Vector3(0.34, 0.04, 0.24))
		for i in range(3):
			var pot := Vector3(x - 0.09 + i * 0.09, ridge + 0.47, mid)
			cylinder("terracotta", pot, 0.028, 0.038, 0.14, Basis.IDENTITY, 8)
			cylinder("soot", pot + Vector3(0, 0.072, 0), 0.026, 0.026, 0.004, Basis.IDENTITY, 8)

func lodging_sign() -> void:
	# A "good beds" board on an iron bracket at the corner, company colour, letters as blocks.
	var at := Vector3(-0.9, 1.3, -0.68)
	box("iron", Vector3(-0.9, 1.47, -0.57), Vector3(0.025, 0.025, 0.24))
	box("iron", Vector3(-0.9, 1.45, -0.68), Vector3(0.01, 0.05, 0.01))
	box("wood_dark", at, Vector3(0.03, 0.3, 0.3))
	box("banner", at + Vector3(-0.018, 0, 0), Vector3(0.006, 0.26, 0.26))
	box("banner", at + Vector3(0.018, 0, 0), Vector3(0.006, 0.26, 0.26))
	for side in [-1.0, 1.0]:
		for i in range(3):
			box("paper", at + Vector3(side * 0.022, 0.07 - i * 0.07, 0), Vector3(0.004, 0.035, 0.18 - i * 0.04))

func backyard() -> void:
	# A brick privy and a cast-iron pump behind the houses.
	box("brick", Vector3(0.62, 0.36, 0.76), Vector3(0.4, 0.6, 0.36))
	face("tar", Vector3(0.4, 0.7, 0.56), Vector3(0.84, 0.7, 0.56), Vector3(0.84, 0.62, 0.95), Vector3(0.4, 0.62, 0.95))
	box("wood_dark", Vector3(0.62, 0.3, 0.575), Vector3(0.18, 0.44, 0.012))
	cylinder("iron", Vector3(-0.55, 0.3, 0.76), 0.05, 0.06, 0.48, Basis.IDENTITY, 10)
	beam("iron", Vector3(-0.55, 0.5, 0.76), Vector3(-0.38, 0.62, 0.76), 0.02, 0.02)
	box("iron", Vector3(-0.55, 0.36, 0.66), Vector3(0.03, 0.03, 0.12))
	cylinder("iron", Vector3(-0.55, 0.1, 0.6), 0.1, 0.09, 0.08, Basis.IDENTITY, 10)
	box("brick_dark", Vector3(0, 0.25, 0.97), Vector3(2.0, 0.38, 0.04))

func washing_line() -> void:
	# Grey washing strung across the yard between two iron posts.
	for x in [-0.88, 0.88]:
		box("iron", Vector3(x, 0.46, -0.86), Vector3(0.025, 0.8, 0.025))
	box("iron", Vector3(0, 0.84, -0.86), Vector3(1.76, 0.006, 0.006))
	var hangs := [[-0.6, 0.16, 0.2, "paper"], [-0.32, 0.14, 0.26, "coat"], [-0.05, 0.2, 0.16, "paper"],
		[0.25, 0.12, 0.22, "coat"], [0.55, 0.18, 0.18, "paper"]]
	for h in hangs:
		box(h[3], Vector3(h[0], 0.84 - h[2] * 0.5, -0.86), Vector3(h[1], h[2], 0.008))

func lodger() -> void:
	# A worn-out lodger slumped on the left step, cap pulled down.
	var p := Vector3(-0.6, 0.14, -0.64)
	box("coat", p + Vector3(0, 0.12, 0.02), Vector3(0.14, 0.2, 0.12), Basis(Vector3.RIGHT, 0.35))
	for x in [-0.035, 0.035]:
		box("coat", p + Vector3(x, 0.04, -0.07), Vector3(0.05, 0.05, 0.14))
		box("coat", p + Vector3(x, -0.03, -0.14), Vector3(0.05, 0.1, 0.05))
	sphere("skin", p + Vector3(0, 0.26, -0.02), 0.045)
	cylinder("coat", p + Vector3(0, 0.29, -0.025), 0.05, 0.052, 0.025, Basis(Vector3.RIGHT, 0.4), 10)

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
