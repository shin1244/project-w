@tool
extends MeshInstance3D

# Four shared silhouettes per species, two surfaces per tree. Selection and
# resource behavior stay on Tree.tscn; this node only supplies the visual mesh.
@export_enum("Oak", "Pine", "Birch") var species: int = 0
static var _meshes: Dictionary = {}
static var _materials: Dictionary = {}

func _ready() -> void:
	# ResourceManager sets this deterministic rotation before entering the tree.
	var variant := posmod(int(get_parent().rotation.y * 137.0), 4)
	var key := species * 4 + variant
	if not _meshes.has(key):
		_meshes[key] = _sculpt(species, variant)
	mesh = _meshes[key]

static func _material(bark: bool, birch: bool) -> ShaderMaterial:
	var key := int(bark) + int(birch) * 2
	if not _materials.has(key):
		var material := ShaderMaterial.new()
		material.shader = preload("res://resources/trees/Foliage.gdshader")
		material.set_shader_parameter("albedo", Color.WHITE)
		material.set_shader_parameter("tree_variation", 0.065 if bark else 0.10)
		material.set_shader_parameter("shade_strength", 0.10 if bark else 0.17)
		material.set_shader_parameter("mottle", 0.045 if bark else 0.07)
		material.set_shader_parameter("bark", bark)
		material.set_shader_parameter("birch", birch)
		_materials[key] = material
	return _materials[key]

static func _sculpt(kind: int, variant: int) -> ArrayMesh:
	var rng := RandomNumberGenerator.new()
	rng.seed = 7283 + kind * 1171 + variant * 4079
	var wood := SurfaceTool.new()
	var leaf := SurfaceTool.new()
	wood.begin(Mesh.PRIMITIVE_TRIANGLES)
	leaf.begin(Mesh.PRIMITIVE_TRIANGLES)
	var lean := Vector3(rng.randf_range(-0.13, 0.13), 0.0, rng.randf_range(-0.10, 0.10))
	match kind:
		0: _oak(wood, leaf, rng, lean)
		1: _pine(wood, leaf, rng, lean)
		2: _birch(wood, leaf, rng, lean)
	wood.set_material(_material(true, kind == 2))
	leaf.set_material(_material(false, false))
	var result := wood.commit()
	leaf.commit(result)
	return result

static func _oak(wood: SurfaceTool, leaf: SurfaceTool, rng: RandomNumberGenerator, lean: Vector3) -> void:
	var bark_color := Color(0.29, 0.235, 0.16)
	_bough(wood, Vector3.ZERO, Vector3(0.04, 1.45, 0.03) + lean, 0.29, 0.19, bark_color, rng)
	_bough(wood, Vector3(0.04, 1.35, 0.03) + lean, Vector3(-0.17, 3.05, 0.04) + lean, 0.20, 0.065, bark_color, rng)
	_roots(wood, 0.29, bark_color, rng)
	# Broad offset shelves with clipped uneven edges instead of spherical lobes.
	var crowns := [
		[Vector3(-0.74, 2.43, -0.12), Vector3(0.91, 0.86, 0.82), Color(0.27, 0.365, 0.18)],
		[Vector3(0.71, 2.67, 0.05), Vector3(0.85, 0.91, 0.87), Color(0.32, 0.405, 0.205)],
		[Vector3(0.18, 2.52, -0.66), Vector3(0.82, 0.74, 0.78), Color(0.295, 0.39, 0.20)],
		[Vector3(-0.17, 2.81, 0.60), Vector3(0.84, 0.82, 0.79), Color(0.32, 0.415, 0.23)],
		[Vector3(-0.35, 3.24, -0.05), Vector3(0.94, 0.97, 0.82), Color(0.365, 0.455, 0.255)],
		[Vector3(0.43, 3.25, 0.21), Vector3(0.65, 0.81, 0.69), Color(0.38, 0.465, 0.255)],
	]
	for i in crowns.size():
		var center: Vector3 = crowns[i][0] + lean + Vector3(rng.randf_range(-0.13, 0.13), rng.randf_range(-0.12, 0.12), rng.randf_range(-0.12, 0.12))
		if i < 4:
			_bough(wood, Vector3(0.0, 1.40 + i * 0.11, 0.0) + lean, center + Vector3(0, -0.15, 0), 0.12, 0.035, bark_color, rng)
		_crown(leaf, center, crowns[i][1], crowns[i][2], rng, 9)

