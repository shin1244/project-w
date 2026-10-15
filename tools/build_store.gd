extends "res://tools/build_supply.gd"
## A 4x4 forward supply depot: stone office, small lookout, and an open return yard.

func _initialize() -> void:
	setup_materials()
	build_store()
	save_compact("res://buildings/Store.tscn", "res://buildings/meshes/Store.res", "Store", 2, "store", Vector2i(4, 4))
	quit()

func build_store() -> void:
	depot_foundation()
	depot_office()
	depot_canopy()
	depot_lookout()
	# Incoming goods flank the open return lane; this is a staffed outpost, not a barn.
	crate(Vector3(1.45, .17, -.84), .39)
	crate(Vector3(1.53, .17, -.35), .32)
	barrel(Vector3(1.61, .17, .50), .22, .56)
	barrel(Vector3(1.61, .17, 1.13), .22, .56)
	for row in range(2):
		for column in range(3 - row):
			var x := -1.55 + column * .22 + row * .11
			var y := .28 + row * .19
			cylinder("wood", Vector3(x, y, -.86), .115, .115, .72, Basis(Vector3.RIGHT, PI * .5))
			cylinder("wood_light", Vector3(x, y, -1.225), .096, .096, .015, Basis(Vector3.RIGHT, PI * .5))
	for x in [-1.72, -1.00]:
		box("wood", Vector3(x, .25, -.86), Vector3(.055, .22, .79))
	# Low boundary stones leave the whole front approach open.
	for side in [-1.0, 1.0]:
		for z in [-1.52, -.70, .16, .98]:
			box("stone", Vector3(side * 1.90, .29, z), Vector3(.14, .30, .64))
		box("stone_light", Vector3(side * 1.90, .47, 1.67), Vector3(.19, .60, .19))

func depot_foundation() -> void:
	var corners := [Vector2(-1.84, -2), Vector2(1.84, -2), Vector2(2, -1.84), Vector2(2, 1.84),
		Vector2(1.84, 2), Vector2(-1.84, 2), Vector2(-2, 1.84), Vector2(-2, -1.84)]
	for i in range(corners.size()):
		var a: Vector2 = corners[i]
		var b: Vector2 = corners[(i + 1) % corners.size()]
		triangle("stone", Vector3(0, .15, 0), Vector3(b.x, .15, b.y), Vector3(a.x, .15, a.y))
		quad("stone_dark", Vector3(a.x, 0, a.y), Vector3(a.x, .15, a.y), Vector3(b.x, .15, b.y), Vector3(b.x, 0, b.y))
	# Broad paving and a short bevelled threshold, not a narrow loading dock.
	for x in range(4):
		for z in range(3):
			box("stone_light" if (x + z) % 3 == 0 else "stone", Vector3(-1.35 + x * .90, .165, -1.47 + z * .53), Vector3(.86, .03, .49))
	quad("stone_light", Vector3(-.57, .001, -1.99), Vector3(-.57, .18, -1.65),
		Vector3(.57, .18, -1.65), Vector3(.57, .001, -1.99))

func depot_office() -> void:
	box("stone_dark", Vector3(-.25, .39, .95), Vector3(2.94, .48, 1.53))
	box("plaster", Vector3(-.25, 1.19, .95), Vector3(2.82, 1.18, 1.42))
	for z in [.17, 1.73]:
		for i in range(8):
			box("stone_light" if i % 3 == 0 else "stone", Vector3(-1.53 + i * .365, .39, z), Vector3(.34, .37, .07))
		box("wood", Vector3(-.25, 1.77, z), Vector3(2.98, .14, .12))
	for side in [-1.0, 1.0]:
		var x: float = -.25 + side * 1.45
		for i in range(4):
			box("stone", Vector3(x, .39, .38 + i * .38), Vector3(.08, .37, .35))
		for z in [.23, 1.67]:
			box("wood", Vector3(x, 1.18, z), Vector3(.13, 1.30, .13))
		box("wood", Vector3(x, 1.77, .95), Vector3(.13, .14, 1.58))
	# One office door and an open reception shutter replace the old full-height loading doors.
	box("dark", Vector3(-.88, .97, .195), Vector3(.72, 1.36, .10))
	for i in range(5):
		box("wood_light", Vector3(-1.15 + i * .135, .97, .13), Vector3(.125, 1.28, .055))
	for x in [-1.28, -.48]:
		box("wood", Vector3(x, 1.00, .11), Vector3(.10, 1.44, .13))
	box("stone_light", Vector3(-.88, 1.73, .10), Vector3(.92, .13, .18))
	box("stone_light", Vector3(-.88, .235, -.04), Vector3(.97, .17, .40))
	sphere("gold", Vector3(-.65, .96, .08), .038)
	box("wood", Vector3(.50, 1.19, .16), Vector3(.91, .73, .10))
	box("dark", Vector3(.50, 1.21, .095), Vector3(.75, .57, .045))
	for x in [.16, .84]:
		box("wood_light", Vector3(x, 1.21, .06), Vector3(.06, .60, .06))
	box("wood_light", Vector3(.50, .83, -.015), Vector3(1.07, .10, .36))
	box("wood_warm", Vector3(.50, 1.61, .03), Vector3(.84, .25, .06))
	box("wood", Vector3(-.06, 1.14, .115), Vector3(.27, .39, .09))
	box("canvas", Vector3(-.06, 1.14, .06), Vector3(.20, .31, .025))
	for y in [1.07, 1.16, 1.25]:
		box("wood_light", Vector3(-.07, y, .043), Vector3(.11, .014, .012))
	hip_roof(Vector2(-.25, .95), Vector2(3.22, 1.81), 1.86, 2.44)
	# Office windows are ordinary shutters, without firing slits or weapon mounts.
	lodging_window(Vector3(-1.715, 1.18, .94), Basis(Vector3.UP, PI * .5))

