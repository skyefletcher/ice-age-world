using System;
using Godot;

namespace IceAgeWorld;

/// <summary>
/// The big ice-age cats built on the four-legged skeleton (the cave lion and the sabre-toothed smilodon): a long,
/// deep-chested body that tucks up at the waist, legs ending in big, round paws with the claws drawn in, a broad cat's
/// head with a short muzzle, whisker pads, a pink nose, amber eyes set to look forward and small rounded ears, and a
/// tail. Each cat supplies its proportions, colours and tail, and can add to its mouth (the smilodon's sabres). Cats
/// sit up on their haunches and lie sphinx-like, as the bear does. Built to the real animal's measurements, then
/// scaled up whole by <see cref="BodyScale"/>. The model faces -Z.
/// </summary>
public abstract partial class BigCat : Quadruped
{
    /// <summary>
    /// How much bigger than the real cat these are drawn: ice-age giants some 3.4 m at the shoulder, a little bigger
    /// than the snow leopard (itself drawn 2.6 times life size) so a lion still stands well taller than a leopard, as it
    /// does in life, and taller than the polar bear too.
    /// </summary>
    protected const float BodyScale = 2.8f;

    protected abstract int Seed { get; }

    // A big cat can turn its head well round to look back along its flank, though not as far as the supple snow leopard.
    protected override float HeadTurn => 1.2f;

    /// <summary>Height of the body's middle, and its half-width, half-depth and half-length.</summary>
    protected abstract float BodyCentre { get; }
    protected abstract Vector3 BodySize { get; }

    /// <summary>How far the back slopes down from the shoulders to the hips, in radians.</summary>
    protected virtual float BackSlope => 0f;

    /// <summary>The coat on the back and flanks, the pale belly, throat and muzzle, and the dark ear backs and lip line.</summary>
    protected abstract Color Coat { get; }
    protected abstract Color Belly { get; }
    protected abstract Color Dark { get; }

    /// <summary>Faint dark spots on the coat, if it has them, in this colour.</summary>
    protected virtual Color? Spots => null;

    protected virtual float HeadScale => 1f;

    /// <summary>How far the muzzle reaches forward, against a lion's.</summary>
    protected virtual float MuzzleScale => 1f;

    /// <summary>The size of the eyes and ears against a lion's, and how wide the pupils open.</summary>
    protected virtual float EyeScale => 1f;
    protected virtual float EarScale => 1f;
    protected virtual float Pupil => 0.42f;

    /// <summary>Leg thickness at the top, knee and ankle, front and back, and the muscle where the legs join the body.</summary>
    protected abstract (float Upper, float Knee, float Ankle) FrontLeg { get; }
    protected abstract (float Upper, float Knee, float Ankle) BackLeg { get; }
    protected abstract Vector3 LegMuscle { get; }

    /// <summary>
    /// The tail's centre line and thickness from root to tip, hanging back from the rump, and whether it ends in a dark
    /// tuft. Each stretch between two points is a jointed segment, so the tail can swing and its end curl.
    /// </summary>
    protected abstract Vector3[] TailPath { get; }
    protected abstract float[] TailRadii { get; }
    protected virtual bool TailTuft => false;

    /// <summary>How long the coat is, from short summer fur to a thick winter coat.</summary>
    protected virtual (float Min, float Max) FurLength => (0.022f, 0.036f);

    protected override float FootHeight => 0.07f;
    protected override bool SitsUp => true;
    protected override float SitPitch => 0.8f;
    protected override float HeadDown => -1.35f;
    // Big cats walk with a long, slow, rolling stride; drawn this big, the legs turn over more slowly still.
    protected override float Cadence => 2.8f / Mathf.Sqrt(BodyScale);
    // A cat drops its jaw wide to get its side teeth into meat.
    protected override float JawGape => 0.6f;

    // A big cat cannot chew: it turns its head onto its side to slice meat off with its scissor-like cheek teeth,
    // pulling up as it shears, and keeps to one side for a while before changing. It laps water fast with its tongue.
    protected override FeedingStyle FeedingStyleFor(Food food) => food switch
    {
        Food.Water => Lapping with { Laps = 4f },
        _ => new() { BiteMin = 1.1f, BiteMax = 2f, BiteSeconds = 0.4f, Held = 0.6f, Tug = 0.25f, Tilt = 0.45f, SwapSide = 0.3f },
    };
    // The thighs stand clear of the rump, so the inside of the far one shows from behind.
    protected override bool MuscleFurAllRound => true;

