extends "res://tools/build_tier_two_base.gd"
## Timber machine workshop: assembly doors, gear sign, boiler and a yard crane.

func _initialize() -> void:
	machine_materials()
	octagonal_base(1.91)
	workshop_shell()
	assembly_doors()
	machine_roof()
	yard_machines()
	validate_clearance()
	save_compact("res://buildings/Workshop.tscn", "res://buildings/meshes/Workshop.res", "Workshop", 8, "workshop", Vector2i(5, 4))
	quit()

func workshop_shell() -> void:
	box("stone_dark", Vector3(-.25, .34, .10), Vector3(2.5, .30, 1.95))
	box("stone_light", Vector3(-.25, .51, .10), Vector3(2.57, .08, 1.99))
	box("wood_warm", Vector3(-.25, 1.59, .10), Vector3(2.36, 2.08, 1.83))
	for z in [-.83, 1.03]:
		for i in range(13):
			box("wood_light" if i % 4 == 0 else "wood_warm", Vector3(-1.37 + i * .186, 1.58, z), Vector3(.172, 1.98, .035))
		for x in [-1.45, -.25, .95]:
			box("wood", Vector3(x, 1.57, z), Vector3(.13, 2.17, .13))
		for y in [.62, 2.62]:
			box("wood", Vector3(-.25, y, z), Vector3(2.52, .15, .14))
		var a := Vector3(-1.45, 2.64, z)
		var b := Vector3(.95, 2.64, z)
		var c := Vector3(-.25, 3.63, z)
		if z < 0: triangle("wood_warm", a, c, b)
		else: triangle("wood_warm", a, b, c)
		beam("wood", a, c, .10, .10)
		beam("wood", b, c, .10, .10)
	for x in [-1.46, .96]:
		for z in [-.73, .10, .93]:
			box("wood", Vector3(x, 1.61, z), Vector3(.10, 2.12, .10))
		beam("wood_light", Vector3(x, .80, -.65), Vector3(x, 1.63, .03), .07, .07)
		beam("wood_light", Vector3(x, 1.63, .17), Vector3(x, 2.46, .84), .07, .07)
		# Small, high-set panes leave the large assembly doors as the main landmark.
		for z in [-.43, .59]:
			box("dark", Vector3(x, 2.12, z), Vector3(.065, .48, .48))
			box("window", Vector3(x * 1.017, 2.12, z), Vector3(.028, .39, .38))
			box("wood", Vector3(x * 1.03, 2.12, z), Vector3(.025, .43, .035))
			box("wood", Vector3(x * 1.03, 2.12, z), Vector3(.025, .035, .43))

func assembly_doors() -> void:
	box("dark", Vector3(-.25, 1.48, -.91), Vector3(1.66, 1.85, .04))
	for i in range(8):
		box("wood_light" if i % 3 else "wood_warm", Vector3(-.97 + i * .205, 1.46, -.945), Vector3(.192, 1.78, .035))
	for x in [-1.11, .61]:
		box("wood", Vector3(x, 1.48, -.965), Vector3(.12, 1.92, .11))
	for y in [.67, 1.22, 2.22]:
		box("dark", Vector3(-.25, y, -.978), Vector3(1.59, .075, .025))
	box("wood", Vector3(-.25, 2.43, -.97), Vector3(1.89, .13, .16))
	for side in [-1.0, 1.0]:
		beam("dark", Vector3(-.25 + side * .06, .77, -.995), Vector3(-.25 + side * .74, 2.12, -.995), .055, .028)
		box("gold", Vector3(-.25 + side * .10, 1.46, -1.018), Vector3(.035, .22, .04))
	# Gear crest reads at map scale without text or a modern factory sign.
	box("dark", Vector3(-.25, 2.97, -.90), Vector3(.82, .69, .06))
	box("banner", Vector3(-.25, 2.97, -.936), Vector3(.76, .63, .025))
	open_wheel(Vector3(-.25, 2.97, -.988), .25, .045, Basis(Vector3.RIGHT, PI * .5), "gold", true)
	for x in [-.80, .30]:
		box("dark", Vector3(x, .205, -1.22), Vector3(.045, .035, .56))
	box("wood_warm", Vector3(-.25, .21, -1.18), Vector3(1.38, .07, .50))

