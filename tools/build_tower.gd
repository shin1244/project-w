extends "res://tools/build_fortress.gd"
## Builds the original 2x2 timber mesh and scales its scene to the server's 3x3 footprint.

func _initialize() -> void:
	material("stone", Color("778176"))
	material("stone_light", Color("a3ac9c"))
	material("stone_dark", Color("59665e"))
	material("wood", Color("583d2d"))
	material("wood_light", Color("986c45"))
	material("dark", Color("25352f"), .3)
	material("gold", Color("c49a49"), .35)
	material("banner", Color("376a94"))
	build_timber_platform()
	var base_mesh := finish_mesh("res://buildings/meshes/Tower.res")
	reset_surfaces()
	build_ballista()
	var weapon_mesh := finish_mesh("res://buildings/meshes/TowerBallista.res", .55)
	save_tower(base_mesh, weapon_mesh, "Tower", 6, 2, 2.10, .55)
	quit()

func build_timber_platform() -> void:
	chamfered_block("stone_dark", 1.0, .16, 0, .10)
	chamfered_block("stone", .92, .14, .10, .07)
	# Four braced timbers replace the fortress's enclosed masonry shaft.
	for x in [-.64, .64]:
		for z in [-.64, .64]:
			box("stone_light", Vector3(x, .235, z), Vector3(.31, .14, .31))
			box("wood", Vector3(x, .98, z), Vector3(.17, 1.52, .17))
			for y in [.40, 1.50]:
				box("dark", Vector3(x, y, z), Vector3(.185, .07, .185))
	for side in [-1.0, 1.0]:
		var x: float = side * .64
		beam("wood_light", Vector3(x, .40, -.56), Vector3(x, 1.50, .56), .085, .085)
		beam("wood_light", Vector3(x, .40, .56), Vector3(x, 1.50, -.56), .085, .085)
		box("wood", Vector3(0, 1.55, side * .64), Vector3(1.50, .16, .15))
		box("wood", Vector3(x, 1.55, 0), Vector3(.15, .16, 1.50))
	beam("wood_light", Vector3(-.56, .40, .64), Vector3(.56, 1.50, .64), .085, .085)
	beam("wood_light", Vector3(.56, .40, .64), Vector3(-.56, 1.50, .64), .085, .085)
	# Small plank fighting platform and waist-high open guardrails.
	box("wood", Vector3(0, 1.67, 0), Vector3(1.76, .14, 1.76))
	for i in range(9):
		box("wood_light" if i % 3 != 0 else "wood", Vector3(-.76 + i * .19, 1.754, 0), Vector3(.178, .035, 1.68))
	for x in [-.79, .79]:
		for z in [-.79, .79]:
			box("wood", Vector3(x, 1.95, z), Vector3(.11, .47, .11))
			box("dark", Vector3(x, 2.17, z), Vector3(.125, .05, .125))
		box("wood_light", Vector3(x, 2.07, 0), Vector3(.085, .09, 1.56))
		box("wood", Vector3(x, 1.88, 0), Vector3(.06, .055, 1.56))
	box("wood_light", Vector3(0, 2.07, .79), Vector3(1.56, .09, .085))
	box("wood", Vector3(0, 1.88, .79), Vector3(1.56, .055, .06))
	for side in [-1.0, 1.0]:
		box("wood_light", Vector3(side * .53, 2.07, -.79), Vector3(.46, .09, .085))
		box("wood", Vector3(side * .31, 1.93, -.79), Vector3(.085, .37, .085))
	# Ladder reaches the front gap and stays inside the two-cell foundation.
	for x in [-.21, .21]:
		beam("wood", Vector3(x, .17, -.93), Vector3(x, 1.78, -.66), .060, .060)
	for i in range(7):
		var fraction := (i + .5) / 7.0
		box("wood_light", Vector3(0, lerpf(.20, 1.72, fraction), lerpf(-.93, -.66, fraction)), Vector3(.46, .045, .065))
	# A side banner supplies the same team-colour cue as the permanent fortress.
	box("gold", Vector3(.744, 1.47, .08), Vector3(.06, .04, .60))
	quad("banner", Vector3(.755, 1.45, -.18), Vector3(.755, .91, -.18), Vector3(.755, .91, .34), Vector3(.755, 1.45, .34))
	triangle("banner", Vector3(.755, .91, -.18), Vector3(.755, .76, .08), Vector3(.755, .91, .34))
	box("gold", Vector3(.768, 1.18, .08), Vector3(.015, .25, .035))
	box("gold", Vector3(.768, 1.22, .08), Vector3(.015, .035, .20))
	cylinder("dark", Vector3(0, 1.87, 0), .27, .34, .21)
	cylinder("gold", Vector3(0, 2.04, 0), .29, .29, .07)
