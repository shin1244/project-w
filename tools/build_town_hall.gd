extends SceneTree
## Builds the town hall: a soot-stained brick factory hall of a noble house (docs/art-direction.md).
## A gilded clock tower over the gate with brass gears turning on its flanks (SpinningPart), and two
## great smoking chimneys over the rear boiler house.
## Modelled on a 3x3 grid, baked by BAKE into a 4x4 mesh, then scaled to the server's 5x5 footprint.

const OUTPUT := "res://buildings/TownHall.tscn"
const BAKE := Vector3(4.0 / 3.0, 1.1, 4.0 / 3.0)
const SERVER_FOOTPRINTS := {
	0: Vector2i(5, 5),
	1: Vector2i(4, 4),
	2: Vector2i(4, 4),
	3: Vector2i(3, 3),
	4: Vector2i(4, 3),
	5: Vector2i(4, 3),
	6: Vector2i(3, 3),
}
var surfaces: Dictionary = {}
var materials: Dictionary = {}
var gears: Array = [] # [name, radius, teeth, position (baked), basis, speed]

func _initialize() -> void:
	# Industrial masonry is matte; the noble trim (gold) and gear brass are polished.
	material("brick", Color("8a4a3a"))
	material("brick_dark", Color("6b382d"))
	material("brick_light", Color("a35b46"))
	material("stone", Color("6f6a63"))
	material("stone_dark", Color("4c4844"))
	material("iron", Color("2f363d"), 0.6, 0.4)
	material("iron_light", Color("8f979c"), 0.65, 0.35)
	material("brass", Color("a88349"), 0.7, 0.32)
	material("gold", Color("d9ae45"), 0.85, 0.22)
	material("copper", Color("b0673e"), 0.6, 0.38)
	material("soot", Color("1e2226"))
	material("ivory", Color("e9dcc0"))
	material("glass", Color("ffd37a"), 0.0, 0.3, 1.4)
	material("roof", Color("3d5f77"), 0.35, 0.55)
	material("roof_mid", Color("37576e"), 0.35, 0.55)
	material("roof_light", Color("466b84"), 0.35, 0.55)
	material("banner", Color("376a94"))
	build_hall()
	save_hall()
	quit()

func material(key: String, color: Color, metallic := 0.0, roughness := 0.85, glow := 0.0) -> void:
	var mat := StandardMaterial3D.new()
	mat.resource_name = key
	mat.albedo_color = color
	mat.metallic = metallic
	mat.roughness = roughness
	if glow > 0.0: # Gaslight windows and lanterns.
		mat.emission_enabled = true
		mat.emission = color
		mat.emission_energy_multiplier = glow
	if key == "banner":
		mat.cull_mode = BaseMaterial3D.CULL_DISABLED
	materials[key] = mat
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	st.set_material(mat)
	surfaces[key] = st

func append_mesh(key: String, mesh: Mesh, transform: Transform3D) -> void:
	var part := SurfaceTool.new()
	part.create_from(mesh, 0)
	part.deindex()
	surfaces[key].append_from(part.commit(), 0, transform)

func box(key: String, center: Vector3, size: Vector3, basis := Basis.IDENTITY) -> void:
	var mesh := BoxMesh.new()
	mesh.size = size
	append_mesh(key, mesh, Transform3D(basis, center))

func cylinder(key: String, center: Vector3, top: float, bottom: float, height: float, basis := Basis.IDENTITY, sides := 14) -> void:
	var mesh := CylinderMesh.new()
	mesh.top_radius = top
	mesh.bottom_radius = bottom
	mesh.height = height
	mesh.radial_segments = sides
	append_mesh(key, mesh, Transform3D(basis, center))

func sphere(key: String, center: Vector3, radius: float, height_scale := 1.0) -> void:
	var mesh := SphereMesh.new()
	mesh.radius = radius
	mesh.height = radius * 2.0
	mesh.radial_segments = 14
	mesh.rings = 7
	append_mesh(key, mesh, Transform3D(Basis.from_scale(Vector3(1, height_scale, 1)), center))

