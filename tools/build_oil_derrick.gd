extends "res://tools/build_supply.gd"
## Late nineteenth-century cable-tool derrick: open timber tower and steam winch.
## Authored at its final 4x4 size so the oil fissure remains visible below the rig.

const DERRICK_SCENE := "res://buildings/OilDerrick.tscn"
const DERRICK_MESH := "res://buildings/meshes/OilDerrick.res"
const TOWER_LEVELS := [Vector2(.26, .91), Vector2(1.62, .75), Vector2(2.96, .59),
	Vector2(4.25, .44), Vector2(5.43, .30), Vector2(6.03, .23)]

func _initialize() -> void:
	setup_materials()
	# Weathered timber and iron retain the established buildings' material names.
	material("wood", Color("513722"))
	material("wood_light", Color("ad8250"))
	material("wood_warm", Color("815b34"))
	material("dark", Color("26312b"), .32)
	material("gold", Color("a88648"), .36)
	material("roof", Color("483b29"))
	material("roof_mid", Color("66513a"))
	material("roof_light", Color("80684a"))
	build_derrick()
	validate_rotation_clearance()
	save_compact(DERRICK_SCENE, DERRICK_MESH, "OilDerrick", 7, "oil_derrick", Vector2i(4, 4))
	quit()

func apply_server_footprint(building: Node3D) -> void:
	# Unlike older generators, this mesh already uses its final server footprint.
	assert(building.get("BuildingType") == 7)
	assert(building.get_meta("footprint") == Vector2i(4, 4))

func build_derrick() -> void:
	timber_tower()
	crown_and_drill_line()
	steam_winch()
	machine_shelter()
	workyard()

func tower_corner(level: Vector2, x_side: float, z_side: float) -> Vector3:
	return Vector3(level.y * x_side, level.x, level.y * z_side)

func timber_tower() -> void:
	# Individual footings leave the middle open; there is no slab over the oilfield.
	for xs in [-1.0, 1.0]:
		for zs in [-1.0, 1.0]:
			box("stone_dark", Vector3(xs * .91, .07, zs * .91), Vector3(.37, .14, .37))
			box("stone", Vector3(xs * .91, .16, zs * .91), Vector3(.30, .09, .30))
			beam("wood", tower_corner(TOWER_LEVELS[0], xs, zs), tower_corner(TOWER_LEVELS[-1], xs, zs), .175, .175)
			# Narrow sun-facing edge on long squared posts keeps timber legible at RTS scale.
			beam("wood_warm", tower_corner(TOWER_LEVELS[0], xs, zs) + Vector3(.072, 0, -.012),
				tower_corner(TOWER_LEVELS[-1], xs, zs) + Vector3(.072, 0, -.012), .028, .11)
			for level: Vector2 in TOWER_LEVELS:
				box("dark", tower_corner(level, xs, zs), Vector3(.193, .055, .193))
	for i in range(TOWER_LEVELS.size()):
		var level: Vector2 = TOWER_LEVELS[i]
		for side in [-1.0, 1.0]:
			if i > 0 or side > 0:
				beam("wood_warm", tower_corner(level, -1, side), tower_corner(level, 1, side), .11, .105)
			beam("wood_warm", tower_corner(level, side, -1), tower_corner(level, side, 1), .11, .105)
		if i == TOWER_LEVELS.size() - 1:
			continue
		var upper: Vector2 = TOWER_LEVELS[i + 1]
		for side in [-1.0, 1.0]:
			# The front and engine-side first bay are open for servicing the drill.
			if i > 0 or side > 0:
				beam("wood_light", tower_corner(level, -1, side), tower_corner(upper, 1, side), .075, .063)
				beam("wood_warm", tower_corner(level, 1, side), tower_corner(upper, -1, side), .073, .061)
			if i > 0 or side > 0:
				beam("wood_light", tower_corner(level, side, -1), tower_corner(upper, side, 1), .073, .065)
				beam("wood_warm", tower_corner(level, side, 1), tower_corner(upper, side, -1), .073, .061)
	# Two short knees support the entrance crosspiece without closing the drilling bay.
	for side in [-1.0, 1.0]:
		beam("wood_light", Vector3(side * .83, .96, -.83), Vector3(side * .42, 1.62, -.75), .095, .085)
	# A ladder follows the taper along the rear, with a gap at the crown platform.
	var low := Vector3(.77, .29, 1.015)
	var high := Vector3(.15, 6.12, .36)
	for dx in [-.12, .12]:
		beam("wood", low + Vector3(dx, 0, 0), high + Vector3(dx, 0, 0), .046, .046)
	for step in range(22):
		var point := low.lerp(high, (step + .5) / 22.0)
		box("wood_light", point, Vector3(.29, .038, .046))
	# A compact observation step below the crown; timber construction, no modern deck.
	for plank in range(5):
		box("wood_warm" if plank % 2 else "wood_light", Vector3(-.30 + plank * .15, 6.10, .02), Vector3(.14, .085, .68))
	for x in [-.34, .34]:
		for z in [-.29, .33]:
			box("wood", Vector3(x, 6.29, z), Vector3(.05, .40, .05))
		box("wood_light", Vector3(x, 6.47, .02), Vector3(.05, .05, .68))
	box("wood_light", Vector3(0, 6.47, .33), Vector3(.73, .05, .05))

