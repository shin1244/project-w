extends SceneTree
## First hero: four weight-bearing legs and ONE raised vestigial arm on the right flank.
## An escaped alchemy subject: a strapped-on rig of fluid cylinders feeds the neck and the
## grafted arm, the left foreleg is a brass piston prosthesis, and the hide is stitched and
## stapled where it was opened. Bakes articulated, flat-shaded meshes. Forward -Z, paws Y=0.

const PALETTE := {
	"fur": Color("45565f"), "light": Color("849291"), "mane": Color("263440"),
	"dark": Color("15252e"), "muzzle": Color("a3a89c"), "bone": Color("ede3c5"),
	"skin": Color("aa7772"), "scar": Color("684952"), "mouth": Color("251d28"),
	"eye": Color("ffb848"), "team": Color.WHITE,
	"old_bone": Color("c9b990"), "withered": Color("514b4c"), "hollow": Color("24272b"),
	"brass": Color("a88349"), "iron": Color("2f363d"), "steel": Color("8f979c"),
	"copper": Color("b0673e"), "leather": Color("4a3426"), "thread": Color("d9cfb2"),
	"ash": Color("7d7a74"), "grime": Color("2a2622"), "glass": Color(.85, .97, .95, .3),
	"fluid": Color("5fe3c0")
}
const METAL := ["brass", "iron", "steel", "copper"]
var materials: Dictionary = {}
var surfaces: Dictionary = {}
var rig: Node3D
var part: Node3D
var origin := Vector3.ZERO

func _initialize() -> void:
	for category in ["neutral", "team", "eye", "metal", "glass", "fluid"]:
		var mat := StandardMaterial3D.new()
		mat.resource_name = "team_cloth" if category == "team" else "wolf_" + category
		mat.vertex_color_use_as_albedo = true
		mat.vertex_color_is_srgb = true
		mat.albedo_color = Color("7cacc6") if category == "team" else Color.WHITE
		mat.roughness = .88
		if category == "eye":
			mat.emission_enabled = true
			mat.emission = Color("ef8b24")
			mat.emission_energy_multiplier = .65
		if category in ["metal", "team"]:
			# Lab hardware is less polished than war machines, but still catches the light.
			mat.metallic = .3 if category == "metal" else .15
			mat.roughness = .42 if category == "metal" else .6
		if category == "glass":
			mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
			mat.roughness = .08
			mat.metallic_specular = .9
		if category == "fluid":
			# The fluid glows only inside its glass and brass cylinders.
			mat.emission_enabled = true
			mat.emission = Color("39c9a3")
			mat.emission_energy_multiplier = .7
		materials[category] = mat
	rig = Node3D.new()
	rig.name = "Rig"
	rig.set_meta("pivot", Vector3.ZERO)
	build_body()
	build_head()
	build_legs()
	build_mutation()
	build_tail()
	var scene := PackedScene.new()
	assert(scene.pack(rig) == OK)
	assert(ResourceSaver.save(scene, "res://units/models/MutantWolfRig.tscn") == OK)
	rig.free()
	print("PASS: mutant wolf rig baked (four legs, one small raised arm)")
	quit()

func begin_part(part_name: String, pivot: Vector3, parent: Node3D = null) -> void:
	if parent == null: parent = rig
	part = Node3D.new()
	part.name = part_name
	part.position = pivot - Vector3(parent.get_meta("pivot"))
	part.set_meta("pivot", pivot)
	parent.add_child(part)
	part.owner = rig
	origin = pivot
	surfaces.clear()

func finish_part() -> void:
	var mesh := ArrayMesh.new()
	for category in surfaces:
		var st: SurfaceTool = surfaces[category]
		st.index()
		st.commit(mesh)
	var model := MeshInstance3D.new()
	model.name = "Mesh"
	model.mesh = mesh
	part.add_child(model)
	model.owner = rig

