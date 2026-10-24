using System;
using Godot;

namespace IceAgeWorld;

/// <summary>
/// A bird of prey, built in code facing -Z: a body that pivots at the hips between standing upright and flying level,
/// a neck and head that stay level whatever the body does, a fan of tail feathers, two legs that fold at a backwards
/// ankle to settle down and trail back in flight, and two wings, each an arm and a hand, that fold along the flanks.
/// A subclass builds the model in <see cref="Build"/> on these pivots and supplies the proportions; this class
/// animates the waddling walk, the crouch to drink, wingbeats that grow with how hard it's flapping, a level glide,
/// a dive with the wings half tucked, sitting, lying and clutching prey. The player tips and banks the whole bird in flight.
/// </summary>
public abstract partial class Bird : Animal
{
    /// <summary>Height of the hips above the toes; the body pivots here between standing upright and flying level.</summary>
    protected abstract float HipHeight { get; }

    /// <summary>How far the body is tipped up from level when perched or walking.</summary>
    protected abstract float StandingPitch { get; }

    /// <summary>Height of the middle of the foot above the ground; the legs reach from the hips down to it.</summary>
    protected virtual float FootHeight => 0.015f;

    // How far the body is tipped up from level, and how high the hips are, sitting and lying.
    protected virtual float SitPitch => 0.65f;
    protected virtual float SitHipHeight => 0.15f;
    protected virtual float LiePitch => 0.25f;
    protected virtual float LieHipHeight => 0.08f;

    /// <summary>Wingbeats a second, over 2π, gliding gently and flapping flat out.</summary>
    protected virtual float SlowBeat => 3f;
    protected virtual float FastBeat => 15f;

    protected Node3D Frame = null!;
    protected Node3D Body = null!;
    protected Node3D Neck = null!;
    protected Node3D Head = null!;
    protected Node3D Tail = null!;
    protected Node3D[] TailFeathers = [];
    protected Node3D[] Arms = [];
    protected Node3D[] Hands = [];
    protected Node3D[] Legs = [];
    protected Node3D[] Shanks = [];
    protected Node3D[] Feet = [];
    protected ShaderMaterial Hair = null!;
    protected float Clock;
    private float _walkCycle;
    private float _wingCycle;

    /// <summary>0 standing, 1 flying: blends the body from upright to level and the legs from under it to tucked back.</summary>
    private float _airborne;

    /// <summary>0 wings spread, 1 folded against the body.</summary>
    private float _fold = 1f;

    /// <summary>0 legs tucked back in flight, 1 reaching down to hold prey in the talons.</summary>
    private float _clutch;

    /// <summary>True while the bird carries something in its talons, so it holds its legs down under it in flight.</summary>
    public bool Clutching { get; set; }

    /// <summary>Where the talons close, between the two feet: where anything the bird carries is held.</summary>
    public Vector3 Talons => (Feet[0].GlobalPosition + Feet[1].GlobalPosition) / 2f;

    public override void _Ready() => Build();

    /// <summary>Builds the model on <see cref="Frame"/>, <see cref="Body"/> and the other pivots, filling every one in.</summary>
    protected abstract void Build();

    /// <summary>Moves anything only this bird has, after the rest of the pose is set: e.g. an owl turning its head.</summary>
    protected virtual void AnimateExtras(float eat, float air, float dt) { }

    // A bird cannot suck or lap: it scoops a beakful of water and tips its head right back to let it run down its throat.
    protected override FeedingStyle FeedingStyleFor(Food food) => food == Food.Water
        ? new() { BiteMin = 1.6f, BiteMax = 2.4f, BiteSeconds = 0.4f, Gape = 0.5f, Held = 0.1f, Gulp = 0.7f }
        : new() { BiteMin = 1f, BiteMax = 1.8f, BiteSeconds = 0.3f, Held = 0.3f, Tug = 0.4f, Gulp = 0.2f };

