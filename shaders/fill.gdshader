// A HUD icon that fills up like a gauge: below the level it shows in full colour, above it in a dim grey, so the
// needs read at a glance as full, half gone or empty. The icons' artwork stands between top and bottom (as a
// share of the icon's height), so an empty icon is all grey and a full one all colour.
//
// With gold on, the icon is recoloured in one gold, as Savannah Life's HUD is: its dark outlines turn deep brown,
// its fill gold and its highlights pale yellow, so every need reads as one set whatever colours its artwork has.
shader_type canvas_item;

uniform float fill : hint_range(0.0, 1.0) = 1.0;
uniform float top = 0.08;
uniform float bottom = 0.93;
uniform bool gold = false;

void fragment() {
	vec4 colour = COLOR;
	float shade = dot(colour.rgb, vec3(0.3, 0.59, 0.11));
	if (gold) {
		vec3 outline = vec3(0.22, 0.14, 0.02);
		vec3 body = vec3(1.0, 0.8, 0.22);
		vec3 shine = vec3(1.0, 0.95, 0.7);
		colour.rgb = mix(mix(outline, body, smoothstep(0.25, 0.35, shade)), shine, smoothstep(0.8, 0.9, shade));
		shade = dot(colour.rgb, vec3(0.3, 0.59, 0.11));
	}
	if (UV.y < mix(bottom, top, fill)) {
		colour.rgb = vec3(shade * 0.4);
		colour.a *= 0.7;
	}
	COLOR = colour;
}
