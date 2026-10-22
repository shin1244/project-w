extends SceneTree
## Bake an articulated, flat-shaded magitech golem. Feet Y=0, forward -Z.
## A riveted iron hull and brass bands carry a caged rune core; copper pipes feed it from
## the boiler on its back, and the old rune stones survive only as fists clamped in iron.

const COLORS := {
	"iron": Color("2f363d"), "plate": Color("4a5560"), "burnt": Color("3b434b"),
	"steel": Color("8f979c"), "brass": Color("a88349"), "gold": Color("d9ae45"),
	"copper": Color("b0673e"), "dark": Color("161b1f"), "soot": Color("2a2622"),
	"stone": Color("75827c"), "edge": Color("a9b2a4"), "gauge": Color("e8dfc4"),
	"rune": Color("72f3d6"), "ember": Color("ff8a3d"), "team": Color.WHITE,
	"glass": Color(1, .95, .82, .28), "lamp": Color("ffe6a8")
}
const METAL := ["iron", "plate", "burnt", "steel", "brass", "gold", "copper"]
var materials: Dictionary = {}
var surfaces: Dictionary = {}
var rig: Node3D
var part: Node3D
var origin := Vector3.ZERO

func _initialize() -> void:
	for category in ["stone", "metal", "rune", "ember", "team", "glass", "lamp"]:
		var mat := StandardMaterial3D.new()
		mat.resource_name = "team_cloth" if category == "team" else "golem_" + category
		mat.vertex_color_use_as_albedo = true
		mat.vertex_color_is_srgb = true
		mat.albedo_color = Color("8dbde3") if category == "team" else Color.WHITE
		mat.roughness = .92
		if category == "metal" or category == "team":
			# Weapon finish: polished enough for highlights, still lit by the flat ambient.
			mat.metallic = .3 if category == "metal" else .2
			mat.roughness = .38 if category == "metal" else .5
		if category in ["rune", "ember", "lamp"]:
			mat.emission_enabled = true
			mat.emission = {"rune": Color("4bcbb4"), "ember": Color("e8742d"), "lamp": Color("ffd37a")}[category]
			mat.emission_energy_multiplier = {"rune": .65, "ember": .8, "lamp": 1.6}[category]
		if category == "glass":
			# Lit panes glow faintly so the lamp still reads from the high game camera.
			mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
			mat.roughness = .08
			mat.metallic_specular = .9
			mat.emission_enabled = true
			mat.emission = Color("ffd37a")
			mat.emission_energy_multiplier = .6
		materials[category] = mat
	rig = Node3D.new()
	rig.name = "Rig"
	rig.set_meta("pivot", Vector3.ZERO)
	build_body()
	var body := part
	build_head(body)
	for side in [-1.0, 1.0]:
		build_arm(body, side)
		build_leg(side)
	var scene := PackedScene.new()
	assert(scene.pack(rig) == OK)
	assert(ResourceSaver.save(scene, "res://units/models/RuneGolemRig.tscn") == OK)
	rig.free()
	print("PASS: articulated magitech golem baked")
	quit()

func build_body() -> void:
	begin_part("Body", Vector3(0, 1.35, 0))
	# Pelvis, belt and the exposed pistons that carry the chest.
	hull("burnt", Vector3(0, 1.08, .04), Vector2(.42, .30), Vector2(.36, .26), .34)
	hull("brass", Vector3(0, 1.255, .03), Vector2(.46, .33), Vector2(.46, .33), .07)
	for side in [-1.0, 1.0]:
		tube("brass", Vector3(side * .26, 1.12, -.12), Vector3(side * .27, 1.27, -.16), .065, .065, 6)
		tube("steel", Vector3(side * .27, 1.25, -.16), Vector3(side * .29, 1.42, -.20), .032, .032, 6)
	# Barrel chest: lacquered iron, brass bands top and bottom, a riveted collar.
	hull("iron", Vector3(0, 1.70, 0), Vector2(.70, .44), Vector2(.50, .36), .78)
	hull("plate", Vector3(0, 1.76, -.035), Vector2(.60, .43), Vector2(.47, .37), .52)
	hull("brass", Vector3(0, 1.345, 0), Vector2(.53, .38), Vector2(.52, .375), .06)
	hull("brass", Vector3(0, 2.08, 0), Vector2(.68, .43), Vector2(.72, .455), .07)
	hull("plate", Vector3(0, 2.12, .0), Vector2(.60, .37), Vector2(.66, .41), .04)
	for i in range(9):
		var x := (i - 4) * .13
		rivet(Vector3(x, 2.08, -.462))
		rivet(Vector3(x * .76, 1.345, -.39))
	# Painted company plate under the core.
	box("team", Vector3(0, 1.47, -.405), Vector3(.62, .15, .04))
	for x in [-.27, .27]:
		rivet(Vector3(x, 1.47, -.43))
	build_core()
	build_gauge(Vector3(-.40, 1.93, -.445))
	build_boiler()
	finish_part()

