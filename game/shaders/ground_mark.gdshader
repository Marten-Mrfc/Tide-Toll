shader_type spatial;
// Marks printed on the ground round the town's buildings (GroundMarks.cs), ONE mesh: soft drop shadows and inked
// yard outlines. Each mark is a quad a little larger than its rounded rectangle:
//   UV = point relative to the rectangle's centre (metres), UV2 = its half size (metres),
//   COLOR = rgb fill colour, a = fill strength,  CUSTOM0 = (kind: 0 shadow, 1 yard; corner radius m; gap x, gap y m)
// The yard's ink line is about 1.3 screen pixels wide at any zoom (metres_per_px from C#), and leaves a gap at the
// door (the gap point, relative to the centre), so the path runs in.
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;
uniform vec4 ink : source_color = vec4(0.169, 0.149, 0.125, 1.0);
uniform float metres_per_px = 0.25;
uniform float line_px = 1.3;
uniform float line_alpha = 0.75;
uniform float shadow_soft = 1.4;     // metres of soft edge on a drop shadow
varying vec4 fill;
varying vec4 info;
float sd_box(vec2 p, vec2 h, float r) { vec2 q = abs(p) - h + r; return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r; }
vec3 to_linear(vec3 c) { return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c)); }
void vertex() { fill = vec4(to_linear(COLOR.rgb), COLOR.a); info = CUSTOM0; }
void fragment() {
	vec2 p = UV, h = UV2;
	float d = sd_box(p, h, min(info.y, min(h.x, h.y)));
	if (info.x < 0.5) {
		ALBEDO = ink.rgb;
		ALPHA = fill.a * (1.0 - smoothstep(-shadow_soft, shadow_soft * 0.6, d));
	} else {
		float aa = fwidth(d);
		float w = line_px * metres_per_px * 0.5;
		float line = 1.0 - smoothstep(w, w + aa, abs(d));
		line *= smoothstep(1.2, 1.6, length(p - info.zw));            // the gap at the door
		float inside = 1.0 - smoothstep(-aa, aa, d);
		vec3 c = mix(fill.rgb, ink.rgb, line);
		ALBEDO = c;
		ALPHA = max(inside * fill.a, line * line_alpha);
	}
}
