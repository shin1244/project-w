extends SceneTree
## Authored low-poly barracks soldiers. Feet Y=0, forward -Z; meshes are baked, not built at runtime.
## One neutral vertex-color surface + one team-color surface per articulated part.

const PALETTE := {
	"steel": Color("8fa8b5"), "edge": Color("d7e2dd"), "iron": Color("455765"),
	"dark": Color("202e36"), "leather": Color("604334"), "warm": Color("94704a"),
	"gold": Color("c6a264"), "linen": Color("d8cdac"), "skin": Color("bd9370"),
	"wood": Color("9a6b40"), "hair": Color("44372d"), "team": Color.WHITE,
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
	build_knight()
	build_archer()
	print("PASS: baked tier-one knight and archer rigs")
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
	chamfered("iron", Vector3(0, 1.13, 0), Vector2(.83, .57), Vector2(.64, .46), .41)
	chamfered("steel", Vector3(0, 1.61, 0), Vector2(.69, .48), Vector2(.99, .59), .67)
	# Breastplate center ridge and two cloth panels leave the belt and articulated knees visible.
	plate("edge", [Vector2(-.27, 1.40), Vector2(.27, 1.40), Vector2(.36, 1.83), Vector2(0, 1.97), Vector2(-.36, 1.83)], -.308, -.275)
	plate("team", [Vector2(-.24, .83), Vector2(0, .73), Vector2(.24, .83), Vector2(.21, 1.70), Vector2(-.21, 1.70)], -.343, -.313)
	box("linen", Vector3(0, 1.51, -.365), Vector3(.055, .22, .022))
	box("linen", Vector3(0, 1.55, -.365), Vector3(.19, .055, .022))
	chamfered("leather", Vector3(0, 1.29, 0), Vector2(.78, .59), Vector2(.78, .59), .115)
	box("gold", Vector3(.06, 1.29, -.32), Vector3(.17, .13, .04))
	box("dark", Vector3(.06, 1.29, -.345), Vector3(.10, .07, .013))
	chamfered("leather", Vector3(.39, 1.12, .10), Vector2(.22, .21), Vector2(.25, .23), .25)
	cylinder("dark", Vector3(0, 1.99, 0), .23, .24, .19)
	# Short surcoat down the back. No oversized cape or plume reserved for later heroes.
	plate("team", [Vector2(-.40, 1.12), Vector2(.40, 1.12), Vector2(.43, 1.78), Vector2(-.43, 1.78)], .25, .30)
	finish_part()
	begin_part("Head", Vector3(0, 2.11, 0))
	chamfered("steel", Vector3(0, 2.29, .015), Vector2(.61, .54), Vector2(.57, .51), .43)
	chamfered("edge", Vector3(0, 2.55, .015), Vector2(.59, .53), Vector2(.30, .29), .12)
	box("iron", Vector3(0, 2.40, -.268), Vector3(.53, .12, .045))
	box("dark", Vector3(0, 2.405, -.296), Vector3(.43, .045, .02))
	box("edge", Vector3(0, 2.34, -.315), Vector3(.065, .30, .07))
	for side in [-1.0, 1.0]:
		for i in range(3): box("dark", Vector3(side * (.10 + i * .052), 2.235, -.282), Vector3(.021, .052, .018))
		box("gold", Vector3(side * .285, 2.38, -.13), Vector3(.035, .06, .09))
	box("gold", Vector3(0, 2.628, .015), Vector3(.095, .035, .37))
	finish_part()
	for side in [-1.0, 1.0]:
		var x: float = side * .54
		begin_part("LeftArm" if side < 0 else "RightArm", Vector3(x, 1.82, 0))
		chamfered("dark", Vector3(x, 1.55, 0), Vector2(.24, .30), Vector2(.27, .32), .59)
		chamfered("steel", Vector3(x, 1.82, 0), Vector2(.40, .51), Vector2(.35, .42), .26)
		box("edge", Vector3(x, 1.71, -.245), Vector3(.40, .07, .035))
		chamfered("iron", Vector3(x, 1.36, -.025), Vector2(.27, .29), Vector2(.31, .31), .25)
		chamfered("steel", Vector3(x, 1.20, -.06), Vector2(.24, .26), Vector2(.29, .29), .17)
		if side < 0: shield(x - .08)
		else: sword(x + .07)
		finish_part()
	save_model("res://units/models/KnightRig.tscn")

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
	chamfered("leather", Vector3(0, 1.58, -.015), Vector2(.64, .48), Vector2(.75, .51), .50)
	# Reinforced leather vest, cross-body quiver strap and a short split shoulder cape.
	beam("warm", Vector3(-.32, 1.88, -.30), Vector3(.30, 1.34, -.285), .12, .06)
	box("gold", Vector3(.035, 1.58, -.335), Vector3(.13, .12, .035))
	chamfered("dark", Vector3(0, 1.26, 0), Vector2(.70, .52), Vector2(.70, .52), .11)
	box("gold", Vector3(-.17, 1.26, -.29), Vector3(.12, .10, .035))
	chamfered("warm", Vector3(-.36, 1.15, .02), Vector2(.19, .21), Vector2(.23, .23), .24)
	plate("team", [Vector2(-.49, 1.26), Vector2(-.06, 1.16), Vector2(.10, 1.31), Vector2(.44, 1.27), Vector2(.47, 1.89), Vector2(-.47, 1.89)], .245, .33)
	# The quiver remains readable from the overhead game camera.
	var tilt := Basis(Vector3.FORWARD, -.22)
	cylinder("leather", Vector3(.28, 1.51, .39), .17, .12, .91, tilt)
	cylinder("gold", Vector3(.37, 1.94, .39), .18, .18, .075, tilt)
	for i in range(4):
		var x := .24 + i * .065
		var tip := Vector3(x + .08, 2.23 + (i % 2) * .08, .40)
		beam("wood", Vector3(x - .08, 1.60, .40), tip, .022, .022)
		box("linen", tip - Vector3(.012, .075, 0), Vector3(.06, .13, .03), tilt)
	finish_part()
	begin_part("Head", Vector3(0, 2.10, 0))
	cylinder("skin", Vector3(0, 2.10, 0), .17, .18, .23)
	chamfered("team", Vector3(0, 2.31, .035), Vector2(.62, .55), Vector2(.61, .56), .46)
	chamfered("team", Vector3(0, 2.59, .05), Vector2(.63, .57), Vector2(.18, .24), .15)
	# Deep dark hood opening, angular face, brow and nose rather than a spherical head.
	plate("dark", [Vector2(-.25, 2.12), Vector2(.25, 2.12), Vector2(.25, 2.44), Vector2(0, 2.55), Vector2(-.25, 2.44)], -.277, -.238)
	chamfered("skin", Vector3(0, 2.29, -.22), Vector2(.28, .17), Vector2(.37, .19), .30)
	box("hair", Vector3(0, 2.433, -.324), Vector3(.33, .065, .036))
	for side in [-1.0, 1.0]: box("dark", Vector3(side * .09, 2.345, -.33), Vector3(.075, .026, .016))
	chamfered("skin", Vector3(0, 2.28, -.35), Vector2(.07, .09), Vector2(.065, .045), .115)
	box("hair", Vector3(0, 2.17, -.307), Vector3(.17, .035, .022))
	# Scarf makes the hood silhouette distinct from the footman's metal gorget.
	chamfered("linen", Vector3(0, 2.04, -.005), Vector2(.52, .46), Vector2(.43, .40), .10)
	finish_part()
	for side in [-1.0, 1.0]:
		var x: float = side * .45
		begin_part("LeftArm" if side < 0 else "RightArm", Vector3(x, 1.81, 0))
		chamfered("team", Vector3(x, 1.59, -.01), Vector2(.22, .27), Vector2(.30, .34), .51)
		chamfered("leather", Vector3(x, 1.36, -.05), Vector2(.23, .26), Vector2(.27, .29), .24)
		for y in [1.30, 1.42]: box("gold", Vector3(x, y, -.185), Vector3(.23, .035, .022))
		if side < 0:
			beam("team", Vector3(x, 1.48, -.06), Vector3(x - .39, 1.25, -.23), .19, .22)
			chamfered("skin", Vector3(x - .445, 1.24, -.23), Vector2(.17, .21), Vector2(.19, .22), .18)
			bow(x - .13)
		else:
			chamfered("skin", Vector3(x, 1.15, -.08), Vector2(.19, .22), Vector2(.23, .24), .19)
			beam("wood", Vector3(x + .015, .85, -.16), Vector3(x + .035, 1.94, -.16), .025, .025)
			box("linen", Vector3(x + .035, 1.83, -.16), Vector3(.07, .15, .025))
		finish_part()
	save_model("res://units/models/ArcherRig.tscn")

func bow(x: float) -> void:
	var profile := [Vector2(.04, .38), Vector2(-.18, .55), Vector2(-.30, .88),
		Vector2(-.33, 1.24), Vector2(-.30, 1.60), Vector2(-.18, 1.94), Vector2(.04, 2.13)]
	for i in range(profile.size() - 1):
		var a: Vector2 = profile[i]
		var b: Vector2 = profile[i + 1]
		beam("wood", Vector3(x + a.x, a.y, -.23), Vector3(x + b.x, b.y, -.23), .07, .075)
	beam("linen", Vector3(x + .04, .38, -.23), Vector3(x + .04, 2.13, -.23), .013, .013)
	box("leather", Vector3(x - .325, 1.24, -.23), Vector3(.092, .23, .10))
	for y in [.46, 2.05]: box("gold", Vector3(x - .04, y, -.23), Vector3(.10, .055, .085))