static func _birch(wood: SurfaceTool, leaf: SurfaceTool, rng: RandomNumberGenerator, lean: Vector3) -> void:
	var bark_color := Color(0.72, 0.725, 0.635)
	_bough(wood, Vector3.ZERO, Vector3(0.03, 1.85, 0.0) + lean, 0.205, 0.115, bark_color, rng)
	_bough(wood, Vector3(0.03, 1.75, 0.0) + lean, Vector3(-0.23, 3.50, 0.04) + lean, 0.12, 0.035, bark_color, rng)
	_bough(wood, Vector3(0.0, 1.10, 0.0), Vector3(0.50, 3.12, -0.06) + lean, 0.105, 0.025, bark_color, rng)
	_roots(wood, 0.18, bark_color, rng)
	# Narrow split crowns leave glimpses of the pale forked trunk.
	var crowns := [
		[Vector3(-0.53, 2.71, -0.08), Vector3(0.62, 0.91, 0.54), Color(0.405, 0.495, 0.285)],
		[Vector3(0.53, 2.79, -0.18), Vector3(0.55, 0.90, 0.54), Color(0.445, 0.53, 0.325)],
		[Vector3(-0.19, 3.48, 0.09), Vector3(0.65, 1.02, 0.56), Color(0.48, 0.555, 0.36)],
		[Vector3(0.43, 3.36, -0.02), Vector3(0.47, 0.76, 0.45), Color(0.49, 0.56, 0.36)],
		[Vector3(0.10, 2.90, 0.51), Vector3(0.51, 0.73, 0.46), Color(0.405, 0.50, 0.29)],
	]
	for i in crowns.size():
		var center: Vector3 = crowns[i][0] + lean + Vector3(rng.randf_range(-0.10, 0.10), rng.randf_range(-0.10, 0.10), rng.randf_range(-0.08, 0.08))
		_bough(wood, Vector3(0.0, 1.85 + i * 0.17, 0.0) + lean, center, 0.058, 0.016, bark_color, rng)
		_crown(leaf, center, crowns[i][1], crowns[i][2], rng, 8)

static func _pine(wood: SurfaceTool, leaf: SurfaceTool, rng: RandomNumberGenerator, lean: Vector3) -> void:
	var bark_color := Color(0.275, 0.22, 0.16)
	_bough(wood, Vector3.ZERO, Vector3(0.0, 4.05, 0.0) + lean, 0.23, 0.035, bark_color, rng)
	_roots(wood, 0.23, bark_color, rng)
	# Long tips and deep notches form branch whorls instead of smooth cones.
	var tiers := [Vector3(1.52, 1.14, 0.95), Vector3(2.13, 0.99, 1.0), Vector3(2.73, 0.78, 0.94), Vector3(3.32, 0.56, 0.86), Vector3(3.82, 0.32, 0.71)]
	for i in tiers.size():
		var tier: Vector3 = tiers[i]
		var center := Vector3(lean.x * tier.x / 4.0, tier.x, lean.z * tier.x / 4.0)
		var color := Color(0.205, 0.335, 0.245).lerp(Color(0.315, 0.43, 0.315), float(i) / 5.0)
		_pine_tier(leaf, center, tier.y, tier.z, i * 0.48 + rng.randf_range(-0.12, 0.12), color, rng)
		if i < 3:
			for j in 3:
				var angle := j * TAU / 3.0 + i * 0.75
				_bough(wood, center + Vector3(0, 0.08, 0), center + Vector3(cos(angle), -0.17, sin(angle)) * tier.y * 0.76, 0.04, 0.012, bark_color, rng)

static func _roots(surface: SurfaceTool, radius: float, color: Color, rng: RandomNumberGenerator) -> void:
	for i in 4:
		var angle := i * TAU / 4.0 + rng.randf_range(-0.22, 0.22)
		_bough(surface, Vector3(0.0, 0.34, 0.0), Vector3(cos(angle) * radius * 1.85, 0.045, sin(angle) * radius * 1.85), radius * 0.40, 0.022, color, rng)

static func _bough(surface: SurfaceTool, start: Vector3, finish: Vector3, base: float, tip: float, color: Color, rng: RandomNumberGenerator) -> void:
	var axis := (finish - start).normalized()
	var tangent := axis.cross(Vector3.FORWARD).normalized()
	if tangent.length_squared() < 0.01:
		tangent = axis.cross(Vector3.RIGHT).normalized()
	var bitangent := tangent.cross(axis).normalized()
	var rings: Array[PackedVector3Array] = []
	var segments := 6
	var phase := rng.randf() * TAU
	for r in 3:
		var t := float(r) / 2.0
		var center := start.lerp(finish, t)
		if r == 1:
			center += tangent * base * 0.18
		var radius := lerpf(base, tip, t)
		var ring := PackedVector3Array()
		for i in segments:
			var angle := float(i) / segments * TAU + phase
			ring.append(center + (tangent * cos(angle) + bitangent * sin(angle)) * radius)
		rings.append(ring)
	for r in 2:
		for i in segments:
			var j := (i + 1) % segments
			var tint := color * (0.93 + 0.10 * float(i % 3) / 2.0)
			_quad(surface, rings[r][i], rings[r][j], rings[r + 1][j], rings[r + 1][i], tint)
	for i in segments:
		_triangle(surface, finish, rings[2][i], rings[2][(i + 1) % segments], color)