func face(key: String, a: Vector3, b: Vector3, c: Vector3, d: Vector3) -> void:
	# Hand-made slopes are emitted on both sides, so their winding can never hide them.
	for tri in [[a, b, c], [a, c, d], [a, c, b], [a, d, c]]:
		var normal: Vector3 = (tri[1] - tri[0]).cross(tri[2] - tri[0]).normalized()
		for point in [tri[0], tri[2], tri[1]]:
			surfaces[key].set_normal(normal)
			surfaces[key].set_uv(Vector2.ZERO)
			surfaces[key].add_vertex(point)

func beam(key: String, a: Vector3, b: Vector3, width: float, depth: float) -> void:
	box(key, (a + b) * 0.5, Vector3(width, a.distance_to(b), depth), Basis(Quaternion(Vector3.UP, (b - a).normalized())))

# ── Hall ──

func build_hall() -> void:
	foundation()
	main_hall()
	mansard_roof()
	gate()
	boiler_house()
	for side in [-1.0, 1.0]:
		chimney(side)
	clock_tower()

func foundation() -> void:
	# A dark stone dock the size of the footprint, chamfered at the corners.
	var outline := [Vector2(-1.38, -1.5), Vector2(1.38, -1.5), Vector2(1.5, -1.38), Vector2(1.5, 1.38),
		Vector2(1.38, 1.5), Vector2(-1.38, 1.5), Vector2(-1.5, 1.38), Vector2(-1.5, -1.38)]
	for i in range(outline.size()):
		var a: Vector2 = outline[i]
		var b: Vector2 = outline[(i + 1) % outline.size()]
		face("stone", Vector3(0, 0.16, 0), Vector3(a.x, 0.16, a.y), Vector3(b.x, 0.16, b.y), Vector3(0, 0.16, 0))
		face("stone_dark", Vector3(a.x, 0, a.y), Vector3(a.x, 0.16, a.y), Vector3(b.x, 0.16, b.y), Vector3(b.x, 0, b.y))
	box("stone_dark", Vector3(0, 0.22, -0.1), Vector3(2.5, 0.12, 2.0))
	# Steps up to the gate.
	for i in range(3):
		box("stone", Vector3(0, 0.05 + i * 0.06, -1.32 + i * 0.1), Vector3(1.0 - i * 0.12, 0.06, 0.24))

func main_hall() -> void:
	var lo := 0.28
	var hi := 1.74
	box("brick", Vector3(0, (lo + hi) * 0.5, -0.15), Vector3(2.3, hi - lo, 1.7))
	# Darker brick courses read as layered masonry; soot gathers under the cornice.
	for i in range(5):
		var y := lo + 0.22 + i * 0.27
		box("brick_dark", Vector3(0, y, -1.004), Vector3(2.3, 0.035, 0.012))
		for side in [-1.0, 1.0]:
			box("brick_dark", Vector3(side * 1.154, y, -0.15), Vector3(0.012, 0.035, 1.7))
	box("soot", Vector3(0, hi - 0.08, -0.15), Vector3(2.32, 0.16, 1.72))
	box("iron", Vector3(0, hi + 0.02, -0.15), Vector3(2.44, 0.07, 1.84))
	box("brass", Vector3(0, hi - 0.03, -1.07), Vector3(2.38, 0.035, 0.03))
	# Iron pilasters with rivets at the corners.
	for x in [-1.13, 1.13]:
		for z in [-0.98, 0.68]:
			box("iron", Vector3(x, (lo + hi) * 0.5, z), Vector3(0.14, hi - lo, 0.14))
			for i in range(5):
				sphere("iron_light", Vector3(x + signf(x) * 0.072, lo + 0.18 + i * 0.28, z), 0.018)
	# Tall arched windows down both sides, lit from inside.
	for side in [-1.0, 1.0]:
		for z in [-0.55, 0.0, 0.45]:
			arched_window(Vector3(side * 1.152, 0.95, z), side)

