// Kelp: the stalks and blades sway slowly in the swell, more the higher up the stalk they are, as the holdfast pins the
// foot to the rock while the floats hold the rest up in the moving water. UV.y carries each vertex's height up its stalk
// in metres. The blades are thin, so light comes through them golden from behind.
shader_type spatial;
render_mode cull_disabled;

void vertex() {
	float up = UV.y;
	// Neighbouring stalks sway nearly together, the forest moving as one in the swell, with a slower drift on top.
	float phase = dot(VERTEX.xz, vec2(0.05, 0.035));
	float bend = up * 0.045;
	VERTEX.x += (sin(TIME * 0.55 + phase - up * 0.08) + 0.4 * sin(TIME * 0.17 + phase * 2.0)) * bend;
	VERTEX.z += (cos(TIME * 0.43 + phase * 1.3 - up * 0.07) + 0.4 * cos(TIME * 0.13 + phase)) * bend * 0.7;
}

void fragment() {
	ALBEDO = COLOR.rgb;
	ROUGHNESS = 0.55;
	BACKLIGHT = COLOR.rgb * 0.7;
	if (!FRONT_FACING) {
		NORMAL = -NORMAL;
	}
}
