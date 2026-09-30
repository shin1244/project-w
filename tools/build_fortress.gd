extends "res://tools/build_town_hall.gd"
## 3x3 stone fortress and separately pivoted ballista, facing local -Z.

const TURRET_HEIGHT := 3.63

func _initialize() -> void:
	material("stone", Color("778176"))
	material("stone_light", Color("a3ac9c"))
	material("stone_dark", Color("59665e"))
	material("wood", Color("583d2d"))
	material("wood_light", Color("986c45"))
	material("dark", Color("25352f"), 0.3)
	material("gold", Color("c49a49"), 0.35)
	material("banner", Color("376a94"))
	build_masonry()
	var base_mesh := finish_mesh("res://buildings/meshes/Fortress.res")
	reset_surfaces()
	build_ballista()
	var weapon_mesh := finish_mesh("res://buildings/meshes/FortressBallista.res")
	save_tower(base_mesh, weapon_mesh)
	quit()

func reset_surfaces() -> void:
	surfaces.clear()
	for key: String in materials:
		var st := SurfaceTool.new()
		st.begin(Mesh.PRIMITIVE_TRIANGLES)
		st.set_material(materials[key])
		surfaces[key] = st

func finish_mesh(path: String, mesh_scale := 1.0) -> ArrayMesh:
	var mesh := ArrayMesh.new()
	for key: String in surfaces:
		var arrays: Array = surfaces[key].commit_to_arrays()
		if arrays[Mesh.ARRAY_VERTEX] != null and arrays[Mesh.ARRAY_VERTEX].size() > 0:
			if is_equal_approx(mesh_scale, 1.0):
				surfaces[key].commit(mesh)
			else:
				var scaled := SurfaceTool.new()
				scaled.begin(Mesh.PRIMITIVE_TRIANGLES)
				scaled.set_material(materials[key])
				scaled.append_from(surfaces[key].commit(), 0, Transform3D(Basis.from_scale(Vector3.ONE * mesh_scale), Vector3.ZERO))
				scaled.commit(mesh)
	assert(ResourceSaver.save(mesh, path, ResourceSaver.FLAG_COMPRESS) == OK)
	return load(path)

func chamfered_block(key: String, half: float, cut: float, bottom: float, height: float) -> void:
	var points := [Vector2(-half + cut, -half), Vector2(half - cut, -half),
		Vector2(half, -half + cut), Vector2(half, half - cut), Vector2(half - cut, half),
		Vector2(-half + cut, half), Vector2(-half, half - cut), Vector2(-half, -half + cut)]
	for i in range(points.size()):
		var a: Vector2 = points[i]
		var b: Vector2 = points[(i + 1) % points.size()]
		triangle(key, Vector3(0, bottom + height, 0), Vector3(b.x, bottom + height, b.y), Vector3(a.x, bottom + height, a.y))
		quad(key, Vector3(a.x, bottom, a.y), Vector3(a.x, bottom + height, a.y),
			Vector3(b.x, bottom + height, b.y), Vector3(b.x, bottom, b.y))

