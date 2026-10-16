extends "res://tools/build_tier_two_base.gd"
## The research lab (기술연구소, server type 9): an academy's octagonal observatory gone a little
## shabby. Faded limestone, marble pilasters and arched windows glowing a faint arcane cyan, a
## columned portico with a company-lacquered door, a copper dome streaked with verdigris and a
## glowing crystal spire. Fine city dust lies on every sill, step and ledge and grime runs down
## under them. A tesla coil crackles at the front left; a gilt armillary turns at the front right.

const SCENE := "res://buildings/ResearchLab.tscn"
const MESH := "res://buildings/meshes/ResearchLab.res"
const ARMILLARY_MESH := "res://buildings/meshes/ResearchLabArmillary.res"
const ARMILLARY_AT := Vector3(1.22, 1.17, -0.80)
const COIL_AT := Vector3(-1.25, 0, -0.80)

func _initialize() -> void:
	lab_materials()
	octagonal_base(1.92)
	base_dust()
	research_house()
	observatory()
	crystal_spire()
	portico()
	tesla_coil()
	armillary_pedestal()
	validate_clearance()
	save_compact(SCENE, MESH, "ResearchLab", 9, "research_lab", Vector2i(4, 4))
	add_moving_parts()
	quit()

func lab_materials() -> void:
	# Fifteen surfaces at most: no wood here, the money went into stone and brass.
	machine_materials()
	material("stone", Color("7d786c"))
	material("stone_light", Color("c4b9a0")) # Marble trim, faded.
	material("stone_dark", Color("4c4c47"))
	material("plaster", Color("b3a587")) # Limestone gone grey-ivory under the dust.
	material("grime", Color("7a6f5c"))
	material("gold", Color("a88d55"), 0.45, 0.62) # Tarnished gilt.
	material("canvas", Color("8a6448"), 0.35, 0.6) # Dull copper dome.
	material("patina", Color("6c8273"), 0.1, 0.8)
	material("window", Color("88aaa2"), 0.0, 0.35, 0.35)
	material("crystal", Color("8fe0ff"), 0.0, 0.2, 2.2)
	material("dust", Color("c9bfa8"))
	material("dark", Color("26292a"), 0.25)

func base_dust() -> void:
	# Dust drifts against the wall foot where nobody walks.
	for i in [2, 3, 5, 6]:
		var basis := Basis(Vector3.UP, TAU * i / 8.0)
		box("dust", Vector3(0, 0.183, 0.10) + basis * Vector3(0, 0, -1.5), Vector3(0.62, 0.008, 0.16), basis)

func research_house() -> void:
	cylinder("stone_dark", Vector3(0, .36, .10), 1.30, 1.39, .36)
	cylinder("stone_light", Vector3(0, .56, .10), 1.36, 1.36, .10)
	for i in range(8):
		var basis := Basis(Vector3.UP, TAU * i / 8.0)
		var center := Vector3(0, 1.66, .10) + basis * Vector3(0, 0, -1.20)
		box("plaster", center, Vector3(.99, 2.10, .13), basis)
		box("stone", center + basis * Vector3(0, -.86, -.075), Vector3(.97, .38, .03), basis)
		for sx in [-.49, .49]:
			box("stone_light", center + basis * Vector3(sx, 0, -.02), Vector3(.10, 2.18, .12), basis)
			box("gold", center + basis * Vector3(sx, .93, -.03), Vector3(.15, .07, .15), basis)
		box("stone_light", Vector3(center.x, 2.61, center.z), Vector3(1.04, .11, .16), basis)
		if i != 0:
			lab_window(center, basis)
		if i % 3 != 0:
			# Grime runs down from the cornice between the window and the pilasters.
			for sx in [-.36, .36]:
				streak(center, basis, sx, .93, 1.05 if i % 2 == 0 else .8, .075)
	# Marble cornice under the company-slate collar; dust collects in the gutter.
	cylinder("stone_light", Vector3(0, 2.72, .10), 1.36, 1.36, .14)
	cylinder("roof", Vector3(0, 2.91, .10), 1.08, 1.48, .30)
	cylinder("dust", Vector3(0, 2.79, .10), 1.418, 1.474, .04)
	cylinder("roof_light", Vector3(0, 3.08, .10), 1.10, 1.10, .08)
	cylinder("gold", Vector3(0, 3.14, .10), 1.12, 1.12, .045)
	cylinder("dust", Vector3(0, 3.165, .10), 1.07, 1.10, .01)

