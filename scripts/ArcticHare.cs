using Godot;

namespace IceAgeWorld;

/// <summary>
/// An arctic hare, built entirely in code to a real hare's proportions: a round body about half a metre long in a thick
/// pure-white winter coat, a high, rounded rump, a short fluffy tail, a blunt face with big side-set eyes for seeing
/// danger coming, ears a little shorter than other hares' (less to lose heat through) tipped in black, slim forelegs,
/// and long, broad hind feet that lie flat on the ground when it crouches and act as snowshoes. It moves in long
/// bounds, forefeet down together, then the hind feet together landing ahead of them. It is the wild animals' prey,
/// not one the player can be. The model faces -Z.
/// </summary>
public partial class ArcticHare : Animal
{
    private const int Seed = 53;

    /// <summary>Height of the middle of the body above the ground while crouched.</summary>
    private const float SpineHeight = 0.2f;

    /// <summary>Sitting up on its haunches to look about, the hare tips nose-up about its rump.</summary>
    private const float SitPitch = 0.8f;
    private static readonly Vector3 Rump = new(0f, -0.15f, 0.16f);

    /// <summary>Lying, it presses its belly flat to the snow.</summary>
    private const float LieHeight = 0.11f;

    private static readonly Color FurRoot = new(0.84f, 0.86f, 0.89f);
    private static readonly Color FurTip = new(1.05f, 1.05f, 1.06f);
    private static readonly Vector3 CoatDrift = new(0, -0.1f, 0.9f);

    private Node3D _frame = null!;
    private Node3D _neck = null!;
    private Node3D[] _ears = [];
    private Node3D[] _forelegs = [];
    private Node3D[] _thighs = [];
    private Node3D _tail = null!;
    private ShaderMaterial _hair = null!;
    private float _cycle;
    private float _time;

    public override string DisplayName => "Arctic hare";
    public override string YoungName => "Arctic hare leveret";

    public override AnimalStats Stats { get; } = new()
    {
        // Arctic hares can bound away at over 60 km/h, faster than a wolf, but only in short bursts: a wolf that keeps
        // after one can run it down once it tires.
        Scores = new() { JumpHeight = 6, JumpLength = 8, LandSpeed = 8, WaterSpeed = 2, Agility = 10, Stamina = 3 },
        FloatDepth = 0.15f,
        WadeDepth = 0.1f,
        MouthDistance = 0.25f,
        EatReach = 0.2f,
        CanGraze = true,
        BodyRadius = 0.15f,
        BodyHeight = 0.35f,
        CameraHeight = 0.3f,
        CameraDistance = 2f,
    };

    public override void _Ready() => Build();

    public override void Animate(float speed, float stride, float eat, float dt)
    {
        _time += dt;

        // A bound covers about a metre and a half, so the faster it goes the faster its legs work.
        _cycle += speed / 1.4f * Mathf.Tau * dt;
        float bound = Mathf.Sin(_cycle);
        float lift = Mathf.Abs(bound) * 0.07f * stride;

        // Crouched, it hunches with its rump high; bounding, its back arches and stretches with each leap. Sitting up,
        // it rears on its haunches about its rump; lying, it flattens down.
        float pitch = Pose(Mathf.Cos(_cycle) * 0.15f * stride - eat * 0.1f, SitPitch, 0f);
        var standing = new Vector3(0, SpineHeight + lift, 0);
        _frame.Position = Pose(standing, new Vector3(0, SpineHeight, 0) + TipAbout(Rump, SitPitch), new Vector3(0, LieHeight, 0));
        _frame.Rotation = new Vector3(pitch, 0, 0);

        // The forelegs reach out together, then the hind legs swing through together, half a bound later. Sitting up,
        // the forelegs hang straight down and the hind legs stay flat on the ground; lying, the forepaws tuck forward
        // and the hind feet fold in under the haunches.
        for (int i = 0; i < 2; i++)
        {
            float reach = bound * 0.7f * stride;
            _forelegs[i].Rotation = new Vector3(Pose(reach, -SitPitch, 1.3f), 0, 0);
            float drive = -bound * 0.8f * stride;
            _thighs[i].Rotation = new Vector3(Pose(drive, -SitPitch, 0.1f), 0, 0);
        }

        // The head stays level, dipping to nibble. Sitting, the hare looks about; lying, it rests its chin down.
        float look = Mathf.Sin(_time * 0.7f) * 0.25f * Mathf.SmoothStep(0f, 1f, Sitting) * Alive;
        _neck.Rotation = new Vector3(Pose(-pitch * 0.7f - eat * 0.7f, -SitPitch * 0.9f, -0.15f), look, 0);

        // Ears stand up and twitch at rest, swept back flat when running, and laid along the back lying down.
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            float twitch = Mathf.Max(0f, Mathf.Sin(_time * 1.3f + i * 2.1f) - 0.9f) * 2f * Alive;
            float back = Pose(0.25f + stride * 0.9f + twitch, 0.15f, 1.2f);
            _ears[i].Rotation = new Vector3(back, 0, -side * Pose(0.2f, 0.15f, 0.1f));
        }

