using System;
using Godot;

namespace IceAgeWorld;

/// <summary>
/// A bald eagle, built entirely in code to a real eagle's proportions: a dark-brown body about 80 cm long carried
/// upright on feathered "trousers" and bare yellow legs with black talons, a white head with a heavy yellow hooked
/// beak, pale yellow eyes under a jutting brow, and a white fan of a tail. Its wings span about two metres, broad
/// and plank-like with the outer flight feathers splayed into fingers at the tips. Folded, they lie along its sides
/// with the tips crossing over the tail; in flight they spread out flat. It waddles, flaps, glides and dives as every
/// <see cref="Bird"/> does. The model faces -Z.
/// </summary>
public partial class BaldEagle : Bird
{
    private const int Seed = 41;

    /// <summary>
    /// How much bigger than a real bald eagle this one is drawn: an ice-age giant with wings some four metres across,
    /// like the great eagles that took prey far bigger than any eagle alive, and bigger than the giant hares it hunts.
    /// The model is built to a real eagle's measurements and scaled up whole.
    /// </summary>
    private const float BodyScale = 2f;

    protected override float HipHeight => 0.245f;
    protected override float StandingPitch => 0.85f;

    private const float ArmLength = 0.42f;
    private const float HandLength = 0.5f;

    private static readonly Color Brown = new(0.17f, 0.11f, 0.07f);
    private static readonly Color White = new(0.95f, 0.95f, 0.93f);
    private static readonly Color Yellow = new(0.95f, 0.75f, 0.2f);

    public override string DisplayName => "Bald eagle";

    // An eagle tears its meat into strips with its hooked beak: a quick bite, then a hard yank up, twisting its head,
    // before swallowing each strip.
    protected override FeedingStyle FeedingStyleFor(Food food) => food == Food.Meat
        ? new() { BiteMin = 0.6f, BiteMax = 1.2f, BiteSeconds = 0.25f, Held = 0.2f, Tug = 0.55f, Tilt = 0.2f, SwapSide = 0.5f, Gulp = 0.15f }
        : base.FeedingStyleFor(food);
    public override string YoungName => "Bald eaglet";

    // As in most birds of prey, the female eagle is the bigger, up to a quarter heavier than her mate.
    protected override float FemaleSize => 1.08f;

    // Like most birds an eagle has a long, flexible neck under its feathers, and can look back over its shoulder.
    protected override float HeadTurn => 1.7f;
    protected override float NeckShare => 0.4f;

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

