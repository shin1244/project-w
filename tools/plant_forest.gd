extends SceneTree
## Author the six forest groves, buildable grass, and permanent stone roads together.
## Run: Godot --headless --path . --script tools/plant_forest.gd
const TREE_SIZE := 2

func _initialize() -> void:
	var path := "res://maps/test.json"
	var data: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(path))
	var layout = load("res://maps/TerrainBuilder.gd").new()
	var rows: Array = data["rows"]
	var cell_size: float = data["cellSize"]
	var origin := Vector2(data["originX"], data["originZ"])
	# Rebuild the silhouette from fixed world coordinates, so reruns never erode it.
	var outline: Array = []
	for z in range(rows.size()):
		var row := ""
		for x in range(rows[0].length()):
			var p := origin + (Vector2(x, z) + Vector2.ONE * 0.5) * cell_size
			row += "." if layout.is_arena_land(p) else "#"
		outline.append(row)
	var roads := 0
	var walls := 0
	for z in range(rows.size()):
		var row: String = outline[z]
		for x in range(row.length()):
			if row[x] == "#":
				continue
			var p := origin + (Vector2(x, z) + Vector2.ONE * 0.5) * cell_size
			# A continuous, two-cell stone boundary replaces the exterior tree belt.
			var perimeter := false
			for dz in range(-layout.WALL_THICKNESS, layout.WALL_THICKNESS + 1):
				for dx in range(-layout.WALL_THICKNESS, layout.WALL_THICKNESS + 1):
					var cx := x + dx
					var cz := z + dz
					if cx < 0 or cz < 0 or cz >= outline.size() or cx >= row.length() or outline[cz][cx] == "#":
						perimeter = true
			row[x] = "W" if perimeter else ("R" if layout.is_road(p) else ".")
			if row[x] == "R":
				roads += 1
			elif row[x] == "W":
				walls += 1
		rows[z] = row
	var trees := 0
	var occupied := {}
	for z in range(0, rows.size() - TREE_SIZE + 1, TREE_SIZE):
		for x in range(0, rows[0].length() - TREE_SIZE + 1, TREE_SIZE):
			var fits := true
			for dz in range(TREE_SIZE):
				for dx in range(TREE_SIZE):
					var p := origin + (Vector2(x + dx, z + dz) + Vector2.ONE * 0.5) * cell_size
					fits = fits and rows[z + dz][x + dx] == "." and layout.is_initial_forest(p)
			if not fits:
				continue
			var row: String = rows[z]
			row[x] = "T"
			rows[z] = row
			trees += 1
			for dz in range(TREE_SIZE):
				for dx in range(TREE_SIZE):
					occupied[Vector2i(x + dx, z + dz)] = true
	# Both teams have the same roads, starting lumber, and empty construction space.
	for z in range(rows.size()):
		for x in range(rows[z].length()):
			var mx: int = rows[z].length() - 1 - x
			var mz: int = rows.size() - 1 - z
			assert((rows[z][x] == "R") == (rows[z][mx] == "R"))
			assert((rows[z][x] == "R") == (rows[mz][x] == "R"))
			assert((rows[z][x] == "W") == (rows[z][mx] == "W"))
			assert((rows[z][x] == "W") == (rows[mz][x] == "W"))
			assert(occupied.has(Vector2i(x, z)) == occupied.has(Vector2i(mx, z)))
			assert(occupied.has(Vector2i(x, z)) == occupied.has(Vector2i(x, mz)))
	data["rows"] = rows
	data["treeSize"] = TREE_SIZE
	data["treeAmount"] = int(data["treeAmount"])
	for tower in data.get("towers", []):
		tower["side"] = int(tower["side"])
		var old: Array = tower["pos"]
		var anchor: Vector2 = layout.TOP_LANE[2] if absf(old[0]) > 35.0 else layout.TOP_LANE[3]
		tower["pos"] = [absf(anchor.x) * signf(old[0]), absf(anchor.y) * signf(old[1])]
	var file := FileAccess.open(path, FileAccess.WRITE)
	assert(file != null)
	file.store_string(JSON.stringify(data, "  ") + "\n")
	file.close()
	print("Map planted: ", trees, " trees, ", roads, " road cells, ", walls, " wall cells; centered towers and six inner groves; X/Z symmetry verified.")
	quit()
