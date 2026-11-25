shader_type spatial;
// Signs on the water, drawn over everything: one MultiMesh of flat 1 x 1 quads scaled per instance to a size in
// screen pixels (RiverLayer). INSTANCE_CUSTOM: x = kind, y = fill 0..1, z = alpha.
//   0 = a boat waiting for its turn: a patience ring (blue arc = patience left, the spent part dotted) round an
//       hourglass, on a paper disc; the arc turns orange and pulses when a third is left
//   1 = a boat held by a crossing it can never pass (mast too tall): the same ring round a mast crossed out
//   2 = a boat turned back here: a vermillion diamond with a paper cross, fading
//   3 = a ferry's crossing: a dotted ink line along the quad's X (fill = number of dots)
// Shape first, colour second: every state has its own glyph (Art tab, "Reading from above").
render_mode unshaded, blend_mix, depth_draw_never, depth_test_disabled, cull_disabled, shadows_disabled, fog_disabled;
uniform vec4 ink : source_color = vec4(0.169, 0.149, 0.125, 1.0);
uniform vec4 paper : source_color = vec4(0.949, 0.918, 0.847, 1.0);
uniform vec4 info : source_color = vec4(0.0, 0.447, 0.698, 1.0);     // #0072B2 boat and water info
uniform vec4 warn : source_color = vec4(0.902, 0.624, 0.0, 1.0);     // #E69F00 problem, slow pulse
uniform vec4 stop : source_color = vec4(0.835, 0.369, 0.0, 1.0);     // #D55E00 blocked, fast pulse
varying vec3 custom;

void vertex() {
	custom = INSTANCE_CUSTOM.xyz;
}

float band(float d, float px) { return 1.0 - smoothstep(0.5 * px, 1.5 * px, d); }

void fragment() {
	vec2 p = UV - 0.5;
	float px = max(fwidth(UV.x), fwidth(UV.y));
	float kind = custom.x, fill = clamp(custom.y, 0.0, 1.0), alpha = custom.z;
	vec3 c = paper.rgb;
	float a = 0.0;
	if (kind < 1.5) {
		float r = length(p);
		float disc = 1.0 - smoothstep(0.46 - px, 0.46, r);
		float rim = band(abs(r - 0.455), px * 1.6);
		// Patience: clockwise from the top.
		float ang = fract(atan(p.x, -p.y) / 6.28318);
		float inArc = step(ang, fill) * band(abs(r - 0.38), 0.07 + px);
		float spent = (1.0 - step(ang, fill)) * band(abs(r - 0.38), px * 1.2) * step(0.5, fract(ang * 24.0));
		bool low = fill < 0.34;
		float pulse = low ? 0.75 + 0.25 * sin(TIME * 6.0) : 1.0;
		vec3 arc = low ? warn.rgb : info.rgb;
		c = paper.rgb;
		c = mix(c, arc * pulse + paper.rgb * (1.0 - pulse), inArc);
		c = mix(c, ink.rgb, max(spent * 0.6, rim));
		float glyph = 0.0;
		if (kind < 0.5) {
			// Hourglass: two triangles meeting in the middle, with a bar top and bottom.
			float tri = step(abs(p.x), abs(p.y) * 0.75 + px) * step(abs(p.y), 0.19);
			float bars = band(abs(abs(p.y) - 0.2), px * 1.4) * step(abs(p.x), 0.17);
			glyph = max(tri, bars);
			c = mix(c, ink.rgb, glyph);
		} else {
			// A mast and yard, crossed out in vermillion: "too tall for this crossing".
			float mast = step(abs(p.x), 0.025 + px * 0.5) * step(abs(p.y + 0.01), 0.22);
			float yard = step(abs(p.y + 0.12), 0.022 + px * 0.5) * step(abs(p.x), 0.13);
			c = mix(c, ink.rgb, max(mast, yard));
			float x1 = band(abs(p.x - p.y) * 0.7071, 0.035 + px), x2 = band(abs(p.x + p.y) * 0.7071, 0.035 + px);
			float cross = max(x1, x2) * step(max(abs(p.x), abs(p.y)), 0.2);
			c = mix(c, stop.rgb, cross);
		}
		a = disc;
	} else if (kind > 2.5) {
		float dots = step(0.5, fract(UV.x * custom.y));
		c = ink.rgb;
		a = dots * (1.0 - smoothstep(0.25, 0.5, abs(p.y))) * 0.8;
	} else {
		// Turned back: a diamond, vermillion with an ink edge and a paper cross.
		float d = abs(p.x) + abs(p.y);
		float body = 1.0 - smoothstep(0.46 - px, 0.46, d);
		float edge = band(abs(d - 0.45), px * 1.6);
		float x1 = band(abs(p.x - p.y) * 0.7071, 0.03 + px), x2 = band(abs(p.x + p.y) * 0.7071, 0.03 + px);
		float cross = max(x1, x2) * step(d, 0.26);
		c = mix(mix(stop.rgb, paper.rgb, cross), ink.rgb, edge);
		a = body;
	}
	ALBEDO = c;
	ALPHA = a * alpha;
}
