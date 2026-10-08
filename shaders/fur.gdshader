// Hair strands: vertex colour gives the strand's shade, UV.x holds how far along the strand a vertex is
// (0 at the root, 1 at the tip) so tips sway while roots stay anchored to the body.
shader_type spatial;
render_mode cull_disabled;

uniform float sway_amount = 0.03;
uniform float sway_speed = 1.6;

void vertex() {
	float w = UV.x * UV.x;
	float phase = TIME * sway_speed + VERTEX.x * 2.0 + VERTEX.z * 1.5 + VERTEX.y;
	VERTEX.x += sin(phase) * w * sway_amount;
	VERTEX.z += cos(phase * 0.8) * w * sway_amount;
}

void fragment() {
	ALBEDO = COLOR.rgb;
	ROUGHNESS = 1.0;
	SPECULAR = 0.2;
}
