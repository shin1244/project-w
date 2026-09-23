extends SceneTree
const TREE_SIZE := 2
## Fill all land except lanes, trails, bases, and the center clearing.
## Run after editing the road layout: Godot --headless --path . --script tools/plant_forest.gd

func _initialize() -> void:
	var path := "res://maps/test.json"
	var data: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(path))
	var layout = load("res://maps/TerrainBuilder.gd").new()
	var rows: Array = data["rows"]
	var cell_size: float = data["cellSize"]
	# Keep the whole blocking cell off the painted road, not just its center.
	var clearance := cell_size * sqrt(2.0) * 0.5
	var trees := 0
	for z in range(rows.size()):
		var row: String = rows[z]
		for x in range(row.length()):
			if row[x] == "#":
				continue
			var p := Vector2(data["originX"] + (x + 0.5) * cell_size, data["originZ"] + (z + 0.5) * cell_size)
			var open: bool = layout.lane_distance(p) <= layout.LANE_WIDTH * 0.5 + 1.2 + clearance \
				or layout.trail_distance(p) <= 2.7 + clearance \
				or p.abs().distance_to(Vector2(layout.BASE_X, 0)) <= 11.0 + clearance \
				or p.length() <= 7.6 + clearance
			row[x] = "." if open else "T"
			if not open:
				trees += 1
		rows[z] = row
	# A T marks the minimum-X/minimum-Z anchor of one complete 2x2 footprint.
	# All four cells must be forest candidates, so no tree blocks a road or void.
	var candidates: Array = rows.duplicate()
	for z in range(rows.size()):
		rows[z] = rows[z].replace("T", ".")
	trees = 0
	for z in range(0, rows.size() - TREE_SIZE + 1, TREE_SIZE):
		for x in range(0, rows[0].length() - TREE_SIZE + 1, TREE_SIZE):
			var fits := true
			for dz in range(TREE_SIZE):
				for dx in range(TREE_SIZE):
					fits = fits and candidates[z + dz][x + dx] == "T"
			if fits:
				var row: String = rows[z]
				row[x] = "T"
				rows[z] = row
				trees += 1
	# Both teams get exactly the same forest footprint.
	var occupied := {}
	for z in range(rows.size()):
		for x in range(rows[z].length()):
			if rows[z][x] == "T":
				for dz in range(TREE_SIZE):
					for dx in range(TREE_SIZE):
						occupied[Vector2i(x + dx, z + dz)] = true
	for z in range(rows.size()):
		for x in range(rows[z].length()):
			assert(occupied.has(Vector2i(x, z)) == occupied.has(Vector2i(rows[z].length() - 1 - x, z)))
			assert(occupied.has(Vector2i(x, z)) == occupied.has(Vector2i(x, rows.size() - 1 - z)))
	data["rows"] = rows
	data["treeSize"] = TREE_SIZE
	# JSON.parse_string reads numbers as floats; resource amount is an integer in Go/C#.
	data["treeAmount"] = int(data["treeAmount"])
	var file := FileAccess.open(path, FileAccess.WRITE)
	assert(file != null)
	file.store_string(JSON.stringify(data, "  ") + "\n")
	file.close()
	print("Forest planted: ", trees, " trees; roads, bases, and center kept clear; X/Z symmetry verified.")
	quit()
