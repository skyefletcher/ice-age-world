using System;
using Godot;

namespace IceAgeWorld;

/// <summary>
/// A moose (elk in Europe), the biggest deer there is, built entirely in code to a real bull's proportions: nearly two
/// metres at the shoulder, with a short, deep body under a high, humped back, very long pale legs for wading deep snow
/// and lakes, a short, thick neck under a stiff dark mane, and a long, heavy head with a great overhanging, drooping
/// nose and a flap of hair-covered skin, the "bell", hanging from the throat. Its antlers are broad, flat palms with
/// a fringe of points round the edge, held out sideways, a good metre and a half across. It walks with a long, slow
/// stride, trots and gallops, grazes, beds down with its legs folded under it, and swims well.
/// The model faces -Z.
/// </summary>
public partial class Moose : Quadruped
{
    private const int Seed = 67;

    private const float BodyCentre = 1.62f;

    private static readonly Color Coat = new(0.24f, 0.16f, 0.1f);
    private static readonly Color Saddle = new(0.4f, 0.3f, 0.2f);
    private static readonly Color Legs = new(0.62f, 0.55f, 0.46f);
    private static readonly Color Muzzle = new(0.16f, 0.11f, 0.08f);
    private static readonly Color FurRoot = new(0.8f, 0.8f, 0.8f);
    private static readonly Color FurTip = new(1.1f, 1.1f, 1.1f);
    private static readonly Vector3 CoatDrift = new(0, -0.25f, 0.8f);

    private Node3D[] _ears = [];
    private Node3D _bell = null!;

    public override string DisplayName => "Moose";
    public override string YoungName => "Moose calf";

    public override AnimalStats Stats { get; } = new()
    {
        // Moose trot tirelessly and can gallop at about 55 km/h, swim for kilometres across lakes, and dive to feed on
        // water plants, but they are big and not nimble.
        Scores = new() { JumpHeight = 5, JumpLength = 6, LandSpeed = 6, WaterSpeed = 7, Agility = 4, Stamina = 8 },
        FloatDepth = 1.35f,
        WadeDepth = 0.9f,
        MouthDistance = 1.55f,
        EatReach = 0.8f,
        CanGraze = true,
        BodyRadius = 0.6f,
        BodyHeight = 2.3f,
        CameraHeight = 2.1f,
        CameraDistance = 8f,
    };

    protected override float FrontUpperLength => 0.7f;
    protected override float FrontLowerLength => 0.66f;
    protected override float BackUpperLength => 0.72f;
    protected override float BackLowerLength => 0.62f;
    protected override float FootHeight => 0.13f;
    protected override float ShoulderOffset => 0.6f;
    protected override float HipOffset => 0.66f;
    protected override float LegSpread => 0.2f;
    protected override float LieDrop => 1.08f;
    protected override float DeadFlank => 0.45f;
    protected override float DeadRoll => BodyCentre;
    protected override float Cadence => 2.4f;

    // A moose's neck is too short to reach the ground on straight legs, so it splays and bends its forelegs to graze.
    protected override float CrouchDrop => 0.4f;
    protected override float CrouchPitch => 0.35f;
    protected override float HeadDown => -1.5f;
    protected override float LieNeck => -0.5f;
    protected override float LieHead => 0.3f;