        _tail.Rotation = new Vector3(Pose(-0.3f + bound * 0.2f * stride, 0.5f, 0f), 0, 0);
        GoLimp();
        _hair.SetShaderParameter("sway_amount", (0.004f + 0.006f * stride) * Alive);
    }

    /// <summary>Dead, the hare flops onto its side, legs stretched out and ears lying flat.</summary>
    private void GoLimp()
    {
        if (Dead <= 0f)
            return;

        _frame.Rotation = Limp(_frame.Rotation, new Vector3(0f, 0f, 1.45f));
        _frame.Position = Limp(_frame.Position, new Vector3(0f, 0.11f, 0f));
        for (int i = 0; i < 2; i++)
        {
            _forelegs[i].Rotation = Limp(_forelegs[i].Rotation, new Vector3(0.7f, 0f, 0f));
            _thighs[i].Rotation = Limp(_thighs[i].Rotation, new Vector3(-0.7f, 0f, 0f));
            _ears[i].Rotation = Limp(_ears[i].Rotation, new Vector3(1.4f, 0f, 0f));
        }
        _neck.Rotation = Limp(_neck.Rotation, new Vector3(0.2f, 0f, 0f));
    }

    private void Build()
    {
        var rng = new RandomNumberGenerator { Seed = Seed };
        var coat = ProceduralTextures.Fur(Seed, new Color(0.82f, 0.84f, 0.87f), new Color(0.97f, 0.98f, 1f), 5f);
        var black = new StandardMaterial3D { AlbedoColor = new Color(0.07f, 0.06f, 0.06f), Roughness = 0.8f };
        var nose = new StandardMaterial3D { AlbedoColor = new Color(0.6f, 0.47f, 0.47f), Roughness = 0.5f };
        _hair = HairMaterial();
        var coatColour = ProceduralTextures.Colouring(coat);

        _frame = Pivot(this, "Frame", new Vector3(0, SpineHeight, 0));

        // Body: round and compact, the rump higher and fuller than the shoulders, as a crouched hare's is.
        Attach(_frame, "Hide", Ellipsoid(BodyShape, 28, 16, coat));
        Attach(_frame, "Coat", Strands(rng, OnShape(rng, BodyShape, 3000, _ => true), CoatDrift,
            0.018f, 0.03f, FurRoot, FurTip, _hair, width: 0.016f, colouring: coatColour));

        // A short neck into a blunt, rounded head.
        _neck = Pivot(_frame, "Neck", new Vector3(0, 0.06f, -0.16f));
        var throat = new Vector3(0.05f, 0.055f, 0.065f);
        Attach(_neck, "Throat", Ellipsoid(throat, 16, 10, coat), new Vector3(0, 0.02f, -0.02f));
        Attach(_neck, "ThroatFur", Strands(rng, OnShape(rng, u => u * throat + new Vector3(0, 0.02f, -0.02f), 250, _ => true), CoatDrift,
            0.014f, 0.024f, FurRoot, FurTip, _hair, width: 0.014f, colouring: coatColour));
        var head = Pivot(_neck, "Head", new Vector3(0, 0.05f, -0.06f));
        var skull = new Vector3(0.055f, 0.058f, 0.072f);
        Attach(head, "Skull", Ellipsoid(skull, 20, 14, coat), new Vector3(0, 0, -0.02f));
        Attach(head, "SkullFur", Strands(rng, OnShape(rng, u => u * skull + new Vector3(0, 0, -0.02f), 350, u => u.Z > -0.8f), CoatDrift,
            0.01f, 0.016f, FurRoot, FurTip, _hair, width: 0.012f, colouring: coatColour));
        Attach(head, "Muzzle", Ellipsoid(new Vector3(0.036f, 0.03f, 0.035f), 14, 10, coat), new Vector3(0, -0.018f, -0.085f));
        Attach(head, "Nose", Ellipsoid(new Vector3(0.012f, 0.008f, 0.006f), 10, 6, nose), new Vector3(0, -0.006f, -0.118f));
        Attach(head, "Whiskers", Strands(rng, OnShape(rng, u => u * new Vector3(0.036f, 0.03f, 0.035f) + new Vector3(0, -0.018f, -0.085f), 20,
            u => Mathf.Abs(u.X) > 0.5f && u.Z < 0f), new Vector3(0, -0.1f, 0.2f), 0.05f, 0.07f, FurRoot, Colors.White, _hair, width: 0.002f));

        _ears = new Node3D[2];
        foreach (float side in new[] { -1f, 1f })
        {
            // Big dark eyes set on the sides of the head, so the hare sees almost all the way round.
            Eye(head, new Vector3(side * 0.043f, 0.018f, -0.045f), new Vector3(0, -side * 1.05f, 0), 0.013f, new Color(0.45f, 0.28f, 0.1f), 0.65f);

            var ear = Pivot(head, side < 0 ? "EarLeft" : "EarRight", new Vector3(side * 0.022f, 0.045f, 0.005f));
            var flap = new Vector3(0.022f, 0.06f, 0.009f);
            Attach(ear, "Ear", Ellipsoid(flap, 14, 10, coat), new Vector3(0, 0.055f, 0));
            Attach(ear, "EarFur", Strands(rng, OnShape(rng, u => u * flap + new Vector3(0, 0.055f, 0), 80, u => u.Z > 0f), new Vector3(0, 0.5f, 0.3f),
                0.006f, 0.01f, FurRoot, FurTip, _hair, width: 0.008f, colouring: coatColour));
            Attach(ear, "Tip", Ellipsoid(new Vector3(0.015f, 0.018f, 0.01f), 10, 6, black), new Vector3(0, 0.104f, 0));
            _ears[side < 0 ? 0 : 1] = ear;
        }

        // Slim forelegs under the chest.
        _forelegs = new Node3D[2];
        _thighs = new Node3D[2];
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            const float foreLength = 0.14f;
            _forelegs[i] = Pivot(_frame, side < 0 ? "ForelegLeft" : "ForelegRight", new Vector3(side * 0.05f, -0.06f, -0.11f));
            Attach(_forelegs[i], "Leg", Tube([Vector3.Zero, Vector3.Down * foreLength], [0.024f, 0.015f], 8, coat, capEnd: false));
            Attach(_forelegs[i], "LegFur", Strands(rng, OnSegment(rng, 80, Vector3.Zero, Vector3.Down * foreLength, 0.024f, 0.015f),
                CoatDrift, 0.008f, 0.014f, FurRoot, FurTip, _hair, width: 0.01f, colouring: coatColour));
            Attach(_forelegs[i], "Paw", Ellipsoid(new Vector3(0.018f, 0.011f, 0.028f), 10, 6, coat), new Vector3(0, -foreLength, -0.012f));

            // Big haunches, and the long hind feet lying flat along the ground beneath them, heel at the back.
            _thighs[i] = Pivot(_frame, side < 0 ? "HindLeft" : "HindRight", new Vector3(side * 0.075f, -0.03f, 0.1f));
            Attach(_thighs[i], "Haunch", Ellipsoid(new Vector3(0.045f, 0.075f, 0.075f), 14, 10, coat), new Vector3(0, -0.01f, 0.01f));
            Attach(_thighs[i], "HaunchFur", Strands(rng, OnShape(rng, u => u * new Vector3(0.045f, 0.075f, 0.075f) + new Vector3(0, -0.01f, 0.01f), 300,
                u => u.X * side > -0.2f), CoatDrift, 0.016f, 0.026f, FurRoot, FurTip, _hair, width: 0.015f, colouring: coatColour));
            var heel = new Vector3(0, -SpineHeight + 0.03f + 0.012f, 0.07f);
            Attach(_thighs[i], "Shank", Tube([new Vector3(0, -0.04f, 0.03f), heel], [0.028f, 0.016f], 8, coat, capEnd: false));
            var foot = new Vector3(0.024f, 0.012f, 0.08f);
            Attach(_thighs[i], "Foot", Ellipsoid(foot, 12, 8, coat), heel + new Vector3(0, -0.004f, -0.07f));
            Attach(_thighs[i], "FootFur", Strands(rng, OnShape(rng, u => u * foot + heel + new Vector3(0, -0.004f, -0.07f), 90, u => u.Y > -0.2f),
                new Vector3(0, 0.1f, -0.6f), 0.01f, 0.02f, FurRoot, FurTip, _hair, width: 0.012f, colouring: coatColour));
        }

        // A short, round, fluffy tail.
        _tail = Pivot(_frame, "Tail", new Vector3(0, 0.04f, 0.21f));
        var puff = new Vector3(0.032f, 0.032f, 0.028f);
        Attach(_tail, "Puff", Ellipsoid(puff, 12, 8, coat), new Vector3(0, 0, 0.015f));
        Attach(_tail, "PuffFur", Strands(rng, OnShape(rng, u => u * puff + new Vector3(0, 0, 0.015f), 120, _ => true), new Vector3(0, 0.2f, 0.6f),
            0.012f, 0.02f, FurRoot, FurTip, _hair, width: 0.012f, colouring: coatColour));
    }

    /// <summary>Round and compact, fuller and higher at the rump, where the big hind legs that power its bounds attach.</summary>
    private static Vector3 BodyShape(Vector3 u)
    {
        var p = new Vector3(u.X * 0.095f, u.Y * 0.1f, u.Z * 0.2f);
        float rump = Mathf.SmoothStep(-0.1f, 0.15f, p.Z);
        p.X *= 0.9f + rump * 0.2f;
        p.Y *= 0.9f + rump * 0.25f;
        p.Y += rump * 0.02f;
        return p;
    }
}
