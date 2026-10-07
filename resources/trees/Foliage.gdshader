shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;

// SculptedTree의 면별 색과 수종별 팔레트를 유지하면서 큰 면적에만 약한 색조를 더한다.
// ResourceManager의 결정적 회전은 같은 종 안에서의 색 변화에도 사용한다.
uniform vec4 albedo : source_color = vec4(0.3, 0.4, 0.2, 1.0);
uniform float tree_variation : hint_range(0.0, 0.3) = 0.12; // 나무마다 밝기·색온도 차이
uniform float shade_strength : hint_range(0.0, 1.0) = 0.35; // 아래쪽이 어두워지는 정도
uniform float mottle : hint_range(0.0, 0.5) = 0.18;         // 잎 덩어리 얼룩
uniform bool bark = false;
uniform bool birch = false;

varying vec3 world_position;
varying vec3 world_normal;
varying vec3 local_position;
varying flat float tree_seed;

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

void vertex() {
	local_position = VERTEX;
	world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
	tree_seed = fract(atan(MODEL_MATRIX[0].z, MODEL_MATRIX[0].x) / 6.2831853) * 97.0;
}

void fragment() {
	float brightness = hash(vec2(tree_seed, 3.7));
	float warmth = hash(vec2(tree_seed, 11.3));
	vec3 color = albedo.rgb * COLOR.rgb * (1.0 + (brightness - 0.5) * 2.0 * tree_variation);
	// 일부 나무는 살짝 노랗게(따뜻하게), 일부는 푸르게(차갑게) 기운다.
	color *= mix(vec3(0.96, 0.99, 1.035), vec3(1.04, 1.015, 0.945), warmth);
	color *= mix(1.0 - shade_strength, 1.0, clamp(world_normal.y * 0.5 + 0.5, 0.0, 1.0));
	float clumps = noise(local_position.xz * 1.3 + local_position.y * 0.7 + tree_seed);
	color *= 1.0 - mottle * 0.5 + clumps * mottle;
	if (bark) {
		float grain = noise(vec2(local_position.x * 19.0 + local_position.z * 11.0, local_position.y * 1.6));
		color *= mix(0.94, 1.03, grain);
		if (birch) {
			float angle = atan(local_position.z, local_position.x);
			float band = floor(local_position.y * 6.5);
			float scar_seed = hash(vec2(floor(angle * 3.5), band));
			float scar = (1.0 - smoothstep(0.09, 0.19, abs(fract(local_position.y * 6.5) - 0.46))) * smoothstep(0.57, 0.74, scar_seed);
			// Linear equivalent of the dark olive bark-mark palette.
			color = mix(color, vec3(0.047, 0.057, 0.040), scar * 0.85);
		}
	}
	ALBEDO = color;
	ROUGHNESS = 0.95;
	SPECULAR = 0.12;
}
