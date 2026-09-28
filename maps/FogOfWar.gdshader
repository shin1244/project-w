shader_type spatial;
render_mode unshaded, fog_disabled, cull_disabled, depth_test_disabled, depth_draw_never, shadows_disabled;

uniform sampler2D depth_texture : hint_depth_texture, filter_nearest, repeat_disable;
uniform sampler2D visibility_mask : filter_linear, repeat_disable;
uniform vec2 map_origin;
uniform vec2 map_extent;
uniform float darkness : hint_range(0.0, 1.0) = 0.78;

// Godot 4.3+ reverse-Z: https://docs.godotengine.org/en/stable/tutorials/shaders/advanced_postprocessing.html
void vertex() {
    POSITION = vec4(VERTEX.xy, 1.0, 1.0);
}

void fragment() {
    float depth = texture(depth_texture, SCREEN_UV).r;
    #if CURRENT_RENDERER == RENDERER_COMPATIBILITY
    vec3 ndc = vec3(SCREEN_UV, depth) * 2.0 - 1.0;
    #else
    vec3 ndc = vec3(SCREEN_UV * 2.0 - 1.0, depth);
    #endif
    vec4 world = INV_VIEW_MATRIX * INV_PROJECTION_MATRIX * vec4(ndc, 1.0);
    vec2 uv = (world.xz / world.w - map_origin) / map_extent;
    float inside = step(0.0, uv.x) * step(0.0, uv.y) * step(uv.x, 1.0) * step(uv.y, 1.0);
    float visible = texture(visibility_mask, uv).r * inside;
    ALBEDO = vec3(0.005, 0.009, 0.016);
    // 지형과 건물은 어둡게 남깁니다. 적 유닛 자체의 숨김은 서버 HIDE가 담당합니다.
    ALPHA = (1.0 - smoothstep(0.0, 1.0, visible)) * darkness;
}
