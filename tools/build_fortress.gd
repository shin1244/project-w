extends "res://tools/build_town_hall.gd"
## The fortress (요새, server type 1): a riveted octagonal bastion of the company with a pivoting
## steam cannon on top (docs/art-direction.md). Iron glacis over a concrete dock, an iron-banded
## brick shaft with gun slits and a gilt shield, a company-painted parapet coping, and a boiler
## exhaust that smokes. The watchtower builder reuses the cannon (build_steam_gun) at a smaller scale.
## Source grid 3x3, scaled to the server's 4x4; local front -Z.

const TURRET_HEIGHT := 3.63
const MUZZLE := Vector3(0, 0.5, -1.39)

func _initialize() -> void:
	defence_materials()
	build_bastion()
	var base_mesh := finish_mesh("res://buildings/meshes/Fortress.res")
	reset_surfaces()
	build_steam_gun()
	var weapon_mesh := finish_mesh("res://buildings/meshes/FortressBallista.res")
	save_tower(base_mesh, weapon_mesh)
	add_exhaust_smoke("res://buildings/Fortress.tscn", Vector3(0.95, 4.35, 0.75))
	quit()

func defence_materials() -> void:
	material("concrete", Color("858179"))
	material("concrete_dark", Color("5c5953"))
	material("brick", Color("6e3f33"))
	material("brick_dark", Color("4d2c24"))
	material("iron", Color("2f363d"), 0.7, 0.32) # Lacquered war iron: weapons shine.
	material("iron_light", Color("8f979c"), 0.7, 0.3)
	material("brass", Color("a88349"), 0.75, 0.3)
	material("gold", Color("d9ae45"), 0.85, 0.22)
	material("copper", Color("b0673e"), 0.65, 0.35)
	material("soot", Color("1e2226"))
	material("lamp", Color("fff1c8"), 0.0, 0.3, 2.4)
	material("roof", Color("3d5f77"), 0.15, 0.7) # Company-painted plate.
	material("banner", Color("376a94"))

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

func octagon(half: float, cut: float) -> Array:
	return [Vector2(-half + cut, -half), Vector2(half - cut, -half), Vector2(half, -half + cut), Vector2(half, half - cut),
		Vector2(half - cut, half), Vector2(-half + cut, half), Vector2(-half, half - cut), Vector2(-half, -half + cut)]

func chamfered_block(key: String, half: float, cut: float, bottom: float, height: float) -> void:
	chamfered_frustum(key, half, half, cut / half, bottom, height)

func chamfered_frustum(key: String, half_bottom: float, half_top: float, cut_ratio: float, bottom: float, height: float) -> void:
	var lo := octagon(half_bottom, half_bottom * cut_ratio)
	var hi := octagon(half_top, half_top * cut_ratio)
	for i in range(8):
		var a: Vector2 = lo[i]
		var b: Vector2 = lo[(i + 1) % 8]
		var c: Vector2 = hi[(i + 1) % 8]
		var d: Vector2 = hi[i]
		face(key, Vector3(a.x, bottom, a.y), Vector3(b.x, bottom, b.y), Vector3(c.x, bottom + height, c.y), Vector3(d.x, bottom + height, d.y))
		face(key, Vector3(0, bottom + height, 0), Vector3(d.x, bottom + height, d.y), Vector3(c.x, bottom + height, c.y), Vector3(0, bottom + height, 0))

# Midpoint and outward direction of octagon side i.
func side_frame(half: float, cut: float, i: int) -> Array:
	var pts := octagon(half, cut)
	var a: Vector2 = pts[i]
	var b: Vector2 = pts[(i + 1) % 8]
	var mid := (a + b) * 0.5
	var along := (b - a).normalized()
	return [Vector3(mid.x, 0, mid.y), Vector3(along.y, 0, -along.x), (b - a).length(), atan2(-(b - a).y, (b - a).x)]

