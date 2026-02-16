shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, unshaded;

// 0.0 = no damage, 1.0 = fully broken
uniform float damage_ratio : hint_range(0.0, 1.0) = 0.0;

// Crack line color (dark for crack lines)
uniform vec4 crack_color : source_color = vec4(0.05, 0.05, 0.05, 1.0);

// Number of crack stages (like Minecraft's 10 stages)
const int STAGES = 5;

// Pseudo-random hash
float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453123);
}

// Value noise
float noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash(i);
    float b = hash(i + vec2(1.0, 0.0));
    float c = hash(i + vec2(0.0, 1.0));
    float d = hash(i + vec2(1.0, 1.0));
    return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

// Fractal noise for crack patterns
float crack_noise(vec2 uv, float scale) {
    float n = 0.0;
    n += noise(uv * scale) * 0.5;
    n += noise(uv * scale * 2.0) * 0.25;
    n += noise(uv * scale * 4.0) * 0.125;
    return n;
}

void fragment() {
    if (damage_ratio < 0.01) {
        // No damage - fully transparent
        ALPHA = 0.0;
    } else {
        // Determine current crack stage
        float stage = damage_ratio * float(STAGES);

        // Generate crack lines using noise
        float n1 = crack_noise(UV, 8.0);
        float n2 = crack_noise(UV + vec2(3.7, 1.3), 12.0);
        float n3 = crack_noise(UV + vec2(7.1, 5.9), 16.0);

        // Threshold: more cracks appear at higher damage
        float threshold = 1.0 - damage_ratio;

        // Create crack lines from noise
        float crack1 = step(threshold + 0.15, abs(n1 - 0.5) * 2.0) * step(0.2, stage);
        float crack2 = step(threshold + 0.1, abs(n2 - 0.5) * 2.0) * step(1.5, stage);
        float crack3 = step(threshold, abs(n3 - 0.5) * 2.0) * step(3.0, stage);

        float cracks = max(crack1, max(crack2, crack3));

        // Add darkening around cracks
        float darken = damage_ratio * 0.3;

        if (cracks > 0.5) {
            ALBEDO = crack_color.rgb;
            ALPHA = 0.9;
        } else if (darken > 0.05) {
            ALBEDO = vec3(0.0);
            ALPHA = darken;
        } else {
            ALPHA = 0.0;
        }
    }
}
