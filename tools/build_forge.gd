extends "res://tools/build_supply.gd"
## The arsenal (조병창, server type 5, key "forge"): the company's arms works, where army attack
## and armour research happens (docs/art-direction.md). A brick foundry hall under a sawtooth roof
## with an open bay onto a glowing forge floor and a steam hammer that keeps striking
## (ReciprocatingPart), a riveted cupola furnace with a smoking stack and a molten tap, and crated
## rifles, armour plate and shells in the yard. Source grid 3x2, scaled to the server's 4x3.

const SCENE := "res://buildings/Forge.tscn"
const MESH := "res://buildings/meshes/Forge.res"
const HAMMER_MESH := "res://buildings/meshes/ForgeHammer.res"
const HAMMER_REST := Vector3(-0.45, 0.285, -0.06)

func _initialize() -> void:
	arsenal_materials()
	arsenal_yard()
	foundry_hall()
	sawtooth_roof()
	forge_floor()
	cupola()
	munitions()
	save_compact(SCENE, MESH, "Forge", 5, "forge", Vector2i(3, 2))
	add_moving_parts()
	quit()

func arsenal_materials() -> void:
	material("brick", Color("74443a"))
	material("brick_dark", Color("4f2e26"))
	material("soot", Color("1e2226"))
	material("cobble", Color("4f4c47"))
	material("iron", Color("2f363d"), 0.55, 0.45)
	material("iron_light", Color("7d868d"), 0.6, 0.4)
	material("brass", Color("a88349"), 0.7, 0.32)
	material("wood", Color("5a4232"))
	material("paper", Color("d6ccb0"))
	material("glass", Color("7c6a48"), 0.0, 0.4, 0.25)
	material("roof", Color("3d5f77"), 0.35, 0.55) # Painted iron, company colour.
	material("banner", Color("376a94"))
	material("ember", Color("e8642a"), 0.0, 0.6, 1.6)
	material("fire", Color("ffc86a"), 0.0, 0.5, 2.2)

func arsenal_yard() -> void:
	box("cobble", Vector3(0, 0.03, 0), Vector3(3.0, 0.06, 2.0))
	box("soot", Vector3(-0.4, 0.062, -0.62), Vector3(1.1, 0.003, 0.4)) # Scorched in front of the bay.
	for x in [-1.2, -0.7, 0.3]:
		box("iron", Vector3(x, 0.065, -0.75), Vector3(0.02, 0.006, 0.5)) # Rails into the hall.

func foundry_hall() -> void:
	var x0 := -1.45
	var x1 := 0.5
	var z0 := -0.35
	var z1 := 0.95
	var back := 0.25 # The bay is a recess down to here.
	var eave := 1.4
	var bay0 := -0.95
	var bay1 := 0.05
	var bay_top := 1.1
	var cx := (x0 + x1) * 0.5
	# Rear block, and the front built around an open bay.
	box("brick", Vector3(cx, (0.06 + eave) * 0.5, (back + z1) * 0.5), Vector3(x1 - x0, eave - 0.06, z1 - back))
	box("brick", Vector3((x0 + bay0) * 0.5, (0.06 + eave) * 0.5, (z0 + back) * 0.5), Vector3(bay0 - x0, eave - 0.06, back - z0))
	box("brick", Vector3((bay1 + x1) * 0.5, (0.06 + eave) * 0.5, (z0 + back) * 0.5), Vector3(x1 - bay1, eave - 0.06, back - z0))
	box("brick", Vector3((bay0 + bay1) * 0.5, (bay_top + eave) * 0.5, (z0 + back) * 0.5), Vector3(bay1 - bay0, eave - bay_top, back - z0))
	box("soot", Vector3((bay0 + bay1) * 0.5, bay_top + 0.005, (z0 + back) * 0.5), Vector3(bay1 - bay0, 0.01, back - z0)) # Recess ceiling.
	box("soot", Vector3(cx, eave - 0.1, z0 - 0.004), Vector3(x1 - x0, 0.2, 0.006))
	for x in [x0, bay0, bay1, x1]:
		box("brick_dark", Vector3(x, (0.06 + eave) * 0.5, z0 - 0.02), Vector3(0.1, eave - 0.06, 0.04))
	for z in [-0.05, 0.35, 0.7]:
		box("glass", Vector3(x0 - 0.008, 0.85, z), Vector3(0.008, 0.6, 0.2))
		cylinder("glass", Vector3(x0 - 0.008, 1.15, z), 0.1, 0.1, 0.008, Basis(Vector3.FORWARD, PI * 0.5), 10)
	box("iron", Vector3((bay0 + bay1) * 0.5, bay_top + 0.03, z0 - 0.04), Vector3(bay1 - bay0 + 0.1, 0.08, 0.06))
	for x in [bay0, bay1]:
		for y in [0.3, 0.6, 0.9]:
			sphere("iron_light", Vector3(x, y, z0 - 0.045), 0.016)
	# Company board over the bay.
	box("wood", Vector3(-0.45, 1.27, z0 - 0.03), Vector3(1.0, 0.16, 0.03))
	box("banner", Vector3(-0.45, 1.27, z0 - 0.05), Vector3(0.94, 0.12, 0.01))
	for i in range(6):
		box("paper", Vector3(-0.85 + i * 0.16, 1.27, z0 - 0.057), Vector3(0.09, 0.06, 0.004))
	# A smaller door on the right.
	box("iron", Vector3(0.28, 0.4, z0 - 0.02), Vector3(0.3, 0.68, 0.03))
	box("glass", Vector3(0.28, 0.6, z0 - 0.04), Vector3(0.18, 0.12, 0.006))

