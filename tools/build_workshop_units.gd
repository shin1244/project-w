extends "res://tools/build_tier_one_units.gd"
## Original articulated brass / iron machines. Forward -Z, ground Y=0.
## Run with a real renderer (a hidden window is fine); MultiMesh buffers cannot
## be serialized by Godot's headless dummy renderer. Per-unit flags preserve the other rig.

# Separate palettes keep the two machines' finishes independent.
const FLAME_PALETTE := {
	"steel": Color("414b4b"), "edge": Color("7d7869"), "iron": Color("2b3232"),
	"dark": Color("151a19"), "gold": Color("4b5345"), "linen": Color("a29776"),
	"team": Color("798276"), "rust": Color("815b3e"), "soot": Color("101312"),
	"ember": Color("d98434"),
}
var flame_palette := false
const ARTILLERY_PALETTE := {
	"steel": Color("626d69"), "edge": Color("aaa590"), "iron": Color("343e40"),
	"dark": Color("192124"), "gold": Color("a58350"), "linen": Color("d2c7aa"),
	"team": Color("a5aaa2"), "rust": Color("7c533b"), "soot": Color("101619"),
	"bronze": Color("806749"), "ember": Color("df8a3e"),
}
var artillery_palette := false

func _initialize() -> void:
	if DisplayServer.get_name() == "headless":
		push_error("Workshop track bake requires a real renderer: omit --headless and use --rendering-method gl_compatibility.")
		quit(1)
		return
	for key in ["neutral", "team"]:
		var mat := StandardMaterial3D.new()
		mat.resource_name = "team_cloth" if key == "team" else "machine_vertex_colors"
		mat.vertex_color_use_as_albedo = true
		mat.vertex_color_is_srgb = true
		mat.albedo_color = Color("477491") if key == "team" else Color.WHITE
		mat.roughness = .56
		mat.metallic = .22
		materials[key] = mat
	for key in ["flame_neutral", "flame_team", "flame_glow"]:
		var mat := StandardMaterial3D.new()
		mat.resource_name = "team_cloth" if key == "flame_team" else key
		mat.vertex_color_use_as_albedo = true
		mat.vertex_color_is_srgb = true
		mat.albedo_color = Color("477491") if key == "flame_team" else Color.WHITE
		mat.roughness = .88
		mat.metallic = .12
		if key == "flame_glow":
			mat.emission_enabled = true
			mat.emission = Color("ad3d0b")
			mat.emission_energy_multiplier = .65
		materials[key] = mat
	for key in ["artillery_neutral", "artillery_team"]:
		var mat := StandardMaterial3D.new()
		mat.resource_name = "team_cloth" if key == "artillery_team" else key
		mat.vertex_color_use_as_albedo = true
		mat.vertex_color_is_srgb = true
		mat.albedo_color = Color("477491") if key == "artillery_team" else Color.WHITE
		mat.roughness = .78
		mat.metallic = .24
		materials[key] = mat
	var args := OS.get_cmdline_user_args()
	if "--artillery-only" not in args:
		flame_palette = true
		build_flame()
		flame_palette = false
	if "--flame-only" not in args:
		artillery_palette = true
		build_artillery()
		artillery_palette = false
	print("PASS: baked " + ("SteamArtillery only" if "--artillery-only" in args else "FlameAutomaton only" if "--flame-only" in args else "FlameAutomaton and SteamArtillery"))
	quit()

func surface(key: String) -> SurfaceTool:
	if not flame_palette and not artillery_palette: return super.surface(key)
	var category := "flame_team" if key == "team" else "flame_glow" if key == "ember" else "flame_neutral"
	if artillery_palette: category = "artillery_team" if key == "team" else "artillery_neutral"
	if not surfaces.has(category):
		var st := SurfaceTool.new()
		st.begin(Mesh.PRIMITIVE_TRIANGLES)
		st.set_material(materials[category])
		surfaces[category] = st
	return surfaces[category]

func triangle(key: String, a: Vector3, b: Vector3, c: Vector3) -> void:
	if not flame_palette and not artillery_palette:
		super.triangle(key, a, b, c)
		return
	var st := surface(key)
	var normal := (b-a).cross(c-a).normalized()
	for vertex in [a, c, b]:
		st.set_normal(normal)
		st.set_color((ARTILLERY_PALETTE if artillery_palette else FLAME_PALETTE).get(key, Color.WHITE))
		st.add_vertex(vertex-origin)

