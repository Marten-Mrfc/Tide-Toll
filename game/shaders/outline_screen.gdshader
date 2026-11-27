// Copy of assets/src/common/outline_screen.gdshader (the canonical file; keep them identical).
// Screen-space ink edge pass for the Mobile renderer: the Living Map outline method (canonical copy;
// game/shaders/outline_screen.gdshader is a copy, keep them identical; tools/contact-sheet loads it from here).
// Full-screen quad, child of the Camera3D. Edges come from the DEPTH buffer only (the Mobile renderer has no normal
// buffer): the second difference of view depth is zero on any flat plane, so it fires only at silhouettes (depth
// jump) and creases (ridge, wall/roof, wall/ground). Shadows, lighting and palette colours do not produce lines.
// Two weights (research n8, docs/plans/n8/research/ink-experiments.md):
//   silhouettes - a second difference of sil_m (0.3 m) or more: full Ink. Their width follows width_px smoothly:
//                 the step floor(width_px) at full strength, the next step out at the fractional part's strength,
//                 so the outline's weight grows with the window and the zoom with no jump at any size.
//   inner creases - found with a one-pixel step and drawn as the colour under them times inner_k (0.6), with a
//                 ramp from the crease threshold to four times it instead of a hard step: a darker shade of the
//                 roof, not a black stripe.
// Work per pixel: 1 + 4 depth reads at step 1 (shared by the inner creases), + 4 at the outer step, + 4 more when
// width_px >= 2 (1440p and up); one colour read of the screen copy. Today's pass read 5. Needs a look on a real GPU.
shader_type spatial;
render_mode unshaded, depth_test_disabled, cull_disabled, blend_mix, skip_vertex_transform;
// STENCIL_MODE
uniform sampler2D depth_tex : hint_depth_texture, filter_nearest, repeat_disable;
uniform sampler2D screen_tex : hint_screen_texture, filter_nearest, repeat_disable;
uniform vec4 ink : source_color = vec4(0.1683, 0.1441, 0.1236, 1.0); // palette Ink #2B2620
uniform float width_px = 1.5;      // silhouette weight in screen pixels (the line is about 2x this wide); InkOutline sets it
uniform float metres_per_px = 0.05; // world metres per screen pixel at the model (ortho: camera.size / viewport height)
uniform float crease = 0.45;       // slope change (about tan of the angle) that counts as a crease (1.5 on soft-ink pixels)
uniform float sil_m = 0.3;         // a second difference of this many metres or more is a silhouette: full Ink
uniform float inner_k = 0.6;       // inner creases: the colour under them times this
uniform float ink_alpha = 1.0;     // strength of the whole pass (soft ink thins as the view zooms out)
uniform float max_step = 8.0;      // the outer step is never above this (small things: InkOutline gives the soft pass 1)
void vertex() { POSITION = vec4(VERTEX.xy, 1.0, 1.0); }
float vz(ivec2 p, vec2 size, mat4 inv_proj) {
	p = clamp(p, ivec2(0), ivec2(size) - 1);   // texelFetch outside the target returns 0, which would draw a frame
	float d = texelFetch(depth_tex, p, 0).r;
	vec4 v = inv_proj * vec4((vec2(p) + 0.5) / size * 2.0 - 1.0, d, 1.0);
	return -(v.z / v.w);
}
// Second difference at step w, both axes; an axis whose neighbours fall outside the target is skipped (clamping
// there turns it into a first difference, which inks a frame line along the viewport edge on tilted ground).
float d2(ivec2 p, int w, float zc, vec2 size, mat4 ip) {
	ivec2 hi = ivec2(size) - 1;
	float lx = (p.x - w >= 0 && p.x + w <= hi.x) ? abs(vz(p + ivec2(w, 0), size, ip) + vz(p - ivec2(w, 0), size, ip) - 2.0 * zc) : 0.0;
	float ly = (p.y - w >= 0 && p.y + w <= hi.y) ? abs(vz(p + ivec2(0, w), size, ip) + vz(p - ivec2(0, w), size, ip) - 2.0 * zc) : 0.0;
	return max(lx, ly);
}
void fragment() {
	ivec2 p = ivec2(FRAGCOORD.xy);
	vec2 size = vec2(textureSize(depth_tex, 0));
	mat4 ip = INV_PROJECTION_MATRIX;
	float zc = vz(p, size, ip);
	float wf = clamp(width_px, 1.0, max_step);
	int w0 = int(floor(wf));
	float frac = wf - float(w0);
	float v1 = d2(p, 1, zc, size, ip);                       // step 1: inner creases, and the outline when w0 = 1
	float v0 = w0 == 1 ? v1 : d2(p, w0, zc, size, ip);
	float sil = step(sil_m, v0);
	if (sil < 0.5 && frac > 0.01) sil = frac * step(sil_m, d2(p, w0 + 1, zc, size, ip));
	float c1 = crease * metres_per_px;
	float inner = smoothstep(c1, 4.0 * c1, v1);
	vec3 under = texelFetch(screen_tex, p, 0).rgb * inner_k;
	// Silhouette over the inner shade: full Ink where sil = 1, a blend toward Ink on the outer ring.
	float a_in = inner * (1.0 - sil);
	float a = sil + a_in;
	ALBEDO = a > 0.0 ? (ink.rgb * sil + under * a_in) / a : ink.rgb;
	ALPHA = clamp(a, 0.0, 1.0) * ink_alpha;
}
// Soft ink (trees, crop rows, reeds, villagers' figures: many small facets): their glTF nodes carry extras
// {"ink": "soft"} (Godot: node metadata "extras"); their materials write stencil 1 (stencil_mode CUSTOM, flags WRITE,
// compare ALWAYS, reference 1), and this pass is drawn twice: replace the "// STENCIL_MODE" line with
// "stencil_mode read, compare_not_equal, 1;" (crease 0.45) in one copy and "stencil_mode read, compare_equal, 1;"
// (crease 1.5) in the other. Silhouettes are depth jumps and are inked in both. tools/contact-sheet does this.
// Use: quad = MeshInstance3D with a 2x2 QuadMesh, extra_cull_margin = 16384, child of the camera at z = -1,
// material_override = ShaderMaterial with this shader. Set metres_per_px = camera.size / viewport height (ortho),
// width_px = InkOutline.LinePx (about 1.8 * height / 1080 at the play view), and keep camera near/far tight (30..90).
// Pitfalls: Mobile has no normal buffer (hence depth only); sample depth with texelFetch(FRAGCOORD), never by uv;
// a compile error does not fail the run - grep the log for "SHADER ERROR".