func build_masonry() -> void:
	chamfered_block("stone_dark", 1.5, .22, 0, .18)
	chamfered_block("stone_light", 1.43, .20, .18, .13)
	chamfered_block("stone", 1.20, .23, .31, .33)
	chamfered_block("stone_light", 1.24, .22, .64, .12)
	chamfered_block("stone_dark", .99, .18, .76, 2.16)
	# Staggered masonry on all four faces. Fine gaps expose the dark mortar core.
	for face in range(4):
		var turn := Basis(Vector3.UP, face * PI / 2)
		for row in range(7):
			var y := .92 + row * .29
			for col in range(4):
				var x := -.69 + col * .46
				var key := "stone_light" if (row * 3 + col + face) % 5 == 0 else "stone"
				box(key, turn * Vector3(x, y, -.995), Vector3(.44, .27, .09), turn)
		# Corner buttresses support a broad, readable fighting platform.
		for x in [-.93, .93]:
			box("stone", turn * Vector3(x, 1.80, -.91), Vector3(.22, 2.12, .22), turn)
			box("stone_light", turn * Vector3(x, 2.88, -.91), Vector3(.32, .14, .32), turn)
		# Narrow arrow slits on the sides and rear.
		if face != 0:
			box("stone_light", turn * Vector3(0, 2.05, -1.06), Vector3(.26, .83, .08), turn)
			box("dark", turn * Vector3(0, 2.05, -1.11), Vector3(.095, .66, .025), turn)
	chamfered_block("stone_dark", 1.22, .18, 2.92, .16)
	chamfered_block("stone_light", 1.36, .18, 3.08, .18)
	chamfered_block("wood_light", 1.17, .14, 3.26, .09)
	for i in range(9):
		box("wood", Vector3(-1.04 + i * .26, 3.356, 0), Vector3(.015, .014, 2.02))
	# Corner merlons and broken parapets leave a clear weapon silhouette.
	for face in range(4):
		var turn := Basis(Vector3.UP, face * PI / 2)
		box("stone", turn * Vector3(0, 3.47, -1.18), Vector3(2.30, .30, .24), turn)
		for x in [-.99, 0.0, .99]:
			box("stone", turn * Vector3(x, 3.75, -1.18), Vector3(.38, .32, .28), turn)
			box("stone_light", turn * Vector3(x, 3.93, -1.18), Vector3(.42, .07, .32), turn)
	# Low door, exterior hinges and two shallow entry steps.
	box("dark", Vector3(0, 1.12, -1.055), Vector3(.70, .96, .055))
	for i in range(5):
		box("wood_light", Vector3((i - 2) * .115, 1.12, -1.09), Vector3(.103, .85, .04))
	for y in [.84, 1.35]: box("dark", Vector3(0, y, -1.12), Vector3(.58, .045, .04))
	box("stone_light", Vector3(0, 1.64, -1.08), Vector3(.86, .16, .19))
	sphere("gold", Vector3(.18, 1.13, -1.15), .04)
	box("stone_light", Vector3(0, .27, -1.29), Vector3(.97, .17, .35))
	box("stone", Vector3(0, .46, -1.14), Vector3(.84, .19, .32))
	# Faction pennant, with the same banner material as the town hall.
	box("gold", Vector3(0, 2.70, -1.105), Vector3(.65, .055, .075))
	quad("banner", Vector3(-.27, 2.67, -1.125), Vector3(-.27, 2.12, -1.125),
		Vector3(.27, 2.12, -1.125), Vector3(.27, 2.67, -1.125))
	triangle("banner", Vector3(-.27, 2.12, -1.125), Vector3(0, 1.92, -1.125), Vector3(.27, 2.12, -1.125))
	box("gold", Vector3(0, 2.37, -1.14), Vector3(.05, .37, .022))
	box("gold", Vector3(0, 2.39, -1.14), Vector3(.25, .05, .022))
	cylinder("dark", Vector3(0, 3.47, 0), .41, .52, .25)
	cylinder("gold", Vector3(0, 3.62, 0), .44, .44, .06)

func build_ballista() -> void:
	# All coordinates relative to the yaw pivot. Full sweep remains within radius 1.5.
	cylinder("dark", Vector3(0, .12, 0), .28, .31, .24)
	for x in [-.22, .22]:
		box("wood", Vector3(x, .31, .08), Vector3(.13, .45, .48))
		box("gold", Vector3(x, .44, .08), Vector3(.16, .065, .51))
	box("wood_light", Vector3(0, .52, -.09), Vector3(.30, .23, 1.64))
	box("dark", Vector3(0, .65, -.13), Vector3(.065, .025, 1.58))
	for z in [-.71, .36, .64]:
		box("dark", Vector3(0, .53, z), Vector3(.33, .26, .085))
	# Swept bow arms, iron tips and a pulled string.
	for side in [-1.0, 1.0]:
		beam("wood_light", Vector3(side * .13, .54, -.53), Vector3(side * .73, .56, -.33), .13, .18)
		beam("wood", Vector3(side * .73, .56, -.33), Vector3(side * 1.19, .58, -.06), .11, .14)
		box("dark", Vector3(side * 1.19, .58, -.06), Vector3(.14, .17, .15))
		beam("gold", Vector3(side * 1.19, .59, -.06), Vector3(0, .67, .51), .022, .022)
		cylinder("dark", Vector3(side * .35, .65, -.52), .075, .09, .35)
		cylinder("gold", Vector3(side * .35, .81, -.52), .095, .095, .055)
	# Loaded bolt: shaft, steel point and rear fins.
	beam("wood_light", Vector3(0, .70, .53), Vector3(0, .70, -1.16), .045, .045)
	cylinder("dark", Vector3(0, .70, -1.27), 0, .11, .24, Basis(Vector3.RIGHT, -PI / 2))
	for side in [-1.0, 1.0]:
		triangle("gold", Vector3(0, .71, .35), Vector3(side * .14, .71, .58), Vector3(0, .71, .55))
	# Rear winding crank.
	beam("dark", Vector3(-.39, .53, .69), Vector3(.39, .53, .69), .055, .055)
	box("wood_light", Vector3(.40, .62, .69), Vector3(.08, .24, .085))