func primitive(key: String, mesh: Mesh, center: Vector3, basis := Basis.IDENTITY) -> void:
	if not flame_palette and not artillery_palette:
		super.primitive(key, mesh, center, basis)
		return
	var data := mesh.surface_get_arrays(0)
	var vertices: PackedVector3Array = data[Mesh.ARRAY_VERTEX]
	var normals: PackedVector3Array = data[Mesh.ARRAY_NORMAL]
	var indices: PackedInt32Array = data[Mesh.ARRAY_INDEX]
	var st := surface(key)
	for i in indices:
		st.set_normal((basis * normals[i]).normalized())
		st.set_color((ARTILLERY_PALETTE if artillery_palette else FLAME_PALETTE).get(key, Color.WHITE))
		st.add_vertex(basis * vertices[i] + center-origin)

func sphere(key: String, center: Vector3, radius: float, scale_y := 1.0) -> void:
	var mesh := SphereMesh.new()
	mesh.radius = radius
	mesh.height = radius * 2.0
	mesh.radial_segments = 12
	mesh.rings = 6
	primitive(key, mesh, center, Basis.from_scale(Vector3(1, scale_y, 1)))

func tube(key: String, a: Vector3, b: Vector3, radius: float) -> void:
	cylinder(key, (a + b) * .5, radius, radius, a.distance_to(b), Basis(Quaternion(Vector3.UP, (b-a).normalized())))

func wheel(center: Vector3, radius: float, width: float, teeth := true) -> void:
	var axis := Basis(Vector3.FORWARD, PI * .5)
	cylinder("dark", center, radius, radius, width, axis)
	cylinder("gold", center, radius * .72, radius * .72, width + .035, axis)
	cylinder("iron", center, radius * .46, radius * .46, width + .055, axis)
	cylinder("gold", center, radius * .18, radius * .18, width + .08, axis)
	for i in range(8):
		var angle := i * TAU / 8.0
		var radial := Vector3(0, cos(angle), sin(angle))
		box("gold", center + radial * radius * .55, Vector3(width + .04, radius * .54, .045), Basis(Vector3.RIGHT, angle))
		if teeth: box("iron", center + radial * radius, Vector3(width, radius * .25, radius * .25), Basis(Vector3.RIGHT, angle))

func ring_rivets(y: float, radius: float) -> void:
	for i in range(12):
		var angle := i * TAU / 12.0
		sphere("edge", Vector3(sin(angle) * radius, y, cos(angle) * radius), .032)

func marker(parent_path: String, marker_name: String, position: Vector3) -> void:
	var parent := rig.get_node(parent_path)
	var node := Marker3D.new()
	node.name = marker_name
	node.position = position
	parent.add_child(node)
	node.owner = rig

func nest(child_name: String, parent_name: String) -> void:
	var child: Node3D = rig.get_node(child_name)
	var parent: Node3D = rig.get_node(parent_name)
	child.position -= parent.position
	clear_owner_recursive(child)
	rig.remove_child(child)
	parent.add_child(child)
	set_owner_recursive(child)

func set_owner_recursive(node: Node) -> void:
	node.owner = rig
	for child in node.get_children(): set_owner_recursive(child)

func clear_owner_recursive(node: Node) -> void:
	node.owner = null
	for child in node.get_children(): clear_owner_recursive(child)

func track_armour(x: float, points: Array) -> void:
	# Narrow side plates follow the triangular running gear, leaving its wheels visible.
	var middle := Vector2.ZERO
	for p: Vector2 in points: middle += p
	middle /= points.size()
	for i in range(points.size()):
		var a: Vector2 = points[i]
		var b: Vector2 = points[(i+1) % points.size()]
		for side in [-1.0, 1.0]:
			var va := Vector3(x + side*.105, a.x, a.y)
			var vb := Vector3(x + side*.105, b.x, b.y)
			triangle("gold", Vector3(x + side*.105, middle.x, middle.y), va if side > 0 else vb, vb if side > 0 else va)
		quad("iron", Vector3(x-.105,a.x,a.y), Vector3(x+.105,a.x,a.y), Vector3(x+.105,b.x,b.y), Vector3(x-.105,b.x,b.y))

