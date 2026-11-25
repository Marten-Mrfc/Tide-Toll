shader_type spatial;
// Trees as round gouache clumps from above (Art tab: "round gouache canopies that read as map tree symbols from far
// away"): the crown's normals are bent toward those of one squashed sphere round the crown's centre, so the sun
// shades the whole clump as a ball, light on its south-east side, instead of lighting every facet on its own. Each
// tree gets a slightly different tone (from its instance index). Writes 1 into the stencil like the other soft-ink
// models, so the ink pass inks only its silhouette and strong creases (InkOutline, InkSoft).
render_mode diffuse_lambert, specular_disabled;
stencil_mode write, compare_always, 1;
uniform sampler2D atlas : source_color, filter_nearest, repeat_disable;
uniform vec3 crown_centre = vec3(0.0, 4.7, 0.0);
uniform float crown_base = 3.3;      // the crown starts here; the trunk below keeps its own normals
uniform float roundness = 0.85;
varying float tone;
float hash11(float p) { p = fract(p * 0.1031); p *= p + 33.33; p *= p + p; return fract(p); }
void vertex() {
	float k = smoothstep(crown_base - 0.5, crown_base + 0.5, VERTEX.y) * roundness;
	vec3 rn = normalize((VERTEX - crown_centre) * vec3(1.0, 1.7, 1.0));
	NORMAL = normalize(mix(NORMAL, rn, k));
	tone = hash11(float(INSTANCE_ID) + 0.5);
}
void fragment() {
	ALBEDO = texture(atlas, UV).rgb * (0.92 + 0.14 * tone);
	ROUGHNESS = 1.0;
}
