extends SceneTree
## Authored low-poly barracks soldiers. Feet Y=0, forward -Z; meshes are baked, not built at runtime.
## One neutral vertex-color surface + one team-color surface per articulated part.

const PALETTE := {
	"steel": Color("8fa8b5"), "edge": Color("d7e2dd"), "iron": Color("455765"),
	"dark": Color("202e36"), "leather": Color("604334"), "warm": Color("94704a"),
	"gold": Color("c6a264"), "linen": Color("d8cdac"), "skin": Color("bd9370"),
	"wood": Color("9a6b40"), "hair": Color("44372d"), "team": Color.WHITE,
	"pressure_red": Color("bd503d"),
}
var surfaces: Dictionary = {}
var materials: Dictionary = {}
var rig: Node3D
var part: Node3D
var origin := Vector3.ZERO

func _initialize() -> void:
	for key in ["neutral", "team"]:
		var mat := StandardMaterial3D.new()
		mat.resource_name = "team_cloth" if key == "team" else "soldier_vertex_colors"
		mat.vertex_color_use_as_albedo = true
		mat.vertex_color_is_srgb = true
		mat.albedo_color = Color("477491") if key == "team" else Color.WHITE
		mat.roughness = .78
		materials[key] = mat
	DirAccess.make_dir_recursive_absolute("res://units/models")
	var args := OS.get_cmdline_user_args()
	if "--archer-only" in args and "--knight-only" in args:
		push_error("Choose exactly one of --archer-only or --knight-only")
		quit(1)
		return
	if not "--archer-only" in args: build_knight()
	if not "--knight-only" in args: build_archer()
	print("PASS: baked steam assault infantry rig" if "--knight-only" in args else "PASS: baked pneumatic rifleman rig" if "--archer-only" in args else "PASS: baked steam assault infantry and pneumatic rifleman rigs")
	quit()

func begin_model(model_name: String) -> void:
	rig = Node3D.new()
	rig.name = model_name

func begin_part(part_name: String, pivot: Vector3) -> void:
	part = Node3D.new()
	part.name = part_name
	part.position = pivot
	rig.add_child(part)
	part.owner = rig
	origin = pivot
	surfaces.clear()

func finish_part() -> void:
	var mesh := ArrayMesh.new()
	for key in surfaces:
		var st: SurfaceTool = surfaces[key]
		st.index()
		st.commit(mesh)
	var model := MeshInstance3D.new()
	model.name = "Mesh"
	model.mesh = mesh
	part.add_child(model)
	model.owner = rig

func save_model(path: String) -> void:
	var scene := PackedScene.new()
	assert(scene.pack(rig) == OK)
	assert(ResourceSaver.save(scene, path) == OK)
	rig.free()

func surface(key: String) -> SurfaceTool:
	var category := "team" if key == "team" else "neutral"
	if not surfaces.has(category):
		var st := SurfaceTool.new()
		st.begin(Mesh.PRIMITIVE_TRIANGLES)
		st.set_material(materials[category])
		surfaces[category] = st
	return surfaces[category]

func triangle(key: String, a: Vector3, b: Vector3, c: Vector3) -> void:
	var st := surface(key)
	var normal := (b - a).cross(c - a).normalized()
	for v in [a, c, b]:
		st.set_normal(normal)
		st.set_color(PALETTE[key])
		st.add_vertex(v - origin)

func quad(key: String, a: Vector3, b: Vector3, c: Vector3, d: Vector3) -> void:
	triangle(key, a, b, c)
	triangle(key, a, c, d)

func primitive(key: String, mesh: Mesh, center: Vector3, basis := Basis.IDENTITY) -> void:
	var data := mesh.surface_get_arrays(0)
	var vertices: PackedVector3Array = data[Mesh.ARRAY_VERTEX]
	var normals: PackedVector3Array = data[Mesh.ARRAY_NORMAL]
	var indices: PackedInt32Array = data[Mesh.ARRAY_INDEX]
	var st := surface(key)
	for i in indices:
		st.set_normal((basis * normals[i]).normalized())
		st.set_color(PALETTE[key])
		st.add_vertex(basis * vertices[i] + center - origin)

