shader_type spatial;
// Villagers: ONE MultiMesh. Per-instance custom data (Float format, 4 floats): x = stride phase (0..1),
// y = stride rate in Hz, z = bob amplitude in metres (0 = standing still), w = outfit (0..1, fixed per villager).
// anim_time comes from C# (not TIME) so pausing the game freezes the bob.
// ALPHA is written (always 1) ONLY to move walkers into the transparent pass: they then do not appear in the depth
// texture the screen-space ink pass reads, so they get no thick screen ink; the hull pass below inks them thinly.
// Outfits are palette swaps on the one body mesh (Art tab): the model's Brick tunic and Soot-brick cape take one of six
// palette tunic colours, its Limewash head becomes a dark hat or hood, so from above a villager is a coloured round
// with a dark crown, not a pale dot.
render_mode specular_disabled, diffuse_lambert, depth_draw_always;
uniform sampler2D atlas : source_color, filter_nearest, repeat_disable;   // palette.png: nearest, never mipmapped
uniform float anim_time = 0.0;
uniform vec4 model_tunic : source_color = vec4(0.643, 0.333, 0.227, 1.0); // Brick: the model's tunic
uniform vec4 model_cape : source_color = vec4(0.478, 0.267, 0.212, 1.0);  // Soot brick: its cape and hood
uniform vec4 model_head : source_color = vec4(0.914, 0.886, 0.812, 1.0);  // Limewash: its head
uniform vec4 tunic0 : source_color; uniform vec4 tunic1 : source_color; uniform vec4 tunic2 : source_color;
uniform vec4 tunic3 : source_color; uniform vec4 tunic4 : source_color; uniform vec4 tunic5 : source_color;
uniform vec4 hat0 : source_color; uniform vec4 hat1 : source_color; uniform vec4 hat2 : source_color;
varying flat float outfit;
void vertex() {
	// A smaller head (the model's head dot covers half the villager from above): scaled about its centre, the same
	// in walker.gdshader and walker_hull.gdshader so the ink hull keeps fitting.
	VERTEX = mix(VERTEX, vec3(0.0, 2.24, 0.06) + (VERTEX - vec3(0.0, 2.24, 0.06)) * 0.6, smoothstep(1.84, 1.95, VERTEX.y));
	float ph = INSTANCE_CUSTOM.x + anim_time * INSTANCE_CUSTOM.y;
	float s = abs(sin(ph * 6.2831853));                 // two bounces per stride, like footfalls
	VERTEX.y += s * INSTANCE_CUSTOM.z * (0.35 + 0.65 * smoothstep(0.0, 1.2, VERTEX.y)); // head lifts more than the feet
	VERTEX.x += sin(ph * 6.2831853) * INSTANCE_CUSTOM.z * 0.25 * smoothstep(0.0, 1.9, VERTEX.y); // tiny sway
	outfit = INSTANCE_CUSTOM.w;
}
bool is(vec3 a, vec3 b) { return dot(abs(a - b), vec3(1.0)) < 0.02; }
void fragment() {
	vec3 c = texture(atlas, UV).rgb;
	int t = int(floor(outfit * 6.0)) % 6;
	vec3 tunic = t == 0 ? tunic0.rgb : t == 1 ? tunic1.rgb : t == 2 ? tunic2.rgb : t == 3 ? tunic3.rgb : t == 4 ? tunic4.rgb : tunic5.rgb;
	int h = int(floor(fract(outfit * 7.31) * 3.0)) % 3;
	vec3 hat = h == 0 ? hat0.rgb : h == 1 ? hat1.rgb : hat2.rgb;
	if (is(c, model_tunic.rgb)) c = tunic;
	else if (is(c, model_cape.rgb)) c = tunic * 0.9;        // the cape: the tunic's own colour, a shade darker (from
	                                                        // above it is most of the villager, so it must read as the tunic)
	else if (is(c, model_head.rgb)) c = hat;
	ALBEDO = c;
	ALPHA = 1.0;
}
