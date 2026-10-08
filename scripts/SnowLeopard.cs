using System.Collections.Generic;
using Godot;

namespace IceAgeWorld;

/// <summary>
/// A snow leopard, built entirely in code: a long low body, a small round head with a short muzzle, rounded
/// ears, two-jointed legs on big furry paws and a very long, thick tail, dressed in a pale coat with dark
/// rosettes and a fringe of soft belly fur. It animates its own walk, which breaks into a bounding gallop at
/// speed, crouches on its forelegs to drink, swings its tail and twitches its ears. The model faces -Z.
/// </summary>
public partial class SnowLeopard : Animal
{
    private const int Seed = 11;
    private const int TailSegments = 7;
    private const float TailSegmentLength = 0.15f;
    private const float HeadDownAngle = -1.3f;

    // How far the body sinks and tips forward while drinking, with the forelegs folding to keep the paws planted.
    private const float CrouchDrop = 0.1f;
    private const float CrouchPitch = 0.18f;

    /// <summary>Distance from the body's centre to the shoulders and hips along the spine.</summary>
    private const float LegOffset = 0.36f;

    private const float FrontUpperLength = 0.25f;
    private const float FrontLowerLength = 0.26f;
    private const float BackUpperLength = 0.33f;
    private const float BackLowerLength = 0.22f;

    private static readonly Color FurRoot = new(0.5f, 0.49f, 0.46f);
    private static readonly Color FurTip = new(0.74f, 0.73f, 0.69f);
    /// <summary>How much bigger the whole head (skull, face, ears and fur) is drawn than it is modelled.</summary>
    private const float HeadScale = 1.3f;
    /// <summary>The coat lies back along the body, from head to tail, but stands up enough to look thick and fluffy.</summary>
    private static readonly Vector3 CoatDrift = new(0, -0.1f, 0.4f);

    // Rest pose of each tail segment, relative to the one above: out behind the rump, down towards the
    // ground, then curling back up at the tip.
    private static readonly float[] TailRest = [-1.2f, 0.2f, 0.2f, 0.2f, -0.35f, -0.35f, -0.35f];

    // Walking moves diagonal pairs of legs together; galloping bounds with the front pair, then the back pair.
    private static readonly float[] WalkPhase = [0f, Mathf.Pi, Mathf.Pi, 0f];
    private static readonly float[] GallopPhase = [0f, 0.4f, Mathf.Pi, Mathf.Pi + 0.4f];

    // Leg order: front left, front right, back left, back right.
    private Node3D _frame = null!;
    private Node3D _neck = null!;
    private Node3D _head = null!;
    private Node3D[] _upperLegs = [];
    private Node3D[] _lowerLegs = [];
    private Node3D[] _tail = [];
    private Node3D[] _ears = [];
    private ShaderMaterial _hair = null!;
    private float _walkCycle;
    private float _time;

    public override string DisplayName => "Snow leopard";

    public override AnimalStats Stats { get; } = new()
    {
        WalkSpeed = 4.5f,
        SprintSpeed = 15f,
        SwimSpeed = 2.5f,
        JumpVelocity = 7.5f,
        FloatDepth = 0.5f,
        WadeDepth = 0.3f,
        MouthDistance = 0.8f,
        EatReach = 0.5f,
        CanGraze = false,
        BodyRadius = 0.3f,
        BodyHeight = 0.9f,
        CameraHeight = 0.9f,
        CameraDistance = 4f,
    };

    public override void _Ready() => Build();

