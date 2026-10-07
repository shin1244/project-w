shader_type spatial;
render_mode diffuse_burley;

// 따뜻한 회색 석회암. 돌의 색차보다 둥글게 닳은 모서리와 연속적인 풍화 얼룩으로 질감을 낸다.
// 균열·거미줄은 가까이에서만 읽히고, 먼 거리에서는 성벽의 단정한 실루엣을 유지한다.
uniform vec4 stone_dark : source_color = vec4(0.49, 0.48, 0.44, 1.0);
uniform vec4 stone_light : source_color = vec4(0.64, 0.62, 0.56, 1.0);
uniform vec4 coping_color : source_color = vec4(0.67, 0.65, 0.59, 1.0);
uniform vec4 seam_color : source_color = vec4(0.34, 0.34, 0.31, 1.0);
uniform vec4 moss_color : source_color = vec4(0.28, 0.32, 0.21, 1.0);
uniform vec4 web_color : source_color = vec4(0.75, 0.75, 0.69, 1.0);
uniform float cobweb_ratio : hint_range(0.0, 0.5) = 0.06;
uniform float cracked_ratio : hint_range(0.0, 0.5) = 0.055;
uniform float mossy_ratio : hint_range(0.0, 1.0) = 0.45;

const float WALL_TOP = 2.4; // TerrainBuilder.build_walls의 벽 몸체 높이(갓돌 아래)

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

void vertex() {
	wall_position = VERTEX;
	wall_normal = NORMAL;
}

// 갓돌 아래에 작게 걸린 실. 화면에서 한 가닥을 구분할 수 없을 때는 부드럽게 사라진다.
float cobweb(vec2 d, float radius, float seed) {
	if (d.x < 0.0 || d.y < 0.0) return 0.0;
	float r = length(d);
	float theta = atan(d.y, d.x) / 1.5707963; // 0..1
	float spokes = 4.0 + floor(seed * 3.0);
	float spoke_phase = abs(fract(theta * spokes + 0.5) - 0.5);
	float aa = max(fwidth(r) * 1.5, 0.006);
	float spoke = 1.0 - smoothstep(0.003, 0.003 + aa, spoke_phase / spokes * 1.5707963 * r);
	// 실 사이에서 안쪽으로 처지는 고리
	float sag = 1.0 - 0.16 * (1.0 - spoke_phase * 2.0);
	float ring_r = r / sag;
	float ring = 1.0 - smoothstep(0.003, 0.003 + aa, abs(fract(ring_r / 0.11) - 0.5) * 0.11);
	float fade = 1.0 - smoothstep(radius * 0.8, radius, r / sag);
	float detail = 1.0 - smoothstep(0.015, 0.07, fwidth(r));
	return max(spoke, ring * step(0.06, r)) * fade * detail;
}