func box(key: String, center: Vector3, size: Vector3, basis := Basis.IDENTITY) -> void:
	var mesh := BoxMesh.new()
	mesh.size = size
	primitive(key, mesh, center, basis)

func beam(key: String, a: Vector3, b: Vector3, width: float, depth: float) -> void:
	box(key, (a + b) * .5, Vector3(width, a.distance_to(b), depth), Basis(Quaternion(Vector3.UP, (b - a).normalized())))

func cylinder(key: String, center: Vector3, top: float, bottom: float, height: float, basis := Basis.IDENTITY) -> void:
	var mesh := CylinderMesh.new()
	mesh.top_radius = top
	mesh.bottom_radius = bottom
	mesh.height = height
	mesh.radial_segments = 8
	primitive(key, mesh, center, basis)

# Chamfered rectangular rings make broad, readable armor planes instead of cube torsos.
func chamfered(key: String, center: Vector3, bottom: Vector2, top: Vector2, height: float) -> void:
	var corners := [Vector2(-.72, -1), Vector2(.72, -1), Vector2(1, -.66), Vector2(1, .66),
		Vector2(.72, 1), Vector2(-.72, 1), Vector2(-1, .66), Vector2(-1, -.66)]
	for i in range(8):
		var a: Vector2 = corners[i]
		var b: Vector2 = corners[(i + 1) % 8]
		var lo_a := center + Vector3(a.x * bottom.x * .5, -height * .5, a.y * bottom.y * .5)
		var lo_b := center + Vector3(b.x * bottom.x * .5, -height * .5, b.y * bottom.y * .5)
		var hi_a := center + Vector3(a.x * top.x * .5, height * .5, a.y * top.y * .5)
		var hi_b := center + Vector3(b.x * top.x * .5, height * .5, b.y * top.y * .5)
		quad(key, lo_a, hi_a, hi_b, lo_b)
		triangle(key, center + Vector3(0, height * .5, 0), hi_b, hi_a)
		triangle(key, center - Vector3(0, height * .5, 0), lo_a, lo_b)

# Solid front-facing polygons for shield, tabard, cape, hood and sword profiles.
func plate(key: String, points: Array, front: float, back: float) -> void:
	var center := Vector2.ZERO
	for p: Vector2 in points: center += p
	center /= points.size()
	for i in range(points.size()):
		var a: Vector2 = points[i]
		var b: Vector2 = points[(i + 1) % points.size()]
		triangle(key, Vector3(center.x, center.y, front), Vector3(b.x, b.y, front), Vector3(a.x, a.y, front))
		triangle(key, Vector3(center.x, center.y, back), Vector3(a.x, a.y, back), Vector3(b.x, b.y, back))
		quad(key, Vector3(a.x, a.y, front), Vector3(b.x, b.y, front), Vector3(b.x, b.y, back), Vector3(a.x, a.y, back))

func legs(armored: bool) -> void:
	for side in [-1.0, 1.0]:
		var x: float = side * .235
		begin_part("LeftLeg" if side < 0 else "RightLeg", Vector3(x, 1.04, 0))
		chamfered("dark", Vector3(x, .71, 0), Vector2(.28, .29), Vector2(.32, .34), .67)
		chamfered("leather", Vector3(x, .26, -.055), Vector2(.32, .49), Vector2(.31, .31), .50)
		if armored:
			chamfered("steel", Vector3(x, .61, -.14), Vector2(.25, .15), Vector2(.31, .16), .32)
			chamfered("edge", Vector3(x, .80, -.15), Vector2(.28, .14), Vector2(.27, .13), .12)
			box("iron", Vector3(x, .12, -.12), Vector3(.33, .13, .40))
		else:
			for y in [.34, .45]: box("warm", Vector3(x, y, -.015), Vector3(.326, .045, .34))
		box("dark", Vector3(x, .035, -.085), Vector3(.34, .07, .46))
		finish_part()

