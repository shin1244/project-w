extends "res://tools/build_tier_two_base.gd"
## Stone-and-timber research house with a copper observatory and brass instruments.

func _initialize() -> void:
	machine_materials()
	octagonal_base(1.92)
	research_house()
	observatory()
	entrance_and_instruments()
	validate_clearance()
	save_compact("res://buildings/ResearchLab.tscn", "res://buildings/meshes/ResearchLab.res", "ResearchLab", 9, "research_lab", Vector2i(4, 4))
	quit()

func research_house() -> void:
	# Eight masonry facets distinguish the research house from rectangular workshops.
	cylinder("stone_dark", Vector3(0, .36, .10), 1.30, 1.39, .36)
	cylinder("stone_light", Vector3(0, .56, .10), 1.36, 1.36, .10)
	for i in range(8):
		var angle := TAU * i / 8.0
		var basis := Basis(Vector3.UP, angle)
		var center := Vector3(0, 1.66, .10) + basis * Vector3(0, 0, -1.20)
		box("plaster", center, Vector3(.99, 2.10, .13), basis)
		for sx in [-.49, .49]:
			box("wood", center + basis * Vector3(sx, 0, -.015), Vector3(.09, 2.18, .12), basis)
		for y in [.71, 2.61]:
			box("wood", Vector3(center.x, y, center.z), Vector3(1.04, .11, .16), basis)
		if i != 0:
			box("dark", center + basis * Vector3(0, .02, -.085), Vector3(.49, .92, .025), basis)
			box("window", center + basis * Vector3(0, .02, -.103), Vector3(.39, .81, .018), basis)
			box("gold", center + basis * Vector3(0, .02, -.121), Vector3(.028, .85, .018), basis)
			box("gold", center + basis * Vector3(0, .02, -.122), Vector3(.43, .03, .018), basis)
			box("stone_light", center + basis * Vector3(0, -.50, -.10), Vector3(.62, .08, .19), basis)
	# Slate collar and cornice tie the copper dome to the other buildings' roofs.
	cylinder("wood", Vector3(0, 2.72, .10), 1.36, 1.36, .14)
	cylinder("roof", Vector3(0, 2.91, .10), 1.08, 1.48, .30)
	cylinder("roof_light", Vector3(0, 3.08, .10), 1.10, 1.10, .08)
	cylinder("gold", Vector3(0, 3.14, .10), 1.12, 1.12, .045)

func observatory() -> void:
	var center := Vector3(0, 3.15, .10)
	var radius := 1.06
	var height := .98
	for row in range(4):
		var a := PI * .5 * row / 4.0
		var b := PI * .5 * (row + 1) / 4.0
		for segment in range(16):
			var u := TAU * segment / 16.0
			var v := TAU * (segment + 1) / 16.0
			var p := center + Vector3(cos(u) * cos(a) * radius, sin(a) * height, sin(u) * cos(a) * radius)
			var q := center + Vector3(cos(u) * cos(b) * radius, sin(b) * height, sin(u) * cos(b) * radius)
			var r := center + Vector3(cos(v) * cos(b) * radius, sin(b) * height, sin(v) * cos(b) * radius)
			var s := center + Vector3(cos(v) * cos(a) * radius, sin(a) * height, sin(v) * cos(a) * radius)
			quad("canvas" if segment % 4 else "gold", p, q, r, s)
	# Raised seams are visible at normal camera distance and avoid texture dependencies.
	for segment in range(8):
		var angle := TAU * segment / 8.0
		for row in range(4):
			var a := PI * .5 * row / 4.0
			var b := PI * .5 * (row + 1) / 4.0
			beam("gold", center + Vector3(cos(angle) * cos(a) * 1.075, sin(a), sin(angle) * cos(a) * 1.075),
				center + Vector3(cos(angle) * cos(b) * 1.075, sin(b), sin(angle) * cos(b) * 1.075), .035, .035)
	# A fixed nineteenth-century telescope protrudes from the front observation hatch.
	box("dark", Vector3(0, 3.56, -.76), Vector3(.46, .52, .08), Basis(Vector3.RIGHT, -.30))
	var near := Vector3(0, 3.53, -.69)
	var far := Vector3(0, 3.94, -1.37)
	var axis := Basis(Quaternion(Vector3.UP, (far - near).normalized()))
	cylinder("gold", (near + far) * .5, .16, .20, near.distance_to(far), axis)
	cylinder("dark", far, .19, .19, .12, axis)
	cylinder("window", far + (far - near).normalized() * .066, .145, .145, .018, axis)
	cylinder("gold", Vector3(0, 4.23, .10), .035, .055, .24)
	sphere("gold", Vector3(0, 4.37, .10), .075)

