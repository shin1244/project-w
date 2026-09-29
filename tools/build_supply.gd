extends "res://tools/build_town_hall.gd"
## Compact 2x2 granary. Reusable visual only; production/population remain server concerns.

const SUPPLY_SCENE := "res://buildings/Supply.tscn"
const SUPPLY_MESH := "res://buildings/meshes/Supply.res"

func _initialize() -> void:
	material("stone", Color("7e8779"))
	material("stone_light", Color("a6ad96"))
	material("stone_dark", Color("526054"))
	material("wood", Color("583b29"))
	material("wood_light", Color("a47947"))
	material("wood_warm", Color("896039"))
	material("dark", Color("28332d"), 0.25)
	material("roof", Color("274e66"))
	material("roof_mid", Color("315971"))
	material("roof_light", Color("3b667d"))
	material("gold", Color("d2aa59"), 0.25)
	material("canvas", Color("cebc85"))
	material("banner", Color("376a94"))
	build_supply()
	save_supply()
	quit()

func foundation() -> void:
	var corners := [Vector2(-.87, -1), Vector2(.87, -1), Vector2(1, -.87), Vector2(1, .87),
		Vector2(.87, 1), Vector2(-.87, 1), Vector2(-1, .87), Vector2(-1, -.87)]
	for i in range(corners.size()):
		var a: Vector2 = corners[i]
		var b: Vector2 = corners[(i + 1) % corners.size()]
		triangle("stone", Vector3(0, .11, 0), Vector3(b.x, .11, b.y), Vector3(a.x, .11, a.y))
		quad("stone_dark", Vector3(a.x, 0, a.y), Vector3(a.x, .11, a.y), Vector3(b.x, .11, b.y), Vector3(b.x, 0, b.y))
	# Raised dry storage base and individually coursed masonry.
	box("stone_dark", Vector3(0, .24, .18), Vector3(1.51, .26, 1.36))
	for side in [-1.0, 1.0]:
		for i in range(5):
			box("stone_light" if i % 3 == 0 else "stone", Vector3(side * .755, .245, -.34 + i * .265), Vector3(.035, .235, .245))
		for i in range(6):
			box("stone_light" if i % 3 == 0 else "stone", Vector3(-.635 + i * .254, .245, .18 + side * .681), Vector3(.237, .235, .035))
	box("stone_light", Vector3(0, .385, .18), Vector3(1.59, .08, 1.43))
	box("stone_light", Vector3(0, .15, -.88), Vector3(.68, .08, .23))
	box("stone", Vector3(0, .235, -.73), Vector3(.63, .09, .22))
	box("stone_light", Vector3(0, .33, -.58), Vector3(.59, .10, .20))

func build_supply() -> void:
	foundation()
	# Recessed dark core makes the little plank joints readable from an RTS camera.
	box("wood", Vector3(0, .91, .19), Vector3(1.37, 1.02, 1.26))
	for i in range(10):
		var x := -.615 + i * .137
		for z in [-.455, .835]:
			box("wood_light" if i % 3 == 0 else "wood_warm", Vector3(x, .91, z), Vector3(.127, .95, .035))
	for side in [-1.0, 1.0]:
		for i in range(9):
			box("wood_light" if i % 3 == 0 else "wood_warm", Vector3(side * .701, .91, -.375 + i * .14), Vector3(.035, .95, .13))
		for z in [-.437, .82]:
			box("wood", Vector3(side * .69, .92, z), Vector3(.115, 1.07, .115))
		box("wood", Vector3(side * .706, .47, .19), Vector3(.095, .10, 1.34))
		box("wood", Vector3(side * .706, 1.38, .19), Vector3(.095, .115, 1.37))
		# A long cross brace and a shuttered ventilation slot on each side.
		beam("wood", Vector3(side * .733, .51, -.32), Vector3(side * .733, 1.30, .40), .055, .075)
		box("wood", Vector3(side * .746, 1.12, .54), Vector3(.065, .40, .34))
		box("dark", Vector3(side * .785, 1.13, .54), Vector3(.025, .28, .25))
		for y in [1.035, 1.12, 1.205]:
			box("wood_light", Vector3(side * .802, y, .54), Vector3(.03, .037, .265))
		box("wood_light", Vector3(side * .80, .895, .54), Vector3(.09, .055, .41))
	# Two timber gables: the front is deliberately visible above the storage doors.
	for z in [-.465, .835]:
		var a := Vector3(-.69, 1.40, z)
		var b := Vector3(.69, 1.40, z)
		var c := Vector3(0, 2.015, z)
		if z < 0: triangle("wood_warm", a, c, b)
		else: triangle("wood_warm", a, b, c)
		box("wood", Vector3(0, 1.405, z), Vector3(1.46, .105, .105))
		beam("wood", a, c, .075, .095)
		beam("wood", c, b, .075, .095)
		box("wood", Vector3(0, 1.66, z), Vector3(.065, .48, .08))
	roof(.875, -.565, .905, 1.455, 2.10, 5, 8)
	storage_doors()
	wheat_crest()
	roof_vent()
	crate(Vector3(-.72, .115, -.76), .30)
	sack(Vector3(-.54, .12, -.86), .11, .30)
	sack(Vector3(-.78, .415, -.76), .10, .25)
	barrel(Vector3(.70, .11, -.72), .18, .43)
	sack(Vector3(.44, .12, -.87), .10, .25)
	crate(Vector3(.84, .11, -.25), .22)
	team_pennant()

