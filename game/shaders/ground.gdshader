shader_type spatial;
// Living Map ground for the whole 256 m map page: ONE plane, ONE draw call (rendering proof 1 and 2).
// Wear from the simulation -> five gouache ground textures blended at the sim's thresholds, sampled in map-local
// metres (world space: nothing swims when the camera moves or turns), with a paper grain on top. The river is an
// analytic band from a per-row centre line, so its ink bank line is crisp at any zoom; the ford is a shallow band
// with the bed showing; crop cells are drawn as a field of rows that trails trample down to earth.
// LIT with a custom light(): unshadowed ground keeps the texture colour exactly, shadows of houses and trees still fall on it.
render_mode specular_disabled, diffuse_lambert, ambient_light_disabled;

uniform sampler2D wear_tex : filter_linear, repeat_disable;                 // RG8 128x128: R = wear/100, G = hollow-way flag
uniform sampler2D mask_tex : filter_nearest, repeat_disable;                // RG8 128x128: R = ford, G = crop (255 = set)
uniform sampler2D river_tex : filter_linear, repeat_disable;                // RF 1x128: river centre x per row, metres
uniform sampler2D ramp_tex : source_color, filter_linear, repeat_disable;   // wear-ramp.png, u = wear
uniform sampler2D g0 : source_color, filter_linear_mipmap, repeat_enable;   // grass
uniform sampler2D g1 : source_color, filter_linear_mipmap, repeat_enable;   // trampled
uniform sampler2D g2 : source_color, filter_linear_mipmap, repeat_enable;   // trail
uniform sampler2D g3 : source_color, filter_linear_mipmap, repeat_enable;   // track
uniform sampler2D g4 : source_color, filter_linear_mipmap, repeat_enable;   // hollow way
uniform sampler2D season_tex : filter_linear, repeat_disable;
uniform sampler2D wetmud_tex : filter_linear, repeat_disable;              // R8 128x128: how wet each mud cell is drawn (rain, Chapter I)
uniform sampler2D gravel_tex : filter_linear, repeat_disable;              // R8 256x256: the gravel strip's edge ramp (0.5 = edge)              // RG8 128x128: R = ice wear/100, G = flood band
uniform sampler2D paper_tex : source_color, filter_linear_mipmap, repeat_enable;
uniform sampler2D water_tex : source_color, filter_linear_mipmap, repeat_enable;

