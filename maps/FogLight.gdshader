shader_type canvas_item;
render_mode unshaded;

uniform float radius;
uniform float feather;
varying vec2 offset;

void vertex() {
    offset = VERTEX;
}

void fragment() {
    float alpha = 1.0 - smoothstep(max(0.0, radius - feather), radius, length(offset));
    COLOR = vec4(1.0, 1.0, 1.0, alpha);
}