func build_knight() -> void:
	begin_model("Rig")
	legs(true)
	begin_part("Body", Vector3(0, 1.22, 0))
	# Felt tunic under a strapped, riveted iron cuirass, matching the rifleman's uniform.
	chamfered("team", Vector3(0, 1.04, 0), Vector2(.82, .57), Vector2(.65, .47), .46)
	chamfered("team", Vector3(0, 1.63, 0), Vector2(.67, .48), Vector2(.91, .58), .69)
	chamfered("leather", Vector3(0, 1.63, 0), Vector2(.70, .50), Vector2(.94, .60), .51)
	plate("iron", [Vector2(-.33, 1.40), Vector2(.33, 1.40), Vector2(.43, 1.86), Vector2(0, 1.96), Vector2(-.43, 1.86)], -.347, -.28)
	plate("steel", [Vector2(-.11, 1.44), Vector2(.11, 1.44), Vector2(.17, 1.85), Vector2(0, 1.92), Vector2(-.17, 1.85)], -.372, -.348)
	for side in [-1.0, 1.0]:
		beam("dark", Vector3(side * .29, 1.93, -.29), Vector3(side * .23, 1.31, -.36), .095, .045)
		beam("leather", Vector3(side * .29, 1.93, -.28), Vector3(side * .29, 1.93, .35), .105, .06)
		box("gold", Vector3(side * .25, 1.56, -.403), Vector3(.13, .10, .035))
		box("dark", Vector3(side * .25, 1.56, -.425), Vector3(.075, .045, .014))
		for y in [1.46, 1.76, 1.85]: cylinder("gold", Vector3(side * .33, y, -.368), .026, .026, .027, Basis(Vector3.RIGHT, PI / 2))
	chamfered("dark", Vector3(0, 1.29, 0), Vector2(.79, .60), Vector2(.79, .60), .13)
	box("gold", Vector3(.04, 1.29, -.333), Vector3(.17, .12, .04))
	box("dark", Vector3(.04, 1.29, -.357), Vector3(.10, .06, .014))
	cylinder("dark", Vector3(0, 1.99, 0), .21, .23, .17)
	# The small boiler and exhaust sit behind the harness, leaving the two hip vessels distinct.
	chamfered("dark", Vector3(0, 1.62, .36), Vector2(.59, .21), Vector2(.62, .25), .71)
	chamfered("iron", Vector3(0, 1.61, .47), Vector2(.47, .22), Vector2(.51, .22), .60)
	for y in [1.39, 1.86]: box("gold", Vector3(0, y, .60), Vector3(.49, .055, .04))
	cylinder("gold", Vector3(.28, 1.92, .48), .072, .072, .30)
	cylinder("dark", Vector3(.28, 2.08, .48), .084, .084, .08)
	cylinder("iron", Vector3(.28, 2.13, .48), .086, .086, .03)
	cylinder("dark", Vector3(.28, 2.15, .48), .058, .058, .012)
	finish_part()
	var body := part
	knight_marker(body, "SteamVent", Vector3(.28, 2.17, .48) - body.position)
	# Rear-facing pressure gauge: a real articulated needle points into a painted red sector.
	var gauge_center := Vector3(-.09, 1.82, .64)
	begin_part("PressureGauge", gauge_center)
	cylinder("gold", gauge_center, .183, .183, .065, Basis(Vector3.RIGHT, PI / 2))
	cylinder("linen", gauge_center + Vector3(0, 0, .038), .151, .151, .016, Basis(Vector3.RIGHT, PI / 2))
	for i in range(9):
		var angle := deg_to_rad(-120.0 + i * 30.0)
		var radial := Vector3(sin(angle), cos(angle), 0)
		beam("pressure_red" if i >= 6 else "dark", gauge_center + radial * .112 + Vector3(0, 0, .051), gauge_center + radial * .143 + Vector3(0, 0, .051), .018, .018)
	finish_part()
	var gauge := part
	begin_part("Needle", gauge_center + Vector3(0, 0, .064))
	beam("pressure_red", gauge_center + Vector3(0, -.024, .064), gauge_center + Vector3(0, .119, .064), .018, .015)
	cylinder("gold", gauge_center + Vector3(0, 0, .066), .034, .034, .025, Basis(Vector3.RIGHT, PI / 2))
	finish_part()
	knight_reparent(part, gauge)
	part.rotation.z = deg_to_rad(120)
	knight_reparent(gauge, body)
	for side in [-1.0, 1.0]: build_knight_winch(side)
	begin_part("Head", Vector3(0, 2.11, 0))
	chamfered("skin", Vector3(0, 2.28, -.015), Vector2(.43, .39), Vector2(.46, .40), .36)
	chamfered("dark", Vector3(0, 2.31, .08), Vector2(.56, .44), Vector2(.57, .46), .38)
	chamfered("iron", Vector3(0, 2.48, .025), Vector2(.62, .58), Vector2(.40, .41), .24)
	box("steel", Vector3(0, 2.365, -.24), Vector3(.62, .055, .19))
	box("leather", Vector3(0, 2.32, -.252), Vector3(.50, .11, .045))
	for side in [-1.0, 1.0]:
		cylinder("gold", Vector3(side * .12, 2.32, -.292), .079, .079, .055, Basis(Vector3.RIGHT, PI / 2))
		cylinder("dark", Vector3(side * .12, 2.32, -.328), .056, .056, .023, Basis(Vector3.RIGHT, PI / 2))
		box("gold", Vector3(side * .267, 2.43, -.15), Vector3(.038, .045, .035))
	box("steel", Vector3(0, 2.17, -.242), Vector3(.31, .16, .06))
	for x in [-.09, -.03, .03, .09]: box("dark", Vector3(x, 2.17, -.277), Vector3(.023, .084, .012))
	finish_part()
	for side in [-1.0, 1.0]:
		var x: float = side * .54
		begin_part("LeftArm" if side < 0 else "RightArm", Vector3(x, 1.82, 0))
		chamfered("team", Vector3(x, 1.55, 0), Vector2(.24, .30), Vector2(.27, .32), .59)
		chamfered("iron", Vector3(x, 1.82, 0), Vector2(.40, .51), Vector2(.35, .42), .26)
		box("gold", Vector3(x, 1.71, -.245), Vector3(.40, .045, .035))
		chamfered("iron", Vector3(x, 1.36, -.025), Vector2(.27, .29), Vector2(.31, .31), .25)
		chamfered("steel", Vector3(x, 1.20, -.06), Vector2(.24, .26), Vector2(.29, .29), .17)
		if side < 0: shield(x - .08)
		else: sword(x + .07)
		finish_part()
	save_model("res://units/models/KnightRig.tscn")