uniform float map_size = 256.0;
uniform float cells = 128.0;
uniform float half_w = 5.0;          // river half width, metres
uniform float tile1 = 28.0;          // metres per texture tile, scale A
uniform float tile2 = 11.3;          // scale B (rotated, not a multiple of A, so no visible repeat)
uniform float ramp_mix = 0.2;       // the wear ramp's colour laid lightly over bare earth
uniform float grain = 0.07;
uniform float time_s = 0.0;          // animation seconds from C#: a paused game freezes the water
uniform float overlay = 0.0;
uniform float flood = 0.0;           // 0..1, eased: the spring flood's wash fills the band outward from the bank
uniform float mud = 0.0;             // 0..1: the band is mud after a flood (worn paths there darken)
uniform float ice = 0.0;             // 0..1: the river is frozen
uniform float band_mark = 0.0;      // 1 with the river rules: the flood band's outer edge drawn faintly dotted on the dry map
uniform float band_m = 8.0;          // flood band width beyond the bank, metres         // 0..1: wear heat map laid over the page like a tracing-paper sheet
uniform vec4 ink : source_color = vec4(0.169, 0.149, 0.125, 1.0);
uniform vec4 water_col : source_color = vec4(0.357, 0.498, 0.584, 1.0);
uniform vec4 deep_col : source_color = vec4(0.247, 0.373, 0.459, 1.0);
uniform vec4 crop_col : source_color = vec4(0.788, 0.647, 0.353, 1.0);   // reed and thatch
uniform vec4 crop_dark : source_color = vec4(0.663, 0.722, 0.431, 1.0);  // bent grass between the rows
uniform vec4 paper : source_color = vec4(0.949, 0.918, 0.847, 1.0);
uniform vec4 heat0 : source_color = vec4(0.949, 0.925, 0.796, 1.0);
uniform vec4 heat1 : source_color = vec4(0.941, 0.765, 0.353, 1.0);
uniform vec4 heat2 : source_color = vec4(0.878, 0.455, 0.173, 1.0);
uniform vec4 heat3 : source_color = vec4(0.690, 0.165, 0.180, 1.0);
uniform vec4 heat4 : source_color = vec4(0.298, 0.063, 0.212, 1.0);
uniform vec4 ice_col : source_color = vec4(0.686, 0.769, 0.808, 1.0);   // pale ice, #AFC4CE
uniform vec4 mud_col : source_color = vec4(0.369, 0.290, 0.212, 1.0);   // #5E4A36
uniform float rain_wet = 0.0;        // 0..1, eased: the ground's wetness after rain (Chapter I)
uniform float mud_on = 0.0;          // any mud from rain to draw
uniform vec4 rain_mud_col : source_color = vec4(0.310, 0.243, 0.184, 1.0); // #4F3E2F, the toy's mud
uniform vec4 wet_col : source_color = vec4(0.498, 0.561, 0.600, 1.0);      // #7F8F99, the toy's wet multiply
uniform vec4 glint_col : source_color = vec4(0.835, 0.886, 0.910, 1.0);    // #D5E2E8, puddle glints
uniform float gravel_on = 0.0;       // any gravel on the map
uniform vec4 gravel_wash : source_color = vec4(0.933, 0.918, 0.863, 1.0);  // #EEEADC, the toy's wash
uniform vec4 gravel_stone : source_color = vec4(0.467, 0.475, 0.435, 1.0); // #77796F
uniform vec4 gravel_rim : source_color = vec4(0.353, 0.361, 0.333, 1.0);   // #5A5C55
uniform float cobble_on = 0.0;       // any cobbles on the map (the town game)
uniform sampler2D cobble_tex : filter_linear, repeat_disable;              // R8 256x256: the cobbled strip's edge ramp, as gravel_tex
uniform vec4 cobble_stone : source_color = vec4(0.537, 0.522, 0.494, 1.0); // #89857E
uniform vec4 cobble_joint : source_color = vec4(0.302, 0.278, 0.247, 1.0); // #4D473F
uniform float crowd_on = 0.0;        // any crowded road (the town game)
uniform sampler2D crowd_tex : filter_linear, repeat_disable;               // R8 128x128: how far over its comfort a road is, 0..1
uniform vec4 crowd_col : source_color = vec4(0.878, 0.455, 0.173, 1.0);    // the heat map's orange, laid on lightly
uniform vec3 shadow_tint = vec3(0.70, 0.74, 0.86);
uniform float bank_px = 1.6;         // ink bank line width in screen pixels

varying vec2 lp;                     // map-local metres: x east, y south, 0..map_size

vec2 rot(vec2 p, float a) { float c = cos(a), s = sin(a); return vec2(c * p.x - s * p.y, s * p.x + c * p.y); }
vec3 two(sampler2D t, vec2 p) { return mix(texture(t, p / tile1).rgb, texture(t, rot(p, 0.9) / tile2).rgb, 0.45); }

vec3 heat(float w) {
	// the toy's heat stops: 0, 10, 25, 50, 85 (wear 0..100)
	if (w < 0.10) return mix(heat0.rgb, heat1.rgb, w / 0.10);
	if (w < 0.25) return mix(heat1.rgb, heat2.rgb, (w - 0.10) / 0.15);
	if (w < 0.50) return mix(heat2.rgb, heat3.rgb, (w - 0.25) / 0.25);
	return mix(heat3.rgb, heat4.rgb, clamp((w - 0.50) / 0.35, 0.0, 1.0));
}

void vertex() { lp = VERTEX.xz + vec2(map_size * 0.5); }

// Value noise in map space (no texture, so it never repeats and never swims): 0..1.
float hash12(vec2 p) { vec3 q = fract(vec3(p.xyx) * 0.1031); q += dot(q, q.yzx + 33.33); return fract((q.x + q.y) * q.z); }
float vnoise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash12(i), hash12(i + vec2(1.0, 0.0)), u.x), mix(hash12(i + vec2(0.0, 1.0)), hash12(i + vec2(1.0, 1.0)), u.x), u.y);
}
float wear_at(vec2 m) { return texture(wear_tex, m / map_size).r; }

