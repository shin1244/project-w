shader_type spatial;
render_mode diffuse_burley;

varying vec3 wall_position;
varying vec3 wall_normal;

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

void vertex() {
	wall_position = VERTEX;
	wall_normal = NORMAL;
}

void fragment() {
	vec3 p = wall_position;
	vec2 uv = abs(wall_normal.y) > 0.5 ? p.xz : vec2(abs(wall_normal.x) > 0.5 ? p.z : p.x, p.y);
	uv /= vec2(1.0, 0.46);
	uv.x += mod(floor(uv.y), 2.0) * 0.5;
	vec2 local = abs(fract(uv) - 0.5);
	float edge = max(local.x, local.y);
	float aa = max(fwidth(edge), 0.015);
	float seam = smoothstep(0.43 - aa, 0.48 + aa, edge);
	vec3 stone = mix(vec3(0.28, 0.31, 0.32), vec3(0.43, 0.46, 0.45), hash(floor(uv)));
	stone *= 0.91 + hash(floor(p.xz * 12.0 + p.y)) * 0.12;
	stone = mix(stone, vec3(0.19, 0.22, 0.22), seam * 0.6);
	// Continuous lighter coping below the crenellations.
	if (p.y >= 2.39 && p.y <= 2.61) stone = vec3(0.50, 0.52, 0.48) * (1.0 - seam * 0.12);
	ALBEDO = stone;
	ROUGHNESS = 0.94;
	SPECULAR = 0.15;
}
