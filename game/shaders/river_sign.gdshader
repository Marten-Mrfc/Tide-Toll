shader_type spatial;
// Signs on the water, drawn over everything: one MultiMesh of flat 1 x 1 quads scaled per instance to a size in
// screen pixels (RiverLayer, CrossingLayer). INSTANCE_CUSTOM: x = kind, y = fill 0..1, z = alpha, w = extra (per kind).
//   0 = a boat waiting for its turn: a patience ring (blue arc = patience left, the spent part dotted) round an
//       hourglass, on a paper disc; the arc turns orange and pulses when a third is left
//   1 = a boat held by a crossing it can never pass (mast too tall): the same ring round a mast crossed out
//   2 = a boat turned back here: a vermillion diamond with a paper cross, fading
//   3 = a ferry's crossing: a dotted ink line along the quad's X (fill = number of dots)
//   4 = a drawbridge's signal, read from above: a paper disc with the signal arm as a thick bar, lying flat across
//       (fill 0: the walkers' turn, ink) or standing along the river (fill 1: the boats' turn, blue), turning between
//   5 = a ferry at overview zoom: the rope as a bold dotted line between two posts, the raft (a plank square) on it at
//       fill (0..1 along the quad), w = the raft's length as a share of the quad
//   6 = a footbridge at overview zoom: a plank deck (wood, an ink rail each side, planks across)
//   7 = a drawbridge at overview zoom: the deck with railings and the gantry at the lift's hinge (west end): a thick ink
//       portal across the deck, its posts standing out past the rails; fill = how far the leaf is up (the leaf's part,
//       w of the length from the west, opens to water); the deck is half as tall in the quad as for kind 6
//   8 = a closed ford: a vermillion bar across the river with an ink edge and paper hatching
// Shape first, colour second: every state has its own glyph (Art tab, "Reading from above").
render_mode unshaded, blend_mix, depth_draw_never, depth_test_disabled, cull_disabled, shadows_disabled, fog_disabled;
uniform vec4 ink : source_color = vec4(0.169, 0.149, 0.125, 1.0);
uniform vec4 paper : source_color = vec4(0.949, 0.918, 0.847, 1.0);
uniform vec4 info : source_color = vec4(0.0, 0.447, 0.698, 1.0);     // #0072B2 boat and water info
uniform vec4 warn : source_color = vec4(0.902, 0.624, 0.0, 1.0);     // #E69F00 problem, slow pulse
uniform vec4 stop : source_color = vec4(0.835, 0.369, 0.0, 1.0);     // #D55E00 blocked, fast pulse
uniform vec4 wood : source_color = vec4(0.722, 0.584, 0.408, 1.0);   // #B89568 planks
varying vec4 custom;

void vertex() {
	custom = INSTANCE_CUSTOM;
}

float band(float d, float px) { return 1.0 - smoothstep(0.5 * px, 1.5 * px, d); }

void fragment() {
	vec2 p = UV - 0.5;
	float px = max(fwidth(UV.x), fwidth(UV.y));
	float kind = custom.x, fill = clamp(custom.y, 0.0, 1.0), alpha = custom.z;
	vec3 c = paper.rgb;
	float a = 0.0;
	// Pixels per quad height along x: the quad's length in units of its height (for the long marks, kinds 5 to 8).
	float aspect = max(1.0, fwidth(UV.y) / max(fwidth(UV.x), 1e-6));
	vec2 q = vec2(UV.x * aspect, p.y);                // x in quad heights, y -0.5..0.5
	if (kind > 4.5) {
		float ex = custom.w;
		if (kind < 5.5) {
			// Ferry: posts at both ends, the rope dotted between them, the raft on it.
			float rope = step(abs(p.y), 0.10 + px) * step(0.5, fract(q.x * 3.0));
			float pa = 1.0 - smoothstep(0.26, 0.26 + px * 2.0, length(vec2(q.x - 0.3, p.y)));
			float pb = 1.0 - smoothstep(0.26, 0.26 + px * 2.0, length(vec2(q.x - (aspect - 0.3), p.y)));
			float raftX = abs(UV.x - fill) / max(ex * 0.5, 1e-3);
			float raft = step(raftX, 1.0) * step(abs(p.y), 0.36);
			float raftEdge = raft * max(step(0.80, raftX), step(0.28, abs(p.y)));
			float planks = raft * step(0.8, fract((UV.x - fill) / max(ex, 1e-3) * 4.0 + 0.5));
			c = mix(ink.rgb, wood.rgb, raft);
			c = mix(c, ink.rgb, max(raftEdge, planks * 0.5));
			a = max(max(rope, max(pa, pb)), raft);
		} else if (kind < 6.5 || kind > 7.5) {
			if (kind > 7.5) {
				// Closed ford: a vermillion bar with an ink edge and paper hatching across.
				float body = step(abs(p.y), 0.42);
				float edge = body * step(0.32, abs(p.y));
				float hatch = step(0.6, fract((q.x + p.y) * 2.2));
				c = mix(mix(stop.rgb, paper.rgb, hatch * 0.85), ink.rgb, edge);
				a = body;
			} else {
				// Footbridge: a plank deck with a rail each side.
				float deck = step(abs(p.y), 0.44);
				float rail = deck * step(0.30, abs(p.y));
				float plank = deck * step(0.78, fract(q.x * 2.2));
				c = mix(wood.rgb, ink.rgb, max(rail, plank * 0.55));
				a = deck;
			}
		} else {
			// Drawbridge: deck in the quad's middle third, gantry at the hinge, leaf open while raised.
			float deck = step(abs(p.y), 0.29);
			float rail = deck * step(0.20, abs(p.y));
			float plank = deck * step(0.8, fract(q.x * 2.2));
			bool onLeaf = UV.x < ex;
			float open = onLeaf ? smoothstep(0.25, 0.6, fill) : 0.0;
			float gx = q.x - 0.35;
			float gantry = step(abs(gx), 0.16) * step(abs(p.y), 0.48);          // the cross beam over the deck
			float posts = step(abs(gx), 0.26) * step(0.33, abs(p.y)) * step(abs(p.y), 0.49);
			// The open leaf: dashed outline only, so the water shows through.
			float dash = rail * step(0.5, fract(q.x * 2.0));
			float solid = deck * (1.0 - open);
			c = mix(wood.rgb, ink.rgb, max(rail, plank * 0.55));
			float frame = max(gantry, posts);
			c = mix(c, ink.rgb, frame);
			a = max(max(solid, dash * open), frame);
		}
		a = clamp(a, 0.0, 1.0);
	} else if (kind < 1.5) {
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
	} else if (kind > 3.5) {
		float r = length(p);
		float disc = 1.0 - smoothstep(0.46 - px, 0.46, r);
		float rim = band(abs(r - 0.455), px * 1.6);
		float ang = fill * 1.5708;
		vec2 q = vec2(cos(ang) * p.x + sin(ang) * p.y, -sin(ang) * p.x + cos(ang) * p.y);
		float bar = step(abs(q.y), 0.075) * step(abs(q.x), 0.34);
		float post = 1.0 - smoothstep(0.06, 0.06 + px, r);
		vec3 arm = mix(ink.rgb, info.rgb, step(0.5, fill));
		c = mix(paper.rgb, arm, bar);
		c = mix(c, ink.rgb, max(rim, post));
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
