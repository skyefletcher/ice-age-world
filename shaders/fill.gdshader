// A HUD icon that fills up like a gauge: below the level it shows in full colour, above it in a dim grey, so the
// needs read at a glance as full, half gone or empty. The icons' artwork stands between top and bottom (as a
// share of the icon's height), so an empty icon is all grey and a full one all colour.
shader_type canvas_item;

uniform float fill : hint_range(0.0, 1.0) = 1.0;
uniform float top = 0.08;
uniform float bottom = 0.93;

void fragment() {
	vec4 colour = COLOR;
	if (UV.y < mix(bottom, top, fill)) {
		float grey = dot(colour.rgb, vec3(0.3, 0.59, 0.11));
		colour.rgb = vec3(grey * 0.4);
		colour.a *= 0.7;
	}
	COLOR = colour;
}
