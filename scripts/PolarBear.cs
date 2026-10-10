using System;
using Godot;

namespace IceAgeWorld;

/// <summary>
/// A polar bear, the biggest land hunter, built entirely in code to a real male's proportions: about 1.3 m at the
/// shoulder and 2.4 m long, with a long, heavy body that rises to the rump, a long neck, a small, narrow head with a
/// black nose and lips, small round ears, and thick, pillar-like legs on huge, furry paws (snowshoes and paddles in
/// one) with black pads and short, curved claws. Its long, creamy-white coat hangs shaggy at the legs and belly. It
/// walks with a rolling, pigeon-toed gait, gallops, sits up on its haunches like a dog, lies flat like a sphinx, and
/// is a powerful swimmer. The model faces -Z.
/// </summary>
public partial class PolarBear : Quadruped
{
    private const int Seed = 71;

    private const float BodyCentre = 1.02f;

    /// <summary>
    /// How much bigger than a modern polar bear this one is drawn: an ice-age giant some four metres tall at the shoulder,
    /// bigger than a mammoth, as the great cave and short-faced bears of the ice age outsized every bear alive today.
    /// The model is built to a real bear's measurements and scaled up whole.
    /// </summary>
    private const float BodyScale = 3f;

    private static readonly Color Cream = new(0.97f, 0.92f, 0.79f);
    private static readonly Color Shade = new(0.86f, 0.79f, 0.64f);
    private static readonly Color FurRoot = new(0.9f, 0.86f, 0.76f);
    private static readonly Color FurTip = new(1.06f, 1.04f, 0.98f);
    private static readonly Vector3 CoatDrift = new(0, -0.3f, 0.8f);

    private Node3D[] _ears = [];

    public override string DisplayName => "Polar bear";
    public override string YoungName => "Polar bear cub";

    public override AnimalStats Stats { get; } = new()
    {
        // Polar bears can gallop at about 40 km/h in short bursts, and swim for days across open sea, paddling with
        // their huge forepaws.
        Scores = new() { JumpHeight = 4, JumpLength = 6, LandSpeed = 7, WaterSpeed = 8, Agility = 5, Stamina = 6 },
        Abilities = Ability.Hunt,
        // A swipe of the paw and a bite from a bear this size can bring down a mammoth.
        Strength = 8f,
        FloatDepth = 0.95f * BodyScale,
        WadeDepth = 0.6f * BodyScale,
        MouthDistance = 1.35f * BodyScale,
        EatReach = 0.7f * BodyScale,
        CanGraze = false,
        BodyRadius = 0.6f * BodyScale,
        BodyHeight = 1.5f * BodyScale,
        CameraHeight = 1.4f * BodyScale,
        CameraDistance = 6.5f * BodyScale,
    };

    protected override float FrontUpperLength => 0.42f;
    protected override float FrontLowerLength => 0.38f;
    protected override float BackUpperLength => 0.46f;
    protected override float BackLowerLength => 0.36f;
    protected override float FootHeight => 0.09f;
    protected override float ShoulderOffset => 0.6f;
    protected override float HipOffset => 0.62f;
    protected override float LegSpread => 0.21f;
    protected override float LieDrop => 0.52f;
    protected override float DeadFlank => 0.45f;
    protected override float DeadRoll => BodyCentre;
    // Long legs take long, slow strides: three times the size, the legs turn over well under twice as slowly.
    protected override float Cadence => 2.6f / Mathf.Sqrt(BodyScale);
    protected override bool SitsUp => true;
    protected override float SitPitch => 0.85f;
    protected override float HeadDown => -1.4f;