func knight_reparent(node: Node3D, parent: Node3D) -> void:
	var position := node.position - parent.position
	node.owner = null
	for child in node.find_children("*", "", true, false): child.owner = null
	rig.remove_child(node)
	parent.add_child(node)
	node.position = position
	node.owner = rig
	for child in node.find_children("*", "", true, false): child.owner = rig

func knight_marker(parent: Node3D, marker_name: String, position: Vector3) -> void:
	var marker := Marker3D.new()
	marker.name = marker_name
	parent.add_child(marker)
	marker.position = position
	marker.owner = rig

func build_knight_winch(side: float) -> void:
	var center := Vector3(side * .64, 1.23, .24)
	begin_part("LeftWinch" if side < 0 else "RightWinch", center)
	box("dark", center + Vector3(-side * .07, 0, .17), Vector3(.39, .53, .68))
	box("iron", center + Vector3(0, -.02, .21), Vector3(.37, .43, .60))
	# Brass vessel is separate from the winch case, forward of its cable drum.
	cylinder("gold", center + Vector3(0, .04, -.22), .154, .154, .44)
	cylinder("warm", center + Vector3(0, .29, -.22), .08, .154, .09)
	cylinder("gold", center + Vector3(0, -.21, -.22), .154, .09, .07)
	for y in [-.13, .18]: cylinder("dark", center + Vector3(0, y, -.22), .164, .164, .065)
	box("gold", center + Vector3(0, .36, -.22), Vector3(.18, .034, .035))
	# Raised seam, rivets and fairlead make the case readable from the isometric camera.
	box("gold", center + Vector3(side * .20, .205, .20), Vector3(.035, .045, .57))
	for z in [-.02, .43]:
		for y in [-.17, .16]: cylinder("gold", center + Vector3(side * .205, y, z), .027, .027, .025, Basis(Vector3.FORWARD, PI / 2))
	cylinder("iron", center + Vector3(0, -.10, -.34), .08, .08, .22, Basis(Vector3.RIGHT, PI / 2))
	cylinder("dark", center + Vector3(0, -.10, -.465), .052, .052, .035, Basis(Vector3.RIGHT, PI / 2))
	finish_part()
	var winch := part
	knight_marker(winch, "CableExit", Vector3(0, -.10, -.49))
	var gear_center := center + Vector3(side * .25, .015, .22)
	begin_part("Gear", gear_center)
	cylinder("gold", gear_center, .224, .224, .065, Basis(Vector3.FORWARD, PI / 2))
	cylinder("dark", gear_center + Vector3(side * .045, 0, 0), .175, .175, .025, Basis(Vector3.FORWARD, PI / 2))
	for i in range(12):
		var angle := i * TAU / 12
		box("gold", gear_center + Vector3(0, cos(angle) * .241, sin(angle) * .241), Vector3(.08, .075, .07), Basis(Vector3.RIGHT, angle))
	for i in range(5):
		var radial := Vector3(0, cos(i * TAU / 5), sin(i * TAU / 5))
		beam("gold", gear_center + Vector3(side * .064, 0, 0), gear_center + Vector3(side * .064, 0, 0) + radial * .17, .035, .035)
	# Visible wire wound in concentric grooves on the outer spool face.
	for radius in [.095, .118, .141]:
		for i in range(16):
			var a := gear_center + Vector3(side * .075, cos(i * TAU / 16) * radius, sin(i * TAU / 16) * radius)
			var b := gear_center + Vector3(side * .075, cos((i + 1) * TAU / 16) * radius, sin((i + 1) * TAU / 16) * radius)
			beam("steel", a, b, .012, .012)
	cylinder("steel", gear_center + Vector3(side * .082, 0, 0), .05, .05, .05, Basis(Vector3.FORWARD, PI / 2))
	finish_part()
	knight_reparent(part, winch)

