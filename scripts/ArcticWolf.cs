using System;
using Godot;

namespace IceAgeWorld;

/// <summary>
/// An arctic wolf, built entirely in code to a real wolf's proportions: a deep, narrow chest over long, slender legs
/// and neat oval paws, a body that tucks up at the waist, and a thick white winter coat with a heavy ruff round the
/// neck and a bushy tail carried low. Its head is drawn big and cute, with a short, puppyish muzzle, small,
/// rounded ears (less to freeze), big round amber eyes and a black nose and lips. It animates its own walk, which lengthens
/// into a gallop at speed, lowers its head and dips its shoulders to drink, swings its tail and flicks its ears.
/// The model faces -Z.
/// </summary>
public partial class ArcticWolf : Animal
{
    private const int Seed = 23;
    private const int TailSegments = 4;
    // A long, full brush that hangs nearly to the hocks.
    private const float TailSegmentLength = 0.16f;
    private const float HeadDownAngle = -1.6f;

    /// <summary>How much bigger the whole head (skull, face, ears and fur) is drawn than it is modelled.</summary>
    private const float HeadScale = 2f;

    // A cute, puppyish face: a shorter muzzle, and bigger eyes and ears, on top of the head's own scale.
    private const float MuzzleScale = 0.72f;
    private const float EyeScale = 1.55f;
    private const float EarScale = 1.2f;

    // How far the body sinks and tips forward while drinking, with the forelegs folding to keep the paws planted.
    private const float CrouchDrop = 0.12f;
    private const float CrouchPitch = 0.25f;

    /// <summary>Distance from the body's centre to the shoulders and hips along the spine.</summary>
    private const float LegOffset = 0.32f;

    private const float FrontUpperLength = 0.27f;
    private const float FrontLowerLength = 0.25f;
    private const float BackUpperLength = 0.29f;
    private const float BackLowerLength = 0.23f;

    /// <summary>Gap between the bottom of a straight leg and the ground, which the paw fills.</summary>
    private const float LegClearance = 0.03f;

    private const float FrontHipHeight = FrontUpperLength + FrontLowerLength + LegClearance;
    private const float BackHipHeight = BackUpperLength + BackLowerLength + LegClearance;

    // Sitting, the wolf rocks back about its shoulders onto its haunches with its forelegs straight, sitting up taller
    // than a cat. Lying, it sinks onto its belly with its forelegs stretched out in front and hind legs folded alongside.
    private const float SitPitch = 0.65f;
    private const float LieDrop = 0.36f;

    // How each tail segment points at rest, in radians from straight down (negative is back): out of the rump and down
    // to the ground, then along it.
    private static readonly float[] SitTail = [-0.6f, -1.3f, -1.57f, -1.57f];
    private static readonly float[] LieTail = [-0.15f, -0.7f, -1.4f, -1.57f];

    // Dead, it lies on its side: how far the body's centre sits off the ground, and the neck stretched out and head
    // laid flat along the ground.
    private const float DeadFlank = 0.19f;
    private const float DeadNeck = -1f;
    private const float DeadHead = 0.45f;

    // A white coat with a faint cream cast along the back, shading a little greyer at the hair roots.
    private static readonly Color FurRoot = new(0.84f, 0.84f, 0.82f);
    private static readonly Color FurTip = Colors.White;
    private static readonly Color Saddle = new(0.9f, 0.87f, 0.8f);
    private static readonly Vector3 CoatDrift = new(0, -0.15f, 0.8f);

    // Rest pose of each tail segment, relative to the one above: out from the rump, then hanging down.
    private static readonly float[] TailRest = [-0.9f, 0.3f, 0.2f, 0.1f];

    // Walking moves diagonal pairs of legs together; galloping bounds with the front pair, then the back pair.
    private static readonly float[] WalkPhase = [0f, Mathf.Pi, Mathf.Pi, 0f];
    private static readonly float[] GallopPhase = [0f, 0.3f, Mathf.Pi, Mathf.Pi + 0.3f];

