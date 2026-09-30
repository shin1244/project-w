shader_type canvas_item;
render_mode unshaded;

uniform vec2 half_extents;
uniform float reach;
uniform float feather;
varying vec2 offset;

void vertex() {
    offset = VERTEX;
}

void fragment() {
    float distance = length(max(abs(offset) - half_extents, vec2(0.0)));
    float alpha = reach > 0.0 ? 1.0 - smoothstep(max(0.0, reach - feather), reach, distance) : 1.0;
    COLOR = vec4(1.0, 1.0, 1.0, alpha);
}