func shield(x: float) -> void:
	# Heater shield: broad shoulders, tapered point, pale brass rim and a bold chevron.
	var outline := [Vector2(x, .55), Vector2(x + .39, .87), Vector2(x + .42, 1.63), Vector2(x - .42, 1.63), Vector2(x - .39, .87)]
	plate("gold", outline, -.52, -.37)
	plate("team", [Vector2(x, .63), Vector2(x + .32, .91), Vector2(x + .35, 1.57), Vector2(x - .35, 1.57), Vector2(x - .32, .91)], -.54, -.522)
	beam("linen", Vector3(x - .29, 1.27, -.555), Vector3(x, 1.10, -.555), .085, .024)
	beam("linen", Vector3(x, 1.10, -.555), Vector3(x + .29, 1.27, -.555), .085, .024)
	box("gold", Vector3(x, 1.41, -.56), Vector3(.09, .09, .03))
	for side in [-1.0, 1.0]:
		for y in [.96, 1.50]: box("edge", Vector3(x + side * .37, y, -.548), Vector3(.042, .042, .025))

func sword(x: float) -> void:
	box("leather", Vector3(x, 1.19, -.15), Vector3(.11, .25, .12))
	box("gold", Vector3(x, 1.35, -.15), Vector3(.39, .075, .15))
	plate("edge", [Vector2(x - .07, 1.39), Vector2(x + .07, 1.39), Vector2(x + .057, 2.14), Vector2(x, 2.32), Vector2(x - .057, 2.14)], -.19, -.11)
	beam("steel", Vector3(x, 1.40, -.199), Vector3(x, 2.16, -.199), .023, .016)
	cylinder("gold", Vector3(x, 1.04, -.15), .095, .065, .085)

