extends "res://tools/build_town_hall.gd"
## Reusable 3x2 tier-one barracks. Model only; local front is -Z.
## Production rules and protocol IDs belong to the server integration.

func _initialize() -> void:
	material("stone", Color("7e8275"))
	material("stone_light", Color("a0a592"))
	material("stone_dark", Color("535d55"))
	material("plaster", Color("c8b993"))
	material("wood", Color("50382a"))
	material("wood_light", Color("8e603b"))
	material("dark", Color("23312f"), 0.25)
	material("roof", Color("274e66"))
	material("roof_mid", Color("315c73"))
	material("roof_light", Color("3b6980"))
	material("gold", Color("c49a49"), 0.35)
	material("steel", Color("a3b3b2"), 0.55)
	material("banner", Color("376a94"))
	foundation()
	main_hall()
	main_roof()
	gatehouse()
	equipment()
	pennant()
	save_barracks()
	quit()

func foundation() -> void:
	# The bevels soften the slab without changing its exact map footprint.
	var points := [Vector2(-1.38, -1), Vector2(1.38, -1), Vector2(1.5, -.88),
		Vector2(1.5, .88), Vector2(1.38, 1), Vector2(-1.38, 1), Vector2(-1.5, .88), Vector2(-1.5, -.88)]
	for i in range(points.size()):
		var a: Vector2 = points[i]
		var b: Vector2 = points[(i + 1) % points.size()]
		triangle("stone", Vector3(0, .16, 0), Vector3(b.x, .16, b.y), Vector3(a.x, .16, a.y))
		quad("stone_dark", Vector3(a.x, 0, a.y), Vector3(a.x, .16, a.y), Vector3(b.x, .16, b.y), Vector3(b.x, 0, b.y))
	box("stone_dark", Vector3(0, .29, .23), Vector3(2.56, .28, 1.19))
	box("stone_light", Vector3(0, .42, .23), Vector3(2.62, .08, 1.25))
	# Forecourt paving, entrance steps and patterned stone edging.
	for i in range(8):
		box("stone_light" if i % 3 == 0 else "stone", Vector3(-1.26 + i * .36, .175, -.83), Vector3(.34, .035, .28))
	box("stone_light", Vector3(0, .235, -.78), Vector3(1.17, .13, .34))
	box("stone", Vector3(0, .335, -.59), Vector3(1.10, .10, .25))
	for side in [-1.0, 1.0]:
		for i in range(4):
			box("stone_light", Vector3(side * 1.365, .195, -.49 + i * .38), Vector3(.15, .07, .345))

func main_hall() -> void:
	box("stone_dark", Vector3(0, .66, .24), Vector3(2.40, .48, 1.06))
	box("plaster", Vector3(0, 1.24, .24), Vector3(2.39, .72, 1.06))
	# Individual blocks retain broad shapes that survive a normal RTS camera.
	for z in [-.302, .782]:
		for row in range(2):
			for col in range(9):
				var x := -1.066 + col * .267
				if z < 0 and absf(x) < .55: continue
				box("stone_light" if (col + row * 3) % 5 == 0 else "stone", Vector3(x, .55 + row * .215, z), Vector3(.25, .20, .055))
	for side in [-1.0, 1.0]:
		for row in range(2):
			for col in range(4):
				box("stone_light" if (col + row) % 3 == 0 else "stone", Vector3(side * 1.211, .55 + row * .215, -.16 + col * .265), Vector3(.055, .20, .247))
	# Oak framing and stone corner piers anchor the building under its heavy roof.
	for x in [-1.20, -.63, .63, 1.20]:
		for z in [-.31, .79]:
			box("wood", Vector3(x, 1.08, z), Vector3(.105, 1.13, .105))
			if absf(x) > 1:
				box("stone", Vector3(x, .65, z), Vector3(.20, .40, .19))
				box("stone_light", Vector3(x, .885, z), Vector3(.23, .07, .215))
			box("dark", Vector3(x, 1.46, z - .059 if z < 0 else z + .059), Vector3(.11, .085, .018))
	for z in [-.319, .80]:
		box("wood", Vector3(0, 1.58, z), Vector3(2.53, .13, .14))
		box("wood_light", Vector3(0, .95, z), Vector3(2.50, .075, .095))
		for side in [-1.0, 1.0]:
			beam("wood", Vector3(side * .70, 1.02, z), Vector3(side * 1.12, 1.51, z), .065, .070)
	# Gable ends, inset louver vents and under-eave braces.
	for side in [-1.0, 1.0]:
		var x: float = side * 1.205
		if side > 0:
			triangle("plaster", Vector3(x, 1.62, -.31), Vector3(x, 2.30, .24), Vector3(x, 1.62, .79))
		else:
			triangle("plaster", Vector3(x, 1.62, .79), Vector3(x, 2.30, .24), Vector3(x, 1.62, -.31))
		box("wood", Vector3(x, 1.60, .24), Vector3(.12, .12, 1.22))
		beam("wood", Vector3(x, 1.63, -.32), Vector3(x, 2.30, .24), .075, .075)
		beam("wood", Vector3(x, 2.30, .24), Vector3(x, 1.63, .80), .075, .075)
		box("wood", Vector3(x, 1.96, .24), Vector3(.115, .66, .080))
		box("dark", Vector3(side * 1.272, 1.27, .23), Vector3(.027, .36, .47))
		for i in range(4):
			box("wood_light", Vector3(side * 1.292, 1.135 + i * .089, .23), Vector3(.025, .045, .42))
		box("stone_light", Vector3(side * 1.286, 1.035, .23), Vector3(.11, .07, .55))
		for z in [-.21, .69]:
			beam("wood_light", Vector3(side * 1.22, 1.36, z), Vector3(side * 1.35, 1.68, z), .065, .08)
	# Narrow defensive windows on the long rear wall.
	for x in [-.85, 0.0, .85]:
		box("wood", Vector3(x, 1.23, .806), Vector3(.28, .43, .06))
		box("dark", Vector3(x, 1.25, .847), Vector3(.17, .30, .025))
		box("wood_light", Vector3(x, 1.25, .867), Vector3(.027, .30, .025))

