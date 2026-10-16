extends SceneTree
## One reusable fissure model with GPU-only, phase-offset oil surges.

const SCENE_PATH := "res://maps/Oilfield.tscn"
const GROUND_PATH := "res://maps/meshes/OilfieldGround.res"
const MOTION_PATH := "res://maps/meshes/OilfieldMotion.res"
var surfaces: Dictionary = {}
var motion := SurfaceTool.new()

func _initialize() -> void:
	make_material("earth", Color("493f30"))
	make_material("earth_light", Color("61513c"))
	make_material("earth_dark", Color("302f26"))
	make_material("oil", Color("050b09"), true)
	build_fissures()
	build_motion()
	save_model()
	quit()

func make_material(key: String, color: Color, wet := false) -> void:
	var material := StandardMaterial3D.new()
	material.resource_name = key
	material.albedo_color = color
	material.roughness = .28 if wet else .94
	material.metallic = 0.0
	material.metallic_specular = .28 if wet else .5
	var surface := SurfaceTool.new()
	surface.begin(Mesh.PRIMITIVE_TRIANGLES)
	surface.set_material(material)
	surfaces[key] = surface

func triangle(key: String, a: Vector3, b: Vector3, c: Vector3, upward := false) -> void:
	if upward and (b - a).cross(c - a).y < 0:
		var swap := b
		b = c
		c = swap
	var normal := (b - a).cross(c - a).normalized()
	for point in [a, c, b]:
		surfaces[key].set_normal(normal)
		surfaces[key].set_uv(Vector2.ZERO)
		surfaces[key].add_vertex(point)

func point(p: Vector2, height: float) -> Vector3:
	return Vector3(p.x, height, p.y)

func build_fissures() -> void:
	var main := PackedVector2Array([Vector2(-1.83, -.58), Vector2(-1.31, -.30), Vector2(-.87, -.47),
		Vector2(-.29, .02), Vector2(.16, -.12), Vector2(.70, .40), Vector2(1.20, .26), Vector2(1.78, .68)])
	crack(main, .16, 0)
	crack(PackedVector2Array([Vector2(-.87, -.47), Vector2(-.70, -.90), Vector2(-1.02, -1.34)]), .105, 8)
	crack(PackedVector2Array([Vector2(.70, .40), Vector2(.42, .89), Vector2(.75, 1.34)]), .11, 12)
	crack(PackedVector2Array([Vector2(-.29, .02), Vector2(-.62, .45), Vector2(-.46, .84)]), .09, 16)

func crack(path: PackedVector2Array, width: float, seed_index: int) -> void:
	for i in range(path.size() - 1):
		var a := path[i]
		var b := path[i + 1]
		var direction := (b - a).normalized()
		var normal := Vector2(-direction.y, direction.x)
		soil_ribbon(a, b, width, seed_index + i)
		# Overlap the dark ribbons at bends so every branch remains a continuous crack.
		var start := a - direction * width * .45
		var finish := b + direction * width * .45
		var inner := normal * width
		triangle("oil", point(start - inner, .026), point(start + inner, .026), point(finish + inner, .026), true)
		triangle("oil", point(start - inner, .026), point(finish + inner, .026), point(finish - inner, .026), true)
		if i == 0:
			triangle("oil", point(start - direction * width * 1.2, .026), point(start - inner, .026), point(start + inner, .026), true)
		if i == path.size() - 2:
			triangle("oil", point(finish + direction * width * 1.2, .026), point(finish - inner, .026), point(finish + inner, .026), true)
		for side in [-1.0, 1.0]:
			var n: Vector2 = normal * side
			var variation := (seed_index + i) % 3
			var rim_height := .13 + variation * .035
			var shoulder := width + .20 + variation * .035
			var p0: Vector2 = a + direction * .07 + n * width * .88
			var p1: Vector2 = b - direction * .06 + n * width * .95
			var p2: Vector2 = b - direction * .12 + n * shoulder
			var p3: Vector2 = a + direction * .13 + n * (shoulder + .045)
			plate(PackedVector2Array([p0, p1, p2, p3]), rim_height, seed_index + i)

func soil_ribbon(a: Vector2, b: Vector2, width: float, index: int) -> void:
	# A flat, jagged strip joins the broken plates to the ground and adjacent branches.
	# Follow each crack rather than filling its outline with a round plinth.
	var direction := (b - a).normalized()
	var normal := Vector2(-direction.y, direction.x)
	var span := width + .30
	var variation := (index % 3) * .025
	var start := a - direction * .15
	var finish := b + direction * .14
	var middle := a.lerp(b, .48)
	var outline := PackedVector2Array([
		start - normal * (span - .09),
		middle - normal * (span + variation),
		finish - normal * (span - .06),
		finish + direction * .10 + normal * .04,
		finish + normal * (span - .11),
		middle + normal * (span + .045 - variation),
		start + normal * (span - .04),
		start - direction * .07 - normal * .03,
	])
	for i in range(outline.size()):
		# Keep the ground skirt inside the original placement envelope.
		outline[i] = outline[i].clamp(Vector2(-2.05, -1.5), Vector2(2.05, 1.5))
	for i in range(outline.size()):
		triangle("earth_dark", point(middle, .016), point(outline[i], .016),
			point(outline[(i + 1) % outline.size()], .016), true)