    private static readonly Color FurRoot = new(0.85f, 0.85f, 0.85f);
    private static readonly Color FurTip = new(1.08f, 1.06f, 1.02f);
    private static readonly Vector3 CoatDrift = new(0, -0.3f, 0.8f);

    private Node3D[] _ears = [];
    private Node3D[] _tail = [];

    protected override void Build()
    {
        Scale = Vector3.One * BodyScale;
        var rng = new RandomNumberGenerator { Seed = (ulong)Seed };
        var coat = Spots is { } spot
            ? ProceduralTextures.SpottedFur(Seed, Coat * 0.85f, Coat * 1.04f, Coat, spot, 3f, rosettes: false)
            : ProceduralTextures.Fur(Seed, Coat * 0.85f, Coat * 1.04f, 3f);
        var grain = ProceduralTextures.Colouring(coat);

        // Paler underneath, fading to cream on the belly, chest and inside the legs, as on most big cats.
        Func<Vector3, Vector3, Color> furColour = (p, n) => grain(p, n).Lerp(Belly, Mathf.SmoothStep(-0.15f, -0.75f, n.Y) * 0.85f);

        var body = Pivot(Frame, "Body", new Vector3(0, BodyCentre, 0));
        body.Rotation = new Vector3(BackSlope, 0, 0);
        Attach(body, "Hide", Ellipsoid(BodyShape, 36, 22, coat));
        Attach(body, "Coat", Strands(rng, OnShape(rng, BodyShape, 7000, u => u.Y >= -0.35f), CoatDrift,
            FurLength.Min, FurLength.Max, FurRoot, FurTip, Hair, width: 0.016f, colouring: furColour));
        Attach(body, "Belly", Strands(rng, OnShape(rng, BodyShape, 1800, u => u.Y < -0.35f), new Vector3(0, -1f, 0.3f),
            FurLength.Min * 1.3f, FurLength.Max * 1.4f, FurRoot, FurTip, Hair, width: 0.016f, colouring: furColour));

        // A thick, muscular neck carrying the head out in front, a little above the line of the back.
        Neck = Pivot(Frame, "Neck", new Vector3(0, BodyCentre + 0.06f, -(BodySize.Z - 0.14f)));
        // The head is carried up at about the height of the shoulders, not slung low like a weasel's.
        var headAt = new Vector3(0, 0.12f, -0.26f);
        var joint = new Vector3(BodySize.X * 0.78f, BodySize.Y * 0.72f, 0.17f);
        Attach(Neck, "Joint", Ellipsoid(joint, 14, 10, coat));
        Attach(Neck, "JointFur", Strands(rng, OnShape(rng, u => u * joint, 700, _ => true), CoatDrift,
            FurLength.Min, FurLength.Max * 1.2f, FurRoot, FurTip, Hair, width: 0.016f, colouring: furColour));
        Attach(Neck, "Throat", Tube([Vector3.Zero, headAt], [joint.X * 0.95f, 0.11f], 12, coat, capEnd: false));
        Attach(Neck, "NeckFur", Strands(rng, OnSegment(rng, 900, Vector3.Zero, headAt, joint.X * 0.95f, 0.11f), CoatDrift,
            FurLength.Min, FurLength.Max * 1.3f, FurRoot, FurTip, Hair, width: 0.016f, colouring: furColour));
        Head = Pivot(Neck, "Head", headAt);
        Head.Scale = Vector3.One * HeadScale;
        BuildHead(rng, coat, furColour);

        BuildLegs(rng, coat, furColour, coat, furColour, FrontLeg, BackLeg, LegMuscle, 450, (FurLength.Min * 0.8f, FurLength.Max * 0.8f),
            FurRoot, FurTip, (foot, _) => Paw(foot, rng, coat, furColour));

        BuildTail(rng, coat, furColour);
        BuildExtras(rng);
    }

    /// <summary>Adds to the finished body, e.g. a lion's mane round the <see cref="Quadruped.Head"/> and <see cref="Quadruped.Neck"/>.</summary>
    protected virtual void BuildExtras(RandomNumberGenerator rng) { }

