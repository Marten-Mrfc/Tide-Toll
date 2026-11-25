// Rain over the map page (Chapter I): slanted streaks in two layers, a near one and a fainter far one, drawn in screen
// pixels so they keep their size at any zoom; the clock stops while the game is paused (rain hangs still), and the whole
// layer fades in and out with the weather's rain level. Procedural: no texture, nothing computed while it is dry.
shader_type canvas_item;

uniform float rain = 0.0;            // 0..1: the weather's rain level (strength, ramped)
uniform float clock = 0.0;           // seconds of rain time (stops while paused)
uniform float px_scale = 1.0;        // interface scale: streaks are sized for 1080p
uniform vec2 size_px = vec2(800.0, 600.0);
uniform vec4 tint : source_color = vec4(0.953, 0.969, 0.980, 1.0);   // #F3F7FA, the toy's rain
uniform vec4 shade : source_color = vec4(0.169, 0.149, 0.125, 1.0);  // ink, for the faint wash under the streaks

float h21(vec2 p) { vec3 q = fract(vec3(p.xyx) * 0.1031); q += dot(q, q.yzx + 33.33); return fract((q.x + q.y) * q.z); }

// One layer of streaks: cells of w x l pixels, one streak in most of them at a random column and phase.
float streaks(vec2 px, float w, float l, float speed, float len, float width) {
	vec2 p = px;
	p.x += p.y * 0.26;                 // slanted, falling to the left from the north-east
	p.y -= clock * speed;
	vec2 cell = vec2(w, l);
	vec2 id = floor(p / cell);
	vec2 f = fract(p / cell);
	float on = step(h21(id), 0.62);
	float x0 = 0.15 + 0.7 * h21(id + 7.31);
	float y0 = h21(id + 3.17) * (1.0 - len);
	float dx = abs(f.x - x0) * w;
	float line = 1.0 - smoothstep(width * 0.5, width * 0.5 + 1.0, dx);
	float along = smoothstep(y0, y0 + len * 0.25, f.y) * (1.0 - smoothstep(y0 + len * 0.6, y0 + len, f.y));
	return on * line * along;
}

void fragment() {
	vec2 px = UV * size_px / px_scale;
	float near = streaks(px, 22.0, 90.0, 520.0, 0.55, 1.3 * px_scale);
	float far = streaks(px * 1.6 + vec2(37.0, 11.0), 16.0, 70.0, 330.0, 0.45, 1.0 * px_scale);
	float a = (near * 0.55 + far * 0.28) * rain;
	// A faint grey wash, so the page under a shower reads dimmer even between the streaks.
	vec3 col = mix(shade.rgb, tint.rgb, step(0.001, near + far));
	COLOR = vec4(col, max(a, 0.06 * rain));
}
