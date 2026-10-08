using System;
using Godot;

namespace IceAgeWorld;

/// <summary>
/// A snow leopard, built entirely in code to a real cat's proportions: a long, low, stocky body that is deep at the
/// chest and high at the rump, short thick legs on broad paws, a small rounded head with a short broad muzzle, pale
/// almond eyes set into the face, small rounded ears and a tail nearly as long as the body. Its thick fur carries the
/// coat's pattern: broken rosettes on the body, solid spots on the head and legs, rings towards the end of the tail
/// and a dark tip, fading to cream underneath. It animates its own walk, which breaks into a bounding gallop at
/// speed, crouches on its forelegs to drink, swings its tail and twitches its ears. The model faces -Z.
/// </summary>
public partial class SnowLeopard : Animal
{
    private const int Seed = 11;
    private const int TailSegments = 7;
    private const float TailSegmentLength = 0.13f;
    private const float HeadDownAngle = -1.3f;

    /// <summary>How much bigger the whole head (skull, face, ears and fur) is drawn than it is modelled.</summary>
    private const float HeadScale = 1.15f;

    // How far the body sinks and tips forward while drinking, with the forelegs folding to keep the paws planted.
    private const float CrouchDrop = 0.1f;
    private const float CrouchPitch = 0.18f;

    /// <summary>Distance from the body's centre to the shoulders and hips along the spine.</summary>
    private const float LegOffset = 0.33f;

    private const float FrontUpperLength = 0.2f;
    private const float FrontLowerLength = 0.2f;
    private const float BackUpperLength = 0.24f;
    private const float BackLowerLength = 0.17f;

    // Hair shades from a little darker than the hide at the root to the hide's own colour at the tip.
    private static readonly Color FurRoot = new(0.8f, 0.8f, 0.8f);
    private static readonly Color FurTip = new(0.98f, 0.98f, 0.98f);
    private static readonly Color Cream = new(0.93f, 0.92f, 0.88f);
    private static readonly Color Spot = new(0.17f, 0.16f, 0.15f);

    /// <summary>The coat lies back along the body, from head to tail, close enough to show its pattern but thick and soft.</summary>
    private static readonly Vector3 CoatDrift = new(0, -0.1f, 0.8f);

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
        FloatDepth = 0.4f,
        WadeDepth = 0.25f,
        MouthDistance = 0.7f,
        EatReach = 0.5f,
        CanGraze = false,
        BodyRadius = 0.3f,
        BodyHeight = 0.8f,
        CameraHeight = 0.75f,
        CameraDistance = 3.5f,
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
        float bob = Mathf.Abs(Mathf.Sin(_walkCycle)) * stride * Mathf.Lerp(0.015f, 0.06f, run);
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

        _hair.SetShaderParameter("sway_amount", 0.006f + 0.015f * stride);
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
        var dark = new Color(0.66f, 0.65f, 0.61f);
        var light = new Color(0.9f, 0.89f, 0.84f);
        var centre = new Color(0.72f, 0.67f, 0.58f);
        var coat = ProceduralTextures.SpottedFur(Seed, dark, light, centre, Spot, scale: 2.2f);
        var spotted = ProceduralTextures.SpottedFur(Seed + 1, dark, light, centre, Spot, scale: 4f, rosettes: false);
        var pale = new StandardMaterial3D { AlbedoColor = Cream, Roughness = 1f };
        var nose = new StandardMaterial3D { AlbedoColor = new Color(0.5f, 0.4f, 0.4f), Roughness = 0.5f };
        var eye = new StandardMaterial3D { AlbedoColor = new Color(0.66f, 0.71f, 0.58f), Roughness = 0.08f };
        var rim = new StandardMaterial3D { AlbedoColor = new Color(0.07f, 0.06f, 0.06f), Roughness = 0.6f };
        var pupil = new StandardMaterial3D { AlbedoColor = new Color(0.02f, 0.02f, 0.02f), Roughness = 0.05f };
        var earBack = new StandardMaterial3D { AlbedoColor = new Color(0.22f, 0.21f, 0.2f), Roughness = 1f };
        _hair = HairMaterial();

        // Hair takes the colour of the hide beneath it, fading to cream on the belly, chest and inner legs.
        var coatColour = ProceduralTextures.Colouring(coat);
        var spotColour = ProceduralTextures.Colouring(spotted);
        Func<Vector3, Vector3, Color> Underneath(Func<Vector3, Vector3, Color> colouring, float from, float to) =>
            (p, n) => colouring(p, n).Lerp(Cream, Mathf.SmoothStep(from, to, n.Y));
        var bodyColour = Underneath(coatColour, -0.1f, -0.5f);
        var legColour = Underneath(spotColour, 0.3f, -0.3f);

