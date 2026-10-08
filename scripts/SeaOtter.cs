using System;
using Godot;

namespace IceAgeWorld;

/// <summary>
/// A sea otter, built entirely in code to a real otter's proportions: a long, thick, round body in dense dark-brown
/// fur, a small round head that is paler and grizzled, a blunt face with puffy whisker pads, a broad nose and small
/// dark eyes, tiny ears, short forelegs with small paws, big webbed hind flippers and a short, flat tail. On land it
/// humps along in a bounding gait. In the water it swims belly-down, kicking with both hind flippers together, and
/// when it stops it rolls over to float on its back with its paws on its chest and its feet in the air, as sea otters
/// do. The model faces -Z.
/// </summary>
public partial class SeaOtter : Animal
{
    private const int Seed = 31;

    /// <summary>Height of the middle of the body above the feet. The body rolls about this line to float on its back.</summary>
    private const float SpineHeight = 0.17f;

    /// <summary>Below this speed through the water the otter rolls over and floats on its back.</summary>
    private const float FloatSpeed = 1.2f;

    // Sitting, the otter rears up on its haunches with its forepaws held to its chest, its tail out behind on the
    // ground. The body tips back about its rump, so its middle rises.
    private const float SitPitch = 1.3f;
    private const float SitHeight = 0.38f;

    /// <summary>Lying, the otter rolls onto its back as it does to float, and its middle sinks to just clear the ground.</summary>
    private const float LieHeight = 0.14f;

    private static readonly Color FurRoot = new(0.75f, 0.75f, 0.75f);
    private static readonly Color FurTip = new(1.05f, 1.05f, 1.05f);
    private static readonly Color Grizzle = new(0.72f, 0.66f, 0.56f);
    private static readonly Vector3 CoatDrift = new(0, -0.1f, 0.9f);

    // Bounding: the forelegs land together, then the hind legs together.
    private static readonly float[] BoundPhase = [0f, 0.25f, Mathf.Pi, Mathf.Pi + 0.25f];

    // Leg order: front left, front right, back left, back right.
    private Node3D _frame = null!;
    private Node3D _neck = null!;
    private Node3D[] _legs = [];
    private Node3D[] _feet = [];
    private Node3D[] _tail = [];
    private ShaderMaterial _hair = null!;
    private float _walkCycle;
    private float _time;

    /// <summary>0 belly-down, 1 floating on its back.</summary>
    private float _onBack;

    public override string DisplayName => "Sea otter";

    public override AnimalStats Stats { get; } = new()
    {
        Scores = new() { JumpHeight = 3, JumpLength = 3, LandSpeed = 5, WaterSpeed = 9, Agility = 9, Stamina = 6 },
        FloatDepth = SpineHeight,
        WadeDepth = 0.12f,
        MouthDistance = 0.5f,
        EatReach = 0.4f,
        CanGraze = false,
        BodyRadius = 0.2f,
        BodyHeight = 0.45f,
        CameraHeight = 0.35f,
        CameraDistance = 2.5f,
    };

    public override void _Ready() => Build();

    public override void Animate(float speed, float stride, float eat, float dt)
    {
        _time += dt;

        // Swimming speed comes with a little extra for treading water; take it back off to see if the otter is idling.
        float swimSpeed = IsSwimming ? Mathf.Max(0f, speed - 2f) : 0f;
        bool floating = IsSwimming && swimSpeed < FloatSpeed;
        _onBack = Mathf.MoveToward(_onBack, floating ? 1f : 0f, dt * 1.5f);
        float back = Mathf.SmoothStep(0f, 1f, _onBack);

        if (IsSwimming)
            Swim(swimSpeed, back, dt);
        else
            Bound(speed, stride, eat, dt);

        _hair.SetShaderParameter("sway_amount", 0.004f + 0.008f * stride);
    }