func main_roof() -> void:
	# Ridge runs along X, expressing the 3x2 hall's broad military frontage.
	var center_z := .24
	var half_depth := .66
	var eave := 1.70
	var ridge := 2.38
	for side in [-1.0, 1.0]:
		var edge_z: float = center_z + side * half_depth
		if side < 0:
			quad("roof", Vector3(-1.40, eave, edge_z), Vector3(-1.40, ridge, center_z), Vector3(1.40, ridge, center_z), Vector3(1.40, eave, edge_z))
		else:
			quad("roof", Vector3(-1.40, ridge, center_z), Vector3(-1.40, eave, edge_z), Vector3(1.40, eave, edge_z), Vector3(1.40, ridge, center_z))
		for row in range(6):
			for col in range(14):
				var near := half_depth * row / 6.0 + .004
				var far := half_depth * (row + 1) / 6.0 - .007
				var za: float = center_z + side * near
				var zb: float = center_z + side * far
				var ya := lerpf(ridge, eave, near / half_depth) + .025
				var yb := lerpf(ridge, eave, far / half_depth) + .030
				var xa := -1.39 + col * 2.78 / 14.0 + .006
				var xb := -1.39 + (col + 1) * 2.78 / 14.0 - .006
				var color: String = ["roof", "roof_mid", "roof_light"][(col * 5 + row * 3 + row / 2) % 3]
				if side < 0:
					quad(color, Vector3(xa, yb, zb), Vector3(xa, ya, za), Vector3(xb, ya, za), Vector3(xb, yb, zb))
				else:
					quad(color, Vector3(xa, ya, za), Vector3(xa, yb, zb), Vector3(xb, yb, zb), Vector3(xb, ya, za))
		box("wood", Vector3(0, eave - .035, edge_z), Vector3(2.84, .10, .085))
		box("gold", Vector3(0, eave + .021, edge_z - side * .012), Vector3(2.81, .018, .025))
	for x in [-1.40, 1.40]:
		beam("wood", Vector3(x, eave, center_z - half_depth), Vector3(x, ridge, center_z), .085, .085)
		beam("wood", Vector3(x, ridge, center_z), Vector3(x, eave, center_z + half_depth), .085, .085)
		beam("gold", Vector3(x, eave + .03, center_z - half_depth), Vector3(x, ridge + .03, center_z), .025, .025)
		beam("gold", Vector3(x, ridge + .03, center_z), Vector3(x, eave + .03, center_z + half_depth), .025, .025)
	box("wood", Vector3(0, ridge + .025, center_z), Vector3(2.88, .09, .12))
	box("gold", Vector3(0, ridge + .078, center_z), Vector3(2.87, .025, .065))
	for x in [-1.30, 1.30]:
		box("dark", Vector3(x, ridge + .07, center_z), Vector3(.09, .11, .145))