func arched_window(center: Vector3, side: float) -> void:
	var outward := Vector3(side, 0, 0)
	box("iron", center + outward * 0.012, Vector3(0.03, 0.62, 0.3))
	box("glass", center + outward * 0.03, Vector3(0.012, 0.5, 0.2))
	cylinder("glass", center + Vector3(0, 0.25, 0) + outward * 0.03, 0.1, 0.1, 0.012, Basis(Vector3.FORWARD, PI * 0.5), 10)
	cylinder("iron", center + Vector3(0, 0.25, 0) + outward * 0.018, 0.15, 0.15, 0.024, Basis(Vector3.FORWARD, PI * 0.5), 10)
	box("iron_light", center + outward * 0.04, Vector3(0.01, 0.5, 0.018))
	box("stone", center + Vector3(0, -0.33, 0) + outward * 0.03, Vector3(0.07, 0.05, 0.36))

func mansard_roof() -> void:
	# Painted iron (team colour) mansard over the hall, iron cresting along the top.
	var y0 := 1.78
	var y1 := 2.18
	var b := [Vector3(-1.22, y0, -1.06), Vector3(1.22, y0, -1.06), Vector3(1.22, y0, 0.76), Vector3(-1.22, y0, 0.76)]
	var t := [Vector3(-0.86, y1, -0.74), Vector3(0.86, y1, -0.74), Vector3(0.86, y1, 0.44), Vector3(-0.86, y1, 0.44)]
	var keys := ["roof", "roof_mid", "roof", "roof_light"]
	for i in range(4):
		face(keys[i], b[i], b[(i + 1) % 4], t[(i + 1) % 4], t[i])
	face("iron", t[0], t[1], t[2], t[3])
	for i in range(4): # Seams between the painted plates.
		beam("iron", b[i], t[i], 0.035, 0.035)
	for i in range(9):
		var x := -0.8 + i * 0.2
		box("iron", Vector3(x, y1 + 0.07, -0.74), Vector3(0.02, 0.14, 0.02))
		box("iron", Vector3(x, y1 + 0.07, 0.44), Vector3(0.02, 0.14, 0.02))
	box("iron", Vector3(0, y1 + 0.14, -0.74), Vector3(1.72, 0.02, 0.02))
	box("iron", Vector3(0, y1 + 0.14, 0.44), Vector3(1.72, 0.02, 0.02))

