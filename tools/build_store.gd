extends "res://tools/build_supply.gd"
## The company branch (브랜치, server type 2, key "store"): a forward drop-off where workers return
## wood and oil (docs/art-direction.md). A corrugated-iron warehouse with a loading dock and hoist,
## a small brick branch office with the company board, and one smoking chimney. The front stays
## open as the return lane. Modelled directly on the 4x4 footprint.

const SCENE := "res://buildings/Store.tscn"
const MESH := "res://buildings/meshes/Store.res"

func _initialize() -> void:
	material("brick", Color("7a4638"))
	material("brick_dark", Color("57342a"))
	material("concrete", Color("8a867e"))
	material("concrete_dark", Color("5f5c56"))
	material("iron", Color("2f363d"), 0.5, 0.55)
	material("iron_light", Color("6f7880"), 0.5, 0.5)
	material("wood", Color("5a4232"))
	material("wood_dark", Color("3e2e24"))
	material("soot", Color("1e2226"))
	material("glass", Color("9c7f4a"), 0.0, 0.35, 0.2)
	material("paper", Color("d9cfb4"))
	material("roof", Color("3d5f77"), 0.35, 0.55) # Painted iron, company colour.
	material("roof_light", Color("466b84"), 0.35, 0.55)
	material("banner", Color("376a94"))
	yard()
	warehouse()
	office()
	goods()
	save_compact(SCENE, MESH, "Store", 2, "store", Vector2i(4, 4))
	add_smoke()
	quit()

func yard() -> void:
	box("concrete_dark", Vector3(0, 0.04, 0), Vector3(4.0, 0.08, 4.0))
	box("concrete", Vector3(0, 0.085, 0), Vector3(3.94, 0.012, 3.94))
	# Wheel ruts and oil stains down the open return lane.
	for x in [-0.15, 0.35]:
		box("concrete_dark", Vector3(x, 0.093, -1.3), Vector3(0.08, 0.004, 1.4))
	box("soot", Vector3(0.6, 0.094, -0.9), Vector3(0.5, 0.003, 0.35))
	# Low bollards along the sides keep the front approach open.
	for side in [-1.0, 1.0]:
		for z in [-1.7, -1.1]:
			cylinder("iron", Vector3(side * 1.88, 0.22, z), 0.05, 0.06, 0.26, Basis.IDENTITY, 8)

