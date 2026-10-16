extends "res://tools/build_tier_two_base.gd"
## The workshop (server type 8): the company's machine assembly works, where flame automatons and
## steam artillery are built (docs/art-direction.md). Unlike the black arsenal and the bright branch
## it is an iron-and-glass hall: a tall glazed gable roof with a ridge lantern, an open portal onto
## a walker hull being assembled, a company-painted gantry crane lowering a boiler in the yard, a big
## flywheel turning on the side wall (SpinningPart) and white steam from the roof vents.
## Authored at world scale on the 4x4 footprint; everything stays within radius 2 as the visual turns.

const SCENE := "res://buildings/Workshop.tscn"
const MESH := "res://buildings/meshes/Workshop.res"
const FLYWHEEL_MESH := "res://buildings/meshes/WorkshopFlywheel.res"
const FLYWHEEL_AT := Vector3(1.33, 1.0, 0.45)

func _initialize() -> void:
	works_materials()
	works_base()
	assembly_hall()
	glass_roof()
	assembly_floor()
	gantry()
	yard_parts()
	validate_clearance()
	save_compact(SCENE, MESH, "Workshop", 8, "workshop", Vector2i(4, 4))
	add_moving_parts()
	quit()

func works_materials() -> void:
	material("concrete", Color("858179"))
	material("concrete_dark", Color("5c5953"))
	material("brick", Color("74463a"))
	material("iron", Color("2f363d"), 0.6, 0.4)
	material("iron_light", Color("8f979c"), 0.65, 0.35)
	material("brass", Color("a88349"), 0.75, 0.3)
	material("copper", Color("b0673e"), 0.7, 0.32)
	material("glass", Color("5d6f6e"), 0.3, 0.25, 0.05) # Grimy factory glazing.
	material("roof", Color("3d5f77"), 0.2, 0.65) # Company-painted steel.
	material("banner", Color("376a94"))
	material("soot", Color("1e2226"))
	material("wood", Color("5a4232"))
	material("paper", Color("d6ccb0"))
	material("lamp", Color("ffd37a"), 0.0, 0.3, 1.6)

func works_base() -> void:
	var outline: Array = []
	for i in range(8):
		var a := TAU * (i + 0.5) / 8.0
		outline.append(Vector2(cos(a), sin(a)) * 1.92)
	for i in range(8):
		var a: Vector2 = outline[i]
		var b: Vector2 = outline[(i + 1) % 8]
		face("concrete", Vector3(0, 0.12, 0), Vector3(a.x, 0.12, a.y), Vector3(b.x, 0.12, b.y), Vector3(0, 0.12, 0))
		face("concrete_dark", Vector3(a.x, 0, a.y), Vector3(a.x, 0.12, a.y), Vector3(b.x, 0.12, b.y), Vector3(b.x, 0, b.y))
	for x in [-0.4, 0.4]: # Rails out of the portal for finished machines.
		box("iron_light", Vector3(x, 0.135, -1.2), Vector3(0.03, 0.02, 1.1))