static func _crown(surface: SurfaceTool, center: Vector3, size: Vector3, color: Color, rng: RandomNumberGenerator, segments: int) -> void:
	var rings: Array[PackedVector3Array] = []
	var phase := rng.randf() * TAU
	var asymmetry := Vector3(rng.randf_range(-0.18, 0.18), 0, rng.randf_range(-0.18, 0.18))
	var widths := [0.48, 1.0, 0.81, 0.22]
	var heights := [-0.48, -0.16, 0.23, 0.52]
	var edge_shape := PackedFloat32Array()
	for i in segments:
		edge_shape.append(rng.randf_range(0.82, 1.16))
	for r in 4:
		var ring := PackedVector3Array()
		for i in segments:
			var angle := float(i) / segments * TAU + phase + (0.09 if r == 2 else 0.0)
			var width: float = widths[r] * edge_shape[i]
			var p := Vector3(cos(angle) * width, heights[r] + rng.randf_range(-0.09, 0.09), sin(angle) * width)
			p += asymmetry * float(r) / 3.0
			ring.append(center + p * size)
		rings.append(ring)
	for r in 3:
		for i in segments:
			var j := (i + 1) % segments
			var tint := color * (0.93 + rng.randf() * 0.11 + r * 0.015)
			_quad(surface, rings[r][i], rings[r][j], rings[r + 1][j], rings[r + 1][i], tint)
	for i in segments:
		var j := (i + 1) % segments
		_triangle(surface, center + Vector3(0, -size.y * 0.51, 0), rings[0][j], rings[0][i], color * 0.89)
		_triangle(surface, center + asymmetry * size + Vector3(0, size.y * 0.56, 0), rings[3][i], rings[3][j], color * 1.04)

static func _pine_tier(surface: SurfaceTool, center: Vector3, radius: float, height: float, phase: float, color: Color, rng: RandomNumberGenerator) -> void:
	var rings: Array[PackedVector3Array] = []
	var segments := 16
	for r in 3:
		var ring := PackedVector3Array()
		for i in segments:
			var angle := float(i) / segments * TAU + phase
			var tip := i % 2 == 0
			var width := radius * (1.0 if tip else 0.60) * rng.randf_range(0.90, 1.10)
			var elevation := -0.22 if tip else -0.04
			if r == 1:
				width *= 0.66
				elevation = height * (0.30 if tip else 0.39)
			elif r == 2:
				width = radius * 0.10
				elevation = height * 0.83
			ring.append(center + Vector3(cos(angle) * width, elevation + rng.randf_range(-0.035, 0.035), sin(angle) * width))
		rings.append(ring)
	for r in 2:
		for i in segments:
			var j := (i + 1) % segments
			var tint := color * (0.94 + (i % 3) * 0.035 + r * 0.035)
			_quad(surface, rings[r][i], rings[r][j], rings[r + 1][j], rings[r + 1][i], tint)
	for i in segments:
		var j := (i + 1) % segments
		_triangle(surface, center + Vector3(0, 0.02, 0), rings[0][j], rings[0][i], color * 0.82)
		_triangle(surface, center + Vector3(0, height, 0), rings[2][i], rings[2][j], color * 1.03)

static func _quad(surface: SurfaceTool, a: Vector3, b: Vector3, c: Vector3, d: Vector3, color: Color) -> void:
	_triangle(surface, a, b, c, color)
	_triangle(surface, a, c, d, color)

static func _triangle(surface: SurfaceTool, a: Vector3, b: Vector3, c: Vector3, color: Color) -> void:
	# Godot front faces wind clockwise; all rings share this orientation.
	var normal := (c - a).cross(b - a).normalized()
	surface.set_normal(normal)
	# Palette constants are authored as sRGB, whereas shader vertex COLOR is
	# linear. Convert exactly once when baking the shared mesh, not per pixel.
	surface.set_color(color.srgb_to_linear())
	surface.add_vertex(a)
	surface.add_vertex(b)
	surface.add_vertex(c)
