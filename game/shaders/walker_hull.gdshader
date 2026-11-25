shader_type spatial;
// Inverted-hull ink for the MultiMesh villagers (next_pass of the walker material). Width is set in metres per
// frame from C# so it stays about 1 screen pixel at any zoom: hull_w = px * metres_per_px / instance_scale.
render_mode unshaded, cull_front, depth_draw_always;
uniform float anim_time = 0.0;
uniform float hull_w = 0.06;
uniform vec4 ink : source_color = vec4(0.1683, 0.1441, 0.1236, 1.0);
void vertex() {
	// Faces that look down (the cape's and the pack's undersides, the hem) are dropped from the hull: pushed out and
	// up, a downward face becomes an ink disc hovering over the tunic, and from above it hid the whole tunic under a
	// dark ring. The flat-shaded mesh has its own vertices per face, so collapsing them removes exactly those faces;
	// the silhouette still comes from the side faces. Only faces above the knee: the hem's own underside and the flared
	// bottom of the tunic give the outline its weight from above. (Applied last: vertex() may not return early.)
	bool drop = NORMAL.y < -0.5 && VERTEX.y > 0.5;
	// A smaller head (the model's head dot covers half the villager from above): scaled about its centre, the same
	// in walker.gdshader and walker_hull.gdshader so the ink hull keeps fitting.
	VERTEX = mix(VERTEX, vec3(0.0, 2.24, 0.06) + (VERTEX - vec3(0.0, 2.24, 0.06)) * 0.6, smoothstep(1.84, 1.95, VERTEX.y));
	float ph = INSTANCE_CUSTOM.x + anim_time * INSTANCE_CUSTOM.y;
	float s = abs(sin(ph * 6.2831853));
	VERTEX.y += s * INSTANCE_CUSTOM.z * (0.35 + 0.65 * smoothstep(0.0, 1.2, VERTEX.y));
	VERTEX.x += sin(ph * 6.2831853) * INSTANCE_CUSTOM.z * 0.25 * smoothstep(0.0, 1.9, VERTEX.y);
	// radial push in XZ, not along NORMAL: flat-shaded low-poly normals differ per face and tear the hull apart
	VERTEX.xz += normalize(VERTEX.xz + vec2(0.0001)) * hull_w;
	VERTEX.y += hull_w * 0.5;
	if (drop) VERTEX = vec3(0.0);
}
void fragment() { ALBEDO = ink.rgb; ALPHA = 1.0; }
