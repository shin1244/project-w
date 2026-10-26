extends "res://tools/build_supply.gd"
## The tunnel (땅굴, server type 11): one end of a paired company tunnel (docs/art-direction.md). A brick-lined
## shaft mouth under a riveted iron arch hood whose crown plate carries the company colour, mine rails running
## into the dark, two gas lamps at the entrance and a vent pipe breathing steam at the back.
## Kept low so it reads as a way down, not a building. Modelled directly on the 2x2 footprint.

const SCENE := "res://buildings/Tunnel.tscn"
const MESH := "res://buildings/meshes/Tunnel.res"
const VENT_TOP := Vector3(0.72, 1.32, 0.7)
const ARCH := 0.62 # Inner radius of the mouth.
const HOOD_FRONT := -0.55
const HOOD_BACK := 0.62

func _initialize() -> void:
	tunnel_materials()
	ground()
	shaft()
	hood()
	rails()
	lamps()
	vent()
	save_compact(SCENE, MESH, "Tunnel", 11, "tunnel", Vector2i(2, 2))
	add_vent_steam()
	quit()

func apply_server_footprint(building: Node3D) -> void:
	assert(building.get("BuildingType") == 11)
	assert(building.get_meta("footprint") == Vector2i(2, 2))

func tunnel_materials() -> void:
	material("concrete", Color("6f6b64"))
	material("concrete_dark", Color("4c4944"))
	material("brick", Color("8a4a3a"))
	material("brick_dark", Color("5e352b"))
	material("void", Color("0b0c0d"), 0.0, 1.0)
	material("soot", Color("2a2622"))
	material("iron", Color("2f363d"), 0.6, 0.35)
	material("iron_light", Color("8f979c"), 0.6, 0.4)
	material("brass", Color("a88349"), 0.7, 0.32)
	material("gold", Color("d9ae45"), 0.8, 0.25)
	material("wood", Color("5a4232"))
	material("roof", Color("3d5f77"), 0.35, 0.55) # Painted iron crown plate, company colour.
	material("banner", Color("376a94"))
	material("lamp", Color("ffd37a"), 0.0, 0.5, 2.0)

func ground() -> void:
	box("concrete_dark", Vector3(0, 0.03, 0), Vector3(2.0, 0.06, 2.0))
	box("concrete", Vector3(0, 0.065, 0), Vector3(1.94, 0.012, 1.94))
	box("soot", Vector3(0, 0.073, -0.78), Vector3(0.9, 0.003, 0.36)) # Boots and tubs blacken the approach.

func arch_point(angle: float, radius: float, z: float) -> Vector3:
	# angle 0 is the right foot of the arch, PI the left; the crown is at PI / 2.
	return Vector3(cos(angle) * radius, 0.07 + sin(angle) * radius, z)

func shaft() -> void:
	# The way down: a dark mouth inside the arch, a brick floor sloping away into it.
	var steps := 12
	for i in range(steps):
		var a := PI * i / steps
		var b := PI * (i + 1) / steps
		# The back wall of the mouth is solid darkness.
		face("void", Vector3(0, 0.07, HOOD_BACK - 0.02), arch_point(b, ARCH, HOOD_BACK - 0.02), arch_point(a, ARCH, HOOD_BACK - 0.02),
			arch_point(a, ARCH, HOOD_BACK - 0.02))
		# Brick lining just inside the hood, fading to dark further in.
		face("brick_dark", arch_point(a, ARCH, HOOD_FRONT + 0.02), arch_point(b, ARCH, HOOD_FRONT + 0.02),
			arch_point(b, ARCH, 0.0), arch_point(a, ARCH, 0.0))
		face("void", arch_point(a, ARCH, 0.0), arch_point(b, ARCH, 0.0),
			arch_point(b, ARCH, HOOD_BACK - 0.02), arch_point(a, ARCH, HOOD_BACK - 0.02))
	box("soot", Vector3(0, 0.075, (HOOD_FRONT + HOOD_BACK) * 0.5), Vector3(ARCH * 2, 0.01, HOOD_BACK - HOOD_FRONT))
	box("void", Vector3(0, 0.08, HOOD_BACK - 0.5), Vector3(ARCH * 2 - 0.04, 0.008, 0.96))