func entrance_and_instruments() -> void:
	box("stone", Vector3(0, .25, -1.30), Vector3(.96, .14, .53))
	box("stone_light", Vector3(0, .39, -1.16), Vector3(.82, .14, .33))
	box("dark", Vector3(0, 1.30, -1.17), Vector3(.73, 1.40, .05))
	for i in range(6):
		box("wood_light", Vector3(-.30 + i * .12, 1.29, -1.205), Vector3(.108, 1.31, .035))
	for y in [.85, 1.72]:
		box("dark", Vector3(0, y, -1.23), Vector3(.65, .045, .025))
	sphere("gold", Vector3(.21, 1.28, -1.26), .035)
	for x in [-.43, .43]:
		box("stone_light", Vector3(x, 1.30, -1.18), Vector3(.12, 1.53, .20))
	box("stone_light", Vector3(0, 2.07, -1.18), Vector3(.99, .15, .22))
	# A compass over the door is the building's identifier; no explanatory lettering.
	box("dark", Vector3(0, 2.34, -1.23), Vector3(.62, .47, .055))
	box("banner", Vector3(0, 2.34, -1.265), Vector3(.56, .41, .018))
	open_wheel(Vector3(0, 2.34, -1.293), .16, .018, Basis(Vector3.RIGHT, PI * .5), "gold")
	beam("gold", Vector3(-.11, 2.23, -1.32), Vector3(.11, 2.45, -1.32), .035, .024)
	# Exterior drafting desk: parchment, straightedge and two bound reference books.
	var desk := Vector3(1.15, .87, -.86)
	for dx in [-.22, .22]:
		for dz in [-.17, .17]:
			box("wood", desk + Vector3(dx, -.32, dz), Vector3(.06, .64, .06))
	box("wood_light", desk, Vector3(.61, .085, .53), Basis(Vector3.RIGHT, -.16))
	box("plaster", desk + Vector3(0, .057, -.015), Vector3(.40, .012, .37), Basis(Vector3.RIGHT, -.16))
	beam("gold", desk + Vector3(-.14, .09, -.11), desk + Vector3(.13, .09, .11), .022, .02)
	for i in range(2):
		box("roof_mid" if i == 0 else "wood_warm", Vector3(1.22, .30 + i * .13, -.86), Vector3(.35, .11, .27))
		box("plaster", Vector3(1.22, .31 + i * .13, -1.001), Vector3(.30, .065, .012))
	# Small copper instrument on a stone pedestal, with crossed open measuring rings.
	box("stone_dark", Vector3(-1.27, .38, -.78), Vector3(.36, .40, .36))
	box("stone_light", Vector3(-1.27, .61, -.78), Vector3(.43, .07, .43))
	cylinder("gold", Vector3(-1.27, .81, -.78), .045, .08, .34)
	open_wheel(Vector3(-1.27, 1.08, -.78), .27, .028, Basis(Vector3.RIGHT, PI * .39), "gold")
	open_wheel(Vector3(-1.27, 1.08, -.78), .24, .025, Basis(Vector3.FORWARD, PI * .34), "gold")
	sphere("window", Vector3(-1.27, 1.08, -.78), .10)
