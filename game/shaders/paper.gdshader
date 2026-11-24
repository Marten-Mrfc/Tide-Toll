shader_type spatial;
// Book paper: the page colour is exactly the palette's Paper where no shadow falls (custom light(), like the
// ground), with the same paper grain as the map, so page and map read as one sheet. Vertex colour red carries
// the page's own shading (the curve into the gutter, darker toward the spine), baked into the mesh.
render_mode specular_disabled, diffuse_lambert, ambient_light_disabled, cull_disabled;
uniform sampler2D paper_tex : source_color, filter_linear_mipmap, repeat_enable;
uniform vec4 paper : source_color = vec4(0.949, 0.918, 0.847, 1.0);
uniform float grain = 0.05;
uniform vec3 shadow_tint = vec3(0.72, 0.75, 0.86);
varying vec2 wp;
void vertex() { wp = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz; }
void fragment() {
	float g = texture(paper_tex, wp / 3.1).r * 0.6 + texture(paper_tex, wp / 23.0).r * 0.4;
	vec3 c = paper.rgb * (1.0 + (g - 0.9) * grain * 8.0);
	ALBEDO = vec3(COLOR.r, COLOR.g, 0.0);
}
void light() {
	DIFFUSE_LIGHT += mix(shadow_tint, vec3(1.0), ATTENUATION);
}
