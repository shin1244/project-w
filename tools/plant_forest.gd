extends SceneTree
## Author the forests, clearings, lanes, town-hall meadows, and two expansion bays.
## Run: Godot --headless --path . --script tools/plant_forest.gd
const TREE_SIZE := 2

func _initialize() -> void:
	var path := "res://maps/test.json"
	var data: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(path))
	var layout = load("res://maps/TerrainBuilder.gd").new()
	# Fixed bounds include both expansions. Repeated runs must not keep padding rows.
	var rows: Array = []
	for z in range(layout.MAP_SIZE.y):
		rows.append("#".repeat(layout.MAP_SIZE.x))
	var cell_size: float = data["cellSize"]
	var origin: Vector2 = layout.MAP_ORIGIN
	data["originX"] = origin.x
	data["originZ"] = origin.y
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
	var width: int = rows[0].length()
	var height: int = rows.size()
	var cell_center := func(x: int, z: int) -> Vector2:
		return origin + (Vector2(x, z) + Vector2.ONE * 0.5) * cell_size
	var fits := func(x: int, z: int, outer: bool) -> bool:
		for dz in range(TREE_SIZE):
			for dx in range(TREE_SIZE):
				var p: Vector2 = cell_center.call(x + dx, z + dz)
				if rows[z + dz][x + dx] != "." or occupied.has(Vector2i(x + dx, z + dz)) \
					or (not layout.is_outer_forest(p) if outer else layout.outside_lanes(p)):
					return false
		return true
	var plant := func(x: int, z: int) -> void:
		var row: String = rows[z]
		row[x] = "T"
		rows[z] = row
		for dz in range(TREE_SIZE):
			for dx in range(TREE_SIZE):
				occupied[Vector2i(x + dx, z + dz)] = true
	# The interior keeps its even-grid anchors, so existing interior tree IDs stay stable.
	for z in range(0, height - TREE_SIZE + 1, TREE_SIZE):
		for x in range(0, width - TREE_SIZE + 1, TREE_SIZE):
			if fits.call(x, z, false):
				plant.call(x, z)
				trees += 1
	# Lane edges fill from the wall inward, so leftover gaps face the lane instead of
	# forming a hidden path behind the trees. Half-turn pairs keep both teams equal.
	var wall_distance := {}
	var queue: Array[Vector2i] = []
	for z in range(height):
		for x in range(width):
			if rows[z][x] == "W":
				wall_distance[Vector2i(x, z)] = 0
				queue.append(Vector2i(x, z))
	var head := 0
	while head < queue.size():
		var cell: Vector2i = queue[head]
		head += 1
		for step: Vector2i in [Vector2i.LEFT, Vector2i.RIGHT, Vector2i.UP, Vector2i.DOWN]:
			var next: Vector2i = cell + step
			if next.x < 0 or next.y < 0 or next.x >= width or next.y >= height \
				or rows[next.y][next.x] == "#" or wall_distance.has(next):
				continue
			wall_distance[next] = wall_distance[cell] + 1
			queue.append(next)
	var candidates: Array[Vector3i] = []
	for z in range(height - TREE_SIZE + 1):
		for x in range(width - TREE_SIZE + 1):
			if fits.call(x, z, true):
				var nearest := 1 << 20
				for dz in range(TREE_SIZE):
					for dx in range(TREE_SIZE):
						nearest = mini(nearest, wall_distance.get(Vector2i(x + dx, z + dz), 1 << 20))
				candidates.append(Vector3i(nearest, z, x))
	candidates.sort()
	for candidate in candidates:
		var x := candidate.z
		var z := candidate.y
		var mx := width - TREE_SIZE - x
		var mz := height - TREE_SIZE - z
		if absi(mx - x) < TREE_SIZE and absi(mz - z) < TREE_SIZE:
			continue # A footprint overlapping its own half-turn cannot be paired.
		if fits.call(x, z, true) and fits.call(mx, mz, true):
			plant.call(x, z)
			plant.call(mx, mz)
			trees += 2
	# Tiny gaps that cannot fit a complete tree stay grass so they read as forest floor
	# instead of stray road tiles. No building footprint fits between the trees.
	# Both teams have the same roads, starting lumber, and empty construction space.
	for z in range(rows.size()):
		for x in range(rows[z].length()):
			var mx: int = rows[z].length() - 1 - x
			var mz: int = rows.size() - 1 - z
			assert((rows[z][x] == "R") == (rows[mz][mx] == "R"))
			assert((rows[z][x] == "W") == (rows[mz][mx] == "W"))
			assert(occupied.has(Vector2i(x, z)) == occupied.has(Vector2i(mx, mz)))
	data["rows"] = rows
	data["activityAreas"] = layout.activity_areas()
	data["expansionSites"] = layout.expansion_sites()
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
	print("Map planted: ", trees, " trees, ", roads, " road cells, ", walls, " wall cells; two expansion bays and four jungle clearings; rotational symmetry verified.")
	quit()