    protected override void Build()
    {
        Scale = Vector3.One * BodyScale;
        var rng = new RandomNumberGenerator { Seed = Seed };
        // An adult's body is an even dark chocolate brown, each feather edged a little paler, not mottled.
        var plumage = ProceduralTextures.Fur(Seed, new Color(0.12f, 0.075f, 0.045f), new Color(0.21f, 0.135f, 0.08f), 6f);
        var whitePlumage = ProceduralTextures.Fur(Seed + 1, new Color(0.84f, 0.84f, 0.82f), new Color(0.98f, 0.98f, 0.97f), 6f);
        var feather = new StandardMaterial3D { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 0.85f };
        var yellow = new StandardMaterial3D { AlbedoColor = Yellow, Roughness = 0.45f };
        var talon = new StandardMaterial3D { AlbedoColor = new Color(0.05f, 0.04f, 0.04f), Roughness = 0.3f };
        Hair = HairMaterial();

        var plumageColour = ProceduralTextures.Colouring(plumage);
        var whiteColour = ProceduralTextures.Colouring(whitePlumage);

        Frame = Pivot(this, "Frame", Vector3.Zero);
        Body = Pivot(Frame, "Body", new Vector3(0, HipHeight, 0));

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
        Attach(Body, "Plumage", Ellipsoid(bodyShape, 28, 18, plumage));
        Attach(Body, "Contour", Strands(rng, OnShape(rng, bodyShape, 2600, _ => true), new Vector3(0, 0, 1f),
            0.022f, 0.036f, Colors.White, Colors.White, Hair, width: 0.022f, colouring: plumageColour));

        // Neck and head, pure white, the head flat-crowned with a heavy brow over the eyes.
        Neck = Pivot(Body, "Neck", new Vector3(0, 0.08f, -0.27f));
        var headPosition = new Vector3(0, 0.06f, -0.06f);
        Attach(Neck, "Throat", Tube([new Vector3(0, -0.02f, 0.04f), headPosition], [0.075f, 0.055f], 12, whitePlumage, capEnd: false));
        Attach(Neck, "Hackles", Strands(rng, OnSegment(rng, 500, new Vector3(0, -0.02f, 0.04f), headPosition, 0.075f, 0.055f),
            new Vector3(0, -0.2f, 1f), 0.022f, 0.04f, Colors.White, Colors.White, Hair, width: 0.014f, colouring: whiteColour));
        Head = Pivot(Neck, "Head", headPosition);
        BuildHead(Head, rng, whitePlumage, whiteColour, yellow);

        // Tail: a fan of broad white feathers from the rump.
        Tail = Pivot(Body, "Tail", new Vector3(0, 0.04f, 0.17f));
        TailFeathers = new Node3D[9];
        for (int i = 0; i < TailFeathers.Length; i++)
        {
            TailFeathers[i] = Pivot(Tail, "TailFeather", Vector3.Zero);
            Attach(TailFeathers[i], "Vane", Feather(rng, new Vector3(0, i * 0.0015f, 0), Vector3.Back, 0.3f, 0.04f, White, feather));
        }

        // Legs: shaggy brown "trousers" over the thighs, then bare yellow scaly legs and big yellow feet with black talons.
        Legs = new Node3D[2];
        Shanks = new Node3D[2];
        Feet = new Node3D[2];
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            Legs[i] = Pivot(Body, "Leg", new Vector3(side * 0.06f, 0f, 0f));
            var thigh = new Vector3(0.045f, 0.075f, 0.05f);
            Attach(Legs[i], "Trousers", Ellipsoid(thigh, 12, 10, plumage), new Vector3(0, -0.05f, 0));
            Attach(Legs[i], "TrouserFeathers", Strands(rng, OnShape(rng, u => u * thigh + new Vector3(0, -0.05f, 0), 220, _ => true),
                new Vector3(0, -1f, 0), 0.03f, 0.05f, Colors.White, Colors.White, Hair, width: 0.022f, colouring: plumageColour));
            // The bare leg hinges below the trousers at the ankle, which bends backwards, so the eagle can settle down
            // onto its folded legs; the foot hinges again to stay flat on the ground.
            Shanks[i] = Pivot(Legs[i], "Shank", new Vector3(0, -0.1f, 0));
            Attach(Shanks[i], "Shank", Tube([Vector3.Zero, new Vector3(0, -0.125f, 0)], [0.016f, 0.014f], 8, yellow, capEnd: false));
            // The feathering runs on down over the top half of the leg, as in all true eagles; only the lower half is bare.
            Attach(Shanks[i], "ShankFeathers", Strands(rng, OnSegment(rng, 140, new Vector3(0, 0.01f, 0), new Vector3(0, -0.055f, 0), 0.02f, 0.017f),
                new Vector3(0, -1f, 0), 0.02f, 0.032f, Colors.White, Colors.White, Hair, width: 0.016f, colouring: plumageColour));

            Feet[i] = Pivot(Shanks[i], "Foot", new Vector3(0, -0.13f, 0));
            var foot = Vector3.Zero;
            Attach(Feet[i], "Ball", new SphereMesh { Radius = 0.018f, Height = 0.036f, Material = yellow }, foot);
            foreach (float angle in new[] { -0.45f, 0f, 0.45f, Mathf.Pi })
            {
                // Three thick toes spread forward and one points back, each ending in a long black talon curved like a
                // sickle; the back one, which does most of the killing, is the biggest.
                var dir = new Vector3(Mathf.Sin(angle), 0, -Mathf.Cos(angle));
                bool hallux = angle == Mathf.Pi;
                float claw = hallux ? 1.3f : 1f;
                var tip = foot + dir * (hallux ? 0.045f : 0.06f) + Vector3.Down * 0.006f;
                Attach(Feet[i], "Toe", Tube([foot, foot + dir * 0.03f + Vector3.Up * 0.003f, tip], [0.013f, 0.011f, 0.009f], 8, yellow, capEnd: true));
                Attach(Feet[i], "Talon", Tube([tip, tip + (dir * 0.014f + Vector3.Up * 0.005f) * claw, tip + (dir * 0.026f + Vector3.Down * 0.002f) * claw,
                    tip + (dir * 0.028f + Vector3.Down * 0.016f) * claw], [0.0068f, 0.0052f, 0.003f, 0.0008f], 6, talon, capEnd: true));
            }
        }