    /// <summary>On land: a humping bound, the back arching as the hind feet come up behind the front.</summary>
    private void Bound(float speed, float stride, float eat, float dt)
    {
        _walkCycle += Mathf.Sqrt(speed) * 5f * dt;
        float arch = Mathf.Sin(_walkCycle) * stride;

        _frame.Position = Pose(new Vector3(0, SpineHeight + Mathf.Abs(Mathf.Sin(_walkCycle)) * 0.03f * stride, 0),
            new Vector3(0, SitHeight, 0), new Vector3(0, LieHeight, 0));
        _frame.Rotation = new Vector3(Pose(arch * 0.12f - eat * 0.15f, SitPitch, 0f), 0, Pose(0f, 0f, Mathf.Pi));

        for (int i = 0; i < 4; i++)
        {
            float phase = _walkCycle + BoundPhase[i];
            float swing = Mathf.Sin(phase) * 0.6f * stride;
            // Flippers stay flat on the ground, peeling up at the heel as the leg swings back.
            float peel = -swing + Mathf.Max(0f, -Mathf.Cos(phase)) * 0.4f * stride;

            // Sitting up, the forepaws are held curled against the chest and the hind legs reach down to the ground
            // a little in front, flippers flat. Lying on its back, it holds its paws and feet up as it does afloat.
            if (i < 2)
            {
                _legs[i].Rotation = new Vector3(Pose(swing, -0.5f, 1.3f), 0, 0);
                _feet[i].Rotation = new Vector3(Pose(peel, 0.8f, -0.8f), 0, 0);
            }
            else
            {
                _legs[i].Rotation = new Vector3(Pose(swing, 0.3f - SitPitch, -0.4f), 0, 0);
                _feet[i].Rotation = new Vector3(Pose(peel, -0.3f, 0.3f + Mathf.Sin(_time * 1.3f + i) * 0.1f), 0, 0);
            }
        }

        // Sitting up, it tips its head forward to look about; on its back, it tucks its chin in to lift its head.
        _neck.Rotation = new Vector3(Pose(eat * -0.7f + Mathf.Sin(_walkCycle * 2f) * 0.05f * stride, -SitPitch * 0.9f, -0.65f), 0, 0);
        Wag(0.15f, stride);

        // Sitting, the tail bends back to lie flat along the ground behind.
        _tail[0].Rotation += new Vector3(Pose(0f, -SitPitch, 0f), 0, 0);
    }

    /// <summary>In the water: belly-down kicking with both hind flippers when on the move, or rolled over and floating at rest.</summary>
    private void Swim(float swimSpeed, float back, float dt)
    {
        _walkCycle += (1.5f + swimSpeed * 1.2f) * dt;
        float kick = Mathf.Sin(_walkCycle * 2f);
        float drive = 1f - back;

        // Undulate through the water when swimming; rock gently on the swell when floating.
        float rock = Mathf.Sin(_time * 0.9f) * 0.05f;
        _frame.Position = new Vector3(0, SpineHeight, 0);
        _frame.Rotation = new Vector3(kick * 0.05f * drive + rock * back, 0, Mathf.Pi * back);

        for (int i = 0; i < 4; i++)
        {
            bool front = i < 2;
            if (front)
            {
                // Swimming, the forepaws tuck back against the chest; floating, they fold up onto it, near the chin.
                _legs[i].Rotation = new Vector3(Mathf.Lerp(-1.2f, 1.3f, back), 0, 0);
                _feet[i].Rotation = new Vector3(Mathf.Lerp(0f, -0.8f, back), 0, 0);
            }
            else
            {
                // Both hind flippers kick together, trailing straight back; floating, they poke up out of the water.
                _legs[i].Rotation = new Vector3(Mathf.Lerp(-1.1f + kick * 0.35f, -0.4f, back), 0, 0);
                _feet[i].Rotation = new Vector3(Mathf.Lerp(-0.3f + kick * 0.4f, 0.3f + Mathf.Sin(_time * 1.3f + i) * 0.1f, back), 0, 0);
            }
        }

        // On its back the otter tucks its chin in, which lifts its head clear of the water.
        _neck.Rotation = new Vector3(Mathf.Lerp(0.15f, -0.65f, back), 0, 0);
        Wag(Mathf.Lerp(0.3f, 0.05f, back), 1f);
    }

    /// <summary>The flat tail sculls up and down behind.</summary>
    private void Wag(float amount, float stride)
    {
        for (int i = 0; i < _tail.Length; i++)
            _tail[i].Rotation = new Vector3(Mathf.Sin(_walkCycle * 2f - i * 0.8f) * amount * stride + (i == 0 ? 0.1f : 0f), 0, 0);
    }

