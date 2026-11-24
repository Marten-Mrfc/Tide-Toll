shader_type spatial;
// Inverted-hull ink for the MultiMesh villagers (next_pass of the walker material). Width is set in metres per
// frame from C# so it stays about 1 screen pixel at any zoom: hull_w = px * metres_per_px / instance_scale.
render_mode unshaded, cull_front, depth_draw_always;
uniform float anim_time = 0.0;
uniform float hull_w = 0.06;
uniform vec4 ink : source_color = vec4(0.1683, 0.1441, 0.1236, 1.0);
void vertex() {
	float ph = INSTANCE_CUSTOM.x + anim_time * INSTANCE_CUSTOM.y;
	float s = abs(sin(ph * 6.2831853));
	VERTEX.y += s * INSTANCE_CUSTOM.z * (0.35 + 0.65 * smoothstep(0.0, 1.2, VERTEX.y));
	VERTEX.x += sin(ph * 6.2831853) * INSTANCE_CUSTOM.z * 0.25 * smoothstep(0.0, 1.9, VERTEX.y);
	// radial push in XZ, not along NORMAL: flat-shaded low-poly normals differ per face and tear the hull apart
	VERTEX.xz += normalize(VERTEX.xz + vec2(0.0001)) * hull_w;
	VERTEX.y += hull_w * 0.5;
}
void fragment() { ALBEDO = ink.rgb; ALPHA = 1.0; }