func triangle(key: String, a: Vector3, b: Vector3, c: Vector3) -> void:
	var normal := (b - a).cross(c - a)
	if normal.length_squared() < .00000001: return
	normal = normal.normalized()
	var category: String = key if key in ["team", "eye", "glass", "fluid"] else "metal" if key in METAL else "neutral"
	if not surfaces.has(category):
		var surface := SurfaceTool.new()
		surface.begin(Mesh.PRIMITIVE_TRIANGLES)
		surface.set_material(materials[category])
		surfaces[category] = surface
	var st: SurfaceTool = surfaces[category]
	for v in [a, c, b]:
		st.set_normal(normal)
		st.set_color(PALETTE[key])
		st.add_vertex(v - origin)

func quad(key: String, a: Vector3, b: Vector3, c: Vector3, d: Vector3) -> void:
	triangle(key, a, b, c)
	triangle(key, a, c, d)

func ellipsoid(key: String, center: Vector3, radius: Vector3, segments: int = 10, rings: int = 6) -> void:
	for y in range(rings):
		var lo := -PI * .5 + PI * y / rings
		var hi := -PI * .5 + PI * (y + 1) / rings
		for x in range(segments):
			var a := TAU * x / segments
			var b := TAU * (x + 1) / segments
			quad(key, center + sphere(lo, a) * radius, center + sphere(hi, a) * radius,
				center + sphere(hi, b) * radius, center + sphere(lo, b) * radius)

func sphere(latitude: float, angle: float) -> Vector3:
	return Vector3(cos(latitude) * cos(angle), sin(latitude), cos(latitude) * sin(angle))

func tube(key: String, a: Vector3, b: Vector3, start_radius: float, end_radius: float, sides: int = 7) -> void:
	var basis := Basis(Quaternion(Vector3.UP, (b - a).normalized()))
	for i in range(sides):
		var u := basis * Vector3(cos(TAU * i / sides), 0, sin(TAU * i / sides))
		var v := basis * Vector3(cos(TAU * (i + 1) / sides), 0, sin(TAU * (i + 1) / sides))
		quad(key, a + u * start_radius, b + u * end_radius, b + v * end_radius, a + v * start_radius)
		triangle(key, a, a + u * start_radius, a + v * start_radius)
		triangle(key, b, b + v * end_radius, b + u * end_radius)

func tuft(key: String, base: Vector3, tip: Vector3, width: float) -> void:
	tube(key, base, tip, width, 0, 4)

func build_body() -> void:
	begin_part("Body", Vector3(0, 1.35, 0))
	ellipsoid("fur", Vector3(0, 1.38, .06), Vector3(.47, .47, .88), 12)
	ellipsoid("fur", Vector3(0, 1.55, -.43), Vector3(.58, .61, .65), 12)
	ellipsoid("light", Vector3(0, 1.18, -.66), Vector3(.32, .38, .30))
	ellipsoid("fur", Vector3(0, 1.23, .72), Vector3(.44, .42, .48))
	ellipsoid("mane", Vector3(0, 1.75, -.70), Vector3(.47, .44, .49))
	# Layered fur follows the spine; the high shoulder and narrow waist keep a canine silhouette.
	for i in [0, 1, 2, 5]:
		# The middle of the spine is shaved for the cylinder rig.
		var z: float = -.75 + i * .27
		var y: float = 2.03 - i * .08
		tuft("mane", Vector3(0, y - .17, z), Vector3(0, y + .25, z + .32), .22 - i * .013)
	for side in [-1.0, 1.0]:
		for i in range(3):
			tuft("light" if i == 0 else "mane", Vector3(side * .42, 1.77 - i * .18, -.48 + i * .10),
				Vector3(side * (.72 - i * .045), 1.45 - i * .19, -.15 + i * .12), .18)
		for i in range(3):
			tuft("fur", Vector3(side * .37, 1.24, .47 + i * .15), Vector3(side * .56, .92, .61 + i * .15), .13)
	build_exposed_ribs(1.0, .27, .31, 4)
	build_exposed_ribs(-1.0, .20, .23, 3)
	build_cylinder_rig()
	build_collar()
	build_stitches()
	finish_part()

