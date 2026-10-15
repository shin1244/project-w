extends "res://tools/build_supply.gd"
## Shared geometry for the two tier-two buildings, authored at final world scale.

func apply_server_footprint(building: Node3D) -> void:
	assert(int(building.get("BuildingType")) in [8, 9])
	assert(building.scale == Vector3.ONE)

func machine_materials() -> void:
	setup_materials()
	material("canvas", Color("b78350"), .35) # Hammered copper, separate from team-coloured slate.
	material("window", Color("b2d0c9"), .15)
	material("gold", Color("cfac65"), .42)

func octagonal_base(radius: float) -> void:
	var outline: Array[Vector3] = []
	for i in range(8):
		var angle := TAU * (i + .5) / 8.0
		outline.append(Vector3(cos(angle) * radius, .18, sin(angle) * radius))
	for i in range(8):
		var a: Vector3 = outline[i]
		var b: Vector3 = outline[(i + 1) % 8]
		triangle("stone", Vector3(0, .18, 0), b, a)
		quad("stone_dark", a * Vector3(1, 0, 1), a, b, b * Vector3(1, 0, 1))
		beam("stone_light", a + Vector3(0, .02, 0), b + Vector3(0, .02, 0), .06, .07)

func open_wheel(center: Vector3, radius: float, thickness: float, basis: Basis, key := "gold", teeth := false) -> void:
	var inner := radius * .70
	for i in range(16):
		var a := TAU * i / 16.0
		var b := TAU * (i + 1) / 16.0
		var oa := Vector3(cos(a) * radius, 0, sin(a) * radius)
		var ob := Vector3(cos(b) * radius, 0, sin(b) * radius)
		var ia := Vector3(cos(a) * inner, 0, sin(a) * inner)
		var ib := Vector3(cos(b) * inner, 0, sin(b) * inner)
		for side in [-1.0, 1.0]:
			var offset := Vector3(0, side * thickness * .5, 0)
			var corners: Array = [oa, ia, ib, ob] if side > 0 else [ob, ib, ia, oa]
			quad(key, center + basis * (corners[0] + offset), center + basis * (corners[1] + offset),
				center + basis * (corners[2] + offset), center + basis * (corners[3] + offset))
		for edge in [[oa, ob], [ib, ia]]:
			quad(key, center + basis * (edge[0] + Vector3(0, thickness * .5, 0)),
				center + basis * (edge[1] + Vector3(0, thickness * .5, 0)),
				center + basis * (edge[1] - Vector3(0, thickness * .5, 0)),
				center + basis * (edge[0] - Vector3(0, thickness * .5, 0)))
	for i in range(6):
		var angle := TAU * i / 6.0
		beam(key, center, center + basis * Vector3(cos(angle) * inner, 0, sin(angle) * inner), radius * .12, thickness * .6)
	if teeth:
		for i in range(12):
			var angle := TAU * i / 12.0
			box(key, center + basis * Vector3(cos(angle) * radius, 0, sin(angle) * radius),
				Vector3(radius * .28, thickness * .9, radius * .18), basis * Basis(Vector3.UP, -angle))
	cylinder("dark", center, radius * .20, radius * .20, thickness * 1.8, basis)

func validate_clearance() -> void:
	var radius := 0.0
	for key: String in surfaces:
		var arrays: Array = surfaces[key].commit_to_arrays()
		if arrays[Mesh.ARRAY_VERTEX] == null:
			continue
		for point: Vector3 in arrays[Mesh.ARRAY_VERTEX]:
			radius = maxf(radius, Vector2(point.x, point.z).length())
	assert(radius <= 2.0, "Tier-two visual exceeds its four-cell narrow axis when turning: %s" % radius)
	print("PASS: tier-two rotated horizontal radius ", radius)