func build_bastion() -> void:
	chamfered_block("concrete_dark", 1.5, 0.32, 0.0, 0.14)
	chamfered_block("concrete", 1.4, 0.3, 0.14, 0.08)
	# Sloped iron glacis round the foot, riveted in two rows.
	chamfered_frustum("iron", 1.3, 1.08, 0.22, 0.22, 0.8)
	for i in range(8):
		var f := side_frame(1.19, 1.19 * 0.22, i)
		for row in [0.42, 0.78]:
			for k in range(-2, 3):
				var p: Vector3 = f[0] + Vector3(cos(f[3]), 0, -sin(f[3])) * (k * f[2] * 0.18) + Vector3(0, row, 0)
				sphere("iron_light", p + f[1] * (0.02 + (0.78 - row) * 0.22), 0.022)
	# Brick shaft with iron bands.
	chamfered_block("brick", 1.02, 0.24, 1.02, 1.88)
	for y in [1.02, 1.96, 2.86]:
		chamfered_block("iron", 1.06, 0.25, y - 0.04, 0.08)
	chamfered_block("soot", 1.025, 0.24, 2.6, 0.26)
	# Gun slits on each face, two storeys.
	for i in range(8):
		var f := side_frame(1.02, 0.24, i)
		for y in [1.55, 2.4]:
			box("soot", f[0] + f[1] * 0.006 + Vector3(0, y, 0), Vector3(0.05, 0.22, 0.02), Basis(Vector3.UP, f[3]))
	# Front blast door in an iron porch with gilt trim.
	box("iron", Vector3(0, 0.6, -1.2), Vector3(0.6, 0.78, 0.36))
	box("soot", Vector3(0, 0.55, -1.385), Vector3(0.36, 0.6, 0.012))
	box("gold", Vector3(0, 0.88, -1.39), Vector3(0.42, 0.04, 0.02))
	for x in [-0.12, 0.12]:
		for y in [0.38, 0.6]:
			sphere("brass", Vector3(x, y, -1.395), 0.018)
	box("concrete", Vector3(0, 0.26, -1.42), Vector3(0.62, 0.06, 0.14))
	# Gilt shield with the company colour on the front of the shaft.
	var shield := Vector3(0, 2.3, -1.03)
	box("gold", shield, Vector3(0.46, 0.36, 0.04))
	face("gold", shield + Vector3(-0.23, -0.18, -0.02), shield + Vector3(0.23, -0.18, -0.02), shield + Vector3(0, -0.42, -0.02), shield + Vector3(-0.23, -0.18, -0.02))
	box("banner", shield + Vector3(0, 0.02, -0.026), Vector3(0.36, 0.28, 0.01))
	face("banner", shield + Vector3(-0.18, -0.12, -0.03), shield + Vector3(0.18, -0.12, -0.03), shield + Vector3(0, -0.33, -0.03), shield + Vector3(-0.18, -0.12, -0.03))
	box("gold", shield + Vector3(0, 0.03, -0.034), Vector3(0.05, 0.2, 0.006))
	# Company banners hang down both flanks.
	for side in [-1.0, 1.0]:
		var x: float = side * 1.035
		box("gold", Vector3(x, 2.8, 0), Vector3(0.03, 0.04, 0.5))
		face("banner", Vector3(x + side * 0.005, 2.78, -0.22), Vector3(x + side * 0.005, 2.78, 0.22), Vector3(x + side * 0.005, 2.0, 0.22), Vector3(x + side * 0.005, 2.0, -0.22))
		face("banner", Vector3(x + side * 0.005, 2.0, -0.22), Vector3(x + side * 0.005, 2.0, 0.22), Vector3(x + side * 0.005, 1.84, 0.0), Vector3(x + side * 0.005, 2.0, -0.22))
	# Gun platform with a riveted parapet; its coping is painted in the company colour.
	chamfered_block("concrete", 1.12, 0.26, 2.9, 0.14)
	for i in range(8):
		var f := side_frame(1.07, 0.25, i)
		box("iron", f[0] + Vector3(0, 3.27, 0), Vector3(f[2] + 0.06, 0.46, 0.09), Basis(Vector3.UP, f[3]))
		box("roof", f[0] + Vector3(0, 3.52, 0), Vector3(f[2] + 0.1, 0.05, 0.16), Basis(Vector3.UP, f[3]))
		for k in [-0.3, 0.0, 0.3]:
			var p: Vector3 = f[0] + Vector3(cos(f[3]), 0, -sin(f[3])) * (k * f[2]) + f[1] * 0.05
			sphere("iron_light", p + Vector3(0, 3.27, 0), 0.02)
	# Boiler exhaust up the back corner for the cannon's steam.
	cylinder("iron", Vector3(0.95, 2.2, 0.75), 0.09, 0.1, 4.2 - 0.06, Basis.IDENTITY, 10)
	for y in [1.4, 2.4, 3.4]:
		cylinder("brass", Vector3(0.95, y, 0.75), 0.11, 0.11, 0.04, Basis.IDENTITY, 10)
	cylinder("soot", Vector3(0.95, 4.27, 0.75), 0.1, 0.1, 0.12, Basis.IDENTITY, 10)

