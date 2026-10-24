using System;
using Godot;

namespace IceAgeWorld;

/// <summary>
/// A woolly rhinoceros, built entirely in code to a real one's proportions: about 1.6 m at the shoulder and 3.5 m long,
/// with a long, barrel-shaped body on short, thick legs, a great hump of muscle over the shoulders that carries the
/// heavy head, and a long head held low to crop the grass. On its nose stand two horns, the front one nearly a metre
/// long, flattened side to side and swept forward (worn flat on its front edge from sweeping snow off the grass), the
/// second short, behind it. A long, shaggy, red-brown coat covers it, darker on the legs, with small furred ears and
/// a short tufted tail, and it walks on round, three-toed feet. It grazes, wallows, and lives mostly alone, a mother
/// keeping her calf with her. The model faces -Z.
/// </summary>
public partial class WoollyRhino : Quadruped
{
    private const int Seed = 97;

    private const float BodyCentre = 1.05f;

    /// <summary>
    /// How much bigger than a real woolly rhino this one is drawn: an ice-age giant nearly 3 m at the shoulder, taller
    /// than the cave lion but well short of a mammoth. The model is built to a real rhino's measurements and scaled up whole.
    /// </summary>
    private const float BodyScale = 1.8f;

    /// <summary>
    /// A friendlier face than a real rhino's: the head drawn a little bigger, with bigger eyes, so it reads as gentle,
    /// which a grazing rhino mostly is.
    /// </summary>
    private const float HeadScale = 1.4f;
    private const float EyeScale = 1.6f;

    /// <summary>
    /// How many times over a rhino shrugs off any harm, from bites, maulings or falls: a hide over a centimetre thick
    /// under its deep coat, so only the biggest hunters or a whole pack can bring one down. Less than a mammoth's.
    /// </summary>
    public const float Toughness = 3f;

    private static readonly Color Coat = new(0.42f, 0.27f, 0.15f);
    private static readonly Color Dark = new(0.2f, 0.13f, 0.08f);
    private static readonly Color Skin = new(0.28f, 0.2f, 0.15f);
    private static readonly Color FurRoot = new(0.75f, 0.72f, 0.7f);
    private static readonly Color FurTip = new(1.15f, 1.1f, 1.02f);
    private static readonly Vector3 CoatDrift = new(0, -0.6f, 0.6f);

    private Node3D[] _ears = [];
    private Node3D[] _tail = [];

    public override string DisplayName => "Woolly rhino";
    public override string YoungName => "Woolly rhino calf";

    // A rhino cow is a little smaller than a bull.
    protected override float FemaleSize => 0.92f;

    // Its long, heavy head hangs on a short neck, so it can barely turn it to either side.
    protected override float HeadTurn => 0.45f;

    public override AnimalStats Stats { get; } = new()
    {
        // Rhinos charge at some 40 km/h for a short way, but they are heavy, turn slowly and can barely jump; they
        // wade and wallow rather than swim.
        Scores = new() { JumpHeight = 2, JumpLength = 3, LandSpeed = 6, WaterSpeed = 3, Agility = 3, Stamina = 6 },
        FloatDepth = 1.1f * BodyScale,
        WadeDepth = 0.6f * BodyScale,
        MouthDistance = 1.9f * BodyScale,
        EatReach = 0.7f * BodyScale,
        CanGraze = true,
        BodyRadius = 0.55f * BodyScale,
        BodyHeight = 1.75f * BodyScale,
        // The shoulder hump comes right up to the top.
        BackHeight = 0.95f,
        CameraHeight = 1.5f * BodyScale,
        CameraDistance = 7f * BodyScale,
    };

