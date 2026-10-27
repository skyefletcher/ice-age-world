using System;
using Godot;

namespace IceAgeWorld;

/// <summary>
/// A bald eagle, built entirely in code to a real eagle's proportions: a dark-brown body about 80 cm long carried
/// upright on feathered "trousers" and bare yellow legs with black talons, a white head with a heavy yellow hooked
/// beak, pale yellow eyes under a jutting brow, and a white fan of a tail. Its wings span about two metres, broad
/// and plank-like with the outer flight feathers splayed into fingers at the tips. Folded, they lie along its sides
/// with the tips crossing over the tail; in flight they spread out flat. It animates its own waddling walk, a
/// standing crouch to drink, wingbeats that grow with how hard it's flapping, a level glide, and a dive with the
/// wings half tucked. The model faces -Z; the player tips and banks the whole bird in flight.
/// </summary>
public partial class BaldEagle : Animal
{
    private const int Seed = 41;

    /// <summary>
    /// How much bigger than a real bald eagle this one is drawn: an ice-age giant with wings some four metres across,
    /// like the great eagles that took prey far bigger than any eagle alive, and bigger than the giant hares it hunts.
    /// The model is built to a real eagle's measurements and scaled up whole.
    /// </summary>
    private const float BodyScale = 2f;

    /// <summary>Height of the hips above the toes; the body pivots here between standing upright and flying level.</summary>
    private const float HipHeight = 0.245f;

    /// <summary>How far the body is tipped up from level when perched or walking.</summary>
    private const float StandingPitch = 0.85f;

    /// <summary>Height of the middle of the foot above the ground; the legs reach from the hips down to it.</summary>
    private const float FootHeight = 0.015f;

    // How far the body is tipped up from level, and how high the hips are, sitting and lying.
    private const float SitPitch = 0.65f;
    private const float SitHipHeight = 0.15f;
    private const float LiePitch = 0.25f;
    private const float LieHipHeight = 0.08f;

    private const float ArmLength = 0.42f;
    private const float HandLength = 0.5f;

    private static readonly Color Brown = new(0.17f, 0.11f, 0.07f);
    private static readonly Color White = new(0.95f, 0.95f, 0.93f);
    private static readonly Color Yellow = new(0.95f, 0.75f, 0.2f);

    private Node3D _frame = null!;
    private Node3D _body = null!;
    private Node3D _neck = null!;
    private Node3D _head = null!;
    private Node3D _tail = null!;
    private Node3D[] _tailFeathers = [];
    private Node3D[] _arms = [];
    private Node3D[] _hands = [];
    private Node3D[] _legs = [];
    private Node3D[] _shanks = [];
    private Node3D[] _feet = [];
    private ShaderMaterial _hair = null!;
    private float _walkCycle;
    private float _wingCycle;
    private float _time;

    /// <summary>0 standing, 1 flying: blends the body from upright to level and the legs from under it to tucked back.</summary>
    private float _airborne;

    /// <summary>0 wings spread, 1 folded against the body.</summary>
    private float _fold = 1f;

    /// <summary>0 legs tucked back in flight, 1 reaching down to hold prey in the talons.</summary>
    private float _clutch;

    /// <summary>True while the eagle carries something in its talons, so it holds its legs down under it in flight.</summary>
    public bool Clutching { get; set; }

    /// <summary>Where the talons close, between the two feet: where anything the eagle carries is held.</summary>
    public Vector3 Talons => (_feet[0].GlobalPosition + _feet[1].GlobalPosition) / 2f;

    public override string DisplayName => "Bald eagle";
    public override string YoungName => "Bald eaglet";

    public override AnimalStats Stats { get; } = new()
    {
        Scores = new() { JumpHeight = 2, JumpLength = 2, LandSpeed = 3, WaterSpeed = 3, Agility = 2, Stamina = 9, FlightAgility = 10 },
        Abilities = Ability.Fly | Ability.Hunt,
        FlySpeed = 16f,
        FloatDepth = 0.15f * BodyScale,
        WadeDepth = 0.12f * BodyScale,
        MouthDistance = 0.4f * BodyScale,
        EatReach = 0.3f * BodyScale,
        CanGraze = false,
        BodyRadius = 0.22f * BodyScale,
        BodyHeight = 0.7f * BodyScale,
        CameraHeight = 0.55f * BodyScale,
        CameraDistance = 3.2f * BodyScale,
    };