func gate() -> void:
	var z := -1.0
	# Riveted iron arch and double door.
	box("iron", Vector3(0, 0.68, z - 0.04), Vector3(0.74, 0.8, 0.08))
	cylinder("iron", Vector3(0, 1.08, z - 0.04), 0.37, 0.37, 0.08, Basis(Vector3.RIGHT, PI * 0.5), 16)
	box("soot", Vector3(0, 0.66, z - 0.085), Vector3(0.56, 0.72, 0.02))
	cylinder("soot", Vector3(0, 1.02, z - 0.085), 0.28, 0.28, 0.02, Basis(Vector3.RIGHT, PI * 0.5), 16)
	box("iron_light", Vector3(0, 0.75, z - 0.1), Vector3(0.012, 0.86, 0.012))
	for x in [-0.14, 0.14]:
		for y in [0.45, 0.68, 0.91]:
			sphere("brass", Vector3(x, y, z - 0.1), 0.018)
	# Gilded crest of the house above the gate, team colour in its field.
	cylinder("gold", Vector3(0, 1.38, z - 0.06), 0.17, 0.17, 0.05, Basis(Vector3.RIGHT, PI * 0.5), 16)
	cylinder("banner", Vector3(0, 1.38, z - 0.09), 0.12, 0.12, 0.02, Basis(Vector3.RIGHT, PI * 0.5), 16)
	for i in range(8):
		var a := TAU * i / 8.0
		box("gold", Vector3(cos(a) * 0.18, 1.38 + sin(a) * 0.18, z - 0.06), Vector3(0.06, 0.06, 0.05))
	# A small gilt crown over the house colour: nobility, not faith.
	box("gold", Vector3(0, 1.35, z - 0.1), Vector3(0.13, 0.035, 0.012))
	for x in [-0.05, 0.0, 0.05]:
		box("gold", Vector3(x, 1.4 if x == 0.0 else 1.39, z - 0.1), Vector3(0.025, 0.08 if x == 0.0 else 0.06, 0.012))
		sphere("gold", Vector3(x, 1.445 if x == 0.0 else 1.425, z - 0.1), 0.013)
	# Propaganda posters in the house colours, a gilt bar on top.
	for x in [-0.4, 0.4]:
		box("banner", Vector3(x, 0.72, z - 0.012), Vector3(0.22, 0.5, 0.012))
		box("ivory", Vector3(x, 0.8, z - 0.02), Vector3(0.12, 0.12, 0.006))
		box("gold", Vector3(x, 0.98, z - 0.02), Vector3(0.25, 0.03, 0.025))
	# Gas lamps on iron posts at the foot of the steps.
	for x in [-0.62, 0.62]:
		cylinder("iron", Vector3(x, 0.55, -1.38), 0.025, 0.035, 0.8)
		box("iron", Vector3(x, 0.98, -1.38), Vector3(0.13, 0.03, 0.13))
		box("glass", Vector3(x, 1.06, -1.38), Vector3(0.09, 0.13, 0.09))
		cylinder("iron", Vector3(x, 1.15, -1.38), 0.0, 0.08, 0.07, Basis.IDENTITY, 4)

func boiler_house() -> void:
	# Riveted iron annex behind the hall feeding both chimneys.
	box("iron", Vector3(0, 0.9, 1.05), Vector3(2.36, 1.24, 0.74))
	box("soot", Vector3(0, 1.48, 1.05), Vector3(2.38, 0.08, 0.76))
	for i in range(7):
		var x := -1.0 + i * 0.333
		box("iron_light", Vector3(x, 0.9, 1.425), Vector3(0.03, 1.2, 0.012))
	for side in [-1.0, 1.0]:
		cylinder("copper", Vector3(side * 0.35, 1.62, 0.95), 0.06, 0.06, 0.4, Basis(Vector3.FORWARD, PI * 0.5))
		cylinder("brass", Vector3(side * 0.12, 1.62, 0.95), 0.08, 0.08, 0.05, Basis(Vector3.FORWARD, PI * 0.5))
	# Coal heap and barrels by the boiler doors.
	sphere("soot", Vector3(-0.9, 0.3, 1.24), 0.18, 0.55)
	for x in [0.85, 1.02]:
		cylinder("iron", Vector3(x, 0.38, 1.33), 0.08, 0.08, 0.2)

func chimney(side: float) -> void:
	var x := side * 0.72
	var z := 1.05
	cylinder("brick", Vector3(x, 2.95, z), 0.24, 0.31, 2.9, Basis.IDENTITY, 16)
	cylinder("brick_dark", Vector3(x, 1.6, z), 0.34, 0.36, 0.25, Basis.IDENTITY, 16)
	for y in [2.1, 2.9, 3.7]: # Iron hoops.
		var r := lerpf(0.31, 0.24, (y - 1.5) / 2.9) + 0.02
		cylinder("iron", Vector3(x, y, z), r, r, 0.07, Basis.IDENTITY, 16)
	# Soot climbs the top of the stack.
	cylinder("soot", Vector3(x, 4.22, z), 0.245, 0.25, 0.36, Basis.IDENTITY, 16)
	cylinder("iron", Vector3(x, 4.42, z), 0.29, 0.29, 0.07, Basis.IDENTITY, 16)
	cylinder("soot", Vector3(x, 4.45, z), 0.2, 0.2, 0.02, Basis.IDENTITY, 16)

