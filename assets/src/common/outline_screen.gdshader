// Screen-space ink edge pass for the Mobile renderer: the Living Map outline method (canonical copy;
// tools/contact-sheet loads it from here, the game should copy or load the same file). Full-screen quad, child of the Camera3D.
// Edges come from the DEPTH buffer only (the Mobile renderer has no normal buffer): the second difference of
// view depth is zero on any flat plane, so it fires only at silhouettes (depth jump) and creases (ridge, wall/roof,
// wall/ground). Shadows, lighting and palette colours do not produce lines. Colour edges are optional (use_colour).
shader_type spatial;
render_mode unshaded, depth_test_disabled, cull_disabled, blend_mix, skip_vertex_transform;
uniform sampler2D depth_tex : hint_depth_texture, filter_nearest, repeat_disable;
uniform sampler2D screen_tex : hint_screen_texture, filter_nearest, repeat_disable;
uniform vec4 ink : source_color = vec4(0.1683, 0.1441, 0.1236, 1.0); // palette Ink #2B2620
uniform float width_px = 1.0;      // sample step in screen pixels; line is about 2x this wide. Scale with height/1080.
uniform float metres_per_px = 0.05; // world metres per screen pixel at the model (ortho: camera.size / viewport height)
uniform float crease = 0.45;       // slope change (about tan of the angle) that counts as a crease
uniform float use_colour = 0.0;    // 1 = also ink where the rendered colour jumps (picks up shadow edges too)
uniform float colour_edge = 0.12;
void vertex() { POSITION = vec4(VERTEX.xy, 1.0, 1.0); }
float vz(ivec2 p, vec2 size, mat4 inv_proj) {
	p = clamp(p, ivec2(0), ivec2(size) - 1);   // texelFetch outside the target returns 0, which would draw a frame
	float d = texelFetch(depth_tex, p, 0).r;
	vec4 v = inv_proj * vec4((vec2(p) + 0.5) / size * 2.0 - 1.0, d, 1.0);
	return -(v.z / v.w);
}
void fragment() {
	ivec2 p = ivec2(FRAGCOORD.xy);
	vec2 size = vec2(textureSize(depth_tex, 0));
	int w = max(int(width_px + 0.5), 1);
	mat4 ip = INV_PROJECTION_MATRIX;
	float zc = vz(p, size, ip);
	float step_m = metres_per_px * float(w);
	// Skip an axis whose neighbours fall outside the target: clamping there turns the second difference into a
	// first difference, which inks a frame line along the viewport edge on any sloped (tilted) ground.
	ivec2 hi = ivec2(size) - 1;
	bool okx = p.x - w >= 0 && p.x + w <= hi.x;
	bool oky = p.y - w >= 0 && p.y + w <= hi.y;
	float lx = okx ? abs(vz(p + ivec2(w, 0), size, ip) + vz(p - ivec2(w, 0), size, ip) - 2.0 * zc) : 0.0;
	float ly = oky ? abs(vz(p + ivec2(0, w), size, ip) + vz(p - ivec2(0, w), size, ip) - 2.0 * zc) : 0.0;
	float e = step(crease * step_m, max(lx, ly));
	if (use_colour > 0.5) {
		vec3 sc = texelFetch(screen_tex, p, 0).rgb;
		float cd = max(length(texelFetch(screen_tex, min(p + ivec2(w, 0), hi), 0).rgb - sc), length(texelFetch(screen_tex, min(p + ivec2(0, w), hi), 0).rgb - sc));
		e = max(e, step(colour_edge, cd));
	}
	ALBEDO = ink.rgb;
	ALPHA = e;
}
// Use (GDScript): quad = MeshInstance3D with a 2x2 QuadMesh, extra_cull_margin = 16384, child of the camera at z = -1,
// material_override = ShaderMaterial with this shader. Set metres_per_px = camera.size / viewport height (ortho),
// width_px about 1.5 * height / 1080 * (zoom factor), and keep camera near/far tight (the sheet uses 30..90).
// Pitfalls: Mobile has no normal buffer (hence depth only); sample depth with texelFetch(FRAGCOORD), never by uv;
// a compile error does not fail the run - grep the log for "SHADER ERROR". Raise `crease` if terrain relief
// ever gives false lines. Colour-only changes (a door painted on a flat wall) get no line unless use_colour = 1.
