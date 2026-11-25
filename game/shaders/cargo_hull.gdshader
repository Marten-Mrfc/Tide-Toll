shader_type spatial;
// Inverted-hull ink for the carried grain sacks (next_pass of cargo.gdshader): the same width rule as the villagers'
// hull (walker_hull.gdshader), pushed out radially round the sack's own centre.
render_mode unshaded, cull_front, depth_draw_always;
uniform float anim_time = 0.0;
uniform float hull_w = 0.06;
uniform vec4 ink : source_color = vec4(0.1683, 0.1441, 0.1236, 1.0);
void vertex() {
	bool drop = NORMAL.y < -0.5;
	float ph = INSTANCE_CUSTOM.x + anim_time * INSTANCE_CUSTOM.y;
	VERTEX.y += abs(sin(ph * 6.2831853)) * INSTANCE_CUSTOM.z;
	VERTEX.x += sin(ph * 6.2831853) * INSTANCE_CUSTOM.z * 0.25;
	VERTEX.xz += normalize(VERTEX.xz + vec2(0.0001)) * hull_w;
	VERTEX.y += hull_w * 0.5;
	if (drop) VERTEX = vec3(0.0);
}
void fragment() { ALBEDO = ink.rgb; ALPHA = 1.0; }