func clock_tower() -> void:
	var z := -0.35
	var top := 3.12
	box("brick", Vector3(0, (1.74 + top) * 0.5, z), Vector3(0.64, top - 1.74, 0.64))
	for x in [-0.32, 0.32]:
		for dz in [-0.32, 0.32]:
			box("iron", Vector3(x, (1.74 + top) * 0.5, z + dz), Vector3(0.07, top - 1.74, 0.07))
	box("gold", Vector3(0, top, z), Vector3(0.74, 0.07, 0.74))
	box("soot", Vector3(0, 2.0, z), Vector3(0.66, 0.1, 0.66))
	# Gilded clock faces on all four sides, hands at ten past ten.
	for i in range(4):
		var basis := Basis(Vector3.UP, i * PI * 0.5)
		var out := basis * Vector3(0, 0, -1)
		var face_center := Vector3(0, 2.78, z) + out * 0.33
		var disc := basis * Basis(Vector3.RIGHT, PI * 0.5)
		cylinder("gold", face_center, 0.25, 0.25, 0.04, disc, 20)
		cylinder("ivory", face_center + out * 0.022, 0.2, 0.2, 0.01, disc, 20)
		for h in range(12):
			var a := TAU * h / 12.0
			box("iron", face_center + out * 0.03 + basis * Vector3(cos(a) * 0.17, sin(a) * 0.17, 0), Vector3(0.022, 0.022, 0.006), basis)
		box("iron", face_center + out * 0.034 + basis * Vector3(-0.05, 0.035, 0), Vector3(0.025, 0.13, 0.008), basis * Basis(Vector3.FORWARD, 0.95))
		box("iron", face_center + out * 0.034 + basis * Vector3(0.06, 0.045, 0), Vector3(0.022, 0.16, 0.008), basis * Basis(Vector3.FORWARD, -0.9))
		sphere("gold", face_center + out * 0.04, 0.02)
	# Gilded onion dome and spire with the house pennant.
	sphere("gold", Vector3(0, top + 0.22, z), 0.3, 1.0)
	cylinder("gold", Vector3(0, top + 0.6, z), 0.02, 0.07, 0.4)
	sphere("gold", Vector3(0, top + 0.84, z), 0.05)
	var a := Vector3(0.02, top + 0.8, z)
	face("banner", a, Vector3(0.55, top + 0.76, z), Vector3(0.48, top + 0.62, z), Vector3(0.02, top + 0.64, z))
	# Gear axles out of both tower sides (the gears themselves turn: tower_gear nodes).
	for side in [-1.0, 1.0]:
		cylinder("iron", Vector3(side * 0.34, 2.3, z), 0.05, 0.05, 0.08, Basis(Vector3.FORWARD, PI * 0.5))
		var center := Vector3(side * 0.37, 2.3, z) * BAKE
		gears.append(["TowerGear" + ("Left" if side < 0 else "Right"), 0.27, 14, center,
			Basis(Vector3.FORWARD, PI * 0.5), 0.55 * side])

# ── Gears (separate meshes so they can turn) ──

func gear_mesh(radius: float, teeth: int) -> ArrayMesh:
	var saved := surfaces
	surfaces = {}
	for key in ["brass", "iron", "gold"]:
		var st := SurfaceTool.new()
		st.begin(Mesh.PRIMITIVE_TRIANGLES)
		st.set_material(materials[key])
		surfaces[key] = st
	var thickness := 0.07
	var root := radius - 0.05
	cylinder("brass", Vector3.ZERO, root, root, thickness, Basis.IDENTITY, teeth * 2)
	cylinder("iron", Vector3.ZERO, root * 0.74, root * 0.74, thickness + 0.012, Basis.IDENTITY, teeth * 2)
	for i in range(teeth):
		var a := TAU * i / teeth
		box("brass", Vector3(cos(a), 0, sin(a)) * (root + 0.03), Vector3(0.07, thickness, 0.065), Basis(Vector3.UP, -a))
	for i in range(5): # Spokes over the dark web.
		var a := TAU * i / 5.0
		box("brass", Vector3(cos(a), 0, sin(a)) * root * 0.4, Vector3(root * 0.75, thickness + 0.02, 0.05), Basis(Vector3.UP, -a))
	cylinder("gold", Vector3.ZERO, radius * 0.18, radius * 0.18, thickness + 0.04, Basis.IDENTITY, 10)
	var mesh := ArrayMesh.new()
	for key in surfaces:
		surfaces[key].commit(mesh)
	surfaces = saved
	return mesh

