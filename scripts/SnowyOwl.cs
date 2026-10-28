using System;
using System.Collections.Generic;
using Godot;

namespace IceAgeWorld;

/// <summary>
/// A snowy owl, built entirely in code to a real owl's proportions: a plump, round body about 60 cm long, standing
/// very upright, with almost no neck under a big round head. The face is a flat disc of feathers with two huge
/// forward-facing yellow eyes and a small black hooked beak half buried in bristles. The legs are feathered all the
/// way down to the toes, like thick white slippers, with long black talons poking out. The wings span about one and a
/// half metres, broad and rounded at the tips, and the tail is drawn long. A cock is nearly pure white, flecked with only a
/// few dark marks; a hen is bigger and barred all over with dark crescents, as camouflage on her nest on the open
/// tundra. It waddles, flaps, glides and dives as every <see cref="Bird"/> does, and can turn its head right round over
/// its shoulder, as owls do, their eyes being fixed in their sockets. The model faces -Z.
/// </summary>
public partial class SnowyOwl : Bird
{
    private const int Seed = 53;

    /// <summary>
    /// How much bigger than a real snowy owl this one is drawn: an ice-age giant with wings well over three metres across,
    /// big enough to carry off the giant hares it hunts, as the eagle does. The model is built to a real owl's
    /// measurements and scaled up whole.
    /// </summary>
    private const float BodyScale = 1.8f;

    // The legs: the thigh hinges at the hip, the ankle below it, and the foot below that.
    private const float ThighLength = 0.1f;
    private const float ShankLength = 0.1f;

    protected override float HipHeight => ThighLength + ShankLength + FootHeight;

    // Owls perch more upright than eagles, the body nearly vertical under the head.
    protected override float StandingPitch => 0.95f;

    protected override float SitHipHeight => 0.11f;
    protected override float LieHipHeight => 0.07f;

    // Owls beat their broad wings slowly and deeply, and silently: soft fringes on the flight feathers muffle them.
    protected override float SlowBeat => 2.5f;
    protected override float FastBeat => 11f;

    /// <summary>
    /// How much bigger the wings are drawn than a real owl's, against its body: long, broad wings to carry a giant's
    /// weight on slow, deep beats. Scales the wing's length, its depth and its flight feathers together.
    /// </summary>
    private const float WingScale = 1.3f;

    /// <summary>
    /// How much longer the tail is drawn than a real snowy owl's short, rounded one: a long fan trailing out behind,
    /// to steer the giant's slow, low flight.
    /// </summary>
    private const float TailScale = 2.2f;

    private const float ArmLength = 0.32f * WingScale;
    private const float HandLength = 0.36f * WingScale;

    private static readonly Color White = new(0.96f, 0.96f, 0.95f);
    private static readonly Color Bar = new(0.16f, 0.13f, 0.11f);
    private static readonly Color Yellow = new(0.98f, 0.8f, 0.12f);

    /// <summary>
    /// One plumage: the body's feathers, the hair colouring that matches them, and the colour along a flight feather
    /// from root (0) to tip (1).
    /// </summary>
    private sealed record Coat(StandardMaterial3D Plumage, Func<Vector3, Vector3, Color> Colouring, Func<float, Color> Flight);

    /// <summary>The cock's coat and the hen's, indexed by <see cref="Sex"/>.</summary>
    private Coat[] _coats = [];

    /// <summary>The parts dressed in each coat, indexed by <see cref="Sex"/>; only the one matching the owl's sex shows.</summary>
    private readonly List<Node3D>[] _dressed = [[], []];


    private Sex? _shownCoat;


    public override string DisplayName => "Snowy owl";

    // An owl swallows small prey whole and big prey in large chunks: a slow pull, then a great gulp with its head
    // thrown back.
    protected override FeedingStyle FeedingStyleFor(Food food) => food == Food.Meat
        ? new() { BiteMin = 1.2f, BiteMax = 2.2f, BiteSeconds = 0.35f, Held = 0.3f, Tug = 0.3f, Tilt = 0.1f, Gulp = 0.5f }
        : base.FeedingStyleFor(food);
    public override string YoungName => "Snowy owlet";

    // The hen snowy owl is the bigger of the pair, as in most owls, by up to a fifth.
    protected override float FemaleSize => 1.1f;

