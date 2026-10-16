extends "res://tools/build_town_hall.gd"
## The labour office (인력 사무소, server type 4): a drab Depression-era employment exchange of the
## company (docs/art-direction.md). Grimy two-storey brick office, a painted company signboard,
## a rooftop hiring billboard and wooden water tank, an iron fire escape, and a queue of men in
## flat caps waiting at the door. Modelled on a 3x2 grid, then scaled to the server's 4x3 footprint.

func _initialize() -> void:
	material("concrete", Color("8a867e"))
	material("concrete_dark", Color("615e58"))
	material("brick", Color("74463a"))
	material("brick_dark", Color("553329"))
	material("soot", Color("1e2226"))
	material("iron", Color("2f363d"), 0.5, 0.55)
	material("iron_light", Color("8f979c"), 0.55, 0.45)
	material("brass", Color("a88349"), 0.6, 0.4)
	material("wood", Color("5a4232"))
	material("wood_dark", Color("3e2e24"))
	material("canvas", Color("8f8470"))
	material("paper", Color("d9cfb4"))
	material("coat", Color("4a4642"))
	material("coat_brown", Color("5b4a3a"))
	material("cap", Color("35322f"))
	material("skin", Color("b48a6c"))
	material("glass", Color("9c7f4a"), 0.0, 0.35, 0.18)
	material("tar", Color("2a2826")) # Dim, dirty light behind grimy panes.
	material("banner", Color("376a94")) # Company colour (team tint).
	sidewalk()
	office()
	signage()
	roof_works()
	fire_escape()
	queue()
	save_barracks()
	quit()

func sidewalk() -> void:
	box("concrete_dark", Vector3(0, 0.03, 0), Vector3(3.0, 0.06, 2.0))
	box("concrete", Vector3(0, 0.065, 0), Vector3(2.96, 0.012, 1.96))
	for x in [-1.0, -0.4, 0.2, 0.8]: # Paving joints.
		box("concrete_dark", Vector3(x, 0.072, -0.7), Vector3(0.012, 0.004, 0.6))
	box("soot", Vector3(-0.6, 0.073, -0.62), Vector3(1.0, 0.003, 0.22)) # Worn where the queue stands.

