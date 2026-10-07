shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;

// Visual detail only: the authoritative R-cell mask still defines roads/building space.
uniform sampler2D road_mask : filter_linear, repeat_disable;
uniform vec2 map_origin;
uniform vec2 map_size;
uniform vec4 grass_dark : source_color = vec4(0.20, 0.30, 0.15, 1.0);
uniform vec4 grass_light : source_color = vec4(0.36, 0.45, 0.24, 1.0);
uniform vec4 grass_dry : source_color = vec4(0.43, 0.43, 0.27, 1.0);
uniform vec4 stone_dark : source_color = vec4(0.42, 0.44, 0.42, 1.0);
uniform vec4 stone_light : source_color = vec4(0.57, 0.58, 0.54, 1.0);
uniform vec4 curb_color : source_color = vec4(0.54, 0.55, 0.49, 1.0);
uniform vec4 dust_color : source_color = vec4(0.50, 0.47, 0.39, 1.0);
uniform float dusty_ratio : hint_range(0.0, 0.5) = 0.18;
uniform float cracked_ratio : hint_range(0.0, 0.5) = 0.065;
uniform float bump_strength : hint_range(0.0, 1.0) = 0.30;

varying vec2 map_position;

float hash(vec2 p) {
	vec3 q = fract(vec3(p.xyx) * 0.1031);
	q += dot(q, q.yzx + 33.33);
	return fract((q.x + q.y) * q.z);
}
float noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x),
		mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0)), u.x), u.y);
}
float detail_visibility(vec2 p) {
	return 1.0 - smoothstep(0.32, 1.0, max(length(dFdx(p)), length(dFdy(p))));
}
// Neighboring stones share the same perturbed boundary: no stretched/random decals.
float row_edge(float row) {
	return row + (hash(vec2(row, 23.7)) - 0.5) * 0.22;
}
float stone_edge(float column, float row) {
	return column + (hash(vec2(column, row)) - 0.5) * 0.32;
}
void vertex() {
	map_position = VERTEX.xz;
}
void fragment() {
	vec2 p = map_position;
	float macro = noise(p * 0.13);
	float patches = noise(p * 0.57 + vec2(17.4, 5.2));
	float grain = noise(p * 3.4) * 0.65 + noise(p * 7.8 + 10.2) * 0.35;
	float fine = mix(0.5, noise(p * 19.0), detail_visibility(p * 19.0));
	float mask = texture(road_mask, (p - map_origin) / map_size).r;
	float road = smoothstep(0.46, 0.54, mask);

	// Broad meadow color patches, quiet close-range blades and scattered clover.
	vec3 grass = mix(grass_dark.rgb, grass_light.rgb, macro * 0.38 + patches * 0.42 + grain * 0.20);
	float dry = smoothstep(0.55, 0.86, noise(p * 0.25 + vec2(3.1, 7.7)));
	grass = mix(grass, grass_dry.rgb, dry * 0.30);
	vec2 blade_uv = p * 8.0;
	vec2 blade_local = fract(blade_uv) - 0.5;
	float angle = hash(floor(blade_uv) + 21.0) * 6.283185;
	vec2 direction = vec2(cos(angle), sin(angle));
	float blade_dist = length(vec2(dot(blade_local, vec2(-direction.y, direction.x)) * 3.5, dot(blade_local, direction)));
	float blade_aa = max(fwidth(blade_dist), 0.02);
	float blade = (1.0 - smoothstep(0.28 - blade_aa, 0.28 + blade_aa, blade_dist)) * detail_visibility(blade_uv);
	grass *= 0.91 + grain * 0.13 + fine * 0.035 + blade * 0.085;
	float clover = smoothstep(0.74, 0.87, noise(p * 5.0 + 7.0)) * detail_visibility(p * 5.0);
	grass = mix(grass, grass_light.rgb * 1.08, clover * 0.25);

	// Laid stone courses retain a readable direction but vary in both width and height.
	float y = p.y / 0.64;
	float row = floor(y);
	if (y < row_edge(row)) row -= 1.0;
	else if (y > row_edge(row + 1.0)) row += 1.0;
	float bottom = row_edge(row);
	float top = row_edge(row + 1.0);
	float width = mix(0.94, 1.20, hash(vec2(row, 8.1)));
	float x = p.x / width + mod(row, 2.0) * 0.5 + (hash(vec2(row, 3.4)) - 0.5) * 0.16;
	float column = floor(x);
	if (x < stone_edge(column, row)) column -= 1.0;
	else if (x > stone_edge(column + 1.0, row)) column += 1.0;
	float left = stone_edge(column, row);
	float right = stone_edge(column + 1.0, row);
	vec2 stone_id = vec2(column, row);
	vec2 half_size = vec2((right - left) * width, (top - bottom) * 0.64) * 0.5;
	vec2 local = vec2((x - (left + right) * 0.5) * width, (y - (bottom + top) * 0.5) * 0.64);
	float rounding = mix(0.028, 0.065, hash(stone_id + 19.3));
	vec2 box = abs(local) - half_size + rounding;
	float edge_distance = length(max(box, vec2(0.0))) + min(max(box.x, box.y), 0.0) - rounding;
	// Small edge nicks stop perfectly ruled courses without bending the entire tile.
	edge_distance += (noise(p * 12.0) - 0.5) * 0.013 * detail_visibility(p * 12.0);
	float aa = max(fwidth(edge_distance), 0.004);
	float joint = smoothstep(-0.019 - aa, -0.007 + aa, edge_distance);
	float bevel = smoothstep(-0.062, -0.016, edge_distance) * (1.0 - joint);
	float visibility = detail_visibility(p / vec2(1.0, 0.64));
	joint *= visibility;
	bevel *= visibility;

	// Neighboring slabs share their patina; individual differences remain restrained.
	float batch = noise(p * 0.22 + 2.0);
	float variation = hash(stone_id + 7.2);
	vec3 stone = mix(stone_dark.rgb, stone_light.rgb, 0.37 + variation * 0.20 + batch * 0.20);
	float warmth = noise(p * 0.31 + vec2(9.0, 2.0));
	stone *= mix(vec3(0.98, 1.00, 1.02), vec3(1.03, 1.01, 0.97), warmth);
	stone *= 0.95 + grain * 0.08 + fine * 0.025;
	float face_wear = 1.0 - smoothstep(0.14, 0.50, length(local / (half_size * 2.0)));
	stone += vec3(0.014, 0.014, 0.012) * face_wear;
	stone *= 1.0 - bevel * 0.085;

	// Dust follows broad patches and collects at recessed edges, never whole square tiles.
	float dust_patch = smoothstep(0.44, 0.76, noise(p * 0.37 + 51.0));
	float dust = dust_patch * (0.16 + dusty_ratio) * (0.25 + bevel * 0.45 + joint * 0.45);
	stone = mix(stone, dust_color.rgb, dust);

	// Rare fine fissures start from one edge and taper into the stone's face.
	float cracked = 1.0 - step(cracked_ratio, hash(stone_id + vec2(41.7, 13.1)));
	float crack_side = mix(-1.0, 1.0, step(0.5, hash(stone_id + 6.1)));
	float depth = half_size.x - local.x * crack_side;
	float crack_length = half_size.x * mix(0.42, 0.96, hash(stone_id + 14.2));
	float crack_origin = (hash(stone_id + 5.3) - 0.5) * half_size.y * 1.15;
	float fissure = local.y - crack_origin - depth * (hash(stone_id + 8.8) - 0.5) * 0.75;
	fissure -= (noise(vec2(depth * 16.0, row + column * 2.7)) - 0.5) * 0.018;
	float crack_aa = max(fwidth(fissure), 0.002);
	float crack = (1.0 - smoothstep(0.003, 0.006 + crack_aa, abs(fissure)))
		* (1.0 - smoothstep(crack_length * 0.55, crack_length, depth)) * step(0.0, depth)
		* cracked * (1.0 - joint) * detail_visibility(p * 6.0);
	stone *= 1.0 - crack * 0.26;

	// Mortar is close to the slab tone, with moss settling in connected damp areas.
	vec3 mortar = mix(stone_dark.rgb * 0.69, dust_color.rgb * 0.83, dust_patch * 0.45);
	float damp = smoothstep(0.53, 0.78, noise(p * 0.42 + vec2(31.0, 7.0)));
	float boundary = 1.0 - smoothstep(0.60, 0.96, mask);
	float moss = damp * (0.12 + boundary * 0.38) * (joint + bevel * 0.2);
	stone = mix(stone, mortar, joint * 0.70);
	stone = mix(stone, grass_dark.rgb * 0.85, moss);
	// Worn border stones connect paving to meadow without a bright outline.
	float curb = road * (1.0 - smoothstep(0.56, 0.70, mask));
	vec3 paving = mix(stone, mix(stone, curb_color.rgb, 0.26), curb);
	vec3 color = mix(grass, paving, road);

	float height = mix(grain * 0.012 + blade * 0.005,
		grain * 0.005 + face_wear * 0.009 - bevel * 0.017 - joint * 0.025 - crack * 0.005, road);
	vec3 n = normalize(NORMAL);
	vec3 dx = dFdx(VERTEX);
	vec3 dy = dFdy(VERTEX);
	vec3 r1 = cross(dy, n);
	vec3 r2 = cross(n, dx);
	float det = dot(dx, r1);
	vec3 gradient = (r1 * dFdx(height) + r2 * dFdy(height)) * sign(det) / max(abs(det), 0.000001);
	NORMAL = normalize(n - gradient * bump_strength);
	ALBEDO = color;
	ROUGHNESS = mix(0.97, 0.91 - face_wear * 0.045, road);
	SPECULAR = 0.13;
}
