shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;

// Analytic road masks preserve the existing layout without vertex-color stair steps.
// All surface detail uses map coordinates: zooming never magnifies a low-res bitmap.
uniform vec2 top_lane[9];
uniform vec4 trails[7];
uniform float lane_width = 8.0;
uniform float base_x = 65.0;
uniform vec4 grass_dark : source_color = vec4(0.20, 0.27, 0.12, 1.0);
uniform vec4 grass_light : source_color = vec4(0.36, 0.42, 0.21, 1.0);
uniform vec4 dirt_dark : source_color = vec4(0.33, 0.27, 0.18, 1.0);
uniform vec4 dirt_light : source_color = vec4(0.51, 0.44, 0.32, 1.0);
uniform vec4 stone_color : source_color = vec4(0.48, 0.47, 0.38, 1.0);
uniform float bump_strength : hint_range(0.0, 1.0) = 0.32;

varying vec2 map_position;

float hash(vec2 p) {
	vec3 q = fract(vec3(p.xyx) * 0.1031);
	q += dot(q, q.yzx + 33.33);
	return fract((q.x + q.y) * q.z);
}

float noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x),
		mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0)), u.x), u.y);
}

float segment_distance(vec2 p, vec2 a, vec2 b) {
	vec2 ab = b - a;
	return length(p - a - ab * clamp(dot(p - a, ab) / dot(ab, ab), 0.0, 1.0));
}

// Fade sub-pixel detail instead of letting it shimmer when zoomed out.
float detail_visibility(vec2 p) {
	return 1.0 - smoothstep(0.35, 1.1, max(length(dFdx(p)), length(dFdy(p))));
}

void vertex() {
	map_position = VERTEX.xz;
}

