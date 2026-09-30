extends "res://tools/build_supply.gd"
## 3x2 forge: stone forge, tall chimney and an open anvil court. Server type 5.

func _initialize() -> void:
	material("stone", Color("777e77"))
	material("stone_light", Color("a3aa9d"))
	material("stone_dark", Color("505a54"))
	material("wood", Color("583b29"))
	material("wood_light", Color("a47947"))
	material("wood_warm", Color("896039"))
	material("dark", Color("252c2b"), .25)
	material("roof", Color("274e66"))
	material("roof_mid", Color("315971"))
	material("roof_light", Color("3b667d"))
	material("gold", Color("c9a25a"), .25)
	material("steel", Color("a9b8bd"), .55)
	material("banner", Color("376a94"))
	material("ember", Color("e76424"))
	material("fire", Color("ffd07a"))
	for key in ["ember", "fire"]:
		materials[key].emission_enabled = true
		materials[key].emission = materials[key].albedo_color
		materials[key].emission_energy_multiplier = .8
	forge_foundation()
	workshop()
	workshop_roof()
	forge()
	anvil_and_tools()
	smith_sign()
	save_compact("res://buildings/Forge.tscn", "res://buildings/meshes/Forge.res", "Forge", 5, "forge", Vector2i(3, 2))
	quit()

func forge_foundation() -> void:
	var corners := [Vector2(-1.37, -1), Vector2(1.37, -1), Vector2(1.5, -.87), Vector2(1.5, .87),
		Vector2(1.37, 1), Vector2(-1.37, 1), Vector2(-1.5, .87), Vector2(-1.5, -.87)]
	for i in range(corners.size()):
		var a: Vector2 = corners[i]
		var b: Vector2 = corners[(i + 1) % corners.size()]
		triangle("stone", Vector3(0, .14, 0), Vector3(b.x, .14, b.y), Vector3(a.x, .14, a.y))
		quad("stone_dark", Vector3(a.x, 0, a.y), Vector3(a.x, .14, a.y), Vector3(b.x, .14, b.y), Vector3(b.x, 0, b.y))
	for col in range(8):
		for row in range(2):
			box("stone_light" if (col + row) % 4 == 0 else "stone_dark", Vector3(-1.24 + col * .35, .151, -.79 + row * .32), Vector3(.33, .024, .30))
	box("stone_dark", Vector3(-.62, .26, .23), Vector3(1.50, .24, 1.29))
	box("stone_light", Vector3(-.62, .40, .23), Vector3(1.55, .06, 1.33))
	box("stone", Vector3(-.53, .22, -.60), Vector3(.70, .14, .31))
	box("stone_light", Vector3(-.53, .32, -.45), Vector3(.65, .10, .22))

func workshop() -> void:
	box("stone", Vector3(-.62, 1.02, .24), Vector3(1.33, 1.19, 1.13))
	# Low stone courses and heavy oak framing leave the open forge readable beside it.
	for z in [-.337, .817]:
		for row in range(2):
			for col in range(5):
				box("stone_light" if (col + row) % 3 == 0 else "stone_dark", Vector3(-1.15 + col * .267, .54 + row * .22, z), Vector3(.25, .20, .035))
		for x in [-1.29, -.62, .05]:
			box("wood", Vector3(x, 1.02, z), Vector3(.095, 1.30, .10))
		for y in [.86, 1.61]:
			box("wood", Vector3(-.62, y, z), Vector3(1.45, .10, .12))
		var a := Vector3(-1.29, 1.64, z)
		var b := Vector3(.05, 1.64, z)
		var c := Vector3(-.62, 2.28, z)
		if z < 0: triangle("wood_warm", a, c, b)
		else: triangle("wood_warm", a, b, c)
		beam("wood", a, c, .065, .07)
		beam("wood", b, c, .065, .07)
		box("wood", Vector3(-.62, 1.90, z), Vector3(.08, .54, .08))
	for x in [-1.31, .07]:
		box("wood", Vector3(x, 1.60, .24), Vector3(.10, .13, 1.29))
		box("wood", Vector3(x, .89, .24), Vector3(.08, .08, 1.24))
		beam("wood", Vector3(x, .95, .0), Vector3(x, 1.54, .71), .065, .065)
	# Iron-strapped workshop door and a barred side window.
	box("dark", Vector3(-.53, .94, -.37), Vector3(.63, 1.02, .04))
	for i in range(5):
		box("wood_light", Vector3(-.772 + i * .121, .94, -.4), Vector3(.111, .97, .04))
	for y in [.61, 1.25]:
		box("dark", Vector3(-.53, y, -.429), Vector3(.57, .045, .02))
	sphere("gold", Vector3(-.34, .93, -.45), .024)
	box("wood", Vector3(-.53, 1.48, -.395), Vector3(.76, .10, .12))
	for z in [.0, .55]:
		box("dark", Vector3(-1.323, 1.22, z), Vector3(.03, .36, .32))
		for dz in [-.09, 0.0, .09]:
			box("steel", Vector3(-1.35, 1.22, z + dz), Vector3(.018, .34, .021))
	# Rear materials hatch keeps the model detailed from the in-game camera direction.
	box("dark", Vector3(-.59, 1.22, .846), Vector3(.47, .38, .03))
	for i in range(4):
		box("wood_light", Vector3(-.59, 1.08 + i * .09, .87), Vector3(.44, .039, .025))