func smoke(name: String, at: Vector3) -> CPUParticles3D:
	# Murky smoke from the stacks: thick near the top, thinning as it drifts.
	var particles := CPUParticles3D.new()
	particles.name = name
	particles.position = at
	particles.amount = 14
	particles.lifetime = 3.6
	particles.preprocess = 3.6
	particles.direction = Vector3(0, 1, 0)
	particles.spread = 10.0
	particles.gravity = Vector3(0.18, 0.22, 0.05)
	particles.initial_velocity_min = 0.45
	particles.initial_velocity_max = 0.7
	particles.scale_amount_min = 0.55
	particles.scale_amount_max = 0.8
	var growth := Curve.new()
	growth.add_point(Vector2(0, 0.45))
	growth.add_point(Vector2(1, 1.6))
	particles.scale_amount_curve = growth
	var ramp := Gradient.new()
	ramp.set_color(0, Color(0.27, 0.25, 0.24, 0.85))
	ramp.add_point(0.35, Color(0.36, 0.34, 0.32, 0.6))
	ramp.set_color(ramp.get_point_count() - 1, Color(0.48, 0.46, 0.44, 0.0))
	particles.color_ramp = ramp
	var quad := QuadMesh.new()
	quad.size = Vector2(0.9, 0.9)
	var mat := StandardMaterial3D.new()
	mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	mat.billboard_mode = BaseMaterial3D.BILLBOARD_PARTICLES
	mat.vertex_color_use_as_albedo = true
	var puff := GradientTexture2D.new() # A round, soft-edged puff rather than a square card.
	puff.fill = GradientTexture2D.FILL_RADIAL
	puff.fill_from = Vector2(0.5, 0.5)
	puff.fill_to = Vector2(0.5, 0.0)
	var falloff := Gradient.new()
	falloff.set_color(0, Color(1, 1, 1, 1))
	falloff.set_color(1, Color(1, 1, 1, 0))
	puff.gradient = falloff
	mat.albedo_texture = puff
	mat.roughness = 1.0
	mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	quad.material = mat
	particles.mesh = quad
	particles.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	return particles

# ── Save ──