        // Everything hangs off a frame that can bob, sink and tip without disturbing the yaw Player sets.
        _frame = Pivot(this, "Frame", Vector3.Zero);

        // Body: long, low and stocky, with thick fur all over that hangs longer along the belly and chest.
        var body = Pivot(_frame, "Body", new Vector3(0, 0.46f, 0));
        Attach(body, "Hide", Ellipsoid(BodyShape, 36, 20, coat));
        Attach(body, "Coat", Strands(rng, OnShape(rng, BodyShape, 7500, u => u.Y >= -0.5f), CoatDrift,
            0.025f, 0.04f, FurRoot, FurTip, _hair, width: 0.022f, colouring: bodyColour));
        Attach(body, "Belly", Strands(rng, OnShape(rng, BodyShape, 2000, u => u.Y < -0.5f), new Vector3(0, -1f, 0.5f),
            0.04f, 0.065f, FurRoot, FurTip, _hair, width: 0.022f, colouring: bodyColour));
        Attach(body, "Ruff", Strands(rng, OnShape(rng, BodyShape, 700, u => u.Z < -0.6f && u.Y < 0.3f), new Vector3(0, -0.8f, 0.2f),
            0.035f, 0.055f, FurRoot, FurTip, _hair, width: 0.022f, colouring: bodyColour));

        // Head carried low on a short, thick neck that bends at the shoulders to drink.
        _neck = Pivot(_frame, "Neck", new Vector3(0, 0.55f, -0.37f));
        var headPosition = new Vector3(0, 0.03f, -0.18f);
        Attach(_neck, "Joint", new SphereMesh { Radius = 0.11f, Height = 0.22f, Material = coat });
        Attach(_neck, "Throat", Tube([Vector3.Zero, headPosition], [0.11f, 0.07f], 12, coat, capEnd: false));
        Attach(_neck, "ThroatFur", Strands(rng, OnSegment(rng, 800, Vector3.Zero, headPosition, 0.11f, 0.07f), CoatDrift,
            0.025f, 0.04f, FurRoot, FurTip, _hair, width: 0.022f, colouring: Underneath(spotColour, 0f, -0.5f)));
        var head = _head = Pivot(_neck, "Head", headPosition);
        head.Scale = Vector3.One * HeadScale;
        BuildHead(head, rng, spotted, spotColour, pale, nose, eye, rim, pupil, earBack);

        // Legs: an upper and lower segment each, ending in a broad, furry paw. The forelegs are thick straight
        // columns; the hind legs have a big muscular thigh and a hock that angles back.
        _upperLegs = new Node3D[4];
        _lowerLegs = new Node3D[4];
        string[] legNames = ["LegFrontLeft", "LegFrontRight", "LegBackLeft", "LegBackRight"];
        for (int i = 0; i < 4; i++)
        {
            bool front = i < 2;
            float side = i % 2 == 0 ? -1f : 1f;
            float upperLength = front ? FrontUpperLength : BackUpperLength;
            float lowerLength = front ? FrontLowerLength : BackLowerLength;
            float upperRadius = front ? 0.065f : 0.09f;
            var knee = front ? new Vector3(0, -upperLength, 0.01f) : new Vector3(0, -upperLength, 0.06f);

            float hipHeight = upperLength + lowerLength + 0.04f;
            _upperLegs[i] = Pivot(_frame, legNames[i], new Vector3(side * 0.1f, hipHeight, front ? -LegOffset : LegOffset));
            Attach(_upperLegs[i], "Upper", Tube([new Vector3(0, 0.05f, 0), knee], [upperRadius, 0.045f], 12, spotted, capEnd: false));
            Attach(_upperLegs[i], "UpperFur", Strands(rng, OnSegment(rng, 450, new Vector3(0, 0.05f, 0), knee, upperRadius, 0.045f),
                new Vector3(0, -0.6f, 0.3f), 0.02f, 0.035f, FurRoot, FurTip, _hair, width: 0.018f, colouring: legColour));

            _lowerLegs[i] = Pivot(_upperLegs[i], "Lower", knee);
            Attach(_lowerLegs[i], "Joint", new SphereMesh { Radius = 0.045f, Height = 0.09f, Material = spotted });
            var ankle = new Vector3(0, -lowerLength, front ? -0.01f : -0.06f);
            Attach(_lowerLegs[i], "Lower", Tube([Vector3.Zero, ankle], [0.042f, 0.036f], 12, spotted, capEnd: false));
            Attach(_lowerLegs[i], "LowerFur", Strands(rng, OnSegment(rng, 250, Vector3.Zero, ankle, 0.042f, 0.036f),
                new Vector3(0, -0.6f, 0.3f), 0.015f, 0.025f, FurRoot, FurTip, _hair, width: 0.016f, colouring: legColour));

            // Big round paws, wide enough to spread the cat's weight on snow, furred all over.
            var pawPosition = ankle + new Vector3(0, -0.005f, -0.025f);
            var pawRadii = new Vector3(0.05f, 0.03f, 0.062f);
            Attach(_lowerLegs[i], "Paw", Ellipsoid(pawRadii, 14, 10, spotted), pawPosition);
            Attach(_lowerLegs[i], "PawFur", Strands(rng, OnShape(rng, u => u * pawRadii + pawPosition, 150, u => u.Y > -0.3f),
                new Vector3(0, -0.3f, -0.2f), 0.012f, 0.022f, FurRoot, FurTip, _hair, width: 0.014f, colouring: legColour));
        }

