shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;

// UV.x offsets each seep's slow cycle. UV.y distinguishes the body from its drop.
// UV2 keeps the source position shared across all vertices of each seep.
void vertex() {
	float phase = fract(TIME / 7.6 + UV.x);
	vec2 offset = VERTEX.xz - UV2;
	if (UV.y < 0.5) {
		float surge = pow(max(sin(phase * 6.2831853), 0.0), 1.65);
		float height = mix(0.065, 0.64, surge);
		float width = mix(0.68, 1.02, surge);
		VERTEX.xz = UV2 + offset * width;
		VERTEX.y = 0.035 + VERTEX.y * height;
		NORMAL = normalize(vec3(NORMAL.x / width, NORMAL.y / height, NORMAL.z / width));
	} else {
		float travel = clamp((phase - 0.26) / 0.36, 0.0, 1.0);
		float active = step(0.26, phase) * (1.0 - step(0.62, phase));
		float scale = active * sin(travel * 3.14159265);
		VERTEX.xz = UV2 + offset * scale + vec2(0.06, -0.045) * sin(travel * 3.14159265);
		VERTEX.y = 0.035 + VERTEX.y * scale + 0.66 * 4.0 * travel * (1.0 - travel);
	}
}

void fragment() {
	ALBEDO = vec3(0.017, 0.033, 0.030);
	ROUGHNESS = 0.20;
	METALLIC = 0.25;
	SPECULAR = 0.62;
}