func depot_canopy() -> void:
	# Canvas shelter and a low receiving counter keep the intake open to the yard.
	for x in [.28, 1.77]:
		box("stone_dark", Vector3(x, .24, -1.44), Vector3(.23, .19, .23))
		box("wood", Vector3(x, 1.00, -1.44), Vector3(.10, 1.59, .10))
		beam("wood", Vector3(x, 1.34, -1.44), Vector3(x, 1.79, -1.08), .065, .065)
		beam("wood", Vector3(x, 1.84, .10), Vector3(x, 1.69, -1.57), .075, .075)
	box("wood", Vector3(1.025, 1.69, -1.57), Vector3(1.65, .09, .09))
	for strip in range(6):
		var left := .20 + strip * .275
		quad("canvas" if strip % 3 != 1 else "banner", Vector3(left, 1.69, -1.60), Vector3(left, 1.93, .08),
			Vector3(left + .275, 1.93, .08), Vector3(left + .275, 1.69, -1.60))
		quad("canvas" if strip % 3 != 1 else "banner", Vector3(left, 1.68, -1.61), Vector3(left + .275, 1.68, -1.61),
			Vector3(left + .275, 1.52, -1.61), Vector3(left, 1.56, -1.61))
	box("wood", Vector3(.88, .48, -.62), Vector3(.11, .62, .66))
	box("wood", Vector3(1.60, .48, -.62), Vector3(.11, .62, .66))
	for i in range(4):
		box("wood_light", Vector3(1.24, .81, -.86 + i * .16), Vector3(.97, .09, .145))
	box("wood_warm", Vector3(1.24, .59, -.96), Vector3(.87, .31, .06))

func depot_lookout() -> void:
	# A modest roofed lookout signals a staffed forward base, without defense equipment.
	box("stone", Vector3(-.91, 2.39, 1.07), Vector3(.79, .43, .78))
	box("stone_light", Vector3(-.91, 2.62, 1.07), Vector3(.91, .10, .88))
	for x in [-1.27, -.55]:
		for z in [.72, 1.42]:
			box("wood", Vector3(x, 2.89, z), Vector3(.08, .56, .08))
	for z in [.71, 1.43]:
		box("wood_light", Vector3(-.91, 2.72, z), Vector3(.79, .085, .07))
		box("wood", Vector3(-.91, 3.13, z), Vector3(.87, .08, .09))
	for x in [-1.28, -.54]:
		box("wood_light", Vector3(x, 2.72, 1.07), Vector3(.07, .085, .76))
	hip_roof(Vector2(-.91, 1.07), Vector2(1.08, 1.04), 3.14, 3.43)
	# Short hanging pennant for team color, without a castle-height flag.
	box("wood", Vector3(-1.44, 1.80, -.10), Vector3(.06, .06, .49))
	quad("banner", Vector3(-1.62, 1.79, -.35), Vector3(-1.62, 1.18, -.35),
		Vector3(-1.43, 1.09, -.35), Vector3(-1.24, 1.18, -.35))
	triangle("banner", Vector3(-1.62, 1.79, -.35), Vector3(-1.24, 1.18, -.35), Vector3(-1.24, 1.79, -.35))
	# A small receiving-bin emblem avoids the healing implication of a cross.
	for x in [-1.53, -1.33]:
		box("gold", Vector3(x, 1.39, -.362), Vector3(.025, .15, .018))
	box("gold", Vector3(-1.43, 1.32, -.362), Vector3(.22, .025, .018))
	box("gold", Vector3(-1.43, 1.58, -.362), Vector3(.025, .18, .018))
	beam("gold", Vector3(-1.50, 1.55, -.362), Vector3(-1.43, 1.48, -.362), .025, .018)
	beam("gold", Vector3(-1.36, 1.55, -.362), Vector3(-1.43, 1.48, -.362), .025, .018)

func hip_roof(center: Vector2, size: Vector2, eave: float, ridge: float) -> void:
	var a := Vector3(center.x - size.x * .5, eave, center.y - size.y * .5)
	var b := Vector3(center.x + size.x * .5, eave, center.y - size.y * .5)
	var c := Vector3(center.x + size.x * .5, eave, center.y + size.y * .5)
	var d := Vector3(center.x - size.x * .5, eave, center.y + size.y * .5)
	var inset := minf(size.x, size.y) * .43
	var left := Vector3(a.x + inset, ridge, center.y)
	var right := Vector3(b.x - inset, ridge, center.y)
	quad("roof_mid", a, left, right, b)
	quad("roof", d, c, right, left)
	triangle("roof_light", a, d, left)
	triangle("roof_mid", b, right, c)
	for row in range(1, 5):
		var t := row / 5.0
		beam("roof_light", a.lerp(left, t), b.lerp(right, t), .035, .035)
		beam("roof_mid", d.lerp(left, t), c.lerp(right, t), .035, .035)
	for edge: Array in [[a, b], [b, c], [c, d], [d, a]]:
		beam("wood", edge[0], edge[1], .09, .08)
	for point: Vector3 in [a, d]:
		beam("roof_light", point, left, .045, .045)
	for point: Vector3 in [b, c]:
		beam("roof_light", point, right, .045, .045)
	beam("gold", left, right, .055, .06)
