shader_type spatial;
// Flow-preview routes (rendering proof 7): ONE ArrayMesh holds every route, arrow and blocked mark, rebuilt only when
// a new preview result arrives, never per frame. Widths are in screen pixels, so they stay constant at any zoom.
//   VERTEX = point on the centre line (map-local metres, a little above the ground)
//   NORMAL = unit offset direction in the ground plane; UV2.x = offset length in screen pixels
//            (NORMAL is stored unit length, so the magnitude travels in UV2.x)
//   UV2.y  = strength 0..1 (parts of an "after" route that the old route already follows are drawn fainter)
//   UV     = x: metres along the route (for dashes), y: side -1..1 across the ribbon
//   COLOR  = rgb, a = style: 1 solid ("after"), 0.5 dashed ("now"), 0.25 dotted (blocked), 0 arrow head
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;
uniform float metres_per_px = 0.25;   // camera size / viewport height; set on zoom or resize only
uniform float march = 0.0;            // dash animation in seconds (driven from C#)
uniform float opacity = 1.0;          // fades the whole preview in and out
varying vec4 col;
varying float along_px;
varying float side;
varying float strength;
vec3 to_linear(vec3 c) {
	// vertex colours arrive as sRGB hex values; ALBEDO is linear
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}
void vertex() {
	VERTEX += normalize(NORMAL) * UV2.x * metres_per_px;
	col = vec4(to_linear(COLOR.rgb), COLOR.a);
	along_px = UV.x / metres_per_px;
	side = UV.y;
	strength = UV2.y;
}
void fragment() {
	float style = col.a;
	float on = 1.0;
	if (style > 0.4 && style < 0.75) {
		// "now": 9 px dashes, 6 px gaps, marching slowly toward the destination
		float m = mod(along_px - march * 12.0, 15.0);
		on = 1.0 - smoothstep(8.0, 9.0, m);
	} else if (style > 0.1 && style <= 0.4) {
		// blocked: short dots
		float m = mod(along_px - march * 8.0, 9.0);
		on = 1.0 - smoothstep(3.0, 4.0, m);
	}
	float edge = style < 0.1 ? 1.0 : 1.0 - smoothstep(0.7, 1.0, abs(side));
	ALBEDO = col.rgb;
	ALPHA = on * edge * strength * opacity;
}