func lab_window(center: Vector3, basis: Basis) -> void:
	var top := .425
	box("dark", center + basis * Vector3(0, .02, -.085), Vector3(.49, .92, .025), basis)
	box("window", center + basis * Vector3(0, .02, -.103), Vector3(.39, .81, .018), basis)
	arch_fan("dark", center, basis, top, .245, -.099)
	arch_fan("window", center, basis, top, .195, -.113)
	for k in range(6):
		var a := PI * k / 6.0
		var b := PI * (k + 1) / 6.0
		beam("gold", center + basis * Vector3(cos(a) * .215, top + sin(a) * .215, -.118),
			center + basis * Vector3(cos(b) * .215, top + sin(b) * .215, -.118), .025, .012)
	box("gold", center + basis * Vector3(0, .1, -.121), Vector3(.026, .86, .014), basis)
	box("gold", center + basis * Vector3(0, top, -.122), Vector3(.41, .026, .014), basis)
	box("stone_light", center + basis * Vector3(0, top + .25, -.085), Vector3(.09, .1, .05), basis)
	# Sill with its dust, and a grime streak beneath.
	box("stone_light", center + basis * Vector3(0, -.42, -.10), Vector3(.56, .07, .16), basis)
	box("dust", center + basis * Vector3(0, -.382, -.115), Vector3(.5, .006, .11), basis)
	streak(center, basis, -.13, -.455, .5, .07)
	streak(center, basis, .11, -.455, .36, .05)

func arch_fan(key: String, center: Vector3, basis: Basis, y: float, radius: float, z: float) -> void:
	# A half disc standing on the window head, facing outward (local -Z).
	var middle := center + basis * Vector3(0, y, z)
	for k in range(8):
		var a := PI * k / 8.0
		var b := PI * (k + 1) / 8.0
		triangle(key, middle, center + basis * Vector3(cos(b) * radius, y + sin(b) * radius, z),
			center + basis * Vector3(cos(a) * radius, y + sin(a) * radius, z))

func streak(center: Vector3, basis: Basis, x: float, y: float, length: float, width: float) -> void:
	# A tapering run of grime just proud of the wall face.
	var z := -.0665
	face("grime", center + basis * Vector3(x - width * .5, y, z), center + basis * Vector3(x + width * .5, y, z),
		center + basis * Vector3(x + width * .15, y - length, z), center + basis * Vector3(x - width * .15, y - length, z))

func observatory() -> void:
	var center := Vector3(0, 3.15, .10)
	var radius := 1.06
	var height := .98
	for row in range(4):
		var a := PI * .5 * row / 4.0
		var b := PI * .5 * (row + 1) / 4.0
		for segment in range(16):
			var u := TAU * segment / 16.0
			var v := TAU * (segment + 1) / 16.0
			var p := center + Vector3(cos(u) * cos(a) * radius, sin(a) * height, sin(u) * cos(a) * radius)
			var q := center + Vector3(cos(u) * cos(b) * radius, sin(b) * height, sin(u) * cos(b) * radius)
			var r := center + Vector3(cos(v) * cos(b) * radius, sin(b) * height, sin(v) * cos(b) * radius)
			var s := center + Vector3(cos(v) * cos(a) * radius, sin(a) * height, sin(v) * cos(a) * radius)
			# Verdigris runs down from the seams in uneven streaks and pools at the foot of the dome.
			var green: bool = row < [2, 1, 3, 0, 1, 2, 0, 1, 3, 1, 0, 2, 1, 0, 2, 1][segment]
			quad("patina" if green else "canvas", p, q, r, s)
	for segment in range(8):
		var angle := TAU * segment / 8.0
		for row in range(4):
			var a := PI * .5 * row / 4.0
			var b := PI * .5 * (row + 1) / 4.0
			beam("gold", center + Vector3(cos(angle) * cos(a) * 1.075, sin(a), sin(angle) * cos(a) * 1.075),
				center + Vector3(cos(angle) * cos(b) * 1.075, sin(b), sin(angle) * cos(b) * 1.075), .035, .035)
	# The brass telescope still points out of the front observation hatch.
	box("dark", Vector3(0, 3.56, -.76), Vector3(.46, .52, .08), Basis(Vector3.RIGHT, -.30))
	var near := Vector3(0, 3.53, -.69)
	var far := Vector3(0, 3.94, -1.37)
	var axis := Basis(Quaternion(Vector3.UP, (far - near).normalized()))
	cylinder("gold", (near + far) * .5, .16, .20, near.distance_to(far), axis)
	cylinder("dark", far, .19, .19, .12, axis)
	cylinder("window", far + (far - near).normalized() * .066, .145, .145, .018, axis)