func warehouse() -> void:
	var x0 := -1.92
	var x1 := 0.3
	var z0 := -0.55
	var z1 := 1.95
	var eave := 1.7
	var ridge := 2.45
	var cx := (x0 + x1) * 0.5
	var cz := (z0 + z1) * 0.5
	var w := x1 - x0
	var d := z1 - z0
	# Brick plinth wall, corrugated iron above.
	box("brick", Vector3(cx, 0.36, cz), Vector3(w, 0.56, d))
	box("iron_light", Vector3(cx, (0.64 + eave) * 0.5, cz), Vector3(w - 0.02, eave - 0.64, d - 0.02))
	var ribs := 14
	for i in range(ribs + 1):
		var x := x0 + w * i / ribs
		box("iron", Vector3(x, (0.64 + eave) * 0.5, z0 - 0.005), Vector3(0.025, eave - 0.64, 0.012))
	for i in range(18):
		var z := z0 + d * i / 17.0
		box("iron", Vector3(x1 + 0.005, (0.64 + eave) * 0.5, z), Vector3(0.012, eave - 0.64, 0.025))
	box("soot", Vector3(cx, eave - 0.05, cz), Vector3(w + 0.01, 0.1, d + 0.01))
	# Gabled roof along the depth, painted company colour, with seams.
	var half := w * 0.5 + 0.07
	for side in [-1.0, 1.0]:
		var e := Vector3(cx + side * half, eave, 0)
		var r := Vector3(cx, ridge, 0)
		face("roof", Vector3(e.x, e.y, z0 - 0.1), Vector3(r.x, r.y, z0 - 0.1), Vector3(r.x, r.y, z1 + 0.0), Vector3(e.x, e.y, z1 + 0.0))
		for i in range(1, 6):
			var t := i / 6.0
			var p := e.lerp(r, t)
			box("roof_light", Vector3(p.x, p.y + 0.012, cz - 0.05), Vector3(0.03, 0.02, d + 0.1), Basis(Vector3.BACK, -side * atan2(ridge - eave, half)))
	box("iron", Vector3(cx, ridge + 0.02, cz - 0.05), Vector3(0.08, 0.05, d + 0.12))
	# Front gable: iron cladding, a round company board in the peak.
	for z in [z0 - 0.005, z1 + 0.005]:
		face("iron_light", Vector3(x0, eave, z), Vector3(cx, ridge, z), Vector3(x1, eave, z), Vector3(x0, eave, z))
	cylinder("wood_dark", Vector3(cx, 2.02, z0 - 0.03), 0.2, 0.2, 0.03, Basis(Vector3.RIGHT, PI * 0.5), 16)
	cylinder("banner", Vector3(cx, 2.02, z0 - 0.05), 0.16, 0.16, 0.012, Basis(Vector3.RIGHT, PI * 0.5), 16)
	box("paper", Vector3(cx, 2.02, z0 - 0.058), Vector3(0.18, 0.05, 0.004))
	# Big sliding door on a top rail, half open into the dark.
	box("soot", Vector3(cx + 0.05, 0.62, z0 - 0.01), Vector3(1.0, 1.06, 0.02))
	box("iron", Vector3(cx - 0.42, 0.62, z0 - 0.04), Vector3(0.55, 1.08, 0.04))
	for y in [0.3, 0.62, 0.94]:
		box("iron_light", Vector3(cx - 0.42, y, z0 - 0.065), Vector3(0.55, 0.03, 0.01))
	box("iron", Vector3(cx, 1.2, z0 - 0.05), Vector3(1.5, 0.05, 0.05))
	# Loading dock with a ramp.
	box("concrete", Vector3(cx, 0.2, z0 - 0.3), Vector3(1.7, 0.24, 0.5))
	box("concrete_dark", Vector3(cx, 0.32, z0 - 0.54), Vector3(1.72, 0.02, 0.03))
	face("concrete", Vector3(cx + 0.85, 0.32, z0 - 0.05), Vector3(cx + 0.85, 0.32, z0 - 0.55),
		Vector3(cx + 1.25, 0.09, z0 - 0.55), Vector3(cx + 1.25, 0.09, z0 - 0.05))
	# Hoist beam from the gable peak with a pulley, chain and a slung crate.
	box("iron", Vector3(cx, 2.3, z0 - 0.35), Vector3(0.08, 0.08, 0.75))
	beam("iron", Vector3(cx, 2.05, z0), Vector3(cx, 2.27, z0 - 0.6), 0.04, 0.04)
	cylinder("iron_light", Vector3(cx, 2.22, z0 - 0.68), 0.06, 0.06, 0.04, Basis(Vector3.FORWARD, PI * 0.5), 10)
	box("iron", Vector3(cx, 1.78, z0 - 0.68), Vector3(0.015, 0.84, 0.015))
	box("wood", Vector3(cx, 1.25, z0 - 0.68), Vector3(0.26, 0.22, 0.26))