void fragment() {
	vec2 uv = lp / map_size;
	vec2 wh = texture(wear_tex, uv).rg;
	// "The paths are the art" (Art tab): the trail is ground worn by feet, not a road. The wear grid itself is never
	// changed here; only how it is drawn. All lengths in metres (one cell is 2 m).
	// 1. Edges that wander: the lookup is warped by low-frequency noise (under half a cell), so cell squares and
	//    straight vector strokes never show.
	float cm = map_size / cells;
	vec2 warp = vec2(vnoise(lp / 9.0), vnoise(lp / 9.0 + 31.7)) - 0.5;
	warp += (vec2(vnoise(lp / 3.1 + 7.3), vnoise(lp / 3.1 + 19.1)) - 0.5) * 0.45;
	vec2 m = lp + warp * cm * 0.9;
	// 2. Three readings of the same wear: the core (softened over under a cell), a wide spread (the trampled fringe
	//    walkers leave beside a trail) and a broad average (only wide, busy trunks stay high in it). The ring samples
	//    also give the wear's slope, which points across the trail: strokes are drawn along the walking direction.
	float core = wear_at(m) * 0.4;
	float spread = 0.0, broad = 0.0;
	vec2 grad = vec2(0.0);
	for (int k = 0; k < 6; k++) {
		float a = float(k) * 1.0471976 + 0.35;
		vec2 d = vec2(cos(a), sin(a));
		core += wear_at(m + d * cm * 0.55) * 0.1;
		float wn = wear_at(m + d * cm * 1.2);
		spread += wear_at(m + d * cm * 2.2) + wn;
		broad += wear_at(m + d * cm * 1.0);
		grad += d * wn;
	}
	spread /= 12.0;
	broad = (broad + wear_at(m) * 2.0) / 8.0;
	vec2 across = grad / max(length(grad), 1e-3);
	vec2 along = vec2(-across.y, across.x);
	// Strokes: value noise stretched 8:1 along the path (fixed on the map, so they never swim).
	float stroke = vnoise(vec2(dot(lp, along) / 1.7, dot(lp, across) / 0.21));
	stroke = mix(0.5, stroke, smoothstep(0.004, 0.03, length(grad)));   // no direction on open ground
	float n1 = vnoise(lp / 2.2), n2 = vnoise(lp / 0.8), n3 = vnoise(lp / 5.5 + 3.1);
	// Meadow, then bent grass: a wide, pale, yellower fringe of flattened blades with strokes along the walking
	// direction, strongest by the trail and fading out raggedly.
	float fringe = smoothstep(0.03, 0.22, max(spread * 1.25, core)) * (0.62 + 0.38 * n3);
	vec3 grass = two(g0, lp);
	vec3 flattened = mix(two(g1, lp), crop_dark.rgb, 0.30) * (1.0 + 0.05 * n2);
	flattened *= 0.93 + 0.16 * stroke;
	vec3 c = mix(grass, flattened, fringe);
	// Bare earth, blended by height: the grass has a height (noise), and wear wears it down to the soil from the lowest
	// tufts up. A lightly used trail is a broken line of soil through grass; the busier it is, the wider and more
	// solid the soil. The sim's thresholds stay the anchors: no soil below wear 25 (trail), solid soil only
	// from about 70 (a track), so the grass breaks through all along its edges.
	float height = 0.50 * n2 + 0.32 * n1 + 0.18 * stroke;
	float worn = clamp((core - 0.25) / 0.50, 0.0, 1.0) * 1.3 - 0.15;
	float bare = smoothstep(height - 0.10, height + 0.10, worn);
	// The soil is light and mottled (trail earth lifted toward the paper), streaked along the walking direction.
	vec3 earth = mix(two(g2, lp), paper.rgb, 0.16) * (0.92 + 0.10 * n2 + 0.06 * n1);
	earth *= 0.96 + 0.08 * stroke;
	c = mix(c, earth, bare);
	// Dark packed earth only where a trail is both well worn and wide (a trunk) or the sim marked a hollow way.
	float packed = smoothstep(0.74, 0.92, broad + (n1 - 0.5) * 0.10) * smoothstep(0.55, 0.70, core);
	float w4 = smoothstep(0.3, 0.7, wh.g) * smoothstep(0.50, 0.65, core);
	c = mix(c, mix(two(g3, lp), earth, 0.25 * n1), packed * 0.8);
	c = mix(c, two(g4, lp), w4);
	c = mix(c, texture(ramp_tex, vec2(core, 0.5)).rgb, ramp_mix * bare * (1.0 - w4));
	float w = core;
	// Rain's mud: wet trail, the same worn ground pulled toward wet earth and darkened, a little more solid over the
	// trail's grassy edges, with a few still puddle glints on the busiest cells. It dries cell by cell (the C# side).
	if (mud_on > 0.0) {
		float mu = texture(wetmud_tex, m / map_size).r * smoothstep(0.17, 0.30, core);
		if (mu > 0.002) {
			float solid = mu * (0.45 + 0.55 * bare);
			c = mix(c, mix(c, rain_mud_col.rgb, 0.34), solid);
			c *= vec3(1.0 - 0.14 * solid, 1.0 - 0.14 * solid, 1.0 - 0.10 * solid);
			// Few and small: still puddles only in the wettest middle of busy trails.
			float glint = smoothstep(0.86, 0.90, vnoise(lp / 0.75 + 17.0)) * smoothstep(0.55, 0.75, core) * smoothstep(0.7, 1.0, mu);
			c = mix(c, glint_col.rgb, glint * 0.45);
		}
	}
	// Wet ground is darker everywhere on land: multiplied toward a cool grey with the wetness (the toy's 0.4).
	c *= mix(vec3(1.0), wet_col.rgb, 0.4 * rain_wet);

	// field: rows of ripe crop running east-west, trampled to earth where trails cross, with an ink edge
	vec2 mask = texture(mask_tex, uv).rg;
	float cell_m = map_size / cells;
	{
		float row = sin(lp.y * 6.2831853 / 1.25 + sin(lp.x * 0.35) * 0.4);
		vec3 crop = mix(crop_dark.rgb, crop_col.rgb, smoothstep(-0.35, 0.35, row));
		crop *= 0.94 + 0.12 * texture(g1, lp / 9.0).g;
		float keep = 1.0 - smoothstep(0.08, 0.35, w);
		c = mix(c, crop, mask.g * keep);
		// edge of the crop area: compare with the mask a few decimetres away in four directions (a thin edge line)
		float e = 0.0;
		vec2 o = vec2(0.14 * cell_m / map_size, 0.0);
		e += abs(texture(mask_tex, uv + o.xy).g - mask.g);
		e += abs(texture(mask_tex, uv - o.xy).g - mask.g);
		e += abs(texture(mask_tex, uv + o.yx).g - mask.g);
		e += abs(texture(mask_tex, uv - o.yx).g - mask.g);
		c = mix(c, mix(c, ink.rgb, 0.6), clamp(e, 0.0, 1.0) * mask.g);
	}

	// Gravel: a pale stony strip with a thin dark rim (a pixel or two at any zoom), stones scattered on it that fade to
	// their mean when they would be smaller than a pixel. It never shows wear: walkers keep it a trail, not a scar.
	if (gravel_on > 0.0) {
		float gv = texture(gravel_tex, (lp + warp * cm * 0.18) / map_size).r - 0.5;
		float ga = max(fwidth(gv), 1e-4);
		float strip = smoothstep(-ga, ga, gv);
		float rim = 1.0 - smoothstep(ga * 0.6, ga * 2.2, abs(gv));
		vec2 sp = lp / 0.62;
		vec2 si = floor(sp);
		vec2 so = vec2(hash12(si + 3.1), hash12(si + 7.7)) - 0.5;
		float sd = length(fract(sp) - 0.5 - so * 0.45);
		float sr = 0.16 + 0.12 * hash12(si + 1.9);
		float stone = (1.0 - smoothstep(sr - 0.06, sr + 0.06, sd)) * step(0.3, hash12(si));
		float fine = 1.0 - smoothstep(0.08, 0.30, fwidth(lp.x));   // stones show from about 3 px a metre
		vec3 wash = gravel_wash.rgb * (0.95 + 0.08 * n2);
		vec3 gc = mix(mix(wash, gravel_stone.rgb, 0.18), mix(wash, gravel_stone.rgb, stone * 0.85), fine);
		c = mix(c, gc, strip);
		c = mix(c, gravel_rim.rgb, rim * 0.9);
	}

	// Cobbles: a darker grey strip of staggered square setts with ink joints, clearly not gravel's pale scatter.
	if (cobble_on > 0.0) {
		float cv = texture(cobble_tex, (lp + warp * cm * 0.12) / map_size).r - 0.5;
		float ca = max(fwidth(cv), 1e-4);
		float cstrip = smoothstep(-ca, ca, cv);
		float crim = 1.0 - smoothstep(ca * 0.6, ca * 2.4, abs(cv));
		vec2 q = lp / 0.75;
		q.x += 0.5 * mod(floor(q.y), 2.0);
		vec2 f = abs(fract(q) - 0.5);
		float jw = max(fwidth(q.x) * 0.9, 0.06);
		float joint = smoothstep(0.5 - jw, 0.5, max(f.x, f.y));
		float fine = 1.0 - smoothstep(0.10, 0.35, fwidth(lp.x));
		vec3 sett = cobble_stone.rgb * (0.88 + 0.22 * hash12(floor(q) + 4.2));
		vec3 cc = mix(mix(cobble_stone.rgb, cobble_joint.rgb, 0.25), mix(sett, cobble_joint.rgb, joint * 0.85), fine);
		c = mix(c, cc, cstrip);
		c = mix(c, cobble_joint.rgb, crim * 0.95);
	}
	// Crowding: a road over its comfort takes a warm wash, stronger the more crowded, only on the road itself.
	if (crowd_on > 0.0) {
		float cr = texture(crowd_tex, m / map_size).r * smoothstep(0.15, 0.35, core);
		c = mix(c, crowd_col.rgb, cr * 0.45);
	}

	// river: signed distance to the bank from the per-row centre line
	float cx = texture(river_tex, vec2(0.5, uv.y)).r;
	float d = abs(lp.x - cx) - half_w;               // < 0 in the water
	float aa = fwidth(d);
	float inside = 1.0 - smoothstep(-aa, aa, d);
	float ford = mask.r;
	vec2 wuv = vec2(lp.x / 40.0, lp.y / 40.0 - time_s * 0.012);
	vec3 wc = mix(water_col.rgb, texture(water_tex, wuv).rgb, 0.55);
	wc = mix(wc, deep_col.rgb, 0.45 * smoothstep(half_w * 0.15, -half_w * 0.6, d)); // deeper down the middle
	// engraved waterlines along the flow that drift slowly
	float k = lp.y * 0.9 - time_s * 0.35 + sin(lp.x * 0.5 + lp.y * 0.05) * 0.5 + (lp.x - cx) * 0.25;
	float line = smoothstep(0.965, 0.985, sin(k) * 0.5 + 0.5) * smoothstep(half_w, half_w - 1.2, abs(lp.x - cx));
	wc = mix(wc, wc * 0.62, line * 0.8 * (1.0 - ford) * (1.0 - ice));
	// ford: shallow, lighter water with the earth bed and pale ripples showing
	vec3 bed = two(g2, lp);
	vec3 shallow = mix(mix(water_col.rgb, bed, 0.45), paper.rgb, 0.12);
	float ripple = smoothstep(0.90, 0.97, sin(lp.y * 2.6 - time_s * 0.8 + sin(lp.x * 1.3) * 0.8) * 0.5 + 0.5);
	shallow = mix(shallow, paper.rgb, ripple * 0.25);
	wc = mix(wc, shallow, ford);
	// Winter ice: the water goes pale and still, with ink cracks; paths walked on the ice (the ice-wear layer, never the
	// wear grid) show as whiter scratched trails.
	if (ice > 0.0) {
		float n4 = vnoise(lp / 4.5), n5 = vnoise(lp / 1.7 + 11.0);
		vec3 ic = ice_col.rgb * (0.96 + 0.06 * n5);
		// Cracks: thin ink lines along a noise contour, about a pixel wide at any zoom, broken up by a second noise.
		float cn = abs(n4 - 0.5);
		float cw = max(fwidth(n4) * 1.1, 0.006);
		float crack = (1.0 - smoothstep(cw * 0.5, cw * 1.5, cn)) * step(0.40, vnoise(lp / 7.0 + 5.0));
		ic = mix(ic, ink.rgb, crack * 0.75);
		float trod = smoothstep(0.02, 0.22, texture(season_tex, uv).r);
		float scratch = step(0.66, vnoise(vec2(lp.x * 3.1, lp.y * 0.4) + 3.0)) * 0.6 + step(0.72, vnoise(vec2(lp.x * 0.4, lp.y * 3.1))) * 0.4;
		vec3 path = mix(vec3(0.97, 0.97, 0.95), ink.rgb, scratch * 0.35);
		ic = mix(ic, path, trod * 0.92);
		wc = mix(wc, ic, ice);
	}
	float band = 1.0 - smoothstep(0.0, aa * bank_px, abs(d));  // ink line centred on the bank
	// The flood band: mud on worn paths after the flood, and the flood's wash filling outward from the bank as it rises.
	if (flood > 0.0 || mud > 0.0) {
		float inBand = texture(season_tex, uv).g * (1.0 - inside);
		float muddy = inBand * mud * (0.25 + 0.75 * smoothstep(0.10, 0.45, core));
		c = mix(c, mud_col.rgb * (0.9 + 0.2 * n2), muddy * 0.7);
		float reach = flood * (band_m + 1.0);
		float wet = inBand * (1.0 - smoothstep(reach - 1.5, reach + 0.5, d));
		vec3 wash = mix(water_col.rgb, paper.rgb, 0.35);
		float drift = smoothstep(0.93, 0.98, sin(lp.y * 0.8 - time_s * 0.4 + sin(lp.x * 0.6) * 0.7) * 0.5 + 0.5);
		wash = mix(wash, paper.rgb, drift * 0.35);
		c = mix(c, wash, wet * 0.62);
		// The flood's edge: a faint dotted ink line where the wash ends.
		float edge = inBand * step(0.05, flood) * (1.0 - smoothstep(0.0, aa * 2.0, abs(d - reach))) * step(0.5, fract(lp.y * 0.6));
		c = mix(c, ink.rgb, edge * 0.45);
	}
	// Where the spring flood will reach, marked faintly on the dry map from the first day (a dotted ink edge, as a
	// surveyor marks a flood line), so a flood never comes as a surprise; the flood's own edge takes over while it rises.
	if (band_mark > 0.0) {
		float gb = texture(season_tex, uv).g * (1.0 - inside);
		float at = band_m - 0.5;
		float line = (1.0 - smoothstep(aa * 0.6, aa * 1.8, abs(d - at))) * step(0.1, gb) * step(0.5, fract(lp.y * 0.3));
		c = mix(c, ink.rgb, line * 0.5 * band_mark * (1.0 - step(0.05, flood)));
	}
	c = mix(c, wc, inside);
	c = mix(c, ink.rgb, band * (1.0 - 0.55 * ford));

	// paper grain: fine and coarse mottling, multiplied, so it reads as gouache on paper
	float g = texture(paper_tex, lp / 3.1).r * 0.6 + texture(paper_tex, rot(lp, 1.3) / 1.3).r * 0.4;
	c *= 1.0 + (g - 0.876) * grain * 8.0;   // 0.876: paper_tex mean red in linear light, so grain keeps the mean colour

	// wear overlay: a tracing-paper sheet (the page tinted pale) with the heat map of wear on land
	if (overlay > 0.0) {
		float land = (1.0 - inside);
		vec3 sheet = mix(c, paper.rgb, 0.55);
		float a = wh.r < 0.005 ? 0.0 : min(1.0, (120.0 + wh.r * 400.0) / 255.0);
		sheet = mix(sheet, heat(wh.r), a * land);
		c = mix(c, sheet, overlay);
	}
	ALBEDO = c;
}

// One sun: unshadowed ground = colour untouched; in shadow a cool, slightly darker wash.
void light() {
	DIFFUSE_LIGHT += mix(shadow_tint, vec3(1.0), ATTENUATION);
}
