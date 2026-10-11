// Water: the ocean heaves with a few long waves rolling in together, each a sine wave travelling at the speed real
// deep-water waves of its length do, so the long swells outrun the short chop. Water.cs works out the same heights for
// floating, so swimmers ride the waves the player sees. The surface is pale and clear in the shallows and dark blue
// over the deep, with foam on the highest crests and where the water washes against the shore. The small lakes lie
// still and plain: no waves, no foam, and one even colour all over, shallows and deep alike.
shader_type spatial;
render_mode blend_mix, cull_disabled, depth_draw_opaque;

const int WAVES = 4;
const float GRAVITY = 9.8;

// Each wave: xy its level direction of travel, z its wavelength in metres, w its share of the wave height.
uniform vec4 waves[WAVES];
uniform float wave_height = 0.5;
// Seconds the waves have been rolling, set from Water.cs so the floating maths keeps step.
uniform float wave_time = 0.0;
// 1 for the open sea, whose crests break, whose surface the wind chops up and whose foam laps up and down the beach;
// 0 for a still lake.
uniform float open_sea = 0.0;

uniform vec3 shallow_colour : source_color = vec3(0.3, 0.55, 0.58);
uniform vec3 deep_colour : source_color = vec3(0.06, 0.2, 0.3);
uniform vec3 still_colour : source_color = vec3(0.2, 0.4, 0.5);
uniform sampler2D depth_texture : hint_depth_texture, filter_linear_mipmap;

varying vec3 world;
varying float rise;

// Height of the water above its level at a world position, and its slope along x and z.
vec3 swell(vec2 p) {
	vec3 h = vec3(0.0);
	for (int i = 0; i < WAVES; i++) {
		vec4 w = waves[i];
		float k = TAU / w.z;
		float phase = k * dot(w.xy, p) - sqrt(GRAVITY * k) * wave_time;
		float a = w.w * wave_height;
		h.x += a * sin(phase);
		h.yz += a * k * cos(phase) * w.xy;
	}
	return h;
}

void vertex() {
	world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	rise = swell(world.xz).x;
	VERTEX.y += rise;
}

void fragment() {
	// The slope comes per pixel, so the light plays over the waves smoothly however coarse the mesh, with a fine,
	// shifting chop from the wind on top.
	vec3 s = swell(world.xz);
	vec2 chop = 0.03 * open_sea * vec2(cos(world.x * 1.1 + world.z * 0.6 + wave_time * 1.7), cos(world.z * 1.3 - world.x * 0.4 + wave_time * 2.1));
	vec3 n = normalize(vec3(-s.y - chop.x, 1.0, -s.z - chop.y));
	NORMAL = normalize((VIEW_MATRIX * vec4(n, 0.0)).xyz);

	// How much water the eye looks through to the bottom (or the rock) behind.
	vec4 behind = INV_PROJECTION_MATRIX * vec4(SCREEN_UV * 2.0 - 1.0, texture(depth_texture, SCREEN_UV).r, 1.0);
	float through = max(0.0, VERTEX.z - behind.z / behind.w);

	// Foam laps at the water's edge, its band swelling and shrinking as each wave washes in, and breaks on the crests.
	float wash = 0.35 + 0.2 * open_sea * sin(wave_time * 1.4 + world.x * 0.21 + world.z * 0.17);
	float shore = 1.0 - smoothstep(0.0, wash, through);
	float crest = open_sea * smoothstep(0.6, 0.95, rise / max(wave_height, 0.001));
	float foam = clamp(max(shore, crest), 0.0, 1.0) * 0.7 * open_sea;

	// Light shines through the thin water near a wave's crest, so the crests stand out paler against the troughs.
	vec3 water = mix(shallow_colour, deep_colour, smoothstep(0.0, 8.0, through));
	water = mix(water, shallow_colour * 1.25, smoothstep(-0.2, 1.0, rise / max(wave_height, 0.001)) * 0.4);
	ALBEDO = mix(mix(still_colour, water, open_sea), vec3(0.95), foam);
	ALPHA = mix(0.85, max(mix(0.55, 0.92, smoothstep(0.0, 6.0, through)), foam), open_sea);
	ROUGHNESS = mix(0.12, 0.7, foam);
	METALLIC = 0.1;
}
