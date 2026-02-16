shader_type canvas_item;

uniform float lens_radius : hint_range(0.0, 0.5) = 0.35;
uniform float alpha : hint_range(0.0, 1.0) = 0.92;
uniform float edge_softness : hint_range(0.0, 0.05) = 0.005;
uniform float aspect_ratio = 1.777;

void fragment() {
    vec2 centered = UV - vec2(0.5);
    centered.x *= aspect_ratio;
    float dist = length(centered);
    float outer = smoothstep(lens_radius - edge_softness, lens_radius + edge_softness, dist);
    COLOR = vec4(0.0, 0.0, 0.0, alpha * outer);
}