func workshop_roof() -> void:
	var center := -.62
	var half := .79
	var front := -.50
	var back := .91
	for side in [-1.0, 1.0]:
		var left := minf(center, center + side * half)
		var right := maxf(center, center + side * half)
		quad("roof", Vector3(left, roof_y(left - center, half, 1.72, 2.36), front),
			Vector3(left, roof_y(left - center, half, 1.72, 2.36), back),
			Vector3(right, roof_y(right - center, half, 1.72, 2.36), back),
			Vector3(right, roof_y(right - center, half, 1.72, 2.36), front))
		for row in range(5):
			for col in range(8):
				var xa: float = center + side * (half * row / 5.0 + .007)
				var xb: float = center + side * (half * (row + 1) / 5.0 - .009)
				var low := minf(xa, xb)
				var high := maxf(xa, xb)
				var za := front + col * (back - front) / 8.0 + .007
				var zb := front + (col + 1) * (back - front) / 8.0 - .007
				var ya := roof_y(low - center, half, 1.72, 2.36) + .025
				var yb := roof_y(high - center, half, 1.72, 2.36) + .025
				var key: String = ["roof", "roof_mid", "roof_light"][(col * 5 + row) % 3]
				quad(key, Vector3(low, ya, za), Vector3(low, ya, zb), Vector3(high, yb, zb), Vector3(high, yb, za))
		box("wood", Vector3(center + side * half, 1.69, .205), Vector3(.075, .10, 1.48))
	for z in [front, back]:
		beam("wood", Vector3(center - half, 1.72, z), Vector3(center, 2.36, z), .075, .075)
		beam("wood", Vector3(center, 2.36, z), Vector3(center + half, 1.72, z), .075, .075)
	box("gold", Vector3(center, 2.40, .205), Vector3(.095, .035, 1.49))

func forge() -> void:
	var x := .82
	# Real open mouth: recessed soot-dark back, two masonry piers and a stone arch.
	box("stone_dark", Vector3(x, .26, .20), Vector3(1.04, .23, 1.19))
	box("stone_light", Vector3(x, .395, .13), Vector3(1.08, .07, 1.24))
	box("stone_dark", Vector3(x, .75, .64), Vector3(.99, .72, .24))
	box("dark", Vector3(x, .72, .49), Vector3(.67, .59, .035))
	for side in [-1.0, 1.0]:
		box("stone", Vector3(x + side * .405, .73, .17), Vector3(.22, .64, .93))
		for row in range(3):
			box("stone_light" if row % 2 == 0 else "stone", Vector3(x + side * .405, .50 + row * .21, -.306), Vector3(.235, .191, .035))
	# Arch wedges in the XY plane, extruded along Z.
	for i in range(7):
		var angle := PI * (i + .5) / 7.0
		box("stone_light" if i % 2 == 0 else "stone", Vector3(x + cos(angle) * .36, .89 + sin(angle) * .27, -.29),
			Vector3(.17, .23, .25), Basis(Vector3.BACK, angle - PI * .5))
	box("dark", Vector3(x, .452, .05), Vector3(.67, .036, .59))
	for i in range(5):
		cylinder("dark", Vector3(.57 + i * .12, .48, -.04), .05, .065, .30, Basis(Vector3.RIGHT, PI * .5))
		box("ember", Vector3(.57 + i * .12, .504, -.055), Vector3(.084, .022, .22))
	# Faceted flames are static emissive geometry; no particles or per-frame lighting work.
	for i in range(3):
		var center := Vector3(.63 + i * .19, .53, -.09 + (i % 2) * .08)
		flame("ember", center, .11, .30 + (i % 2) * .12)
		flame("fire", center + Vector3(0, .015, -.045), .060, .19 + (i % 2) * .07)
	# Tapered stone smoke hood transitioning into the square chimney shaft.
	var low := [Vector3(.30, 1.04, -.39), Vector3(1.34, 1.04, -.39), Vector3(1.34, 1.04, .78), Vector3(.30, 1.04, .78)]
	var high := [Vector3(.56, 1.61, .14), Vector3(1.08, 1.61, .14), Vector3(1.08, 1.61, .67), Vector3(.56, 1.61, .67)]
	for i in range(4):
		var next := (i + 1) % 4
		quad("stone" if i % 2 == 0 else "stone_dark", low[i], high[i], high[next], low[next])
	box("stone_dark", Vector3(x, 2.28, .405), Vector3(.48, 1.37, .49))
	for row in range(8):
		var y := 1.70 + row * .16
		for z in [.147, .663]:
			for col in range(2):
				box("stone_light" if (row + col) % 4 == 0 else "stone", Vector3(x - .127 + col * .254, y, z), Vector3(.240, .146, .038))
		for sx in [.556, 1.084]:
			box("stone" if row % 3 == 0 else "stone_dark", Vector3(sx, y, .405), Vector3(.038, .146, .48))
	for y in [1.62, 2.45]:
		box("dark", Vector3(x, y, .405), Vector3(.565, .075, .575))
	# Four cap stones leave a visible black flue instead of a solid top.
	box("dark", Vector3(x, 2.982, .405), Vector3(.38, .03, .39))
	for z in [.107, .703]:
		box("stone_light", Vector3(x, 3.02, z), Vector3(.68, .14, .10))
	for sx in [.53, 1.11]:
		box("stone_light", Vector3(sx, 3.02, .405), Vector3(.10, .14, .51))