func sawtooth_roof() -> void:
	# Three teeth: painted iron slopes face the street, the glazed north lights face the back.
	var x0 := -1.48
	var x1 := 0.53
	var eave := 1.4
	var rise := 0.42
	for i in range(3):
		var za := -0.38 + i * 0.443
		var zb := za + 0.443
		face("roof", Vector3(x0, eave, za), Vector3(x1, eave, za), Vector3(x1, eave + rise, zb), Vector3(x0, eave + rise, zb))
		face("glass", Vector3(x0, eave, zb), Vector3(x1, eave, zb), Vector3(x1, eave + rise, zb), Vector3(x0, eave + rise, zb))
		box("iron", Vector3((x0 + x1) * 0.5, eave + rise + 0.015, zb), Vector3(x1 - x0 + 0.02, 0.03, 0.04))
		for x in [x0, x1]:
			face("brick_dark", Vector3(x, eave, za), Vector3(x, eave + rise, zb), Vector3(x, eave, zb), Vector3(x, eave, za))
		for k in range(1, 6): # Seams in the painted iron.
			var x := lerpf(x0, x1, k / 6.0)
			beam("iron", Vector3(x, eave + 0.01, za), Vector3(x, eave + rise + 0.01, zb), 0.015, 0.015)

func forge_floor() -> void:
	# In the bay: the glowing hearth on the back wall, the steam hammer frame over its anvil.
	box("ember", Vector3(-0.45, 0.42, 0.235), Vector3(0.7, 0.6, 0.02))
	box("fire", Vector3(-0.45, 0.28, 0.22), Vector3(0.45, 0.2, 0.02))
	box("iron", Vector3(-0.45, 0.13, -0.05), Vector3(0.36, 0.14, 0.24))
	box("iron_light", Vector3(-0.45, 0.22, -0.05), Vector3(0.3, 0.04, 0.2)) # Anvil face.
	box("ember", Vector3(-0.45, 0.255, -0.06), Vector3(0.16, 0.03, 0.06)) # The billet being struck.
	for x in [-0.78, -0.12]:
		box("iron", Vector3(x, 0.58, -0.05), Vector3(0.08, 1.04, 0.12))
	box("iron", Vector3(-0.45, 1.02, -0.05), Vector3(0.76, 0.1, 0.16))
	cylinder("brass", Vector3(-0.45, 0.93, -0.05), 0.08, 0.08, 0.1)

func cupola() -> void:
	# A riveted cupola furnace on the right with a charging bridge, its stack trailing smoke.
	var c := Vector3(1.0, 0, 0.35)
	cylinder("brick_dark", c + Vector3(0, 0.2, 0), 0.38, 0.4, 0.28, Basis.IDENTITY, 16)
	cylinder("iron", c + Vector3(0, 1.0, 0), 0.32, 0.36, 1.4, Basis.IDENTITY, 16)
	for y in [0.45, 0.8, 1.15, 1.5]:
		cylinder("iron_light", c + Vector3(0, y, 0), 0.355 - y * 0.02, 0.36 - y * 0.02, 0.04, Basis.IDENTITY, 16)
	cylinder("iron", c + Vector3(0, 2.2, 0), 0.17, 0.22, 1.0, Basis.IDENTITY, 14)
	cylinder("soot", c + Vector3(0, 2.66, 0), 0.172, 0.175, 0.12, Basis.IDENTITY, 14)
	cylinder("iron", c + Vector3(0, 2.74, 0), 0.21, 0.21, 0.04, Basis.IDENTITY, 14)
	# Tap hole and a molten runner into a ladle at the front.
	box("fire", c + Vector3(0, 0.38, -0.36), Vector3(0.1, 0.08, 0.04))
	box("ember", c + Vector3(0, 0.3, -0.5), Vector3(0.08, 0.02, 0.26), Basis(Vector3.RIGHT, -0.2))
	cylinder("iron", c + Vector3(0, 0.17, -0.68), 0.12, 0.09, 0.2, Basis.IDENTITY, 12)
	cylinder("fire", c + Vector3(0, 0.275, -0.68), 0.1, 0.1, 0.01, Basis.IDENTITY, 12)
	# Charging bridge from the roof line with a skip of coke.
	box("iron", Vector3(0.68, 1.42, 0.35), Vector3(0.3, 0.04, 0.24))
	for z in [0.24, 0.46]:
		box("iron", Vector3(0.68, 1.5, z), Vector3(0.3, 0.012, 0.012))
	box("iron", Vector3(0.62, 1.5, 0.35), Vector3(0.14, 0.1, 0.12))
	box("soot", Vector3(0.62, 1.555, 0.35), Vector3(0.12, 0.02, 0.1))
	# Furnace glow through an inspection port.
	box("ember", c + Vector3(-0.2, 1.0, -0.27), Vector3(0.1, 0.08, 0.02), Basis(Vector3.UP, 0.6))