func flank_point(side: float, y: float, z: float, lift: float = 0.0) -> Vector3:
	# Follow the chest/waist envelope so exposed ribs curve around the body.
	var x := 0.0
	for shape in [Vector3(.47, .47, .88), Vector3(.58, .61, .65)]:
		var center := Vector3(0, 1.38, .06) if shape.x < .5 else Vector3(0, 1.55, -.43)
		var radius_squared := 1.0 - pow((y - center.y) / shape.y, 2) - pow((z - center.z) / shape.z, 2)
		if radius_squared > 0.0:
			x = maxf(x, shape.x * sqrt(radius_squared))
	return Vector3(side * (x + lift), y, z)

func build_exposed_ribs(side: float, center_z: float, width: float, count: int) -> void:
	# An irregular dry rim surrounds dark gaps between individual curved ribs.
	var outline := [Vector2(-.92, .45), Vector2(-.63, .96), Vector2(-.13, .85),
		Vector2(.25, 1.0), Vector2(.86, .67), Vector2(1.0, .11),
		Vector2(.75, -.70), Vector2(.21, -.96), Vector2(-.20, -.80), Vector2(-.85, -.56)]
	var center := flank_point(side, 1.43, center_z, .016)
	for i in range(outline.size()):
		var a: Vector2 = outline[i]
		var b: Vector2 = outline[(i + 1) % outline.size()]
		var outer_a := flank_point(side, 1.43 + a.y * .32, center_z + a.x * width, .012)
		var outer_b := flank_point(side, 1.43 + b.y * .32, center_z + b.x * width, .012)
		var inner_a := flank_point(side, 1.43 + a.y * .278, center_z + a.x * (width - .025), .018)
		var inner_b := flank_point(side, 1.43 + b.y * .278, center_z + b.x * (width - .025), .018)
		if side > 0:
			quad("withered", outer_b, inner_b, inner_a, outer_a)
			triangle("hollow", center, inner_a, inner_b)
		else:
			quad("withered", outer_a, inner_a, inner_b, outer_b)
			triangle("hollow", center, inner_b, inner_a)
	for i in range(count):
		var z := center_z - width * .65 + i * width * 1.25 / (count - 1)
		var top := flank_point(side, 1.69 - i * .018, z, .037)
		var bend := flank_point(side, 1.48, z + .035, .049)
		var lower := flank_point(side, 1.28 + i * .009, z + .075, .041)
		var tip := flank_point(side, 1.18 + i * .026, z + .115, .024)
		tube("old_bone", top, bend, .040, .037, 5)
		tube("old_bone", bend, lower, .037, .031, 5)
		tube("old_bone", lower, tip, .031, .016, 5)
	# Surgical staples bridge the rim where the hide was pinned open.
	for i in range(0, outline.size(), 2):
		var p: Vector2 = outline[i]
		var inner := flank_point(side, 1.43 + p.y * .25, center_z + p.x * (width - .05), .02)
		var outer := flank_point(side, 1.43 + p.y * .39, center_z + p.x * (width + .05), .02)
		tube("steel", inner, outer, .014, .014, 4)