func assembly_hall() -> void:
	var x0 := -1.2
	var x1 := 1.2
	var z0 := -0.75
	var z1 := 1.15
	var eave := 1.9
	var cz := (z0 + z1) * 0.5
	# Brick base course, glass curtain above between iron columns.
	box("brick", Vector3(0, 0.62, (cz + z1) * 0.5 + 0.05), Vector3(x1 - x0, 1.0, z1 - cz))
	for x in [x0, x1]:
		box("brick", Vector3(x, 0.62, cz), Vector3(0.06, 1.0, z1 - z0))
		box("glass", Vector3(x, 1.5, cz), Vector3(0.03, 0.78, z1 - z0))
	box("glass", Vector3(0, 1.5, z1), Vector3(x1 - x0, 0.78, 0.03))
	for side_x in [-1.0, 1.0]:
		for z in [z0, -0.12, 0.52, z1]:
			box("iron", Vector3(side_x * x1, (0.12 + eave) * 0.5, z), Vector3(0.09, eave - 0.12, 0.09))
		box("iron", Vector3(side_x * x1, eave, cz), Vector3(0.1, 0.08, z1 - z0 + 0.06))
		box("iron", Vector3(side_x * x1, 1.12, cz), Vector3(0.08, 0.05, z1 - z0))
		for k in range(1, 6): # Glazing bars.
			box("iron", Vector3(side_x * (x1 + 0.005), 1.5, z0 + (z1 - z0) * k / 6.0), Vector3(0.02, 0.78, 0.02))
	# Front: a wide open portal under a riveted lintel, glass either side.
	for x in [-0.95, 0.95]:
		box("brick", Vector3(x, 0.62, z0), Vector3(0.5, 1.0, 0.06))
		box("glass", Vector3(x, 1.5, z0), Vector3(0.5, 0.78, 0.03))
	for x in [-0.68, 0.68]:
		box("iron", Vector3(x, (0.12 + eave) * 0.5, z0), Vector3(0.1, eave - 0.12, 0.1))
	box("iron", Vector3(0, 1.62, z0), Vector3(1.46, 0.16, 0.12))
	for x in [-0.55, -0.3, -0.05, 0.2, 0.45]:
		sphere("iron_light", Vector3(x + 0.05, 1.62, z0 - 0.065), 0.018)
	box("iron", Vector3(0, 1.82, z0), Vector3(2.4, 0.16, 0.06))
	box("soot", Vector3(0, 0.84, z1 - 0.06), Vector3(2.3, 1.44, 0.02)) # The shadowed back of the hall.
	box("concrete_dark", Vector3(0, 0.13, cz), Vector3(2.3, 0.02, 1.8)) # Shop floor.
	# Copper steam main along the left wall, with gauges.
	cylinder("copper", Vector3(x0 - 0.08, 0.95, cz), 0.04, 0.04, z1 - z0, Basis(Vector3.RIGHT, PI * 0.5), 10)
	for z in [-0.3, 0.4]:
		cylinder("brass", Vector3(x0 - 0.13, 1.08, z), 0.06, 0.06, 0.02, Basis(Vector3.FORWARD, PI * 0.5), 12)
		cylinder("paper", Vector3(x0 - 0.142, 1.08, z), 0.045, 0.045, 0.004, Basis(Vector3.FORWARD, PI * 0.5), 12)

func glass_roof() -> void:
	# A tall glazed gable with an iron frame, and a ridge lantern capped in the company colour.
	var x0 := -1.27
	var x1 := 1.27
	var z0 := -0.8
	var z1 := 1.2
	var eave := 1.95
	var ridge := 2.6
	face("glass", Vector3(x0, eave, z0), Vector3(0, ridge, z0), Vector3(0, ridge, z1), Vector3(x0, eave, z1))
	face("glass", Vector3(x1, eave, z0), Vector3(0, ridge, z0), Vector3(0, ridge, z1), Vector3(x1, eave, z1))
	for k in range(6):
		var z := lerpf(z0, z1, k / 5.0)
		beam("iron", Vector3(x0, eave, z), Vector3(0, ridge, z), 0.04, 0.04)
		beam("iron", Vector3(0, ridge, z), Vector3(x1, eave, z), 0.04, 0.04)
	for t in [0.33, 0.66]:
		for side in [-1.0, 1.0]:
			var p := Vector3(side * 1.27, eave, 0).lerp(Vector3(0, ridge, 0), t)
			box("iron", Vector3(p.x, p.y + 0.01, (z0 + z1) * 0.5), Vector3(0.03, 0.03, z1 - z0))
	# Gable ends glazed, the company roundel at the front apex.
	for z in [z0, z1]:
		face("glass", Vector3(x0 + 0.05, eave, z), Vector3(0, ridge - 0.04, z), Vector3(x1 - 0.05, eave, z), Vector3(x0 + 0.05, eave, z))
		beam("iron", Vector3(0, eave, z), Vector3(0, ridge, z), 0.04, 0.04)
	cylinder("iron", Vector3(0, 2.22, z0 - 0.03), 0.17, 0.17, 0.03, Basis(Vector3.RIGHT, PI * 0.5), 16)
	cylinder("banner", Vector3(0, 2.22, z0 - 0.05), 0.13, 0.13, 0.01, Basis(Vector3.RIGHT, PI * 0.5), 16)
	box("paper", Vector3(0, 2.22, z0 - 0.056), Vector3(0.14, 0.04, 0.004))
	for side in [-1.0, 1.0]:
		var lo := Vector3(side * 1.27, eave, 0)
		var hi := Vector3(0, ridge, 0)
		var p := lo.lerp(hi, 0.85)
		box("soot", Vector3(p.x, p.y + 0.02, (z0 + z1) * 0.5), Vector3(0.22, 0.012, z1 - z0), Basis(Vector3.BACK, side * atan2(ridge - eave, 1.27)))
	# Ridge lantern.
	box("glass", Vector3(0, 2.7, 0.2), Vector3(0.5, 0.22, 1.5))
	for x in [-0.25, 0.25]:
		for k in range(5):
			box("iron", Vector3(x, 2.7, -0.5 + k * 0.35), Vector3(0.03, 0.22, 0.03))
	face("roof", Vector3(-0.32, 2.81, -0.58), Vector3(0, 2.92, -0.58), Vector3(0, 2.92, 0.98), Vector3(-0.32, 2.81, 0.98))
	face("roof", Vector3(0.32, 2.81, -0.58), Vector3(0, 2.92, -0.58), Vector3(0, 2.92, 0.98), Vector3(0.32, 2.81, 0.98))
	# Steam vents at the back of the roof.
	for x in [-0.72, 0.72]:
		cylinder("copper", Vector3(x, 2.45, 0.92), 0.06, 0.07, 0.6, Basis.IDENTITY, 10)
		cylinder("brass", Vector3(x, 2.76, 0.92), 0.085, 0.085, 0.04, Basis.IDENTITY, 10)
	# Lamps over the portal.
	for x in [-0.45, 0.45]:
		beam("iron", Vector3(x, 1.75, -0.8), Vector3(x, 1.7, -0.95), 0.02, 0.02)
		box("lamp", Vector3(x, 1.65, -0.95), Vector3(0.07, 0.08, 0.07))

