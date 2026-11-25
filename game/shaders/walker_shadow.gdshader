shader_type spatial;
// A small soft shadow under each villager: a flat disc drawn from the walkers' own MultiMesh buffer (a second
// MultiMesh, same data), pushed toward the north-west, away from the sun in the south-east, and drawn before the
// villagers. world_vertex_coords: the push is in world space whatever the walker's facing.
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled, world_vertex_coords;
uniform vec4 ink : source_color = vec4(0.169, 0.149, 0.125, 1.0);
uniform float strength = 0.22;
uniform float push = 0.35;       // metres per unit of instance scale
void vertex() {
	float sc = length(MODEL_MATRIX[0].xyz);
	VERTEX.xz += vec2(-push, -push) * sc;
}
void fragment() {
	float r = length(UV - 0.5) * 2.0;           // 0 at the centre, 1 at the rim
	ALBEDO = ink.rgb;
	ALPHA = strength * (1.0 - smoothstep(0.35, 1.0, r));
}
