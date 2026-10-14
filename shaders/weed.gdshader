// Kelp and waterweed: vertex colour gives the shade, and the plant is built one unit tall, so VERTEX.y is how far up it
// a vertex is (0 at the holdfast, 1 at the top). The current sways the tops while the base stays anchored to the bed,
// each plant at its own phase, from where it stands, so a forest of them waves rather than marching in step.
shader_type spatial;
render_mode cull_disabled;

uniform float sway_amount = 0.8;
uniform float sway_speed = 0.6;

void vertex() {
	float w = VERTEX.y * VERTEX.y;
	vec3 root = (MODEL_MATRIX * vec4(0.0, 0.0, 0.0, 1.0)).xyz;
	float phase = TIME * sway_speed + root.x * 0.13 + root.z * 0.17 + VERTEX.y * 2.0;
	VERTEX.x += sin(phase) * w * sway_amount;
	VERTEX.z += cos(phase * 0.7) * w * sway_amount * 0.6;
}

void fragment() {
	ALBEDO = COLOR.rgb;
	ROUGHNESS = 0.6;
	SPECULAR = 0.3;
}
