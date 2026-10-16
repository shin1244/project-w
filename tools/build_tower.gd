extends "res://tools/build_fortress.gd"
## The watchtower (감시탑, server type 6): a riveted steel lattice tower on concrete footings with an
## iron-grated lookout, a searchlight that sweeps slowly round (SpinningPart) and the fortress's steam
## cannon at 0.55 scale on a pedestal. Built on the server's 2x2 footprint at full scale.

const SEARCHLIGHT_MESH := "res://buildings/meshes/TowerSearchlight.res"
const DECK := 1.9
const PIVOT := DECK + 0.46

func _initialize() -> void:
	defence_materials()
	build_lattice()
	var base_mesh := finish_mesh("res://buildings/meshes/Tower.res")
	reset_surfaces()
	build_steam_gun()
	var weapon_mesh := finish_mesh("res://buildings/meshes/TowerBallista.res", .55)
	save_tower(base_mesh, weapon_mesh, "Tower", 6, 2, PIVOT, .55)
	add_searchlight()
	quit()

func build_lattice() -> void:
	chamfered_block("concrete_dark", 1.0, 0.16, 0.0, 0.08)
	var foot := 0.7
	var top := 0.55
	var deck := DECK
	for x in [-1.0, 1.0]:
		for z in [-1.0, 1.0]:
			box("concrete", Vector3(x * foot, 0.13, z * foot), Vector3(0.26, 0.12, 0.26))
			beam("iron", Vector3(x * foot, 0.18, z * foot), Vector3(x * top, deck, z * top), 0.085, 0.085)
			for y in [0.5, 1.0, 1.45]:
				var t: float = (y - 0.18) / (deck - 0.18)
				var r := lerpf(foot, top, t)
				sphere("iron_light", Vector3(x * r, y, z * r), 0.028)
	# X-bracing on every face, two bays, plus horizontal struts.
	for face_axis in [0, 1]:
		for side in [-1.0, 1.0]:
			var levels := [0.18, 0.9, deck]
			for k in range(2):
				var y0: float = levels[k]
				var y1: float = levels[k + 1]
				var r0 := lerpf(foot, top, (y0 - 0.18) / (deck - 0.18))
				var r1 := lerpf(foot, top, (y1 - 0.18) / (deck - 0.18))
				var a0 := Vector3(-r0, y0, side * r0) if face_axis == 0 else Vector3(side * r0, y0, -r0)
				var b1 := Vector3(r1, y1, side * r1) if face_axis == 0 else Vector3(side * r1, y1, r1)
				var a1 := Vector3(-r1, y1, side * r1) if face_axis == 0 else Vector3(side * r1, y1, -r1)
				var b0 := Vector3(r0, y0, side * r0) if face_axis == 0 else Vector3(side * r0, y0, r0)
				beam("iron", a0, b1, 0.035, 0.035)
				beam("iron", b0, a1, 0.035, 0.035)
				beam("iron", a1, b1, 0.045, 0.045)
	# Iron-grated lookout deck with railings; the toe boards carry the company colour.
	box("iron", Vector3(0, deck + 0.04, 0), Vector3(1.5, 0.08, 1.5))
	for i in range(8):
		box("iron_light", Vector3(-0.66 + i * 0.19, deck + 0.085, 0), Vector3(0.05, 0.012, 1.44))
	for x in [-0.73, 0.73]:
		for z in [-0.73, 0.73]:
			box("iron", Vector3(x, deck + 0.3, z), Vector3(0.05, 0.46, 0.05))
	for side in [-1.0, 1.0]:
		box("roof", Vector3(side * 0.75, deck + 0.13, 0), Vector3(0.03, 0.1, 1.5))
		box("roof", Vector3(0, deck + 0.13, side * 0.75), Vector3(1.5, 0.1, 0.03))
		box("iron", Vector3(side * 0.73, deck + 0.52, 0), Vector3(0.035, 0.035, 1.46))
		box("iron", Vector3(0, deck + 0.52, side * 0.73), Vector3(1.46, 0.035, 0.035))
		box("iron", Vector3(side * 0.73, deck + 0.33, 0), Vector3(0.02, 0.02, 1.46))
		box("iron", Vector3(0, deck + 0.33, side * 0.73), Vector3(1.46, 0.02, 0.02))
	# Ladder up the front face to a hatch.
	for x in [-0.14, 0.14]:
		beam("iron", Vector3(x, 0.1, -0.84), Vector3(x, deck, -0.58), 0.035, 0.035)
	for i in range(8):
		var t := (i + 0.5) / 8.0
		box("iron_light", Vector3(0, lerpf(0.15, deck - 0.05, t), lerpf(-0.84, -0.58, t)), Vector3(0.28, 0.025, 0.03))
	# Cannon pedestal from the deck up to the pivot.
	cylinder("iron", Vector3(0, deck + 0.24, 0), 0.16, 0.22, 0.32, Basis.IDENTITY, 14)
	cylinder("brass", Vector3(0, deck + 0.43, 0), 0.2, 0.2, 0.05, Basis.IDENTITY, 14)
	# Company banner off the side rail, and the searchlight's post at the back corner.
	box("gold", Vector3(0.76, deck - 0.02, 0.1), Vector3(0.03, 0.03, 0.44))
	face("banner", Vector3(0.765, deck - 0.03, -0.12), Vector3(0.765, deck - 0.03, 0.32), Vector3(0.765, deck - 0.55, 0.32), Vector3(0.765, deck - 0.55, -0.12))
	face("banner", Vector3(0.765, deck - 0.55, -0.12), Vector3(0.765, deck - 0.55, 0.32), Vector3(0.765, deck - 0.68, 0.1), Vector3(0.765, deck - 0.55, -0.12))
	box("iron", Vector3(-0.62, deck + 0.3, 0.62), Vector3(0.06, 0.44, 0.06))
	cylinder("brass", Vector3(-0.62, deck + 0.53, 0.62), 0.06, 0.06, 0.03, Basis.IDENTITY, 10)