func machine_roof() -> void:
	# Translate the existing slate roof builder into the workshop's offset centre.
	var before: Dictionary = {}
	for key: String in surfaces:
		before[key] = surfaces[key]
	var roof_surfaces: Dictionary = {}
	for key: String in surfaces:
		var part := SurfaceTool.new()
		part.begin(Mesh.PRIMITIVE_TRIANGLES)
		part.set_material(materials[key])
		roof_surfaces[key] = part
	surfaces = roof_surfaces
	roof(1.32, -.96, 1.13, 2.71, 3.73, 6, 9)
	for key: String in surfaces:
		var arrays: Array = surfaces[key].commit_to_arrays()
		if arrays[Mesh.ARRAY_VERTEX] != null and arrays[Mesh.ARRAY_VERTEX].size() > 0:
			before[key].append_from(surfaces[key].commit(), 0, Transform3D(Basis.IDENTITY, Vector3(-.25, 0, 0)))
	surfaces = before
	# Low clerestory vent and a metal-capped stack give a nineteenth-century silhouette.
	box("dark", Vector3(-.25, 3.77, .32), Vector3(.60, .26, .63))
	for z in [.08, .23, .38, .53]:
		box("wood_light", Vector3(-.25, 3.78, z), Vector3(.64, .035, .045))
	box("roof_light", Vector3(-.25, 3.96, .32), Vector3(.79, .10, .79))
	box("gold", Vector3(-.25, 4.02, .32), Vector3(.84, .025, .84))

func yard_machines() -> void:
	# Yard crane has an open timber frame and a hanging hook, not a modern excavator.
	box("stone_dark", Vector3(1.20, .29, -.40), Vector3(.38, .23, .45))
	box("wood", Vector3(1.20, 1.78, -.40), Vector3(.18, 2.82, .18))
	beam("wood_light", Vector3(1.20, 3.10, -.40), Vector3(.56, 3.41, -1.10), .13, .13)
	beam("wood", Vector3(1.20, 2.40, -.40), Vector3(.56, 3.41, -1.10), .095, .095)
	beam("dark", Vector3(.56, 3.39, -1.10), Vector3(.56, 1.68, -1.10), .022, .022)
	beam("dark", Vector3(.56, 1.68, -1.10), Vector3(.66, 1.58, -1.10), .045, .045)
	beam("dark", Vector3(.66, 1.58, -1.10), Vector3(.73, 1.69, -1.10), .045, .045)
	open_wheel(Vector3(1.21, 1.05, -.55), .23, .09, Basis(Vector3.RIGHT, PI * .5), "dark")
	# Compact boiler with copper bands and visible flywheel.
	cylinder("dark", Vector3(1.28, .63, .33), .29, .29, .78, Basis(Vector3.RIGHT, PI * .5))
	for z in [.00, .64]:
		cylinder("canvas", Vector3(1.28, .63, z), .304, .304, .065, Basis(Vector3.RIGHT, PI * .5))
	cylinder("dark", Vector3(1.34, 1.50, .56), .075, .10, 1.18)
	cylinder("canvas", Vector3(1.34, 2.14, .56), .13, .12, .11)
	open_wheel(Vector3(1.58, .53, .29), .27, .065, Basis(Vector3.FORWARD, PI * .5), "gold")
	for x in [.99, 1.54]:
		box("stone_dark", Vector3(x, .25, .32), Vector3(.11, .15, .74))
	crate(Vector3(-1.16, .19, -1.20), .32)
	barrel(Vector3(-.75, .19, -1.35), .17, .41)