func flame_track(side: float) -> void:
	var x: float = side*.70
	var track_name := "LeftTrack" if side < 0 else "RightTrack"
	begin_part(track_name, Vector3(x, 0, 0))
	track_armour(x, [Vector2(.29,-.57), Vector2(.80,-.54), Vector2(.35,.61)])
	beam("iron", Vector3(x,.29,-.56), Vector3(x,.25,.65), .21, .14)
	for z in [-.46, -.10, .28, .65]:
		wheel(Vector3(x,.24,z), .145, .26, false)
	for p in [Vector2(.36,-.49), Vector2(.65,-.50), Vector2(.41,.26)]:
		sphere("edge", Vector3(x+side*.12,p.x,p.y), .026)
	# Rust traces on the armour do not alter the mechanical silhouette.
	beam("rust", Vector3(x+side*.115,.74,-.50), Vector3(x+side*.115,.41,.35), .022, .022)
	finish_part()
	var track := part
	var path := Path3D.new()
	path.name = "TrackPath"
	path.curve = Curve3D.new()
	path.curve.bake_interval = .018
	# Bottom runs rear -> front; negate travel distance to roll against the ground.
	for point in [Vector3(0,.045,.72), Vector3(0,.045,-.64), Vector3(0,.20,-.85),
		Vector3(0,.87,-.72), Vector3(0,1.02,-.53), Vector3(0,.32,.84), Vector3(0,.15,.90), Vector3(0,.045,.72)]:
		path.curve.add_point(point)
	track.add_child(path)
	path.owner = rig
	var length := path.curve.get_baked_length()
	var count := 38
	var shoe := BoxMesh.new()
	shoe.size = Vector3(.34,.09,length/count*.94)
	var tread_material := StandardMaterial3D.new()
	tread_material.resource_name = "flame_tread_iron"
	tread_material.vertex_color_use_as_albedo = true
	tread_material.vertex_color_is_srgb = true
	tread_material.roughness = .92
	tread_material.metallic = .16
	shoe.material = tread_material
	var belt := MultiMeshInstance3D.new()
	belt.name = "Belt"
	belt.multimesh = MultiMesh.new()
	belt.multimesh.transform_format = MultiMesh.TRANSFORM_3D
	belt.multimesh.use_colors = true
	belt.multimesh.instance_count = count
	belt.multimesh.mesh = shoe
	for i in range(count):
		var d := i * length / count
		var p := path.curve.sample_baked(d)
		var tangent := (path.curve.sample_baked(fposmod(d+.025,length)) - path.curve.sample_baked(fposmod(d-.025,length))).normalized()
		belt.multimesh.set_instance_transform(i, Transform3D(Basis(Vector3.RIGHT,-atan2(tangent.y,tangent.z)),p))
		belt.multimesh.set_instance_color(i, Color("434a46") if i % 5 != 0 else Color("5b5041"))
	track.add_child(belt)
	belt.owner = rig

func build_flame() -> void:
	begin_model("Rig")
	begin_part("Body", Vector3(0, 1.15, 0))
	cylinder("iron", Vector3(0, .65, 0), .54, .47, .26)
	cylinder("gold", Vector3(0, 1.18, 0), .54, .54, 1.05)
	for y in [.72, 1.02, 1.64]:
		cylinder("iron", Vector3(0, y, 0), .565, .565, .09)
		ring_rivets(y, .574)
	for side in [-1.0, 1.0]:
		box("team", Vector3(side * .37, 1.41, -.424), Vector3(.105, .24, .07), Basis(Vector3.UP, side * -.35))
		beam("iron", Vector3(side * .41, 1.10, .02), Vector3(side * .67, .53, .02), .19, .24)
		cylinder("iron", Vector3(side * .28, 1.15, .56), .18, .18, .81)
		cylinder("gold", Vector3(side * .28, 1.53, .56), .14, .18, .17)
		tube("rust", Vector3(side * .28, 1.6, .56), Vector3(side * .28, 1.91, .56), .065)
		tube("soot", Vector3(side * .28, 1.83, .56), Vector3(side * .28, 1.96, .56), .075)
		tube("soot", Vector3(side * .28, 1.91, .56), Vector3(side * .28, 1.91, .36), .065)
		# Irregular drips below the rear exhaust seam.
		box("soot", Vector3(side*.28, 1.42, .744), Vector3(.085,.34,.018))
		box("soot", Vector3(side*.33, 1.36, .734), Vector3(.045,.21,.015))
	plate("soot", [Vector2(-.23,1.62),Vector2(.15,1.65),Vector2(.27,1.43),Vector2(.18,1.06),Vector2(.04,1.12),Vector2(-.17,.99),Vector2(-.27,1.28)], -.551, -.542)
	plate("rust", [Vector2(-.23,.77),Vector2(-.05,.79),Vector2(-.09,.73),Vector2(-.25,.72)], -.57,-.554)
	box("soot",Vector3(-.11,1.77,-.535),Vector3(.13,.20,.025))
	# Furnace vent and pressure gauge leave a clear mechanical front silhouette.
	box("dark", Vector3(0, .85, -.55), Vector3(.34, .22, .05))
	box("ember", Vector3(0, .85, -.58), Vector3(.28, .115, .012))
	for x in [-.12, -.04, .04, .12]: box("iron", Vector3(x, .85, -.595), Vector3(.035, .19, .025))
	cylinder("iron", Vector3(.28, 1.61, -.44), .13, .13, .07, Basis(Vector3.RIGHT, PI*.5))
	cylinder("linen", Vector3(.28, 1.61, -.487), .10, .10, .015, Basis(Vector3.RIGHT, PI*.5))
	beam("dark", Vector3(.28, 1.61, -.505), Vector3(.32, 1.67, -.505), .016, .016)
	finish_part()
	begin_part("Head", Vector3(0, 1.75, 0))
	cylinder("iron", Vector3(0, 1.76, 0), .54, .54, .11)
	sphere("steel", Vector3(0, 1.79, 0), .52, .78)
	cylinder("gold", Vector3(0, 1.8, 0), .535, .535, .06)
	box("soot", Vector3(.13,1.83,-.495), Vector3(.12,.13,.035))
	box("soot", Vector3(.20,1.85,-.477), Vector3(.05,.20,.03))
	box("iron", Vector3(-.12, 1.99, -.412), Vector3(.48, .19, .14))
	box("dark", Vector3(-.12, 1.99, -.49), Vector3(.39, .11, .035))
	box("ember", Vector3(-.16, 1.99, -.512), Vector3(.16, .046, .02))
	cylinder("gold", Vector3(0, 2.17, .04), .09, .13, .15)
	finish_part()
	begin_part("Nozzle", Vector3(0, 1.31, -.47))
	tube("iron", Vector3(0, 1.31, -.46), Vector3(0, 1.31, -.92), .19)
	tube("rust", Vector3(0, 1.31, -.64), Vector3(0, 1.31, -.79), .155)
	tube("soot", Vector3(0, 1.31, -.79), Vector3(0, 1.31, -.935), .167)
	tube("dark", Vector3(0, 1.31, -.94), Vector3(0, 1.31, -.955), .115)
	for side in [-1.0, 1.0]: tube("rust", Vector3(side*.16, 1.23, -.48), Vector3(side*.16, 1.23, -.86), .033)
	finish_part()
	marker("Nozzle", "Muzzle", Vector3(0, 0, -.50))
	for side in [-1.0, 1.0]:
		flame_track(side)
		begin_part("LeftWheel" if side < 0 else "RightWheel", Vector3(side*.70, .80, -.55))
		wheel(origin, .19, .28)
		finish_part()
	marker("Body", "SteamLeft", Vector3(-.28, .80, .55))
	marker("Body", "SteamRight", Vector3(.28, .80, .55))
	save_model("res://units/models/FlameAutomatonRig.tscn")