void fragment() {
	vec3 p = wall_position;
	bool top = abs(wall_normal.y) > 0.5;
	bool facing_x = abs(wall_normal.x) > 0.5;
	// 면은 칸 경계에 있으므로 법선 반대쪽으로 살짝 들어가서 찾는다.
	vec2 cell = floor(p.xz - wall_normal.xz * 0.01);
	float along = facing_x ? p.z : p.x;
	vec2 surface = top ? p.xz : vec2(along, p.y);
	bool coping = p.y >= WALL_TOP - 0.01 && p.y <= WALL_TOP + 0.21;
	vec2 block_size = top ? vec2(1.35, 0.86) : vec2(1.1, 0.46);
	float course = floor(surface.y / block_size.y);
	block_size.x *= mix(0.93, 1.09, hash(vec2(course, 9.0)));
	vec2 uv = surface / block_size;
	uv.x += mod(course, 2.0) * 0.5 + (hash(vec2(course, 4.0)) - 0.5) * 0.12;
	vec2 brick = floor(uv);
	vec2 local = fract(uv) - 0.5;
	vec2 edge_distance = (vec2(0.5) - abs(local)) * block_size;
	float grain = noise(surface * 12.0 + vec2(3.0, 17.0));
	float inset = min(edge_distance.x, edge_distance.y) + (grain - 0.5) * 0.012;
	float aa = max(fwidth(inset) * 0.65, 0.002);
	float seam = 1.0 - smoothstep(0.007 - aa, 0.022 + aa, inset);
	float bevel = (1.0 - smoothstep(0.018, 0.063, inset)) * (1.0 - seam);
	float highlight = edge_distance.y < edge_distance.x ? sign(local.y) : -sign(local.x);

	float patina = noise(surface * vec2(0.37, 0.65) + vec2(11.0, 7.0));
	float tone = 0.31 + hash(brick) * 0.31 + (patina - 0.5) * 0.18;
	vec3 stone = mix(stone_dark.rgb, stone_light.rgb, tone);
	stone *= mix(vec3(0.99, 1.0, 1.01), vec3(1.015, 1.0, 0.985), hash(brick + 17.0));
	stone *= 0.975 + grain * 0.05;
	stone *= 1.0 + bevel * highlight * 0.105;
	stone = mix(stone, seam_color.rgb * (0.95 + patina * 0.1), seam * 0.63);

	// 짧고 가는 석회암 결. 검은 표식을 찍는 대신 돌 색보다 한 단계 어둡게만 표현한다.
	float cracked = step(hash(brick + vec2(7.0, 3.0)), cracked_ratio);
	float angle = hash(brick + 2.0) * 3.14159;
	vec2 dir = vec2(cos(angle), sin(angle));
	vec2 local_m = local * block_size;
	float lengthwise = dot(local_m, dir);
	float across = dot(local_m, vec2(-dir.y, dir.x))
		- (noise(vec2(lengthwise * 11.0, brick.x + brick.y * 7.0)) - 0.5) * 0.025;
	float crack_aa = max(fwidth(across), 0.002);
	float crack = cracked * (1.0 - smoothstep(0.002, 0.005 + crack_aa, abs(across)))
		* (1.0 - smoothstep(0.11, 0.32, abs(lengthwise)))
		* (1.0 - smoothstep(0.016, 0.06, crack_aa)) * (1.0 - seam);
	stone = mix(stone, seam_color.rgb, crack * 0.42);

	if (!top) {
		// 지면의 습기는 칸마다 끊기지 않고 이어지고 이끼가 그 위에 불규칙하게 자란다.
		float wet_patch = noise(p.xz * 0.67 + vec2(19.0, 3.0));
		float reach = 0.22 + wet_patch * 0.65;
		float damp = 1.0 - smoothstep(0.03, reach + 0.28, p.y);
		stone *= 1.0 - damp * (0.055 + wet_patch * 0.055);
		float moss_threshold = mix(0.86, 0.2, mossy_ratio);
		float moss_patch = smoothstep(moss_threshold - 0.1, moss_threshold + 0.12, wet_patch);
		float moss = moss_patch * (1.0 - smoothstep(reach - 0.23, reach + 0.12, p.y + (grain - 0.5) * 0.15));
		stone = mix(stone, moss_color.rgb * (0.94 + grain * 0.12), moss * 0.55 * step(0.001, mossy_ratio));
		// 갓돌 아래의 빗물 자국도 가늘고 낮은 대비로 연결한다.
		float streak = smoothstep(0.56, 0.82, noise(vec2(along * 3.7, p.y * 0.25)));
		stone *= 1.0 - streak * smoothstep(0.8, WALL_TOP, p.y) * 0.045;
		float seed = hash(cell + 47.0);
		if (seed < cobweb_ratio && p.y < WALL_TOP) {
			float right = step(0.5, hash(cell + 3.0));
			float h = fract(along);
			vec2 d = vec2(mix(h, 1.0 - h, right), WALL_TOP - p.y);
			float web = cobweb(d, 0.3 + hash(cell + 9.0) * 0.15, hash(cell + 13.0));
			stone = mix(stone, web_color.rgb, web * 0.27);
		}
	}
	// 밝은 갓돌이 성벽 전체를 묶는다. 좁은 수직 모서리는 닳아서 한 톤 더 밝다.
	if (coping) {
		stone = coping_color.rgb * (0.965 + patina * 0.045 + (grain - 0.5) * 0.025);
		stone *= 1.0 - seam * 0.13 + bevel * highlight * 0.055;
		if (!top) stone *= 1.0 + smoothstep(WALL_TOP + 0.12, WALL_TOP + 0.2, p.y) * 0.065;
	}
	ALBEDO = stone;
	ROUGHNESS = 0.91 + grain * 0.055;
	SPECULAR = 0.13;
}
