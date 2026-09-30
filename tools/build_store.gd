extends "res://tools/build_supply.gd"
## A separate 2x2 resource warehouse: loading bay, crates, grain sacks and stacked timber.

func _initialize() -> void:
	setup_materials()
	build_store()
	save_compact("res://buildings/Store.tscn", "res://buildings/meshes/Store.res", "Store", 2, "store")
	quit()

func build_store() -> void:
	# Exact 2x2 stone apron with a low loading platform instead of domestic stairs.
	box("stone_dark", Vector3(0, .055, 0), Vector3(2, .11, 2))
	box("stone", Vector3(0, .145, .25), Vector3(1.65, .18, 1.42))
	for i in range(6):
		box("stone_light", Vector3(-.69 + i * .275, .15, -.47), Vector3(.253, .13, .065))
	box("wood", Vector3(0, .84, .25), Vector3(1.47, 1.23, 1.24))
	for z in [-.385, .885]:
		for i in range(11):
			box("wood_light" if i % 3 == 0 else "wood_warm", Vector3(-.675 + i * .135, .835, z), Vector3(.123, 1.19, .034))
	for side in [-1.0, 1.0]:
		for i in range(9):
			box("wood_light" if i % 3 == 0 else "wood_warm", Vector3(side * .75, .835, -.30 + i * .14), Vector3(.035, 1.19, .128))
		for z in [-.37, .86]:
			box("wood", Vector3(side * .73, .86, z), Vector3(.11, 1.32, .11))
		for y in [.31, 1.39]:
			box("wood", Vector3(side * .765, y, .25), Vector3(.08, .10, 1.33))
		beam("wood", Vector3(side * .785, .38, -.27), Vector3(side * .785, 1.31, .77), .075, .065)
	for z in [-.40, .88]:
		var a := Vector3(-.74, 1.44, z)
		var b := Vector3(.74, 1.44, z)
		var c := Vector3(0, 2.05, z)
		if z < 0: triangle("wood_warm", a, c, b)
		else: triangle("wood_warm", a, b, c)
		beam("wood", a, c, .07, .08)
		beam("wood", b, c, .07, .08)
		box("wood", Vector3(0, 1.445, z), Vector3(1.55, .09, .08))
	roof(.94, -.50, .95, 1.49, 2.13, 5, 8)
	# Wide, iron-bound double doors with a hoist beam above the loading entrance.
	box("dark", Vector3(0, .80, -.414), Vector3(.85, 1.10, .04))
	for i in range(8):
		box("wood_light", Vector3(-.36 + i * .103, .80, -.445), Vector3(.094, 1.03, .038))
	for x in [-.45, 0.0, .45]:
		box("wood", Vector3(x, .81, -.478), Vector3(.055, 1.13, .047))
	for side in [-1.0, 1.0]:
		for y in [.49, 1.11]:
			box("dark", Vector3(side * .24, y, -.49), Vector3(.39, .047, .026))
		beam("wood", Vector3(side * .07, .47, -.505), Vector3(side * .38, 1.16, -.505), .047, .027)
		sphere("gold", Vector3(side * .075, .84, -.527), .025)
	box("wood", Vector3(0, 1.45, -.65), Vector3(.095, .095, .61))
	beam("wood", Vector3(0, 1.61, -.41), Vector3(0, 1.45, -.88), .05, .05)
	cylinder("gold", Vector3(0, 1.37, -.86), .068, .068, .035, Basis(Vector3.RIGHT, PI * .5))
	beam("canvas", Vector3(0, 1.34, -.875), Vector3(0, 1.12, -.875), .018, .018)
	box("dark", Vector3(.025, 1.09, -.875), Vector3(.07, .025, .025))
	# Goods stay within the 2x2 apron, leaving the center loading lane open.
	crate(Vector3(-.67, .11, -.76), .37)
	crate(Vector3(-.69, .50, -.73), .29)
	sack(Vector3(-.33, .11, -.83), .12, .33)
	barrel(Vector3(.79, .11, .52), .17, .43)
	for row in range(3):
		for column in range(3 - row):
			var x := .39 + column * .17 + row * .085
			var y := .20 + row * .15
			cylinder("wood", Vector3(x, y, -.65), .088, .088, .62, Basis(Vector3.RIGHT, PI * .5))
			cylinder("wood_light", Vector3(x, y, -.965), .073, .073, .012, Basis(Vector3.RIGHT, PI * .5))
			cylinder("wood_warm", Vector3(x, y, -.973), .035, .035, .008, Basis(Vector3.RIGHT, PI * .5))
	for x in [.29, .90]:
		box("wood", Vector3(x, .23, -.65), Vector3(.035, .29, .65))
	# Crate emblem and a team pennant identify the depot at RTS zoom.
	box("banner", Vector3(0, 1.76, -.443), Vector3(.36, .30, .035))
	box("wood_light", Vector3(0, 1.765, -.47), Vector3(.225, .18, .02))
	beam("gold", Vector3(-.105, 1.68, -.486), Vector3(.105, 1.85, -.486), .025, .015)
	beam("gold", Vector3(.105, 1.68, -.486), Vector3(-.105, 1.85, -.486), .025, .015)
