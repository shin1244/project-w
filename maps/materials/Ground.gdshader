shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;

// The same R cells drive paving, minimap colors, and client/server construction rules.
uniform sampler2D road_mask : filter_linear, repeat_disable;
uniform vec2 map_origin;
uniform vec2 map_size;
uniform vec4 grass_dark : source_color = vec4(0.17, 0.31, 0.10, 1.0);
uniform vec4 grass_light : source_color = vec4(0.36, 0.51, 0.22, 1.0);
uniform vec4 stone_dark : source_color = vec4(0.36, 0.39, 0.40, 1.0);
uniform vec4 stone_light : source_color = vec4(0.57, 0.58, 0.55, 1.0);
uniform vec4 curb_color : source_color = vec4(0.64, 0.64, 0.57, 1.0);
uniform float bump_strength : hint_range(0.0, 1.0) = 0.26;

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

float detail_visibility(vec2 p) {
	return 1.0 - smoothstep(0.35, 1.1, max(length(dFdx(p)), length(dFdy(p))));
}

void vertex() {
	map_position = VERTEX.xz;
}

void fragment() {
	vec2 p = map_position;
	float macro = noise(p * 0.17);
	float patches = noise(p * 0.61 + vec2(17.4, 5.2));
	float soil = noise(p * 3.1) * 0.6 + noise(p * 4.7 + 10.2) * 0.4;
	float fine = mix(0.5, noise(p * 18.0), detail_visibility(p * 18.0));
	vec2 blade_uv = p * 8.0;
	vec2 blade_local = fract(blade_uv) - 0.5;
	float angle = hash(floor(blade_uv) + 21.0) * 6.283185;
	vec2 direction = vec2(cos(angle), sin(angle));
	float blade_dist = length(vec2(dot(blade_local, vec2(-direction.y, direction.x)) * 4.0, dot(blade_local, direction)));
	float blade_aa = max(fwidth(blade_dist), 0.02);
	float blade = (1.0 - smoothstep(0.29 - blade_aa, 0.29 + blade_aa, blade_dist)) * detail_visibility(blade_uv);
	vec3 grass = mix(grass_dark.rgb, grass_light.rgb, macro * 0.50 + patches * 0.30 + soil * 0.20);
	grass *= 0.83 + soil * 0.23 + fine * 0.10 + blade * 0.15;

	// Staggered, individually weathered cobbles with dark mortar and rounded edges.
	vec2 stone_uv = p / vec2(0.85, 0.60);
	stone_uv.x += mod(floor(stone_uv.y), 2.0) * 0.5;
	stone_uv += vec2(noise(p * 2.7), noise(p * 2.7 + 8.0)) * 0.08;
	vec2 stone_id = floor(stone_uv);
	vec2 stone_local = abs(fract(stone_uv) - 0.5);
	float edge = max(stone_local.x, stone_local.y);
	float aa = max(fwidth(edge) * 0.6, 0.012);
	float joint = smoothstep(0.43 - aa, 0.48 + aa, edge) * detail_visibility(stone_uv);
	float variation = hash(stone_id);
	vec3 stone = mix(stone_dark.rgb, stone_light.rgb, variation * 0.65 + macro * 0.35);
	stone *= 0.93 + soil * 0.14 + fine * 0.06;
	stone = mix(stone, vec3(0.20, 0.23, 0.21), joint * 0.76);
	float moss = smoothstep(0.54, 0.76, patches) * joint * 0.30;
	stone = mix(stone, grass_dark.rgb, moss);

	float mask = texture(road_mask, (p - map_origin) / map_size).r;
	float road = smoothstep(0.46, 0.54, mask);
	// A restrained stone edging keeps the buildable grass easy to read at game zoom.
	float curb = road * (1.0 - smoothstep(0.61, 0.77, mask));
	vec3 paving = mix(stone, curb_color.rgb * (0.90 + soil * 0.14 - joint * 0.12), curb);
	vec3 color = mix(grass, paving, road);

	float height = mix(soil * 0.020 + blade * 0.010, soil * 0.012 - joint * 0.036 + curb * 0.018, road);
	vec3 n = normalize(NORMAL);
	vec3 dx = dFdx(VERTEX);
	vec3 dy = dFdy(VERTEX);
	vec3 r1 = cross(dy, n);
	vec3 r2 = cross(n, dx);
	float det = dot(dx, r1);
	vec3 gradient = (r1 * dFdx(height) + r2 * dFdy(height)) * sign(det) / max(abs(det), 0.000001);
	NORMAL = normalize(n - gradient * bump_strength);
	ALBEDO = color;
	ROUGHNESS = mix(0.97, 0.89, road);
	SPECULAR = 0.18;
}