    private void Build()
    {
        var rng = new RandomNumberGenerator { Seed = Seed };
        var coat = ProceduralTextures.Fur(Seed, new Color(0.14f, 0.09f, 0.06f), new Color(0.32f, 0.22f, 0.15f), 4f);
        var head = ProceduralTextures.Fur(Seed + 1, new Color(0.62f, 0.56f, 0.47f), new Color(0.8f, 0.74f, 0.64f), 5f);
        var skin = new StandardMaterial3D { AlbedoColor = new Color(0.17f, 0.11f, 0.08f), Roughness = 0.7f };
        _hair = HairMaterial();

        var coatColour = ProceduralTextures.Colouring(coat);
        var headColour = ProceduralTextures.Colouring(head);

        // Everything hangs off a frame through the middle of the body, so it can roll over without leaving the water line.
        _frame = Pivot(this, "Frame", new Vector3(0, SpineHeight, 0));

        // Body: long and thick, broadest at the hips, with the head end paling into the grizzled neck.
        Attach(_frame, "Hide", Ellipsoid(BodyShape, 32, 18, coat));
        Func<Vector3, Vector3, Color> bodyColour = (p, n) => coatColour(p, n).Lerp(Grizzle, Mathf.SmoothStep(-0.2f, -0.38f, p.Z) * 0.8f);
        Attach(_frame, "Coat", Strands(rng, OnShape(rng, BodyShape, 6000, _ => true), CoatDrift,
            0.015f, 0.025f, FurRoot, FurTip, _hair, width: 0.018f, colouring: bodyColour));

        // Thick neck straight into a small round head; there is barely any neck to see.
        _neck = Pivot(_frame, "Neck", new Vector3(0, 0.03f, -0.33f));
        Attach(_neck, "Throat", Ellipsoid(new Vector3(0.1f, 0.09f, 0.1f), 18, 12, head), new Vector3(0, 0.01f, -0.04f));
        Attach(_neck, "ThroatFur", Strands(rng, OnShape(rng, u => u * new Vector3(0.1f, 0.09f, 0.1f) + new Vector3(0, 0.01f, -0.04f), 700, _ => true),
            CoatDrift, 0.012f, 0.02f, FurRoot, FurTip, _hair, width: 0.016f, colouring: headColour));
        var skullPivot = Pivot(_neck, "Head", new Vector3(0, 0.04f, -0.13f));
        BuildHead(skullPivot, rng, head, headColour, skin);

        // Short forelegs with small paws; short hind legs ending in big, flat, webbed flippers.
        _legs = new Node3D[4];
        _feet = new Node3D[4];
        string[] legNames = ["LegFrontLeft", "LegFrontRight", "LegBackLeft", "LegBackRight"];
        for (int i = 0; i < 4; i++)
        {
            bool front = i < 2;
            float side = i % 2 == 0 ? -1f : 1f;
            const float length = 0.125f;
            _legs[i] = Pivot(_frame, legNames[i], new Vector3(side * (front ? 0.09f : 0.1f), -0.025f, front ? -0.24f : 0.24f));
            Attach(_legs[i], "Leg", Tube([Vector3.Zero, Vector3.Down * length], [front ? 0.045f : 0.06f, 0.03f], 10, coat, capEnd: false));
            Attach(_legs[i], "LegFur", Strands(rng, OnSegment(rng, 200, Vector3.Zero, Vector3.Down * length, front ? 0.045f : 0.06f, 0.03f),
                CoatDrift, 0.01f, 0.018f, FurRoot, FurTip, _hair, width: 0.014f, colouring: coatColour));

            _feet[i] = Pivot(_legs[i], "Foot", Vector3.Down * length);
            if (front)
            {
                Attach(_feet[i], "Paw", Ellipsoid(new Vector3(0.03f, 0.018f, 0.035f), 12, 8, coat), new Vector3(0, 0, -0.015f));
            }
            else
            {
                // A wide flipper, fanning out to five webbed toes, pointing back the way it pushes.
                Func<Vector3, Vector3> flipper = u =>
                {
                    float spread = Mathf.Lerp(0.6f, 1.2f, (u.Z + 1f) * 0.5f);
                    return new Vector3(u.X * 0.045f * spread, u.Y * 0.012f, u.Z * 0.085f);
                };
                Attach(_feet[i], "Flipper", Ellipsoid(flipper, 16, 8, skin), new Vector3(0, -0.005f, 0.05f));
                Attach(_feet[i], "FlipperFur", Strands(rng, OnShape(rng, u => flipper(u) + new Vector3(0, -0.005f, 0.05f), 150, u => u.Y > 0.3f && u.Z < 0.4f),
                    CoatDrift, 0.008f, 0.014f, FurRoot, FurTip, _hair, width: 0.012f, colouring: coatColour));
            }
        }

        // Tail: short, thick at the root and flattened like a paddle.
        _tail = new Node3D[2];
        Node3D parent = _frame;
        var position = new Vector3(0, 0.02f, 0.4f);
        for (int i = 0; i < _tail.Length; i++)
        {
            _tail[i] = Pivot(parent, "Tail", position);
            var radii = new Vector3(Mathf.Lerp(0.055f, 0.045f, i), Mathf.Lerp(0.03f, 0.018f, i), 0.09f);
            Attach(_tail[i], "Paddle", Ellipsoid(radii, 14, 8, coat), new Vector3(0, 0, 0.07f));
            Attach(_tail[i], "PaddleFur", Strands(rng, OnShape(rng, u => u * radii + new Vector3(0, 0, 0.07f), 250, _ => true),
                new Vector3(0, 0, 1f), 0.012f, 0.02f, FurRoot, FurTip, _hair, width: 0.014f, colouring: coatColour));
            parent = _tail[i];
            position = new Vector3(0, 0, 0.14f);
        }
    }