func artillery_node(node_name: String, parent_path: String, position := Vector3.ZERO) -> Node3D:
	var node := Node3D.new()
	node.name = node_name
	(rig if parent_path.is_empty() else rig.get_node(parent_path)).add_child(node)
	node.owner = rig
	node.position = position
	return node

func attach_artillery_part(parent_path: String, position := Vector3.ZERO) -> void:
	finish_part()
	if not parent_path.is_empty():
		clear_owner_recursive(part)
		rig.remove_child(part)
		rig.get_node(parent_path).add_child(part)
		set_owner_recursive(part)
	part.position = position

func artillery_cog(radius: float, width: float) -> void:
	# An open annulus, spokes and separate teeth keep the great elevation gears readable.
	var inner := radius*.70
	var outer := radius*.93
	for i in range(24):
		var a := i*TAU/24
		var b := (i+1)*TAU/24
		var ra := Vector3(0, cos(a), sin(a))
		var rb := Vector3(0, cos(b), sin(b))
		for side in [-1.0,1.0]:
			var x := Vector3(side*width*.5,0,0)
			if side < 0: quad("gold",x+ra*inner,x+rb*inner,x+rb*outer,x+ra*outer)
			else: quad("gold",x+ra*outer,x+rb*outer,x+rb*inner,x+ra*inner)
		quad("bronze",Vector3(-width*.5,0,0)+ra*outer,Vector3(width*.5,0,0)+ra*outer,Vector3(width*.5,0,0)+rb*outer,Vector3(-width*.5,0,0)+rb*outer)
		quad("iron",Vector3(-width*.5,0,0)+ra*inner,Vector3(-width*.5,0,0)+rb*inner,Vector3(width*.5,0,0)+rb*inner,Vector3(width*.5,0,0)+ra*inner)
		box("gold",ra*radius,Vector3(width+.025,radius*.20,radius*.16),Basis(Vector3.RIGHT,a))
	cylinder("iron",Vector3.ZERO,radius*.23,radius*.23,width+.09,Basis(Vector3.FORWARD,PI*.5))
	cylinder("gold",Vector3.ZERO,radius*.12,radius*.12,width+.13,Basis(Vector3.FORWARD,PI*.5))
	for i in range(8):
		var a := i*TAU/8
		var radial := Vector3(0,cos(a),sin(a))
		beam("bronze",radial*radius*.19,radial*radius*.77,width*.7,radius*.105)
		for side in [-1.0,1.0]: sphere("edge",Vector3(side*(width*.5+.01),0,0)+radial*radius*.80,.025)