        // Tail: nearly as long as the body and thickly furred all the way, a chain of segments rooted on the rump
        // so it can sway and curl. Rosettes give way to dark rings along its outer half, ending in a dark tip.
        _tail = new Node3D[TailSegments];
        Node3D parent = _frame;
        var position = new Vector3(0, 0.58f, 0.46f);
        const float tailLength = TailSegments * TailSegmentLength;
        for (int i = 0; i < TailSegments; i++)
        {
            float top = Mathf.Lerp(0.045f, 0.038f, i / (float)TailSegments);
            float bottom = Mathf.Lerp(0.045f, 0.038f, (i + 1) / (float)TailSegments);
            float start = i * TailSegmentLength;
            Color TailColour(Vector3 p, Vector3 n)
            {
                float along = (start - p.Y) / tailLength;
                float ring = Mathf.SmoothStep(0.4f, 0.55f, along) * Mathf.SmoothStep(0.6f, 0.8f, Mathf.Sin(along * Mathf.Pi * 9f));
                float tip = Mathf.SmoothStep(0.86f, 0.93f, along);
                return coatColour(p, n).Lerp(Cream, Mathf.SmoothStep(0.2f, -0.6f, n.Z) * 0.4f).Lerp(Spot, Mathf.Max(ring * 0.85f, tip * 0.9f));
            }

            _tail[i] = Pivot(parent, "Tail", position);
            Attach(_tail[i], "Joint", new SphereMesh { Radius = top, Height = top * 2f, Material = coat });
            Attach(_tail[i], "Segment", Tube(
                [Vector3.Zero, Vector3.Down * TailSegmentLength], [top, bottom], 10, coat, capEnd: i == TailSegments - 1));
            Attach(_tail[i], "Fluff", Strands(rng, OnTube(rng, 220, top * 0.9f, 0f, -TailSegmentLength), new Vector3(0, -0.6f, 0),
                0.04f, 0.06f, FurRoot, FurTip, _hair, width: 0.02f, colouring: TailColour));
            parent = _tail[i];
            position = Vector3.Down * TailSegmentLength;
        }
    }

    /// <summary>
    /// A small, round cat's head: a domed skull with wide furred cheeks, a short broad nose and pale whisker pads,
    /// pale almond eyes with round pupils set into the face behind dark rims, and small rounded ears set wide.
    /// </summary>
    private void BuildHead(Node3D head, RandomNumberGenerator rng, Material spotted, Func<Vector3, Vector3, Color> spotColour,
        Material pale, Material nose, Material eye, Material rim, Material pupil, Material earBack)
    {
        var skull = new Vector3(0.085f, 0.072f, 0.09f);
        Attach(head, "Skull", Ellipsoid(skull, 24, 16, spotted));
        Attach(head, "SkullFur", Strands(rng, OnShape(rng, u => u * skull, 900, u => u.Z > -0.65f), CoatDrift,
            0.012f, 0.022f, FurRoot, FurTip, _hair, width: 0.012f, colouring: spotColour));

        // Cheeks flare out below and behind the eyes into a ruff of longer, paler fur.
        var cheeks = new Vector3(0.08f, 0.05f, 0.06f);
        var cheekPosition = new Vector3(0, -0.028f, -0.03f);
        Attach(head, "Cheeks", Ellipsoid(cheeks, 20, 12, spotted), cheekPosition);
        Attach(head, "CheekFur", Strands(rng, OnShape(rng, u => u * cheeks + cheekPosition, 500, u => Mathf.Abs(u.X) > 0.5f && u.Z > -0.6f),
            new Vector3(0, -0.4f, 0.6f), 0.02f, 0.035f, FurRoot, FurTip, _hair, width: 0.012f,
            colouring: (p, n) => spotColour(p, n).Lerp(Cream, Mathf.SmoothStep(0f, -0.5f, n.Y))));

        // Muzzle: a broad nose bridge running down to a pink-grey nose, pale whisker pads either side and a pale chin.
        Attach(head, "Bridge", Ellipsoid(new Vector3(0.026f, 0.024f, 0.05f), 14, 10, spotted), new Vector3(0, -0.008f, -0.07f));
        Attach(head, "Chin", Ellipsoid(new Vector3(0.022f, 0.014f, 0.024f), 12, 8, pale), new Vector3(0, -0.06f, -0.088f));
        Attach(head, "Nose", Ellipsoid(new Vector3(0.014f, 0.008f, 0.009f), 10, 8, nose), new Vector3(0, -0.016f, -0.122f));
        foreach (float side in new[] { -1f, 1f })
        {
            var pad = new Vector3(0.026f, 0.022f, 0.028f);
            var padPosition = new Vector3(side * 0.018f, -0.036f, -0.1f);
            Attach(head, "WhiskerPad", Ellipsoid(pad, 12, 8, pale), padPosition);
            Attach(head, "Whiskers", Strands(rng, OnShape(rng, u => u * pad + padPosition, 14, u => u.X * side > 0.5f && u.Z < 0.2f),
                new Vector3(side * 0.4f, 0.2f, 0.3f), 0.06f, 0.09f, Cream, Colors.White, _hair, width: 0.003f));

            // Eyes look forward and a little outward, slanting up towards their outer corners, and sit deep in the
            // face so only the front of each shows.
            var socket = Pivot(head, "Eye", new Vector3(side * 0.034f, 0.01f, -0.071f));
            socket.Rotation = new Vector3(0, -side * 0.3f, side * 0.2f);
            Attach(socket, "Rim", Ellipsoid(new Vector3(0.02f, 0.013f, 0.014f), 16, 10, rim));
            Attach(socket, "Eyeball", Ellipsoid(new Vector3(0.017f, 0.0115f, 0.015f), 16, 10, eye), new Vector3(0, 0, -0.002f));
            Attach(socket, "Pupil", Ellipsoid(new Vector3(0.0065f, 0.0065f, 0.003f), 10, 8, pupil), new Vector3(0, 0, -0.0155f));
        }

        // Small, rounded ears set wide and tipped outward, dark on the back with pale, furred insides.
        _ears = new Node3D[2];
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            _ears[i] = Pivot(head, "Ear", new Vector3(side * 0.055f, 0.05f, 0.02f));
            var tilt = Pivot(_ears[i], "Tilt", Vector3.Zero);
            tilt.Rotation = new Vector3(-0.2f, -side * 0.4f, -side * 0.5f);
            Attach(tilt, "Flap", Ellipsoid(new Vector3(0.032f, 0.03f, 0.009f), 12, 8, earBack), new Vector3(0, 0.022f, 0));
            var inner = new Vector3(0.024f, 0.022f, 0.004f);
            var innerPosition = new Vector3(0, 0.02f, -0.007f);
            Attach(tilt, "Inside", Ellipsoid(inner, 12, 8, pale), innerPosition);
            Attach(tilt, "Tuft", Strands(rng, OnShape(rng, u => u * inner + innerPosition, 25, u => u.Z < 0f && u.Y < 0.5f),
                new Vector3(0, 0.2f, -0.6f), 0.008f, 0.014f, Cream, Colors.White, _hair, width: 0.006f));
        }
    }

    /// <summary>Long low body, deeper and a little broader through the chest, and higher over the rump.</summary>
    private static Vector3 BodyShape(Vector3 u)
    {
        var p = new Vector3(u.X * 0.17f, u.Y * 0.17f, u.Z * 0.5f);
        float chest = Mathf.SmoothStep(0.25f, -0.35f, p.Z);
        p.X *= 1f + chest * 0.12f;
        if (u.Y < 0)
            p.Y *= 1f + chest * 0.35f;
        else
            p.Y *= 1f + Mathf.SmoothStep(0.05f, 0.35f, p.Z) * 0.12f;
        return p;
    }
}