        // Wings: an arm and a hand, each a broad covert panel over a row of long flight feathers.
        Arms = new Node3D[2];
        Hands = new Node3D[2];
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            var outward = new Vector3(side, 0, 0);
            Arms[i] = Pivot(Body, "Wing", new Vector3(side * 0.085f, 0.09f, -0.16f));

            // Coverts over the arm, then a row of greater coverts lapping over the bases of the secondaries, which overlap
            // each other into one unbroken trailing edge, each a little longer towards the body.
            Attach(Arms[i], "Coverts", Ellipsoid(new Vector3(ArmLength * 0.6f, 0.022f, 0.12f), 18, 8, plumage),
                outward * ArmLength * 0.5f + new Vector3(0, 0.008f, 0.05f));
            for (int f = 0; f < 12; f++)
            {
                float x = Mathf.Lerp(0.03f, ArmLength, f / 11f);
                Attach(Arms[i], "GreaterCovert", Feather(rng, outward * x + new Vector3(0, 0.006f + 0.001f * (f % 2), 0.06f), Vector3.Back,
                    0.14f, 0.04f, Brown * 1.15f, feather));
            }
            for (int f = 0; f < 16; f++)
            {
                float x = Mathf.Lerp(0.03f, ArmLength, f / 15f);
                Attach(Arms[i], "Secondary", Feather(rng, outward * x + new Vector3(0, -0.003f * (f % 2), 0.02f), Vector3.Back,
                    Mathf.Lerp(0.3f, 0.27f, f / 15f), 0.04f, Brown, feather));
            }

            // The hand: primaries fan from pointing back at the wrist to pointing out at the tip. The inner ones are broad
            // and overlap; the outer six are narrow near their ends and stand apart as the "fingers" eagles soar on.
            Hands[i] = Pivot(Arms[i], "Hand", outward * ArmLength);
            Attach(Hands[i], "Coverts", Ellipsoid(new Vector3(HandLength * 0.38f, 0.018f, 0.1f), 14, 8, plumage),
                outward * HandLength * 0.25f + new Vector3(0, 0.008f, 0.04f));
            for (int f = 0; f < 10; f++)
            {
                float t = f / 9f;
                bool finger = f >= 4;
                float angle = finger ? Mathf.Lerp(0.55f, 1.3f, (f - 4) / 5f) : Mathf.Lerp(0.12f, 0.45f, f / 3f);
                var dir = new Vector3(side * Mathf.Sin(angle), 0, Mathf.Cos(angle));
                var root = outward * Mathf.Lerp(0.02f, HandLength * 0.42f, t) + new Vector3(0, -0.003f * (f % 2), 0.02f);
                Attach(Hands[i], "Primary", Feather(rng, root, dir, Mathf.Lerp(0.3f, 0.42f, Mathf.Sin(t * 2.6f)), finger ? 0.027f : 0.042f,
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
            0.012f, 0.022f, Colors.White, Colors.White, Hair, width: 0.011f, colouring: whiteColour));

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
}