func traverse_cog(pitch_radius: float, inner_radius: float, teeth: int, width: float, phase := 0.0) -> void:
	# Common tooth spacing for the 40:10 horizontal traverse gear pair. Both
	# spin about Y; their exposed top faces make the different axis unmistakable.
	var root_radius := pitch_radius-.05
	for i in range(teeth):
		var a: float = i*TAU/teeth+phase
		var b: float = (i+1)*TAU/teeth+phase
		var ra := Vector3(cos(a),0,sin(a))
		var rb := Vector3(cos(b),0,sin(b))
		var top := Vector3(0,width*.5,0)
		var bottom := -top
		quad("gold",top+ra*inner_radius,top+rb*inner_radius,top+rb*root_radius,top+ra*root_radius)
		quad("bronze",bottom+ra*root_radius,bottom+rb*root_radius,bottom+rb*inner_radius,bottom+ra*inner_radius)
		quad("bronze",bottom+ra*root_radius,top+ra*root_radius,top+rb*root_radius,bottom+rb*root_radius)
		quad("iron",bottom+ra*inner_radius,bottom+rb*inner_radius,top+rb*inner_radius,top+ra*inner_radius)
		box("gold",ra*pitch_radius,Vector3(.10,width+.018,.067),Basis(Vector3.UP,-a))
	var disk := CylinderMesh.new()
	disk.top_radius = inner_radius+.015
	disk.bottom_radius = inner_radius+.015
	disk.height = width*.72
	disk.radial_segments = teeth
	primitive("iron",disk,Vector3.ZERO)
	for i in range(8 if teeth > 10 else 4):
		var a: float = i*TAU/(8 if teeth > 10 else 4)
		var radius: float = (inner_radius+root_radius)*.5
		sphere("edge",Vector3(cos(a)*radius,width*.5+.012,sin(a)*radius),.024 if teeth > 10 else .014)

func artillery_leg(side: float, index: int) -> void:
	var prefix := "Left" if side < 0 else "Right"
	var name_leg := prefix + str(index)
	var z_positions := [-1.12,-.38,.38,1.12]
	artillery_node(name_leg,"Legs",Vector3(side*.89,1.30,z_positions[index]))
	var path := "Legs/"+name_leg
	# Triangular link frames, not a spider's single bent limb. The animated rig
	# positions the same six pins and eight rigid rods from a shared gait phase.
	var pins := {
		"A":Vector3(0,0,0), "B":Vector3(side*.13,-.30,-.24),
		"C":Vector3(side*.20,-.15,.28), "D":Vector3(side*.42,-.64,-.06),
		"E":Vector3(side*.55,-.55,.28), "F":Vector3(side*.70,-1.23,-.03),
	}
	for pair in ["AB","AC","BD","CD","DE","CE","EF","CF"]:
		begin_part("Link"+pair,Vector3.ZERO)
		box("bronze" if pair in ["AC","CE","EF"] else "steel",Vector3.ZERO,Vector3(.085,1,.10))
		box("dark",Vector3(0,0,.052),Vector3(.031,.69,.014))
		attach_artillery_part(path)
		var a: Vector3 = pins[pair[0]]
		var b: Vector3 = pins[pair[1]]
		part.position = (a+b)*.5
		part.basis = Basis(Quaternion(Vector3.UP,(b-a).normalized()))
		part.scale.y = a.distance_to(b)
	for pin_name in pins:
		begin_part("Joint"+pin_name,Vector3.ZERO)
		cylinder("dark",Vector3.ZERO,.102,.102,.16,Basis(Vector3.FORWARD,PI*.5))
		cylinder("gold",Vector3.ZERO,.065,.065,.195,Basis(Vector3.FORWARD,PI*.5))
		cylinder("edge",Vector3.ZERO,.027,.027,.21,Basis(Vector3.FORWARD,PI*.5))
		attach_artillery_part(path,pins[pin_name])
	begin_part("Foot",Vector3.ZERO)
	chamfered("iron",Vector3(0,-.015,0),Vector2(.28,.40),Vector2(.22,.30),.10)
	box("bronze",Vector3(0,.043,0),Vector3(.17,.04,.22))
	attach_artillery_part(path,pins["F"])