func save_hall() -> void:
	DirAccess.make_dir_recursive_absolute("res://buildings/meshes")
	var mesh := ArrayMesh.new()
	for key: String in surfaces:
		surfaces[key].commit(mesh)
	var enlarged := ArrayMesh.new()
	for surface in range(mesh.get_surface_count()):
		var part := SurfaceTool.new()
		part.begin(Mesh.PRIMITIVE_TRIANGLES)
		part.set_material(mesh.surface_get_material(surface))
		part.append_from(mesh, surface, Transform3D(Basis.from_scale(BAKE), Vector3.ZERO))
		part.commit(enlarged)
	mesh = enlarged
	var bounds := mesh.get_aabb()
	assert(bounds.position.x >= -2.001 and bounds.end.x <= 2.001)
	assert(bounds.position.z >= -2.001 and bounds.end.z <= 2.001)
	assert(bounds.position.y >= -0.001)
	assert(ResourceSaver.save(mesh, "res://buildings/meshes/TownHall.res", ResourceSaver.FLAG_COMPRESS) == OK)
	var hall := Node3D.new()
	hall.set_script(load("res://buildings/Building.cs"))
	hall.name = "TownHall"
	hall.set("HealthBarHeight", bounds.end.y + 0.35)
	hall.set("ShotWidth", 0.18)
	hall.set("ShotDuration", 0.2)
	hall.set("ImpactRadius", 0.35)
	hall.set_meta("footprint", Vector2i(4, 4))
	hall.set_meta("front", "-Z")
	var visual := MeshInstance3D.new()
	visual.name = "Visual"
	visual.mesh = load("res://buildings/meshes/TownHall.res")
	hall.add_child(visual)
	visual.owner = hall
	var spinner := load("res://visuals/SpinningPart.cs")
	var gear_meshes := {}
	for spec in gears:
		var key := "%.2f_%d" % [spec[1], spec[2]]
		if not gear_meshes.has(key):
			var path := "res://buildings/meshes/TownHallGear_%d.res" % gear_meshes.size()
			assert(ResourceSaver.save(gear_mesh(spec[1], spec[2]), path, ResourceSaver.FLAG_COMPRESS) == OK)
			gear_meshes[key] = load(path)
		var gear := MeshInstance3D.new()
		gear.name = spec[0]
		gear.mesh = gear_meshes[key]
		gear.transform = Transform3D(spec[4], spec[3])
		gear.set_script(spinner)
		gear.set("Speed", spec[5])
		visual.add_child(gear)
		gear.owner = hall
	for side in [-1.0, 1.0]:
		var stack := smoke("Smoke" + ("Left" if side < 0 else "Right"), Vector3(side * 0.72, 4.5, 1.05) * BAKE)
		visual.add_child(stack)
		stack.owner = hall
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
	collision.position.y = bounds.end.y * .5
	var shape := BoxShape3D.new()
	shape.size = Vector3(4, bounds.end.y, 4)
	collision.shape = shape
	selection.add_child(collision)
	collision.owner = hall
	var entrance_marker := Marker3D.new()
	entrance_marker.name = "Entrance"
	entrance_marker.position = Vector3(0, 0, -2)
	hall.add_child(entrance_marker)
	entrance_marker.owner = hall
	apply_server_footprint(hall)
	# The hall fires from the clock tower top (after the footprint scale, in root space).
	var muzzle := Marker3D.new()
	muzzle.name = "Muzzle"
	muzzle.position = Vector3(0, 3.3, -0.35) * BAKE * (visual.scale.x)
	hall.add_child(muzzle)
	muzzle.owner = hall
	var scene := PackedScene.new()
	assert(scene.pack(hall) == OK)
	assert(ResourceSaver.save(scene, OUTPUT) == OK)
	print("PASS: TownHall footprint ", hall.get_meta("footprint"), ", source mesh bounds ", bounds, ", gears ", gears.size())
	hall.free()

func apply_server_footprint(building: Node3D) -> void:
	# Keep mesh resources and the gameplay root unchanged; scale only their presentation.
	var original: Vector2i = building.get_meta("footprint")
	var target: Vector2i = SERVER_FOOTPRINTS[int(building.get("BuildingType"))]
	var width_scale := float(target.x) / original.x
	var depth_scale := float(target.y) / original.y
	var model_scale := Vector3(width_scale, minf(width_scale, depth_scale), depth_scale)
	if int(building.get("BuildingType")) in [4, 5]:
		model_scale.y = depth_scale
	var visual := building.get_node("Visual") as Node3D
	visual.scale *= model_scale
	var turret := building.get_node_or_null("Turret") as Node3D
	if turret != null:
		turret.scale *= model_scale
		turret.position *= model_scale
	var collision := building.get_node("SelectionArea/CollisionShape3D") as CollisionShape3D
	var shape := collision.shape as BoxShape3D
	shape.size *= model_scale
	collision.position *= model_scale
	var entrance := building.get_node("Entrance") as Marker3D
	entrance.position *= model_scale
	building.set("HealthBarHeight", float(building.get("HealthBarHeight")) * model_scale.y)
	building.set_meta("footprint", target)