func crown_and_drill_line() -> void:
	# The crown sheave carries the cable down the open centre to a cable-tool bit.
	for x in [-.16, .16]:
		box("wood", Vector3(x, 6.31, -.16), Vector3(.105, .38, .32))
		box("dark", Vector3(x, 6.44, -.16), Vector3(.12, .06, .24))
	wheel(Vector3(0, 6.43, -.16), .205, .105, Basis(Vector3.FORWARD, PI * .5), "dark", "gold")
	beam("dark", Vector3(0, .79, -.355), Vector3(0, 6.43, -.355), .019, .019)
	beam("dark", Vector3(-.89, .83, -.60), Vector3(0, 6.43, .04), .018, .018)
	cylinder("dark", Vector3(0, .69, -.355), .072, .09, .32)
	cylinder("gold", Vector3(0, .83, -.355), .09, .09, .055)
	cylinder("dark", Vector3(0, .42, -.355), .09, .018, .23)
	# Short guide casing and square timber clamps sit over, not across, the fissure.
	cylinder("dark", Vector3(0, .18, -.355), .15, .18, .25)
	for x in [-.23, .23]:
		box("wood_warm", Vector3(x, .20, -.355), Vector3(.12, .16, .43))
	# Small plain pennant retains the existing building team-colour material cue.
	beam("dark", Vector3(.31, 4.19, -.45), Vector3(.89, 4.19, -.45), .035, .035)
	quad("banner", Vector3(.48, 4.17, -.45), Vector3(.48, 3.66, -.46),
		Vector3(.69, 3.53, -.45), Vector3(.88, 3.66, -.44))
	triangle("banner", Vector3(.48, 4.17, -.45), Vector3(.88, 3.66, -.44), Vector3(.88, 4.17, -.44))

func steam_winch() -> void:
	# A compact riveted boiler and exposed flywheel place the rig before electric motors.
	for z in [-.26, .57]:
		box("stone_dark", Vector3(-1.18, .10, z), Vector3(.71, .20, .23))
		box("wood", Vector3(-1.18, .23, z), Vector3(.70, .10, .26))
	var horizontal := Basis(Vector3.RIGHT, PI * .5)
	cylinder("dark", Vector3(-1.19, .58, .14), .28, .28, .98, horizontal)
	for z in [-.36, .64]:
		cylinder("dark", Vector3(-1.19, .58, z), .29, .29, .065, horizontal)
		for n in range(8):
			var angle := TAU * n / 8.0
			sphere("gold", Vector3(-1.19 + cos(angle) * .235, .58 + sin(angle) * .235, z - .04), .016)
	cylinder("dark", Vector3(-1.28, 1.36, .54), .085, .10, 1.14)
	cylinder("dark", Vector3(-1.28, 1.96, .54), .13, .115, .10)
	cylinder("dark", Vector3(-1.28, 2.025, .54), .15, .13, .035)
	# Brass valve, pressure dial and a small steam cylinder beside the flywheel.
	cylinder("gold", Vector3(-1.04, .98, .08), .045, .06, .30)
	wheel(Vector3(-1.04, 1.15, .08), .10, .025, Basis.IDENTITY, "dark", "gold")
	cylinder("gold", Vector3(-1.13, .87, -.31), .09, .09, .06, horizontal)
	cylinder("canvas", Vector3(-1.13, .87, -.348), .072, .072, .012, horizontal)
	beam("dark", Vector3(-1.13, .87, -.358), Vector3(-1.10, .91, -.358), .01, .01)
	cylinder("dark", Vector3(-.91, .40, -.60), .12, .12, .37, Basis(Vector3.FORWARD, PI * .5))
	box("dark", Vector3(-1.20, .35, -.70), Vector3(.58, .16, .19))
	wheel(Vector3(-1.18, .70, -.73), .365, .09, horizontal, "dark", "wood_warm")
	beam("gold", Vector3(-1.18, .70, -.79), Vector3(-.88, .43, -.79), .035, .035)
	# Cable spool behind the open tower leg, driven by the steam flywheel.
	cylinder("wood", Vector3(-.72, .66, -.42), .18, .18, .40, Basis(Vector3.FORWARD, PI * .5))
	for x in [-.94, -.50]:
		cylinder("dark", Vector3(x, .66, -.42), .24, .24, .045, Basis(Vector3.FORWARD, PI * .5))
	for turn in range(8):
		cylinder("dark", Vector3(-.88 + turn * .045, .66, -.42), .186, .186, .018, Basis(Vector3.FORWARD, PI * .5))