    /// <summary>
    /// The tail as a chain of segments along <see cref="TailPath"/>, each with a ball at its root so the joints stay
    /// smooth as it bends, and furred along its own length so the fur follows the curve.
    /// </summary>
    private void BuildTail(RandomNumberGenerator rng, StandardMaterial3D coat, Func<Vector3, Vector3, Color> furColour)
    {
        var path = TailPath;
        var radii = TailRadii;
        _tail = new Node3D[path.Length - 1];
        Node3D parent = Frame;
        for (int i = 0; i < _tail.Length; i++)
        {
            var at = i == 0 ? new Vector3(0, BodyCentre + BodySize.Y * 0.45f, BodySize.Z - 0.05f) : path[i] - path[i - 1];
            _tail[i] = Pivot(parent, "Tail", at);
            var end = path[i + 1] - path[i];
            Attach(_tail[i], "Joint", Ellipsoid(Vector3.One * radii[i], 10, 8, coat));
            Attach(_tail[i], "Segment", Tube([Vector3.Zero, end], [radii[i], radii[i + 1]], 10, coat, capEnd: i == _tail.Length - 1));
            Attach(_tail[i], "Fur", Strands(rng, OnSegment(rng, 70, Vector3.Zero, end, radii[i], radii[i + 1]),
                end.Normalized() * 0.7f, FurLength.Min, FurLength.Max, FurRoot, FurTip, Hair, width: 0.016f, colouring: furColour));
            parent = _tail[i];
        }

        if (TailTuft)
        {
            // A lion's tail ends in a dark tuft of long hair, carried on along the line of the tip.
            var along = (path[^1] - path[^2]).Normalized();
            var tuftAt = path[^1] - path[^2] + along * 0.025f;
            var tuft = Vector3.One * 0.045f;
            var dark = new StandardMaterial3D { AlbedoColor = Dark, Roughness = 1f };
            Attach(_tail[^1], "Tuft", Ellipsoid(tuft, 10, 8, dark), tuftAt);
            Attach(_tail[^1], "TuftFur", Strands(rng, OnShape(rng, u => u * tuft + tuftAt, 160, _ => true), along + Vector3.Down * 0.3f,
                0.04f, 0.07f, Dark, Dark * 1.4f, Hair, width: 0.02f));
        }
    }

