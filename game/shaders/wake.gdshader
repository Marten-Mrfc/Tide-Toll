shader_type spatial;
// The wake behind a boat: a flat quad trailing from the stern (UV.y 0 at the stern, 1 at the far end), drawn as two
// pale waterlines spreading in a V with dashes that run away from the boat, and a faint churn in the middle.
// INSTANCE_CUSTOM.x = strength (0 when the boat is still, 1 under way), .y = width of the V at its end (0..1).
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;
uniform vec4 foam : source_color = vec4(0.949, 0.918, 0.847, 1.0);
uniform float anim_time = 0.0;
varying vec2 custom;

void vertex() {
	custom = INSTANCE_CUSTOM.xy;
}

void fragment() {
	float v = UV.y, u = UV.x - 0.5;
	float px = max(fwidth(UV.x), 0.0001);
	float spread = mix(0.08, 0.5 * custom.y, v);
	float line = 1.0 - smoothstep(px * 1.0, px * 2.4, abs(abs(u) - spread));
	float dash = step(0.35, fract(v * 7.0 - anim_time * 2.2));
	float churn = (1.0 - smoothstep(0.0, 0.07, abs(u))) * step(0.5, fract(v * 11.0 - anim_time * 3.1)) * (1.0 - v);
	float fade = (1.0 - v) * (1.0 - v);
	ALBEDO = foam.rgb;
	ALPHA = clamp(max(line * dash, churn * 0.5) * fade * custom.x * 0.85, 0.0, 1.0);
}