    public override void Animate(float speed, float stride, float eat, float dt)
    {
        _time += dt;

        // Short legs step quickly, but the cadence rises more slowly than speed so the gallop doesn't blur.
        _walkCycle += Mathf.Sqrt(speed) * 4.4f * dt;
        float run = Mathf.Clamp((speed - Stats.WalkSpeed) / (Stats.SprintSpeed - Stats.WalkSpeed), 0f, 1f);

        // Crouch to drink: the body sinks and tips forward onto bent forelegs.
        float drop = eat * CrouchDrop;
        float pitch = eat * CrouchPitch;
        float bob = Mathf.Abs(Mathf.Sin(_walkCycle)) * stride * Mathf.Lerp(0.02f, 0.07f, run);
        _frame.Position = new Vector3(0, bob - drop, 0);
        _frame.Rotation = new Vector3(-pitch + Mathf.Sin(_walkCycle) * run * 0.06f, 0, 0);

        float amplitude = Mathf.Lerp(0.4f, 0.85f, run) * stride;
        float frontFold = FoldAngle(drop + LegOffset * Mathf.Sin(pitch), FrontUpperLength + FrontLowerLength);
        float backFold = FoldAngle(drop - LegOffset * Mathf.Sin(pitch), BackUpperLength + BackLowerLength);

        for (int i = 0; i < 4; i++)
        {
            bool front = i < 2;
            float phase = _walkCycle + Mathf.Lerp(WalkPhase[i], GallopPhase[i], run);
            float swing = Mathf.Sin(phase) * amplitude;

            // Lift the paw while the leg swings forward.
            float lift = Mathf.Max(0f, Mathf.Cos(phase)) * stride * Mathf.Lerp(0.8f, 1.3f, run);

            // Forelegs fold with the elbow behind, hind legs with the knee in front. Both are corrected for
            // the body's forward tip so the paws stay under the shoulders and hips.
            if (front)
            {
                _upperLegs[i].Rotation = new Vector3(swing - frontFold + pitch, 0, 0);
                _lowerLegs[i].Rotation = new Vector3(-lift + frontFold * 2f, 0, 0);
            }
            else
            {
                _upperLegs[i].Rotation = new Vector3(swing + backFold + pitch, 0, 0);
                _lowerLegs[i].Rotation = new Vector3(lift * 0.7f - backFold * 2f, 0, 0);
            }
        }

        // The neck lowers to drink and nods slightly in step, while the head tilts back up so the chin,
        // not the forehead, meets the water.
        _neck.Rotation = new Vector3(eat * HeadDownAngle + Mathf.Sin(_walkCycle * 2f) * 0.04f * stride, 0, 0);
        _head.Rotation = new Vector3(-eat * HeadDownAngle * 0.5f, 0, 0);

        // The tail sways slowly, each segment lagging the one above; it streams out straighter at a run.
        for (int i = 0; i < _tail.Length; i++)
        {
            float lag = i * 0.5f;
            float sway = Mathf.Sin(_time * 1.1f - lag) * 0.12f + Mathf.Sin(_walkCycle * 0.5f - lag) * 0.1f * stride;
            float lift = i == 0 ? -run * 0.3f : -TailRest[i] * run * 0.7f;
            _tail[i].Rotation = new Vector3(TailRest[i] + lift + Mathf.Sin(_time * 0.7f - lag) * 0.05f, 0, sway);
        }

        // Ears flick now and then.
        float twitch = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(_time * 0.8f)), 12f) * 0.4f;
        _ears[0].Rotation = new Vector3(0, 0, twitch);
        _ears[1].Rotation = new Vector3(0, 0, -Mathf.Pow(Mathf.Max(0f, Mathf.Sin(_time * 0.8f + 2f)), 12f) * 0.4f);

        _hair.SetShaderParameter("sway_amount", 0.008f + 0.02f * stride);
    }

    /// <summary>
    /// How far each joint of a two-segment leg of the given length must bend for the leg to reach
    /// <paramref name="shorten"/> less far, with the foot staying under the hip.
    /// </summary>
    private static float FoldAngle(float shorten, float length) =>
        Mathf.Acos(Mathf.Clamp(1f - Mathf.Max(0f, shorten) / length, -1f, 1f));

    private void Build()
    {
        var rng = new RandomNumberGenerator { Seed = Seed };
        var coat = ProceduralTextures.SpottedFur(Seed,
            dark: new Color(0.58f, 0.58f, 0.55f), light: new Color(0.85f, 0.84f, 0.79f),
            centre: new Color(0.6f, 0.53f, 0.42f), spot: new Color(0.16f, 0.15f, 0.15f), scale: 2.6f);
        var nose = new StandardMaterial3D { AlbedoColor = new Color(0.42f, 0.3f, 0.3f), Roughness = 0.5f };
        var eye = new StandardMaterial3D { AlbedoColor = new Color(0.62f, 0.7f, 0.55f), Roughness = 0.15f };
        var pupil = new StandardMaterial3D { AlbedoColor = new Color(0.03f, 0.03f, 0.03f), Roughness = 0.1f };
        _hair = HairMaterial();

        // Everything hangs off a frame that can bob, sink and tip without disturbing the yaw Player sets.
        _frame = Pivot(this, "Frame", Vector3.Zero);

        // Body: long and low, deeper at the chest, with soft pale fur all over that hangs longer along the belly.
        var body = Pivot(_frame, "Body", new Vector3(0, 0.6f, 0));
        Attach(body, "Hide", Ellipsoid(BodyShape, 36, 20, coat));
        Attach(body, "Coat", Strands(rng, OnShape(rng, BodyShape, 5000, u => u.Y >= -0.35f), CoatDrift,
            0.035f, 0.065f, FurRoot, FurTip, _hair, width: 0.026f));
        Attach(body, "Belly", Strands(rng, OnShape(rng, BodyShape, 2000, u => u.Y < -0.35f), new Vector3(0, -1f, 0),
            0.05f, 0.085f, FurRoot, FurTip, _hair, width: 0.026f));
        Attach(body, "Ruff", Strands(rng, OnShape(rng, BodyShape, 500, u => u.Z < -0.6f && u.Y < 0.3f), new Vector3(0, -0.6f, -0.3f),
            0.05f, 0.08f, FurRoot, FurTip, _hair, width: 0.026f));

        // Head on a short neck that bends at the shoulders to drink.
        _neck = Pivot(_frame, "Neck", new Vector3(0, 0.68f, -0.42f));
        var headPosition = new Vector3(0, 0.1f, -0.28f);
        Attach(_neck, "Joint", new SphereMesh { Radius = 0.135f, Height = 0.27f, Material = coat });
        Attach(_neck, "Throat", Tube([Vector3.Zero, headPosition], [0.14f, 0.09f], 12, coat, capEnd: false));
        Attach(_neck, "ThroatFur", Strands(rng, OnSegment(rng, 700, Vector3.Zero, headPosition, 0.14f, 0.09f), CoatDrift,
            0.035f, 0.065f, FurRoot, FurTip, _hair, width: 0.026f));
        var head = _head = Pivot(_neck, "Head", headPosition);
        head.Scale = Vector3.One * HeadScale;
        Attach(head, "Skull", Ellipsoid(new Vector3(0.11f, 0.095f, 0.11f), 24, 16, coat));
        Attach(head, "SkullFur", Strands(rng, OnShape(rng, u => u * new Vector3(0.11f, 0.095f, 0.11f), 500, u => u.Z > -0.7f), CoatDrift,
            0.018f, 0.032f, FurRoot, FurTip, _hair, width: 0.016f));
        Attach(head, "Cheeks", Ellipsoid(new Vector3(0.12f, 0.06f, 0.07f), 20, 12, coat), new Vector3(0, -0.035f, -0.03f));
        Attach(head, "Muzzle", Ellipsoid(new Vector3(0.055f, 0.045f, 0.06f), 16, 12, coat), new Vector3(0, -0.035f, -0.1f));
        Attach(head, "Nose", Ellipsoid(new Vector3(0.022f, 0.014f, 0.012f), 10, 8, nose), new Vector3(0, -0.005f, -0.157f));
        Attach(head, "Whiskers", Strands(rng, OnShape(rng, u => u * new Vector3(0.055f, 0.045f, 0.06f) + new Vector3(0, -0.035f, -0.1f), 30,
            u => Mathf.Abs(u.X) > 0.6f && u.Z < 0f), Vector3.Zero, 0.07f, 0.1f, FurTip, FurTip, _hair, width: 0.004f));

        foreach (float side in new[] { -1f, 1f })
        {
            var eyePosition = new Vector3(side * 0.05f, 0.025f, -0.088f);
            Attach(head, "Eye", new SphereMesh { Radius = 0.019f, Height = 0.038f, Material = eye }, eyePosition);
            Attach(head, "Pupil", new SphereMesh { Radius = 0.009f, Height = 0.018f, Material = pupil },
                eyePosition + new Vector3(side * 0.004f, 0, -0.013f));
        }

        // Small round ears set wide on the head.
        _ears = new Node3D[2];
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            _ears[i] = Pivot(head, "Ear", new Vector3(side * 0.065f, 0.07f, 0.02f));
            Attach(_ears[i], "Flap", Ellipsoid(new Vector3(0.035f, 0.04f, 0.012f), 12, 8, coat), new Vector3(side * 0.01f, 0.03f, 0));
        }

        // Legs: an upper and lower segment each, ending in a broad paw. The forelegs are straight columns;
        // the hind legs have a big thigh and a hock that angles back.
        _upperLegs = new Node3D[4];
        _lowerLegs = new Node3D[4];
        string[] legNames = ["LegFrontLeft", "LegFrontRight", "LegBackLeft", "LegBackRight"];
        for (int i = 0; i < 4; i++)
        {
            bool front = i < 2;
            float side = i % 2 == 0 ? -1f : 1f;
            float upperLength = front ? FrontUpperLength : BackUpperLength;
            float lowerLength = front ? FrontLowerLength : BackLowerLength;
            var knee = front ? new Vector3(0, -upperLength, 0.01f) : new Vector3(0, -upperLength, 0.07f);

            float hipHeight = upperLength + lowerLength + 0.04f;
            _upperLegs[i] = Pivot(_frame, legNames[i], new Vector3(side * 0.11f, hipHeight, front ? -LegOffset : LegOffset));
            Attach(_upperLegs[i], "Upper", Tube([new Vector3(0, 0.05f, 0), knee],
                front ? [0.08f, 0.055f] : [0.11f, 0.055f], 12, coat, capEnd: false));
            Attach(_upperLegs[i], "UpperFur", Strands(rng, OnSegment(rng, 400, new Vector3(0, 0.05f, 0), knee,
                front ? 0.08f : 0.11f, 0.055f), new Vector3(0, -1f, 0), 0.03f, 0.055f, FurRoot, FurTip, _hair, width: 0.024f));

            _lowerLegs[i] = Pivot(_upperLegs[i], "Lower", knee);
            Attach(_lowerLegs[i], "Joint", new SphereMesh { Radius = 0.055f, Height = 0.11f, Material = coat });
            var ankle = new Vector3(0, -lowerLength, front ? -0.01f : -0.07f);
            Attach(_lowerLegs[i], "Lower", Tube([Vector3.Zero, ankle], [0.05f, 0.042f], 12, coat, capEnd: false));
            Attach(_lowerLegs[i], "LowerFur", Strands(rng, OnSegment(rng, 250, Vector3.Zero, ankle, 0.05f, 0.042f),
                new Vector3(0, -1f, 0), 0.025f, 0.045f, FurRoot, FurTip, _hair, width: 0.02f));
            Attach(_lowerLegs[i], "Paw", Ellipsoid(new Vector3(0.06f, 0.04f, 0.075f), 14, 10, coat), ankle + new Vector3(0, 0, -0.02f));
        }

        // Tail: as long as the body and almost as thick as a forearm, a chain of segments rooted on the rump
        // so it can sway and curl, with a fluff of fur along its length.
        _tail = new Node3D[TailSegments];
        Node3D parent = _frame;
        var position = new Vector3(0, 0.68f, 0.48f);
        for (int i = 0; i < TailSegments; i++)
        {
            float top = Mathf.Lerp(0.07f, 0.06f, i / (float)TailSegments);
            float bottom = Mathf.Lerp(0.07f, 0.06f, (i + 1) / (float)TailSegments);
            _tail[i] = Pivot(parent, "Tail", position);
            Attach(_tail[i], "Joint", new SphereMesh { Radius = top, Height = top * 2f, Material = coat });
            Attach(_tail[i], "Segment", Tube(
                [Vector3.Zero, Vector3.Down * TailSegmentLength], [top, bottom], 10, coat, capEnd: i == TailSegments - 1));
            Attach(_tail[i], "Fluff", Strands(rng, OnTube(rng, 110, top * 0.9f, 0f, -TailSegmentLength), new Vector3(0, -0.6f, 0),
                0.045f, 0.08f, FurRoot, FurTip, _hair, width: 0.024f));
            parent = _tail[i];
            position = Vector3.Down * TailSegmentLength;
        }
    }

    /// <summary>Long low body, a little deeper through the chest than the flanks.</summary>
    private static Vector3 BodyShape(Vector3 u)
    {
        var p = new Vector3(u.X * 0.19f, u.Y * 0.19f, u.Z * 0.52f);
        float chest = Mathf.SmoothStep(0.3f, -0.4f, p.Z);
        p.X *= 1f + chest * 0.12f;
        if (u.Y < 0)
            p.Y *= 1f + chest * 0.25f;
        return p;
    }
}
