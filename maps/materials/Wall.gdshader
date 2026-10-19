shader_type spatial;
render_mode diffuse_burley;

// 붉은 벽돌 방벽 위에 리벳 강판 갓을 씌웠다. 위쪽 벽돌은 굴뚝 그을음에 어둡고 갓 아래로 그을음이
// 흘러내린다. 옛 구간은 같은 셰이더에 더 어둡고 그을린 벽돌을 쓴다(TerrainBuilder.build_walls).
// 줄눈·리벳은 가까이에서만 읽히고, 먼 거리에서는 벽의 단정한 실루엣을 유지한다.
uniform vec4 brick_dark : source_color = vec4(0.42, 0.22, 0.17, 1.0);
uniform vec4 brick_light : source_color = vec4(0.56, 0.30, 0.23, 1.0);
uniform vec4 mortar_color : source_color = vec4(0.52, 0.49, 0.45, 1.0);
uniform vec4 cap_color : source_color = vec4(0.40, 0.42, 0.43, 1.0);
uniform vec4 soot_color : source_color = vec4(0.16, 0.14, 0.13, 1.0);
uniform float soot_strength : hint_range(0.0, 1.0) = 0.35;
uniform float colour_fade : hint_range(0.0, 1.0) = 0.0;

const float WALL_TOP = 2.4; // TerrainBuilder.build_walls의 벽 몸체 높이(갓 아래)
const vec2 BRICK = vec2(0.5, 0.2);
const float PLATE = 1.0; // 갓 강판 한 장의 길이

varying vec3 wall_position;
varying vec3 wall_normal;

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
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

void vertex() {
	wall_position = VERTEX;
	wall_normal = NORMAL;
}

void fragment() {
	vec3 p = wall_position;
	bool top = abs(wall_normal.y) > 0.5;
	bool facing_x = abs(wall_normal.x) > 0.5;
	float along = facing_x ? p.z : p.x;
	bool cap = top || p.y >= WALL_TOP - 0.01;
	float grain = noise((top ? p.xz : vec2(along, p.y)) * 12.0 + vec2(3.0, 17.0));
	vec3 color;
	float roughness;
	float metallic = 0.0;

	if (cap) {
		// 칸마다 강판 한 장. 이음매를 따라 가장자리에 리벳을 둘렀다.
		vec2 surface = top ? p.xz : vec2(along, p.y - WALL_TOP);
		vec2 in_plate = top ? fract(p.xz / PLATE) : vec2(fract(along / PLATE), 0.5);
		vec2 to_edge = min(in_plate, 1.0 - in_plate) * PLATE;
		float seam_distance = min(to_edge.x, to_edge.y);
		float visibility = detail_visibility(surface * 4.0);
		float aa = max(fwidth(seam_distance), 0.003);
		float seam = (1.0 - smoothstep(0.008, 0.016 + aa, seam_distance)) * visibility;
		vec2 rivet_cell = top ? fract(surface * 4.0) - 0.5 : vec2(fract(along * 4.0) - 0.5, (p.y - WALL_TOP - 0.1) / 0.25);
		float rivet_row = top ? step(seam_distance, 0.2) : 1.0; // 4x4 칸의 바깥 줄
		float rivet = (1.0 - smoothstep(0.16, 0.24, length(rivet_cell))) * rivet_row * visibility;
		float plate = hash(floor(top ? p.xz / PLATE : vec2(along / PLATE, 2.0)));
		color = cap_color.rgb * (0.93 + plate * 0.1 + (grain - 0.5) * 0.05);
		// 이음매와 리벳에서 번진 녹이 가늘게 남는다.
		float rust = smoothstep(0.7, 0.9, noise(surface * 2.6 + vec2(7.0, 1.0))) * (0.4 + (1.0 - smoothstep(0.0, 0.12, seam_distance)) * 0.6);
		color = mix(color, vec3(0.38, 0.26, 0.2), rust * 0.3);
		color *= 1.0 - seam * 0.35;
		color = mix(color, cap_color.rgb * 1.25, rivet * 0.6);
		if (!top) color *= 0.92;
		metallic = 0.45;
		roughness = 0.55 + grain * 0.1;
	} else {
		// 반장 엇갈림 쌓기. 벽돌 하나하나의 색차는 작게, 그을음과 물때로 질감을 낸다.
		vec2 surface = vec2(along, p.y);
		float course = floor(surface.y / BRICK.y);
		vec2 uv = surface / BRICK;
		uv.x += mod(course, 2.0) * 0.5;
		vec2 brick_id = floor(uv);
		vec2 local = fract(uv) - 0.5;
		vec2 edge_distance = (vec2(0.5) - abs(local)) * BRICK;
		float inset = min(edge_distance.x, edge_distance.y);
		float visibility = detail_visibility(uv);
		float aa = max(fwidth(inset) * 0.65, 0.002);
		float joint = (1.0 - smoothstep(0.008 - aa, 0.018 + aa, inset)) * visibility;
		float tone = hash(brick_id) * 0.55 + noise(surface * vec2(0.4, 0.9)) * 0.3;
		color = mix(brick_dark.rgb, brick_light.rgb, 0.25 + tone * 0.6);
		color *= mix(vec3(0.97, 1.0, 1.03), vec3(1.04, 0.99, 0.96), hash(brick_id + 17.0));
		color *= 0.96 + grain * 0.06;
		color = mix(color, mortar_color.rgb, joint * 0.7);
		// 굴뚝 그을음은 위쪽 벽돌에 쌓이고 갓 아래로 가늘게 흘러내린다.
		float streak = smoothstep(0.5, 0.8, noise(vec2(along * 3.1, p.y * 0.3)));
		float soot = smoothstep(1.2, WALL_TOP, p.y) * (0.45 + streak * 0.55) + streak * smoothstep(0.6, WALL_TOP, p.y) * 0.35;
		color = mix(color, soot_color.rgb, clamp(soot, 0.0, 1.0) * soot_strength);
		// 땅에서 오른 물때는 칸마다 끊기지 않고 이어진다.
		float wet = noise(p.xz * 0.67 + vec2(19.0, 3.0));
		color *= 1.0 - (1.0 - smoothstep(0.03, 0.25 + wet * 0.5, p.y)) * (0.06 + wet * 0.06);
		roughness = 0.9 + grain * 0.06;
	}
	color = mix(color, vec3(dot(color, vec3(0.299, 0.587, 0.114))) * vec3(1.02, 1.0, 0.96), colour_fade);
	ALBEDO = color;
	METALLIC = metallic;
	ROUGHNESS = roughness;
	SPECULAR = cap ? 0.4 : 0.13;
}