    // Short, thick legs: the belly hangs well down between them.
    protected override float FrontUpperLength => 0.44f;
    protected override float FrontLowerLength => 0.36f;
    protected override float BackUpperLength => 0.46f;
    protected override float BackLowerLength => 0.34f;
    protected override float FootHeight => 0.1f;
    protected override float ShoulderOffset => 0.78f;
    protected override float HipOffset => 0.82f;
    protected override float LegSpread => 0.26f;
    protected override float LieDrop => 0.5f;
    protected override float DeadFlank => 0.5f;
    protected override float DeadRoll => BodyCentre;
    protected override float Cadence => 2.8f / Mathf.Sqrt(BodyScale);
    // The head already hangs low, built for grazing, so it needs only a short dip to reach the grass.
    protected override float HeadDown => -0.9f;

    // A woolly rhino grazed with its broad, square lips at the turf and its huge head held low, swinging it slowly from
    // side to side like a mower, cropping steadily. It sucks water up.
    protected override FeedingStyle FeedingStyleFor(Food food) => food switch
    {
        Food.Water => Sipping,
        _ => new() { BiteMin = 0.8f, BiteMax = 1.3f, BiteSeconds = 0.4f, Gape = 0.5f, Held = 0f, Tug = 0.04f, Grind = 0.06f, Sweep = 0.35f, SweepSeconds = 3.5f },
    };
    protected override float CrouchDrop => 0.08f;