func searchlight_mesh() -> ArrayMesh:
	var saved := surfaces
	surfaces = {}
	for key in ["iron", "brass", "lamp"]:
		var st := SurfaceTool.new()
		st.begin(Mesh.PRIMITIVE_TRIANGLES)
		st.set_material(materials[key])
		surfaces[key] = st
	# A yoke and a drum lamp tilted down, its lens facing local -Z.
	box("iron", Vector3(0, 0.03, 0), Vector3(0.05, 0.06, 0.05))
	for x in [-0.09, 0.09]:
		box("iron", Vector3(x, 0.1, 0), Vector3(0.02, 0.14, 0.03))
	var tilt := Basis(Vector3.RIGHT, PI * 0.5 + 0.35)
	cylinder("iron", Vector3(0, 0.15, 0), 0.075, 0.085, 0.18, tilt, 14)
	cylinder("brass", Vector3(0, 0.15, 0) + tilt * Vector3(0, 0.09, 0), 0.088, 0.088, 0.02, tilt, 14)
	cylinder("lamp", Vector3(0, 0.15, 0) + tilt * Vector3(0, 0.1, 0), 0.07, 0.07, 0.01, tilt, 14)
	var mesh := ArrayMesh.new()
	for key in surfaces:
		surfaces[key].commit(mesh)
	surfaces = saved
	return mesh

func add_searchlight() -> void:
	assert(ResourceSaver.save(searchlight_mesh(), SEARCHLIGHT_MESH, ResourceSaver.FLAG_COMPRESS) == OK)
	var scene: PackedScene = load("res://buildings/Tower.tscn")
	var root := scene.instantiate()
	var visual := root.get_node("Visual") as Node3D
	var light := MeshInstance3D.new()
	light.name = "Searchlight"
	light.mesh = load(SEARCHLIGHT_MESH)
	light.position = Vector3(-0.62, DECK + 0.54, 0.62)
	light.set_script(load("res://visuals/SpinningPart.cs"))
	light.set("Speed", 0.6)
	visual.add_child(light)
	light.owner = root
	var packed := PackedScene.new()
	assert(packed.pack(root) == OK)
	assert(ResourceSaver.save(packed, "res://buildings/Tower.tscn") == OK)
	root.free()
	print("PASS: Tower searchlight added")
