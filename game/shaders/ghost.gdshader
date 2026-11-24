shader_type spatial;
// Tool ghosts: one MultiMesh of flat 1 x 1 quads (scaled per instance to cells), drawn over the map.
// INSTANCE_CUSTOM.x picks the pattern, COLOR is the instance colour:
//   0 = will be built: pale fill with a dotted outline whose dots march (player preview)
//   1 = cannot be built: diagonal hatching with a cross (shape, not only colour)
//   2 = will be removed: hatching with a solid outline
//   3 = a ring (the erase brush)
render_mode unshaded, blend_mix, depth_draw_never, depth_test_disabled, cull_disabled, shadows_disabled, fog_disabled;
uniform vec4 ink : source_color = vec4(0.169, 0.149, 0.125, 1.0);
varying vec4 tint;
varying float kind;
vec3 to_linear(vec3 c) {
	// vertex colours arrive as sRGB hex values; ALBEDO is linear
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}
void vertex() {
	tint = vec4(to_linear(COLOR.rgb), COLOR.a);
	kind = INSTANCE_CUSTOM.x;
}
void fragment() {
	vec2 p = UV - 0.5;                       // -0.5..0.5 across the quad
	vec2 fw = fwidth(UV);
	float px = max(fw.x, fw.y);              // one screen pixel in quad units
	float a = 0.0;
	vec3 c = tint.rgb;
	float border = 0.5 - max(abs(p.x), abs(p.y));        // distance to the edge, quad units
	if (kind < 0.5) {
		float line = 1.0 - smoothstep(2.0 * px, 3.0 * px, border);
		float perim = (p.x + p.y) / px;                  // marching dots along the outline
		float dots = step(0.5, fract(perim / 8.0 - TIME * 1.5));
		a = max(0.62, line * dots);
		c = mix(c, ink.rgb, line * dots * 0.85);
	} else if (kind < 1.5) {
		float hatch = step(0.55, fract((p.x - p.y) / (px * 7.0)));
		float cross = 1.0 - smoothstep(1.2 * px, 2.2 * px, min(abs(p.x - p.y), abs(p.x + p.y)));
		cross *= step(abs(p.x), 0.38);
		float line = 1.0 - smoothstep(1.0 * px, 2.0 * px, border);
		a = max(max(0.30 + 0.45 * hatch, cross), line);
	} else if (kind < 2.5) {
		float hatch = step(0.5, fract((p.x + p.y) / (px * 6.0)));
		float line = 1.0 - smoothstep(1.5 * px, 2.5 * px, border);
		a = max(0.25 + 0.5 * hatch, line);
		c = mix(c, ink.rgb, line * 0.4);
	} else {
		float r = length(p);
		float ring = 1.0 - smoothstep(1.5 * px, 2.5 * px, abs(r - 0.47 + 2.0 * px));
		float dots = step(0.35, fract(atan(p.y, p.x) * 6.0 / 3.14159 + TIME * 0.5));
		a = ring * dots;
	}
	ALBEDO = c;
	ALPHA = a * tint.a;
}