    // Leg order: front left, front right, back left, back right.
    private Node3D _frame = null!;
    private Node3D _neck = null!;
    private Node3D _head = null!;
    private Node3D[] _upperLegs = [];
    private Node3D[] _lowerLegs = [];
    private Node3D[] _paws = [];
    private Node3D[] _tail = [];
    private Node3D[] _ears = [];
    private ShaderMaterial _hair = null!;
    private float _walkCycle;
    private float _time;

    public override string DisplayName => "Arctic wolf";
    public override string YoungName => "Arctic wolf pup";

    public override AnimalStats Stats { get; } = new()
    {
        Scores = new() { JumpHeight = 6, JumpLength = 6, LandSpeed = 7, WaterSpeed = 4, Agility = 7, Stamina = 8 },
        Abilities = Ability.Pack | Ability.Hunt,
        LeaderSize = 1.25f,
        Companions = 3,
        FloatDepth = 0.55f,
        WadeDepth = 0.3f,
        MouthDistance = 0.7f,
        EatReach = 0.5f,
        CanGraze = false,
        BodyRadius = 0.32f,
        BodyHeight = 0.95f,
        CameraHeight = 0.9f,
        CameraDistance = 4f,
    };

    public override void _Ready() => Build();

    public override void Animate(float speed, float stride, float eat, float dt)
    {
        _time += dt;

        // Long legs take long strides, so the cadence rises more slowly than speed.
        _walkCycle += Mathf.Sqrt(speed) * 3.6f * dt;
        float run = Mathf.Clamp((speed - Stats.WalkSpeed) / (Stats.SprintSpeed - Stats.WalkSpeed), 0f, 1f);

        float drop = eat * CrouchDrop;
        float pitch = eat * CrouchPitch;
        float bob = Mathf.Abs(Mathf.Sin(_walkCycle)) * stride * Mathf.Lerp(0.015f, 0.05f, run);
        // Sitting tips the body back about the shoulders, so the forelegs stay planted while the rump sinks to the ground.
        var sitShift = TipAbout(new Vector3(0, FrontHipHeight, -LegOffset), SitPitch);
        _frame.Position = Pose(new Vector3(0, bob - drop, 0), sitShift, new Vector3(0, -LieDrop, 0));
        _frame.Rotation = new Vector3(Pose(-pitch + Mathf.Sin(_walkCycle) * run * 0.05f, SitPitch, 0f), 0, 0);

        float amplitude = Mathf.Lerp(0.38f, 0.8f, run) * stride;
        float frontFold = FoldAngle(drop + LegOffset * Mathf.Sin(pitch), FrontUpperLength + FrontLowerLength);
        float backFold = FoldAngle(drop - LegOffset * Mathf.Sin(pitch), BackUpperLength + BackLowerLength);

        // At rest the hind legs fold right up, far enough to bring the paws under the lowered hips.
        float sitHip = (new Vector3(0, BackHipHeight, LegOffset).Rotated(Vector3.Right, SitPitch) + sitShift).Y;
        float sitFold = FoldToReach(_lowerLegs[2].Position, _paws[2].Position, sitHip - LegClearance);
        float lieFold = FoldToReach(_lowerLegs[2].Position, _paws[2].Position, BackHipHeight - LieDrop - LegClearance);

        // Lying, the forelegs reach forward from the shoulder to rest the elbows on the ground, forearms out in front.
        float reach = Mathf.Acos(Mathf.Clamp((FrontHipHeight - LieDrop - LegClearance) / FrontUpperLength, -1f, 1f));

        for (int i = 0; i < 4; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            float phase = _walkCycle + Mathf.Lerp(WalkPhase[i], GallopPhase[i], run);
            float swing = Mathf.Sin(phase) * amplitude;
            float lift = Mathf.Max(0f, Mathf.Cos(phase)) * stride * Mathf.Lerp(0.7f, 1.2f, run);

            // Forelegs fold with the elbow behind, hind legs with the hock behind and the knee in front.
            // Sitting, the forelegs stand straight and the hind legs fold under the haunches; lying, the hind legs fold
            // alongside the belly. Every paw lies flat on the ground.
            if (i < 2)
            {
                _upperLegs[i].Rotation = new Vector3(Pose(swing - frontFold + pitch, -SitPitch + 0.05f, reach), 0, 0);
                _lowerLegs[i].Rotation = new Vector3(Pose(-lift + frontFold * 2f, 0f, Mathf.Pi / 2f - reach), 0, 0);
                _paws[i].Rotation = new Vector3(Pose(0f, 0f, -Mathf.Pi / 2f), 0, 0);
            }
            else
            {
                _upperLegs[i].Rotation = new Vector3(Pose(swing + backFold + pitch, sitFold - SitPitch, lieFold), 0,
                    Pose(0f, side * 0.12f, side * 0.3f));
                _lowerLegs[i].Rotation = new Vector3(Pose(lift * 0.7f - backFold * 2f, -sitFold * 2f, -lieFold * 2f), 0, 0);
                _paws[i].Rotation = new Vector3(Pose(0f, sitFold, lieFold), 0, 0);
            }
        }

        // Wolves travel with the head carried low and level, and drop it right down to drink. At rest the head is held
        // up, looking ahead, however the body is tipped.
        _neck.Rotation = new Vector3(Pose(eat * HeadDownAngle - run * 0.25f + Mathf.Sin(_walkCycle * 2f) * 0.03f * stride,
            -SitPitch * 0.6f, 0.1f), 0, 0);
        _head.Rotation = new Vector3(Pose(-eat * HeadDownAngle * 0.45f + run * 0.2f, -SitPitch * 0.4f, -0.1f), 0, 0);

        // The tail hangs and sways, lifting out behind at a run. At rest it lies along the ground, sweeping gently.
        for (int i = 0; i < _tail.Length; i++)
        {
            float lag = i * 0.5f;
            float sway = Mathf.Sin(_time * 1.2f - lag) * 0.1f + Mathf.Sin(_walkCycle * 0.5f - lag) * 0.12f * stride;
            float lift = i == 0 ? -run * 0.5f : -TailRest[i] * run * 0.8f;
            float curl = i < 2 ? 0f : 0.3f + sway;
            _tail[i].Rotation = new Vector3(
                Pose(TailRest[i] + lift,
                    i == 0 ? SitTail[0] - SitPitch : SitTail[i] - SitTail[i - 1],
                    i == 0 ? LieTail[0] : LieTail[i] - LieTail[i - 1]),
                0, Pose(sway, curl, curl));
        }

        float twitch = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(_time * 0.7f)), 12f) * 0.35f * Alive;
        _ears[0].Rotation = new Vector3(0, 0, twitch);
        _ears[1].Rotation = new Vector3(0, 0, -Mathf.Pow(Mathf.Max(0f, Mathf.Sin(_time * 0.7f + 2.5f)), 12f) * 0.35f * Alive);

        GoLimp();
        _hair.SetShaderParameter("sway_amount", (0.008f + 0.02f * stride) * Alive);
    }

    /// <summary>
    /// Dead, the wolf flops over onto its left flank, legs stretched loosely out from the body, head laid flat on the
    /// ground at the end of its outstretched neck and tail trailing straight behind.
    /// </summary>
    private void GoLimp()
    {
        if (Dead <= 0f)
            return;

        // Rolled onto its side, the body's centre comes down to its half-width in coat above the ground.
        _frame.Position = Limp(_frame.Position, new Vector3(0.62f, DeadFlank, 0f));
        _frame.Rotation = Limp(_frame.Rotation, new Vector3(0f, 0f, Mathf.Pi / 2f));

        for (int i = 0; i < 4; i++)
        {
            bool front = i < 2;
            _upperLegs[i].Rotation = Limp(_upperLegs[i].Rotation, new Vector3(front ? 0.5f : -0.45f, 0f, 0f));
            _lowerLegs[i].Rotation = Limp(_lowerLegs[i].Rotation, new Vector3(front ? 0.25f : -0.25f, 0f, 0f));
            _paws[i].Rotation = Limp(_paws[i].Rotation, new Vector3(front ? 0.3f : 0.2f, 0f, 0f));
        }

        _neck.Rotation = Limp(_neck.Rotation, new Vector3(DeadNeck, 0f, 0f));
        _head.Rotation = Limp(_head.Rotation, new Vector3(DeadHead, 0f, 0f));
        for (int i = 0; i < _tail.Length; i++)
            _tail[i].Rotation = Limp(_tail[i].Rotation, new Vector3(i == 0 ? -1.4f : 0.05f, 0f, 0f));
    }

    private void Build()
    {
        var rng = new RandomNumberGenerator { Seed = Seed };
        var coat = ProceduralTextures.Fur(Seed, new Color(0.8f, 0.8f, 0.78f), new Color(0.98f, 0.98f, 0.96f), 3f);
        _hair = HairMaterial();

        // Hair takes the hide's grain, with a cream saddle over the back.
        var hide = ProceduralTextures.Colouring(coat);
        Func<Vector3, Vector3, Color> coatColour = (p, n) => hide(p, n).Lerp(Saddle, Mathf.SmoothStep(0.4f, 0.9f, n.Y) * 0.6f);

        _frame = Pivot(this, "Frame", Vector3.Zero);

        // Body: deep at the chest, tucked up at the waist, with a thick coat that hangs longer under the chest.
        var body = Pivot(_frame, "Body", new Vector3(0, 0.62f, 0));
        Attach(body, "Hide", Ellipsoid(BodyShape, 36, 20, coat));
        Attach(body, "Coat", Strands(rng, OnShape(rng, BodyShape, 6500, u => u.Y >= -0.4f), CoatDrift,
            0.045f, 0.07f, FurRoot, FurTip, _hair, width: 0.026f, colouring: coatColour));
        Attach(body, "Belly", Strands(rng, OnShape(rng, BodyShape, 1800, u => u.Y < -0.4f), new Vector3(0, -1f, 0.4f),
            0.05f, 0.09f, FurRoot, FurTip, _hair, width: 0.026f, colouring: coatColour));

        // Neck carried forward and a little up from the shoulders, buried in a heavy ruff of long fur.
        _neck = Pivot(_frame, "Neck", new Vector3(0, 0.7f, -0.36f));
        var headPosition = new Vector3(0, 0.12f, -0.2f);
        Attach(_neck, "Joint", new SphereMesh { Radius = 0.13f, Height = 0.26f, Material = coat });
        Attach(_neck, "Throat", Tube([Vector3.Zero, headPosition], [0.13f, 0.09f], 12, coat, capEnd: false));
        Attach(_neck, "Ruff", Strands(rng, OnSegment(rng, 1600, new Vector3(0, -0.02f, 0.06f), headPosition, 0.14f, 0.1f), CoatDrift,
            0.07f, 0.11f, FurRoot, FurTip, _hair, width: 0.03f, colouring: coatColour));
        _head = Pivot(_neck, "Head", headPosition);
        _head.Scale = Vector3.One * HeadScale;
        BuildHead(_head, rng, coat, coatColour);

        _upperLegs = new Node3D[4];
        _lowerLegs = new Node3D[4];
        _paws = new Node3D[4];
        string[] legNames = ["LegFrontLeft", "LegFrontRight", "LegBackLeft", "LegBackRight"];
        var pad = new StandardMaterial3D { AlbedoColor = new Color(0.12f, 0.1f, 0.1f), Roughness = 0.8f };
        for (int i = 0; i < 4; i++)
        {
            bool front = i < 2;
            float side = i % 2 == 0 ? -1f : 1f;
            float upperLength = front ? FrontUpperLength : BackUpperLength;
            float lowerLength = front ? FrontLowerLength : BackLowerLength;
            float upperRadius = front ? 0.065f : 0.09f;
            float kneeRadius = front ? 0.042f : 0.045f;
            var knee = front ? new Vector3(0, -upperLength, 0.01f) : new Vector3(0, -upperLength, -0.05f);

            float hipHeight = upperLength + lowerLength + LegClearance;
            _upperLegs[i] = Pivot(_frame, legNames[i], new Vector3(side * 0.1f, hipHeight, front ? -LegOffset : LegOffset));
            Attach(_upperLegs[i], "Upper", Tube([new Vector3(0, 0.06f, 0), knee], [upperRadius, kneeRadius], 12, coat, capEnd: false));
            Attach(_upperLegs[i], "UpperFur", Strands(rng, OnSegment(rng, 450, new Vector3(0, 0.06f, 0), knee, upperRadius, kneeRadius),
                new Vector3(0, -0.6f, 0.3f), 0.03f, 0.05f, FurRoot, FurTip, _hair, width: 0.02f, colouring: coatColour));

            // Muscle where the leg meets the body: the shoulder in front, the long thigh behind.
            var muscle = front ? new Vector3(0.07f, 0.12f, 0.09f) : new Vector3(0.08f, 0.13f, 0.11f);
            var musclePosition = new Vector3(0, -0.02f, front ? 0f : 0.02f);
            Attach(_upperLegs[i], "Muscle", Ellipsoid(muscle, 14, 10, coat), musclePosition);
            Attach(_upperLegs[i], "MuscleFur", Strands(rng, OnShape(rng, u => u * muscle + musclePosition, 450, u => u.X * side > -0.2f),
                CoatDrift, 0.04f, 0.06f, FurRoot, FurTip, _hair, width: 0.024f, colouring: coatColour));

            _lowerLegs[i] = Pivot(_upperLegs[i], "Lower", knee);
            Attach(_lowerLegs[i], "Joint", new SphereMesh { Radius = kneeRadius, Height = kneeRadius * 2f, Material = coat });
            // The hind leg's lower half is the long hock, angled back from the knee to the heel.
            var ankle = new Vector3(0, -lowerLength, front ? -0.01f : 0.05f);
            Attach(_lowerLegs[i], "Lower", Tube([Vector3.Zero, ankle], [kneeRadius * 0.9f, 0.03f], 10, coat, capEnd: false));
            Attach(_lowerLegs[i], "LowerFur", Strands(rng, OnSegment(rng, 250, Vector3.Zero, ankle, kneeRadius * 0.9f, 0.03f),
                new Vector3(0, -0.6f, 0.3f), 0.015f, 0.03f, FurRoot, FurTip, _hair, width: 0.014f, colouring: coatColour));

            // Neat oval paws, furred between the toes against the snow, with dark pads and claws peeping out in front.
            // They hinge at the ankle so they can lie flat when the leg is folded or stretched out at rest.
            _paws[i] = Pivot(_lowerLegs[i], "Foot", ankle);
            var pawPosition = new Vector3(0, 0.002f, -0.035f);
            var pawRadii = new Vector3(0.042f, 0.028f, 0.058f);
            Attach(_paws[i], "Paw", Ellipsoid(pawRadii, 14, 10, coat), pawPosition);
            Attach(_paws[i], "Pad", Ellipsoid(new Vector3(0.03f, 0.008f, 0.04f), 10, 6, pad), pawPosition + new Vector3(0, -0.022f, 0));
            Attach(_paws[i], "PawFur", Strands(rng, OnShape(rng, u => u * pawRadii + pawPosition, 160, u => u.Y > -0.2f),
                new Vector3(0, -0.3f, -0.3f), 0.012f, 0.022f, FurRoot, FurTip, _hair, width: 0.014f, colouring: coatColour));
            foreach (float toe in new[] { -0.6f, -0.2f, 0.2f, 0.6f })
                Attach(_paws[i], "Claw", Ellipsoid(new Vector3(0.005f, 0.005f, 0.012f), 6, 4, pad),
                    pawPosition + new Vector3(toe * pawRadii.X, -0.012f, -pawRadii.Z * 0.95f));
        }

        // Tail: thick and bushy, rooted in the rump and carried hanging low, a chain of segments that sways.
        _tail = new Node3D[TailSegments];
        Node3D parent = _frame;
        var position = new Vector3(0, 0.7f, 0.42f);
        for (int i = 0; i < TailSegments; i++)
        {
            float top = Mathf.Lerp(0.05f, 0.035f, i / (float)TailSegments);
            float bottom = Mathf.Lerp(0.05f, 0.035f, (i + 1) / (float)TailSegments);
            _tail[i] = Pivot(parent, "Tail", position);
            if (i > 0)
                Attach(_tail[i], "Joint", new SphereMesh { Radius = top, Height = top * 2f, Material = coat });
            Attach(_tail[i], "Segment", Tube([Vector3.Zero, Vector3.Down * TailSegmentLength], [top, bottom], 10, coat, capEnd: i == TailSegments - 1));
            Attach(_tail[i], "Brush", Strands(rng, OnTube(rng, 550, bottom * 0.9f, 0f, -TailSegmentLength), new Vector3(0, -0.8f, 0),
                0.07f, 0.11f, FurRoot, FurTip, _hair, width: 0.026f, colouring: coatColour));
            parent = _tail[i];
            position = Vector3.Down * TailSegmentLength;
        }
    }

    /// <summary>
    /// A wolf's head: a broad skull with a gentle stop down to a tapering muzzle, a black nose and lip line, amber eyes
    /// set forward under a brow, small rounded ears, and thick cheek fur that flares into the neck ruff.
    /// </summary>
    private void BuildHead(Node3D head, RandomNumberGenerator rng, StandardMaterial3D coat, Func<Vector3, Vector3, Color> coatColour)
    {
        var face = (StandardMaterial3D)coat.Duplicate();
        face.VertexColorUseAsAlbedo = true;
        var nose = new StandardMaterial3D { AlbedoColor = new Color(0.05f, 0.045f, 0.045f), Roughness = 0.35f };
        var earInside = new StandardMaterial3D { AlbedoColor = new Color(0.75f, 0.68f, 0.66f), Roughness = 1f };

        var skull = new Vector3(0.085f, 0.075f, 0.1f);
        Attach(head, "Skull", Ellipsoid(skull, 28, 18, coat));
        Attach(head, "SkullFur", Strands(rng, OnShape(rng, u => u * skull, 900, u => u.Z > -0.6f), CoatDrift,
            0.015f, 0.03f, FurRoot, FurTip, _hair, width: 0.014f, colouring: coatColour));

        // Cheek ruff: longer fur flaring out and back below the ears.
        var cheeks = new Vector3(0.09f, 0.06f, 0.07f);
        var cheekPosition = new Vector3(0, -0.03f, 0.01f);
        Attach(head, "Cheeks", Ellipsoid(cheeks, 18, 12, coat), cheekPosition);
        Attach(head, "CheekFur", Strands(rng, OnShape(rng, u => u * cheeks + cheekPosition, 700, u => Mathf.Abs(u.X) > 0.4f && u.Z > -0.5f),
            new Vector3(0, -0.3f, 0.8f), 0.04f, 0.07f, FurRoot, FurTip, _hair, width: 0.02f, colouring: coatColour));

        // Muzzle: tapering forward from the face, its lower edge the black lip line. It hangs off a pivot where it
        // meets the face, so it can be drawn shorter than it is modelled, like a pup's.
        var snout = Pivot(head, "Snout", new Vector3(0, -0.03f, -0.07f));
        snout.Scale = Vector3.One * MuzzleScale;
        var muzzlePosition = new Vector3(0, 0f, -0.03f);
        Func<Vector3, Vector3> muzzle = u =>
        {
            float taper = Mathf.Lerp(1f, 0.7f, Mathf.Max(0f, -u.Z));
            return new Vector3(u.X * 0.045f * taper, u.Y * 0.04f * taper, u.Z * 0.085f);
        };
        Attach(snout, "Muzzle", Ellipsoid(muzzle, 20, 12, face,
            (p, n) => Colors.White.Lerp(new Color(0.08f, 0.07f, 0.07f), Mathf.SmoothStep(-0.25f, -0.5f, n.Y) * Mathf.SmoothStep(0f, -0.04f, p.Z))),
            muzzlePosition);
        Attach(snout, "MuzzleFur", Strands(rng, OnShape(rng, u => muzzle(u) + muzzlePosition, 300, u => u.Y > -0.2f && u.Z > -0.7f),
            new Vector3(0, 0, 1f), 0.01f, 0.018f, FurRoot, FurTip, _hair, width: 0.01f, colouring: coatColour));
        Attach(snout, "Jaw", Ellipsoid(new Vector3(0.032f, 0.018f, 0.06f), 14, 8, coat), new Vector3(0, -0.03f, -0.015f));

        // A slightly bigger, rounder nose button suits the shorter muzzle.
        Attach(snout, "Nose", Ellipsoid(new Vector3(0.02f, 0.015f, 0.016f), 14, 10, nose), new Vector3(0, 0.02f, -0.11f));

        foreach (float side in new[] { -1f, 1f })
        {
            // Big, level eyes look forward and slightly out, with the brow raised clear of them; wide pupils and an
            // open, unfurrowed brow make the face soft and friendly.
            Eye(head, new Vector3(side * 0.043f, 0.02f, -0.078f), new Vector3(0, -side * 0.35f, 0f), 0.0105f * EyeScale,
                new Color(0.78f, 0.55f, 0.2f), 0.62f);
            Attach(head, "Brow", Ellipsoid(new Vector3(0.02f, 0.008f, 0.016f), 10, 6, coat), new Vector3(side * 0.042f, 0.047f, -0.066f));
        }

        // Small, rounded triangular ears, upright and set well apart.
        _ears = new Node3D[2];
        Func<Vector3, Vector3> earShape = u =>
        {
            float up = (u.Y + 1f) * 0.5f;
            return new Vector3(u.X * 0.032f * (1f - 0.75f * up * up), u.Y * 0.035f, u.Z * 0.012f * (1f - 0.5f * up));
        };
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            _ears[i] = Pivot(head, "Ear", new Vector3(side * 0.05f, 0.06f, 0.02f));
            var tilt = Pivot(_ears[i], "Tilt", Vector3.Zero);
            tilt.Rotation = new Vector3(-0.1f, -side * 0.35f, -side * 0.3f);
            tilt.Scale = Vector3.One * EarScale;
            Attach(tilt, "Flap", Ellipsoid(earShape, 14, 10, coat), new Vector3(0, 0.03f, 0));
            Attach(tilt, "Inside", Ellipsoid(u => earShape(u) * new Vector3(0.75f, 0.8f, 0.5f), 12, 8, earInside), new Vector3(0, 0.025f, -0.006f));
            Attach(tilt, "EarFur", Strands(rng, OnShape(rng, u => earShape(u) + new Vector3(0, 0.03f, 0), 160, u => u.Z > -0.3f),
                new Vector3(0, 0.6f, 0.4f), 0.008f, 0.016f, FurRoot, FurTip, _hair, width: 0.01f, colouring: coatColour));
        }
    }

    /// <summary>Deep at the chest and shallow at the loins, where a wolf's belly tucks up towards the hind legs.</summary>
    private static Vector3 BodyShape(Vector3 u)
    {
        var p = new Vector3(u.X * 0.15f, u.Y * 0.17f, u.Z * 0.48f);
        float chest = Mathf.SmoothStep(0.3f, -0.3f, p.Z);
        p.X *= 0.9f + chest * 0.15f;
        if (u.Y < 0)
            p.Y *= 0.75f + chest * 0.5f;
        return p;
    }
}
