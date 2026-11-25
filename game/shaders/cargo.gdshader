shader_type spatial;
// Grain sacks carried above the head (Chapter II: a walker taking grain to the landing), one MultiMesh fed from the
// walkers' own instance data, so a sack bobs and sways with its carrier's head. INSTANCE_CUSTOM: x = stride phase,
// y = stride rate, z = bob amplitude (as walker.gdshader), w = 1 when the grain went through the ford and is wet.
// A wet sack is darker and river-tinted with glints running down it, drips falling from it: wading costs trade.
render_mode specular_disabled, diffuse_lambert, depth_draw_always;
uniform sampler2D atlas : source_color, filter_nearest, repeat_disable;
uniform float anim_time = 0.0;
uniform vec4 glint_col : source_color = vec4(0.835, 0.886, 0.910, 1.0);
varying flat float wet;
varying vec3 lp;
void vertex() {
	lp = VERTEX;
	float ph = INSTANCE_CUSTOM.x + anim_time * INSTANCE_CUSTOM.y;
	VERTEX.y += abs(sin(ph * 6.2831853)) * INSTANCE_CUSTOM.z;               // the head's lift (walker.gdshader at the top)
	VERTEX.x += sin(ph * 6.2831853) * INSTANCE_CUSTOM.z * 0.25;
	wet = INSTANCE_CUSTOM.w;
}
void fragment() {
	vec3 c = texture(atlas, UV).rgb;
	// Dry: pale burlap, so a carried sack reads apart from the head and hat under it.
	c = mix(c, vec3(0.92, 0.76, 0.40), 0.6);   // straw-gold burlap: apart from heads, hats and the pale ford stones
	if (wet > 0.5) {
		// Wet: soaked dark and river-blue, with water running down it.
		c = mix(c, vec3(0.16, 0.33, 0.55), 0.78);   // soaked: a deep river blue
		// Water running down the sack: thin bright streaks sliding downward, broken up round the sack.
		float a = atan(lp.z, lp.x);
		float run = fract(lp.y * 2.2 + anim_time * 0.9 + sin(a * 3.0) * 0.35);
		float streak = smoothstep(0.86, 0.97, run) * step(0.35, fract(a * 0.955 + 0.2));
		c = mix(c, glint_col.rgb, streak * 0.85);
	}
	ALBEDO = c;
	ALPHA = 1.0;
}