    // An owl's eyes are fixed in its skull, so it turns its whole head instead, up to 135 degrees either way, 270 in all,
    // on a neck with twice as many bones as ours, while its body stays still.
    protected override float HeadTurn => 2.4f;
    protected override float NeckShare => 0.15f;

    public override AnimalStats Stats { get; } = new()
    {
        // Less of a soarer than the eagle and slower in the air, but quick and nimble on the wing close to the ground,
        // where it hunts.
        Scores = new() { JumpHeight = 2, JumpLength = 2, LandSpeed = 3, WaterSpeed = 2, Agility = 3, Stamina = 7, FlightAgility = 9 },
        Abilities = Ability.Fly | Ability.Hunt,
        FlySpeed = 13f,
        FloatDepth = 0.13f * BodyScale,
        WadeDepth = 0.1f * BodyScale,
        MouthDistance = 0.3f * BodyScale,
        EatReach = 0.3f * BodyScale,
        CanGraze = false,
        BodyRadius = 0.2f * BodyScale,
        BodyHeight = 0.6f * BodyScale,
        CameraHeight = 0.5f * BodyScale,
        CameraDistance = 3.2f * BodyScale,
    };

    protected override void AnimateExtras(float eat, float air, float dt)
    {
        if (_shownCoat != Sex)
        {
            _shownCoat = Sex;
            for (int c = 0; c < _dressed.Length; c++)
                foreach (var part in _dressed[c])
                    part.Visible = c == (int)Sex;
        }
    }