func office() -> void:
	var x0 := -1.3
	var x1 := 1.02
	var z0 := -0.42
	var z1 := 0.92
	var top := 2.18
	var cx := (x0 + x1) * 0.5
	box("brick", Vector3(cx, (0.07 + top) * 0.5, (z0 + z1) * 0.5), Vector3(x1 - x0, top - 0.07, z1 - z0))
	# Concrete ground-floor front, floor band and parapet with coping.
	box("concrete", Vector3(cx, 0.55, z0 - 0.01), Vector3(x1 - x0 + 0.02, 0.96, 0.03))
	box("concrete", Vector3(cx, 1.17, (z0 + z1) * 0.5), Vector3(x1 - x0 + 0.06, 0.07, z1 - z0 + 0.06))
	for z in [z0, z1]: # Parapet walls around the roof.
		box("brick_dark", Vector3(cx, top + 0.08, z), Vector3(x1 - x0 + 0.02, 0.16, 0.06))
	for x in [x0, x1]:
		box("brick_dark", Vector3(x, top + 0.08, (z0 + z1) * 0.5), Vector3(0.06, 0.16, z1 - z0 + 0.02))
	# Tarred roof inside a concrete coping rim.
	box("tar", Vector3(cx, top + 0.1, (z0 + z1) * 0.5), Vector3(x1 - x0 - 0.06, 0.02, z1 - z0 - 0.06))
	for z in [z0, z1]:
		box("concrete", Vector3(cx, top + 0.17, z), Vector3(x1 - x0 + 0.08, 0.03, 0.08))
	for x in [x0, x1]:
		box("concrete", Vector3(x, top + 0.17, (z0 + z1) * 0.5), Vector3(0.08, 0.03, z1 - z0 + 0.08))
	# Soot streaks run down from the parapet and the upper sills.
	for x in [-1.05, -0.55, 0.15, 0.7]:
		box("soot", Vector3(x, top - 0.25, z0 - 0.012), Vector3(0.08, 0.45, 0.006))
	box("soot", Vector3(cx, top - 0.04, (z0 + z1) * 0.5), Vector3(x1 - x0 + 0.01, 0.06, z1 - z0 + 0.01))
	# Shopfront windows either side of the door; the door sits a step back.
	for span in [[-1.2, -0.38], [0.36, 0.94]]:
		var w: float = span[1] - span[0]
		var c: float = (span[0] + span[1]) * 0.5
		box("wood_dark", Vector3(c, 0.6, z0 - 0.035), Vector3(w, 0.62, 0.025))
		box("glass", Vector3(c, 0.62, z0 - 0.05), Vector3(w - 0.06, 0.52, 0.01))
		for i in range(1, 4): # Mullions.
			box("wood_dark", Vector3(span[0] + w * i / 4.0, 0.62, z0 - 0.056), Vector3(0.02, 0.52, 0.008))
		box("concrete_dark", Vector3(c, 0.27, z0 - 0.06), Vector3(w + 0.04, 0.05, 0.06))
		# Job notices pasted inside the glass.
		for i in range(3):
			box("paper", Vector3(span[0] + 0.1 + i * w * 0.3, 0.72 - (i % 2) * 0.12, z0 - 0.058), Vector3(0.09, 0.12, 0.004))
	box("wood_dark", Vector3(0, 0.5, z0 - 0.02), Vector3(0.42, 0.86, 0.03))
	box("wood", Vector3(0, 0.46, z0 - 0.04), Vector3(0.32, 0.76, 0.02))
	box("glass", Vector3(0, 0.7, z0 - 0.052), Vector3(0.2, 0.18, 0.006))
	sphere("brass", Vector3(0.11, 0.45, z0 - 0.06), 0.018)
	for i in range(2): # Worn steps.
		box("concrete_dark", Vector3(0, 0.09 + i * 0.04, z0 - 0.12 + i * 0.05), Vector3(0.5, 0.04, 0.14))
	# Upper floor: tall grimy sash windows, some with drawn blinds.
	for i in range(5):
		var x := x0 + 0.24 + i * 0.46
		box("concrete", Vector3(x, 1.27, z0 - 0.02), Vector3(0.3, 0.04, 0.05))
		box("wood_dark", Vector3(x, 1.62, z0 - 0.01), Vector3(0.26, 0.62, 0.02))
		box("paper" if i % 3 == 1 else "glass", Vector3(x, 1.62, z0 - 0.025), Vector3(0.2, 0.54, 0.008))
		box("wood_dark", Vector3(x, 1.62, z0 - 0.03), Vector3(0.2, 0.02, 0.006))
	# Side windows and a back door.
	for z in [-0.05, 0.5]:
		box("glass", Vector3(x0 - 0.012, 1.62, z), Vector3(0.008, 0.5, 0.2))
		box("glass", Vector3(x0 - 0.012, 0.62, z), Vector3(0.008, 0.45, 0.2))

func signage() -> void:
	var z := -0.47
	# The painted company board over the shopfront, letters as ivory blocks.
	box("wood_dark", Vector3(-0.14, 1.02, z), Vector3(2.3, 0.2, 0.04))
	box("banner", Vector3(-0.14, 1.02, z - 0.025), Vector3(2.22, 0.15, 0.012))
	var widths := [0.07, 0.06, 0.08, 0.06, 0.07, 0.0, 0.08, 0.06, 0.07, 0.06, 0.08, 0.06, 0.07, 0.0, 0.07, 0.08, 0.06]
	var x := -1.1
	for w in widths:
		if w > 0.0:
			box("paper", Vector3(x + w * 0.5, 1.02, z - 0.034), Vector3(w * 0.75, 0.085, 0.004))
		x += maxf(w, 0.05) + 0.03
	# A drab canvas awning over the door on iron brackets.
	box("canvas", Vector3(0, 0.98, -0.6), Vector3(0.6, 0.03, 0.34), Basis(Vector3.RIGHT, -0.32))
	for side in [-1.0, 1.0]:
		beam("iron", Vector3(side * 0.28, 1.04, -0.43), Vector3(side * 0.28, 0.9, -0.76), 0.015, 0.015)
	# Notice board by the door: postings, most already torn.
	box("wood", Vector3(-0.29, 0.62, z - 0.01), Vector3(0.12, 0.3, 0.02))
	for i in range(3):
		box("paper", Vector3(-0.29, 0.52 + i * 0.1, z - 0.024), Vector3(0.09, 0.07, 0.004))
	# A gas lamp at the corner.
	cylinder("iron", Vector3(0.98, 0.55, -0.78), 0.02, 0.03, 0.96)
	box("glass", Vector3(0.98, 1.06, -0.78), Vector3(0.08, 0.11, 0.08))
	cylinder("iron", Vector3(0.98, 1.14, -0.78), 0.0, 0.07, 0.06, Basis.IDENTITY, 4)