func build_head() -> void:
	begin_part("Head", Vector3(0, 1.72, -.92), rig.get_node("Body"))
	var head := part
	ellipsoid("fur", Vector3(0, 1.76, -1.18), Vector3(.33, .29, .43))
	ellipsoid("light", Vector3(0, 1.72, -1.50), Vector3(.25, .17, .38), 8)
	ellipsoid("muzzle", Vector3(0, 1.60, -1.80), Vector3(.20, .115, .34), 8, 4)
	ellipsoid("dark", Vector3(0, 1.64, -2.10), Vector3(.17, .095, .10), 8, 4)
	ellipsoid("mouth", Vector3(0, 1.48, -1.71), Vector3(.22, .125, .39), 8, 4)
	for side in [-1.0, 1.0]:
		# Brow slopes down toward the nose, leaving a narrow amber eye underneath.
		ellipsoid("dark", Vector3(side * .295, 1.84, -1.43), Vector3(.077, .075, .13), 6, 4)
		ellipsoid("eye", Vector3(side * .327, 1.844, -1.47), Vector3(.030, .022, .073), 6, 4)
		if side > 0:
			# Only the right brow is exposed; preserve the amber eye in its socket.
			tube("withered", Vector3(.26, 1.90, -1.30), Vector3(.30, 1.89, -1.57), .079, .034, 5)
			tube("old_bone", Vector3(.283, 1.917, -1.30), Vector3(.322, 1.902, -1.56), .061, .025, 5)
		else:
			tube("mane", Vector3(side * .26, 1.90, -1.30), Vector3(side * .30, 1.89, -1.57), .09, .035, 5)
		for i in range(3):
			tuft("light", Vector3(side * .24, 1.71 - i * .09, -1.17),
				Vector3(side * (.51 - i * .06), 1.57 - i * .12, -.88), .13)
		var a := Vector3(side * .12, 1.98, -1.05)
		var b := Vector3(side * .40, 1.96, -.92)
		var tip := Vector3(side * .37, 2.44, -.97)
		var back := Vector3(side * .25, 2.06, -.83)
		if side > 0:
			triangle("mane", a, b, tip)
			triangle("fur", a, tip, back)
			triangle("fur", tip, b, back)
		else:
			triangle("mane", a, tip, b)
			triangle("fur", a, back, tip)
			triangle("fur", tip, back, b)
		var inset_a := a.lerp(tip, .18) + Vector3(0, 0, -.012)
		var inset_b := b.lerp(tip, .18) + Vector3(0, 0, -.012)
		var inset_tip := tip.lerp((a + b) * .5, .20) + Vector3(0, 0, -.012)
		if side > 0: triangle("skin", inset_a, inset_b, inset_tip)
		else: triangle("skin", inset_a, inset_tip, inset_b)
		for i in range(3):
			var z := -1.83 + i * .16
			tube("bone", Vector3(side * (.17 + i * .018), 1.56, z),
				Vector3(side * (.18 + i * .018), 1.40 if i == 1 else 1.48, z - .015), .044, 0, 5)
	build_exposed_skull()
	# Stapled incision across the crown and a brass subject tag clipped to the left ear.
	var crown := []
	for i in range(6):
		var x := -.22 + i * .065
		var z := -1.10 + i * .02
		crown.append(Vector3(x, 1.76 + .29 * sqrt(maxf(0, 1 - pow(x / .33, 2) - pow((z + 1.18) / .43, 2))) + .008, z))
	seam(crown, Vector3.UP, .045)
	tube("brass", Vector3(-.27, 1.97, -.98), Vector3(-.27, 1.90, -.99), .012, .012, 4)
	box("brass", Vector3(-.27, 1.84, -1.0), Vector3(.10, .09, .015))
	box("hollow", Vector3(-.27, 1.845, -1.009), Vector3(.06, .016, .004))
	finish_part()
	begin_part("Jaw", Vector3(0, 1.46, -1.25), head)
	ellipsoid("muzzle", Vector3(0, 1.36, -1.67), Vector3(.20, .085, .39), 8, 4)
	for side in [-1.0, 1.0]:
		tube("bone", Vector3(side * .18, 1.40, -1.81), Vector3(side * .19, 1.54, -1.83), .04, 0, 5)
	finish_part()