func build_core() -> void:
	# The rune crystal sits behind a brass porthole and gold cage, never in the open.
	var c := Vector3(0, 1.73, -.40)
	tube("brass", c + Vector3(0, 0, .02), c + Vector3(0, 0, -.08), .28, .27, 10)
	tube("dark", c + Vector3(0, 0, -.07), c + Vector3(0, 0, -.085), .21, .21, 10)
	rock("rune", c + Vector3(0, 0, -.10), Vector3(.13, .18, .06), 4, 2)
	tube("gold", c + Vector3(-.22, 0, -.12), c + Vector3(.22, 0, -.12), .02, .02, 4)
	tube("gold", c + Vector3(0, -.22, -.12), c + Vector3(0, .22, -.12), .02, .02, 4)
	for i in range(8):
		var angle := TAU * i / 8 + PI / 8
		rivet(c + Vector3(cos(angle) * .245, sin(angle) * .245, -.085))
	# Copper feed pipes run from the porthole over the shoulders to the boiler.
	for side in [-1.0, 1.0]:
		var path := [c + Vector3(side * .25, .10, -.06), Vector3(side * .36, 1.98, -.42),
			Vector3(side * .40, 2.16, -.14), Vector3(side * .37, 2.16, .26), Vector3(side * .25, 2.00, .48)]
		for i in range(path.size() - 1):
			tube("copper", path[i], path[i + 1], .045, .045, 6)
		for i in range(1, path.size() - 1):
			rock("brass", path[i], Vector3(.065, .065, .065), 6, 3)

func build_gauge(c: Vector3) -> void:
	tube("brass", c + Vector3(0, 0, .03), c + Vector3(0, 0, -.03), .105, .105, 10)
	tube("gauge", c + Vector3(0, 0, -.03), c + Vector3(0, 0, -.036), .082, .082, 10)
	tube("dark", c + Vector3(0, 0, -.04), c + Vector3(.05, .045, -.04), .012, .006, 4)

func build_boiler() -> void:
	var c := Vector3(0, 1.40, .52)
	tube("copper", c, c + Vector3(0, .62, 0), .30, .30, 10)
	rock("copper", c + Vector3(0, .62, 0), Vector3(.30, .13, .30), 10, 3)
	for y in [.12, .50]:
		tube("brass", c + Vector3(0, y, 0), c + Vector3(0, y + .05, 0), .315, .315, 10)
		for i in range(7):
			var angle := PI * (.15 + .7 * i / 6.0)
			rivet(c + Vector3(cos(angle) * .32, y + .025, sin(angle) * .32))
	# Firebox grate faces backward; its glow is the only furnace light on the golem.
	box("dark", c + Vector3(0, .14, .29), Vector3(.26, .17, .05))
	for i in range(3):
		box("ember", c + Vector3((i - 1) * .075, .14, .31), Vector3(.045, .12, .02))
	box("brass", c + Vector3(0, .24, .305), Vector3(.30, .03, .04))
	for side in [-1.0, 1.0]:
		var base := Vector3(side * .15, 1.98, .58)
		var top := Vector3(side * .21, 2.60, .64)
		tube("burnt", base, top, .075, .07, 8)
		tube("brass", top - Vector3(0, .05, 0), top + Vector3(0, .04, 0), .10, .10, 8)
		tube("soot", top + Vector3(0, .04, 0), top + Vector3(0, .08, 0), .085, .08, 8)