func save_tower(base_mesh: ArrayMesh, weapon_mesh: ArrayMesh, model_name := "Fortress", type := 1, size := 3, pivot_height := TURRET_HEIGHT, weapon_scale := 1.0) -> void:
	var bounds := base_mesh.get_aabb()
	var half := size * .5
	assert(bounds.position.x >= -half - .001 and bounds.end.x <= half + .001)
	assert(bounds.position.z >= -half - .001 and bounds.end.z <= half + .001)
	var height := maxf(bounds.end.y, pivot_height + weapon_mesh.get_aabb().end.y)
	for surface in range(weapon_mesh.get_surface_count()):
		for vertex: Vector3 in weapon_mesh.surface_get_arrays(surface)[Mesh.ARRAY_VERTEX]:
			assert(Vector2(vertex.x, vertex.z).length() + (.15 if type == 6 else 0.0) <= half, "Rotating weapon exceeds footprint")
	var tower := Node3D.new()
	tower.name = model_name
	tower.set_script(load("res://buildings/Building.cs"))
	tower.set("BuildingType", type)
	tower.set("HealthBarHeight", 4.9 if type == 1 else height + .30)
	tower.set_meta("footprint", Vector2i(size, size))
	tower.set_meta("front", "-Z")
	tower.set_meta("role", model_name.to_lower())
	var visual := MeshInstance3D.new()
	visual.name = "Visual"
	visual.mesh = base_mesh
	tower.add_child(visual)
	visual.owner = tower
	var turret := Node3D.new()
	turret.name = "Turret"
	turret.position.y = pivot_height
	tower.add_child(turret)
	turret.owner = tower
	var weapon := MeshInstance3D.new()
	weapon.name = "Ballista"
	weapon.mesh = weapon_mesh
	turret.add_child(weapon)
	weapon.owner = tower
	var muzzle := Marker3D.new()
	muzzle.name = "Muzzle"
	muzzle.position = Vector3(0, .70, -1.39) * weapon_scale
	turret.add_child(muzzle)
	muzzle.owner = tower
	var selection := Area3D.new()
	selection.name = "SelectionArea"
	selection.collision_layer = 8
	selection.collision_mask = 0
	selection.monitoring = false
	selection.monitorable = false
	tower.add_child(selection)
	selection.owner = tower
	var collision := CollisionShape3D.new()
	collision.name = "CollisionShape3D"
	collision.position.y = height * .5
	var shape := BoxShape3D.new()
	shape.size = Vector3(size, height, size)
	collision.shape = shape
	selection.add_child(collision)
	collision.owner = tower
	var entrance := Marker3D.new()
	entrance.name = "Entrance"
	entrance.position = Vector3(0, 0, -half)
	tower.add_child(entrance)
	entrance.owner = tower
	apply_server_footprint(tower)
	var scene := PackedScene.new()
	assert(scene.pack(tower) == OK)
	assert(ResourceSaver.save(scene, "res://buildings/%s.tscn" % model_name) == OK)
	print("PASS: ", model_name, " footprint ", tower.get_meta("footprint"), ", source height ", height, ", source base bounds ", bounds, "; rotating weapon fits footprint")
	tower.free()