func build_exposed_skull() -> void:
	# A torn patch on the right temple joins a partial orbital rim and cheekbone.
	# It belongs to Head, so the exposure stays attached during breathing and biting.
	var center := Vector3(.296, 1.935, -1.272)
	var edge := [Vector3(.155, 2.021, -1.23), Vector3(.240, 1.988, -1.10),
		Vector3(.335, 1.840, -1.18), Vector3(.356, 1.840, -1.32),
		Vector3(.327, 1.922, -1.43), Vector3(.200, 1.985, -1.43)]
	for i in range(edge.size()):
		var a: Vector3 = edge[i]
		var b: Vector3 = edge[(i + 1) % edge.size()]
		var inner_a := a.lerp(center, .17) + Vector3(.008, 0, 0)
		var inner_b := b.lerp(center, .17) + Vector3(.008, 0, 0)
		quad("withered", b, inner_b, inner_a, a)
		triangle("old_bone", center, inner_a, inner_b)
	var cheek_back := Vector3(.349, 1.790, -1.245)
	var cheek_middle := Vector3(.356, 1.735, -1.405)
	var cheek_front := Vector3(.251, 1.675, -1.64)
	tube("hollow", cheek_back - Vector3(.013, 0, 0), cheek_middle, .052, .047, 5)
	tube("hollow", cheek_middle, cheek_front, .047, .031, 5)
	tube("old_bone", cheek_back + Vector3(.013, 0, 0), cheek_middle + Vector3(.020, 0, 0), .034, .030, 5)
	tube("old_bone", cheek_middle + Vector3(.020, 0, 0), cheek_front + Vector3(.013, 0, 0), .030, .019, 5)

func build_legs() -> void:
	for side in [-1.0, 1.0]:
		var suffix := "Left" if side < 0 else "Right"
		var hip := Vector3(side * .44, 1.48, -.48)
		var elbow := Vector3(side * .55, .72, -.37)
		begin_part("Front" + suffix, hip)
		var upper := part
		ellipsoid("fur", hip - Vector3(0, .16, -.015), Vector3(.24, .39, .29), 8)
		tube("fur", hip, elbow, .20, .12)
		tuft("light", hip + Vector3(side * .06, -.15, -.10), elbow + Vector3(side * .10, .17, .20), .16)
		if side > 0:
			# A small scapula patch embedded in the shoulder, edged with dry tissue.
			ellipsoid("withered", Vector3(.649, 1.40, -.52), Vector3(.045, .235, .17), 7, 4)
			ellipsoid("hollow", Vector3(.670, 1.40, -.52), Vector3(.031, .193, .13), 7, 4)
			ellipsoid("old_bone", Vector3(.688, 1.44, -.55), Vector3(.022, .14, .085), 5, 4)
			tube("old_bone", Vector3(.697, 1.50, -.58), Vector3(.694, 1.29, -.48), .032, .022, 5)
		finish_part()
		begin_part("Lower", elbow, upper)
		if side < 0:
			build_prosthesis(elbow, Vector3(side * .56, .24, -.67))
		else:
			tube("fur", elbow, Vector3(side * .56, .24, -.67), .13, .09)
			paw(Vector3(side * .56, .13, -.77))
			ellipsoid("grime", Vector3(side * .56, .21, -.74), Vector3(.17, .05, .2), 6, 3)
		finish_part()
		var haunch := Vector3(side * .39, 1.19, .68)
		var knee := Vector3(side * .53, .68, .42)
		begin_part("Hind" + suffix, haunch)
		upper = part
		ellipsoid("fur", haunch - Vector3(0, .12, 0), Vector3(.265, .36, .32), 8)
		tube("fur", haunch, knee, .23, .12)
		finish_part()
		begin_part("Lower", knee, upper)
		var hock := Vector3(side * .53, .34, .96)
		if side > 0:
			# Missing fur along one shin reveals a thinner shaft, with fur at both joints.
			var bone_start := knee.lerp(hock, .24)
			var bone_end := knee.lerp(hock, .83)
			tube("mane", knee, bone_start, .13, .097)
			tube("withered", bone_start, bone_start.lerp(bone_end, .15), .093, .062)
			tube("old_bone", bone_start, bone_end, .050, .037)
			tube("withered", bone_end.lerp(bone_start, .10), hock, .061, .083)
			tube("hollow", bone_start + Vector3(-.05, 0, 0), bone_end + Vector3(-.035, 0, 0), .020, .014, 5)
		else:
			tube("mane", knee, hock, .13, .085)
		tube("fur", hock, Vector3(side * .53, .18, .75), .087, .075)
		paw(Vector3(side * .53, .12, .70))
		finish_part()