    protected override void AnimateExtras(float stride, float run, float dt)
    {
        for (int i = 0; i < 2; i++)
        {
            float twitch = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Clock * 0.5f + i * 2.9f)), 16f) * 0.4f * Alive;
            _ears[i].Rotation = new Vector3(twitch, 0, 0);
        }

        // The head swings low from side to side as the bear walks, the way polar bears plod.
        Head.Rotation += new Vector3(0, Mathf.Sin(WalkCycle) * 0.08f * stride * (1f - run), 0);
    }

    protected override void Build()
    {
        // Built to a real polar bear's measurements, then scaled up whole into an ice-age giant.
        Scale = Vector3.One * BodyScale;
        var rng = new RandomNumberGenerator { Seed = Seed };
        var coat = ProceduralTextures.Fur(Seed, Cream * 0.8f, Cream * 1.05f, 3f);
        var grain = ProceduralTextures.Colouring(coat);
        var black = new StandardMaterial3D { AlbedoColor = new Color(0.05f, 0.045f, 0.045f), Roughness = 0.4f };

        // A little yellower and greyer beneath and on the legs, where the coat gets dirty and wet.
        Func<Vector3, Vector3, Color> furColour = (p, n) => grain(p, n).Lerp(Shade, Mathf.SmoothStep(-0.2f, -0.8f, n.Y) * 0.5f);

        var body = Pivot(Frame, "Body", new Vector3(0, BodyCentre, 0));
        Attach(body, "Hide", Ellipsoid(BodyShape, 36, 22, coat));
        // The coat is dense and fairly smooth over the back, hanging longer and shaggier beneath.
        Attach(body, "Coat", Strands(rng, OnShape(rng, BodyShape, 10000, u => u.Y >= -0.4f), CoatDrift,
            0.035f, 0.055f, FurRoot, FurTip, Hair, width: 0.02f, colouring: furColour));
        Attach(body, "Belly", Strands(rng, OnShape(rng, BodyShape, 2800, u => u.Y < -0.4f), new Vector3(0, -1f, 0.3f),
            0.06f, 0.1f, FurRoot, FurTip, Hair, width: 0.022f, colouring: furColour));

        // A long neck, carried low and forward, thick with fur.
        Neck = Pivot(Frame, "Neck", new Vector3(0, BodyCentre + 0.05f, -0.82f));
        var headAt = new Vector3(0, 0.06f, -0.46f);
        var joint = new Vector3(0.25f, 0.27f, 0.25f);
        Attach(Neck, "Joint", Ellipsoid(joint, 14, 10, coat));
        Attach(Neck, "JointFur", Strands(rng, OnShape(rng, u => u * joint, 1000, _ => true), CoatDrift,
            0.035f, 0.06f, FurRoot, FurTip, Hair, width: 0.02f, colouring: furColour));
        Attach(Neck, "Throat", Tube([Vector3.Zero, headAt], [0.27f, 0.17f], 12, coat, capEnd: false));
        Attach(Neck, "NeckFur", Strands(rng, OnSegment(rng, 2000, Vector3.Zero, headAt, 0.27f, 0.17f), CoatDrift,
            0.035f, 0.06f, FurRoot, FurTip, Hair, width: 0.02f, colouring: furColour));
        Head = Pivot(Neck, "Head", headAt);
        BuildHead(rng, coat, furColour, black);

        // Thick legs, shaggy behind, on huge round paws.
        BuildLegs(rng, coat, furColour, coat, furColour, (0.13f, 0.1f, 0.085f), (0.15f, 0.1f, 0.085f),
            new Vector3(0.15f, 0.22f, 0.18f), 600, (0.035f, 0.065f), FurRoot, FurTip,
            (foot, _) => Paw(foot, rng, coat, furColour, black));

        // A short, stubby tail.
        var tail = Pivot(Frame, "Tail", new Vector3(0, BodyCentre + 0.12f, 0.93f));
        var stub = new Vector3(0.07f, 0.06f, 0.07f);
        Attach(tail, "Tail", Ellipsoid(stub, 10, 8, coat), new Vector3(0, 0, 0.03f));
        Attach(tail, "TailFur", Strands(rng, OnShape(rng, u => u * stub + new Vector3(0, 0, 0.03f), 120, _ => true), new Vector3(0, -0.6f, 0.6f),
            0.04f, 0.07f, FurRoot, FurTip, Hair, width: 0.025f, colouring: furColour));
    }

    /// <summary>
    /// A small, long head with a flat brow running straight down to the muzzle (a "Roman" profile), a black nose and
    /// lips, small dark eyes, and little round ears set well back.
    /// </summary>
    private void BuildHead(RandomNumberGenerator rng, StandardMaterial3D coat, Func<Vector3, Vector3, Color> furColour, Material black)
    {
        var skull = new Vector3(0.15f, 0.14f, 0.18f);
        Attach(Head, "Skull", Ellipsoid(skull, 20, 14, coat));

        // Short fur on the face, kept clear of the eyes so they show.
        Attach(Head, "SkullFur", Strands(rng, OnShape(rng, u => u * skull, 900, u => u.Z > -0.6f && !(Mathf.Abs(u.X) > 0.45f && u.Y > -0.2f && u.Z < 0.2f)),
            CoatDrift, 0.015f, 0.028f, FurRoot, FurTip, Hair, width: 0.013f, colouring: furColour));

        // The forehead runs straight down into the long muzzle with hardly a dip between: the "Roman nose".
        var bridgeAt = new Vector3(0, 0.015f, -0.13f);
        var bridge = new Vector3(0.11f, 0.095f, 0.14f);
        Attach(Head, "Bridge", Ellipsoid(bridge, 16, 12, coat), bridgeAt);
        Attach(Head, "BridgeFur", Strands(rng, OnShape(rng, u => u * bridge + bridgeAt, 500, u => u.Y > -0.3f && !(Mathf.Abs(u.X) > 0.6f && u.Y < 0.5f)),
            new Vector3(0, -0.1f, 0.9f), 0.012f, 0.022f, FurRoot, FurTip, Hair, width: 0.012f, colouring: furColour));

        Func<Vector3, Vector3> muzzle = u =>
        {
            float taper = Mathf.Lerp(1f, 0.74f, Mathf.Max(0f, -u.Z));
            return new Vector3(u.X * 0.092f * taper, u.Y * 0.08f * taper, u.Z * 0.19f);
        };
        var muzzleAt = new Vector3(0, -0.035f, -0.22f);
        Attach(Head, "Muzzle", Ellipsoid(muzzle, 18, 12, coat), muzzleAt);
        Attach(Head, "MuzzleFur", Strands(rng, OnShape(rng, u => muzzle(u) + muzzleAt, 400, u => u.Y > -0.3f && u.Z > -0.8f),
            new Vector3(0, 0, 1f), 0.01f, 0.017f, FurRoot, FurTip, Hair, width: 0.01f, colouring: furColour));

        // The big black nose caps the end of the muzzle, broad on top and set into it rather than stuck on.
        Func<Vector3, Vector3> nose = u => new Vector3(u.X * 0.05f * (0.8f + 0.25f * u.Y), u.Y * 0.032f, u.Z * 0.03f);
        Attach(Head, "Nose", Ellipsoid(nose, 14, 10, black), muzzleAt + new Vector3(0, 0.018f, -0.175f));

        // The jaw beneath, with the black lips showing as a line along each side of the mouth.
        Attach(Head, "Jaw", Ellipsoid(new Vector3(0.068f, 0.035f, 0.13f), 12, 8, coat), muzzleAt + new Vector3(0, -0.06f, 0.0f));
        foreach (float side in new[] { -1f, 1f })
        {
            var lip = Pivot(Head, "Lip", muzzleAt + new Vector3(side * 0.058f, -0.036f, -0.07f));
            lip.Rotation = new Vector3(0.12f, side * 0.18f, 0f);
            Attach(lip, "Line", Ellipsoid(new Vector3(0.006f, 0.008f, 0.085f), 8, 6, black));
        }

        _ears = new Node3D[2];
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;

            // Small dark eyes, set forward on the face and just proud of it, with a black rim of bare skin.
            Eye(Head, new Vector3(side * 0.095f, 0.045f, -0.13f), new Vector3(0, -side * 0.6f, 0), 0.016f, new Color(0.1f, 0.06f, 0.04f), 0.8f);

            // Small, round ears, set far back: less to lose heat through.
            _ears[i] = Pivot(Head, "Ear", new Vector3(side * 0.11f, 0.11f, 0.06f));
            var ear = new Vector3(0.045f, 0.042f, 0.02f);
            Attach(_ears[i], "Ear", Ellipsoid(ear, 12, 8, coat), new Vector3(0, 0.03f, 0));
            Attach(_ears[i], "EarFur", Strands(rng, OnShape(rng, u => u * ear + new Vector3(0, 0.03f, 0), 60, u => u.Z > -0.2f),
                new Vector3(0, 0.6f, 0.4f), 0.012f, 0.02f, FurRoot, FurTip, Hair, width: 0.012f, colouring: furColour));
        }
    }

    /// <summary>
    /// A huge, round paw, nearly 30 cm across, furred between the toes for grip on ice, with black pads beneath and
    /// five short, curved black claws in front.
    /// </summary>
    private void Paw(Node3D foot, RandomNumberGenerator rng, Material coat, Func<Vector3, Vector3, Color> furColour, Material black)
    {
        var paw = new Vector3(0.12f, 0.06f, 0.15f);
        var at = new Vector3(0, -FootHeight + paw.Y * 0.6f, -0.07f);
        Attach(foot, "Paw", Ellipsoid(paw, 14, 10, coat), at);
        Attach(foot, "PawFur", Strands(rng, OnShape(rng, u => u * paw + at, 220, u => u.Y > -0.3f), new Vector3(0, -0.5f, -0.3f),
            0.03f, 0.05f, FurRoot, FurTip, Hair, width: 0.022f, colouring: furColour));
        Attach(foot, "Pad", Ellipsoid(new Vector3(0.08f, 0.012f, 0.09f), 10, 6, black), at + new Vector3(0, -paw.Y * 0.75f, 0.01f));
        for (int t = 0; t < 5; t++)
        {
            float x = (t - 2) * 0.045f;
            var root = at + new Vector3(x, -0.01f, -paw.Z * 0.9f);
            Attach(foot, "Claw", Tube([root, root + new Vector3(0, -0.02f, -0.04f), root + new Vector3(0, -0.05f, -0.055f)],
                [0.012f, 0.009f, 0.003f], 6, black, capEnd: true));
        }
    }

    /// <summary>Long and heavy, rising from the shoulders to a higher rump, as a polar bear's back does.</summary>
    private static Vector3 BodyShape(Vector3 u)
    {
        var p = new Vector3(u.X * 0.4f, u.Y * 0.44f, u.Z * 0.95f);
        float rump = Mathf.SmoothStep(-0.3f, 0.5f, p.Z);
        p.X *= 0.92f + rump * 0.1f;
        if (u.Y > 0)
            p.Y *= 0.9f + rump * 0.2f;
        p.Y += rump * 0.05f;
        return p;
    }
}