    protected override void Build()
    {
        Scale = Vector3.One * BodyScale;
        var rng = new RandomNumberGenerator { Seed = Seed };
        var feather = new StandardMaterial3D { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 0.9f };
        var talon = new StandardMaterial3D { AlbedoColor = new Color(0.04f, 0.035f, 0.035f), Roughness = 0.3f };
        Hair = HairMaterial();

        var cream = new Color(0.9f, 0.9f, 0.88f);
        _coats = new Coat[2];
        _coats[(int)Sex.Male] = MakeCoat(ProceduralTextures.BarredFur(Seed, cream, White, Bar, 6f, 0.06f), 0f);
        _coats[(int)Sex.Female] = MakeCoat(ProceduralTextures.BarredFur(Seed, cream, White, Bar, 6f, 0.85f), 1f);

        Frame = Pivot(this, "Frame", Vector3.Zero);
        Body = Pivot(Frame, "Body", new Vector3(0, HipHeight, 0));

        // Body: a plump, rounded barrel, broad at the breast and hardly tapering to the tail, made rounder still by
        // a deep layer of soft, fluffy feathers against the arctic cold.
        var bodyRadii = new Vector3(0.13f, 0.13f, 0.2f);
        var bodyPosition = new Vector3(0, 0.06f, -0.07f);
        Func<Vector3, Vector3> bodyShape = u =>
        {
            var p = u * bodyRadii;
            p.X *= 1f - 0.15f * Mathf.Max(0f, u.Z);
            p.Y *= 1f - 0.15f * Mathf.Max(0f, u.Z);
            return p + bodyPosition;
        };
        Dress(Body, "Plumage", c => Ellipsoid(bodyShape, 28, 18, c.Plumage));
        Dress(Body, "Contour", c => Strands(rng, OnShape(rng, bodyShape, 1800, _ => true), new Vector3(0, 0, 1f),
            0.03f, 0.05f, Colors.White, Colors.White, Hair, width: 0.035f, colouring: c.Colouring));

        // Hardly any neck shows: the round head sits right down on the shoulders.
        Neck = Pivot(Body, "Neck", new Vector3(0, 0.06f, -0.22f));
        var headPosition = new Vector3(0, 0.05f, -0.03f);
        Dress(Neck, "Ruff", c => Tube([new Vector3(0, -0.02f, 0.03f), headPosition], [0.1f, 0.08f], 14, c.Plumage, capEnd: false));
        Dress(Neck, "RuffFeathers", c => Strands(rng, OnSegment(rng, 500, new Vector3(0, -0.02f, 0.03f), headPosition, 0.1f, 0.08f),
            new Vector3(0, -0.2f, 1f), 0.03f, 0.05f, Colors.White, Colors.White, Hair, width: 0.03f, colouring: c.Colouring));
        Head = Pivot(Neck, "Head", headPosition);
        BuildHead(Head, rng, talon);

        // Tail: a long, rounded fan, barred across in the hen.
        Tail = Pivot(Body, "Tail", new Vector3(0, 0.05f, 0.1f));
        TailFeathers = new Node3D[11];
        for (int i = 0; i < TailFeathers.Length; i++)
        {
            TailFeathers[i] = Pivot(Tail, "TailFeather", Vector3.Zero);
            var root = new Vector3(0, i * 0.0015f, 0);
            Dress(TailFeathers[i], "Vane", c => Feather(rng, root, Vector3.Back, 0.15f * TailScale, 0.04f, White, feather, c.Flight));
        }

        // Legs: feathered right down to the claws, the thickest-legged of owls, so they look like fluffy white slippers
        // on the snow; only the long, curved black talons show.
        Legs = new Node3D[2];
        Shanks = new Node3D[2];
        Feet = new Node3D[2];
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            Legs[i] = Pivot(Body, "Leg", new Vector3(side * 0.06f, 0f, 0f));
            var thigh = new Vector3(0.05f, 0.07f, 0.055f);
            var thighCentre = new Vector3(0, -0.04f, 0);
            Dress(Legs[i], "Thigh", c => Ellipsoid(thigh, 12, 10, c.Plumage), thighCentre);
            Dress(Legs[i], "ThighFeathers", c => Strands(rng, OnShape(rng, u => u * thigh + thighCentre, 220, _ => true),
                new Vector3(0, -1f, 0), 0.03f, 0.05f, Colors.White, Colors.White, Hair, width: 0.025f, colouring: c.Colouring));

            // The leg below the ankle bends backwards, so the owl can settle down onto its folded legs; the foot hinges
            // again to stay flat on the ground.
            Shanks[i] = Pivot(Legs[i], "Shank", new Vector3(0, -ThighLength, 0));
            var shankEnd = new Vector3(0, -ShankLength, 0);
            Dress(Shanks[i], "Shank", c => Tube([Vector3.Zero, shankEnd], [0.026f, 0.024f], 10, c.Plumage, capEnd: false));
            Dress(Shanks[i], "ShankFeathers", c => Strands(rng, OnSegment(rng, 160, Vector3.Zero, shankEnd, 0.026f, 0.024f),
                new Vector3(0, -1f, 0), 0.02f, 0.035f, Colors.White, Colors.White, Hair, width: 0.02f, colouring: c.Colouring));

            Feet[i] = Pivot(Shanks[i], "Foot", new Vector3(0, -ShankLength, 0));
            var foot = Vector3.Zero;
            Dress(Feet[i], "Ball", c => Ellipsoid(new Vector3(0.026f, 0.018f, 0.026f), 10, 8, c.Plumage), foot);
            // Two toes point forward, one out to the side and one back. The outer toe can swing forward or back, so an
            // owl grips its prey two toes in front and two behind.
            foreach (float angle in new[] { -0.3f, 0.3f, side * 1.4f, Mathf.Pi })
            {
                var dir = new Vector3(Mathf.Sin(angle), 0, -Mathf.Cos(angle));
                var tip = foot + dir * (angle == Mathf.Pi ? 0.035f : 0.05f) + Vector3.Down * 0.008f;
                Dress(Feet[i], "Toe", c => Tube([foot, tip], [0.015f, 0.011f], 8, c.Plumage, capEnd: true));
                Dress(Feet[i], "ToeFeathers", c => Strands(rng, OnSegment(rng, 30, foot, tip, 0.015f, 0.011f),
                    dir, 0.012f, 0.02f, Colors.White, Colors.White, Hair, width: 0.014f, colouring: c.Colouring));
                Attach(Feet[i], "Talon", Tube([tip, tip + dir * 0.016f + Vector3.Up * 0.003f, tip + dir * 0.028f + Vector3.Down * 0.012f],
                    [0.006f, 0.004f, 0.001f], 6, talon, capEnd: true));
            }
        }

        // Wings: an arm and a hand, each a broad covert panel over a row of flight feathers.
        Arms = new Node3D[2];
        Hands = new Node3D[2];
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            var outward = new Vector3(side, 0, 0);
            Arms[i] = Pivot(Body, "Wing", new Vector3(side * 0.095f, 0.09f, -0.13f));