void fragment() {
	vec2 p = map_position;
	vec2 q = abs(p);
	vec2 rotated = vec2(p.x * 0.8 + p.y * 0.6, -p.x * 0.6 + p.y * 0.8);
	float macro = noise(p * 0.16);
	vec2 warp = vec2(noise(p * 0.31), noise(rotated * 0.27 + 11.0));
	float patches = noise(rotated * 0.72 + warp + vec2(17.4, 5.2));
	float soil = noise(rotated * 3.1 + warp * 2.0) * 0.6 + noise(p * 4.7 + 10.2) * 0.4;
	float fine = mix(0.5, noise(rotated * 18.0), detail_visibility(rotated * 18.0));

	float lane = 1000.0;
	vec2 lane_point = vec2(p.x, -abs(p.y));
	for (int i = 0; i < 8; i++) {
		lane = min(lane, segment_distance(lane_point, top_lane[i], top_lane[i + 1]));
	}
	float trail = 1000.0;
	for (int i = 0; i < 7; i++) {
		trail = min(trail, segment_distance(q, trails[i].xy, trails[i].zw));
	}
	float edge_grain = (noise(p * 4.0) - 0.5) * 0.18;
	float road = 1.0 - smoothstep(lane_width * 0.5 - 0.6, lane_width * 0.5 + 1.2, lane + edge_grain);
	float footpath = 1.0 - smoothstep(1.4, 2.7, trail + edge_grain);
	float base_distance = length(q - vec2(base_x, 0.0));
	float center_distance = length(p);
	float base = 1.0 - smoothstep(10.95, 11.05, base_distance);
	float center = 1.0 - smoothstep(7.55, 7.65, center_distance);
	float clearing = max(base, center);
	float dirt_mask = max(max(road, footpath), clearing);

	// Fine, variably oriented grass fibers, plus broad soil patches underneath.
	vec2 blade_uv = rotated * 8.0 + warp * 3.0;
	vec2 blade_cell = floor(blade_uv);
	vec2 blade_local = fract(blade_uv) - 0.5;
	float angle = hash(blade_cell + 21.0) * 6.283185;
	vec2 direction = vec2(cos(angle), sin(angle));
	float along = dot(blade_local, direction);
	float across = dot(blade_local, vec2(-direction.y, direction.x));
	float blade_dist = length(vec2(across * 4.0, along));
	float blade_aa = max(fwidth(blade_dist), 0.02);
	float blade = (1.0 - smoothstep(0.29 - blade_aa, 0.29 + blade_aa, blade_dist)) * detail_visibility(blade_uv);
	vec3 grass = mix(grass_dark.rgb, grass_light.rgb, clamp(macro * 0.55 + patches * 0.25 + soil * 0.2, 0.0, 1.0));
	grass *= 0.80 + soil * 0.28 + fine * 0.12 + blade * 0.18;
	grass = mix(grass, dirt_dark.rgb * 0.82, smoothstep(0.60, 0.80, patches) * 0.40);

	// Small embedded stones. Each cell has a random offset and aspect ratio.
	vec2 pebble_uv = rotated * 3.2 + warp;
	vec2 pebble_cell = floor(pebble_uv);
	float pebble_random = hash(pebble_cell + 7.0);
	vec2 pebble_offset = vec2(hash(pebble_cell + 3.0), hash(pebble_cell + 9.0));
	vec2 pebble_local = fract(pebble_uv) - (0.15 + pebble_offset * 0.7);
	float pebble_dist = length(pebble_local * vec2(1.0, 1.2 + pebble_random));
	float pebble_radius = 0.06 + pebble_random * 0.075;
	float pebble_aa = max(fwidth(pebble_dist), 0.015);
	float pebble = (1.0 - smoothstep(pebble_radius - pebble_aa, pebble_radius + pebble_aa, pebble_dist))
		* step(0.55, pebble_random) * detail_visibility(pebble_uv);
	vec3 dirt = mix(dirt_dark.rgb, dirt_light.rgb, 0.28 + macro * 0.42 + soil * 0.20);
	dirt *= 0.87 + fine * 0.22;
	dirt = mix(dirt, stone_color.rgb * (0.80 + pebble_random * 0.35), pebble * 0.70);
	vec3 color = mix(grass, dirt, dirt_mask);

	// Existing base/center rings become worn stone borders, retaining their radii.
	float base_ring = smoothstep(9.65, 9.75, base_distance) * base;
	float center_ring = smoothstep(6.75, 6.85, center_distance) * center;
	float ring = max(base_ring, center_ring);
	vec2 ring_point = base > center ? vec2(abs(p.x) - base_x, p.y) : p;
	float sectors = base > center ? 48.0 : 36.0;
	float sector = (atan(ring_point.y, ring_point.x) / 6.283185 + 0.5) * sectors;
	float joint_distance = min(fract(sector), 1.0 - fract(sector));
	float joint_aa = min(fwidth(sector), 0.08);
	float joint = 1.0 - smoothstep(0.018, 0.035 + joint_aa, joint_distance);
	float slab_variation = hash(vec2(floor(sector), sectors));
	vec3 stone = stone_color.rgb * (0.85 + slab_variation * 0.20 + soil * 0.12);
	stone *= 1.0 - joint * 0.30;
	color = mix(color, stone, ring);

	// Screen derivatives perturb lighting only. The flat mesh/collision stays intact.
	float height = mix(soil * 0.025 + blade * 0.012, soil * 0.012 + pebble * 0.025, dirt_mask);
	height = mix(height, soil * 0.012 - joint * 0.014, ring);
	vec3 n = normalize(NORMAL);
	vec3 dx = dFdx(VERTEX);
	vec3 dy = dFdy(VERTEX);
	vec3 r1 = cross(dy, n);
	vec3 r2 = cross(n, dx);
	float det = dot(dx, r1);
	vec3 gradient = (r1 * dFdx(height) + r2 * dFdy(height)) * sign(det) / max(abs(det), 0.000001);
	NORMAL = normalize(n - gradient * bump_strength);
	ALBEDO = color;
	ROUGHNESS = mix(0.97, 0.87, max(dirt_mask * 0.45, ring));
	SPECULAR = 0.22;
}