func build_archer() -> void:
	begin_model("Rig")
	legs(false)
	begin_part("Body", Vector3(0, 1.22, 0))
	chamfered("team", Vector3(0, 1.05, 0), Vector2(.79, .56), Vector2(.62, .43), .50)
	chamfered("team", Vector3(0, 1.62, 0), Vector2(.61, .45), Vector2(.80, .52), .73)
	# A short infantry coat and broad tank harness keep the human silhouette readable.
	chamfered("leather", Vector3(0, 1.59, .005), Vector2(.64, .47), Vector2(.76, .53), .47)
	for side in [-1.0, 1.0]:
		beam("dark", Vector3(side * .25, 1.91, -.25), Vector3(side * .23, 1.30, -.28), .115, .045)
		box("gold", Vector3(side * .24, 1.61, -.306), Vector3(.14, .11, .035))
		box("dark", Vector3(side * .24, 1.61, -.328), Vector3(.082, .055, .013))
		beam("leather", Vector3(side * .25, 1.92, -.24), Vector3(side * .25, 1.92, .34), .11, .07)
	chamfered("dark", Vector3(0, 1.26, 0), Vector2(.70, .52), Vector2(.70, .52), .11)
	box("gold", Vector3(-.17, 1.26, -.29), Vector3(.12, .10, .035))
	for side in [-1.0, 1.0]:
		chamfered("leather", Vector3(side * .36, 1.15, .02), Vector2(.20, .22), Vector2(.25, .24), .25)
		box("gold", Vector3(side * .36, 1.20, -.105), Vector3(.045, .055, .025))
	# One clearly visible pressure vessel replaces the quiver. The upper valve can be reached.
	box("dark", Vector3(0, 1.59, .31), Vector3(.55, .91, .16))
	cylinder("iron", Vector3(0, 1.62, .51), .255, .255, .84)
	cylinder("steel", Vector3(0, 2.08, .51), .16, .255, .14)
	cylinder("iron", Vector3(0, 1.14, .51), .255, .15, .13)
	for y in [1.29, 1.91]:
		cylinder("gold", Vector3(0, y, .51), .274, .274, .095)
		box("dark", Vector3(0, y, .795), Vector3(.10, .075, .025))
	cylinder("dark", Vector3(0, 2.19, .51), .09, .11, .09)
	beam("gold", Vector3(0, 2.19, .51), Vector3(-.36, 2.19, .31), .075, .075)
	beam("gold", Vector3(-.36, 2.19, .31), Vector3(-.36, 1.96, .31), .075, .075)
	cylinder("iron", Vector3(-.36, 1.96, .31), .125, .115, .10)
	cylinder("gold", Vector3(-.36, 2.025, .31), .095, .095, .045)
	# A rear-facing pressure gauge and right-side hose union make the mechanism legible.
	var forward_axis := Basis(Vector3.RIGHT, PI * .5)
	cylinder("gold", Vector3(.16, 2.09, .68), .105, .105, .065, forward_axis)
	cylinder("linen", Vector3(.16, 2.09, .72), .083, .083, .014, forward_axis)
	beam("dark", Vector3(.16, 2.09, .732), Vector3(.12, 2.145, .732), .015, .012)
	beam("gold", Vector3(.22, 1.90, .51), Vector3(.31, 1.90, .44), .09, .09)
	finish_part()
	archer_marker(part, "PumpButton", Vector3(-.36, 2.052, .31))
	archer_marker(part, "HoseTankExit", Vector3(.31, 1.90, .44))
	begin_part("Head", Vector3(0, 2.10, 0))
	cylinder("skin", Vector3(0, 2.10, 0), .17, .18, .23)
	chamfered("skin", Vector3(0, 2.30, -.025), Vector2(.40, .34), Vector2(.48, .39), .37)
	chamfered("hair", Vector3(0, 2.47, .055), Vector2(.51, .43), Vector2(.48, .41), .14)
	# Brass-rimmed goggles and a low leather service cap, without a heroic oversized crest.
	chamfered("leather", Vector3(0, 2.57, .025), Vector2(.61, .54), Vector2(.53, .48), .22)
	chamfered("dark", Vector3(0, 2.46, -.045), Vector2(.63, .62), Vector2(.63, .62), .07)
	box("leather", Vector3(0, 2.435, -.35), Vector3(.55, .055, .23), Basis(Vector3.RIGHT, -.1))
	box("gold", Vector3(0, 2.565, -.253), Vector3(.10, .09, .025))
	for side in [-1.0, 1.0]:
		cylinder("gold", Vector3(side * .13, 2.36, -.25), .112, .112, .07, forward_axis)
		cylinder("dark", Vector3(side * .13, 2.36, -.292), .085, .085, .018, forward_axis)
		box("steel", Vector3(side * .13 - .023, 2.386, -.308), Vector3(.036, .032, .012))
		box("leather", Vector3(side * .25, 2.36, -.005), Vector3(.035, .075, .35))
	box("gold", Vector3(0, 2.36, -.285), Vector3(.065, .035, .045))
	chamfered("skin", Vector3(0, 2.27, -.25), Vector2(.075, .13), Vector2(.065, .07), .12)
	box("hair", Vector3(0, 2.18, -.209), Vector3(.18, .035, .022))
	chamfered("linen", Vector3(0, 2.05, -.005), Vector2(.54, .46), Vector2(.44, .40), .13)
	finish_part()
	# The gun is independent of either arm: both hands follow its grip markers, and the left
	# hand can leave the forestock to press the tank valve while the right hand keeps hold.
	begin_part("Rifle", Vector3(.13, 1.68, -.40))
	chamfered("wood", Vector3(.13, 1.65, -.045), Vector2(.17, .40), Vector2(.19, .40), .20)
	box("gold", Vector3(.13, 1.65, .16), Vector3(.19, .235, .045))
	beam("wood", Vector3(.13, 1.65, -.13), Vector3(.13, 1.72, -.46), .15, .16)
	box("iron", Vector3(.13, 1.74, -.38), Vector3(.205, .16, .30))
	box("gold", Vector3(.244, 1.74, -.37), Vector3(.035, .12, .23))
	box("dark", Vector3(.13, 1.62, -.30), Vector3(.10, .105, .12))
	box("gold", Vector3(.13, 1.58, -.29), Vector3(.075, .025, .17))
	box("wood", Vector3(.13, 1.69, -.74), Vector3(.15, .12, .58))
	cylinder("dark", Vector3(.13, 1.78, -.98), .073, .073, 1.20, forward_axis)
	for z in [-.48, -.79, -1.07, -1.53]:
		cylinder("gold", Vector3(.13, 1.78, z), .086, .086, .055, forward_axis)
	cylinder("iron", Vector3(.13, 1.78, -1.61), .10, .10, .10, forward_axis)
	cylinder("dark", Vector3(.13, 1.78, -1.666), .071, .071, .012, forward_axis)
	box("gold", Vector3(.13, 1.895, -1.48), Vector3(.035, .10, .05))
	box("iron", Vector3(.13, 1.87, -.49), Vector3(.12, .065, .06))
	# The little pressure chamber below the lock is distinct from the long musket barrel.
	cylinder("gold", Vector3(.25, 1.68, -.44), .072, .072, .24, forward_axis)
	beam("gold", Vector3(.25, 1.68, -.32), Vector3(.29, 1.65, -.24), .075, .075)
	finish_part()
	archer_marker(part, "Muzzle", Vector3(.13, 1.78, -1.68))
	archer_marker(part, "LeftGrip", Vector3(.02, 1.66, -.70))
	archer_marker(part, "RightGrip", Vector3(.21, 1.65, -.25))
	archer_marker(part, "HoseGunInlet", Vector3(.29, 1.65, -.24))
	for side in [-1.0, 1.0]:
		var x: float = side * .45
		begin_part("LeftArm" if side < 0 else "RightArm", Vector3(x, 1.81, 0))
		chamfered("team", Vector3(x, 1.61, 0), Vector2(.22, .25), Vector2(.30, .32), .40)
		chamfered("leather", Vector3(x, 1.80, 0), Vector2(.30, .32), Vector2(.32, .34), .13)
		finish_part()
		var shoulder := part
		begin_part("Elbow", Vector3(x, 1.38, 0))
		chamfered("team", Vector3(x, 1.23, 0), Vector2(.19, .22), Vector2(.23, .25), .30)
		chamfered("leather", Vector3(x, 1.045, 0), Vector2(.22, .24), Vector2(.22, .24), .13)
		box("gold", Vector3(x, 1.085, -.13), Vector3(.21, .035, .022))
		chamfered("warm", Vector3(x, .935, -.005), Vector2(.18, .22), Vector2(.20, .23), .15)
		finish_part()
		part.owner = null
		part.get_node("Mesh").owner = null
		part.reparent(shoulder, false)
		part.owner = rig
		part.get_node("Mesh").owner = rig
		part.position = Vector3(0, -.43, 0)
		var hand := Marker3D.new()
		hand.name = "Hand"
		hand.position = Vector3(0, -.47, 0)
		part.add_child(hand)
		hand.owner = rig
		archer_rest_arm(shoulder, part, Vector3(.02, 1.66, -.70) if side < 0 else Vector3(.21, 1.65, -.25), side)
	archer_hose()
	save_model("res://units/models/ArcherRig.tscn")

