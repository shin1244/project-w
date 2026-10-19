shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;

// Visual detail only: the authoritative R-cell mask still defines roads/building space.
// Roads are an industrial quarter's: pale brick lanes with a narrow-gauge track down the middle,
// packed works-yard earth stained with coal dust around the HQs, brick rings on the magic stone
// plaza and cinder paths elsewhere. Grass stays bright, with ash settling beside the roads.
uniform sampler2D road_mask : filter_linear, repeat_disable;
// TerrainBuilder.road_surface: R lane brick, G works-yard earth, B plaza rings, A coal dust.
uniform sampler2D surface_mask : filter_linear, repeat_disable;
uniform vec2 lane_points[33]; // TerrainBuilder.lane_curve(); the bottom lane mirrors it
uniform vec2 map_origin;
uniform vec2 map_size;
uniform vec4 grass_dark : source_color = vec4(0.20, 0.30, 0.15, 1.0);
uniform vec4 grass_light : source_color = vec4(0.36, 0.45, 0.24, 1.0);
uniform vec4 grass_dry : source_color = vec4(0.43, 0.43, 0.27, 1.0);
// Pale stock brick, a little darker than before so units and buildings still read above it.
uniform vec4 brick_dark : source_color = vec4(0.42, 0.38, 0.33, 1.0);
uniform vec4 brick_light : source_color = vec4(0.55, 0.50, 0.43, 1.0);
uniform vec4 brick_red : source_color = vec4(0.47, 0.35, 0.30, 1.0);
uniform vec4 mortar_color : source_color = vec4(0.38, 0.36, 0.33, 1.0);
uniform vec4 cinder_color : source_color = vec4(0.42, 0.40, 0.37, 1.0);
uniform vec4 earth_color : source_color = vec4(0.44, 0.40, 0.35, 1.0);
uniform vec4 coal_color : source_color = vec4(0.25, 0.24, 0.23, 1.0);
uniform vec4 ash_color : source_color = vec4(0.50, 0.49, 0.47, 1.0);
uniform vec4 sleeper_color : source_color = vec4(0.25, 0.20, 0.16, 1.0);
uniform vec4 rail_color : source_color = vec4(0.60, 0.61, 0.62, 1.0);
uniform float red_brick_ratio : hint_range(0.0, 0.5) = 0.06;
uniform float coal_ratio : hint_range(0.0, 1.0) = 0.3;
uniform float bump_strength : hint_range(0.0, 1.0) = 0.30;

const float GAUGE = 1.0;        // rail centre to rail centre
const float TRACK_HALF = 1.35;  // cinder ballast either side of the centre line
const float SLEEPER_STEP = 0.8;
const vec2 LANE_BRICK = vec2(0.78, 0.34);  // along the lane, across it
const float PLAZA_COURSE = 0.4;

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

// Distance from the nearer lane's centre line and the distance run along it.
vec2 lane_frame(vec2 p) {
	vec2 q = vec2(p.x, -abs(p.y));
	float best = 1e9;
	float along = 0.0;
	float run = 0.0;
	for (int i = 0; i < 32; i++) {
		vec2 a = lane_points[i];
		vec2 ab = lane_points[i + 1] - a;
		float len = length(ab);
		float t = clamp(dot(q - a, ab) / (len * len), 0.0, 1.0);
		float d = length(q - a - ab * t);
		if (d < best) {
			best = d;
			along = run + t * len;
		}
		run += len;
	}
	return vec2(best, along);
}

// A laid brick: colour and height for its id, the distance to its nearest joint and its position
// within it. Joints fade out where a single brick is too small to read.
vec3 brick(vec2 id, float edge, vec2 local, vec2 size, float visibility, float grain, inout float height) {
	float tone = hash(id + 3.1);
	vec3 color = mix(brick_dark.rgb, brick_light.rgb, 0.30 + tone * 0.50);
	color = mix(color, brick_red.rgb, step(hash(id + 11.7), red_brick_ratio) * 0.85);
	color *= mix(vec3(0.98, 1.0, 1.02), vec3(1.03, 1.0, 0.97), hash(id + 5.9));
	color *= 0.95 + grain * 0.08;
	float aa = max(fwidth(edge), 0.003);
	float joint = (1.0 - smoothstep(0.012, 0.026 + aa, edge)) * visibility;
	float bevel = (1.0 - smoothstep(0.02, 0.06, edge)) * (1.0 - joint) * visibility;
	float face = 1.0 - smoothstep(0.1, 0.5, length(local / size));
	color *= 1.0 - bevel * 0.08;
	color += vec3(0.012) * face;
	color = mix(color, mortar_color.rgb, joint * 0.8);
	height += face * 0.006 - bevel * 0.012 - joint * 0.022;
	return color;
}