func build_head(body: Node3D) -> void:
	begin_part("Head", Vector3(0, 2.19, -.03), body)
	tube("burnt", Vector3(0, 2.04, -.02), Vector3(0, 2.24, -.05), .16, .15, 8)
	# The head is a gas lamp: flared glass panes in an iron cage, a glowing mantle over a
	# brass burner with a reflector behind it. The vent cap stays small so the lit glass
	# still shows from the high game camera.
	var c := Vector3(0, 2.47, -.06)
	hull("brass", Vector3(0, 2.25, -.06), Vector2(.20, .20), Vector2(.23, .23), .08)
	hull("iron", Vector3(0, 2.305, -.06), Vector2(.21, .21), Vector2(.21, .21), .03)
	tube("brass", Vector3(0, 2.32, -.06), Vector3(0, 2.37, -.06), .055, .04, 6)
	rock("lamp", c, Vector3(.1, .14, .1), 6, 3)
	tube("brass", c + Vector3(0, 0, .13), c + Vector3(0, 0, .15), .12, .12, 8)
	hull("glass", c, Vector2(.27, .27), Vector2(.20, .20), .32)
	var outline := [Vector2(-.7, -1), Vector2(.7, -1), Vector2(1, -.65), Vector2(1, .65),
		Vector2(.7, 1), Vector2(-.7, 1), Vector2(-1, .65), Vector2(-1, -.65)]
	for p: Vector2 in outline:
		tube("iron", c + Vector3(p.x * .20, -.16, p.y * .20), c + Vector3(p.x * .27, .16, p.y * .27), .017, .017, 4)
	hull("brass", c, Vector2(.24, .24), Vector2(.24, .24), .02)
	hull("brass", c + Vector3(0, .175, 0), Vector2(.29, .29), Vector2(.29, .29), .03)
	for p: Vector2 in outline:
		tube("iron", c + Vector3(p.x * .27, .16, p.y * .27), c + Vector3(0, .26, 0), .014, .014, 4)
	hull("iron", c + Vector3(0, .23, 0), Vector2(.06, .06), Vector2(.15, .15), .1)
	tube("iron", c + Vector3(0, .27, 0), c + Vector3(0, .32, 0), .045, .045, 6)
	hull("iron", c + Vector3(0, .335, 0), Vector2(.03, .03), Vector2(.09, .09), .035)
	rock("gold", c + Vector3(0, .38, 0), Vector3(.022, .035, .022), 4, 2)
	finish_part()

