using System;
using Godot;

namespace IceAgeWorld;

/// <summary>
/// A cave hyena, the ice-age spotted hyena of Europe and Asia, bigger than any hyena alive: about a metre at the
/// shoulder, built front-heavy, with long forelegs and a deep chest, a back that slopes down to short hind legs, a
/// thick, powerful neck under a short, bristly dark mane, and a big, broad head with a blunt, dark muzzle and
/// bone-crushing jaws. Its ears are big and round, its sandy-grey coat is scattered with dark spots, and its tail is
/// short and bushy with a dark tip. Clans of them hunted together, ran down horses and reindeer, and denned in caves,
/// where their bones and their prey's are found in heaps. It lopes tirelessly, sits like a dog and lies flat. The
/// model faces -Z.
/// </summary>
public partial class CaveHyena : Quadruped
{
    private const int Seed = 103;

    private const float BodyCentre = 0.74f;

    /// <summary>
    /// How much bigger than a real cave hyena this one is drawn: some 1.3 m at the shoulder, a little taller than the
    /// arctic wolf, as the cave hyena was the bigger and heavier of the two. The model is built to a real one's
    /// measurements and scaled up whole.
    /// </summary>
    private const float BodyScale = 1.3f;

    /// <summary>
    /// The head drawn a little bigger than a real hyena's, to show off its broad face, on a muzzle of about the real
    /// length, with eyes of about the real size.
    /// </summary>
    private const float HeadScale = 1.2f;
    private const float MuzzleScale = 1.05f;
    private const float EyeScale = 1.05f;

    /// <summary>How far the back slopes down from the high shoulders to the low hips, in radians.</summary>
    private const float BackSlope = 0.16f;

    /// <summary>
    /// How many times the blotched coat repeats across a unit of body, which sets how big the blotches come out: a
    /// hand's breadth across, crowded over the flanks, legs and rump as on a real spotted hyena.
    /// </summary>
    private const float SpotScale = 1.5f;

    // A pale, greyish cream coat with bold, blackish-brown blotches, a reddish-brown mane and a black muzzle.
    private static readonly Color Coat = new(0.8f, 0.72f, 0.6f);
    private static readonly Color Belly = new(0.88f, 0.83f, 0.74f);
    private static readonly Color Dark = new(0.08f, 0.065f, 0.055f);
    private static readonly Color Spot = new(0.17f, 0.11f, 0.07f);
    private static readonly Color Mane = new(0.5f, 0.3f, 0.15f);
    private static readonly Color FurRoot = new(0.85f, 0.85f, 0.85f);
    private static readonly Color FurTip = new(1.08f, 1.05f, 1f);
    private static readonly Vector3 CoatDrift = new(0, -0.3f, 0.8f);

    private Node3D[] _ears = [];
    private Node3D[] _tail = [];

    public override string DisplayName => "Cave hyena";
    public override string YoungName => "Cave hyena cub";

    // A female hyena is the bigger, about a tenth heavier than a male, and rules the clan.
    protected override float FemaleSize => 1.03f;

    // Its thick, muscled neck, built for its bone-crushing jaws, turns less far than a wolf's.
    protected override float HeadTurn => 1f;

    public override AnimalStats Stats { get; } = new()
    {
        // Hyenas lope for kilometres without tiring and run prey down over long chases at up to 60 km/h; they are
        // fair swimmers and wade readily.
        Scores = new() { JumpHeight = 4, JumpLength = 5, LandSpeed = 7, WaterSpeed = 4, Agility = 6, Stamina = 9 },
        Abilities = Ability.Pack | Ability.Hunt,
        // A clan is led by its biggest female: female hyenas outweigh the males and rule the clan.
        LeaderSize = 1.2f,
        Companions = 3,
        // The strongest bite of any mammal its size, built to crack bones for the marrow inside.
        Strength = 1.5f,
        FloatDepth = 0.5f * BodyScale,
        WadeDepth = 0.3f * BodyScale,
        MouthDistance = 0.8f * BodyScale,
        EatReach = 0.4f * BodyScale,
        CanGraze = false,
        BodyRadius = 0.28f * BodyScale,
        BodyHeight = 0.95f * BodyScale,
        CameraHeight = 0.9f * BodyScale,
        CameraDistance = 4f * BodyScale,
    };

