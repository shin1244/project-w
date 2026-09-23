shader_type spatial;
render_mode diffuse_burley;
varying vec2 ground_position;

void vertex() { ground_position = VERTEX.xz; }
void fragment() {
	vec2 p = ground_position;
	vec2 cells = abs(fract(p) - 0.5);
	float width = max(fwidth(p.x), fwidth(p.y));
	float grid = 1.0 - smoothstep(0.012, 0.012 + width, min(cells.x, cells.y));
	float distance_to_edge = abs(max(abs(p.x), abs(p.y)) - 1.5);
	float footprint = 1.0 - smoothstep(0.025, 0.025 + width, distance_to_edge);
	vec3 base = mix(vec3(0.016, 0.024, 0.031), vec3(0.040, 0.052, 0.061), grid * 0.5);
	ALBEDO = mix(base, vec3(0.48, 0.34, 0.12), footprint);
	ROUGHNESS = 1.0;
}