func crystal_spire() -> void:
	# A glowing arcane crystal held in tarnished gilt prongs on top of the dome.
	var top := Vector3(0, 4.13, .10)
	cylinder("gold", top + Vector3(0, .05, 0), .07, .13, .10)
	cylinder("dark", top + Vector3(0, .17, 0), .04, .05, .16)
	for i in range(3):
		var a := TAU * i / 3.0
		beam("gold", top + Vector3(cos(a) * .1, .14, sin(a) * .1), top + Vector3(cos(a) * .075, .64, sin(a) * .075), .025, .025)
	crystal("crystal", top + Vector3(0, .42, 0), .09, .22)
	halo("gold", top + Vector3(0, .42, 0), .13, .025)
	halo("gold", top + Vector3(0, .42, 0), .15, .018, Basis(Vector3.FORWARD, .5))

func crystal(key: String, center: Vector3, radius: float, half_height: float, sides := 6) -> void:
	var up := center + Vector3(0, half_height, 0)
	var down := center - Vector3(0, half_height, 0)
	for i in range(sides):
		var a := TAU * i / sides
		var b := TAU * (i + 1) / sides
		var pa := center + Vector3(cos(a) * radius, 0, sin(a) * radius)
		var pb := center + Vector3(cos(b) * radius, 0, sin(b) * radius)
		triangle(key, up, pb, pa)
		triangle(key, down, pa, pb)

func halo(key: String, center: Vector3, radius: float, thickness: float, basis := Basis.IDENTITY, segments := 16) -> void:
	for i in range(segments):
		var a := TAU * (i + .5) / segments
		box(key, center + basis * (Vector3(cos(a), 0, sin(a)) * radius),
			Vector3(thickness, thickness, radius * TAU / segments + .005), basis * Basis(Vector3.UP, -a))

func portico() -> void:
	# Steps with dust drifted into the corners nobody treads.
	box("stone", Vector3(0, .25, -1.32), Vector3(1.1, .14, .55))
	box("stone_light", Vector3(0, .39, -1.215), Vector3(1.3, .14, .40))
	for x in [-.44, .44]:
		box("dust", Vector3(x, .323, -1.47), Vector3(.2, .006, .2))
	for x in [-.6, .6]:
		box("dust", Vector3(x, .463, -1.25), Vector3(.1, .006, .3))
	# A company-lacquered double door in gilt frames.
	box("dark", Vector3(0, 1.30, -1.17), Vector3(.73, 1.40, .05))
	box("banner", Vector3(0, 1.28, -1.20), Vector3(.64, 1.30, .03))
	box("gold", Vector3(0, 1.28, -1.218), Vector3(.022, 1.30, .01))
	for y in [.85, 1.72]:
		box("gold", Vector3(0, y, -1.218), Vector3(.64, .022, .01))
	for x in [-.06, .06]:
		sphere("gold", Vector3(x, 1.28, -1.225), .025)
	for x in [-.43, .43]:
		box("stone_light", Vector3(x, 1.30, -1.18), Vector3(.12, 1.53, .20))
	# Two marble columns under a pediment carrying the company roundel.
	for x in [-.56, .56]:
		box("stone_light", Vector3(x, .49, -1.33), Vector3(.17, .06, .17))
		cylinder("stone_light", Vector3(x, 1.24, -1.33), .058, .068, 1.44, Basis.IDENTITY, 10)
		box("gold", Vector3(x, 1.99, -1.33), Vector3(.17, .06, .17))
		face("grime", Vector3(x - .03, 1.9, -1.4), Vector3(x + .03, 1.9, -1.4), Vector3(x + .01, 1.45, -1.4), Vector3(x - .01, 1.45, -1.4))
	box("stone_light", Vector3(0, 2.085, -1.30), Vector3(1.34, .13, .32))
	box("dust", Vector3(0, 2.153, -1.40), Vector3(1.3, .006, .1))
	var left := Vector3(-.67, 2.15, -1.46)
	var apex := Vector3(0, 2.45, -1.46)
	var right := Vector3(.67, 2.15, -1.46)
	var back := Vector3(0, 0, .29)
	triangle("stone_light", left, apex, right)
	face("stone_light", left, apex, apex + back, left + back)
	face("stone_light", apex, right, right + back, apex + back)
	for pair in [[left, apex, Vector2(-.409, .913)], [apex, right, Vector2(.409, .913)]]:
		var lift := Vector3(pair[2].x, pair[2].y, 0) * .012
		beam("dust", pair[0] + lift + Vector3(0, 0, .12), pair[1] + lift + Vector3(0, 0, .12), .012, .22)
	cylinder("banner", Vector3(0, 2.26, -1.47), .1, .1, .02, Basis(Vector3.RIGHT, PI * .5), 16)
	open_wheel(Vector3(0, 2.26, -1.482), .095, .012, Basis(Vector3.RIGHT, PI * .5), "gold")

