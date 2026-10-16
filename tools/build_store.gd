extends "res://tools/build_supply.gd"
## The company branch (브랜치, server type 2, key "store"): a forward drop-off where workers return
## wood and oil (docs/art-direction.md). A logistics depot, kept bright and unlike the black arsenal:
## a galvanised barrel-vault warehouse with a painted sliding door, a tall lattice jib crane over a
## rail spur and loaded flatcar, a small brick branch office with the company board and one smoking
## chimney. The front right stays open as the return lane. Modelled directly on the 4x4 footprint.

const SCENE := "res://buildings/Store.tscn"
const MESH := "res://buildings/meshes/Store.res"

func _initialize() -> void:
	material("brick", Color("7a4638"))
	material("brick_dark", Color("57342a"))
	material("concrete", Color("8a867e"))
	material("concrete_dark", Color("5f5c56"))
	material("iron", Color("2f363d"), 0.5, 0.55)
	material("iron_light", Color("a3aaae"), 0.55, 0.45) # Galvanised sheet.
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
	crane()
	rail()
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
	# A bright galvanised warehouse under a barrel-vault roof in the company colour: a logistics
	# shed, not a works. Its round gable faces the street with a big painted sliding door.
	var x0 := -1.9
	var x1 := 0.28
	var z0 := -0.55
	var z1 := 1.95
	var eave := 1.25
	var rise := 0.95
	var cx := (x0 + x1) * 0.5
	var cz := (z0 + z1) * 0.5
	var w := x1 - x0
	var d := z1 - z0
	var hw := w * 0.5 + 0.05
	box("brick", Vector3(cx, 0.3, cz), Vector3(w, 0.44, d))
	box("iron_light", Vector3(cx, (0.52 + eave) * 0.5, cz), Vector3(w - 0.02, eave - 0.52, d - 0.02))
	for i in range(19):
		var z := z0 + d * i / 18.0
		for x in [x0 - 0.005, x1 + 0.005]:
			box("iron", Vector3(x, (0.52 + eave) * 0.5, z), Vector3(0.01, eave - 0.52, 0.02))
	# Barrel vault: painted sheets between arc points, iron ribs at intervals.
	var steps := 12
	var arc: Array = []
	for i in range(steps + 1):
		var t := PI * i / steps
		arc.append(Vector2(cx + hw * cos(t), eave + rise * sin(t)))
	for i in range(steps):
		var a: Vector2 = arc[i]
		var b: Vector2 = arc[i + 1]
		face("roof" if i % 2 == 0 else "roof_light", Vector3(a.x, a.y, z0 - 0.06), Vector3(b.x, b.y, z0 - 0.06), Vector3(b.x, b.y, z1), Vector3(a.x, a.y, z1))
	for k in range(6):
		var z := z0 - 0.04 + (d + 0.02) * k / 5.0
		for i in range(steps):
			var a: Vector2 = arc[i]
			var b: Vector2 = arc[i + 1]
			beam("iron", Vector3(a.x, a.y + 0.012, z), Vector3(b.x, b.y + 0.012, z), 0.035, 0.035)
	# Round gable ends in galvanised sheet.
	for z in [z0 - 0.005, z1 + 0.005]:
		for i in range(steps):
			var a: Vector2 = arc[i]
			var b: Vector2 = arc[i + 1]
			face("iron_light", Vector3(a.x, a.y, z), Vector3(b.x, b.y, z), Vector3(b.x, eave, z), Vector3(a.x, eave, z))
		face("iron_light", Vector3(x0, 0.52, z), Vector3(x1, 0.52, z), Vector3(x1, eave, z), Vector3(x0, eave, z))
	# Company roundel in the arch and a big sliding door painted in the company colour.
	cylinder("wood_dark", Vector3(cx, eave + 0.5, z0 - 0.03), 0.2, 0.2, 0.03, Basis(Vector3.RIGHT, PI * 0.5), 16)
	cylinder("banner", Vector3(cx, eave + 0.5, z0 - 0.05), 0.16, 0.16, 0.012, Basis(Vector3.RIGHT, PI * 0.5), 16)
	box("paper", Vector3(cx, eave + 0.5, z0 - 0.058), Vector3(0.18, 0.05, 0.004))
	box("soot", Vector3(cx + 0.25, 0.6, z0 - 0.01), Vector3(0.62, 0.98, 0.02))
	box("banner", Vector3(cx - 0.32, 0.6, z0 - 0.04), Vector3(0.62, 1.0, 0.03))
	for y in [0.25, 0.6, 0.95]:
		box("iron", Vector3(cx - 0.32, y, z0 - 0.06), Vector3(0.62, 0.03, 0.01))
	beam("iron", Vector3(cx - 0.6, 0.15, z0 - 0.06), Vector3(cx - 0.04, 1.05, z0 - 0.06), 0.03, 0.01)
	box("iron", Vector3(cx, 1.13, z0 - 0.05), Vector3(1.5, 0.05, 0.05))
	# Loading dock along the gable, at the rail.
	box("concrete", Vector3(cx, 0.2, z0 - 0.3), Vector3(1.9, 0.24, 0.5))
	box("concrete_dark", Vector3(cx, 0.32, z0 - 0.54), Vector3(1.92, 0.02, 0.03))