func machine_shelter() -> void:
	# An open-sided plank lean-to shelters the boiler without hiding the open derrick.
	for z in [-.88, .88]:
		for x in [-1.59, -.48]:
			var height := 1.47 if x < -1 else 1.74
			box("stone_dark", Vector3(x, .075, z), Vector3(.18, .15, .18))
			box("wood", Vector3(x, height * .5 + .10, z), Vector3(.08, height - .10, .08))
			beam("wood_warm", Vector3(x, height - .36, z), Vector3(x, height - .03, z * .52), .055, .055)
		beam("wood", Vector3(-1.66, 1.52, z), Vector3(-.41, 1.83, z), .075, .085)
	for plank in range(9):
		var z := -.92 + plank * .23
		# Leave a cutout in the back two courses where the boiler chimney exits.
		if plank in [6, 7]:
			roof_plank(-1.68, -1.43, z, .215, plank)
			roof_plank(-1.12, -.39, z, .215, plank)
		else:
			roof_plank(-1.68, -.39, z, .215, plank)
	for x in [-1.61, -.45]:
		var y := lerpf(1.52, 1.83, (x + 1.66) / 1.25)
		beam("wood_light", Vector3(x, y + .07, -1.02), Vector3(x, y + .07, 1.025), .045, .055)

func roof_plank(left: float, right: float, z: float, depth: float, variation: int) -> void:
	var ya := lerpf(1.52, 1.83, (left + 1.66) / 1.25)
	var yb := lerpf(1.52, 1.83, (right + 1.66) / 1.25)
	var mat: String = ["roof", "roof_mid", "roof_light"][variation % 3]
	var a := Vector3(left, ya, z - depth * .5)
	var b := Vector3(left, ya, z + depth * .5)
	var c := Vector3(right, yb, z + depth * .5)
	var d := Vector3(right, yb, z - depth * .5)
	quad(mat, a, b, c, d)
	for edge: Array in [[a, d], [d, c], [c, b], [b, a]]:
		quad("wood", edge[0], edge[1], edge[1] - Vector3(0, .055, 0), edge[0] - Vector3(0, .055, 0))

func workyard() -> void:
	barrel(Vector3(.97, .015, -.92), .235, .57)
	barrel(Vector3(1.30, .015, -.42), .22, .54)
	crate(Vector3(.97, .015, .43), .43)
	crate(Vector3(1.30, .015, .64), .30)
	# A few loose planks provide a small work step while exposing most of the fissure.
	for plank in range(5):
		box("wood_warm" if plank % 2 else "wood_light", Vector3(-.36 + plank * .17, .10, -1.12), Vector3(.16, .07, .44))
	for x in [-.34, .34]:
		box("wood", Vector3(x, .04, -1.12), Vector3(.085, .08, .46))
	for i in range(3):
		beam("dark", Vector3(.91 + i * .06, .17, .20), Vector3(.42 + i * .06, 2.35, .40), .025, .025)

func wheel(center: Vector3, radius: float, depth: float, basis: Basis, rim_mat: String, spoke_mat: String) -> void:
	# An open-spoked wheel, never a solid disc; default axle follows local Y.
	var inner := radius * .76
	for segment in range(16):
		var a := TAU * segment / 16.0
		var b := TAU * (segment + 1) / 16.0
		var outer_a := Vector3(cos(a) * radius, 0, sin(a) * radius)
		var outer_b := Vector3(cos(b) * radius, 0, sin(b) * radius)
		var inner_a := Vector3(cos(a) * inner, 0, sin(a) * inner)
		var inner_b := Vector3(cos(b) * inner, 0, sin(b) * inner)
		for side in [-1.0, 1.0]:
			var offset := Vector3(0, side * depth * .5, 0)
			var points := [outer_a, inner_a, inner_b, outer_b] if side > 0 else [outer_b, inner_b, inner_a, outer_a]
			quad(rim_mat, center + basis * (points[0] + offset), center + basis * (points[1] + offset),
				center + basis * (points[2] + offset), center + basis * (points[3] + offset))
		for radii in [[outer_a, outer_b], [inner_b, inner_a]]:
			quad(rim_mat, center + basis * (radii[0] + Vector3(0, depth * .5, 0)),
				center + basis * (radii[1] + Vector3(0, depth * .5, 0)),
				center + basis * (radii[1] - Vector3(0, depth * .5, 0)),
				center + basis * (radii[0] - Vector3(0, depth * .5, 0)))
	for spoke in range(6):
		var angle := TAU * spoke / 6.0
		beam(spoke_mat, center, center + basis * Vector3(cos(angle) * inner, 0, sin(angle) * inner), radius * .115, depth * .65)
	cylinder(rim_mat, center, radius * .20, radius * .20, depth * 1.6, basis)

func validate_rotation_clearance() -> void:
	var radius := 0.0
	for key: String in surfaces:
		var arrays: Array = surfaces[key].commit_to_arrays()
		if arrays[Mesh.ARRAY_VERTEX] == null:
			continue
		for point: Vector3 in arrays[Mesh.ARRAY_VERTEX]:
			radius = maxf(radius, Vector2(point.x, point.z).length())
	assert(radius <= 2.0, "Derrick exceeds the 4x4 footprint when its visual turns: %s" % radius)
	print("PASS: oil derrick rotated horizontal radius ", radius)