func paw(center: Vector3) -> void:
	ellipsoid("light", center, Vector3(.195, .115, .25), 8, 4)
	for x in [-.115, 0.0, .115]:
		tube("bone", center + Vector3(x, -.015, -.15), center + Vector3(x, -.065, -.36), .041, 0, 5)

func build_mutation() -> void:
	# Only one extra arm: bent away from the right ribs, with a small three-finger hand.
	var shoulder := Vector3(.48, 1.58, -.17)
	var elbow := Vector3(1.01, 1.48, .03)
	begin_part("ExtraArm", shoulder, rig.get_node("Body"))
	var upper := part
	ellipsoid("scar", shoulder, Vector3(.20, .20, .23), 8)
	ellipsoid("skin", shoulder + Vector3(.06, .015, 0), Vector3(.17, .15, .19), 8)
	tube("skin", shoulder, elbow, .13, .075)
	tube("muzzle", shoulder + Vector3(.10, .07, .02), elbow + Vector3(0, .055, .015), .075, .04)
	ellipsoid("skin", elbow, Vector3(.105, .10, .10), 8, 4)
	# Brass fixation clamps hold the graft straight.
	var clamp_at := shoulder.lerp(elbow, .55)
	var along := (elbow - shoulder).normalized()
	tube("brass", clamp_at - along * .035, clamp_at + along * .035, .115, .115, 7)
	for side in [-1.0, 1.0]:
		tube("steel", clamp_at + Vector3(0, .1, side * .07), clamp_at + Vector3(0, .17, side * .07), .016, .016, 4)
	finish_part()
	begin_part("Forearm", elbow, upper)
	var wrist := Vector3(1.10, 1.18, -.31)
	tube("skin", elbow, wrist, .078, .055)
	tube("brass", elbow.lerp(wrist, .55), elbow.lerp(wrist, .7), .085, .08, 7)
	ellipsoid("muzzle", Vector3(1.10, 1.17, -.43), Vector3(.12, .062, .14), 8, 4)
	for i in range(3):
		var x := 1.015 + i * .09
		var knuckle := Vector3(x, 1.17, -.50)
		var bend := Vector3(x + (i - 1) * .018, 1.09, -.65 - (1 - absi(i - 1)) * .06)
		tube("skin", knuckle, bend, .032, .023, 5)
		tube("bone", bend, bend + Vector3(.015, -.085, -.09), .025, 0, 5)
	finish_part()

func build_tail() -> void:
	begin_part("Tail", Vector3(0, 1.20, 1.02), rig.get_node("Body"))
	tube("fur", Vector3(0, 1.20, 1.02), Vector3(.12, 1.18, 1.66), .23, .20)
	tube("mane", Vector3(.12, 1.18, 1.66), Vector3(.19, .90, 2.22), .20, .10)
	tuft("light", Vector3(.19, .90, 2.22), Vector3(.22, .77, 2.46), .105)
	for i in range(3):
		tuft("mane", Vector3(.10, 1.27 - i * .12, 1.50 + i * .20), Vector3(.13, 1.23 - i * .15, 1.98 + i * .17), .11)
	finish_part()