            // Coverts over the arm, and secondaries along its trailing edge, each a little longer towards the body. The
            // coverts are clothed in the same soft, fluffy contour feathers as the body, lying back towards the trailing
            // edge, so the wing looks of a piece with the owl, barred the same way in the hen.
            var armCoverts = new Vector3(ArmLength * 0.6f, 0.024f, 0.11f * WingScale);
            var armCentre = outward * ArmLength * 0.5f + new Vector3(0, 0.008f, 0.045f * WingScale);
            Dress(Arms[i], "Coverts", c => Ellipsoid(armCoverts, 18, 8, c.Plumage), armCentre);
            Dress(Arms[i], "CovertFeathers", c => Strands(rng, OnShape(rng, u => u * armCoverts + armCentre, 600, _ => true),
                new Vector3(0, 0, 1f), 0.025f, 0.045f, Colors.White, Colors.White, Hair, width: 0.03f, colouring: c.Colouring));
            for (int f = 0; f < 10; f++)
            {
                float x = Mathf.Lerp(0.03f, ArmLength, f / 9f);
                var root = outward * x + new Vector3(0, -0.004f * (f % 2), 0.02f);
                float length = Mathf.Lerp(0.24f, 0.21f, f / 9f) * WingScale;
                Dress(Arms[i], "Secondary", c => Feather(rng, root, Vector3.Back, length, 0.032f * WingScale, White, feather, c.Flight));
            }

