shader_type spatial;
render_mode diffuse_burley;

varying vec2 ground_position;
uniform vec2 footprint_size = vec2(2.0, 2.0);

void vertex() {
	ground_position = VERTEX.xz;
}

void fragment() {
	vec2 p = ground_position;
	vec2 half_size = footprint_size * 0.5;
	vec2 cells = abs(fract(p + half_size + 0.5) - 0.5);
	float width = max(max(fwidth(p.x), fwidth(p.y)), 0.002);
	float grid = 1.0 - smoothstep(0.006, 0.006 + width, min(cells.x, cells.y));
	vec2 q = abs(p) - half_size;
	float edge = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0);
	float footprint = 1.0 - smoothstep(0.017, 0.017 + width, abs(edge));
	float falloff = 1.0 - smoothstep(2.0, 8.0, length(p));
	vec3 base = mix(vec3(0.045, 0.061, 0.065), vec3(0.076, 0.097, 0.099), grid * 0.5 * falloff);
	ALBEDO = mix(base, vec3(0.63, 0.43, 0.16), footprint);
	ROUGHNESS = 1.0;
}