func build_steam_gun() -> void:
	# Relative to the yaw pivot; the full sweep stays inside radius 1.5 (0.55 scale on the watchtower).
	cylinder("iron", Vector3(0, 0.07, 0), 0.34, 0.36, 0.14, Basis.IDENTITY, 16)
	cylinder("brass", Vector3(0, 0.15, 0), 0.36, 0.36, 0.03, Basis.IDENTITY, 16)
	# Riveted mantlet housing with a sloped front plate.
	box("iron", Vector3(0, 0.42, 0.12), Vector3(0.62, 0.5, 0.62))
	face("iron", Vector3(-0.31, 0.17, -0.19), Vector3(0.31, 0.17, -0.19), Vector3(0.31, 0.62, -0.08), Vector3(-0.31, 0.62, -0.08))
	box("roof", Vector3(0, 0.68, 0.12), Vector3(0.6, 0.03, 0.6)) # Painted roof plate.
	for x in [-0.32, 0.32]:
		box("banner", Vector3(x, 0.42, 0.16), Vector3(0.01, 0.22, 0.3))
		for z in [-0.05, 0.12, 0.3]:
			sphere("iron_light", Vector3(x, 0.6, z), 0.018)
	box("gold", Vector3(0, 0.66, -0.12), Vector3(0.5, 0.03, 0.03))
	# The barrel, banded in brass, with a slotted muzzle brake at MUZZLE.
	cylinder("iron", Vector3(0, 0.5, -0.75), 0.075, 0.09, 1.1, Basis(Vector3.RIGHT, PI * 0.5), 14)
	for z in [-0.42, -0.78, -1.08]:
		cylinder("brass", Vector3(0, 0.5, z), 0.1, 0.1, 0.04, Basis(Vector3.RIGHT, PI * 0.5), 14)
	cylinder("iron", Vector3(0, 0.5, -1.33), 0.1, 0.1, 0.12, Basis(Vector3.RIGHT, PI * 0.5), 14)
	box("soot", Vector3(0, 0.5, -1.33), Vector3(0.22, 0.04, 0.05))
	# Recoil cylinders above the barrel.
	for x in [-0.12, 0.12]:
		cylinder("brass", Vector3(x, 0.66, -0.42), 0.035, 0.035, 0.5, Basis(Vector3.RIGHT, PI * 0.5), 10)
	# Boiler drum behind with a gauge and a steam line to the breech.
	cylinder("copper", Vector3(0, 0.42, 0.62), 0.19, 0.19, 0.54, Basis(Vector3.FORWARD, PI * 0.5), 14)
	for x in [-0.27, 0.27]:
		cylinder("brass", Vector3(x, 0.42, 0.62), 0.2, 0.2, 0.03, Basis(Vector3.FORWARD, PI * 0.5), 14)
	cylinder("brass", Vector3(0.12, 0.64, 0.62), 0.07, 0.07, 0.03, Basis.IDENTITY, 12)
	cylinder("lamp", Vector3(0.12, 0.656, 0.62), 0.05, 0.05, 0.004, Basis.IDENTITY, 12)
	beam("copper", Vector3(-0.12, 0.6, 0.55), Vector3(-0.12, 0.6, 0.3), 0.04, 0.04)

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
	weapon.name = "Ballista" # Building.cs recoils this node; the name is kept for it.
	weapon.mesh = weapon_mesh
	turret.add_child(weapon)
	weapon.owner = tower
	var muzzle := Marker3D.new()
	muzzle.name = "Muzzle"
	muzzle.position = MUZZLE * weapon_scale
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

func add_exhaust_smoke(scene_path: String, at: Vector3) -> void:
	var scene: PackedScene = load(scene_path)
	var root := scene.instantiate()
	var visual := root.get_node("Visual") as Node3D
	var stack := smoke("Smoke", at)
	stack.amount = 8
	stack.scale_amount_min = 0.35
	stack.scale_amount_max = 0.55
	visual.add_child(stack)
	stack.owner = root
	var packed := PackedScene.new()
	assert(packed.pack(root) == OK)
	assert(ResourceSaver.save(packed, scene_path) == OK)
	root.free()