func office() -> void:
	var x0 := 0.55
	var x1 := 1.95
	var z0 := 0.15
	var z1 := 1.9
	var top := 1.95
	var cx := (x0 + x1) * 0.5
	var cz := (z0 + z1) * 0.5
	box("brick", Vector3(cx, (0.09 + top) * 0.5, cz), Vector3(x1 - x0, top - 0.09, z1 - z0))
	box("concrete", Vector3(cx, 1.02, cz), Vector3(x1 - x0 + 0.05, 0.06, z1 - z0 + 0.05))
	for z in [z0, z1]:
		box("brick_dark", Vector3(cx, top + 0.07, z), Vector3(x1 - x0 + 0.02, 0.14, 0.06))
	for x in [x0, x1]:
		box("brick_dark", Vector3(x, top + 0.07, cz), Vector3(0.06, 0.14, z1 - z0 + 0.02))
	box("soot", Vector3(cx, top + 0.02, cz), Vector3(x1 - x0 - 0.06, 0.02, z1 - z0 - 0.06))
	box("soot", Vector3(cx, top - 0.06, z0 - 0.012), Vector3(x1 - x0, 0.12, 0.006))
	# Door with the company board over it, windows either side and above.
	box("wood_dark", Vector3(cx - 0.2, 0.47, z0 - 0.02), Vector3(0.32, 0.76, 0.03))
	box("glass", Vector3(cx - 0.2, 0.68, z0 - 0.04), Vector3(0.16, 0.16, 0.006))
	box("wood_dark", Vector3(cx, 0.94, z0 - 0.03), Vector3(1.24, 0.15, 0.03))
	box("banner", Vector3(cx, 0.94, z0 - 0.05), Vector3(1.18, 0.11, 0.01))
	for i in range(7):
		box("paper", Vector3(x0 + 0.2 + i * 0.16, 0.94, z0 - 0.057), Vector3(0.08, 0.06, 0.004))
	box("wood_dark", Vector3(cx + 0.35, 0.55, z0 - 0.02), Vector3(0.4, 0.5, 0.025))
	box("glass", Vector3(cx + 0.35, 0.55, z0 - 0.035), Vector3(0.34, 0.44, 0.006))
	for x in [cx - 0.38, cx, cx + 0.38]:
		box("wood_dark", Vector3(x, 1.5, z0 - 0.015), Vector3(0.26, 0.5, 0.02))
		box("glass", Vector3(x, 1.5, z0 - 0.03), Vector3(0.2, 0.42, 0.006))
	for z in [0.6, 1.4]:
		box("glass", Vector3(x0 - 0.01, 1.5, z), Vector3(0.006, 0.42, 0.2))
	# The one chimney, soot climbing its top.
	var chimney := Vector3(1.6, 0, 1.5)
	box("brick_dark", Vector3(chimney.x, 2.55, chimney.z), Vector3(0.3, 1.3, 0.3))
	for y in [2.4, 2.9]:
		box("iron", Vector3(chimney.x, y, chimney.z), Vector3(0.33, 0.05, 0.33))
	box("soot", Vector3(chimney.x, 3.13, chimney.z), Vector3(0.31, 0.16, 0.31))
	box("iron", Vector3(chimney.x, 3.23, chimney.z), Vector3(0.36, 0.04, 0.36))

func goods() -> void:
	# Returned timber, oil drums and crates around the dock and lane.
	for row in range(2):
		for column in range(3 - row):
			var x := -1.72 + column * 0.2 + row * 0.1
			cylinder("wood", Vector3(x, 0.19 + row * 0.17, -1.45), 0.1, 0.1, 0.8, Basis(Vector3.RIGHT, PI * 0.5), 10)
	for p in [Vector3(1.45, 0, -0.35), Vector3(1.7, 0, -0.55), Vector3(1.48, 0, -0.75)]:
		cylinder("iron", p + Vector3(0, 0.29, 0), 0.13, 0.13, 0.38, Basis.IDENTITY, 12)
		cylinder("soot", p + Vector3(0, 0.485, 0), 0.11, 0.11, 0.01, Basis.IDENTITY, 12)
	for p in [Vector3(-1.3, 0, -0.92), Vector3(-1.62, 0, -0.95), Vector3(-1.45, 0.28, -0.93)]:
		box("wood", p + Vector3(0, 0.23, 0), Vector3(0.27, 0.27, 0.27))
		box("wood_dark", p + Vector3(0, 0.23, -0.14), Vector3(0.28, 0.04, 0.005))
	# A hand cart and a gas lamp at the lane corner.
	box("wood", Vector3(1.1, 0.3, -1.45), Vector3(0.4, 0.06, 0.28))
	cylinder("iron", Vector3(1.1, 0.2, -1.6), 0.11, 0.11, 0.03, Basis(Vector3.FORWARD, PI * 0.5), 12)
	beam("wood_dark", Vector3(0.92, 0.3, -1.33), Vector3(0.75, 0.45, -1.1), 0.025, 0.025)
	cylinder("iron", Vector3(1.82, 0.55, -1.82), 0.02, 0.03, 0.92)
	box("glass", Vector3(1.82, 1.05, -1.82), Vector3(0.08, 0.11, 0.08))
	cylinder("iron", Vector3(1.82, 1.13, -1.82), 0.0, 0.07, 0.06, Basis.IDENTITY, 4)

func add_smoke() -> void:
	# save_compact packs only the mesh; reopen the scene and give the chimney its smoke.
	var scene: PackedScene = load(SCENE)
	var root := scene.instantiate()
	var visual := root.get_node("Visual") as Node3D
	var stack := smoke("Smoke", Vector3(1.6, 3.3, 1.5) / visual.scale)
	stack.amount = 10
	stack.scale_amount_min = 0.4
	stack.scale_amount_max = 0.6
	visual.add_child(stack)
	stack.owner = root
	var packed := PackedScene.new()
	assert(packed.pack(root) == OK)
	assert(ResourceSaver.save(packed, SCENE) == OK)
	root.free()
	print("PASS: Store smoke added")
