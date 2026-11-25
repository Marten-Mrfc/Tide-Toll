shader_type spatial;
// The Hamlet buildings: the same Living Map look as the standard model material (palette atlas sampled nearest,
// Lambert, no specular), plus one per-building instance uniform: the tone of its thatch, so a row of cottages is
// not one roof repeated (fresh straw, weathered, old grey thatch).
render_mode diffuse_lambert, specular_disabled;
uniform sampler2D atlas : source_color, filter_nearest, repeat_disable;
uniform vec4 thatch : source_color = vec4(0.788, 0.647, 0.353, 1.0);   // Reed and thatch in the model
instance uniform vec4 thatch_tone : source_color = vec4(0.788, 0.647, 0.353, 1.0);
void fragment() {
	vec3 c = texture(atlas, UV).rgb;
	if (dot(abs(c - thatch.rgb), vec3(1.0)) < 0.02) c = thatch_tone.rgb;
	ALBEDO = c;
	ROUGHNESS = 1.0;
}
