extends "res://tools/build_forge.gd"
## The coal drill (석탄 시추기, server type 10): a fixed fuel pit that never runs dry, worked by labourers at
## its walls (docs/art-direction.md). A steam roadheader chocked in place on its crawler tracks: its hinged
## boom rises and slams a studded cutter drum into a coal seam over and over (ReciprocatingPart with Swing),
## the drum turning as it bites (SpinningPart) and throwing sparks, while the boiler on its back smokes.
## Coal lumps pile beside the pit and a loaded tub waits on a short rail. Modelled on the 3x3 footprint.

const DRILL_SCENE := "res://buildings/CoalDrill.tscn"
const DRILL_MESH := "res://buildings/meshes/CoalDrill.res"
const BOOM_MESH := "res://buildings/meshes/CoalDrillBoom.res"
const CUTTER_MESH := "res://buildings/meshes/CoalDrillCutter.res"
const PIVOT := Vector3(0.0, 0.98, 0.0) # Boom hinge at the front of the machine's turret.
const STRIKE := Vector3(0.0, 0.2, -0.92) # Cutter drum centre at rest, on the seam.
const STACK_TOP := Vector3(0.38, 1.95, 0.95)

func _initialize() -> void:
	drill_materials()
	pad()
	seam()
	tracks()
	machine()
	boiler()
	coal_heap()
	tub_and_rail()
	save_compact(DRILL_SCENE, DRILL_MESH, "CoalDrill", 10, "coal_drill", Vector2i(3, 3))
	add_moving_parts()
	quit()

func apply_server_footprint(building: Node3D) -> void:
	# Authored at its final server footprint, like the derrick.
	assert(building.get("BuildingType") == 10)
	assert(building.get_meta("footprint") == Vector2i(3, 3))

func drill_materials() -> void:
	material("concrete", Color("6f6b64"))
	material("concrete_dark", Color("4c4944"))
	material("coal", Color("1d1f22"), 0.0, 0.62) # A faint sheen on fresh coal.
	material("soot", Color("2a2622"))
	material("iron", Color("2f363d"), 0.7, 0.3) # Lacquered machine plate: a prized engine.
	material("iron_dark", Color("3b434b"), 0.45, 0.6)
	material("iron_light", Color("8f979c"), 0.6, 0.4)
	material("brass", Color("a88349"), 0.7, 0.32)
	material("gold", Color("d9ae45"), 0.8, 0.25)
	material("copper", Color("b0673e"), 0.6, 0.38)
	material("wood", Color("5a4232"))
	material("roof", Color("3d5f77"), 0.35, 0.55) # Painted side plates, company colour.
	material("banner", Color("376a94"))
	material("fire", Color("ff8a3d"), 0.0, 0.5, 2.2)

func pad() -> void:
	box("concrete_dark", Vector3(0, 0.03, 0), Vector3(3.0, 0.06, 3.0))
	box("concrete", Vector3(0, 0.065, 0), Vector3(2.94, 0.012, 2.94))
	# Coal dust trodden along the walls where workers stand.
	box("soot", Vector3(0, 0.073, -1.32), Vector3(2.2, 0.003, 0.26))
	box("soot", Vector3(1.32, 0.073, 0), Vector3(0.26, 0.003, 2.2))
	box("soot", Vector3(-1.32, 0.073, 0.2), Vector3(0.26, 0.003, 1.6))

func seam() -> void:
	# The struck ground: a black coal seam broken open in front of the machine, rimmed by cracked slabs.
	var c := Vector3(STRIKE.x, 0.075, STRIKE.z)
	cylinder("coal", c, 0.4, 0.4, 0.012, Basis.IDENTITY, 18)
	cylinder("soot", c + Vector3(0, 0.002, 0), 0.5, 0.5, 0.008, Basis.IDENTITY, 18)
	for i in range(7):
		var angle := TAU * i / 7.0 + 0.3
		var at := c + Vector3(cos(angle) * 0.42, 0.03, sin(angle) * 0.42)
		box("concrete_dark", at, Vector3(0.16, 0.05, 0.1), Basis(Vector3.UP, -angle) * Basis(Vector3.FORWARD, 0.25 * (1 if i % 2 == 0 else -1)))
	for p in [Vector3(-0.3, 0.05, 0.05), Vector3(0.28, 0.05, -0.12), Vector3(0.1, 0.05, 0.3), Vector3(-0.18, 0.05, -0.3)]:
		sphere("coal", c + p, 0.07, 0.7)