func roof_works() -> void:
	var top := 2.38
	# Wooden water tank on iron legs: the American rooftop silhouette.
	var tank := Vector3(-0.72, top, 0.45)
	for dx in [-0.18, 0.18]:
		for dz in [-0.18, 0.18]:
			beam("iron", tank + Vector3(dx, 0, dz), tank + Vector3(dx * 0.8, 0.32, dz * 0.8), 0.03, 0.03)
	cylinder("wood", tank + Vector3(0, 0.57, 0), 0.25, 0.25, 0.5, Basis.IDENTITY, 14)
	for y in [0.4, 0.57, 0.74]:
		cylinder("iron", tank + Vector3(0, y, 0), 0.258, 0.258, 0.025, Basis.IDENTITY, 14)
	cylinder("wood_dark", tank + Vector3(0, 0.9, 0), 0.0, 0.29, 0.22, Basis.IDENTITY, 14)
	# The hiring billboard on an iron truss, facing the street.
	var board := Vector3(0.38, top + 0.42, 0.1)
	for dx in [-0.42, 0.42]:
		beam("iron", Vector3(board.x + dx, top, board.z + 0.12), Vector3(board.x + dx, top + 0.62, board.z), 0.03, 0.03)
		beam("iron", Vector3(board.x + dx, top, board.z - 0.1), Vector3(board.x + dx, top + 0.3, board.z), 0.025, 0.025)
	box("wood_dark", board, Vector3(1.0, 0.42, 0.04))
	box("banner", board + Vector3(0, 0, -0.025), Vector3(0.94, 0.36, 0.01))
	box("paper", board + Vector3(-0.08, 0.06, -0.032), Vector3(0.62, 0.09, 0.004))
	box("paper", board + Vector3(-0.2, -0.08, -0.032), Vector3(0.38, 0.06, 0.004))
	cylinder("paper", board + Vector3(0.32, -0.02, -0.032), 0.1, 0.1, 0.004, Basis(Vector3.RIGHT, PI * 0.5), 12)
	# A squat chimney and roof vents.
	box("brick_dark", Vector3(0.85, top + 0.15, 0.72), Vector3(0.18, 0.34, 0.18))
	box("soot", Vector3(0.85, top + 0.33, 0.72), Vector3(0.19, 0.04, 0.19))
	for x in [-0.15, 0.1]:
		cylinder("iron", Vector3(x, top + 0.08, 0.7), 0.05, 0.05, 0.14)

func fire_escape() -> void:
	# Iron landings and switchback stairs down the side wall.
	var x := 1.2
	for y in [1.15, 2.05]:
		box("iron", Vector3(x, y, 0.25), Vector3(0.34, 0.03, 0.8))
		for z in [-0.12, 0.25, 0.62]:
			beam("iron", Vector3(x + 0.16, y, z), Vector3(x + 0.16, y + 0.22, z), 0.015, 0.015)
		box("iron", Vector3(x + 0.16, y + 0.22, 0.25), Vector3(0.015, 0.015, 0.8))
	beam("iron", Vector3(x, 1.15, 0.6), Vector3(x, 2.05, -0.1), 0.2, 0.03)
	beam("iron", Vector3(x, 0.07, -0.1), Vector3(x, 1.15, 0.6), 0.2, 0.03)
	for y in [0.5, 1.6]:
		box("iron_light", Vector3(x - 0.17, y, 0.25), Vector3(0.012, 0.012, 0.85))