func build_artillery() -> void:
	begin_model("Rig")
	artillery_node("Aim", "")
	begin_part("Hull",Vector3.ZERO)
	# A low riveted iron citadel, carried high by the linkage legs during travel.
	chamfered("dark",Vector3(0,.23,0),Vector2(1.76,3.28),Vector2(1.89,3.42),.25)
	chamfered("iron",Vector3(0,.48,-.02),Vector2(1.91,3.40),Vector2(1.62,3.12),.43)
	chamfered("steel",Vector3(0,.69,-.06),Vector2(1.77,3.23),Vector2(1.71,3.14),.09)
	# Broad keel skids remain above the earth, leaving room for the pressure outlets.
	for z in [-1.22,1.22]:
		chamfered("dark",Vector3(0,.085,z),Vector2(1.92,.56),Vector2(1.71,.47),.14)
		box("bronze",Vector3(0,.17,z),Vector3(1.77,.05,.36))
	for side in [-1.0,1.0]:
		for z in [-1.17,-.39,.39,1.17]:
			box("steel",Vector3(side*.938,.43,z),Vector3(.055,.30,.63))
			box("dark",Vector3(side*.969,.43,z),Vector3(.013,.22,.47))
			for edge_z in [-.25,.25]:
				for y in [.33,.53]: sphere("gold",Vector3(side*.98,y,z+edge_z),.030)
		beam("bronze",Vector3(side*.78,.75,-1.35),Vector3(side*.78,.75,1.24),.065,.065)
		for z in [-1.12,-.38,.38,1.12]:
			cylinder("iron",Vector3(side*.91,.45,z),.16,.16,.20,Basis(Vector3.FORWARD,PI*.5))
			cylinder("gold",Vector3(side*1.02,.45,z),.065,.065,.05,Basis(Vector3.FORWARD,PI*.5))
		# Small flank heraldry is subordinate to the gun and exposed machinery.
		box("team",Vector3(side*.97,.48,.79),Vector3(.02,.19,.33))
		box("gold",Vector3(side*.989,.48,.79),Vector3(.014,.15,.035))
	# Heavy front plate, tow hooks and inset lamps.
	box("steel",Vector3(0,.42,-1.70),Vector3(1.14,.26,.09))
	for side in [-1.0,1.0]:
		box("gold",Vector3(side*.68,.49,-1.63),Vector3(.15,.14,.08))
		box("linen",Vector3(side*.68,.49,-1.68),Vector3(.075,.066,.025))
		tube("iron",Vector3(side*.43,.19,-1.69),Vector3(side*.43,.32,-1.78),.06)
	# A squat rear boiler, armored coal hatch and twin soot-dark chimneys.
	# Keep the rear machinery outside the gun's swept volume as the carriage traverses.
	cylinder("bronze",Vector3(0,.84,1.63),.24,.24,.98,Basis(Vector3.FORWARD,PI*.5))
	for x in [-.38,0,.38]:
		cylinder("iron",Vector3(x,.84,1.63),.26,.26,.075,Basis(Vector3.FORWARD,PI*.5))
	box("iron",Vector3(0,.85,1.87),Vector3(.62,.30,.09))
	box("soot",Vector3(0,.85,1.925),Vector3(.49,.20,.025))
	for x in [-.18,-.06,.06,.18]: box("bronze",Vector3(x,.85,1.947),Vector3(.035,.18,.025))
	for side in [-1.0,1.0]:
		tube("iron",Vector3(side*.67,.82,1.50),Vector3(side*.67,1.37,1.50),.105)
		cylinder("bronze",Vector3(side*.67,1.27,1.50),.13,.13,.08)
		tube("soot",Vector3(side*.67,1.31,1.50),Vector3(side*.67,1.42,1.50),.115)
		tube("bronze",Vector3(side*.45,.84,1.41),Vector3(side*.67,.92,1.50),.047)
		tube("bronze",Vector3(side*.67,.92,1.40),Vector3(side*.73,.92,.42),.047)
		box("soot",Vector3(side*.68,.74,1.35),Vector3(.14,.025,.45))
	attach_artillery_part("",Vector3(0,.85,0))
	artillery_node("Chassis", "Hull")
	marker("Hull/Chassis","SteamLeft",Vector3(-.67,1.45,1.50))
	marker("Hull/Chassis","SteamRight",Vector3(.67,1.45,1.50))
	# Six low outlets are distinct from the chimney smoke. Their markers face
	# outwards so pressure-release effects can use each nozzle's local -Z axis.
	begin_part("PressureVentPipes",Vector3.ZERO)
	for side in [-1.0,1.0]:
		for z in [-.85,0,.85]:
			tube("iron",Vector3(side*.63,.22,z),Vector3(side*.63,-.02,z),.085)
			tube("iron",Vector3(side*.63,-.02,z),Vector3(side*1.02,-.02,z),.10)
			tube("bronze",Vector3(side*.79,-.02,z),Vector3(side*.88,-.02,z),.12)
			tube("gold",Vector3(side*.98,-.02,z),Vector3(side*1.065,-.02,z),.135)
			tube("soot",Vector3(side*1.067,-.02,z),Vector3(side*1.075,-.02,z),.095)
			tube("dark",Vector3(side*1.077,-.02,z),Vector3(side*1.079,-.02,z),.071)
	attach_artillery_part("Hull")
	artillery_node("PressureVents","Hull")
	for side in [-1.0,1.0]:
		for i in range(3):
			var vent_name: String = ("Left" if side < 0 else "Right") + ["Front","Middle","Rear"][i]
			marker("Hull/PressureVents",vent_name,Vector3(side*1.08,-.02,[-.85,0,.85][i]))
			rig.get_node("Hull/PressureVents/"+vent_name).rotation.y = -side*PI*.5

	# Compact self-defense turret remains a separate weapon beneath the great gun.
	begin_part("MobileTurret",Vector3.ZERO)
	cylinder("dark",Vector3(0,.02,0),.41,.44,.12)
	cylinder("bronze",Vector3(0,.09,0),.38,.38,.08)
	chamfered("steel",Vector3(0,.25,.015),Vector2(.78,.69),Vector2(.62,.57),.30)
	chamfered("iron",Vector3(0,.43,.02),Vector2(.65,.59),Vector2(.49,.45),.10)
	box("team",Vector3(0,.49,.02),Vector3(.27,.025,.29))
	for side in [-1.0,1.0]:
		cylinder("gold",Vector3(side*.36,.29,-.20),.12,.12,.09,Basis(Vector3.FORWARD,PI*.5))
		for z in [-.12,.09]: sphere("edge",Vector3(side*.359,.23,z),.022)
	attach_artillery_part("Hull",Vector3(0,.48,-1.05))
	begin_part("Barrel",Vector3.ZERO)
	tube("iron",Vector3(0,0,.10),Vector3(0,0,-.47),.14)
	tube("bronze",Vector3(0,0,-.10),Vector3(0,0,-.23),.16)
	for side in [-1.0,1.0]: tube("steel",Vector3(side*.14,-.02,.05),Vector3(side*.14,-.02,-.38),.027)
	attach_artillery_part("Hull/MobileTurret",Vector3(0,.30,-.24))
	begin_part("Tube",Vector3.ZERO)
	tube("steel",Vector3.ZERO,Vector3(0,0,-.51),.095)
	tube("iron",Vector3(0,0,-.41),Vector3(0,0,-.56),.13)
	tube("soot",Vector3(0,0,-.56),Vector3(0,0,-.568),.083)
	attach_artillery_part("Hull/MobileTurret/Barrel",Vector3(0,0,-.43))
	marker("Hull/MobileTurret/Barrel/Tube","Muzzle",Vector3(0,0,-.58))

	# The firing platform evokes an enormous railway-gun cradle with open gearing.
	# Fixed bearing and drive housing stay on the deck when the upper gun traverses.
	begin_part("TraverseBearing",Vector3.ZERO)
	var bearing := CylinderMesh.new()
	bearing.top_radius = .84
	bearing.bottom_radius = .86
	bearing.height = .09
	bearing.radial_segments = 40
	primitive("iron",bearing,Vector3.ZERO)
	var bearing_drum := CylinderMesh.new()
	bearing_drum.top_radius = .63
	bearing_drum.bottom_radius = .68
	bearing_drum.height = .10
	bearing_drum.radial_segments = 32
	primitive("iron",bearing_drum,Vector3(0,.055,0))
	cylinder("bronze",Vector3(0,.10,0),.65,.65,.04)
	for side in [-1.0,1.0]:
		for z in [-.51,.51]:
			box("steel",Vector3(side*.60,-.02,z),Vector3(.20,.08,.20))
			sphere("edge",Vector3(side*.60,.032,z),.035)
	box("iron",Vector3(.7955,-.006,-.7955),Vector3(.42,.10,.42))
	cylinder("dark",Vector3(.7955,.08,-.7955),.18,.20,.20)
	attach_artillery_part("Hull",Vector3(0,.73,.38))
	begin_part("TraversePinion",Vector3.ZERO)
	traverse_cog(.225,.073,10,.12,deg_to_rad(9))
	cylinder("bronze",Vector3(0,.02,0),.075,.075,.15)
	cylinder("iron",Vector3(0,.075,0),.085,.085,.04)
	attach_artillery_part("Hull",Vector3(.7955,.88,-.4155))
	begin_part("MainTurret",Vector3.ZERO)
	for side in [-1.0,1.0]:
		chamfered("iron",Vector3(side*.59,.21,.07),Vector2(.25,1.45),Vector2(.18,1.15),.44)
		beam("steel",Vector3(side*.60,.08,.69),Vector3(side*.60,.58,-.06),.16,.16)
		beam("bronze",Vector3(side*.60,.02,-.62),Vector3(side*.60,.38,-.05),.12,.12)
		box("steel",Vector3(side*.54,.52,.40),Vector3(.11,.08,.62))
		for z in [-.46,-.14,.23,.54]: sphere("gold",Vector3(side*.727,.17,z),.034)
	chamfered("iron",Vector3(0,.16,.67),Vector2(1.18,.34),Vector2(1.07,.29),.27)
	box("bronze",Vector3(0,.31,.69),Vector3(.82,.05,.23))
	attach_artillery_part("Hull",Vector3(0,1.0,.38))
	# Lift the upper cradle and its common gun/elevation-gear axis together.
	# The broad central riser leaves a real gap between the two gear planes.
	part.get_node("Mesh").position.y = .32
	begin_part("TraverseRing",Vector3.ZERO)
	traverse_cog(.90,.59,40,.13)
	attach_artillery_part("Hull/MainTurret",Vector3(0,-.12,0))
	begin_part("TraverseRiser",Vector3.ZERO)
	var riser := CylinderMesh.new()
	riser.top_radius = .50
	riser.bottom_radius = .53
	riser.height = .34
	riser.radial_segments = 24
	primitive("iron",riser,Vector3(0,.12,0))
	cylinder("bronze",Vector3(0,-.025,0),.55,.55,.05)
	chamfered("steel",Vector3(0,.28,.05),Vector2(1.18,1.28),Vector2(1.12,1.22),.08)
	for side in [-1.0,1.0]:
		for z in [-.30,.30]:
			tube("bronze",Vector3(side*.36,0,z),Vector3(side*.36,.24,z),.035)
	attach_artillery_part("Hull/MainTurret")
	for side in [-1.0,1.0]:
		begin_part("LeftGear" if side < 0 else "RightGear",Vector3.ZERO)
		artillery_cog(.56,.14)
		attach_artillery_part("Hull/MainTurret",Vector3(side*.78,.70,-.05))

	begin_part("Barrel",Vector3.ZERO)
	# Thick bronze breech, iron barrel jacket and longitudinal recoil cylinders.
	tube("bronze",Vector3(0,0,.70),Vector3(0,0,.19),.365)
	tube("iron",Vector3(0,0,.58),Vector3(0,0,-1.03),.31)
	for z in [.56,.15,-.34,-.82]: tube("gold",Vector3(0,0,z+.055),Vector3(0,0,z-.055),.35)
	tube("dark",Vector3(0,0,.73),Vector3(0,0,.77),.27)
	tube("bronze",Vector3(0,0,.77),Vector3(0,0,.86),.16)
	for side in [-1.0,1.0]:
		tube("iron",Vector3(side*.38,-.18,.55),Vector3(side*.38,-.18,-.84),.09)
		tube("steel",Vector3(side*.38,-.18,-.30),Vector3(side*.38,-.18,-1.29),.052)
		box("bronze",Vector3(side*.38,-.18,-1.21),Vector3(.12,.16,.12))
	for i in range(12):
		var angle := i*TAU/12
		sphere("edge",Vector3(sin(angle)*.358,cos(angle)*.358,.59),.026)
	attach_artillery_part("Hull/MainTurret",Vector3(0,.70,-.05))
	part.rotation.x = .10
	begin_part("Tube",Vector3.ZERO)
	# A wide bored muzzle and strengthening hoops recall a monumental cast bombard.
	tube("steel",Vector3(0,0,.05),Vector3(0,0,-1.93),.245)
	for z in [-.18,-.75,-1.35]: tube("bronze",Vector3(0,0,z+.065),Vector3(0,0,z-.065),.277)
	tube("iron",Vector3(0,0,-1.68),Vector3(0,0,-2.03),.325)
	tube("gold",Vector3(0,0,-1.93),Vector3(0,0,-2.065),.347)
	tube("soot",Vector3(0,0,-2.067),Vector3(0,0,-2.079),.253)
	tube("dark",Vector3(0,0,-2.080),Vector3(0,0,-2.085),.195)
	for side in [-1.0,1.0]: beam("iron",Vector3(side*.20,-.19,-.06),Vector3(side*.20,-.19,-1.58),.06,.06)
	attach_artillery_part("Hull/MainTurret/Barrel",Vector3(0,0,-.85))
	marker("Hull/MainTurret/Barrel/Tube","Muzzle",Vector3(0,0,-2.10))
	artillery_node("Legs", "")
	for side in [-1.0,1.0]:
		for i in range(4): artillery_leg(side,i)
	save_model("res://units/models/SteamArtilleryRig.tscn")
