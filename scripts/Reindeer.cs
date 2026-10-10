using System;
using Godot;

namespace IceAgeWorld;

/// <summary>
/// A reindeer (caribou), built entirely in code to a real one's proportions: about 1.1 m at the shoulder, with a thick
/// grey-brown winter coat, a long cream mane hanging from the throat, a pale belly and a white rump patch, dark legs
/// ending in broad cloven hooves that spread like snowshoes, a short white-edged tail, and a long, furry muzzle. Both
/// sexes grow antlers, the only deer that do: tall, sweeping, many-pointed beams, with a flattened "shovel" brow tine
/// reaching down over the face. It walks and gallops, grazes, beds down with its legs folded under it, and swims well.
/// The model faces -Z.
/// </summary>
public partial class Reindeer : Quadruped
{
    private const int Seed = 61;

    private const float BodyCentre = 0.96f;

    /// <summary>
    /// How much bigger the head is drawn than it is modelled: a reindeer's head is long and heavy for its body. The
    /// antlers are sized on their own, as a big bull's, over a metre from burr to tip.
    /// </summary>
    private const float HeadScale = 1.2f;
    private const float AntlerScale = 1.05f;

    private static readonly Color Back = new(0.42f, 0.35f, 0.28f);
    private static readonly Color Mane = new(0.9f, 0.87f, 0.8f);
    private static readonly Color Belly = new(0.8f, 0.76f, 0.7f);
    private static readonly Color Legs = new(0.25f, 0.2f, 0.16f);
    private static readonly Color Rump = new(0.95f, 0.94f, 0.9f);
    private static readonly Color Face = new(0.28f, 0.22f, 0.17f);
    private static readonly Color Muzzle = new(0.72f, 0.68f, 0.62f);
    private static readonly Color FurRoot = new(0.8f, 0.8f, 0.8f);
    private static readonly Color FurTip = new(1.08f, 1.08f, 1.08f);
    private static readonly Vector3 CoatDrift = new(0, -0.2f, 0.8f);

    private Node3D[] _ears = [];
    private Node3D _tail = null!;

    public override string DisplayName => "Reindeer";
    public override string YoungName => "Reindeer calf";

    public override AnimalStats Stats { get; } = new()
    {
        // Reindeer travel farther than any other land animal on their migrations, swim rivers and lakes with ease (their
        // hollow hairs keep them afloat), and can sprint at up to 80 km/h.
        Scores = new() { JumpHeight = 6, JumpLength = 7, LandSpeed = 8, WaterSpeed = 7, Agility = 6, Stamina = 10 },
        Abilities = Ability.Herd,
        Companions = 4,
        FloatDepth = 0.75f,
        WadeDepth = 0.5f,
        MouthDistance = 1.05f,
        EatReach = 0.6f,
        CanGraze = true,
        BodyRadius = 0.4f,
        BodyHeight = 1.5f,
        CameraHeight = 1.4f,
        CameraDistance = 5.5f,
    };

    protected override float FrontUpperLength => 0.42f;
    protected override float FrontLowerLength => 0.4f;
    protected override float BackUpperLength => 0.44f;
    protected override float BackLowerLength => 0.38f;
    protected override float FootHeight => 0.09f;
    protected override float ShoulderOffset => 0.42f;
    protected override float HipOffset => 0.44f;
    protected override float LegSpread => 0.13f;
    protected override float LieDrop => 0.58f;
    protected override float DeadFlank => 0.3f;
    protected override float DeadRoll => BodyCentre;
    protected override float CrouchDrop => 0.2f;
    protected override float HeadDown => -1.75f;
    protected override float LieNeck => -0.9f;
    protected override float LieHead => 0.5f;