func build_cylinder_rig() -> void:
	# A painted saddle plate, strapped around the chest and haunch, carries two cylinders.
	var top := 1.86
	box("brass", Vector3(0, top - .03, .22), Vector3(.54, .03, .62))
	box("team", Vector3(0, top, .22), Vector3(.48, .05, .56))
	strap(Vector3(0, 1.38, .06), Vector3(.47, .47, .88), -.12)
	strap(Vector3(0, 1.23, .72), Vector3(.44, .42, .48), .64)
	for side in [-1.0, 1.0]:
		var base := Vector3(side * .155, top + .025, .22)
		var level := .36 if side < 0 else .27
		tube("brass", base, base + Vector3(0, .07, 0), .14, .14, 10)
		tube("fluid", base + Vector3(0, .07, 0), base + Vector3(0, .07 + level, 0), .1, .1, 10)
		tube("glass", base + Vector3(0, .07, 0), base + Vector3(0, .5, 0), .118, .118, 10)
		tube("brass", base + Vector3(0, .5, 0), base + Vector3(0, .57, 0), .14, .135, 10)
		for k in range(3):
			var angle := TAU * k / 3 + (.5 if side > 0 else 0.0)
			var offset := Vector3(cos(angle), 0, sin(angle)) * .132
			tube("brass", base + offset + Vector3(0, .07, 0), base + offset + Vector3(0, .5, 0), .013, .013, 4)
		tube("brass", base + Vector3(0, .57, 0), base + Vector3(0, .63, 0), .035, .035, 6)
		box("iron", base + Vector3(0, .65, 0), Vector3(.13, .025, .025), Vector3(0, side * .6, 0))
		# Feed line into the neck, through a clamped port in the hide.
		var path := [base + Vector3(0, .6, -.02), Vector3(side * .16, 2.52, .02),
			Vector3(side * .20, 2.24, -.38), Vector3(side * .22, 2.10, -.60)]
		for i in range(path.size() - 1):
			tube("copper", path[i], path[i + 1], .03, .03, 6)
		port(path[path.size() - 1])
	# Second line from the right cylinder into the grafted arm.
	var arm_line := [Vector3(.27, 1.93, .22), Vector3(.44, 1.84, .04), Vector3(.53, 1.70, -.10)]
	for i in range(arm_line.size() - 1):
		tube("copper", arm_line[i], arm_line[i + 1], .026, .026, 6)
	port(arm_line[arm_line.size() - 1])

func port(at: Vector3) -> void:
	ellipsoid("brass", at, Vector3(.06, .06, .06), 6, 3)
	ellipsoid("iron", at + Vector3(0, -.03, 0), Vector3(.09, .03, .09), 6, 2)

## Leather strap around an ellipsoid's cross-section at z, buckled on the left side.
func strap(center: Vector3, radius: Vector3, z: float) -> void:
	var scale := sqrt(1.0 - pow((z - center.z) / radius.z, 2))
	var points := []
	for i in range(16):
		var angle := TAU * i / 16
		points.append(Vector3(cos(angle) * (radius.x * scale + .02), center.y + sin(angle) * (radius.y * scale + .02), z))
	for i in range(points.size()):
		tube("leather", points[i], points[(i + 1) % points.size()], .03, .03, 4)
	box("brass", Vector3(-(radius.x * scale + .035), center.y - .06, z), Vector3(.03, .1, .08))

func build_collar() -> void:
	# Iron restraint collar with a broken chain and a stamped subject tag.
	var center := Vector3(0, 1.72, -.93)
	var ring := []
	for i in range(14):
		var angle := TAU * i / 14
		ring.append(center + Vector3(cos(angle) * .45, sin(angle) * .43, 0))
	for i in range(ring.size()):
		tube("iron", ring[i], ring[(i + 1) % ring.size()], .055, .055, 5)
	for i in range(0, ring.size(), 2):
		ellipsoid("brass", ring[i] + Vector3(0, 0, -.05), Vector3(.026, .026, .02), 4, 2)
	var hang := center + Vector3(0, -.47, -.02)
	for i in range(3):
		var link := hang + Vector3(.012 * i, -.075 * i, -.012 * i)
		box("iron", link, Vector3(.05, .09, .022) if i % 2 == 0 else Vector3(.022, .09, .05))
	box("brass", center + Vector3(.13, -.5, -.05), Vector3(.1, .12, .015), Vector3(0, 0, .2))
	box("hollow", center + Vector3(.13, -.49, -.059), Vector3(.06, .016, .004), Vector3(0, 0, .2))