func munitions() -> void:
	# Long rifle crates, stacked armour plate, a pyramid of shells and bar stock in the yard.
	for i in range(3):
		var p := Vector3(-1.2, 0.1 + i * 0.09, -0.78 + (i % 2) * 0.02)
		box("wood", p, Vector3(0.5, 0.08, 0.16))
		box("paper", p + Vector3(0, 0, -0.082), Vector3(0.16, 0.04, 0.004))
	for i in range(4):
		box("iron_light", Vector3(0.35, 0.09 + i * 0.03, -0.72), Vector3(0.36, 0.025, 0.24), Basis(Vector3.UP, 0.08 * i))
	for row in range(3):
		for k in range(3 - row):
			var p := Vector3(0.95 + k * 0.08 + row * 0.04, 0.1 + row * 0.07, -0.85)
			cylinder("brass", p, 0.035, 0.035, 0.1, Basis.IDENTITY, 8)
			sphere("iron", p + Vector3(0, 0.06, 0), 0.034)
	for i in range(5):
		box("iron", Vector3(-0.15, 0.08 + (i / 3) * 0.04, -0.85 + (i % 3) * 0.06), Vector3(0.3, 0.035, 0.05))
	cylinder("iron", Vector3(1.35, 0.21, -0.3), 0.11, 0.11, 0.3, Basis.IDENTITY, 12) # Quench drum.
	cylinder("soot", Vector3(1.35, 0.365, -0.3), 0.095, 0.095, 0.01, Basis.IDENTITY, 12)

func hammer_mesh() -> ArrayMesh:
	# The striking ram: iron head and tup on a brass piston rod, rest pose on the billet.
	var saved := surfaces
	surfaces = {}
	for key in ["iron", "brass", "iron_light"]:
		var st := SurfaceTool.new()
		st.begin(Mesh.PRIMITIVE_TRIANGLES)
		st.set_material(materials[key])
		surfaces[key] = st
	box("iron", Vector3(0, 0.08, 0), Vector3(0.24, 0.14, 0.18))
	box("iron_light", Vector3(0, 0.0, 0), Vector3(0.18, 0.025, 0.14))
	cylinder("brass", Vector3(0, 0.33, 0), 0.03, 0.03, 0.4, Basis.IDENTITY, 10)
	var mesh := ArrayMesh.new()
	for key in surfaces:
		surfaces[key].commit(mesh)
	surfaces = saved
	return mesh

func add_moving_parts() -> void:
	# save_compact packs only the static mesh; reopen the scene for the hammer and the smoke.
	assert(ResourceSaver.save(hammer_mesh(), HAMMER_MESH, ResourceSaver.FLAG_COMPRESS) == OK)
	var scene: PackedScene = load(SCENE)
	var root := scene.instantiate()
	var visual := root.get_node("Visual") as Node3D
	var hammer := MeshInstance3D.new()
	hammer.name = "SteamHammer"
	hammer.mesh = load(HAMMER_MESH)
	hammer.position = HAMMER_REST
	hammer.set_script(load("res://visuals/ReciprocatingPart.cs"))
	hammer.set("Travel", 0.32)
	hammer.set("Period", 1.5)
	visual.add_child(hammer)
	hammer.owner = root
	var stack := smoke("Smoke", Vector3(1.0, 2.8, 0.35))
	stack.amount = 12
	stack.scale_amount_min = 0.45
	stack.scale_amount_max = 0.7
	visual.add_child(stack)
	stack.owner = root
	var packed := PackedScene.new()
	assert(packed.pack(root) == OK)
	assert(ResourceSaver.save(packed, SCENE) == OK)
	root.free()
	print("PASS: Forge hammer and smoke added")