    protected override void AnimateExtras(float stride, float run, float dt)
    {
        // Ears flick now and then, and lie back at a gallop; the short tail flicks up when running.
        for (int i = 0; i < 2; i++)
        {
            float twitch = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Clock * 0.8f + i * 2.3f)), 14f) * 0.4f * Alive;
            _ears[i].Rotation = new Vector3(run * 0.5f + twitch, 0, 0);
        }
        _tail.Rotation = new Vector3(-0.3f - run * 0.6f + Mathf.Sin(Clock * 3f) * 0.08f * Alive, 0, 0);
    }

    protected override void Build()
    {
        var rng = new RandomNumberGenerator { Seed = Seed };
        var coat = ProceduralTextures.Fur(Seed, new Color(0.72f, 0.72f, 0.72f), new Color(1f, 1f, 1f), 3f);
        coat.VertexColorUseAsAlbedo = true;
        var grain = ProceduralTextures.Colouring(coat);
        var horn = new StandardMaterial3D { AlbedoColor = new Color(0.1f, 0.09f, 0.08f), Roughness = 0.5f };
        var antler = new StandardMaterial3D { AlbedoColor = new Color(0.6f, 0.5f, 0.38f), Roughness = 0.75f };

        // The body's colour by where on it a point is: a cream throat and chest blending into the grey-brown back, a
        // pale belly, and a white patch round the tail.
        Func<Vector3, Vector3, Color> bodyColour = (p, n) =>
        {
            var c = Back.Lerp(Belly, Mathf.SmoothStep(-0.2f, -0.7f, n.Y));
            c = c.Lerp(Mane, Mathf.SmoothStep(-0.3f, -0.55f, p.Z) * 0.9f);
            return c.Lerp(Rump, Mathf.SmoothStep(0.45f, 0.62f, p.Z) * Mathf.SmoothStep(0.3f, -0.1f, p.Y));
        };
        Func<Vector3, Vector3, Color> furColour = (p, n) => bodyColour(p, n) * grain(p, n);

        var body = Pivot(Frame, "Body", new Vector3(0, BodyCentre, 0));
        Attach(body, "Hide", Ellipsoid(BodyShape, 36, 20, coat, bodyColour));
        Attach(body, "Coat", Strands(rng, OnShape(rng, BodyShape, 5500, _ => true), CoatDrift,
            0.03f, 0.05f, FurRoot, FurTip, Hair, width: 0.026f, colouring: furColour));

        // A thick neck carried forward and only a little up, the head held level in front, with a shaggy cream mane
        // hanging from the throat.
        Neck = Pivot(Frame, "Neck", new Vector3(0, BodyCentre + 0.1f, -0.52f));
        var headAt = new Vector3(0, 0.22f, -0.34f);
        Func<Vector3, Vector3, Color> neckColour = (p, n) => Back.Lerp(Mane, Mathf.SmoothStep(0.2f, -0.4f, n.Y) * 0.9f + 0.1f) * grain(p, n);
        Attach(Neck, "Joint", Ellipsoid(new Vector3(0.15f, 0.17f, 0.15f), 14, 10, coat, (p, n) => Mane));
        // Tubes carry no patches of colour, so the neck wears a plain coat of its own, between the back and the mane.
        var neckCoat = ProceduralTextures.Fur(Seed + 2, Back.Lerp(Mane, 0.5f) * 0.8f, Back.Lerp(Mane, 0.5f) * 1.15f, 3f);
        Attach(Neck, "Throat", Tube([Vector3.Zero, headAt], [0.17f, 0.1f], 12, neckCoat, capEnd: false));
        Attach(Neck, "NeckFur", Strands(rng, OnSegment(rng, 1000, Vector3.Zero, headAt, 0.17f, 0.1f), CoatDrift,
            0.035f, 0.06f, FurRoot, FurTip, Hair, width: 0.026f, colouring: neckColour));
        Attach(Neck, "Mane", Strands(rng, OnSegment(rng, 900, new Vector3(0, -0.08f, 0.04f), headAt + new Vector3(0, -0.09f, 0.06f), 0.14f, 0.08f),
            new Vector3(0, -1f, 0.15f), 0.1f, 0.17f, FurRoot, FurTip, Hair, width: 0.03f, colouring: (p, n) => Mane));
        Head = Pivot(Neck, "Head", headAt);
        Head.Scale = Vector3.One * HeadScale;
        BuildHead(rng, coat, grain, horn, antler);

        // The legs are dark brown, below shoulders and haunches the colour of the body, with pale socks just above the
        // broad hooves.
        var socks = ProceduralTextures.Fur(Seed + 3, Mane * 0.8f, Mane * 1.05f, 3f);
        var legCoat = ProceduralTextures.Fur(Seed + 1, Legs * 0.75f, Legs * 1.3f, 3f);
        BuildLegs(rng, legCoat, ProceduralTextures.Colouring(legCoat), coat, furColour, (0.085f, 0.058f, 0.04f), (0.1f, 0.062f, 0.04f),
            new Vector3(0.09f, 0.17f, 0.13f), 260, (0.02f, 0.035f), FurRoot, FurTip,
            (foot, _) => Hooves(foot, 0.065f, socks, horn));

        // A short tail, dark above and white beneath, set in the white rump patch.
        _tail = Pivot(Frame, "Tail", new Vector3(0, BodyCentre + 0.1f, 0.64f));
        var tail = new Vector3(0.05f, 0.025f, 0.08f);
        Attach(_tail, "Tail", Ellipsoid(tail, 10, 8, coat, (p, n) => n.Y > 0 ? Back : Rump), new Vector3(0, 0, 0.06f));
        Attach(_tail, "TailFur", Strands(rng, OnShape(rng, u => u * tail + new Vector3(0, 0, 0.06f), 120, _ => true),
            new Vector3(0, -0.5f, 0.6f), 0.03f, 0.05f, FurRoot, FurTip, Hair, width: 0.02f, colouring: (p, n) => n.Y > 0 ? Back : Rump));
    }

    /// <summary>
    /// A long head: a dark brown face running down to a broad, pale, furry muzzle (reindeer noses are furred all over
    /// against the cold, with no bare black pad), slit nostrils, big dark eyes set on the sides, short oval ears held up
    /// and out, and the antlers.
    /// </summary>
    private void BuildHead(RandomNumberGenerator rng, StandardMaterial3D coat, Func<Vector3, Vector3, Color> grain,
        Material horn, Material antler)
    {
        // Darkest down the face, paling to grey-cream at the muzzle and on the chin.
        Func<Vector3, Vector3, Color> faceColour = (p, n) =>
            Face.Lerp(Back, Mathf.SmoothStep(0.05f, -0.05f, p.Z) * 0.6f).Lerp(Muzzle, Mathf.SmoothStep(-0.22f, -0.3f, p.Z));
        Func<Vector3, Vector3, Color> faceFur = (p, n) => faceColour(p, n) * grain(p, n);

        var skull = new Vector3(0.085f, 0.09f, 0.11f);
        Attach(Head, "Skull", Ellipsoid(skull, 20, 14, coat, faceColour));

        // The long face tapers a little to the muzzle, which swells again at the end, broad and soft.
        Func<Vector3, Vector3> face = u =>
        {
            float t = Mathf.Clamp((1f - u.Z) * 0.5f, 0f, 1f);
            return new Vector3(u.X * Mathf.Lerp(0.072f, 0.056f, t), u.Y * Mathf.Lerp(0.078f, 0.058f, t), u.Z * 0.13f);
        };
        var faceAt = new Vector3(0, -0.03f, -0.16f);
        Attach(Head, "Face", Ellipsoid(u => face(u) + faceAt, 20, 12, coat, faceColour));
        var muzzleAt = new Vector3(0, -0.045f, -0.3f);
        var muzzle = new Vector3(0.06f, 0.058f, 0.065f);
        Attach(Head, "Muzzle", Ellipsoid(muzzle, 16, 12, coat, (p, n) => Muzzle), muzzleAt);
        Attach(Head, "Chin", Ellipsoid(new Vector3(0.04f, 0.022f, 0.05f), 12, 8, coat, (p, n) => Muzzle), new Vector3(0, -0.09f, -0.27f));
        Attach(Head, "HeadFur", Strands(rng, OnShape(rng, u => face(u) + faceAt, 500, u => u.Y > -0.4f), new Vector3(0, -0.1f, 0.8f),
            0.012f, 0.022f, FurRoot, FurTip, Hair, width: 0.013f, colouring: faceFur));
        Attach(Head, "SkullFur", Strands(rng, OnShape(rng, u => u * skull, 400, u => u.Z > -0.6f), CoatDrift,
            0.015f, 0.025f, FurRoot, FurTip, Hair, width: 0.014f, colouring: faceFur));
        Attach(Head, "MuzzleFur", Strands(rng, OnShape(rng, u => u * muzzle + muzzleAt, 350, u => u.Z < 0.4f), new Vector3(0, -0.3f, -0.4f),
            0.01f, 0.018f, FurRoot, FurTip, Hair, width: 0.011f, colouring: (p, n) => Muzzle * grain(p, n)));

        _ears = new Node3D[2];
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;

            // Comma-shaped nostrils on the front of the muzzle, angled in towards each other.
            var nostril = Pivot(Head, "Nostril", muzzleAt + new Vector3(side * 0.03f, 0.015f, -0.058f));
            nostril.Rotation = new Vector3(0, 0, side * 0.5f);
            Attach(nostril, "Slit", Ellipsoid(new Vector3(0.008f, 0.017f, 0.006f), 8, 6, horn));

            Eye(Head, new Vector3(side * 0.07f, 0.025f, -0.06f), new Vector3(0, -side * 1.0f, 0), 0.016f, new Color(0.15f, 0.09f, 0.05f), 0.75f);

            // Short, oval ears, furred inside and out, held up and out to the sides.
            _ears[i] = Pivot(Head, "Ear", new Vector3(side * 0.075f, 0.07f, 0.05f));
            var tilt = Pivot(_ears[i], "Tilt", Vector3.Zero);
            tilt.Rotation = new Vector3(0.25f, 0, -side * 0.6f);
            var ear = new Vector3(0.028f, 0.065f, 0.012f);
            Attach(tilt, "Ear", Ellipsoid(ear, 12, 8, coat, (p, n) => Back), new Vector3(0, 0.055f, 0));
            Attach(tilt, "EarFur", Strands(rng, OnShape(rng, u => u * ear + new Vector3(0, 0.055f, 0), 90, _ => true), new Vector3(0, 0.6f, 0.3f),
                0.01f, 0.018f, FurRoot, FurTip, Hair, width: 0.01f, colouring: (p, n) => Back * grain(p, n)));

            BuildAntler(side, antler, rng, shovel: i == 0);
        }
    }

    /// <summary>
    /// One antler, as a caribou's grow: a long, slender beam that leaves the skull going back and out, sweeps up in a
    /// wide C and curves forward again at the top, where it flattens into a palm fringed with points. Low down a bez
    /// tine reaches forward, itself flattened and pointed at the tip; halfway up a short back tine points behind. On one
    /// side only, the brow tine grows into the "shovel": a flat, upright, toothed plate reaching forward over the face.
    /// The other side's brow tine is a plain spike. The antler is darker at the base and pales towards the tips.
    /// </summary>
    private void BuildAntler(float side, Material antler, RandomNumberGenerator rng, bool shovel)
    {
        var root = Pivot(Head, "Antler", new Vector3(side * 0.045f, 0.085f, 0.02f));
        root.Scale = Vector3.One * (AntlerScale / HeadScale);
        var tips = new StandardMaterial3D { AlbedoColor = new Color(0.82f, 0.76f, 0.64f), Roughness = 0.7f };

        // The main beam: a cubic curve back, out and up, and forward at the top.
        var p0 = Vector3.Zero;
        var p1 = new Vector3(side * 0.12f, 0.26f, 0.34f);
        var p2 = new Vector3(side * 0.38f, 0.8f, 0.34f);
        var p3 = new Vector3(side * 0.32f, 1.04f, -0.1f);
        Vector3 Beam(float t)
        {
            float s = 1f - t;
            return p0 * s * s * s + p1 * 3f * s * s * t + p2 * 3f * s * t * t + p3 * t * t * t;
        }
        const int samples = 12;
        var path = new Vector3[samples];
        var radii = new float[samples];
        for (int k = 0; k < samples; k++)
        {
            float t = k / (float)(samples - 1) * 0.92f;
            path[k] = Beam(t);
            radii[k] = Mathf.Lerp(0.028f, 0.014f, t);
        }
        Attach(root, "Beam", Tube(path, radii, 8, antler, capEnd: false));

        // Brow tine: forward over the face, from just above the burr.
        var brow = Beam(0.04f);
        if (shovel)
        {
            // The shovel: the brow tine runs forward, then turns down in front of the forehead and broadens into an
            // upright blade, thin from side to side, its lower edge toothed with short points.
            var end = brow + new Vector3(-side * 0.015f, -0.01f, -0.2f);
            Tine(root, brow, brow + new Vector3(0, 0.05f, -0.12f), end, 0.018f, antler);
            var blade = Pivot(root, "Shovel", end);
            blade.Basis = Basis.LookingAt(new Vector3(0, -1f, -0.35f).Normalized(), Vector3.Forward);
            Palm(blade, 0.13f, 0.06f, 0.01f, 5, 0.02f, 0.045f, antler, rng);
        }
        else
        {
            Tine(root, brow, brow + new Vector3(side * 0.01f, 0.06f, -0.07f), brow + new Vector3(side * 0.02f, 0.14f, -0.14f), 0.014f, antler);
        }

        // Bez tine: forward and up from low on the beam, flattening into a small palm with a few points.
        var bez = Beam(0.24f);
        var bezEnd = bez + new Vector3(side * 0.06f, 0.12f, -0.2f);
        Tine(root, bez, bez + new Vector3(side * 0.04f, 0.03f, -0.1f), bezEnd, 0.016f, antler);
        var bezPalm = Pivot(root, "BezPalm", bezEnd);
        bezPalm.Basis = Basis.LookingAt((bezEnd - bez).Normalized(), Vector3.Up);
        Palm(bezPalm, 0.1f, 0.045f, 0.009f, 3, 0.05f, 0.09f, tips, rng);

        // Back tine: a short point behind, halfway up.
        var back = Beam(0.55f);
        Tine(root, back, back + new Vector3(side * 0.03f, 0.05f, 0.08f), back + new Vector3(side * 0.05f, 0.08f, 0.17f), 0.012f, tips);

        // The top palm: the beam's end flattens into a broad plate, facing out sideways, fringed with points.
        var crown = Beam(0.92f);
        var top = Pivot(root, "TopPalm", crown);
        top.Basis = Basis.LookingAt((Beam(0.96f) - Beam(0.86f)).Normalized(), Vector3.Up);
        Palm(top, 0.14f, 0.07f, 0.012f, 4, 0.09f, 0.18f, tips, rng);
    }

    /// <summary>
    /// A palm: a flat blade of antler, thin in its own X, running <paramref name="length"/> out along its own -Z from
    /// the pivot and widening towards its far end like a hand, with <paramref name="points"/> tines fanning out of that
    /// end, from straight on round to upward (its +Y), each between <paramref name="shortest"/> and
    /// <paramref name="longest"/> long, like fingers.
    /// </summary>
    private static void Palm(Node3D pivot, float length, float width, float thickness, int points, float shortest, float longest,
        Material antler, RandomNumberGenerator rng)
    {
        var middle = new Vector3(0, width * 0.25f, -length * 0.5f);
        Func<Vector3, Vector3> blade = u =>
        {
            float far = Mathf.Clamp((1f - u.Z) * 0.5f, 0f, 1f);
            return new Vector3(u.X * thickness, u.Y * width * Mathf.Lerp(0.35f, 1f, far), u.Z * length * 0.5f);
        };
        Attach(pivot, "Palm", Ellipsoid(u => blade(u) + middle, 14, 8, antler));
        for (int t = 0; t < points; t++)
        {
            float a = Mathf.Lerp(-0.25f, 1.25f, t / (float)Mathf.Max(1, points - 1)) + rng.RandfRange(-0.1f, 0.1f);
            var edge = middle + new Vector3(0, Mathf.Sin(a) * width * 0.85f, -Mathf.Cos(a) * length * 0.45f);
            var direction = new Vector3(rng.RandfRange(-0.15f, 0.15f), Mathf.Sin(a), -Mathf.Cos(a)).Normalized();
            float reach = rng.RandfRange(shortest, longest);
            var tip = edge + direction * reach;
            Attach(pivot, "Point", Tube([edge, edge.Lerp(tip, 0.5f) + Vector3.Up * reach * 0.08f, tip],
                [thickness * 1.3f, thickness * 0.9f, thickness * 0.3f], 6, antler, capEnd: true));
        }
    }

    /// <summary>A tine curving through <paramref name="bend"/> from <paramref name="from"/> to a point at <paramref name="to"/>.</summary>
    private static void Tine(Node3D parent, Vector3 from, Vector3 bend, Vector3 to, float radius, Material antler) =>
        Attach(parent, "Tine", Tube([from, bend, to], [radius, radius * 0.75f, radius * 0.35f], 6, antler, capEnd: true));

    /// <summary>Deep at the chest, with the shoulders a little higher than the rump, and a thick winter coat.</summary>
    private static Vector3 BodyShape(Vector3 u)
    {
        var p = new Vector3(u.X * 0.24f, u.Y * 0.27f, u.Z * 0.62f);
        float chest = Mathf.SmoothStep(0.4f, -0.4f, p.Z);
        p.X *= 0.92f + chest * 0.12f;
        if (u.Y < 0)
            p.Y *= 0.85f + chest * 0.25f;
        else
            p.Y *= 0.95f + chest * 0.12f;
        return p;
    }
}