func assembly_floor() -> void:
	# A steam-walker hull on stands, half its legs fitted, under the shop's travelling crane.
	var hull := Vector3(0, 0.62, 0.25)
	for x in [-0.32, 0.32]:
		box("iron_light", Vector3(x, 0.32, hull.z), Vector3(0.08, 0.4, 0.5))
	box("iron", hull, Vector3(0.9, 0.32, 0.7))
	box("roof", hull + Vector3(0, 0.17, 0), Vector3(0.84, 0.02, 0.64)) # Primer in the company colour.
	cylinder("copper", hull + Vector3(0, 0.26, 0.12), 0.14, 0.14, 0.5, Basis(Vector3.FORWARD, PI * 0.5), 12)
	for side in [-1.0, 1.0]:
		for z in [-0.2, 0.2]:
			beam("iron_light", hull + Vector3(side * 0.45, -0.05, z), hull + Vector3(side * 0.62, -0.45, z - 0.05), 0.04, 0.04)
	box("iron", Vector3(0, 1.72, 0.25), Vector3(2.3, 0.1, 0.12))
	box("iron", Vector3(0.1, 1.62, 0.25), Vector3(0.18, 0.1, 0.18))
	box("iron", Vector3(0.1, 1.28, 0.25), Vector3(0.012, 0.58, 0.012))
	# Workbench and parts racks inside the portal.
	box("wood", Vector3(-0.9, 0.42, 0.7), Vector3(0.4, 0.06, 0.25))
	for x in [-1.05, -0.75]:
		box("wood", Vector3(x, 0.27, 0.7), Vector3(0.04, 0.3, 0.2))

func gantry() -> void:
	# A company-painted gantry crane straddling the yard, lowering a boiler onto a wheeled chassis.
	var z := -1.22
	for x in [-1.28, 1.28]:
		beam("roof", Vector3(x, 0.12, z - 0.16), Vector3(x, 2.05, z), 0.07, 0.07)
		beam("roof", Vector3(x, 0.12, z + 0.16), Vector3(x, 2.05, z), 0.07, 0.07)
		box("iron", Vector3(x, 0.17, z), Vector3(0.12, 0.1, 0.4))
	box("roof", Vector3(0, 2.08, z), Vector3(2.66, 0.12, 0.14))
	box("iron", Vector3(0, 2.16, z), Vector3(2.6, 0.03, 0.18))
	box("iron", Vector3(-0.15, 1.97, z), Vector3(0.22, 0.12, 0.2)) # Trolley.
	box("iron", Vector3(-0.15, 1.6, z), Vector3(0.012, 0.62, 0.012))
	cylinder("copper", Vector3(-0.15, 1.12, z), 0.17, 0.17, 0.55, Basis(Vector3.FORWARD, PI * 0.5), 14)
	for x in [-0.42, 0.12]:
		cylinder("brass", Vector3(x, 1.12, z), 0.18, 0.18, 0.03, Basis(Vector3.FORWARD, PI * 0.5), 14)
	box("wood", Vector3(-0.15, 0.3, z), Vector3(0.7, 0.08, 0.36))
	for x in [-0.4, 0.1]:
		for dz in [-0.2, 0.2]:
			cylinder("iron", Vector3(x, 0.2, z + dz), 0.08, 0.08, 0.04, Basis(Vector3.RIGHT, PI * 0.5), 10)