func gatehouse() -> void:
	# Recessed oak double doors, iron hinge bands and a raised heraldic porch.
	box("stone_dark", Vector3(0, .965, -.366), Vector3(1.20, 1.12, .16))
	box("dark", Vector3(0, .98, -.467), Vector3(1.02, 1.16, .052))
	for i in range(8):
		box("wood_light" if i % 3 != 0 else "wood", Vector3(-.441 + i * .126, .97, -.506), Vector3(.116, 1.10, .055))
	for x in [-.59, .59]:
		box("stone", Vector3(x, .99, -.46), Vector3(.19, 1.18, .27))
		for row in range(5):
			box("stone_light" if row % 2 == 0 else "stone", Vector3(x, .51 + row * .235, -.605), Vector3(.215, .215, .045))
	box("stone_light", Vector3(0, 1.59, -.478), Vector3(1.43, .19, .30))
	for x in [-.25, .25]:
		for y in [.65, 1.22]:
			box("dark", Vector3(x, y, -.548), Vector3(.45, .065, .026))
			for dx in [-.15, .15]:
				sphere("gold", Vector3(x + dx, y, -.568), .017)
	for x in [-.10, .10]:
		cylinder("dark", Vector3(x, .96, -.553), .055, .055, .022, Basis(Vector3.RIGHT, PI / 2))
		var ring := TorusMesh.new()
		ring.inner_radius = .026
		ring.outer_radius = .040
		ring.rings = 12
		ring.ring_segments = 6
		append_mesh("gold", ring, Transform3D(Basis(Vector3.RIGHT, PI / 2), Vector3(x, .938, -.58)))
	# Front-facing gable breaks the long main roof and carries the military crest.
	triangle("wood", Vector3(-.66, 1.68, -.58), Vector3(0, 2.20, -.58), Vector3(.66, 1.68, -.58))
	triangle("plaster", Vector3(-.53, 1.73, -.59), Vector3(0, 2.12, -.59), Vector3(.53, 1.73, -.59))
	for side in [-1.0, 1.0]:
		var x: float = side * .75
		if side > 0:
			quad("roof_mid", Vector3(0, 2.23, -.70), Vector3(0, 2.23, -.05), Vector3(x, 1.68, -.05), Vector3(x, 1.68, -.70))
		else:
			quad("roof_mid", Vector3(x, 1.68, -.70), Vector3(x, 1.68, -.05), Vector3(0, 2.23, -.05), Vector3(0, 2.23, -.70))
		for row in range(4):
			var xa: float = side * (.007 + row * .184)
			var xb: float = side * (.174 + row * .184)
			var ya := lerpf(2.23, 1.68, absf(xa) / .75) + .02
			var yb := lerpf(2.23, 1.68, absf(xb) / .75) + .026
			for col in range(4):
				var za := -.689 + col * .157
				var zb := za + .142
				if side > 0:
					quad("roof_light" if (row + col) % 3 == 0 else "roof", Vector3(xa, ya, za), Vector3(xa, ya, zb), Vector3(xb, yb, zb), Vector3(xb, yb, za))
				else:
					quad("roof_light" if (row + col) % 3 == 0 else "roof", Vector3(xb, yb, za), Vector3(xb, yb, zb), Vector3(xa, ya, zb), Vector3(xa, ya, za))
		beam("wood", Vector3(x, 1.68, -.70), Vector3(0, 2.23, -.70), .075, .075)
		beam("gold", Vector3(x, 1.715, -.72), Vector3(0, 2.265, -.72), .025, .025)
	box("gold", Vector3(0, 2.258, -.39), Vector3(.068, .035, .68))
	shield(Vector3(0, 1.95, -.645), .28, "banner")
	# Two bright crossed swords make the recruitment role readable from afar.
	for side in [-1.0, 1.0]:
		var low := Vector3(side * .22, 1.78, -.677)
		var high := Vector3(-side * .22, 2.11, -.677)
		beam("steel", low, high, .040, .023)
		beam("gold", low + Vector3(-.055, .066, 0), low + Vector3(.055, -.066, 0), .022, .022)
		beam("wood_light", low, low + Vector3(side * .068, -.051, 0), .041, .030)