    /// <summary>
    /// A small, round, blunt head: a broad, flat-topped skull, puffy pale whisker pads either side of a wide dark nose,
    /// long pale whiskers, small dark eyes set high and forward, and tiny ears almost hidden in the fur.
    /// </summary>
    private void BuildHead(Node3D head, RandomNumberGenerator rng, StandardMaterial3D fur, Func<Vector3, Vector3, Color> headColour,
        StandardMaterial3D skin)
    {
        var nose = new StandardMaterial3D { AlbedoColor = new Color(0.06f, 0.05f, 0.05f), Roughness = 0.35f };
        var pale = ProceduralTextures.Fur(Seed + 2, new Color(0.72f, 0.67f, 0.58f), new Color(0.9f, 0.86f, 0.78f), 6f);

        var skull = new Vector3(0.075f, 0.062f, 0.075f);
        Attach(head, "Skull", Ellipsoid(skull, 24, 16, fur));
        Attach(head, "SkullFur", Strands(rng, OnShape(rng, u => u * skull, 500, u => u.Z > -0.7f), CoatDrift,
            0.01f, 0.016f, FurRoot, FurTip, _hair, width: 0.012f, colouring: headColour));

        var nosePosition = new Vector3(0, -0.008f, -0.092f);
        Attach(head, "Nose", Ellipsoid(u => new Vector3(u.X * 0.021f * (0.8f + 0.25f * u.Y), u.Y * 0.012f, u.Z * 0.012f), 14, 10, nose), nosePosition);

        foreach (float side in new[] { -1f, 1f })
        {
            var pad = new Vector3(0.028f, 0.022f, 0.026f);
            var padPosition = new Vector3(side * 0.024f, -0.028f, -0.07f);
            Attach(head, "WhiskerPad", Ellipsoid(pad, 14, 10, pale), padPosition);
            Attach(head, "Whiskers", Strands(rng, OnShape(rng, u => u * pad + padPosition, 16, u => u.X * side > 0.4f && u.Z < 0.2f),
                new Vector3(side * 0.6f, -0.1f, 0.3f), 0.06f, 0.09f, Grizzle, Colors.White, _hair, width: 0.003f));

            Eye(head, new Vector3(side * 0.04f, 0.02f, -0.06f), new Vector3(0, -side * 0.45f, 0), 0.0085f, new Color(0.08f, 0.05f, 0.04f), 0.7f);

            // Tiny round ears, set low and far back on the sides of the head.
            Attach(head, "Ear", Ellipsoid(new Vector3(0.006f, 0.01f, 0.009f), 10, 6, fur), new Vector3(side * 0.066f, 0.026f, 0.025f));
        }
        Attach(head, "Chin", Ellipsoid(new Vector3(0.03f, 0.016f, 0.03f), 12, 8, pale), new Vector3(0, -0.045f, -0.05f));
    }

    /// <summary>Long and round, a little deeper at the hips, where the powerful hind legs that drive it through the water attach.</summary>
    private static Vector3 BodyShape(Vector3 u)
    {
        var p = new Vector3(u.X * 0.14f, u.Y * 0.12f, u.Z * 0.42f);
        float hips = Mathf.SmoothStep(-0.1f, 0.25f, p.Z);
        p.X *= 0.95f + hips * 0.12f;
        p.Y *= 0.95f + hips * 0.1f;
        return p;
    }
}