func storage_doors() -> void:
	box("dark", Vector3(0, .845, -.493), Vector3(.68, .86, .07))
	for i in range(6):
		box("wood_light" if i % 2 == 0 else "wood_warm", Vector3(-.255 + i * .102, .845, -.537), Vector3(.094, .83, .04))
	for x in [-.35, .35]:
		box("wood", Vector3(x, .84, -.542), Vector3(.09, .88, .105))
	box("wood", Vector3(0, 1.30, -.54), Vector3(.80, .10, .12))
	box("wood", Vector3(0, .845, -.566), Vector3(.022, .82, .03))
	for side in [-1.0, 1.0]:
		for y in [.59, 1.10]:
			box("dark", Vector3(side * .17, y, -.568), Vector3(.285, .04, .025))
		beam("wood", Vector3(side * .045, .63, -.59), Vector3(side * .285, 1.04, -.59), .047, .027)
		sphere("gold", Vector3(side * .055, .855, -.596), .022)
	# Short loading canopy brackets below the main eave.
	for x in [-.49, .49]:
		beam("wood", Vector3(x, 1.13, -.48), Vector3(x, 1.37, -.66), .055, .055)
	box("wood_light", Vector3(0, 1.395, -.615), Vector3(1.09, .065, .24))

func wheat_crest() -> void:
	# Gold wheat on a dark wooden medallion signals stored food / population capacity.
	cylinder("wood", Vector3(0, 1.685, -.512), .19, .19, .065, Basis(Vector3.RIGHT, PI * .5))
	cylinder("gold", Vector3(0, 1.685, -.550), .167, .167, .016, Basis(Vector3.RIGHT, PI * .5))
	cylinder("dark", Vector3(0, 1.685, -.562), .142, .142, .014, Basis(Vector3.RIGHT, PI * .5))
	for side in [-1.0, 0.0, 1.0]:
		var bottom := Vector3(0, 1.575, -.58)
		var top := Vector3(side * .072, 1.79 if side == 0 else 1.765, -.58)
		beam("gold", bottom, top, .013, .014)
		for i in range(3):
			var t := .53 + i * .17
			var point := bottom.lerp(top, t)
			for direction in [-1.0, 1.0]:
				grain(point, Vector3(direction * .028, .033, 0), .013)
	box("gold", Vector3(0, 1.612, -.59), Vector3(.074, .018, .015))

func grain(base: Vector3, stem: Vector3, width: float) -> void:
	var tip := base + stem
	var midpoint := (base + tip) * .5
	var across := Vector3(stem.y, -stem.x, 0).normalized() * width
	var ridge := midpoint + Vector3(0, 0, -.012)
	triangle("gold", base, midpoint - across, ridge)
	triangle("gold", midpoint - across, tip, ridge)
	triangle("gold", tip, midpoint + across, ridge)
	triangle("gold", midpoint + across, base, ridge)