    public override void Animate(float speed, float stride, float eat, float dt)
    {
        Clock += dt;
        _airborne = Mathf.MoveToward(_airborne, IsFlying ? 1f : 0f, dt * 3f);
        float air = Mathf.SmoothStep(0f, 1f, _airborne);

        // Spread the wings to fly, half-tuck them to dive, and fold them away on the ground.
        _fold = Mathf.MoveToward(_fold, IsFlying ? Dive * 0.55f : 1f, dt * 3.5f);

        // Waddle: a short alternate step with a side-to-side roll of the body.
        _walkCycle += speed * 9f * dt;
        float step = Mathf.Sin(_walkCycle) * stride * (1f - air);
        Frame.Position = new Vector3(0, Mathf.Abs(step) * 0.02f, 0);
        Frame.Rotation = new Vector3(0, 0, step * 0.12f);

        // Upright when standing and tipping forward to drink; level in flight. Sitting, the bird settles low on its
        // folded legs, leaning forward a little; lying, it rests its breast on the ground, nearly level, as on a nest.
        float pitch = Pose(Mathf.Lerp(StandingPitch - eat * 0.75f, 0f, air), SitPitch, LiePitch);
        float hips = Pose(HipHeight, SitHipHeight, LieHipHeight);
        Body.Position = new Vector3(0, hips, 0);
        Body.Rotation = new Vector3(pitch, 0, 0);

        _clutch = Mathf.MoveToward(_clutch, Clutching ? 1f : 0f, dt * 4f);
        float clutch = Mathf.SmoothStep(0f, 1f, _clutch);

        // The ankle bends backwards to lower the hips, and the foot stays flat.
        float fold = FoldToReach(Shanks[0].Position, Feet[0].Position, hips - FootHeight);

        for (int i = 0; i < 2; i++)
        {
            float swing = (i == 0 ? step : -step) * 0.4f;
            // Legs hang straight down under an upright body, and trail back under the tail in flight. Carrying prey,
            // they reach down and a little forward under the body instead, toes curled to clench it in the talons.
            float flying = Mathf.Lerp(-1.35f, 0.15f, clutch);
            Legs[i].Rotation = new Vector3(Mathf.Lerp(-pitch + swing, flying, air) - fold, 0, 0);
            Shanks[i].Rotation = new Vector3(fold * 2f, 0, 0);
            Feet[i].Rotation = new Vector3(-fold + clutch * air * 0.5f, 0, 0);
        }

        // The head stays level whatever the body does, and dips to drink.
        Neck.Rotation = new Vector3(-pitch * 0.6f - eat * 0.7f, 0, 0);
        Head.Rotation = new Vector3(-pitch * 0.4f - eat * 0.4f, 0, 0);

        // The tail fans out wide in flight, to steer and brake, and tips down a little at rest. Sitting or lying, it
        // lifts to lie out behind, just clear of the ground.
        Tail.Rotation = new Vector3(Pose(Mathf.Lerp(0.15f, -0.05f, air), -0.5f, -0.1f), 0, 0);
        float fan = Mathf.Lerp(0.45f, 1f, air * (1f - _fold));
        for (int i = 0; i < TailFeathers.Length; i++)
        {
            float f = i / (TailFeathers.Length - 1f) - 0.5f;
            TailFeathers[i].Rotation = new Vector3(0, -f * 1.1f * fan, 0);
        }

        AnimateWings(dt);
        Chew(eat, dt);
        AnimateExtras(eat, air, dt);
        Hair.SetShaderParameter("sway_amount", 0.004f + 0.01f * air);
    }

    /// <summary>
    /// Wingbeats: the whole wing sweeps up and down from the shoulder, the hand following a beat behind so the tip
    /// whips through. Gliding holds the wings out flat with a slight upward V. Folding swings each wing back along
    /// the body, rolls it onto its edge against the flank and slides the long hand feathers in under the arm.
    /// </summary>
    private void AnimateWings(float dt)
    {
        _wingCycle += Mathf.Lerp(SlowBeat, FastBeat, Flap) * dt;
        float beat = Mathf.Sin(_wingCycle) * Flap;
        float fold = Mathf.SmoothStep(0f, 1f, _fold);

        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            float sweep = side * (0.08f + beat * 0.7f) * (1f - fold);
            Arms[i].Rotation = new Vector3(1.35f * fold, -side * 1.55f * fold, sweep);
            Arms[i].Scale = new Vector3(1f, 1f, Mathf.Lerp(1f, 0.5f, fold));
            Hands[i].Rotation = new Vector3(0, side * 0.8f * fold, side * Mathf.Sin(_wingCycle - 0.7f) * Flap * 0.35f * (1f - fold));
            Hands[i].Scale = new Vector3(Mathf.Lerp(1f, 0.45f, fold), 1f, 1f);
        }
    }

    /// <summary>
    /// One feather: a long, flat, slightly tapering vane rooted at <paramref name="root"/> and lying along
    /// <paramref name="along"/> in the horizontal plane, its colour varied a touch from feather to feather.
    /// <paramref name="colouring"/>, if given, colours it along its length instead, e.g. with bars across it.
    /// </summary>
    protected static ArrayMesh Feather(RandomNumberGenerator rng, Vector3 root, Vector3 along, float length, float width, Color colour,
        Material material, Func<float, Color>? colouring = null)
    {
        var across = Vector3.Up.Cross(along).Normalized();
        float shade = rng.RandfRange(0.85f, 1.1f);
        Func<Vector3, Vector3> shape = u =>
        {
            float t = (u.Z + 1f) * 0.5f;
            return root + along * length * t + across * width * u.X * (1f - 0.35f * t) + Vector3.Up * 0.004f * u.Y;
        };
        return Ellipsoid(shape, 10, 8, material, (p, _) =>
        {
            var c = colouring?.Invoke((p - root).Dot(along) / length) ?? colour;
            return new Color(c.R * shade, c.G * shade, c.B * shade);
        });
    }
}