func shield(center: Vector3, scale_value: float, fill: String) -> void:
	var outer := [Vector2(-.72, .90), Vector2(.72, .90), Vector2(.66, -.12), Vector2(0, -.94), Vector2(-.66, -.12)]
	for i in range(outer.size()):
		var a: Vector2 = outer[i] * scale_value
		var b: Vector2 = outer[(i + 1) % outer.size()] * scale_value
		triangle("gold", center + Vector3(0, 0, -.003), center + Vector3(a.x, a.y, 0), center + Vector3(b.x, b.y, 0))
		triangle(fill, center + Vector3(0, 0, -.015), center + Vector3(a.x * .78, a.y * .78, -.009), center + Vector3(b.x * .78, b.y * .78, -.009))

func equipment() -> void:
	# Spear and shield rack beside the gate, entirely inside the forecourt.
	for x in [-1.29, -.83]:
		box("wood", Vector3(x, .67, -.73), Vector3(.075, .96, .080))
		box("stone_light", Vector3(x, .22, -.73), Vector3(.14, .10, .18))
	for y in [.47, 1.06]:
		box("wood_light", Vector3(-1.06, y, -.745), Vector3(.57, .075, .08))
	for i in range(3):
		var x := -1.26 + i * .17
		var y := 1.39 + (i % 2) * .11
		beam("wood_light", Vector3(x - .045, .20, -.79), Vector3(x, y, -.73), .026, .026)
		cylinder("steel", Vector3(x, y + .082, -.73), 0, .045, .17)
		box("dark", Vector3(x, y - .015, -.73), Vector3(.048, .085, .048))
	shield(Vector3(-1.055, .73, -.818), .235, "banner")
	sphere("gold", Vector3(-1.055, .735, -.847), .040)
	# Wrapped practice dummy: foot, post, straw body, helmet and wooden sword.
	cylinder("stone_dark", Vector3(1.045, .205, -.72), .20, .23, .09)
	box("wood", Vector3(1.045, .64, -.72), Vector3(.068, .81, .068))
	beam("wood", Vector3(.795, .97, -.72), Vector3(1.305, .97, -.72), .060, .065)
	cylinder("wood_light", Vector3(1.045, .91, -.72), .125, .095, .34)
	for y in [.80, .90, 1.025]:
		cylinder("gold", Vector3(1.045, y, -.72), .129, .129, .023)
	sphere("wood_light", Vector3(1.045, 1.18, -.72), .118)
	cylinder("dark", Vector3(1.045, 1.246, -.72), .07, .13, .12)
	cylinder("steel", Vector3(1.045, 1.197, -.72), .141, .141, .023)
	box("dark", Vector3(1.045, 1.195, -.837), Vector3(.018, .080, .020))
	beam("wood_light", Vector3(1.285, .76, -.72), Vector3(1.335, 1.25, -.72), .032, .037)
	box("dark", Vector3(1.31, .925, -.72), Vector3(.14, .030, .04))

func pennant() -> void:
	# A single off-centre mast gives the barracks a clear military silhouette.
	var x := -.79
	var z := .27
	box("dark", Vector3(x, 2.425, z), Vector3(.14, .075, .17))
	cylinder("wood", Vector3(x, 2.78, z), .021, .028, .75)
	sphere("gold", Vector3(x, 3.174, z), .044)
	var a := Vector3(x + .024, 3.105, z)
	var b := Vector3(x + .58, 3.125, z + .027)
	var c := Vector3(x + .49, 2.94, z - .027)
	var d := Vector3(x + .59, 2.775, z + .018)
	var e := Vector3(x + .024, 2.80, z)
	triangle("banner", a, c, b)
	triangle("banner", a, e, c)
	triangle("banner", e, d, c)
	box("gold", Vector3(x + .105, 2.955, z - .014), Vector3(.030, .282, .018))

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
	assert(bounds.position.x >= -1.501 and bounds.end.x <= 1.501, "Barracks exceeds 3-wide footprint")
	assert(bounds.position.z >= -1.001 and bounds.end.z <= 1.001, "Barracks exceeds 2-deep footprint")
	assert(bounds.position.y >= -.001, "Barracks extends below ground")
	assert(mesh.get_surface_count() <= 15 and triangles < 35000)
	assert(ResourceSaver.save(mesh, "res://buildings/meshes/Barracks.res", ResourceSaver.FLAG_COMPRESS) == OK)
	var barracks := Node3D.new()
	barracks.name = "Barracks"
	barracks.set_script(load("res://buildings/Building.cs"))
	barracks.set("BuildingType", 4)
	barracks.set("HealthBarHeight", 3.47)
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