func crane() -> void:
	# A tall lattice jib crane at the front-left corner, its boom over the rail wagon: the
	# branch's silhouette from any distance.
	var base := Vector3(-1.72, 0, -1.68)
	var top := 3.35
	for dx in [-0.15, 0.15]:
		for dz in [-0.15, 0.15]:
			beam("iron", base + Vector3(dx, 0.08, dz), base + Vector3(dx * 0.6, top, dz * 0.6), 0.05, 0.05)
	for i in range(6):
		var y0 := 0.15 + i * 0.52
		var y1 := y0 + 0.52
		var s0 := lerpf(0.15, 0.09, y0 / top)
		var s1 := lerpf(0.15, 0.09, minf(y1, top) / top)
		for side in [-1.0, 1.0]:
			beam("iron", base + Vector3(-s0, y0, side * s0), base + Vector3(s1, minf(y1, top), side * s1), 0.02, 0.02)
			beam("iron", base + Vector3(side * s0, y0, -s0), base + Vector3(side * s1, minf(y1, top), s1), 0.02, 0.02)
	box("concrete", base + Vector3(0, 0.06, 0), Vector3(0.42, 0.12, 0.42))
	# Slewing house, boom and counterweight.
	box("iron_light", base + Vector3(0, top + 0.1, 0), Vector3(0.28, 0.2, 0.28))
	box("banner", base + Vector3(0, top + 0.1, -0.142), Vector3(0.2, 0.12, 0.006))
	var tip := Vector3(-0.55, 2.85, -1.25)
	beam("iron", base + Vector3(-0.05, top + 0.12, 0.05), tip, 0.05, 0.05)
	beam("iron", base + Vector3(0.05, top + 0.12, -0.05), tip, 0.05, 0.05)
	beam("iron", base + Vector3(0, top + 0.4, 0), tip, 0.02, 0.02)
	box("iron", base + Vector3(0, top + 0.3, 0), Vector3(0.05, 0.4, 0.05))
	box("concrete_dark", base + Vector3(-0.17, top + 0.12, -0.17), Vector3(0.14, 0.16, 0.14))
	# Hoist cable down to a slung crate over the wagon.
	box("iron", tip + Vector3(0, -0.75, 0), Vector3(0.012, 1.5, 0.012))
	box("wood", tip + Vector3(0, -1.6, 0), Vector3(0.24, 0.2, 0.24))

func rail() -> void:
	# A rail spur along the dock with a loaded flatcar.
	for z in [-1.37, -1.15]:
		box("iron_light", Vector3(-0.95, 0.105, z), Vector3(2.1, 0.03, 0.025))
	for i in range(12):
		box("wood_dark", Vector3(-1.95 + i * 0.18, 0.095, -1.26), Vector3(0.07, 0.025, 0.36))
	var car := Vector3(-0.75, 0, -1.26)
	for dx in [-0.35, 0.35]:
		for dz in [-0.11, 0.11]:
			cylinder("iron", car + Vector3(dx, 0.17, dz), 0.075, 0.075, 0.03, Basis(Vector3.RIGHT, PI * 0.5), 10)
		box("iron", car + Vector3(dx, 0.2, 0), Vector3(0.2, 0.06, 0.26))
	box("wood", car + Vector3(0, 0.28, 0), Vector3(1.0, 0.07, 0.36))
	box("banner", car + Vector3(0, 0.28, -0.182), Vector3(0.9, 0.04, 0.006))
	for row in range(2):
		for column in range(3 - row):
			cylinder("wood", car + Vector3(-0.42 + 0.0, 0.0, 0) + Vector3(0.25, 0.37 + row * 0.15, -0.1 + column * 0.1 + row * 0.05), 0.07, 0.07, 0.5, Basis(Vector3.FORWARD, PI * 0.5), 10)
	box("wood", car + Vector3(0.28, 0.42, 0), Vector3(0.26, 0.22, 0.26))
	box("wood", car + Vector3(0.3, 0.6, 0.02), Vector3(0.2, 0.16, 0.2))

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
	# Oil drums by the office and crates waiting on the dock.
	for p in [Vector3(1.45, 0, -0.35), Vector3(1.7, 0, -0.55), Vector3(1.48, 0, -0.75)]:
		cylinder("iron", p + Vector3(0, 0.29, 0), 0.13, 0.13, 0.38, Basis.IDENTITY, 12)
		cylinder("soot", p + Vector3(0, 0.485, 0), 0.11, 0.11, 0.01, Basis.IDENTITY, 12)
	for p in [Vector3(-1.3, 0.22, -0.82), Vector3(-1.58, 0.22, -0.84), Vector3(-1.44, 0.5, -0.83)]:
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