void vertex() {
	map_position = VERTEX.xz;
}

void fragment() {
	vec2 p = map_position;
	vec2 uv = (p - map_origin) / map_size;
	float macro = noise(p * 0.13);
	float patches = noise(p * 0.57 + vec2(17.4, 5.2));
	float grain = noise(p * 3.4) * 0.65 + noise(p * 7.8 + 10.2) * 0.35;
	float fine = mix(0.5, noise(p * 19.0), detail_visibility(p * 19.0));
	float mask = texture(road_mask, uv).r;
	vec4 surface = texture(surface_mask, uv);
	// Ragged edges for loose surfaces; laid brick keeps a straight kerb.
	float ragged = (noise(p * 1.7 + 4.0) - 0.5) * 0.22 * (1.0 - surface.r);
	float road = smoothstep(0.46, 0.54, mask + ragged);
	float height = 0.0;

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
	// Ash from the works settles on the grass beside the roads.
	float roadside = smoothstep(0.12, 0.46, mask) * (1.0 - road);
	grass = mix(grass, ash_color.rgb * (0.92 + grain * 0.1), roadside * (0.18 + patches * 0.22));
	float grass_height = grain * 0.012 + blade * 0.005;

	// Cinder paths: crushed ash and clinker, the default road surface.
	float clinker = step(0.78, hash(floor(p * 7.0))) * detail_visibility(p * 7.0);
	vec3 cinder = cinder_color.rgb * (0.9 + macro * 0.1 + grain * 0.1 + fine * 0.04);
	cinder = mix(cinder, cinder * mix(0.72, 1.18, hash(floor(p * 7.0) + 2.0)), clinker * 0.6);
	float cinder_height = grain * 0.008 + clinker * 0.006;

	// Works yards: packed earth, the HQ's ground darkened by coal dust in loose drifts.
	vec3 earth = earth_color.rgb * (0.9 + macro * 0.12 + grain * 0.08 + fine * 0.03);
	float pebble = step(0.86, hash(floor(p * 9.0) + 5.0)) * detail_visibility(p * 9.0);
	earth = mix(earth, earth * 0.78, pebble * 0.5);
	float drift = noise(p * 0.55 + 41.0) * 0.6 + noise(p * 1.9 + 3.0) * 0.4;
	float coal = smoothstep(0.62 - coal_ratio * 0.3, 0.8 - coal_ratio * 0.3, drift) * surface.a;
	earth = mix(earth, coal_color.rgb * (0.9 + grain * 0.2), coal * 0.55);
	float earth_height = grain * 0.01 + pebble * 0.004 - coal * 0.003;

	// Lanes: stretcher courses follow the road; a track of sleepers and rails runs down the middle.
	vec2 lane = lane_frame(p);
	vec2 lane_uv = vec2(lane.y, lane.x) / LANE_BRICK;
	float lane_row = floor(lane_uv.y);
	lane_uv.x += mod(lane_row, 2.0) * 0.5;
	vec2 lane_id = vec2(floor(lane_uv.x), lane_row);
	vec2 lane_local = (fract(lane_uv) - 0.5) * LANE_BRICK;
	vec2 lane_edge = LANE_BRICK * 0.5 - abs(lane_local);
	float lane_height = 0.0;
	vec3 paving = brick(lane_id, min(lane_edge.x, lane_edge.y), lane_local, LANE_BRICK,
		detail_visibility(lane_uv), grain, lane_height);
	// Wheel-worn dust either side of the track, never whole bricks.
	float wear = (1.0 - smoothstep(TRACK_HALF, TRACK_HALF + 1.8, lane.x)) * smoothstep(0.45, 0.8, noise(p * 0.6 + 9.0));
	paving = mix(paving, ash_color.rgb * 0.92, wear * 0.35);
	float ballast_edge = TRACK_HALF + (noise(p * 3.0) - 0.5) * 0.16;
	float ballast = 1.0 - smoothstep(ballast_edge - 0.05, ballast_edge + 0.05, lane.x);
	vec3 track = cinder * vec3(0.92, 0.92, 0.95);
	float sleeper_phase = abs(fract(lane.y / SLEEPER_STEP) - 0.5) * SLEEPER_STEP;
	float track_detail = detail_visibility(vec2(lane.y / SLEEPER_STEP, lane.x * 4.0));
	float sleeper = (1.0 - smoothstep(0.12, 0.14, sleeper_phase)) * (1.0 - smoothstep(0.88, 0.92, lane.x));
	track = mix(track, sleeper_color.rgb * (0.88 + hash(vec2(floor(lane.y / SLEEPER_STEP), 3.0)) * 0.2), sleeper * mix(0.55, 1.0, track_detail));
	float rail_aa = max(fwidth(lane.x), 0.002);
	float rail = 1.0 - smoothstep(0.035, 0.045 + rail_aa, abs(lane.x - GAUGE * 0.5));
	float rail_shadow = (1.0 - smoothstep(0.045, 0.12, abs(lane.x - GAUGE * 0.5))) * (1.0 - rail);
	track *= 1.0 - rail_shadow * 0.3 * track_detail;
	track = mix(track, rail_color.rgb, rail * mix(0.4, 1.0, track_detail));
	float lane_track = ballast * smoothstep(0.75, 0.95, surface.r);
	paving = mix(paving, track, lane_track);
	lane_height = mix(lane_height, cinder_height + sleeper * 0.01 + rail * 0.02, lane_track);

	// The magic stone plaza: concentric brick courses with a darker band every fifth ring.
	float radius = length(p);
	float ring = floor(radius / PLAZA_COURSE);
	float count = max(6.0, floor(TAU * (ring + 0.5) * PLAZA_COURSE / 0.8));
	float around = (atan(p.y, p.x) / TAU + 0.5) * count + mod(ring, 2.0) * 0.5;
	vec2 plaza_id = vec2(floor(around), ring);
	vec2 plaza_size = vec2(TAU * radius / count, PLAZA_COURSE);
	vec2 plaza_local = vec2((fract(around) - 0.5) * plaza_size.x, (fract(radius / PLAZA_COURSE) - 0.5) * PLAZA_COURSE);
	vec2 plaza_edge = plaza_size * 0.5 - abs(plaza_local);
	float plaza_height = 0.0;
	vec3 plaza = brick(plaza_id, min(plaza_edge.x, plaza_edge.y), plaza_local, plaza_size,
		detail_visibility(vec2(around, radius / PLAZA_COURSE)), grain, plaza_height);
	plaza = mix(plaza, plaza * vec3(0.78, 0.74, 0.74), step(4.0, mod(ring, 5.0)));

	vec3 surface_color = cinder;
	float surface_height = cinder_height;
	float yard = smoothstep(0.35, 0.65, surface.g + (noise(p * 0.9) - 0.5) * 0.3);
	surface_color = mix(surface_color, earth, yard);
	surface_height = mix(surface_height, earth_height, yard);
	float laned = smoothstep(0.45, 0.55, surface.r);
	surface_color = mix(surface_color, paving, laned);
	surface_height = mix(surface_height, lane_height, laned);
	float plazaed = smoothstep(0.45, 0.55, surface.b);
	surface_color = mix(surface_color, plaza, plazaed);
	surface_height = mix(surface_height, plaza_height, plazaed);
	// Soot gathers at the kerb where the road meets the grass.
	float kerb = road * (1.0 - smoothstep(0.56, 0.72, mask));
	surface_color *= 1.0 - kerb * 0.12;

	vec3 color = mix(grass, surface_color, road);
	height = mix(grass_height, surface_height, road);
	vec3 n = normalize(NORMAL);
	vec3 dx = dFdx(VERTEX);
	vec3 dy = dFdy(VERTEX);
	vec3 r1 = cross(dy, n);
	vec3 r2 = cross(n, dx);
	float det = dot(dx, r1);
	vec3 gradient = (r1 * dFdx(height) + r2 * dFdy(height)) * sign(det) / max(abs(det), 0.000001);
	NORMAL = normalize(n - gradient * bump_strength);
	ALBEDO = color;
	float steel = rail * lane_track * laned * road;
	METALLIC = steel * 0.6;
	ROUGHNESS = mix(mix(0.97, 0.9, road), 0.4, steel);
	SPECULAR = mix(0.13, 0.5, steel);
}
