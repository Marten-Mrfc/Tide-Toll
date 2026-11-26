// A villager's body: the tunic faces (modelled in Brick, palette cell uv 0.65625, 0.03125) take the instance's tunic
// colour (MultiMesh colours, five palette colours); legs, shoes, belt and hands keep their own palette colours.
// Lambert, no specular, like ModelLib's material. Night 3: before, the whole body was dyed (a coloured pawn).
shader_type spatial;
render_mode diffuse_lambert, specular_disabled;
// Soft ink (InkOutline's second pass): a small figure keeps its outline but not a clog of inner lines.
stencil_mode write, compare_always, 1;

uniform sampler2D palette : source_color, filter_nearest;
uniform vec2 tunic_uv = vec2(0.65625, 0.03125);

varying vec3 dye;

void vertex() {
	dye = pow(COLOR.rgb, vec3(2.2));   // palette colours are sRGB; the palette texture is read linear
}

void fragment() {
	vec3 base = texture(palette, UV).rgb;
	ALBEDO = distance(UV, tunic_uv) < 0.012 ? dye : base;
	ROUGHNESS = 1.0;
}
