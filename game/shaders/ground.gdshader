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
uniform float ramp_mix = 0.25;
uniform float noise_amp = 0.05;      // ragged trail edges (wear units)
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

void fragment() {
	vec2 uv = lp / map_size;
	float n = texture(paper_tex, lp / 17.0).r;                    // paper_tex red is about 0.9 +- 0.05
	vec2 wh = texture(wear_tex, uv).rg;
	float w = clamp(wh.r + (n - 0.9) * noise_amp * 4.0, 0.0, 1.0);
	float s = 0.035;
	float w1 = smoothstep(0.10 - s, 0.10 + s, w);   // trampled
	float w2 = smoothstep(0.25 - s, 0.25 + s, w);   // trail
	float w3 = smoothstep(0.50 - s, 0.50 + s, w);   // track
	// hollow way: the sim's flag (held for a long time once wear passes 85), only where the ground is still a track
	float w4 = w3 * smoothstep(0.3, 0.7, wh.g);
	vec3 c = two(g0, lp);
	c = mix(c, two(g1, lp), w1);
	c = mix(c, two(g2, lp), w2);
	c = mix(c, two(g3, lp), w3);
	c = mix(c, two(g4, lp), w4);
	c = mix(c, texture(ramp_tex, vec2(w, 0.5)).rgb, ramp_mix * (1.0 - w4 * 0.6));

	// field: rows of ripe crop running east-west, trampled to earth where trails cross, with an ink edge
	vec2 mask = texture(mask_tex, uv).rg;
	float cell_m = map_size / cells;
	{
		float row = sin(lp.y * 6.2831853 / 1.25 + sin(lp.x * 0.35) * 0.4);
		vec3 crop = mix(crop_dark.rgb, crop_col.rgb, smoothstep(-0.35, 0.35, row));
		crop *= 0.94 + 0.12 * texture(g1, lp / 9.0).g;
		float keep = 1.0 - smoothstep(0.08, 0.35, w);
		c = mix(c, crop, mask.g * keep);
		// edge of the crop area: compare with the mask half a cell away in four directions
		float e = 0.0;
		vec2 o = vec2(0.45 * cell_m / map_size, 0.0);
		e += abs(texture(mask_tex, uv + o.xy).g - mask.g);
		e += abs(texture(mask_tex, uv - o.xy).g - mask.g);
		e += abs(texture(mask_tex, uv + o.yx).g - mask.g);
		e += abs(texture(mask_tex, uv - o.yx).g - mask.g);
		c = mix(c, mix(c, ink.rgb, 0.55), clamp(e, 0.0, 1.0) * mask.g);
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
	c *= 1.0 + (g - 0.9) * grain * 8.0;

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