    public override void _Ready() => Build();

    public override void Animate(float speed, float stride, float eat, float dt)
    {
        _time += dt;
        _airborne = Mathf.MoveToward(_airborne, IsFlying ? 1f : 0f, dt * 3f);
        float air = Mathf.SmoothStep(0f, 1f, _airborne);

        // Spread the wings to fly, half-tuck them to dive, and fold them away on the ground.
        _fold = Mathf.MoveToward(_fold, IsFlying ? Dive * 0.55f : 1f, dt * 3.5f);

        // Waddle: a short alternate step with a side-to-side roll of the body.
        _walkCycle += speed * 9f * dt;
        float step = Mathf.Sin(_walkCycle) * stride * (1f - air);
        _frame.Position = new Vector3(0, Mathf.Abs(step) * 0.02f, 0);
        _frame.Rotation = new Vector3(0, 0, step * 0.12f);

        // Upright when standing and tipping forward to drink; level in flight. Sitting, the eagle settles low on its
        // folded legs, leaning forward a little; lying, it rests its breast on the ground, nearly level, as on a nest.
        float pitch = Pose(Mathf.Lerp(StandingPitch - eat * 0.75f, 0f, air), SitPitch, LiePitch);
        float hips = Pose(HipHeight, SitHipHeight, LieHipHeight);
        _body.Position = new Vector3(0, hips, 0);
        _body.Rotation = new Vector3(pitch, 0, 0);

        _clutch = Mathf.MoveToward(_clutch, Clutching ? 1f : 0f, dt * 4f);
        float clutch = Mathf.SmoothStep(0f, 1f, _clutch);

        // The ankle bends backwards to lower the hips, and the foot stays flat.
        float fold = FoldToReach(_shanks[0].Position, _feet[0].Position, hips - FootHeight);

        for (int i = 0; i < 2; i++)
        {
            float swing = (i == 0 ? step : -step) * 0.4f;
            // Legs hang straight down under an upright body, and trail back under the tail in flight. Carrying prey,
            // they reach down and a little forward under the body instead, toes curled to clench it in the talons.
            float flying = Mathf.Lerp(-1.35f, 0.15f, clutch);
            _legs[i].Rotation = new Vector3(Mathf.Lerp(-pitch + swing, flying, air) - fold, 0, 0);
            _shanks[i].Rotation = new Vector3(fold * 2f, 0, 0);
            _feet[i].Rotation = new Vector3(-fold + clutch * air * 0.5f, 0, 0);
        }

        // The head stays level whatever the body does, and dips to drink.
        _neck.Rotation = new Vector3(-pitch * 0.6f - eat * 0.7f, 0, 0);
        _head.Rotation = new Vector3(-pitch * 0.4f - eat * 0.4f, 0, 0);

        // The tail fans out wide in flight, to steer and brake, and tips down a little at rest. Sitting or lying, it
        // lifts to lie out behind, just clear of the ground.
        _tail.Rotation = new Vector3(Pose(Mathf.Lerp(0.15f, -0.05f, air), -0.5f, -0.1f), 0, 0);
        float fan = Mathf.Lerp(0.45f, 1f, air * (1f - _fold));
        for (int i = 0; i < _tailFeathers.Length; i++)
        {
            float f = i / (_tailFeathers.Length - 1f) - 0.5f;
            _tailFeathers[i].Rotation = new Vector3(0, -f * 1.1f * fan, 0);
        }

        AnimateWings(dt);
        Chew(eat, dt);
        _hair.SetShaderParameter("sway_amount", 0.004f + 0.01f * air);
    }