func tesla_coil() -> void:
	var at := COIL_AT
	box("stone_dark", at + Vector3(0, .36, 0), Vector3(.38, .36, .38))
	box("stone_light", at + Vector3(0, .57, 0), Vector3(.44, .06, .44))
	box("dust", at + Vector3(.11, .603, -.1), Vector3(.18, .006, .2))
	cylinder("dark", at + Vector3(0, 1.08, 0), .06, .085, .96)
	for k in range(8):
		cylinder("gold", at + Vector3(0, .7 + k * .105, 0), .088 - k * .003, .088 - k * .003, .03)
	halo("gold", at + Vector3(0, 1.62, 0), .16, .055)
	cylinder("dark", at + Vector3(0, 1.62, 0), .11, .11, .04)
	crystal("crystal", at + Vector3(0, 1.78, 0), .055, .12)
	# A brass conduit feeds the lab wall.
	beam("gold", at + Vector3(.06, .95, .06), Vector3(-.93, .95, -.72), .04, .04)
	box("dark", Vector3(-.92, .95, -.74), Vector3(.12, .14, .06), Basis(Vector3.UP, TAU / 8.0))

func armillary_pedestal() -> void:
	var at := Vector3(ARMILLARY_AT.x, 0, ARMILLARY_AT.z)
	box("stone_dark", at + Vector3(0, .36, 0), Vector3(.34, .36, .34))
	box("stone_light", at + Vector3(0, .57, 0), Vector3(.42, .06, .42))
	box("dust", at + Vector3(-.1, .603, -.09), Vector3(.16, .006, .18))
	cylinder("gold", at + Vector3(0, .76, 0), .04, .075, .32)
	cylinder("gold", at + Vector3(0, .935, 0), .06, .06, .03)

func armillary_mesh() -> ArrayMesh:
	var saved := surfaces
	surfaces = {}
	for key in ["gold", "crystal", "dark"]:
		var st := SurfaceTool.new()
		st.begin(Mesh.PRIMITIVE_TRIANGLES)
		st.set_material(materials[key])
		surfaces[key] = st
	# A meridian ring, a tilted ecliptic ring and a glowing core; it turns about local Y.
	halo("gold", Vector3.ZERO, .24, .026, Basis(Vector3.RIGHT, PI * .5), 20)
	halo("gold", Vector3.ZERO, .21, .022, Basis(Vector3.FORWARD, .41), 20)
	box("dark", Vector3.ZERO, Vector3(.016, .5, .016))
	sphere("crystal", Vector3.ZERO, .07)
	var mesh := ArrayMesh.new()
	for key in surfaces:
		surfaces[key].commit(mesh)
	surfaces = saved
	return mesh

func arc_sparks(name: String, at: Vector3) -> CPUParticles3D:
	# Short cyan crackles around the coil's crystal.
	var particles := CPUParticles3D.new()
	particles.name = name
	particles.position = at
	particles.amount = 8
	particles.lifetime = 0.35
	particles.preprocess = 0.35
	particles.explosiveness = 0.3
	particles.direction = Vector3(0, 1, 0)
	particles.spread = 180.0
	particles.gravity = Vector3.ZERO
	particles.initial_velocity_min = 0.4
	particles.initial_velocity_max = 0.8
	var ramp := Gradient.new()
	ramp.set_color(0, Color(0.85, 0.97, 1.0, 1.0))
	ramp.set_color(1, Color(0.4, 0.85, 1.0, 0.0))
	particles.color_ramp = ramp
	var quad := QuadMesh.new()
	quad.size = Vector2(0.03, 0.03)
	var mat := StandardMaterial3D.new()
	mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	mat.billboard_mode = BaseMaterial3D.BILLBOARD_PARTICLES
	mat.vertex_color_use_as_albedo = true
	mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	quad.material = mat
	particles.mesh = quad
	particles.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	return particles

func add_moving_parts() -> void:
	# save_compact packs only the static mesh; reopen the scene for the armillary and the sparks.
	assert(ResourceSaver.save(armillary_mesh(), ARMILLARY_MESH, ResourceSaver.FLAG_COMPRESS) == OK)
	var scene: PackedScene = load(SCENE)
	var root := scene.instantiate()
	var visual := root.get_node("Visual") as Node3D
	var armillary := MeshInstance3D.new()
	armillary.name = "Armillary"
	armillary.mesh = load(ARMILLARY_MESH)
	armillary.position = ARMILLARY_AT
	armillary.set_script(load("res://visuals/SpinningPart.cs"))
	armillary.set("Speed", 0.8)
	visual.add_child(armillary)
	armillary.owner = root
	var crackle := arc_sparks("CoilSparks", COIL_AT + Vector3(0, 1.78, 0))
	visual.add_child(crackle)
	crackle.owner = root
	var packed := PackedScene.new()
	assert(packed.pack(root) == OK)
	assert(ResourceSaver.save(packed, SCENE) == OK)
	root.free()
	print("PASS: ResearchLab armillary and coil sparks added")