    // Long forelegs and short hind legs give the hyena its sloping back.
    protected override float FrontUpperLength => 0.34f;
    protected override float FrontLowerLength => 0.3f;
    protected override float BackUpperLength => 0.3f;
    protected override float BackLowerLength => 0.24f;
    protected override float FootHeight => 0.05f;
    protected override float ShoulderOffset => 0.3f;
    protected override float HipOffset => 0.34f;
    protected override float LegSpread => 0.1f;
    protected override float LieDrop => 0.5f;
    protected override float DeadFlank => 0.2f;
    protected override float DeadRoll => BodyCentre;
    protected override float Cadence => 3.3f / Mathf.Sqrt(BodyScale);
    protected override bool SitsUp => true;
    protected override float SitPitch => 0.7f;
    protected override float HeadDown => -1.35f;
    protected override float JawGape => 0.7f;

    // A hyena cracks bones: it turns its head to get the great crushing teeth at the back of its jaw on one, bites down
    // slowly and hard, and crunches, tugging little. It laps water like a dog.
    protected override FeedingStyle FeedingStyleFor(Food food) => food switch
    {
        Food.Water => Lapping with { Laps = 3f },
        _ => new() { BiteMin = 1f, BiteMax = 1.8f, BiteSeconds = 0.45f, Held = 0.3f, Tug = 0.12f, Tilt = 0.35f, SwapSide = 0.25f, Grind = 0.08f, GrindRate = 2.5f },
    };