func roof_vent() -> void:
	# Low grain-drying vent breaks the silhouette without making a second civic tower.
	box("wood", Vector3(0, 2.07, .46), Vector3(.35, .15, .41))
	box("dark", Vector3(0, 2.18, .46), Vector3(.26, .12, .31))
	for x in [-.145, .145]:
		for z in [.295, .625]:
			box("wood_light", Vector3(x, 2.18, z), Vector3(.035, .17, .035))
	for z in [.286, .634]:
		for y in [2.15, 2.215]:
			box("wood_light", Vector3(0, y, z), Vector3(.27, .025, .02))
	var tip := Vector3(0, 2.385, .46)
	var points := [Vector3(-.23, 2.26, .20), Vector3(.23, 2.26, .20), Vector3(.23, 2.26, .72), Vector3(-.23, 2.26, .72)]
	for i in range(4):
		triangle("roof_light" if i % 2 == 0 else "roof_mid", points[(i + 1) % 4], points[i], tip)
		beam("gold", points[i], tip, .018, .018)

func crate(bottom: Vector3, size: float) -> void:
	box("wood_warm", bottom + Vector3(0, size * .5, 0), Vector3.ONE * size)
	for side in [-1.0, 1.0]:
		for t in [-.40, .40]:
			box("wood_light", bottom + Vector3(t * size, size * .5, side * (size * .5 + .006)), Vector3(size * .10, size, .018))
			box("wood_light", bottom + Vector3(side * (size * .5 + .006), size * .5, t * size), Vector3(.018, size, size * .10))
		for y in [.12, .88]:
			box("wood", bottom + Vector3(0, size * y, side * (size * .5 + .015)), Vector3(size, .032, .015))
		beam("wood_light", bottom + Vector3(-size * .36, size * .18, side * (size * .5 + .024)), bottom + Vector3(size * .36, size * .82, side * (size * .5 + .024)), .023, .016)
	for i in [-1, 0, 1]:
		box("wood_light", bottom + Vector3(i * size * .31, size + .009, 0), Vector3(size * .27, .018, size))

func sack(bottom: Vector3, radius: float, height: float) -> void:
	var mesh := SphereMesh.new()
	mesh.radius = 1.0
	mesh.height = 2.0
	mesh.radial_segments = 12
	mesh.rings = 6
	append_mesh("canvas", mesh, Transform3D(Basis.from_scale(Vector3(radius, height * .41, radius * .88)), bottom + Vector3(0, height * .41, 0)))
	cylinder("canvas", bottom + Vector3(0, height * .84, 0), radius * .35, radius * .24, height * .16)
	cylinder("wood", bottom + Vector3(0, height * .795, 0), radius * .275, radius * .275, .018)
	# Stitched seam and a short tie make this read as a sack instead of a stone.
	beam("wood_light", bottom + Vector3(-radius * .10, height * .19, -radius * .84), bottom + Vector3(-radius * .10, height * .67, -radius * .71), .009, .009)
	beam("wood", bottom + Vector3(0, height * .80, -.015), bottom + Vector3(.07, height * .71, -.01), .009, .01)

func barrel(bottom: Vector3, radius: float, height: float) -> void:
	var profile := [Vector2(radius * .84, 0), Vector2(radius, height * .35), Vector2(radius, height * .66), Vector2(radius * .84, height)]
	for segment in range(12):
		var a := TAU * segment / 12.0
		var b := TAU * (segment + 1) / 12.0
		for level in range(3):
			var low: Vector2 = profile[level]
			var high: Vector2 = profile[level + 1]
			quad("wood_light" if segment % 3 == 0 else "wood_warm",
				bottom + Vector3(cos(a) * low.x, low.y, sin(a) * low.x),
				bottom + Vector3(cos(a) * high.x, high.y, sin(a) * high.x),
				bottom + Vector3(cos(b) * high.x, high.y, sin(b) * high.x),
				bottom + Vector3(cos(b) * low.x, low.y, sin(b) * low.x))
	for fraction in [.14, .47, .86]:
		var band_radius := radius * (1.01 if fraction == .47 else .94)
		# Only the thin rim is generated: a full cylinder would hide the barrel lid.
		for segment in range(12):
			var a := TAU * segment / 12.0
			var b := TAU * (segment + 1) / 12.0
			quad("dark", bottom + Vector3(cos(a) * band_radius, height * fraction - .013, sin(a) * band_radius),
				bottom + Vector3(cos(a) * band_radius, height * fraction + .013, sin(a) * band_radius),
				bottom + Vector3(cos(b) * band_radius, height * fraction + .013, sin(b) * band_radius),
				bottom + Vector3(cos(b) * band_radius, height * fraction - .013, sin(b) * band_radius))
	cylinder("wood_light", bottom + Vector3(0, height + .002, 0), radius * .83, radius * .83, .018)
	for offset in [-.05, .05]:
		box("wood", bottom + Vector3(offset, height + .013, 0), Vector3(.008, .008, radius * 1.48))