func yard_parts() -> void:
	# Spare walker legs leaning on the hall and a stack of road wheels.
	for k in range(3):
		beam("iron_light", Vector3(-1.27, 0.14, -0.55 + k * 0.18), Vector3(-1.32, 1.0, -0.45 + k * 0.18), 0.05, 0.05)
	for i in range(3):
		cylinder("iron", Vector3(0.95, 0.17 + i * 0.07, -0.95), 0.16, 0.16, 0.06, Basis.IDENTITY, 14)
		cylinder("brass", Vector3(0.95, 0.205 + i * 0.07, -0.95), 0.06, 0.06, 0.012, Basis.IDENTITY, 10)
	box("wood", Vector3(0.95, 0.25, -0.62), Vector3(0.24, 0.24, 0.24))

func flywheel_mesh() -> ArrayMesh:
	var saved := surfaces
	surfaces = {}
	for key in ["iron", "brass", "iron_light"]:
		var st := SurfaceTool.new()
		st.begin(Mesh.PRIMITIVE_TRIANGLES)
		st.set_material(materials[key])
		surfaces[key] = st
	# Rim, six spokes and a hub; local axis Y.
	var r := 0.5
	for i in range(20):
		var a := TAU * i / 20.0
		box("iron", Vector3(cos(a), 0, sin(a)) * r, Vector3(0.1, 0.09, r * TAU / 20.0 + 0.01), Basis(Vector3.UP, -a))
	for i in range(6):
		var a := TAU * i / 6.0
		box("iron_light", Vector3(cos(a), 0, sin(a)) * r * 0.5, Vector3(r * 0.95, 0.05, 0.05), Basis(Vector3.UP, -a))
	cylinder("brass", Vector3.ZERO, 0.08, 0.08, 0.14, Basis.IDENTITY, 12)
	var mesh := ArrayMesh.new()
	for key in surfaces:
		surfaces[key].commit(mesh)
	surfaces = saved
	return mesh

func add_moving_parts() -> void:
	# save_compact packs only the static mesh; reopen the scene for the flywheel and the steam.
	assert(ResourceSaver.save(flywheel_mesh(), FLYWHEEL_MESH, ResourceSaver.FLAG_COMPRESS) == OK)
	var scene: PackedScene = load(SCENE)
	var root := scene.instantiate()
	var visual := root.get_node("Visual") as Node3D
	var wheel := MeshInstance3D.new()
	wheel.name = "Flywheel"
	wheel.mesh = load(FLYWHEEL_MESH)
	wheel.transform = Transform3D(Basis(Vector3.FORWARD, PI * 0.5), FLYWHEEL_AT)
	wheel.set_script(load("res://visuals/SpinningPart.cs"))
	wheel.set("Speed", 2.2)
	visual.add_child(wheel)
	wheel.owner = root
	for x in [-0.72, 0.72]:
		var plume := smoke("Steam" + ("Left" if x < 0 else "Right"), Vector3(x, 2.8, 0.92))
		plume.amount = 6
		plume.lifetime = 1.6
		plume.preprocess = 1.6
		plume.scale_amount_min = 0.1
		plume.scale_amount_max = 0.15
		var ramp := Gradient.new() # White steam, not the arsenal's smoke.
		ramp.set_color(0, Color(0.95, 0.95, 0.95, 0.75))
		ramp.set_color(1, Color(1, 1, 1, 0.0))
		plume.color_ramp = ramp
		visual.add_child(plume)
		plume.owner = root
	var packed := PackedScene.new()
	assert(packed.pack(root) == OK)
	assert(ResourceSaver.save(packed, SCENE) == OK)
	root.free()
	print("PASS: Workshop flywheel and steam added")
