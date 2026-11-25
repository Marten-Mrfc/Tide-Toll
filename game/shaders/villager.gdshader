// A villager's clothes: the model's palette shading tinted to the instance's colour (MultiMesh colours), so a
// handful of light, distinct clothes colours read against the grass. Lambert, no specular, like ModelLib's material.
shader_type spatial;
render_mode diffuse_lambert, specular_disabled;
// Soft ink (InkOutline's second pass): a small figure keeps its outline but not a clog of inner lines.
stencil_mode write, compare_always, 1;

uniform sampler2D palette : source_color, filter_nearest;

varying vec3 dye;

void vertex() {
	dye = COLOR.rgb;
}

void fragment() {
	vec3 base = texture(palette, UV).rgb;
	float lum = dot(base, vec3(0.3, 0.59, 0.11));
	// Keep the model's light and dark parts (belt, shoes) but carry them in the clothes colour.
	ALBEDO = dye * clamp(0.62 + lum * 0.75, 0.55, 1.15);
	ROUGHNESS = 1.0;
}