func build_arm(body: Node3D, side: float) -> void:
	var label := "Left" if side < 0 else "Right"
	begin_part(label + "Arm", Vector3(side * .78, 1.96, 0), body)
	rock("burnt", Vector3(side * .80, 1.90, .02), Vector3(.22, .22, .22), 8, 4)
	# Chamfered pauldron in the chest's plate language over a painted company lame.
	hull("plate", Vector3(side * .99, 2.10, .02), Vector2(.27, .30), Vector2(.43, .45), .26)
	hull("iron", Vector3(side * .99, 2.25, .02), Vector2(.19, .22), Vector2(.27, .30), .05)
	hull("team", Vector3(side * .99, 1.90, .02), Vector2(.43, .45), Vector2(.45, .47), .14)
	hull("brass", Vector3(side * .99, 1.815, .02), Vector2(.46, .48), Vector2(.46, .48), .04)
	for i in range(5):
		rivet(Vector3(side * 1.445, 1.90, .02 + (i - 2) * .12))
		rivet(Vector3(side * .99 + (i - 2) * .11, 1.90, -.455))
	tube("burnt", Vector3(side * .97, 1.86, .02), Vector3(side * 1.0, 1.42, -.01), .15, .14, 8)
	# Outer piston that drives the punch.
	tube("brass", Vector3(side * 1.17, 1.86, .07), Vector3(side * 1.17, 1.60, .05), .055, .055, 6)
	tube("steel", Vector3(side * 1.17, 1.60, .05), Vector3(side * 1.16, 1.34, .01), .027, .027, 6)
	tube("brass", Vector3(side * .86, 1.36, -.02), Vector3(side * 1.14, 1.36, -.02), .13, .13, 10)
	finish_part()
	begin_part("Fist", Vector3(side * 1.00, 1.32, -.02), part)
	tube("brass", Vector3(side * 1.0, 1.30, -.04), Vector3(side * 1.02, 1.22, -.06), .2, .21, 8)
	# The original rune stone survives as the fist, clamped by iron bands.
	rock("stone", Vector3(side * 1.04, 1.04, -.09), Vector3(.37, .41, .36), 7, 4)
	rock("edge", Vector3(side * 1.04, .98, -.28), Vector3(.33, .25, .2), 7, 3)
	for band in [[1.20, .355], [.93, .385]]:
		tube("iron", Vector3(side * 1.04, band[0] - .035, -.09), Vector3(side * 1.04, band[0] + .035, -.09),
			band[1], band[1], 8)
	for j in range(4):
		var x := side * 1.04 + (j - 1.5) * .15
		box("iron", Vector3(x, .87, -.40), Vector3(.12, .15, .15))
		box("brass", Vector3(x, .87, -.48), Vector3(.13, .1, .03))
	box("dark", Vector3(side * 1.04, 1.07, -.445), Vector3(.40, .05, .03))
	box("rune", Vector3(side * 1.04, 1.07, -.46), Vector3(.33, .022, .02))
	finish_part()

func build_leg(side: float) -> void:
	var label := "Left" if side < 0 else "Right"
	begin_part(label + "Leg", Vector3(side * .34, .95, .07))
	rock("burnt", Vector3(side * .34, .92, .07), Vector3(.2, .2, .2), 8, 4)
	hull("iron", Vector3(side * .35, .74, .07), Vector2(.20, .20), Vector2(.17, .18), .36)
	tube("brass", Vector3(side * .19, .55, .03), Vector3(side * .51, .55, .03), .12, .12, 10)
	rock("plate", Vector3(side * .35, .56, -.12), Vector3(.13, .12, .07), 6, 3)
	hull("iron", Vector3(side * .36, .33, .06), Vector2(.17, .17), Vector2(.22, .24), .36)
	box("dark", Vector3(side * .36, .34, -.15), Vector3(.05, .2, .03))
	box("rune", Vector3(side * .36, .34, -.165), Vector3(.022, .15, .02))
	tube("brass", Vector3(side * .36, .84, .27), Vector3(side * .36, .60, .28), .05, .05, 6)
	tube("steel", Vector3(side * .36, .60, .28), Vector3(side * .36, .24, .27), .025, .025, 6)
	# Heavy boot: soot only where it meets the ground.
	hull("burnt", Vector3(side * .37, .10, -.08), Vector2(.25, .36), Vector2(.28, .42), .16)
	hull("brass", Vector3(side * .37, .09, -.45), Vector2(.2, .05), Vector2(.23, .06), .12)
	hull("soot", Vector3(side * .37, .012, -.08), Vector2(.285, .425), Vector2(.285, .425), .024)
	finish_part()

func begin_part(label: String, pivot: Vector3, parent: Node3D = null) -> void:
	if parent == null: parent = rig
	part = Node3D.new()
	part.name = label
	part.position = pivot - Vector3(parent.get_meta("pivot"))
	part.set_meta("pivot", pivot)
	parent.add_child(part)
	part.owner = rig
	origin = pivot
	surfaces.clear()

func finish_part() -> void:
	var mesh := ArrayMesh.new()
	for surface: SurfaceTool in surfaces.values():
		surface.index()
		surface.commit(mesh)
	var model := MeshInstance3D.new()
	model.name = "Mesh"
	model.mesh = mesh
	part.add_child(model)
	model.owner = rig