func tracks() -> void:
	# Crawler tracks either side, chocked with timber: this machine is anchored, not going anywhere.
	for side in [-1.0, 1.0]:
		var x: float = side * 0.52
		box("iron_dark", Vector3(x, 0.2, 0.42), Vector3(0.3, 0.28, 1.2))
		for z in [-0.12, 0.96]:
			cylinder("iron_dark", Vector3(x, 0.2, z), 0.14, 0.14, 0.3, Basis(Vector3.FORWARD, PI * 0.5), 12)
		for i in range(9):
			box("iron_light", Vector3(x, 0.345, -0.05 + i * 0.13), Vector3(0.31, 0.012, 0.05))
		for z in [-0.28, 1.12]:
			box("wood", Vector3(x, 0.12, z), Vector3(0.34, 0.12, 0.16), Basis(Vector3.RIGHT, 0.35 * signf(z - 0.4)))
		for z in [0.12, 0.72]:
			box("brass", Vector3(x + side * 0.16, 0.2, z), Vector3(0.02, 0.06, 0.06)) # Anchor bolt heads.

func machine() -> void:
	# The roadheader's body on the tracks, its turret and the boom hinge at the front.
	box("iron", Vector3(0, 0.55, 0.42), Vector3(1.0, 0.42, 1.1))
	for side in [-1.0, 1.0]:
		box("roof", Vector3(side * 0.505, 0.58, 0.42), Vector3(0.012, 0.26, 0.8)) # Painted company side plates.
		for z in [0.08, 0.42, 0.76]:
			sphere("brass", Vector3(side * 0.512, 0.72, z), 0.02)
	box("gold", Vector3(0, 0.58, -0.135), Vector3(0.3, 0.16, 0.012)) # Gilt maker's plate on the nose.
	box("banner", Vector3(0, 0.58, -0.142), Vector3(0.24, 0.1, 0.004))
	cylinder("iron", Vector3(0, 0.84, 0.25), 0.36, 0.42, 0.16, Basis.IDENTITY, 16) # Slewing turret.
	cylinder("brass", Vector3(0, 0.93, 0.25), 0.37, 0.37, 0.03, Basis.IDENTITY, 16)
	# Hinge cheeks the boom swings between, and the two rams that drive it.
	for x in [-0.17, 0.17]:
		box("iron", Vector3(x, PIVOT.y - 0.06, PIVOT.z + 0.04), Vector3(0.06, 0.32, 0.3))
		cylinder("brass", Vector3(x, PIVOT.y, PIVOT.z), 0.07, 0.07, 0.08, Basis(Vector3.FORWARD, PI * 0.5), 10)
		beam("iron_light", Vector3(x * 1.6, 0.66, 0.05), Vector3(x * 1.6, 0.86, -0.2), 0.05, 0.05)
		cylinder("iron_dark", Vector3(x * 1.6, 0.66, 0.05), 0.045, 0.045, 0.16, Basis(Vector3.RIGHT, 0.9), 8)
	# Operator's perch behind the turret under a tin canopy.
	box("wood", Vector3(-0.28, 0.85, 0.72), Vector3(0.22, 0.05, 0.2))
	for p in [Vector3(-0.42, 1.06, 0.6), Vector3(-0.14, 1.06, 0.6), Vector3(-0.42, 1.06, 0.86), Vector3(-0.14, 1.06, 0.86)]:
		box("iron_dark", p, Vector3(0.025, 0.42, 0.025))
	box("roof", Vector3(-0.28, 1.28, 0.73), Vector3(0.36, 0.025, 0.34), Basis(Vector3.RIGHT, 0.12))
	# Gauge and steam line from the boiler to the rams.
	cylinder("brass", Vector3(0.2, 0.9, 0.62), 0.055, 0.055, 0.03, Basis(Vector3.RIGHT, PI * 0.5), 12)
	cylinder("concrete", Vector3(0.2, 0.9, 0.603), 0.042, 0.042, 0.006, Basis(Vector3.RIGHT, PI * 0.5), 12)
	beam("copper", Vector3(0.3, 0.78, 0.82), Vector3(0.3, 0.78, 0.12), 0.05, 0.05)