            // The hand: primaries fan from pointing back at the wrist to pointing out at the tip, broad all the way to a
            // rounded wingtip, not splayed into fingers as an eagle's are, for quiet, slow flight low over the tundra.
            Hands[i] = Pivot(Arms[i], "Hand", outward * ArmLength);
            var handCoverts = new Vector3(HandLength * 0.4f, 0.02f, 0.09f * WingScale);
            var handCentre = outward * HandLength * 0.25f + new Vector3(0, 0.008f, 0.035f * WingScale);
            Dress(Hands[i], "Coverts", c => Ellipsoid(handCoverts, 14, 8, c.Plumage), handCentre);
            Dress(Hands[i], "CovertFeathers", c => Strands(rng, OnShape(rng, u => u * handCoverts + handCentre, 350, _ => true),
                new Vector3(0, 0, 1f), 0.02f, 0.04f, Colors.White, Colors.White, Hair, width: 0.028f, colouring: c.Colouring));
            for (int f = 0; f < 10; f++)
            {
                float t = f / 9f;
                float angle = Mathf.Lerp(0.15f, 1.25f, t);
                var dir = new Vector3(side * Mathf.Sin(angle), 0, Mathf.Cos(angle));
                var root = outward * Mathf.Lerp(0.02f, HandLength * 0.42f, t) + new Vector3(0, -0.004f * (f % 2), 0.02f);
                float length = Mathf.Lerp(0.24f, 0.32f, Mathf.Sin(t * 2.4f)) * WingScale;
                // Even a cock's outer primaries carry a few dark bars near their tips.
                Dress(Hands[i], "Primary", c => Feather(rng, root, dir, length, 0.034f * WingScale, White, feather,
                    t > 0.6f ? _coats[(int)Sex.Female].Flight : c.Flight));
            }
        }
    }

    /// <summary>
    /// The round head: a broad skull with a flat facial disc of stiff feathers that funnels sound to the ears hidden
    /// at its edges, two huge golden-yellow eyes looking straight ahead under a white brow, and a small black beak
    /// hooking down between them, nearly hidden in bristly feathers.
    /// </summary>
    private void BuildHead(Node3D head, RandomNumberGenerator rng, StandardMaterial3D black)
    {
        var skull = new Vector3(0.085f, 0.075f, 0.075f);
        // The face is flattened in front into the facial disc.
        Func<Vector3, Vector3> skullShape = u => (u * skull) with { Z = u.Z * skull.Z * (u.Z < 0 ? 0.7f : 1f) };
        Dress(head, "Skull", c => Ellipsoid(skullShape, 24, 18, c.Plumage));
        Dress(head, "Feathers", c => Strands(rng, OnShape(rng, skullShape, 700, u => u.Z > -0.5f), new Vector3(0, -0.1f, 1f),
            0.015f, 0.03f, Colors.White, Colors.White, Hair, width: 0.018f, colouring: c.Colouring));

        // The facial disc's white feathers radiate out from round the eyes and beak, unbarred in either sex.
        var face = new StandardMaterial3D { AlbedoColor = White, Roughness = 1f };
        Attach(head, "Disc", Ellipsoid(new Vector3(0.075f, 0.062f, 0.012f), 20, 10, face), new Vector3(0, -0.003f, -0.048f));
        var discRoots = OnShape(rng, u => u * new Vector3(0.072f, 0.06f, 0.01f) + new Vector3(0, -0.003f, -0.05f), 260, u => u.Z < 0f);
        var discColouring = (Func<Vector3, Vector3, Color>)((_, _) => White);
        Attach(head, "DiscFeathers", Strands(rng, discRoots, new Vector3(0, 0, 0.3f), 0.01f, 0.018f, Colors.White, Colors.White, Hair,
            width: 0.012f, colouring: discColouring));

        // Beak: short, deep and hooked, set low between the eyes.
        Vector3 p0 = new(0, 0.002f, -0.058f), p1 = new(0, 0.004f, -0.075f), p2 = new(0, -0.006f, -0.086f), p3 = new(0, -0.024f, -0.08f);
        const int steps = 10;
        var path = new Vector3[steps + 1];
        var radii = new float[steps + 1];
        for (int s = 0; s <= steps; s++)
        {
            float t = s / (float)steps;
            path[s] = p0.BezierInterpolate(p1, p2, p3, t);
            radii[s] = Mathf.Lerp(0.011f, 0.002f, Mathf.Pow(t, 1.3f));
        }
        Attach(head, "Beak", Tube(path, radii, 10, black, capEnd: true));
        // The lower mandible hinges at the corner of the mouth, so the beak gapes to swallow food or drink.
        var jaw = Jaw(head, new Vector3(0, -0.008f, -0.056f));
        Attach(jaw, "LowerBeak", Tube([new Vector3(0, -0.002f, -0.002f), new Vector3(0, -0.008f, -0.022f)], [0.008f, 0.003f], 8, black, capEnd: true));
        MouthInside(jaw, new Vector3(0.004f, 0.002f, 0.01f), new Vector3(0, 0.003f, -0.01f));
        // Bristly feathers sweep forward over the base of the beak.
        Attach(head, "Bristles", Strands(rng, OnShape(rng, u => u * new Vector3(0.016f, 0.012f, 0.006f) + new Vector3(0, 0.006f, -0.058f), 80,
                _ => true), new Vector3(0, -0.4f, -1f), 0.012f, 0.02f, Colors.White, Colors.White, Hair, width: 0.01f,
            colouring: discColouring));

        foreach (float side in new[] { -1f, 1f })
        {
            // The eyes are so big they are tubes rather than balls, fixed in the skull and looking straight ahead,
            // under a ledge of white brow that makes the owl look stern.
            Eye(head, new Vector3(side * 0.031f, 0.012f, -0.058f), new Vector3(0, -side * 0.12f, 0), 0.0155f, Yellow, 0.55f);
            Attach(head, "Brow", Ellipsoid(new Vector3(0.022f, 0.007f, 0.014f), 10, 6, face), new Vector3(side * 0.028f, 0.03f, -0.058f));
        }
    }

    /// <summary>A coat from its plumage: <paramref name="barring"/> is how heavily its flight feathers are barred, 0 to 1.</summary>
    private static Coat MakeCoat(StandardMaterial3D plumage, float barring) =>
        new(plumage, ProceduralTextures.Colouring(plumage), t =>
        {
            // Bars run across each flight feather at regular steps along it, darker towards the tip.
            float bar = Mathf.SmoothStep(0.55f, 0.85f, Mathf.Sin(t * 22f)) * Mathf.Lerp(0.5f, 1f, t) * barring;
            return White.Lerp(Bar, bar * 0.85f);
        });

    /// <summary>Attaches a part once in each coat, the cock's and the hen's, so only the one matching the owl's sex shows.</summary>
    private void Dress(Node3D parent, string name, Func<Coat, Mesh> mesh, Vector3 position = default)
    {
        for (int c = 0; c < _coats.Length; c++)
            _dressed[c].Add(Attach(parent, name, mesh(_coats[c]), position));
    }
}