func plate(outline: PackedVector2Array, rim_height: float, index: int) -> void:
	var center := Vector2.ZERO
	for p in outline:
		center += p
	center /= outline.size()
	var heights := [.16, .12, .028, .035]
	for i in range(outline.size()):
		var j := (i + 1) % outline.size()
		var a := point(outline[i], heights[i] * rim_height / .16)
		var b := point(outline[j], heights[j] * rim_height / .16)
		var peak := point(center, rim_height * .72)
		triangle("earth_light" if (index + i) % 4 == 0 else "earth", a, b, peak, true)
		var base_a := point(outline[i], .014)
		var base_b := point(outline[j], .014)
		var outward := (a + b) * .5 - point(center, 0)
		if (a - base_a).cross(b - base_a).dot(outward) < 0:
			triangle("earth_dark", base_a, b, a)
			triangle("earth_dark", base_a, base_b, b)
		else:
			triangle("earth_dark", base_a, a, b)
			triangle("earth_dark", base_a, b, base_b)

func build_motion() -> void:
	motion.begin(Mesh.PRIMITIVE_TRIANGLES)
	var material := ShaderMaterial.new()
	material.shader = load("res://maps/materials/OilSeep.gdshader")
	motion.set_material(material)
	# All placements share this mesh and material; local UV phases offset the three sources.
	for entry in [[Vector2(-1.06, -.38), .04], [Vector2(.24, -.04), .38], [Vector2(.57, .66), .73]]:
		var center: Vector2 = entry[0]
		var phase: float = entry[1]
		jet(center, phase)
		drop(center, phase)

func motion_triangle(a: Vector3, b: Vector3, c: Vector3, center: Vector2, phase: float, kind: float) -> void:
	var normal := (b - a).cross(c - a).normalized()
	for p in [a, c, b]:
		motion.set_normal(normal)
		motion.set_uv(Vector2(phase, kind))
		motion.set_uv2(center)
		motion.add_vertex(p)

func jet(center: Vector2, phase: float) -> void:
	var profile := [Vector2(.18, 0), Vector2(.15, .16), Vector2(.125, .42), Vector2(.12, .68),
		Vector2(.10, .84), Vector2(.075, .94), Vector2(.045, .99)]
	for ring in range(profile.size() - 1):
		for segment in range(7):
			var angle_a := segment * TAU / 7.0
			var angle_b := (segment + 1) * TAU / 7.0
			var lo: Vector2 = profile[ring]
			var hi: Vector2 = profile[ring + 1]
			var a := jet_point(center, lo, angle_a)
			var b := jet_point(center, hi, angle_a)
			var c := jet_point(center, hi, angle_b)
			var d := jet_point(center, lo, angle_b)
			motion_triangle(a, b, c, center, phase, 0)
			motion_triangle(a, c, d, center, phase, 0)
	for segment in range(7):
		var a := jet_point(center, profile[-1], segment * TAU / 7.0)
		var b := jet_point(center, profile[-1], (segment + 1) * TAU / 7.0)
		motion_triangle(a, point(center + Vector2(.065, -.025), 1.015), b, center, phase, 0)

func jet_point(center: Vector2, profile: Vector2, angle: float) -> Vector3:
	var bend := Vector2(.065, -.025) * profile.y * profile.y
	var offset := Vector2(cos(angle), sin(angle)) * profile.x + bend
	return point(center + offset, profile.y)

func drop(center: Vector2, phase: float) -> void:
	var top := point(center, .067)
	var bottom := point(center, -.055)
	for segment in range(6):
		var a := point(center + Vector2(cos(segment * TAU / 6.0), sin(segment * TAU / 6.0)) * .045, 0)
		var b := point(center + Vector2(cos((segment + 1) * TAU / 6.0), sin((segment + 1) * TAU / 6.0)) * .045, 0)
		motion_triangle(top, b, a, center, phase, 1)
		motion_triangle(bottom, a, b, center, phase, 1)

func save_model() -> void:
	DirAccess.make_dir_recursive_absolute("res://maps/meshes")
	var ground := ArrayMesh.new()
	for key in surfaces:
		surfaces[key].commit(ground)
	var animated := motion.commit()
	var bounds := ground.get_aabb()
	assert(bounds.position.x >= -2.1 and bounds.end.x <= 2.1)
	assert(bounds.position.z >= -1.7 and bounds.end.z <= 1.7)
	assert(bounds.position.y >= .012 and bounds.end.y < 1.2)
	assert(ResourceSaver.save(ground, GROUND_PATH, ResourceSaver.FLAG_COMPRESS) == OK)
	assert(ResourceSaver.save(animated, MOTION_PATH, ResourceSaver.FLAG_COMPRESS) == OK)
	var model := Node3D.new()
	model.name = "Oilfield"
	for entry in [["Fissure", GROUND_PATH], ["OilSurges", MOTION_PATH]]:
		var visual := MeshInstance3D.new()
		visual.name = entry[0]
		visual.mesh = load(entry[1])
		visual.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		if entry[0] == "OilSurges":
			visual.custom_aabb = AABB(Vector3(-2.1, .012, -1.7), Vector3(4.2, 1.15, 3.4))
		model.add_child(visual)
		visual.owner = model
	var packed := PackedScene.new()
	assert(packed.pack(model) == OK)
	assert(ResourceSaver.save(packed, SCENE_PATH) == OK)
	model.free()
	print("PASS: shared oil fissure generated; ground bounds ", bounds, "; GPU-only motion, no collision or process script")
