shader_type spatial;
// Villagers: ONE MultiMesh. Per-instance custom data (Float format, 4 floats): x = stride phase (0..1),
// y = stride rate in Hz, z = bob amplitude in metres (0 = standing still), w = spare (colour variant).
// anim_time comes from C# (not TIME) so pausing the game freezes the bob.
// ALPHA is written (always 1) ONLY to move walkers into the transparent pass: they then do not appear in the depth
// texture the screen-space ink pass reads, so they get no thick screen ink; the hull pass below inks them thinly.
render_mode specular_disabled, diffuse_lambert, depth_draw_always;
uniform sampler2D atlas : source_color, filter_nearest, repeat_disable;   // palette.png: nearest, never mipmapped
uniform float anim_time = 0.0;
void vertex() {
	float ph = INSTANCE_CUSTOM.x + anim_time * INSTANCE_CUSTOM.y;
	float s = abs(sin(ph * 6.2831853));                 // two bounces per stride, like footfalls
	VERTEX.y += s * INSTANCE_CUSTOM.z * (0.35 + 0.65 * smoothstep(0.0, 1.2, VERTEX.y)); // head lifts more than the feet
	VERTEX.x += sin(ph * 6.2831853) * INSTANCE_CUSTOM.z * 0.25 * smoothstep(0.0, 1.9, VERTEX.y); // tiny sway
}
void fragment() { ALBEDO = texture(atlas, UV).rgb; ALPHA = 1.0; }