    protected override void AnimateExtras(float stride, float run, float dt)
    {
        for (int i = 0; i < 2; i++)
        {
            // A rhino's ears are always on the move, swivelling to catch sounds, since it sees poorly.
            float swivel = Mathf.Sin(Clock * 0.7f + i * 1.9f) * 0.25f * Alive;
            float twitch = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Clock * 1.3f + i * 2.7f)), 20f) * 0.4f * Alive;
            _ears[i].Rotation = new Vector3(twitch, swivel, 0);
        }

        // The short tail swishes at flies, and goes up stiffly at a run.
        for (int i = 0; i < _tail.Length; i++)
        {
            float swish = Mathf.Sin(Clock * 2.2f - i * 0.8f) * 0.25f * Alive;
            _tail[i].Rotation = new Vector3(i == 0 ? -0.9f * run : 0f, swish, 0);
        }
    }

    protected override void GoLimpExtras()
    {
        foreach (var segment in _tail)
            segment.Rotation = Limp(segment.Rotation, Vector3.Zero);
    }

    protected override void Build()
    {
        Scale = Vector3.One * BodyScale;
        var rng = new RandomNumberGenerator { Seed = Seed };
        var coat = ProceduralTextures.Fur(Seed, Coat * 0.75f, Coat * 1.1f, 3f);
        var grain = ProceduralTextures.Colouring(coat);
        var legCoat = ProceduralTextures.Fur(Seed + 1, Dark * 0.8f, Dark * 1.2f, 3f);
        var legGrain = ProceduralTextures.Colouring(legCoat);

        // Paler and more golden over the hump and back, where the sun bleached the long guard hairs, darker beneath.
        Func<Vector3, Vector3, Color> furColour = (p, n) => grain(p, n)
            .Lerp(Coat * 1.35f, Mathf.SmoothStep(0.3f, 0.9f, n.Y) * 0.5f)
            .Lerp(Dark, Mathf.SmoothStep(-0.2f, -0.8f, n.Y) * 0.6f);

        var body = Pivot(Frame, "Body", new Vector3(0, BodyCentre, 0));
        Attach(body, "Hide", Ellipsoid(BodyShape, 36, 22, coat));
        Attach(body, "Coat", Strands(rng, OnShape(rng, BodyShape, 9000, u => u.Y >= -0.4f), CoatDrift,
            0.06f, 0.11f, FurRoot, FurTip, Hair, width: 0.026f, colouring: furColour));
        // The coat hangs longest under the belly and chest, in a shaggy skirt.
        Attach(body, "Skirt", Strands(rng, OnShape(rng, BodyShape, 2600, u => u.Y < -0.4f), new Vector3(0, -1f, 0.2f),
            0.1f, 0.18f, FurRoot, FurTip, Hair, width: 0.028f, colouring: furColour));

        // A short, massive neck rising into the hump, carrying the head out in front and low.
        Neck = Pivot(Frame, "Neck", new Vector3(0, BodyCentre + 0.05f, -1.0f));
        var headAt = new Vector3(0, -0.12f, -0.32f);
        var joint = new Vector3(0.36f, 0.4f, 0.3f);
        Attach(Neck, "Joint", Ellipsoid(joint, 14, 10, coat));
        Attach(Neck, "JointFur", Strands(rng, OnShape(rng, u => u * joint, 1100, _ => true), CoatDrift,
            0.07f, 0.12f, FurRoot, FurTip, Hair, width: 0.026f, colouring: furColour));
        Attach(Neck, "Throat", Tube([Vector3.Zero, headAt], [0.36f, 0.24f], 12, coat, capEnd: false));
        Attach(Neck, "NeckFur", Strands(rng, OnSegment(rng, 900, Vector3.Zero, headAt, 0.36f, 0.24f), new Vector3(0, -0.8f, 0.4f),
            0.08f, 0.14f, FurRoot, FurTip, Hair, width: 0.026f, colouring: furColour));
        Head = Pivot(Neck, "Head", headAt);
        Head.Scale = Vector3.One * HeadScale;
        BuildHead(rng, coat, furColour);

        // Thick, pillar-like legs, dark and shaggy, on round three-toed feet.
        Func<Vector3, Vector3, Color> legColour = (p, n) => legGrain(p, n);
        BuildLegs(rng, legCoat, legColour, coat, furColour, (0.14f, 0.11f, 0.095f), (0.15f, 0.105f, 0.09f),
            new Vector3(0.17f, 0.3f, 0.22f), 600, (0.04f, 0.08f), FurRoot, FurTip, (foot, _) => Foot(foot, rng, legCoat, legColour));

        BuildTail(rng, coat, furColour);
    }

    /// <summary>
    /// A long head held low and angled down, broad across the cheeks, ending in wide, square lips for cropping grass.
    /// The long front horn sweeps forward and up off the nose, flattened side to side, and the short second horn stands
    /// between the eyes. Small eyes sit low on the sides, and small, furred ears stand up at the back.
    /// </summary>
    private void BuildHead(RandomNumberGenerator rng, StandardMaterial3D coat, Func<Vector3, Vector3, Color> furColour)
    {
        var skin = new StandardMaterial3D { AlbedoColor = Skin, Roughness = 0.8f };
        var horn = ProceduralTextures.Fur(Seed + 2, new Color(0.22f, 0.19f, 0.15f), new Color(0.42f, 0.37f, 0.3f), 8f);
        horn.Roughness = 0.55f;

        // The whole face tips down, so the lips meet the grass while the head is carried low.
        var face = Pivot(Head, "Face", Vector3.Zero);
        face.Rotation = new Vector3(-0.25f, 0, 0);

        Func<Vector3, Vector3> skull = u =>
        {
            // Broad and deep at the back, narrowing a little towards the nose and flattening on top.
            float taper = Mathf.Lerp(1f, 0.72f, Mathf.SmoothStep(0.4f, -1f, u.Z));
            return new Vector3(u.X * 0.19f * taper, u.Y * 0.2f * taper * (u.Y > 0 ? 0.85f : 1f), u.Z * 0.4f);
        };
        var skullAt = new Vector3(0, 0, -0.26f);
        Attach(face, "Skull", Ellipsoid(skull, 22, 14, coat), skullAt);
        Attach(face, "SkullFur", Strands(rng, OnShape(rng, u => skull(u) + skullAt, 900,
                u => u.Z > -0.55f && !(Mathf.Abs(u.X) > 0.55f && u.Y > -0.35f && u.Y < 0.45f && u.Z < 0f && u.Z > -0.5f)),
            new Vector3(0, -0.3f, 0.8f), 0.025f, 0.05f, FurRoot, FurTip, Hair, width: 0.018f, colouring: furColour));

        // Wide, square lips of bare skin: a grazer's mouth, unlike the pointed, grasping lip of the browsing rhinos.
        var lips = new Vector3(0.12f, 0.085f, 0.08f);
        var lipsAt = new Vector3(0, -0.07f, -0.6f);
        Attach(face, "Lips", Ellipsoid(u => new Vector3(u.X * lips.X * (u.Z < 0 ? 1f - 0.25f * u.Z * u.Z : 1f), u.Y * lips.Y, u.Z * lips.Z),
            14, 10, skin), lipsAt);
        foreach (float side in new[] { -1f, 1f })
        {
            var nostril = new StandardMaterial3D { AlbedoColor = new Color(0.06f, 0.05f, 0.05f), Roughness = 0.6f };
            Attach(face, "Nostril", Ellipsoid(new Vector3(0.012f, 0.02f, 0.01f), 8, 6, nostril), lipsAt + new Vector3(side * 0.05f, 0.045f, -0.055f));
        }
        var jaw = Jaw(face, new Vector3(0, -0.12f, -0.3f));
        Attach(jaw, "Chin", Ellipsoid(new Vector3(0.1f, 0.05f, 0.18f), 12, 8, coat), new Vector3(0, 0f, -0.18f));
        MouthInside(jaw, new Vector3(0.07f, 0.015f, 0.12f), new Vector3(0, 0.01f, -0.2f));

        // The front horn: flattened side to side into a blade, sweeping forward and up from the nose, thinning to a
        // blunt tip. The second horn, far shorter, stands up behind it, between the eyes.
        var front = Pivot(face, "Horn", new Vector3(0, 0.08f, -0.5f));
        front.Scale = new Vector3(0.45f, 1f, 1f);
        Attach(front, "Blade", Tube([Vector3.Zero, new(0, 0.14f, -0.1f), new(0, 0.28f, -0.23f), new(0, 0.4f, -0.38f), new(0, 0.48f, -0.53f)],
            [0.13f, 0.1f, 0.07f, 0.04f, 0.012f], 12, horn, capEnd: true));
        var rear = Pivot(face, "Horn", new Vector3(0, 0.15f, -0.22f));
        rear.Scale = new Vector3(0.6f, 1f, 1f);
        Attach(rear, "Blade", Tube([Vector3.Zero, new(0, 0.1f, -0.02f), new(0, 0.2f, -0.06f)], [0.08f, 0.05f, 0.012f], 10, horn, capEnd: true));

        _ears = new Node3D[2];
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;

            // Small dark eyes, low on the side of the head, behind the horns: a rhino sees poorly but hears and smells well.
            Eye(face, new Vector3(side * 0.155f, 0.03f, -0.3f), new Vector3(0, -side * 1.1f, 0), 0.016f * EyeScale, new Color(0.18f, 0.11f, 0.06f), 0.7f);

            // Small, upright ears at the back of the head, tufted with long hair.
            _ears[i] = Pivot(face, "Ear", new Vector3(side * 0.12f, 0.15f, 0.04f));
            var tilt = Pivot(_ears[i], "Tilt", Vector3.Zero);
            tilt.Rotation = new Vector3(0.2f, 0, -side * 0.4f);
            var ear = new Vector3(0.04f, 0.075f, 0.022f);
            var earAt = new Vector3(0, 0.06f, 0);
            Attach(tilt, "Ear", Ellipsoid(ear, 12, 8, coat), earAt);
            Attach(tilt, "EarFur", Strands(rng, OnShape(rng, u => u * ear + earAt, 90, _ => true), new Vector3(0, 1f, 0.2f),
                0.03f, 0.06f, FurRoot, FurTip, Hair, width: 0.016f, colouring: furColour));
        }
    }

    /// <summary>A short tail, a few jointed segments with a tuft of long dark hair at the end.</summary>
    private void BuildTail(RandomNumberGenerator rng, StandardMaterial3D coat, Func<Vector3, Vector3, Color> furColour)
    {
        Vector3[] path = [Vector3.Zero, new(0, -0.1f, 0.08f), new(0, -0.24f, 0.12f), new(0, -0.38f, 0.13f)];
        float[] radii = [0.05f, 0.04f, 0.032f, 0.026f];
        _tail = new Node3D[path.Length - 1];
        Node3D parent = Frame;
        for (int i = 0; i < _tail.Length; i++)
        {
            var at = i == 0 ? new Vector3(0, BodyCentre + 0.3f, 1.18f) : path[i] - path[i - 1];
            _tail[i] = Pivot(parent, "Tail", at);
            var end = path[i + 1] - path[i];
            Attach(_tail[i], "Joint", Ellipsoid(Vector3.One * radii[i], 8, 6, coat));
            Attach(_tail[i], "Segment", Tube([Vector3.Zero, end], [radii[i], radii[i + 1]], 8, coat, capEnd: false));
            Attach(_tail[i], "Fur", Strands(rng, OnSegment(rng, 50, Vector3.Zero, end, radii[i], radii[i + 1]),
                end.Normalized() * 0.7f, 0.03f, 0.05f, FurRoot, FurTip, Hair, width: 0.016f, colouring: furColour));
            parent = _tail[i];
        }
        var tuftAt = path[^1] - path[^2];
        Attach(_tail[^1], "Tuft", Strands(rng, OnShape(rng, u => u * 0.03f + tuftAt, 80, _ => true), Vector3.Down,
            0.08f, 0.13f, Dark, Dark * 1.5f, Hair, width: 0.02f));
    }

    /// <summary>
    /// A round, columnar foot, furred to the ground, on three toes, each capped with a broad, blunt nail, as all
    /// rhinos have.
    /// </summary>
    private void Foot(Node3D foot, RandomNumberGenerator rng, Material coat, Func<Vector3, Vector3, Color> furColour)
    {
        var nail = new StandardMaterial3D { AlbedoColor = new Color(0.3f, 0.27f, 0.23f), Roughness = 0.5f };
        var pad = new Vector3(0.12f, 0.07f, 0.13f);
        var at = new Vector3(0, -FootHeight + pad.Y, -0.02f);
        Attach(foot, "Foot", Ellipsoid(pad, 14, 10, coat), at);
        Attach(foot, "FootFur", Strands(rng, OnShape(rng, u => u * pad + at, 160, u => u.Y > -0.2f), new Vector3(0, -1f, -0.2f),
            0.04f, 0.07f, FurRoot, FurTip, Hair, width: 0.02f, colouring: furColour));
        for (int t = -1; t <= 1; t++)
        {
            var toeAt = at + new Vector3(t * 0.075f, -pad.Y * 0.45f, -pad.Z * (0.85f - 0.12f * Mathf.Abs(t)));
            Attach(foot, "Nail", Ellipsoid(new Vector3(0.045f, 0.035f, 0.035f), 10, 6, nail), toeAt);
        }
    }

    /// <summary>
    /// A long barrel of a body, deep and round, with a great hump over the shoulders, where the muscle that holds up
    /// the heavy head is anchored, and the back dipping behind it before rising a little to the rump.
    /// </summary>
    private static Vector3 BodyShape(Vector3 u)
    {
        var p = new Vector3(u.X * 0.48f, u.Y * 0.5f, u.Z * 1.2f);
        // Full right to the ends, so the chest and rump stay deep.
        float full = Mathf.Pow(Mathf.Max(0.05f, Mathf.Sqrt(Mathf.Max(0f, 1f - u.Z * u.Z))), -0.3f);
        p.X *= Mathf.Min(full, 1.4f);
        p.Y *= Mathf.Min(full, 1.4f);
        if (u.Y > 0)
        {
            float hump = Mathf.Exp(-Mathf.Pow((u.Z + 0.55f) / 0.28f, 2f));
            p.Y += u.Y * u.Y * 0.22f * hump * (1f - Mathf.Abs(u.X));
        }
        return p;
    }
}