func boiler() -> void:
	# A copper-banded boiler drum across the back, firebox glowing, a riveted stack smoking above it.
	cylinder("iron_dark", Vector3(0.1, 0.92, 0.96), 0.2, 0.2, 0.86, Basis(Vector3.FORWARD, PI * 0.5), 16)
	for x in [-0.22, 0.1, 0.42]:
		cylinder("copper", Vector3(x, 0.92, 0.96), 0.21, 0.21, 0.04, Basis(Vector3.FORWARD, PI * 0.5), 16)
	box("iron_dark", Vector3(-0.38, 0.6, 0.96), Vector3(0.2, 0.36, 0.34))
	box("fire", Vector3(-0.485, 0.58, 0.96), Vector3(0.004, 0.12, 0.18))
	var stack := Vector3(STACK_TOP.x, 0, STACK_TOP.z)
	cylinder("iron", stack + Vector3(0, 1.5, 0), 0.08, 0.1, 0.9, Basis.IDENTITY, 12)
	for y in [1.25, 1.65]:
		cylinder("brass", stack + Vector3(0, y, 0), 0.105, 0.105, 0.03, Basis.IDENTITY, 12)
	cylinder("soot", stack + Vector3(0, STACK_TOP.y - 0.01, 0), 0.11, 0.095, 0.05, Basis.IDENTITY, 12)

func coal_heap() -> void:
	# The yield: lumps the pounding throws out, heaped at the front right for the labourers.
	var c := Vector3(0.95, 0.07, -0.6)
	cylinder("coal", c + Vector3(0, 0.12, 0), 0.06, 0.36, 0.24, Basis.IDENTITY, 12)
	var lumps := [Vector3(-0.26, 0.05, -0.18), Vector3(0.24, 0.05, 0.16), Vector3(0.04, 0.23, 0.02), Vector3(-0.1, 0.15, 0.18),
		Vector3(0.18, 0.13, -0.13), Vector3(-0.3, 0.04, 0.2), Vector3(0.32, 0.04, -0.05)]
	for p in lumps:
		sphere("coal", c + p, 0.065, 0.75)
	beam("wood", c + Vector3(0.08, 0.18, -0.05), c + Vector3(0.2, 0.58, -0.16), 0.025, 0.025)
	box("iron_dark", c + Vector3(0.05, 0.18, -0.02), Vector3(0.1, 0.13, 0.015), Basis(Vector3.UP, 0.7) * Basis(Vector3.RIGHT, -0.4))

func tub_and_rail() -> void:
	# A short rail along the left side with a loaded tub.
	var x := -1.05
	for dx in [-0.12, 0.12]:
		box("iron_light", Vector3(x + dx, 0.085, -0.3), Vector3(0.025, 0.025, 1.9))
	for i in range(9):
		box("wood", Vector3(x, 0.075, -1.16 + i * 0.22), Vector3(0.36, 0.02, 0.06))
	var tub := Vector3(x, 0.1, -0.7)
	for dx in [-0.12, 0.12]:
		for dz in [-0.12, 0.12]:
			cylinder("iron_dark", tub + Vector3(dx, 0.04, dz), 0.045, 0.045, 0.03, Basis(Vector3.FORWARD, PI * 0.5), 10)
	face("iron_dark", tub + Vector3(-0.17, 0.08, -0.2), tub + Vector3(0.17, 0.08, -0.2), tub + Vector3(0.2, 0.32, -0.24), tub + Vector3(-0.2, 0.32, -0.24))
	face("iron_dark", tub + Vector3(-0.17, 0.08, 0.2), tub + Vector3(0.17, 0.08, 0.2), tub + Vector3(0.2, 0.32, 0.24), tub + Vector3(-0.2, 0.32, 0.24))
	face("iron_dark", tub + Vector3(-0.17, 0.08, -0.2), tub + Vector3(-0.17, 0.08, 0.2), tub + Vector3(-0.2, 0.32, 0.24), tub + Vector3(-0.2, 0.32, -0.24))
	face("iron_dark", tub + Vector3(0.17, 0.08, -0.2), tub + Vector3(0.17, 0.08, 0.2), tub + Vector3(0.2, 0.32, 0.24), tub + Vector3(0.2, 0.32, -0.24))
	box("iron_dark", tub + Vector3(0, 0.09, 0), Vector3(0.34, 0.02, 0.4))
	for p in [Vector3(-0.08, 0.32, -0.08), Vector3(0.07, 0.33, 0.06), Vector3(0, 0.36, -0.02), Vector3(-0.06, 0.32, 0.1)]:
		sphere("coal", tub + p, 0.07, 0.7)

func part_surfaces(keys: Array) -> Dictionary:
	var saved := surfaces
	surfaces = {}
	for key in keys:
		var st := SurfaceTool.new()
		st.begin(Mesh.PRIMITIVE_TRIANGLES)
		st.set_material(materials[key])
		surfaces[key] = st
	return saved

func commit_part(saved: Dictionary) -> ArrayMesh:
	var mesh := ArrayMesh.new()
	for key in surfaces:
		surfaces[key].commit(mesh)
	surfaces = saved
	return mesh