func flame(key: String, bottom: Vector3, radius: float, height: float) -> void:
	var peak := bottom + Vector3(radius * .20, height, 0)
	for i in range(5):
		var a := TAU * i / 5.0
		var b := TAU * (i + 1) / 5.0
		triangle(key, bottom + Vector3(cos(a) * radius, 0, sin(a) * radius), peak,
			bottom + Vector3(cos(b) * radius, 0, sin(b) * radius))

func anvil_and_tools() -> void:
	# Broad steel face, narrow waist and tapered horn on an oak stump.
	cylinder("wood", Vector3(.49, .305, -.73), .22, .26, .29)
	cylinder("dark", Vector3(.49, .32, -.73), .225, .24, .05)
	box("dark", Vector3(.49, .465, -.73), Vector3(.36, .065, .26))
	box("dark", Vector3(.49, .57, -.73), Vector3(.18, .18, .17))
	box("steel", Vector3(.49, .705, -.73), Vector3(.48, .13, .23))
	cylinder("steel", Vector3(.16, .71, -.73), 0, .103, .25, Basis(Vector3.BACK, PI * .5))
	box("dark", Vector3(.62, .772, -.73), Vector3(.044, .007, .045))
	# Hammer rests across the working face.
	beam("wood_light", Vector3(.38, .79, -.86), Vector3(.65, .79, -.59), .033, .033)
	box("dark", Vector3(.65, .815, -.59), Vector3(.19, .087, .080), Basis(Vector3.UP, -.78))
	# Low bench with billets and tongs to the left of the workshop door.
	for x in [-1.29, -.99]:
		for z in [-.87, -.61]:
			box("wood", Vector3(x, .36, z), Vector3(.06, .40, .06))
	box("wood_light", Vector3(-1.14, .57, -.74), Vector3(.50, .09, .42))
	for i in range(3):
		box("steel", Vector3(-1.27 + i * .115, .655, -.76), Vector3(.09, .08, .22))
	for direction in [-1.0, 1.0]:
		beam("dark", Vector3(-1.25, .643, -.54 + direction * .035), Vector3(-1.00, .643, -.54 - direction * .025), .016, .016)
	# Cooling tub and a rack of forged blades fill the outer edge without overhang.
	barrel(Vector3(1.20, .16, -.72), .19, .37)
	cylinder("dark", Vector3(1.20, .538, -.72), .149, .149, .01)
	cylinder("steel", Vector3(1.20, .544, -.72), .126, .126, .005)
	for z in [.06, .65]:
		box("wood", Vector3(1.405, .54, z), Vector3(.065, .74, .065))
	for y in [.39, .80]:
		box("wood_light", Vector3(1.405, y, .355), Vector3(.07, .06, .73))
	for i in range(3):
		var z := .13 + i * .20
		beam("steel", Vector3(1.42, .28, z), Vector3(1.37, 1.06, z), .038, .045)
		box("gold", Vector3(1.38, .91, z), Vector3(.06, .028, .14))
		box("wood", Vector3(1.375, .999, z), Vector3(.045, .14, .047))

func smith_sign() -> void:
	# Gold hammer on a blue gable plate; the plate and roof support existing team tint.
	box("dark", Vector3(-.62, 1.96, -.381), Vector3(.47, .43, .045))
	box("banner", Vector3(-.62, 1.96, -.411), Vector3(.41, .37, .025))
	beam("gold", Vector3(-.73, 1.84, -.432), Vector3(-.54, 2.04, -.432), .042, .025)
	box("steel", Vector3(-.54, 2.04, -.451), Vector3(.22, .092, .038), Basis(Vector3.BACK, -.75))
