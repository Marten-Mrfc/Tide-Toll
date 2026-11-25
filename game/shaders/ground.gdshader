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
uniform float overlay = 0.0;         // 0..1: wear heat map laid over the page like a tracing-paper sheet
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
	//    walkers leave beside a trail) and a broad average (only wide, busy trunks stay high in it).
	float core = wear_at(m) * 0.4;
	float spread = 0.0, broad = 0.0;
	for (int k = 0; k < 6; k++) {
		float a = float(k) * 1.0471976 + 0.35;
		vec2 d = vec2(cos(a), sin(a));
		core += wear_at(m + d * cm * 0.55) * 0.1;
		spread += wear_at(m + d * cm * 2.2) + wear_at(m + d * cm * 1.2);
		broad += wear_at(m + d * cm * 1.0);
	}
	spread /= 12.0;
	broad = (broad + wear_at(m) * 2.0) / 8.0;
	float n1 = vnoise(lp / 2.2), n2 = vnoise(lp / 0.8), n3 = vnoise(lp / 5.5 + 3.1);
	// The sim's own thresholds still drive the stages (10 trampled, 25 trail, 50 track, hollow flag), read through
	// the softened core; noise moves each edge by a little, so it frays.
	float fringe = smoothstep(0.03, 0.20, max(spread * 1.25, core)) * (0.75 + 0.25 * n3);
	float bare = smoothstep(0.24, 0.40, core + (n1 - 0.5) * 0.16 + (n2 - 0.5) * 0.08);
	// Dark packed earth only where a trail is both well worn and wide (a trunk) or the sim marked a hollow way.
	float packed = smoothstep(0.78, 0.95, broad + (n1 - 0.5) * 0.10) * smoothstep(0.50, 0.65, core);
	float w4 = smoothstep(0.3, 0.7, wh.g) * smoothstep(0.50, 0.65, core);
	// Meadow, then flattened grass: paler and yellower, a lighter sheen where blades lie flat.
	vec3 grass = two(g0, lp);
	vec3 flattened = mix(two(g1, lp), crop_dark.rgb, 0.25) * (1.04 + 0.06 * n2);
	vec3 c = mix(grass, flattened, fringe);
	// Bare earth: the trail texture lifted toward paper, mottled, with tufts of grass left standing near its edges.
	vec3 earth = mix(two(g2, lp), paper.rgb, 0.16) * (0.93 + 0.12 * n2);
	float tufts = smoothstep(0.62, 0.80, n2) * (1.0 - smoothstep(0.42, 0.70, core));
	earth = mix(earth, flattened, tufts * 0.7);
	c = mix(c, earth, bare);
	// Packed track, then hollow way: the darker browns, kept for the busiest ground.
	c = mix(c, mix(two(g3, lp), earth, 0.25 * n1), packed * 0.85);
	c = mix(c, two(g4, lp), w4);
	c = mix(c, texture(ramp_tex, vec2(core, 0.5)).rgb, ramp_mix * bare * (1.0 - w4));
	float w = core;

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
	wc = mix(wc, wc * 0.62, line * 0.8 * (1.0 - ford));
	// ford: shallow, lighter water with the earth bed and pale ripples showing
	vec3 bed = two(g2, lp);
	vec3 shallow = mix(mix(water_col.rgb, bed, 0.45), paper.rgb, 0.12);
	float ripple = smoothstep(0.90, 0.97, sin(lp.y * 2.6 - time_s * 0.8 + sin(lp.x * 1.3) * 0.8) * 0.5 + 0.5);
	shallow = mix(shallow, paper.rgb, ripple * 0.25);
	wc = mix(wc, shallow, ford);
	float band = 1.0 - smoothstep(0.0, aa * bank_px, abs(d));  // ink line centred on the bank
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