func boom_mesh() -> ArrayMesh:
	# Authored about the hinge: the boom reaches from the origin to the cutter drum at rest.
	var saved := part_surfaces(["iron", "iron_light", "brass", "roof"])
	var tip := STRIKE - PIVOT
	cylinder("brass", Vector3.ZERO, 0.075, 0.075, 0.28, Basis(Vector3.FORWARD, PI * 0.5), 12)
	beam("iron", Vector3(0, 0, -0.02), tip * 0.92, 0.22, 0.2)
	beam("roof", Vector3(0, 0.105, -0.06), tip * 0.8 + Vector3(0, 0.105, 0), 0.16, 0.012) # Painted top plate.
	for t in [0.25, 0.5, 0.75]:
		box("iron_light", tip * t, Vector3(0.24, 0.05, 0.05), Basis(Quaternion(Vector3.UP, tip.normalized())))
	# Fork arms holding the drum axle.
	for x in [-0.16, 0.16]:
		beam("iron", tip * 0.85 + Vector3(x, 0, 0), tip + Vector3(x, 0, 0), 0.05, 0.12)
		cylinder("brass", tip + Vector3(x, 0, 0), 0.06, 0.06, 0.05, Basis(Vector3.FORWARD, PI * 0.5), 10)
	return commit_part(saved)

func cutter_mesh() -> ArrayMesh:
	# The studded cutter drum about its own Y axis, so SpinningPart turns it on the axle.
	var saved := part_surfaces(["iron_light", "iron"])
	cylinder("iron", Vector3.ZERO, 0.15, 0.15, 0.26, Basis.IDENTITY, 14)
	for ring in range(3):
		var y := -0.09 + ring * 0.09
		for i in range(7):
			var angle := TAU * (i + ring * 0.5) / 7.0
			var out := Vector3(cos(angle), 0, sin(angle))
			cylinder("iron_light", Vector3(0, y, 0) + out * 0.19, 0.0, 0.035, 0.09, Basis(Quaternion(Vector3.UP, out)), 6)
	return commit_part(saved)

func add_moving_parts() -> void:
	assert(ResourceSaver.save(boom_mesh(), BOOM_MESH, ResourceSaver.FLAG_COMPRESS) == OK)
	assert(ResourceSaver.save(cutter_mesh(), CUTTER_MESH, ResourceSaver.FLAG_COMPRESS) == OK)
	var scene: PackedScene = load(DRILL_SCENE)
	var root := scene.instantiate()
	var visual := root.get_node("Visual") as Node3D
	var boom := MeshInstance3D.new()
	boom.name = "Boom"
	boom.mesh = load(BOOM_MESH)
	boom.position = PIVOT
	boom.set_script(load("res://visuals/ReciprocatingPart.cs"))
	boom.set("Swing", 0.5) # Rises about 29 degrees and slams back onto the seam.
	boom.set("Period", 1.4)
	visual.add_child(boom)
	boom.owner = root
	var cutter := MeshInstance3D.new()
	cutter.name = "Cutter"
	cutter.mesh = load(CUTTER_MESH)
	# The drum's own Y axis lies across the boom (world X): it rolls into the seam.
	cutter.transform = Transform3D(Basis(Vector3.FORWARD, PI * 0.5), STRIKE - PIVOT)
	cutter.set_script(load("res://visuals/SpinningPart.cs"))
	cutter.set("Speed", 4.0)
	boom.add_child(cutter)
	cutter.owner = root
	visual.add_child(sparks("ImpactSparks", STRIKE + Vector3(0, -0.12, -0.05), 1.1, 8))
	visual.get_node("ImpactSparks").owner = root
	var dust := smoke("Dust", STRIKE + Vector3(0, -0.1, 0))
	dust.amount = 6
	dust.scale_amount_min = 0.25
	dust.scale_amount_max = 0.4
	dust.initial_velocity_min = 0.15
	dust.initial_velocity_max = 0.3
	visual.add_child(dust)
	dust.owner = root
	var stack := smoke("Smoke", STACK_TOP + Vector3(0, 0.05, 0))
	stack.amount = 10
	stack.scale_amount_min = 0.3
	stack.scale_amount_max = 0.45
	visual.add_child(stack)
	stack.owner = root
	var packed := PackedScene.new()
	assert(packed.pack(root) == OK)
	assert(ResourceSaver.save(packed, DRILL_SCENE) == OK)
	root.free()
	print("PASS: CoalDrill boom, cutter, sparks and smoke added")