# Archer-only helpers intentionally leave the shared knight/workshop primitives unchanged.
func archer_marker(parent: Node3D, marker_name: String, world_position: Vector3) -> void:
	var marker := Marker3D.new()
	marker.name = marker_name
	marker.position = world_position - parent.position
	parent.add_child(marker)
	marker.owner = rig

func archer_rest_arm(shoulder: Node3D, elbow: Node3D, destination: Vector3, side: float) -> void:
	var reach := destination - shoulder.position
	var distance := minf(reach.length(), .899)
	var direction := reach.normalized()
	var bend := Vector3(side * .65, -.9, .15)
	bend = (bend - direction * bend.dot(direction)).normalized()
	var along := (.43 * .43 - .47 * .47 + distance * distance) / (2.0 * distance)
	var height := sqrt(maxf(0.0, .43 * .43 - along * along))
	var upper := direction * along + bend * height
	shoulder.quaternion = Quaternion(Vector3.DOWN, upper.normalized())
	var lower_local := shoulder.basis.inverse() * (reach - upper)
	elbow.quaternion = Quaternion(Vector3.DOWN, lower_local.normalized())

func archer_hose() -> void:
	var hose := Node3D.new()
	hose.name = "Hose"
	rig.add_child(hose)
	hose.owner = rig
	# The twelve baked cylinders are reused by the animation, following both live endpoints.
	var hose_material := StandardMaterial3D.new()
	hose_material.resource_name = "pneumatic_hose_rubber"
	hose_material.albedo_color = Color("242925")
	hose_material.roughness = .93
	var hose_mesh := CylinderMesh.new()
	hose_mesh.top_radius = .033
	hose_mesh.bottom_radius = .033
	hose_mesh.height = 1
	hose_mesh.radial_segments = 6
	hose_mesh.material = hose_material
	var points: Array[Vector3] = []
	for i in range(13):
		var t := float(i) / 12.0
		points.append(Vector3(.31, 1.90, .44).bezier_interpolate(Vector3(.83, 1.68, .42), Vector3(.80, 1.19, -.14), Vector3(.29, 1.65, -.24), t))
	for i in range(12):
		var segment := MeshInstance3D.new()
		segment.name = "Segment%d" % i
		segment.mesh = hose_mesh
		var direction := points[i + 1] - points[i]
		segment.position = (points[i + 1] + points[i]) * .5
		segment.quaternion = Quaternion(Vector3.UP, direction.normalized())
		segment.scale.y = direction.length() * 1.025
		hose.add_child(segment)
		segment.owner = rig