    /// <summary>
    /// A broad, rounded cat's head: a domed skull, a short muzzle with puffed, pale whisker pads and a pink nose, a jaw
    /// hinged to drop open, forward-looking amber eyes with pale fur beneath them, and small, round ears with dark backs.
    /// </summary>
    private void BuildHead(RandomNumberGenerator rng, StandardMaterial3D coat, Func<Vector3, Vector3, Color> furColour)
    {
        // The pale face markings are painted onto a light, finely grained hide by vertex colour.
        var face = ProceduralTextures.Fur(Seed + 1, new Color(0.85f, 0.85f, 0.85f), Colors.White, 6f);
        face.VertexColorUseAsAlbedo = true;
        var nose = new StandardMaterial3D { AlbedoColor = new Color(0.6f, 0.4f, 0.36f), Roughness = 0.45f };
        var nostril = new StandardMaterial3D { AlbedoColor = new Color(0.08f, 0.05f, 0.05f), Roughness = 0.6f };

        var skull = new Vector3(0.12f, 0.105f, 0.14f);
        var skullAt = new Vector3(0, 0.01f, -0.06f);
        Attach(Head, "Skull", Ellipsoid(skull, 24, 16, coat), skullAt);
        Attach(Head, "SkullFur", Strands(rng, OnShape(rng, u => u * skull + skullAt, 900,
                u => u.Z > -0.55f && !(Mathf.Abs(u.X) > 0.3f && Mathf.Abs(u.X) < 0.75f && u.Y > -0.1f && u.Y < 0.5f && u.Z < 0f)),
            CoatDrift, 0.012f, 0.022f, FurRoot, FurTip, Hair, width: 0.012f, colouring: furColour));

        // Full cheeks flaring out below and behind the eyes, pale beneath.
        foreach (float side in new[] { -1f, 1f })
        {
            var cheek = new Vector3(0.06f, 0.06f, 0.07f);
            var cheekAt = new Vector3(side * 0.07f, -0.04f, -0.09f);
            Attach(Head, "Cheek", Ellipsoid(cheek, 14, 10, face, (_, n) => Coat.Lerp(Belly, Mathf.SmoothStep(0.1f, -0.5f, n.Y))), cheekAt);
            Attach(Head, "CheekFur", Strands(rng, OnShape(rng, u => u * cheek + cheekAt, 260, u => u.X * side > 0.2f && u.Z > -0.5f),
                new Vector3(side * 0.3f, -0.4f, 0.7f), 0.02f, 0.04f, FurRoot, FurTip, Hair, width: 0.012f,
                colouring: (p, n) => furColour(p, n).Lerp(Belly, Mathf.SmoothStep(0f, -0.5f, n.Y))));
        }

        // The muzzle hangs off a pivot where it meets the face, so each cat can draw it longer or shorter.
        var snout = Pivot(Head, "Snout", new Vector3(0, -0.01f, -0.14f));
        snout.Scale = new Vector3(1f, 1f, MuzzleScale);
        // A cat's face slopes down from the brow to the nose rather than jutting straight forward.
        snout.Rotation = new Vector3(-0.12f, 0, 0);

        // A broad, straight bridge from the forehead down to the nose.
        Attach(snout, "Bridge", Ellipsoid(new Vector3(0.072f, 0.062f, 0.11f), 16, 10, coat), new Vector3(0, 0.022f, -0.04f));
        Attach(snout, "Muzzle", Ellipsoid(new Vector3(0.078f, 0.065f, 0.088f), 16, 10, face, (_, _) => Coat.Lerp(Belly, 0.4f)),
            new Vector3(0, -0.025f, -0.08f));

        // The nose: wide at the top, narrowing to a point above the split lip, with dark nostrils at its sides.
        // The nose leather lies flat across the front of the muzzle rather than standing out from it.
        var noseAt = new Vector3(0, 0.006f, -0.157f);
        Attach(snout, "Nose", Ellipsoid(u => new Vector3(u.X * 0.03f * (0.7f + 0.4f * Mathf.Max(u.Y, -0.5f)), u.Y * 0.02f, u.Z * 0.012f),
            14, 10, nose), noseAt);

        foreach (float side in new[] { -1f, 1f })
        {
            Attach(snout, "Nostril", Ellipsoid(new Vector3(0.007f, 0.0035f, 0.005f), 8, 6, nostril), noseAt + new Vector3(side * 0.012f, -0.01f, -0.008f));

            // Puffed, pale whisker pads, edged underneath by the dark line of the upper lip, sprouting long whiskers.
            var pad = new Vector3(0.04f, 0.034f, 0.047f);
            var padAt = new Vector3(side * 0.034f, -0.04f, -0.122f);
            Attach(snout, "WhiskerPad", Ellipsoid(pad, 16, 10, face, (_, n) => Belly.Lerp(Coat, 0.25f).Lerp(Dark, Mathf.SmoothStep(-0.6f, -0.85f, n.Y))), padAt);
            Attach(snout, "Whiskers", Strands(rng, OnShape(rng, u => u * pad + padAt, 14, u => u.X * side > 0.5f && u.Z < 0.2f),
                new Vector3(side * 0.4f, 0.1f, 0.3f), 0.12f, 0.18f, Belly, Colors.White, Hair, width: 0.004f));

            // Amber eyes set forward for judging the pounce, a pale patch of fur beneath each one.
            Eye(Head, new Vector3(side * 0.06f, 0.035f, -0.168f), new Vector3(0, -side * 0.3f, 0), 0.019f * EyeScale, new Color(0.8f, 0.58f, 0.2f), Pupil);
            Attach(Head, "EyePatch", Ellipsoid(new Vector3(0.022f, 0.008f, 0.016f) * EyeScale, 10, 6, face, (_, _) => Coat.Lerp(Belly, 0.6f)),
                new Vector3(side * 0.062f, 0.035f - 0.023f * EyeScale, -0.165f));
        }

        // A white chin on a jaw hinged at the back of the mouth, which drops open to eat.
        var jaw = Jaw(snout, new Vector3(0, -0.06f, 0.06f));
        Attach(jaw, "Chin", Ellipsoid(new Vector3(0.05f, 0.03f, 0.1f), 12, 8, face, (_, n) => Belly.Lerp(Dark, Mathf.SmoothStep(0.6f, 0.85f, n.Y))),
            new Vector3(0, -0.025f, -0.13f));
        MouthInside(jaw, new Vector3(0.035f, 0.01f, 0.08f), new Vector3(0, 0.002f, -0.12f));
        BuildMouthExtras(snout, jaw, face);

        // Small, rounded ears set wide apart, black on the back.
        _ears = new Node3D[2];
        // A lion's ear stands only some 10 cm, half hidden in the fur.
        var flap = new Vector3(0.032f, 0.03f, 0.01f) * EarScale;
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            _ears[i] = Pivot(Head, "Ear", new Vector3(side * 0.085f, 0.085f, -0.02f));
            var tilt = Pivot(_ears[i], "Tilt", Vector3.Zero);
            tilt.Rotation = new Vector3(-0.1f, -side * 0.3f, -side * 0.35f);
            var flapAt = new Vector3(0, flap.Y * 0.73f, 0);
            Attach(tilt, "Flap", Ellipsoid(u => u * flap + new Vector3(0, 0, -0.01f * u.X * u.X), 14, 8, face, (p, n) =>
            {
                float edge = Mathf.Sqrt(p.X * p.X / (flap.X * flap.X) + p.Y * p.Y / (flap.Y * flap.Y));
                return n.Z > 0f ? Coat.Lerp(Dark, Mathf.SmoothStep(0.35f, 0.7f, edge)) : Coat.Lerp(Belly, 0.5f);
            }), flapAt);
        }
    }

    /// <summary>Adds to the mouth: teeth on the <paramref name="snout"/> or <paramref name="jaw"/>, drawn in the <paramref name="face"/> hide's colours.</summary>
    protected virtual void BuildMouthExtras(Node3D snout, Node3D jaw, StandardMaterial3D face) { }

    /// <summary>A big, round, furred paw on four plump toes, the claws drawn in out of sight as a cat walks.</summary>
    private void Paw(Node3D foot, RandomNumberGenerator rng, Material coat, Func<Vector3, Vector3, Color> furColour)
    {
        var paw = new Vector3(0.065f, 0.035f, 0.08f);
        var at = new Vector3(0, -FootHeight + paw.Y, -0.03f);
        Attach(foot, "Paw", Ellipsoid(paw, 14, 10, coat), at);
        Attach(foot, "PawFur", Strands(rng, OnShape(rng, u => u * paw + at, 120, u => u.Y > -0.2f), new Vector3(0, -0.4f, -0.4f),
            0.015f, 0.025f, FurRoot, FurTip, Hair, width: 0.014f, colouring: furColour));
        for (int t = 0; t < 4; t++)
        {
            float x = (t - 1.5f) * 0.032f;
            Attach(foot, "Toe", Ellipsoid(new Vector3(0.022f, 0.022f, 0.026f), 10, 6, coat),
                at + new Vector3(x, -paw.Y * 0.3f, -paw.Z * (0.8f - 0.12f * Mathf.Abs(t - 1.5f))));
        }
    }

    protected override void AnimateExtras(float stride, float run, float dt)
    {
        for (int i = 0; i < 2; i++)
        {
            float twitch = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Clock * 0.6f + i * 2.3f)), 16f) * 0.4f * Alive;
            _ears[i].Rotation = new Vector3(twitch, 0, 0);
        }

        // The tail sways lazily from the root, the swing travelling down it to the tip, and lifts out behind for balance
        // at a gallop. Its end keeps its curl, which tightens and loosens as a cat's tail tip twitches.
        for (int i = 0; i < _tail.Length; i++)
        {
            float t = (i + 1f) / _tail.Length;
            float sway = (Mathf.Sin(Clock * 0.9f - t * 1.2f) * 0.1f + Mathf.Sin(WalkCycle - t) * 0.05f * stride) * Alive;
            float curl = t > 0.6f ? Mathf.Sin(Clock * 1.7f + t * 2f) * 0.12f * Alive : 0f;
            _tail[i].Rotation = new Vector3(i == 0 ? -0.6f * run : curl, sway, 0);
        }
    }

    protected override void GoLimpExtras()
    {
        for (int i = 0; i < _tail.Length; i++)
            _tail[i].Rotation = Limp(_tail[i].Rotation, i == 0 ? new Vector3(0.2f, 0.3f, 0) : Vector3.Zero);
    }

    /// <summary>
    /// A cat's body: a deep chest behind the shoulders, the belly tucking up towards the narrow waist, and a fairly
    /// flat back. It stays full towards both ends rather than tapering like an egg, so the chest fills out between the
    /// shoulders and the rump is broad and round where the thighs join it.
    /// </summary>
    private Vector3 BodyShape(Vector3 u)
    {
        var s = BodySize;
        float full = Mathf.Pow(Mathf.Max(0.05f, Mathf.Sqrt(Mathf.Max(0f, 1f - u.Z * u.Z))), -0.45f);
        var p = new Vector3(u.X * s.X * full, u.Y * s.Y * full, u.Z * s.Z);
        float waist = Mathf.SmoothStep(-0.2f, 0.6f, u.Z);
        if (u.Y < 0)
            p.Y *= 1f - 0.3f * waist;
        else
            p.Y = s.Y * Mathf.Pow(u.Y, 0.75f);
        p.X *= 1f - 0.12f * waist;
        return p;
    }
}
