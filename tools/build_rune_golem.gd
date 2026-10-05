extends SceneTree
## Bake an articulated, flat-shaded stone guardian. Feet Y=0, forward -Z.

const COLORS := {
	"stone": Color("6b807b"), "edge": Color("abb8a6"), "dark": Color("293d3c"),
	"moss": Color("4b6750"), "gold": Color("c4ac72"),
	"rune": Color("72f3d6"), "team": Color.WHITE
}
var materials: Dictionary = {}
var surfaces: Dictionary = {}
var rig: Node3D
var part: Node3D
var origin := Vector3.ZERO

func _initialize() -> void:
	for category in ["stone", "rune", "team"]:
		var mat := StandardMaterial3D.new()
		mat.resource_name = "team_cloth" if category == "team" else "golem_" + category
		mat.vertex_color_use_as_albedo = true
		mat.vertex_color_is_srgb = true
		mat.albedo_color = Color("8dbde3") if category == "team" else Color.WHITE
		mat.roughness = .92
		if category == "rune":
			mat.emission_enabled = true
			mat.emission = Color("4bcbb4")
			mat.emission_energy_multiplier = .65
		materials[category] = mat
	rig = Node3D.new()
	rig.name = "Rig"
	rig.set_meta("pivot", Vector3.ZERO)
	begin_part("Body", Vector3(0, 1.35, 0))
	rock("dark", Vector3(0, 1.05, .04), Vector3(.43, .36, .34))
	rock("stone", Vector3(0, 1.64, 0), Vector3(.70, .65, .46), 8, 5)
	rock("edge", Vector3(0, 1.78, -.28), Vector3(.57, .33, .26), 8, 4)
	# Inset diamond core with a broad stone bezel, readable at gameplay scale.
	rock("dark", Vector3(0, 1.69, -.475), Vector3(.28, .35, .055), 4, 2)
	rock("gold", Vector3(0, 1.69, -.52), Vector3(.22, .29, .04), 4, 2)
	rock("rune", Vector3(0, 1.69, -.56), Vector3(.14, .21, .045), 4, 2)
	for side in [-1.0, 1.0]:
		tube("rune", Vector3(side*.22, 1.85, -.47), Vector3(side*.48, 1.95, -.38), .024, .016, 4)
		tube("gold", Vector3(side*.31, 1.18, -.28), Vector3(side*.44, 1.43, -.37), .036, .036, 5)
		rock("team", Vector3(side*.38, 1.21, -.33), Vector3(.19, .19, .045), 4, 2)
	rock("moss", Vector3(-.36, 2.01, .20), Vector3(.31, .16, .29))
	finish_part()
	var body := part
	begin_part("Head", Vector3(0, 2.19, -.03), body)
	rock("dark", Vector3(0, 2.17, -.04), Vector3(.22, .20, .23))
	rock("stone", Vector3(0, 2.34, -.08), Vector3(.40, .39, .33), 6, 4)
	rock("dark", Vector3(0, 2.35, -.365), Vector3(.28, .13, .045), 6, 3)
	for side in [-1.0, 1.0]:
		tube("rune", Vector3(side*.06, 2.36, -.412), Vector3(side*.24, 2.38, -.394), .029, .018, 4)
		tube("edge", Vector3(side*.03, 2.47, -.34), Vector3(side*.31, 2.51, -.25), .085, .055, 5)
	rock("edge", Vector3(0, 2.12, -.29), Vector3(.28, .14, .12), 6, 3)
	rock("gold", Vector3(0, 2.65, -.12), Vector3(.10, .14, .07), 4, 2)
	finish_part()
	for side in [-1.0, 1.0]:
		var label := "Left" if side < 0 else "Right"
		begin_part(label + "Arm", Vector3(side*.78, 1.96, 0), body)
		rock("dark", Vector3(side*.77, 1.83, .02), Vector3(.26, .30, .27))
		rock("stone", Vector3(side*.93, 1.99, .02), Vector3(.48, .38, .47), 7, 4)
		rock("edge", Vector3(side*1.01, 2.10, -.20), Vector3(.32, .20, .28), 6, 3)
		rock("team", Vector3(side*1.02, 2.12, -.405), Vector3(.20, .16, .045), 4, 2)
		rock("stone", Vector3(side*.97, 1.56, .02), Vector3(.24, .33, .25))
		for j in range(2):
			var p := Vector3(side*(.78+j*.25), 2.23, .12)
			tube("moss" if j == 0 else "edge", p, p+Vector3(side*.12, .27+j*.13, .10), .15, 0, 5)
		finish_part()
		var arm := part
		begin_part("Fist", Vector3(side*1.00, 1.32, -.02), arm)
		rock("dark", Vector3(side*1.01, 1.30, -.02), Vector3(.23, .24, .23))
		rock("stone", Vector3(side*1.04, 1.04, -.09), Vector3(.39, .45, .38), 7, 4)
		rock("edge", Vector3(side*1.04, .98, -.30), Vector3(.35, .26, .22), 7, 3)
		for j in range(3):
			rock("stone", Vector3(side*1.04+(j-1)*.19, .88, -.40), Vector3(.115, .17, .16), 5, 3)
		tube("rune", Vector3(side*1.04-.21, 1.18, -.427), Vector3(side*1.04+.21, 1.18, -.427), .023, .023, 4)
		finish_part()
		begin_part(label + "Leg", Vector3(side*.34, .95, .07))
		rock("dark", Vector3(side*.34, .86, .07), Vector3(.23, .28, .23))
		rock("stone", Vector3(side*.35, .67, .08), Vector3(.29, .32, .29))
		rock("edge", Vector3(side*.35, .56, -.15), Vector3(.26, .24, .20), 6, 3)
		rock("stone", Vector3(side*.37, .25, -.06), Vector3(.34, .25, .46), 7, 4)
		tube("rune", Vector3(side*.36, .37, -.385), Vector3(side*.36, .52, -.31), .022, .022, 4)
		finish_part()
	var scene := PackedScene.new()
	assert(scene.pack(rig) == OK)
	assert(ResourceSaver.save(scene, "res://units/models/RuneGolemRig.tscn") == OK)
	rig.free()
	print("PASS: articulated rune golem baked")
	quit()

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
	var category := key if key in ["rune", "team"] else "stone"
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