func tri(key: String, a: Vector3, b: Vector3, c: Vector3) -> void:
	var normal := (b-a).cross(c-a)
	if normal.length_squared() < .00000001: return
	var category: String = key if key in ["rune", "ember", "team", "glass", "lamp"] else "metal" if key in METAL else "stone"
	if not surfaces.has(category):
		var surface := SurfaceTool.new()
		surface.begin(Mesh.PRIMITIVE_TRIANGLES)
		surface.set_material(materials[category])
		surfaces[category] = surface
	var st: SurfaceTool = surfaces[category]
	for v in [a, c, b]:
		st.set_normal(normal.normalized())
		st.set_color(COLORS[key])
		st.add_vertex(v-origin)

## Convex polygon, wound so its normal points away from `inside`.
func face(key: String, points: Array, inside: Vector3) -> void:
	var a: Vector3 = points[0]
	var flip: bool = (points[1] - a).cross(points[2] - a).dot(a - inside) < 0
	for i in range(1, points.size() - 1):
		if flip: tri(key, a, points[i + 1], points[i])
		else: tri(key, a, points[i], points[i + 1])

func box(key: String, center: Vector3, size: Vector3, rotation := Vector3.ZERO) -> void:
	var basis := Basis.from_euler(rotation)
	var h := size * .5
	var corner := func(x: float, y: float, z: float) -> Vector3: return center + basis * Vector3(x * h.x, y * h.y, z * h.z)
	for axis in range(3):
		for sign in [-1.0, 1.0]:
			var quad := []
			for k in [[-1, -1], [1, -1], [1, 1], [-1, 1]]:
				var p := [0.0, 0.0, 0.0]
				p[axis] = sign
				p[(axis + 1) % 3] = k[0]
				p[(axis + 2) % 3] = k[1]
				quad.append(corner.call(p[0], p[1], p[2]))
			face(key, quad, center)

## Octagonal frustum, the chamfered block used for armour plates.
func hull(key: String, center: Vector3, top: Vector2, bottom: Vector2, height: float) -> void:
	var outline := [Vector2(-.7, -1), Vector2(.7, -1), Vector2(1, -.65), Vector2(1, .65),
		Vector2(.7, 1), Vector2(-.7, 1), Vector2(-1, .65), Vector2(-1, -.65)]
	var upper := []
	var lower := []
	for p: Vector2 in outline:
		upper.append(center + Vector3(p.x * top.x, height * .5, p.y * top.y))
		lower.append(center + Vector3(p.x * bottom.x, -height * .5, p.y * bottom.y))
	face(key, upper, center)
	face(key, lower, center)
	for i in range(outline.size()):
		var j := (i + 1) % outline.size()
		face(key, [upper[i], upper[j], lower[j], lower[i]], center)

func rivet(center: Vector3) -> void:
	rock("gold", center, Vector3(.024, .024, .024), 4, 2)

func rock(key: String, center: Vector3, radius: Vector3, segments: int = 7, rings: int = 4) -> void:
	for y in range(rings):
		var lo := -PI*.5 + PI*y/rings
		var hi := -PI*.5 + PI*(y+1)/rings
		for x in range(segments):
			var a := TAU*x/segments
			var b := TAU*(x+1)/segments
			var p := center+sphere(lo,a)*radius
			var q := center+sphere(hi,a)*radius
			var r := center+sphere(hi,b)*radius
			var s := center+sphere(lo,b)*radius
			tri(key,p,q,r)
			tri(key,p,r,s)

func sphere(latitude: float, angle: float) -> Vector3:
	return Vector3(cos(latitude)*cos(angle),sin(latitude),cos(latitude)*sin(angle))

func tube(key: String, a: Vector3, b: Vector3, r0: float, r1: float, sides: int) -> void:
	var basis := Basis(Quaternion(Vector3.UP,(b-a).normalized()))
	for i in range(sides):
		var u := basis*Vector3(cos(TAU*i/sides),0,sin(TAU*i/sides))
		var v := basis*Vector3(cos(TAU*(i+1)/sides),0,sin(TAU*(i+1)/sides))
		tri(key,a+u*r0,b+u*r1,b+v*r1)
		tri(key,a+u*r0,b+v*r1,a+v*r0)
		tri(key,a,a+u*r0,a+v*r0)
		tri(key,b,b+v*r1,b+u*r1)