func hood() -> void:
	# Riveted iron hood: an outer half-cylinder shell, a thick front ring with brass rivets and a key plate.
	var outer := ARCH + 0.1
	var steps := 14
	for i in range(steps):
		var a := PI * i / steps
		var b := PI * (i + 1) / steps
		face("iron", arch_point(a, outer, HOOD_FRONT), arch_point(a, outer, HOOD_BACK), arch_point(b, outer, HOOD_BACK), arch_point(b, outer, HOOD_FRONT))
		# Front ring face between the mouth and the shell.
		face("iron", arch_point(a, outer, HOOD_FRONT), arch_point(b, outer, HOOD_FRONT), arch_point(b, ARCH, HOOD_FRONT), arch_point(a, ARCH, HOOD_FRONT))
		face("iron", arch_point(a, ARCH, HOOD_BACK), arch_point(b, ARCH, HOOD_BACK), arch_point(b, outer, HOOD_BACK), arch_point(a, outer, HOOD_BACK))
		sphere("brass", arch_point((a + b) * 0.5, (ARCH + outer) * 0.5, HOOD_FRONT - 0.012), 0.022)
	# Ribs over the shell.
	for z in [HOOD_FRONT + 0.05, 0.03, HOOD_BACK - 0.05]:
		for i in range(steps):
			var a := PI * i / steps
			var b := PI * (i + 1) / steps
			beam("iron_light", arch_point(a, outer + 0.02, z), arch_point(b, outer + 0.02, z), 0.04, 0.05)
	# The company-coloured crown plate along the top, and a gilt keystone over the mouth.
	var crown := 0.07 + outer
	face("roof", Vector3(-0.28, crown - 0.03 + 0.012, HOOD_FRONT + 0.06), Vector3(0.28, crown - 0.03 + 0.012, HOOD_FRONT + 0.06),
		Vector3(0.28, crown - 0.03 + 0.012, HOOD_BACK - 0.06), Vector3(-0.28, crown - 0.03 + 0.012, HOOD_BACK - 0.06))
	box("roof", Vector3(0, crown + 0.0, (HOOD_FRONT + HOOD_BACK) * 0.5), Vector3(0.5, 0.03, HOOD_BACK - HOOD_FRONT - 0.12))
	box("gold", Vector3(0, crown - 0.02, HOOD_FRONT - 0.03), Vector3(0.16, 0.18, 0.06))
	box("banner", Vector3(0, 0.07 + outer + 0.17, HOOD_FRONT + 0.02), Vector3(0.44, 0.2, 0.025))
	box("brass", Vector3(0, 0.07 + outer + 0.17, HOOD_FRONT + 0.035), Vector3(0.48, 0.24, 0.01))
	# Concrete footings under the arch feet.
	for x in [-1.0, 1.0]:
		box("concrete", Vector3(x * (ARCH + 0.05), 0.11, (HOOD_FRONT + HOOD_BACK) * 0.5), Vector3(0.2, 0.1, HOOD_BACK - HOOD_FRONT + 0.06))

func rails() -> void:
	# Mine rails from the front edge into the dark, on sleepers.
	for x in [-0.16, 0.16]:
		box("iron_light", Vector3(x, 0.09, -0.3), Vector3(0.025, 0.02, 1.36))
	for i in range(7):
		box("wood", Vector3(0, 0.078, -0.92 + i * 0.2), Vector3(0.46, 0.016, 0.06))

func lamps() -> void:
	# Two gas lamps on iron posts either side of the mouth.
	for x in [-0.86, 0.86]:
		var base := Vector3(x, 0.06, -0.78)
		cylinder("iron", base + Vector3(0, 0.03, 0), 0.06, 0.07, 0.06, Basis.IDENTITY, 8)
		cylinder("iron", base + Vector3(0, 0.45, 0), 0.022, 0.022, 0.8, Basis.IDENTITY, 8)
		cylinder("brass", base + Vector3(0, 0.88, 0), 0.06, 0.04, 0.05, Basis.IDENTITY, 8)
		cylinder("lamp", base + Vector3(0, 0.96, 0), 0.045, 0.045, 0.1, Basis.IDENTITY, 8)
		cylinder("iron", base + Vector3(0, 1.04, 0), 0.0, 0.07, 0.07, Basis.IDENTITY, 8)

func vent() -> void:
	# A riveted vent pipe at the back corner: the tunnel breathes steam.
	var at := Vector3(VENT_TOP.x, 0, VENT_TOP.z)
	box("concrete", at + Vector3(0, 0.1, 0), Vector3(0.26, 0.08, 0.26))
	cylinder("iron", at + Vector3(0, 0.7, 0), 0.08, 0.09, 1.2, Basis.IDENTITY, 10)
	for y in [0.35, 0.8]:
		cylinder("brass", at + Vector3(0, y, 0), 0.095, 0.095, 0.03, Basis.IDENTITY, 10)
	cylinder("soot", at + Vector3(0, VENT_TOP.y - 0.01, 0), 0.1, 0.085, 0.05, Basis.IDENTITY, 10)
	# A pressure gauge on the pipe facing the entrance.
	cylinder("brass", at + Vector3(-0.1, 0.6, 0), 0.05, 0.05, 0.03, Basis(Vector3.FORWARD, PI * 0.5), 10)

func add_vent_steam() -> void:
	var scene: PackedScene = load(SCENE)
	var root := scene.instantiate()
	var visual := root.get_node("Visual") as Node3D
	var steam := smoke("Steam", VENT_TOP + Vector3(0, 0.04, 0))
	steam.amount = 6
	steam.scale_amount_min = 0.16
	steam.scale_amount_max = 0.24
	var ramp := Gradient.new() # Pale steam rather than coal smoke.
	ramp.set_color(0, Color(0.86, 0.86, 0.84, 0.7))
	ramp.set_color(ramp.get_point_count() - 1, Color(0.92, 0.92, 0.9, 0.0))
	steam.color_ramp = ramp
	visual.add_child(steam)
	steam.owner = root
	var packed := PackedScene.new()
	assert(packed.pack(root) == OK)
	assert(ResourceSaver.save(packed, SCENE) == OK)
	root.free()
	print("PASS: Tunnel vent steam added")