    protected override void AnimateExtras(float stride, float run, float dt)
    {
        for (int i = 0; i < 2; i++)
        {
            float twitch = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Clock * 0.6f + i * 2.7f)), 14f) * 0.5f * Alive;
            _ears[i].Rotation = new Vector3(run * 0.6f + twitch, 0, 0);
        }

        // The bell swings with every stride.
        _bell.Rotation = new Vector3(Mathf.Sin(WalkCycle * 2f) * 0.25f * stride + Mathf.Sin(Clock * 1.1f) * 0.05f * Alive, 0, 0);
    }

    protected override void Build()
    {
        var rng = new RandomNumberGenerator { Seed = Seed };
        var coat = ProceduralTextures.Fur(Seed, new Color(0.7f, 0.7f, 0.7f), new Color(1f, 1f, 1f), 3f);
        coat.VertexColorUseAsAlbedo = true;
        var grain = ProceduralTextures.Colouring(coat);
        var horn = new StandardMaterial3D { AlbedoColor = new Color(0.12f, 0.1f, 0.08f), Roughness = 0.5f };
        var antler = new StandardMaterial3D { AlbedoColor = new Color(0.66f, 0.56f, 0.42f), Roughness = 0.8f };

        // Dark brown all over, greyer along the saddle of the back, almost black beneath.
        Func<Vector3, Vector3, Color> bodyColour = (p, n) =>
            Coat.Lerp(Saddle, Mathf.SmoothStep(0.4f, 0.9f, n.Y) * Mathf.SmoothStep(-0.2f, 0.4f, p.Z) * 0.8f)
                .Lerp(Coat * 0.6f, Mathf.SmoothStep(-0.3f, -0.8f, n.Y));
        Func<Vector3, Vector3, Color> furColour = (p, n) => bodyColour(p, n) * grain(p, n);

        var body = Pivot(Frame, "Body", new Vector3(0, BodyCentre, 0));
        Attach(body, "Hide", Ellipsoid(BodyShape, 36, 22, coat, bodyColour));
        Attach(body, "Coat", Strands(rng, OnShape(rng, BodyShape, 6000, _ => true), CoatDrift,
            0.035f, 0.06f, FurRoot, FurTip, Hair, width: 0.03f, colouring: furColour));

        // A short, thick neck from the front of the hump, with a stiff dark mane along its top.
        Neck = Pivot(Frame, "Neck", new Vector3(0, BodyCentre + 0.2f, -0.82f));
        var headAt = new Vector3(0, 0.12f, -0.42f);
        Attach(Neck, "Joint", Ellipsoid(new Vector3(0.24f, 0.3f, 0.24f), 14, 10, coat, bodyColour));
        // Tubes carry no patches of colour, so the neck wears a plain coat of its own.
        var neckCoat = ProceduralTextures.Fur(Seed + 2, Coat * 0.75f, Coat * 1.2f, 3f);
        Attach(Neck, "Throat", Tube([Vector3.Zero, headAt], [0.26f, 0.17f], 12, neckCoat, capEnd: false));
        Attach(Neck, "NeckFur", Strands(rng, OnSegment(rng, 1100, Vector3.Zero, headAt, 0.26f, 0.17f), CoatDrift,
            0.04f, 0.07f, FurRoot, FurTip, Hair, width: 0.03f, colouring: furColour));
        Attach(Neck, "Mane", Strands(rng, OnSegment(rng, 500, new Vector3(0, 0.22f, 0.25f), headAt + new Vector3(0, 0.14f, 0.05f), 0.04f, 0.03f),
            new Vector3(0, 1f, 0.3f), 0.07f, 0.12f, FurRoot, FurTip, Hair, width: 0.03f, colouring: (p, n) => Coat * 0.7f));
        Head = Pivot(Neck, "Head", headAt);
        BuildHead(rng, coat, grain, horn, antler);

        // Long, pale, grey-brown legs below dark shoulders and haunches.
        var legCoat = ProceduralTextures.Fur(Seed + 1, Legs * 0.75f, Legs * 1.2f, 3f);
        BuildLegs(rng, legCoat, ProceduralTextures.Colouring(legCoat), coat, furColour, (0.11f, 0.075f, 0.05f), (0.13f, 0.08f, 0.05f),
            new Vector3(0.14f, 0.28f, 0.2f), 320, (0.02f, 0.035f), FurRoot, FurTip,
            (foot, _) => Hooves(foot, 0.1f, legCoat, horn));

        // A stubby tail, barely showing.
        var tail = Pivot(Frame, "Tail", new Vector3(0, BodyCentre + 0.12f, 0.95f));
        Attach(tail, "Tail", Ellipsoid(new Vector3(0.06f, 0.05f, 0.08f), 10, 8, coat, bodyColour), new Vector3(0, -0.04f, 0.03f));
    }

    /// <summary>
    /// A long, heavy head: a narrow brow, a big, swollen muzzle with a drooping, overhanging upper lip and wide
    /// nostrils, small eyes set high, long ears held out to the sides, the bell hanging from the throat, and the
    /// broad palmate antlers.
    /// </summary>
    private void BuildHead(RandomNumberGenerator rng, StandardMaterial3D coat, Func<Vector3, Vector3, Color> grain,
        Material horn, Material antler)
    {
        Func<Vector3, Vector3, Color> faceColour = (p, n) => Coat.Lerp(Muzzle, Mathf.SmoothStep(-0.2f, -0.4f, p.Z));
        var skull = new Vector3(0.13f, 0.14f, 0.17f);
        Attach(Head, "Skull", Ellipsoid(skull, 20, 14, coat, faceColour));

        // The long, Roman-nosed face sloping down to the great bulbous muzzle.
        Func<Vector3, Vector3> face = u =>
        {
            float t = Mathf.Clamp((-u.Z + 1f) * 0.5f, 0f, 1f);
            return new Vector3(u.X * Mathf.Lerp(0.1f, 0.12f, t * t), u.Y * Mathf.Lerp(0.1f, 0.13f, t), u.Z * 0.26f);
        };
        var faceAt = new Vector3(0, -0.06f, -0.27f);
        Attach(Head, "Face", Ellipsoid(face, 20, 14, coat, faceColour), faceAt);
        Attach(Head, "HeadFur", Strands(rng, OnShape(rng, u => face(u) + faceAt, 600, u => u.Y > -0.3f), new Vector3(0, -0.2f, 0.7f),
            0.012f, 0.022f, FurRoot, FurTip, Hair, width: 0.014f, colouring: (p, n) => faceColour(p, n) * grain(p, n)));

        // The overhanging upper lip droops over the mouth; wide, dark nostrils sit on its front.
        var lipAt = faceAt + new Vector3(0, -0.04f, -0.24f);
        Attach(Head, "Lip", Ellipsoid(new Vector3(0.12f, 0.085f, 0.08f), 16, 10, coat, (p, n) => Muzzle), lipAt);
        foreach (float side in new[] { -1f, 1f })
            Attach(Head, "Nostril", Ellipsoid(new Vector3(0.02f, 0.035f, 0.012f), 8, 6, horn), lipAt + new Vector3(side * 0.05f, 0.01f, -0.072f));
        Attach(Head, "Jaw", Ellipsoid(new Vector3(0.08f, 0.04f, 0.14f), 12, 8, coat, (p, n) => Muzzle), faceAt + new Vector3(0, -0.1f, -0.06f));

        // The bell: a flap of skin and long hair hanging from the throat.
        _bell = Pivot(Head, "Bell", new Vector3(0, -0.12f, 0.08f));
        var flap = new Vector3(0.025f, 0.14f, 0.06f);
        Attach(_bell, "Flap", Ellipsoid(flap, 10, 8, coat, (p, n) => Coat * 0.7f), new Vector3(0, -0.12f, 0));
        Attach(_bell, "BellHair", Strands(rng, OnShape(rng, u => u * flap + new Vector3(0, -0.12f, 0), 160, _ => true), new Vector3(0, -1f, 0),
            0.05f, 0.09f, FurRoot, FurTip, Hair, width: 0.02f, colouring: (p, n) => Coat * 0.7f));

        _ears = new Node3D[2];
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            Eye(Head, new Vector3(side * 0.11f, 0.03f, -0.07f), new Vector3(0, -side * 1.1f, 0), 0.016f, new Color(0.1f, 0.06f, 0.04f), 0.75f);

            // Long ears held out to the sides, like a donkey's.
            _ears[i] = Pivot(Head, "Ear", new Vector3(side * 0.12f, 0.1f, 0.08f));
            var tilt = Pivot(_ears[i], "Tilt", Vector3.Zero);
            tilt.Rotation = new Vector3(0.2f, 0, -side * 0.9f);
            Attach(tilt, "Ear", Ellipsoid(new Vector3(0.045f, 0.12f, 0.018f), 12, 8, coat, (p, n) => Coat), new Vector3(0, 0.1f, 0));

            BuildAntler(side, antler, rng);
        }
    }

    /// <summary>
    /// One palmate antler: a short, thick beam out from the brow to a broad, flat palm tipped up and out, with a
    /// fringe of points round its outer edge and a smaller brow palm reaching forward.
    /// </summary>
    private void BuildAntler(float side, Material antler, RandomNumberGenerator rng)
    {
        var root = Pivot(Head, "Antler", new Vector3(side * 0.09f, 0.11f, 0.06f));
        var palmAt = new Vector3(side * 0.42f, 0.2f, 0.02f);
        Attach(root, "Beam", Tube([Vector3.Zero, new Vector3(side * 0.16f, 0.06f, 0.02f), palmAt * 0.7f], [0.05f, 0.045f, 0.04f], 8, antler, capEnd: false));

        // The palm: a broad, flat plate, its face turned up and forward, tilted up at the outer edge.
        var palm = Pivot(root, "Palm", palmAt);
        palm.Rotation = new Vector3(0.35f, side * 0.2f, side * 0.45f);
        var plate = new Vector3(0.36f, 0.035f, 0.26f);
        Attach(palm, "Plate", Ellipsoid(u => new Vector3(u.X * plate.X, u.Y * plate.Y, u.Z * plate.Z * (u.X * side > 0 ? 1.1f : 0.75f)), 16, 8, antler));

        // Points round the outer and back edge of the palm, longest at the outer tip.
        int points = 7;
        for (int t = 0; t < points; t++)
        {
            float a = Mathf.Lerp(-1.1f, 1.4f, t / (float)(points - 1));
            var edge = new Vector3(side * Mathf.Cos(a) * plate.X * 0.9f, 0f, -Mathf.Sin(a) * plate.Z);
            var tip = edge + new Vector3(side * Mathf.Cos(a), 0.15f, -Mathf.Sin(a)) * rng.RandfRange(0.09f, 0.15f);
            Attach(palm, "Point", Tube([edge, tip], [0.03f, 0.008f], 6, antler, capEnd: true));
        }

        // A smaller brow palm reaching forward from the beam, with a few short points.
        var brow = Pivot(root, "BrowPalm", new Vector3(side * 0.2f, 0.08f, -0.05f));
        brow.Rotation = new Vector3(0.3f, -side * 0.6f, side * 0.2f);
        Attach(brow, "Plate", Ellipsoid(new Vector3(0.1f, 0.025f, 0.14f), 12, 6, antler), new Vector3(0, 0, -0.1f));
        for (int t = 0; t < 3; t++)
        {
            var edge = new Vector3((t - 1) * 0.06f, 0, -0.22f);
            Attach(brow, "Point", Tube([edge, edge + new Vector3((t - 1) * 0.02f, 0.06f, -0.08f)], [0.022f, 0.006f], 6, antler, capEnd: true));
        }
    }

    /// <summary>Short and deep, with a high hump over the shoulders where the neck muscles anchor, falling to a lower rump.</summary>
    private static Vector3 BodyShape(Vector3 u)
    {
        var p = new Vector3(u.X * 0.38f, u.Y * 0.42f, u.Z * 0.95f);
        float front = Mathf.SmoothStep(0.5f, -0.6f, p.Z);
        p.X *= 0.9f + front * 0.12f;
        if (u.Y > 0)
            p.Y *= 0.85f + front * 0.45f;
        else
            p.Y *= 0.9f + front * 0.15f;
        return p;
    }
}