func queue() -> void:
	# Men waiting for a day's contract, shoulders down, in a line from the door.
	var spots := [Vector3(-0.2, 0, -0.72), Vector3(-0.43, 0, -0.74), Vector3(-0.66, 0, -0.7),
		Vector3(-0.88, 0, -0.75), Vector3(-1.1, 0, -0.71), Vector3(-1.32, 0, -0.68), Vector3(-1.38, 0, -0.42)]
	for i in range(spots.size()):
		var p: Vector3 = spots[i] + Vector3(0, 0.07, 0)
		var coat := "coat_brown" if i % 3 == 1 else "coat"
		var lean := Basis(Vector3.FORWARD, 0.06 * (1 if i % 2 == 0 else -1)) * Basis(Vector3.RIGHT, 0.12)
		box("cap", p + Vector3(-0.03, 0.09, 0), Vector3(0.045, 0.18, 0.05))
		box("cap", p + Vector3(0.03, 0.09, 0), Vector3(0.045, 0.18, 0.05))
		cylinder(coat, p + Vector3(0, 0.33, 0), 0.06, 0.085, 0.32, lean, 8)
		sphere("skin", p + Vector3(0, 0.54, 0.01), 0.05)
		cylinder("cap", p + Vector3(0, 0.58, 0.005), 0.055, 0.06, 0.03, Basis.IDENTITY, 10)
		box("cap", p + Vector3(0, 0.57, -0.05), Vector3(0.09, 0.012, 0.05)) # Peak.
		if i == 2: # One holds a folded newspaper.
			box("paper", p + Vector3(0.07, 0.33, -0.04), Vector3(0.05, 0.08, 0.01))

func save_barracks() -> void:
	DirAccess.make_dir_recursive_absolute("res://buildings/meshes")
	var mesh := ArrayMesh.new()
	var triangles := 0
	for key: String in surfaces:
		var arrays: Array = surfaces[key].commit_to_arrays()
		if arrays[Mesh.ARRAY_VERTEX] == null or arrays[Mesh.ARRAY_VERTEX].is_empty(): continue
		triangles += arrays[Mesh.ARRAY_VERTEX].size() / 3
		surfaces[key].commit(mesh)
	var bounds := mesh.get_aabb()
	assert(bounds.position.x >= -1.501 and bounds.end.x <= 1.501, "Labour office exceeds 3-wide footprint")
	assert(bounds.position.z >= -1.001 and bounds.end.z <= 1.001, "Labour office exceeds 2-deep footprint")
	assert(bounds.position.y >= -.001, "Labour office extends below ground")
	assert(mesh.get_surface_count() <= 24 and triangles < 35000)
	assert(ResourceSaver.save(mesh, "res://buildings/meshes/Barracks.res", ResourceSaver.FLAG_COMPRESS) == OK)
	var barracks := Node3D.new()
	barracks.name = "Barracks"
	barracks.set_script(load("res://buildings/Building.cs"))
	barracks.set("BuildingType", 4)
	barracks.set("HealthBarHeight", bounds.end.y + 0.25)
	barracks.set_meta("footprint", Vector2i(3, 2))
	barracks.set_meta("front", "-Z")
	barracks.set_meta("role", "barracks")
	barracks.set_meta("tier", 1)
	var visual := MeshInstance3D.new()
	visual.name = "Visual"
	visual.mesh = load("res://buildings/meshes/Barracks.res")
	barracks.add_child(visual)
	visual.owner = barracks
	var selection := Area3D.new()
	selection.name = "SelectionArea"
	selection.collision_layer = 8
	selection.collision_mask = 0
	selection.monitoring = false
	selection.monitorable = false
	barracks.add_child(selection)
	selection.owner = barracks
	var collision := CollisionShape3D.new()
	collision.name = "CollisionShape3D"
	collision.position.y = bounds.end.y * .5
	var shape := BoxShape3D.new()
	shape.size = Vector3(3, bounds.end.y, 2)
	collision.shape = shape
	selection.add_child(collision)
	collision.owner = barracks
	var entrance_marker := Marker3D.new()
	entrance_marker.name = "Entrance"
	entrance_marker.position = Vector3(0, 0, -1)
	barracks.add_child(entrance_marker)
	entrance_marker.owner = barracks
	apply_server_footprint(barracks)
	var scene := PackedScene.new()
	assert(scene.pack(barracks) == OK)
	assert(ResourceSaver.save(scene, "res://buildings/Barracks.tscn") == OK)
	print("PASS: Barracks footprint ", barracks.get_meta("footprint"), "; source bounds ", bounds, "; triangles ", triangles, "; material surfaces ", mesh.get_surface_count())
	barracks.free()