func team_pennant() -> void:
	# A small wall pennant is visible from the front, and changes with Building.SideId.
	box("dark", Vector3(.53, 1.215, -.52), Vector3(.055, .055, .14))
	beam("gold", Vector3(.395, 1.23, -.594), Vector3(.665, 1.23, -.594), .022, .022)
	quad("banner", Vector3(.415, 1.217, -.603), Vector3(.415, .90, -.611), Vector3(.535, .835, -.604), Vector3(.645, .90, -.597))
	triangle("banner", Vector3(.415, 1.217, -.603), Vector3(.645, .90, -.597), Vector3(.645, 1.217, -.597))
	box("gold", Vector3(.53, 1.054, -.617), Vector3(.035, .185, .01))
	box("gold", Vector3(.53, 1.095, -.617), Vector3(.12, .03, .01))

func save_supply() -> void:
	DirAccess.make_dir_recursive_absolute("res://buildings/meshes")
	var mesh := ArrayMesh.new()
	for key: String in surfaces:
		var arrays: Array = surfaces[key].commit_to_arrays()
		if arrays[Mesh.ARRAY_VERTEX] != null and arrays[Mesh.ARRAY_VERTEX].size() > 0:
			surfaces[key].commit(mesh)
	var bounds := mesh.get_aabb()
	assert(bounds.position.x >= -1.0001 and bounds.end.x <= 1.0001, "Supply exceeds 2-wide footprint: %s" % bounds)
	assert(bounds.position.z >= -1.0001 and bounds.end.z <= 1.0001, "Supply exceeds 2-deep footprint: %s" % bounds)
	assert(bounds.position.y >= -.0001, "Supply extends below ground")
	var triangles := 0
	for surface in range(mesh.get_surface_count()):
		var arrays := mesh.surface_get_arrays(surface)
		var indices = arrays[Mesh.ARRAY_INDEX]
		var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
		triangles += (indices.size() if indices != null and not indices.is_empty() else vertices.size()) / 3
	assert(triangles < 30000)
	assert(mesh.get_surface_count() <= 15)
	assert(ResourceSaver.save(mesh, SUPPLY_MESH, ResourceSaver.FLAG_COMPRESS) == OK)
	var supply := Node3D.new()
	supply.set_script(load("res://buildings/Building.cs"))
	supply.name = "Supply"
	supply.set("BuildingType", 2) # Local asset identity; no network registration here.
	supply.set("HealthBarHeight", bounds.end.y + .30)
	supply.set_meta("role", "supply")
	supply.set_meta("footprint", Vector2i(2, 2))
	supply.set_meta("front", "-Z")
	var visual := MeshInstance3D.new()
	visual.name = "Visual"
	visual.mesh = load(SUPPLY_MESH)
	supply.add_child(visual)
	visual.owner = supply
	var selection := Area3D.new()
	selection.name = "SelectionArea"
	selection.collision_layer = 8
	selection.collision_mask = 0
	selection.monitoring = false
	selection.monitorable = false
	supply.add_child(selection)
	selection.owner = supply
	var collision := CollisionShape3D.new()
	collision.name = "CollisionShape3D"
	collision.position.y = bounds.end.y * .5
	var shape := BoxShape3D.new()
	shape.size = Vector3(2, bounds.end.y, 2)
	collision.shape = shape
	selection.add_child(collision)
	collision.owner = supply
	var entrance_marker := Marker3D.new()
	entrance_marker.name = "Entrance"
	entrance_marker.position = Vector3(0, 0, -1)
	supply.add_child(entrance_marker)
	entrance_marker.owner = supply
	var scene := PackedScene.new()
	assert(scene.pack(supply) == OK)
	assert(ResourceSaver.save(scene, SUPPLY_SCENE) == OK)
	print("PASS: Supply footprint 2x2, bounds ", bounds, ", triangles ", triangles, ", surfaces ", mesh.get_surface_count())
	supply.free()