func build_stitches() -> void:
	# Long stitched incision across the left haunch.
	var cut := []
	for i in range(7):
		var t := i / 6.0
		var y := lerpf(1.52, 1.08, t)
		var z := lerpf(.50, .92, t) + sin(t * PI) * .05
		cut.append(hide_point(-1.0, y, z, .012))
	seam(cut, Vector3.LEFT, .05)
	# A ring of stitches where the extra arm was grafted on.
	var graft := []
	for i in range(13):
		var angle := TAU * i / 12
		graft.append(hide_point(1.0, 1.58 + sin(angle) * .23, -.17 + cos(angle) * .25, .012))
	seam(graft, Vector3.RIGHT, .045)
	# Sparse ash on the shoulders and back; never more than a few patches.
	for patch in [[-1.0, 1.78, -.52, .14], [1.0, 1.66, .56, .12], [-1.0, 1.30, -.05, .1]]:
		var at := hide_point(patch[0], patch[1], patch[2], -.01)
		ellipsoid("ash", at, Vector3(.035, patch[3], patch[3] * 1.3), 6, 3)

## Surface point across the chest, waist and haunch envelopes.
func hide_point(side: float, y: float, z: float, lift: float) -> Vector3:
	var x := flank_point(side, y, z).x * side
	var center := Vector3(0, 1.23, .72)
	var radius := Vector3(.44, .42, .48)
	var squared := 1.0 - pow((y - center.y) / radius.y, 2) - pow((z - center.z) / radius.z, 2)
	if squared > 0.0:
		x = maxf(x, radius.x * sqrt(squared))
	return Vector3(side * (x + lift), y, z)

## Dark seam line with pale cross-stitches lying in the surface facing `outward`.
func seam(points: Array, outward: Vector3, width: float) -> void:
	for i in range(points.size() - 1):
		var a: Vector3 = points[i]
		var b: Vector3 = points[i + 1]
		tube("scar", a, b, .02, .02, 4)
		var across := (b - a).cross(outward).normalized() * width
		var mid := a.lerp(b, .5) + outward * .006
		tube("thread", mid - across, mid + across, .014, .014, 4)

func build_prosthesis(elbow: Vector3, ankle: Vector3) -> void:
	# Leather socket stitched to the stump, brass cylinder, steel piston, iron claw foot.
	var socket_end := elbow.lerp(ankle, .24)
	ellipsoid("scar", elbow, Vector3(.14, .12, .14), 6, 3)
	tube("leather", elbow + Vector3(0, .04, 0), socket_end, .15, .125, 7)
	tube("brass", socket_end + Vector3(0, .03, 0), socket_end - Vector3(0, .03, 0), .135, .135, 7)
	tube("brass", socket_end, elbow.lerp(ankle, .58), .075, .068, 7)
	tube("steel", elbow.lerp(ankle, .55), ankle, .036, .036, 6)
	tube("iron", elbow.lerp(ankle, .3) + Vector3(0, 0, .1), ankle + Vector3(0, .04, .07), .024, .024, 5)
	ellipsoid("brass", ankle, Vector3(.075, .075, .075), 6, 3)
	for x in [-.1, 0.0, .1]:
		var knuckle := ankle + Vector3(x, -.17, -.2)
		tube("iron", ankle, knuckle, .04, .032, 5)
		tube("steel", knuckle, knuckle + Vector3(x * .2, -.065, -.11), .03, 0, 5)
	tube("iron", ankle, ankle + Vector3(0, -.21, .13), .035, .02, 5)

func box(key: String, center: Vector3, size: Vector3, rotation := Vector3.ZERO) -> void:
	var basis := Basis.from_euler(rotation)
	var h := size * .5
	for axis in range(3):
		for sign in [-1.0, 1.0]:
			var corners := []
			for k in [[-1, -1], [1, -1], [1, 1], [-1, 1]]:
				var p := [0.0, 0.0, 0.0]
				p[axis] = sign
				p[(axis + 1) % 3] = k[0]
				p[(axis + 2) % 3] = k[1]
				corners.append(center + basis * Vector3(p[0] * h.x, p[1] * h.y, p[2] * h.z))
			var a: Vector3 = corners[0]
			if (corners[1] - a).cross(corners[2] - a).dot(a - center) < 0:
				corners.reverse()
			triangle(key, corners[0], corners[1], corners[2])
			triangle(key, corners[0], corners[2], corners[3])