    protected override void AnimateExtras(float stride, float run, float dt)
    {
        for (int i = 0; i < 2; i++)
        {
            float twitch = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Clock * 0.8f + i * 2.1f)), 16f) * 0.4f * Alive;
            _ears[i].Rotation = new Vector3(twitch, 0, 0);
        }

        // The bushy tail hangs down at rest, wags a little at a walk, and is carried out behind at a run.
        for (int i = 0; i < _tail.Length; i++)
        {
            float sway = (Mathf.Sin(Clock * 1.1f - i * 0.9f) * 0.1f + Mathf.Sin(WalkCycle - i) * 0.08f * stride) * Alive;
            _tail[i].Rotation = new Vector3(i == 0 ? -0.9f * run : 0f, sway, 0);
        }
    }

    protected override void GoLimpExtras()
    {
        for (int i = 0; i < _tail.Length; i++)
            _tail[i].Rotation = Limp(_tail[i].Rotation, i == 0 ? new Vector3(0.2f, 0.3f, 0) : Vector3.Zero);
    }

    protected override void Build()
    {
        Scale = Vector3.One * BodyScale;
        var rng = new RandomNumberGenerator { Seed = Seed };
        var coat = ProceduralTextures.BlotchedFur(Seed, Coat * 0.85f, Coat * 1.05f, Spot, SpotScale);
        var grain = ProceduralTextures.Colouring(coat);

        // Paler beneath and inside the legs.
        Func<Vector3, Vector3, Color> furColour = (p, n) => grain(p, n).Lerp(Belly, Mathf.SmoothStep(-0.2f, -0.8f, n.Y) * 0.7f);

        var body = Pivot(Frame, "Body", new Vector3(0, BodyCentre, 0));
        body.Rotation = new Vector3(BackSlope, 0, 0);
        Attach(body, "Hide", Ellipsoid(BodyShape, 32, 20, coat));
        Attach(body, "Coat", Strands(rng, OnShape(rng, BodyShape, 5000, u => u.Y >= -0.35f), CoatDrift,
            0.02f, 0.035f, FurRoot, FurTip, Hair, width: 0.014f, colouring: furColour));
        Attach(body, "Belly", Strands(rng, OnShape(rng, BodyShape, 1200, u => u.Y < -0.35f), new Vector3(0, -1f, 0.3f),
            0.025f, 0.045f, FurRoot, FurTip, Hair, width: 0.014f, colouring: furColour));
        // A bristly, reddish-brown mane along the top of the back, standing up from the shoulders and dwindling
        // towards the rump.
        Attach(body, "Mane", Strands(rng, OnShape(rng, BodyShape, 700, u => Mathf.Abs(u.X) < 0.16f && u.Y > 0.5f && u.Z < 0.35f),
            new Vector3(0, 1f, 0.6f), 0.05f, 0.09f, Mane * 0.8f, Mane * 1.3f, Hair, width: 0.018f));

        // A thick, muscular neck, the mane running up it to the head, which is carried a little above the back.
        Neck = Pivot(Frame, "Neck", new Vector3(0, BodyCentre + 0.1f, -0.42f));
        var headAt = new Vector3(0, 0.1f, -0.22f);
        var joint = new Vector3(0.17f, 0.2f, 0.15f);
        Attach(Neck, "Joint", Ellipsoid(joint, 14, 10, coat));
        Attach(Neck, "JointFur", Strands(rng, OnShape(rng, u => u * joint, 500, _ => true), CoatDrift,
            0.02f, 0.04f, FurRoot, FurTip, Hair, width: 0.014f, colouring: furColour));
        Attach(Neck, "Throat", Tube([Vector3.Zero, headAt], [0.16f, 0.1f], 12, coat, capEnd: false));
        Attach(Neck, "NeckFur", Strands(rng, OnSegment(rng, 600, Vector3.Zero, headAt, 0.16f, 0.1f), CoatDrift,
            0.02f, 0.04f, FurRoot, FurTip, Hair, width: 0.014f, colouring: furColour));
        Attach(Neck, "NeckMane", Strands(rng, OnSegment(rng, 380, new Vector3(0, 0.15f, 0.05f), headAt + new Vector3(0, 0.09f, 0.02f), 0.035f, 0.02f),
            new Vector3(0, 1f, 0.7f), 0.06f, 0.1f, Mane * 0.8f, Mane * 1.3f, Hair, width: 0.018f));
        Head = Pivot(Neck, "Head", headAt);
        Head.Scale = Vector3.One * HeadScale;
        BuildHead(rng, coat, furColour);

        BuildLegs(rng, coat, furColour, coat, furColour, (0.075f, 0.05f, 0.04f), (0.08f, 0.048f, 0.036f),
            new Vector3(0.06f, 0.17f, 0.1f), 350, (0.015f, 0.028f), FurRoot, FurTip, (foot, _) => Paw(foot, coat));

        BuildTail(rng, coat, furColour);
    }

    /// <summary>
    /// A big, broad head: a wide skull, pale and only faintly spotted, with heavy jaw muscles at the cheeks, a blunt
    /// black muzzle and chin, dark eyes and rounded ears standing up on top, dark round their inside edge.
    /// </summary>
    private void BuildHead(RandomNumberGenerator rng, StandardMaterial3D coat, Func<Vector3, Vector3, Color> furColour)
    {
        var face = ProceduralTextures.Fur(Seed + 1, new Color(0.85f, 0.85f, 0.85f), Colors.White, 6f);
        face.VertexColorUseAsAlbedo = true;
        // The painted colours are given as they look on screen, so they are not washed out paler.
        face.VertexColorIsSrgb = true;
        var black = new StandardMaterial3D { AlbedoColor = new Color(0.05f, 0.045f, 0.045f), Roughness = 0.4f };
        // The head is paler than the body and nearly plain, the spots fading out over the brow and cheeks.
        var hide = ProceduralTextures.Fur(Seed + 2, Coat * 0.9f, Coat * 1.06f, 3f);
        var hideGrain = ProceduralTextures.Colouring(hide);
        Func<Vector3, Vector3, Color> headColour = (p, n) => hideGrain(p, n).Lerp(Belly, Mathf.SmoothStep(-0.1f, -0.7f, n.Y) * 0.6f);

        var skull = new Vector3(0.1f, 0.09f, 0.11f);
        Attach(Head, "Skull", Ellipsoid(skull, 20, 14, hide));
        Attach(Head, "SkullFur", Strands(rng, OnShape(rng, u => u * skull, 500,
                u => u.Z > -0.5f && !(Mathf.Abs(u.X) > 0.35f && u.Y > -0.2f && u.Y < 0.6f && u.Z < 0.1f)),
            CoatDrift, 0.01f, 0.02f, FurRoot, FurTip, Hair, width: 0.011f, colouring: headColour));

        // Bulging cheeks: the huge jaw muscles that give a hyena its crushing bite.
        foreach (float side in new[] { -1f, 1f })
            Attach(Head, "Cheek", Ellipsoid(new Vector3(0.05f, 0.055f, 0.06f), 12, 8, hide), new Vector3(side * 0.06f, -0.035f, -0.02f));

        // A blunt muzzle, pale where it leaves the face and black over its front half, round the nose and lips.
        var snout = Pivot(Head, "Snout", new Vector3(0, -0.02f, -0.09f));
        snout.Scale = new Vector3(1f, 1f, MuzzleScale);
        snout.Rotation = new Vector3(-0.18f, 0, 0);
        // Narrowing a little towards the nose, so it is a muzzle rather than a ball.
        Func<Vector3, Vector3> muzzle = u =>
        {
            float taper = Mathf.Lerp(1f, 0.8f, Mathf.Max(0f, -u.Z));
            return new Vector3(u.X * 0.05f * taper, u.Y * 0.046f * taper, u.Z * 0.1f);
        };
        var muzzleAt = new Vector3(0, 0, -0.07f);
        Attach(snout, "Muzzle", Ellipsoid(muzzle, 16, 10, face, (_, n) => Coat.Lerp(Dark, Mathf.SmoothStep(0.15f, -0.55f, n.Z))), muzzleAt);
        Attach(snout, "Nose", Ellipsoid(new Vector3(0.026f, 0.019f, 0.016f), 12, 8, black), muzzleAt + new Vector3(0, 0.018f, -0.088f));

        var jaw = Jaw(snout, new Vector3(0, -0.04f, 0.02f));
        Attach(jaw, "Chin", Ellipsoid(new Vector3(0.04f, 0.022f, 0.085f), 12, 8, face, (_, n) => Coat.Lerp(Dark, Mathf.SmoothStep(0.3f, -0.4f, n.Z))), new Vector3(0, -0.01f, -0.09f));
        MouthInside(jaw, new Vector3(0.024f, 0.006f, 0.06f), new Vector3(0, 0.012f, -0.08f));

        _ears = new Node3D[2];
        var flap = new Vector3(0.04f, 0.048f, 0.012f);
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;

            Eye(Head, new Vector3(side * 0.05f, 0.03f, -0.088f), new Vector3(0, -side * 0.35f, 0), 0.016f * EyeScale, new Color(0.2f, 0.12f, 0.06f), 0.6f);

            // Rounded ears standing up, pale behind and dark round the inside edge.
            _ears[i] = Pivot(Head, "Ear", new Vector3(side * 0.065f, 0.075f, 0.02f));
            var tilt = Pivot(_ears[i], "Tilt", Vector3.Zero);
            tilt.Rotation = new Vector3(-0.05f, -side * 0.3f, -side * 0.2f);
            var flapAt = new Vector3(0, flap.Y * 0.8f, 0);
            Attach(tilt, "Flap", Ellipsoid(u => u * flap + new Vector3(0, 0, -0.012f * u.X * u.X), 14, 8, face, (p, n) =>
            {
                float edge = Mathf.Sqrt(p.X * p.X / (flap.X * flap.X) + p.Y * p.Y / (flap.Y * flap.Y));
                return n.Z > 0f ? Coat.Lerp(Dark, Mathf.SmoothStep(0.75f, 1f, edge) * 0.6f) : Belly.Lerp(Dark, Mathf.SmoothStep(0.45f, 0.85f, edge));
            }), flapAt);
        }
    }

    /// <summary>A short, bushy tail, hanging down, darkening along its length to a tuft of long black hair.</summary>
    private void BuildTail(RandomNumberGenerator rng, StandardMaterial3D coat, Func<Vector3, Vector3, Color> furColour)
    {
        Vector3[] path = [Vector3.Zero, new(0, -0.08f, 0.07f), new(0, -0.18f, 0.1f), new(0, -0.28f, 0.11f)];
        float[] radii = [0.03f, 0.025f, 0.022f, 0.02f];
        _tail = new Node3D[path.Length - 1];
        Node3D parent = Frame;
        for (int i = 0; i < _tail.Length; i++)
        {
            // The tail roots on the rump, which sits lower than the shoulders.
            var at = i == 0 ? new Vector3(0, BodyCentre + 0.1f - 0.48f * Mathf.Sin(BackSlope), 0.47f) : path[i] - path[i - 1];
            _tail[i] = Pivot(parent, "Tail", at);
            float darken = 0.15f + 0.25f * i;
            var end = path[i + 1] - path[i];
            Attach(_tail[i], "Joint", Ellipsoid(Vector3.One * radii[i], 8, 6, coat));
            Attach(_tail[i], "Segment", Tube([Vector3.Zero, end], [radii[i], radii[i + 1]], 8, coat, capEnd: false));
            // Bushy: the hairs stand out well from the thin tail.
            Attach(_tail[i], "Fur", Strands(rng, OnSegment(rng, 90, Vector3.Zero, end, radii[i], radii[i + 1]),
                end.Normalized() * 0.5f, 0.04f, 0.07f, FurRoot, FurTip, Hair, width: 0.016f, colouring: (p, n) => furColour(p, n).Lerp(Dark, darken)));
            parent = _tail[i];
        }
        var tuftAt = path[^1] - path[^2];
        Attach(_tail[^1], "Tuft", Strands(rng, OnShape(rng, u => u * 0.025f + tuftAt, 120, _ => true), Vector3.Down,
            0.06f, 0.1f, Dark, Dark * 1.4f, Hair, width: 0.018f));
    }

    /// <summary>A compact, dog-like paw on four toes, with short blunt claws that, unlike a cat's, never draw in.</summary>
    private void Paw(Node3D foot, Material coat)
    {
        var claw = new StandardMaterial3D { AlbedoColor = new Color(0.12f, 0.1f, 0.09f), Roughness = 0.5f };
        var paw = new Vector3(0.042f, 0.025f, 0.055f);
        var at = new Vector3(0, -FootHeight + paw.Y, -0.02f);
        Attach(foot, "Paw", Ellipsoid(paw, 12, 8, coat), at);
        for (int t = 0; t < 4; t++)
        {
            float x = (t - 1.5f) * 0.022f;
            var toe = at + new Vector3(x, -paw.Y * 0.3f, -paw.Z * (0.8f - 0.12f * Mathf.Abs(t - 1.5f)));
            Attach(foot, "Toe", Ellipsoid(new Vector3(0.015f, 0.015f, 0.018f), 8, 6, coat), toe);
            Attach(foot, "Claw", Ellipsoid(new Vector3(0.006f, 0.006f, 0.012f), 6, 4, claw), toe + new Vector3(0, -0.004f, -0.016f));
        }
    }

    /// <summary>
    /// A deep chest behind the shoulders tapering back to a narrow, tucked-up waist and smaller hindquarters, so the
    /// hyena looks all front.
    /// </summary>
    private static Vector3 BodyShape(Vector3 u)
    {
        float front = Mathf.SmoothStep(0.6f, -0.6f, u.Z);
        var p = new Vector3(u.X * 0.17f * (0.82f + 0.25f * front), u.Y * 0.22f * (0.8f + 0.3f * front), u.Z * 0.5f);
        if (u.Y < 0)
            p.Y *= 1f - 0.25f * Mathf.SmoothStep(-0.1f, 0.7f, u.Z);
        return p;
    }
}