    /// <summary>
    /// Wingbeats: the whole wing sweeps up and down from the shoulder, the hand following a beat behind so the tip
    /// whips through. Gliding holds the wings out flat with a slight upward V. Folding swings each wing back along
    /// the body, rolls it onto its edge against the flank and slides the long hand feathers in under the arm.
    /// </summary>
    private void AnimateWings(float dt)
    {
        _wingCycle += Mathf.Lerp(3f, 15f, Flap) * dt;
        float beat = Mathf.Sin(_wingCycle) * Flap;
        float fold = Mathf.SmoothStep(0f, 1f, _fold);

        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            float sweep = side * (0.08f + beat * 0.7f) * (1f - fold);
            _arms[i].Rotation = new Vector3(1.35f * fold, -side * 1.55f * fold, sweep);
            _arms[i].Scale = new Vector3(1f, 1f, Mathf.Lerp(1f, 0.5f, fold));
            _hands[i].Rotation = new Vector3(0, side * 0.8f * fold, side * Mathf.Sin(_wingCycle - 0.7f) * Flap * 0.35f * (1f - fold));
            _hands[i].Scale = new Vector3(Mathf.Lerp(1f, 0.45f, fold), 1f, 1f);
        }
    }

    private void Build()
    {
        Scale = Vector3.One * BodyScale;
        var rng = new RandomNumberGenerator { Seed = Seed };
        // An adult's body is an even dark chocolate brown, each feather edged a little paler, not mottled.
        var plumage = ProceduralTextures.Fur(Seed, new Color(0.12f, 0.075f, 0.045f), new Color(0.21f, 0.135f, 0.08f), 6f);
        var whitePlumage = ProceduralTextures.Fur(Seed + 1, new Color(0.84f, 0.84f, 0.82f), new Color(0.98f, 0.98f, 0.97f), 6f);
        var feather = new StandardMaterial3D { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 0.85f };
        var yellow = new StandardMaterial3D { AlbedoColor = Yellow, Roughness = 0.45f };
        var talon = new StandardMaterial3D { AlbedoColor = new Color(0.05f, 0.04f, 0.04f), Roughness = 0.3f };
        _hair = HairMaterial();

        var plumageColour = ProceduralTextures.Colouring(plumage);
        var whiteColour = ProceduralTextures.Colouring(whitePlumage);

        _frame = Pivot(this, "Frame", Vector3.Zero);
        _body = Pivot(_frame, "Body", new Vector3(0, HipHeight, 0));

        // Body: a deep-chested teardrop from breast to tail, in layered contour feathers.
        var bodyRadii = new Vector3(0.12f, 0.12f, 0.25f);
        var bodyPosition = new Vector3(0, 0.05f, -0.06f);
        Func<Vector3, Vector3> bodyShape = u =>
        {
            var p = u * bodyRadii;
            p.X *= 1f - 0.3f * Mathf.Max(0f, u.Z);
            p.Y *= 1f - 0.3f * Mathf.Max(0f, u.Z);
            return p + bodyPosition;
        };
        Attach(_body, "Plumage", Ellipsoid(bodyShape, 28, 18, plumage));
        Attach(_body, "Contour", Strands(rng, OnShape(rng, bodyShape, 2600, _ => true), new Vector3(0, 0, 1f),
            0.022f, 0.036f, Colors.White, Colors.White, _hair, width: 0.022f, colouring: plumageColour));

        // Neck and head, pure white, the head flat-crowned with a heavy brow over the eyes.
        _neck = Pivot(_body, "Neck", new Vector3(0, 0.08f, -0.27f));
        var headPosition = new Vector3(0, 0.06f, -0.06f);
        Attach(_neck, "Throat", Tube([new Vector3(0, -0.02f, 0.04f), headPosition], [0.075f, 0.055f], 12, whitePlumage, capEnd: false));
        Attach(_neck, "Hackles", Strands(rng, OnSegment(rng, 500, new Vector3(0, -0.02f, 0.04f), headPosition, 0.075f, 0.055f),
            new Vector3(0, -0.2f, 1f), 0.022f, 0.04f, Colors.White, Colors.White, _hair, width: 0.014f, colouring: whiteColour));
        _head = Pivot(_neck, "Head", headPosition);
        BuildHead(_head, rng, whitePlumage, whiteColour, yellow);

        // Tail: a fan of broad white feathers from the rump.
        _tail = Pivot(_body, "Tail", new Vector3(0, 0.04f, 0.17f));
        _tailFeathers = new Node3D[9];
        for (int i = 0; i < _tailFeathers.Length; i++)
        {
            _tailFeathers[i] = Pivot(_tail, "TailFeather", Vector3.Zero);
            Attach(_tailFeathers[i], "Vane", Feather(rng, new Vector3(0, i * 0.0015f, 0), Vector3.Back, 0.3f, 0.04f, White, feather));
        }

        // Legs: shaggy brown "trousers" over the thighs, then bare yellow scaly legs and big yellow feet with black talons.
        _legs = new Node3D[2];
        _shanks = new Node3D[2];
        _feet = new Node3D[2];
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            _legs[i] = Pivot(_body, "Leg", new Vector3(side * 0.06f, 0f, 0f));
            var thigh = new Vector3(0.045f, 0.075f, 0.05f);
            Attach(_legs[i], "Trousers", Ellipsoid(thigh, 12, 10, plumage), new Vector3(0, -0.05f, 0));
            Attach(_legs[i], "TrouserFeathers", Strands(rng, OnShape(rng, u => u * thigh + new Vector3(0, -0.05f, 0), 220, _ => true),
                new Vector3(0, -1f, 0), 0.03f, 0.05f, Colors.White, Colors.White, _hair, width: 0.022f, colouring: plumageColour));
            // The bare leg hinges below the trousers at the ankle, which bends backwards, so the eagle can settle down
            // onto its folded legs; the foot hinges again to stay flat on the ground.
            _shanks[i] = Pivot(_legs[i], "Shank", new Vector3(0, -0.1f, 0));
            Attach(_shanks[i], "Shank", Tube([Vector3.Zero, new Vector3(0, -0.125f, 0)], [0.016f, 0.014f], 8, yellow, capEnd: false));
            // The feathering runs on down over the top half of the leg, as in all true eagles; only the lower half is bare.
            Attach(_shanks[i], "ShankFeathers", Strands(rng, OnSegment(rng, 140, new Vector3(0, 0.01f, 0), new Vector3(0, -0.055f, 0), 0.02f, 0.017f),
                new Vector3(0, -1f, 0), 0.02f, 0.032f, Colors.White, Colors.White, _hair, width: 0.016f, colouring: plumageColour));

            _feet[i] = Pivot(_shanks[i], "Foot", new Vector3(0, -0.13f, 0));
            var foot = Vector3.Zero;
            Attach(_feet[i], "Ball", new SphereMesh { Radius = 0.018f, Height = 0.036f, Material = yellow }, foot);
            foreach (float angle in new[] { -0.45f, 0f, 0.45f, Mathf.Pi })
            {
                // Three thick toes spread forward and one points back, each ending in a long black talon curved like a
                // sickle; the back one, which does most of the killing, is the biggest.
                var dir = new Vector3(Mathf.Sin(angle), 0, -Mathf.Cos(angle));
                bool hallux = angle == Mathf.Pi;
                float claw = hallux ? 1.3f : 1f;
                var tip = foot + dir * (hallux ? 0.045f : 0.06f) + Vector3.Down * 0.006f;
                Attach(_feet[i], "Toe", Tube([foot, foot + dir * 0.03f + Vector3.Up * 0.003f, tip], [0.013f, 0.011f, 0.009f], 8, yellow, capEnd: true));
                Attach(_feet[i], "Talon", Tube([tip, tip + (dir * 0.014f + Vector3.Up * 0.005f) * claw, tip + (dir * 0.026f + Vector3.Down * 0.002f) * claw,
                    tip + (dir * 0.028f + Vector3.Down * 0.016f) * claw], [0.0068f, 0.0052f, 0.003f, 0.0008f], 6, talon, capEnd: true));
            }
        }

        // Wings: an arm and a hand, each a broad covert panel over a row of long flight feathers.
        _arms = new Node3D[2];
        _hands = new Node3D[2];
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            var outward = new Vector3(side, 0, 0);
            _arms[i] = Pivot(_body, "Wing", new Vector3(side * 0.085f, 0.09f, -0.16f));

            // Coverts over the arm, then a row of greater coverts lapping over the bases of the secondaries, which overlap
            // each other into one unbroken trailing edge, each a little longer towards the body.
            Attach(_arms[i], "Coverts", Ellipsoid(new Vector3(ArmLength * 0.6f, 0.022f, 0.12f), 18, 8, plumage),
                outward * ArmLength * 0.5f + new Vector3(0, 0.008f, 0.05f));
            for (int f = 0; f < 12; f++)
            {
                float x = Mathf.Lerp(0.03f, ArmLength, f / 11f);
                Attach(_arms[i], "GreaterCovert", Feather(rng, outward * x + new Vector3(0, 0.006f + 0.001f * (f % 2), 0.06f), Vector3.Back,
                    0.14f, 0.04f, Brown * 1.15f, feather));
            }
            for (int f = 0; f < 16; f++)
            {
                float x = Mathf.Lerp(0.03f, ArmLength, f / 15f);
                Attach(_arms[i], "Secondary", Feather(rng, outward * x + new Vector3(0, -0.003f * (f % 2), 0.02f), Vector3.Back,
                    Mathf.Lerp(0.3f, 0.27f, f / 15f), 0.04f, Brown, feather));
            }

            // The hand: primaries fan from pointing back at the wrist to pointing out at the tip. The inner ones are broad
            // and overlap; the outer six are narrow near their ends and stand apart as the "fingers" eagles soar on.
            _hands[i] = Pivot(_arms[i], "Hand", outward * ArmLength);
            Attach(_hands[i], "Coverts", Ellipsoid(new Vector3(HandLength * 0.38f, 0.018f, 0.1f), 14, 8, plumage),
                outward * HandLength * 0.25f + new Vector3(0, 0.008f, 0.04f));
            for (int f = 0; f < 10; f++)
            {
                float t = f / 9f;
                bool finger = f >= 4;
                float angle = finger ? Mathf.Lerp(0.55f, 1.3f, (f - 4) / 5f) : Mathf.Lerp(0.12f, 0.45f, f / 3f);
                var dir = new Vector3(side * Mathf.Sin(angle), 0, Mathf.Cos(angle));
                var root = outward * Mathf.Lerp(0.02f, HandLength * 0.42f, t) + new Vector3(0, -0.003f * (f % 2), 0.02f);
                Attach(_hands[i], "Primary", Feather(rng, root, dir, Mathf.Lerp(0.3f, 0.42f, Mathf.Sin(t * 2.6f)), finger ? 0.027f : 0.042f,
                    Brown, feather));
            }
        }
    }

    /// <summary>
    /// The white head: a flat-crowned skull with a jutting brow that gives the eagle its stern look, pale yellow eyes,
    /// and a big yellow beak that is deep at the base, where the waxy cere holds the nostrils, and hooks down to a point.
    /// </summary>
    private void BuildHead(Node3D head, RandomNumberGenerator rng, StandardMaterial3D white, Func<Vector3, Vector3, Color> whiteColour,
        StandardMaterial3D yellow)
    {
        var dark = new StandardMaterial3D { AlbedoColor = new Color(0.05f, 0.04f, 0.04f), Roughness = 0.5f };

        var skull = new Vector3(0.055f, 0.05f, 0.072f);
        Attach(head, "Skull", Ellipsoid(u => (u * skull) with { Y = u.Y * skull.Y * (u.Y > 0 ? 0.85f : 1f) }, 22, 16, white));
        Attach(head, "Feathers", Strands(rng, OnShape(rng, u => u * skull, 700, u => u.Z > -0.6f), new Vector3(0, -0.1f, 1f),
            0.012f, 0.022f, Colors.White, Colors.White, _hair, width: 0.011f, colouring: whiteColour));

        // Beak: the upper mandible sweeps forward from the cere and hooks down over a smaller lower one.
        Vector3 p0 = new(0, 0.002f, -0.05f), p1 = new(0, 0.01f, -0.09f), p2 = new(0, -0.002f, -0.12f), p3 = new(0, -0.036f, -0.115f);
        const int steps = 12;
        var path = new Vector3[steps + 1];
        var radii = new float[steps + 1];
        for (int s = 0; s <= steps; s++)
        {
            float t = s / (float)steps;
            path[s] = p0.BezierInterpolate(p1, p2, p3, t);
            radii[s] = Mathf.Lerp(0.025f, 0.003f, Mathf.Pow(t, 1.2f));
        }
        // It is narrow from side to side and deep from top to bottom: a blade for tearing rather than a round cone.
        Attach(head, "Beak", Tube(path, radii, 12, yellow, capEnd: true)).Scale = new Vector3(0.72f, 1f, 1f);
        // The cere: a band of softer, paler waxy skin over the base of the beak, where the nostrils open.
        var cere = new StandardMaterial3D { AlbedoColor = new Color(0.98f, 0.84f, 0.38f), Roughness = 0.7f };
        Attach(head, "Cere", Tube([new Vector3(0, 0.003f, -0.045f), new Vector3(0, 0.005f, -0.068f)], [0.024f, 0.022f], 12, cere, capEnd: false))
            .Scale = new Vector3(0.76f, 1f, 1f);
        // The lower mandible hinges at the corner of the mouth, so the beak gapes to tear at food or drink. It runs
        // nearly the length of the upper one, tucked inside the hook.
        var jaw = Jaw(head, new Vector3(0, -0.012f, -0.045f));
        Attach(jaw, "LowerBeak", Tube([new Vector3(0, -0.002f, -0.003f), new Vector3(0, -0.005f, -0.03f), new Vector3(0, -0.002f, -0.05f)],
            [0.014f, 0.008f, 0.003f], 10, yellow, capEnd: true)).Scale = new Vector3(0.7f, 1f, 1f);
        MouthInside(jaw, new Vector3(0.006f, 0.003f, 0.018f), new Vector3(0, 0.004f, -0.018f));

        foreach (float side in new[] { -1f, 1f })
        {
            // A slit of a nostril on the cere, angled back along the beak.
            Attach(head, "Nostril", Ellipsoid(new Vector3(0.0015f, 0.003f, 0.0065f), 8, 6, dark), new Vector3(side * 0.0165f, 0.009f, -0.058f))
                .Rotation = new Vector3(-0.35f, 0, 0);
            // The yellow gape: the corner of the mouth runs back to below the eye.
            Attach(head, "Gape", Tube([new Vector3(side * 0.016f, -0.011f, -0.052f), new Vector3(side * 0.027f, -0.006f, -0.034f)],
                [0.004f, 0.0025f], 6, yellow, capEnd: true));

            // Eyes face forward and out, giving the eagle its binocular stare, deep-set under a ledge of brow.
            Eye(head, new Vector3(side * 0.039f, 0.01f, -0.038f), new Vector3(0, -side * 0.6f, 0), 0.011f, new Color(0.96f, 0.88f, 0.5f), 0.45f);
            Attach(head, "Brow", Ellipsoid(new Vector3(0.017f, 0.007f, 0.026f), 10, 6, white), new Vector3(side * 0.03f, 0.022f, -0.041f));
        }
    }

    /// <summary>
    /// One feather: a long, flat, slightly tapering vane rooted at <paramref name="root"/> and lying along
    /// <paramref name="along"/> in the horizontal plane, its colour varied a touch from feather to feather.
    /// </summary>
    private static ArrayMesh Feather(RandomNumberGenerator rng, Vector3 root, Vector3 along, float length, float width, Color colour,
        Material material)
    {
        var across = Vector3.Up.Cross(along).Normalized();
        float shade = rng.RandfRange(0.85f, 1.1f);
        Func<Vector3, Vector3> shape = u =>
        {
            float t = (u.Z + 1f) * 0.5f;
            return root + along * length * t + across * width * u.X * (1f - 0.35f * t) + Vector3.Up * 0.004f * u.Y;
        };
        return Ellipsoid(shape, 10, 8, material, (_, n) => new Color(colour.R * shade, colour.G * shade, colour.B * shade));
    }
}
