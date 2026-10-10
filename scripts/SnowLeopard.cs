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

    /// <summary>
    /// How much bigger the whole cat is drawn than it is modelled, so it stands taller among the other animals. Its
    /// collision body, camera and reach grow with it; everything below is in modelled (unscaled) units.
    /// </summary>
    private const float BodyScale = 2.6f;

    /// <summary>How much bigger the whole head (skull, face, ears and fur) is drawn than it is modelled.</summary>
    private const float HeadScale = 1.6f;

    /// <summary>How much smaller the muzzle (nose bridge, nose, whisker pads and chin) is drawn than it is modelled.</summary>
    private const float MuzzleScale = 0.8f;

    // How much bigger the eyes and ears are drawn than they are modelled, on top of the head's own scale.
    private const float EyeScale = 1.45f;
    private const float EarScale = 1.6f;

    // How far the body sinks and tips forward while drinking, with the forelegs folding to keep the paws planted.
    private const float CrouchDrop = 0.1f;
    private const float CrouchPitch = 0.18f;

    /// <summary>How far the body tips nose-down, in radians, when landing forepaws first from a drop.</summary>
    private const float LandingTilt = 0.6f;

    /// <summary>Distance from the body's centre to the shoulders and hips along the spine.</summary>
    private const float LegOffset = 0.33f;

    private const float FrontUpperLength = 0.2f;
    private const float FrontLowerLength = 0.2f;
    private const float BackUpperLength = 0.24f;
    private const float BackLowerLength = 0.17f;

    /// <summary>Gap between the bottom of a straight leg and the ground, which the paw fills.</summary>
    private const float LegClearance = 0.04f;

    private const float FrontHipHeight = FrontUpperLength + FrontLowerLength + LegClearance;
    private const float BackHipHeight = BackUpperLength + BackLowerLength + LegClearance;

    // Sitting, the cat rocks back about its shoulders onto its haunches, forelegs straight and tail curled round its
    // side. Lying, it sinks onto its belly like a sphinx, forelegs stretched out in front and hind legs folded alongside.
    private const float SitPitch = 0.55f;
    private const float LieDrop = 0.24f;

    // How each tail segment points at rest, in radians from straight down (negative is back): out of the rump and down
    // to the ground, then along it.
    private static readonly float[] SitTail = [-1.25f, -1.57f, -1.57f, -1.57f, -1.57f, -1.57f, -1.57f];
    private static readonly float[] LieTail = [-0.4f, -1f, -1.57f, -1.57f, -1.57f, -1.57f, -1.57f];

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
    private Node3D[] _paws = [];
    private Node3D[] _tail = [];
    private Node3D[] _ears = [];
    private ShaderMaterial _hair = null!;
    private float _walkCycle;
    private float _time;

    public override string DisplayName => "Snow leopard";
    public override string YoungName => "Snow leopard cub";

    public override AnimalStats Stats { get; } = new()
    {
        Scores = new() { JumpHeight = 10, JumpLength = 10, LandSpeed = 9, WaterSpeed = 3, Agility = 10, Stamina = 7 },
        Abilities = Ability.ClimbTrees | Ability.Hunt | Ability.Grip,
        FloatDepth = 0.4f * BodyScale,
        WadeDepth = 0.25f * BodyScale,
        MouthDistance = 0.7f * BodyScale,
        EatReach = 0.5f * BodyScale,
        CanGraze = false,
        BodyRadius = 0.3f * BodyScale,
        BodyHeight = 0.8f * BodyScale,
        CameraHeight = 0.75f * BodyScale,
        CameraDistance = 3.5f * BodyScale,
    };

    public override void _Ready()
    {
        Scale = Vector3.One * BodyScale;
        Build();
    }

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

        // Dropping from a tree, a snow leopard tips nose-down so its forepaws, stretched out ahead, meet the ground
        // first and its strong shoulders take the impact; the hind legs trail and swing down after. The body pivots
        // about the forepaws, so they stay at ground level while the hindquarters rise.
        float tilt = Landing * LandingTilt;

        // Sitting tips the body back about the shoulders, so the forelegs stay planted while the rump sinks to the ground.
        var sitShift = TipAbout(new Vector3(0, FrontHipHeight, -LegOffset), SitPitch);
        _frame.Position = Pose(new Vector3(0, bob - drop + LegOffset * Mathf.Sin(tilt), 0), sitShift, new Vector3(0, -LieDrop, 0));
        _frame.Rotation = new Vector3(Pose(-pitch - tilt + Mathf.Sin(_walkCycle) * run * 0.06f, SitPitch, 0f), 0, 0);

        float amplitude = Mathf.Lerp(0.4f, 0.85f, run) * stride;
        float frontFold = FoldAngle(drop + LegOffset * Mathf.Sin(pitch), FrontUpperLength + FrontLowerLength);
        float backFold = FoldAngle(drop - LegOffset * Mathf.Sin(pitch), BackUpperLength + BackLowerLength);

        // At rest the hind legs fold right up, far enough to bring the paws under the lowered hips.
        float sitHip = (new Vector3(0, BackHipHeight, LegOffset).Rotated(Vector3.Right, SitPitch) + sitShift).Y;
        float sitFold = FoldToReach(_lowerLegs[2].Position, _paws[2].Position, sitHip - LegClearance);
        float lieFold = FoldToReach(_lowerLegs[2].Position, _paws[2].Position, BackHipHeight - LieDrop - LegClearance);

        for (int i = 0; i < 4; i++)
        {
            bool front = i < 2;
            float side = i % 2 == 0 ? -1f : 1f;
            float phase = _walkCycle + Mathf.Lerp(WalkPhase[i], GallopPhase[i], run);
            float swing = Mathf.Sin(phase) * amplitude;

            // Lift the paw while the leg swings forward.
            float lift = Mathf.Max(0f, Mathf.Cos(phase)) * stride * Mathf.Lerp(0.8f, 1.3f, run);

            // Forelegs fold with the elbow behind, hind legs with the knee in front. Both are corrected for
            // the body's forward tip so the paws stay under the shoulders and hips.
            // When landing, the forelegs reach straight down to the ground and the hind legs trail, half folded.
            // Sitting, the forelegs stand straight and the hind legs fold under the haunches. Lying, the elbows rest on
            // the ground with the forearms stretched out in front, and the hind legs fold alongside the belly.
            // Every paw lies flat on the ground.
            if (front)
            {
                _upperLegs[i].Rotation = new Vector3(Pose(swing - frontFold + pitch + tilt, -SitPitch + 0.05f, 0.6f), 0, 0);
                _lowerLegs[i].Rotation = new Vector3(Pose(-lift + frontFold * 2f, 0f, 0.97f), 0, 0);
                _paws[i].Rotation = new Vector3(Pose(0f, 0f, -1.57f), 0, 0);
            }
            else
            {
                _upperLegs[i].Rotation = new Vector3(Pose(swing + backFold + pitch - Landing * 0.4f, sitFold - SitPitch, lieFold), 0,
                    Pose(0f, side * 0.15f, side * 0.3f));
                _lowerLegs[i].Rotation = new Vector3(Pose(lift * 0.7f - backFold * 2f + Landing * 0.6f, -sitFold * 2f, -lieFold * 2f), 0, 0);
                _paws[i].Rotation = new Vector3(Pose(0f, sitFold, lieFold), 0, 0);
            }
        }

        // The neck lowers to drink and nods slightly in step, while the head tilts back up so the chin,
        // not the forehead, meets the water.
        // Landing, it raises its head to keep its eyes on the ground ahead rather than the ground below.
        // At rest it holds its head up and looks straight ahead, however the body is tipped.
        _neck.Rotation = new Vector3(Pose(eat * HeadDownAngle + Mathf.Sin(_walkCycle * 2f) * 0.04f * stride + tilt * 0.7f, -SitPitch * 0.6f, 0.1f), 0, 0);
        _head.Rotation = new Vector3(Pose(-eat * HeadDownAngle * 0.5f, -SitPitch * 0.4f, -0.1f), 0, 0);

        // The tail sways slowly, each segment lagging the one above; it streams out straighter at a run.
        // At rest it lies along the ground and curls round the cat's side, its tip still twitching.
        for (int i = 0; i < _tail.Length; i++)
        {
            float lag = i * 0.5f;
            float sway = Mathf.Sin(_time * 1.1f - lag) * 0.12f + Mathf.Sin(_walkCycle * 0.5f - lag) * 0.1f * stride;
            float lift = i == 0 ? -run * 0.3f : -TailRest[i] * run * 0.7f;
            float curl = i < 2 ? 0f : 0.5f + sway * 0.5f;
            _tail[i].Rotation = new Vector3(
                Pose(TailRest[i] + lift + Mathf.Sin(_time * 0.7f - lag) * 0.05f,
                    i == 0 ? SitTail[0] - SitPitch : SitTail[i] - SitTail[i - 1],
                    i == 0 ? LieTail[0] : LieTail[i] - LieTail[i - 1]),
                0, Pose(sway, curl, curl * 0.8f));
        }

        // Ears flick now and then.
        float twitch = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(_time * 0.8f)), 12f) * 0.4f;
        _ears[0].Rotation = new Vector3(0, 0, twitch);
        _ears[1].Rotation = new Vector3(0, 0, -Mathf.Pow(Mathf.Max(0f, Mathf.Sin(_time * 0.8f + 2f)), 12f) * 0.4f);

        _hair.SetShaderParameter("sway_amount", 0.006f + 0.015f * stride);
    }

    private void Build()
    {
        var rng = new RandomNumberGenerator { Seed = Seed };
        var dark = new Color(0.66f, 0.65f, 0.61f);
        var light = new Color(0.9f, 0.89f, 0.84f);
        var centre = new Color(0.72f, 0.67f, 0.58f);
        var coat = ProceduralTextures.SpottedFur(Seed, dark, light, centre, Spot, scale: 2.2f);
        var spotted = ProceduralTextures.SpottedFur(Seed + 1, dark, light, centre, Spot, scale: 4f, rosettes: false);
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

        // Head held up on a short, thick neck that angles upward from the shoulders and bends down to drink.
        _neck = Pivot(_frame, "Neck", new Vector3(0, 0.49f, -0.37f));
        var headPosition = new Vector3(0, 0.13f, -0.17f);
        Attach(_neck, "Joint", new SphereMesh { Radius = 0.11f, Height = 0.22f, Material = coat });
        Attach(_neck, "Throat", Tube([Vector3.Zero, headPosition], [0.11f, 0.07f], 12, coat, capEnd: false));
        Attach(_neck, "ThroatFur", Strands(rng, OnSegment(rng, 800, Vector3.Zero, headPosition, 0.11f, 0.07f), CoatDrift,
            0.025f, 0.04f, FurRoot, FurTip, _hair, width: 0.022f, colouring: Underneath(spotColour, 0f, -0.5f)));
        var head = _head = Pivot(_neck, "Head", headPosition);
        head.Scale = Vector3.One * HeadScale;
        BuildHead(head, rng, spotted, spotColour);

        // Legs: an upper and lower segment each, ending in a broad, furry paw. The forelegs are thick, powerful
        // columns under heavy shoulders; the hind legs have big muscular thighs and a hock that angles back.
        _upperLegs = new Node3D[4];
        _lowerLegs = new Node3D[4];
        _paws = new Node3D[4];
        string[] legNames = ["LegFrontLeft", "LegFrontRight", "LegBackLeft", "LegBackRight"];
        for (int i = 0; i < 4; i++)
        {
            bool front = i < 2;
            float side = i % 2 == 0 ? -1f : 1f;
            float upperLength = front ? FrontUpperLength : BackUpperLength;
            float lowerLength = front ? FrontLowerLength : BackLowerLength;
            float upperRadius = front ? 0.085f : 0.115f;
            float kneeRadius = front ? 0.06f : 0.064f;
            var knee = front ? new Vector3(0, -upperLength, 0.01f) : new Vector3(0, -upperLength, 0.06f);

            float hipHeight = upperLength + lowerLength + LegClearance;
            _upperLegs[i] = Pivot(_frame, legNames[i], new Vector3(side * 0.11f, hipHeight, front ? -LegOffset : LegOffset));
            Attach(_upperLegs[i], "Upper", Tube([new Vector3(0, 0.05f, 0), knee], [upperRadius, kneeRadius], 12, spotted, capEnd: false));
            Attach(_upperLegs[i], "UpperFur", Strands(rng, OnSegment(rng, 600, new Vector3(0, 0.05f, 0), knee, upperRadius, kneeRadius),
                new Vector3(0, -0.6f, 0.3f), 0.02f, 0.035f, FurRoot, FurTip, _hair, width: 0.018f, colouring: legColour));

            // A swell of muscle where the leg meets the body: the shoulder blade in front, the big thigh behind.
            var muscle = front ? new Vector3(0.07f, 0.1f, 0.09f) : new Vector3(0.09f, 0.11f, 0.11f);
            var musclePosition = front ? new Vector3(0, -0.015f, 0) : new Vector3(0, -0.03f, 0.02f);
            Attach(_upperLegs[i], "Muscle", Ellipsoid(muscle, 16, 10, spotted), musclePosition);
            Attach(_upperLegs[i], "MuscleFur", Strands(rng, OnShape(rng, u => u * muscle + musclePosition, 450, u => u.X * side > -0.2f),
                new Vector3(0, -0.6f, 0.3f), 0.02f, 0.035f, FurRoot, FurTip, _hair, width: 0.02f, colouring: legColour));

            _lowerLegs[i] = Pivot(_upperLegs[i], "Lower", knee);
            Attach(_lowerLegs[i], "Joint", new SphereMesh { Radius = kneeRadius, Height = kneeRadius * 2f, Material = spotted });
            var ankle = new Vector3(0, -lowerLength, front ? -0.01f : -0.06f);
            float shinRadius = front ? 0.056f : 0.05f;
            float ankleRadius = front ? 0.048f : 0.044f;
            Attach(_lowerLegs[i], "Lower", Tube([Vector3.Zero, ankle], [shinRadius, ankleRadius], 12, spotted, capEnd: false));
            Attach(_lowerLegs[i], "LowerFur", Strands(rng, OnSegment(rng, 350, Vector3.Zero, ankle, shinRadius, ankleRadius),
                new Vector3(0, -0.6f, 0.3f), 0.015f, 0.025f, FurRoot, FurTip, _hair, width: 0.016f, colouring: legColour));

            // Big round paws, wide enough to spread the cat's weight on snow, furred all over. They hinge at the ankle
            // so they can lie flat when the leg is folded or stretched out at rest.
            _paws[i] = Pivot(_lowerLegs[i], "Foot", ankle);
            var pawPosition = new Vector3(0, 0.001f, -0.03f);
            var pawRadii = new Vector3(0.062f, 0.036f, 0.076f);
            Attach(_paws[i], "Paw", Ellipsoid(pawRadii, 14, 10, spotted), pawPosition);
            Attach(_paws[i], "PawFur", Strands(rng, OnShape(rng, u => u * pawRadii + pawPosition, 220, u => u.Y > -0.3f),
                new Vector3(0, -0.3f, -0.2f), 0.012f, 0.022f, FurRoot, FurTip, _hair, width: 0.014f, colouring: legColour));
        }

        // Tail: nearly as long as the body and thickly furred all the way, a chain of segments rooted inside the
        // rump so it grows out of the body, thick at its base, and can sway and curl. Rosettes give way to dark rings
        // along its outer half, ending in a dark tip.
        _tail = new Node3D[TailSegments];
        Node3D parent = _frame;
        var position = new Vector3(0, 0.5f, 0.38f);
        const float tailLength = TailSegments * TailSegmentLength;
        static float TailRadius(int segment) => segment == 0 ? 0.06f : Mathf.Lerp(0.045f, 0.038f, segment / (float)TailSegments);
        for (int i = 0; i < TailSegments; i++)
        {
            float top = TailRadius(i);
            float bottom = TailRadius(i + 1);
            float start = i * TailSegmentLength;
            Color TailColour(Vector3 p, Vector3 n)
            {
                float along = (start - p.Y) / tailLength;
                float ring = Mathf.SmoothStep(0.4f, 0.55f, along) * Mathf.SmoothStep(0.6f, 0.8f, Mathf.Sin(along * Mathf.Pi * 9f));
                float tip = Mathf.SmoothStep(0.86f, 0.93f, along);
                return coatColour(p, n).Lerp(Cream, Mathf.SmoothStep(0.2f, -0.6f, n.Z) * 0.4f).Lerp(Spot, Mathf.Max(ring * 0.85f, tip * 0.9f));
            }

            _tail[i] = Pivot(parent, "Tail", position);
            // The root is buried in the rump; every other joint is rounded so the tail bends smoothly.
            if (i > 0)
                Attach(_tail[i], "Joint", new SphereMesh { Radius = top, Height = top * 2f, Material = coat });
            Attach(_tail[i], "Segment", Tube(
                [Vector3.Zero, Vector3.Down * TailSegmentLength], [top, bottom], 10, coat, capEnd: i == TailSegments - 1));
            Attach(_tail[i], "Fluff", Strands(rng, OnTube(rng, 220, Mathf.Min(top, bottom) * 0.9f, 0f, -TailSegmentLength), new Vector3(0, -0.6f, 0),
                0.04f, 0.06f, FurRoot, FurTip, _hair, width: 0.02f, colouring: TailColour));
            parent = _tail[i];
            position = Vector3.Down * TailSegmentLength;
        }
    }

    /// <summary>
    /// A small, round cat's head: a domed skull with wide furred cheeks and a broad, straight nose bridge running down
    /// to a wide, heart-shaped nose. Spotted whisker pads sit either side of a dark philtrum above a black lip line and
    /// a pale chin. Pale grey-green almond eyes with round pupils sit flush in the face, lined in black and framed by
    /// pale fur, under small, rounded, cupped ears set wide, black-rimmed on the back with a smoky grey centre.
    /// </summary>
    private void BuildHead(Node3D head, RandomNumberGenerator rng, StandardMaterial3D spotted, Func<Vector3, Vector3, Color> spotColour)
    {
        // The face keeps the head's spots, shaded by vertex colour into its paler and darker markings.
        var face = (StandardMaterial3D)spotted.Duplicate();
        face.VertexColorUseAsAlbedo = true;
        var nose = new StandardMaterial3D { AlbedoColor = new Color(0.56f, 0.45f, 0.44f), Roughness = 0.45f };
        var nostril = new StandardMaterial3D { AlbedoColor = new Color(0.08f, 0.05f, 0.05f), Roughness = 0.6f };
        // Pale grey-green irises darkening to a ring at their edge, under a clear glossy cornea that catches the light.
        var iris = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.7f, 0.73f, 0.6f), Roughness = 0.3f,
            ClearcoatEnabled = true, Clearcoat = 1f, ClearcoatRoughness = 0f,
        };
        var limbus = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.4f, 0.42f, 0.32f), Roughness = 0.3f,
            ClearcoatEnabled = true, Clearcoat = 1f, ClearcoatRoughness = 0f,
        };
        var rim = new StandardMaterial3D { AlbedoColor = new Color(0.04f, 0.035f, 0.035f), Roughness = 0.5f };
        var pupil = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.01f, 0.01f, 0.01f), Roughness = 0.2f,
            ClearcoatEnabled = true, Clearcoat = 1f, ClearcoatRoughness = 0f,
        };
        var catchlight = new StandardMaterial3D { AlbedoColor = Colors.White, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
        var earBack = new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 1f };
        var earInside = new StandardMaterial3D { AlbedoColor = new Color(0.78f, 0.76f, 0.73f), Roughness = 1f };

        // Smoky grey forehead and crown, pale around the eyes, and a faint dark streak back from each eye's outer corner.
        var skull = new Vector3(0.085f, 0.072f, 0.09f);
        Attach(head, "Skull", Ellipsoid(skull, 32, 20, face, (p, _) =>
        {
            float x = Mathf.Abs(p.X);
            float pale = Mathf.Max(Blob(new Vector3(x, p.Y, p.Z), new Vector3(0.036f, -0.006f, -0.072f), new Vector3(0.024f, 0.009f, 0.03f)),
                Blob(new Vector3(x, p.Y, p.Z), new Vector3(0.033f, 0.023f, -0.07f), new Vector3(0.02f, 0.007f, 0.03f)));
            float streak = Blob(new Vector3(x, p.Y, p.Z), new Vector3(0.06f, -0.012f, -0.052f), new Vector3(0.007f, 0.016f, 0.014f));
            return Grey(0.86f).Lerp(Colors.White, pale).Lerp(Grey(0.45f), streak * 0.6f);
        }));
        Attach(head, "SkullFur", Strands(rng, OnShape(rng, u => u * skull, 900, u => u.Z > -0.65f), CoatDrift,
            0.012f, 0.022f, FurRoot, FurTip, _hair, width: 0.012f, colouring: spotColour));

        // Cheeks flare out below and behind the eyes into a ruff of longer, paler fur.
        var cheeks = new Vector3(0.08f, 0.05f, 0.06f);
        var cheekPosition = new Vector3(0, -0.028f, -0.03f);
        Attach(head, "Cheeks", Ellipsoid(cheeks, 20, 12, face, (_, n) => Grey(0.88f).Lerp(Colors.White, Mathf.SmoothStep(0f, -0.6f, n.Y))),
            cheekPosition);
        Attach(head, "CheekFur", Strands(rng, OnShape(rng, u => u * cheeks + cheekPosition, 500, u => Mathf.Abs(u.X) > 0.5f && u.Z > -0.6f),
            new Vector3(0, -0.4f, 0.6f), 0.02f, 0.035f, FurRoot, FurTip, _hair, width: 0.012f,
            colouring: (p, n) => spotColour(p, n).Lerp(Cream, Mathf.SmoothStep(0f, -0.5f, n.Y))));

        // The muzzle hangs off a pivot where it meets the face, so it can be drawn smaller than it is modelled.
        var muzzle = Pivot(head, "Muzzle", new Vector3(0, -0.03f, -0.07f));
        muzzle.Scale = Vector3.One * MuzzleScale;

        // A broad, straight nose bridge running from the forehead down to the nose.
        Attach(muzzle, "Bridge", Ellipsoid(new Vector3(0.03f, 0.024f, 0.056f), 16, 10, face, (_, _) => Grey(0.9f)),
            new Vector3(0, 0.026f, 0.004f));

        // A wide nose, broadest at the top and narrowing to a point above the philtrum, with dark nostrils at its sides.
        var nosePosition = new Vector3(0, 0.013f, -0.05f);
        Attach(muzzle, "Nose", Ellipsoid(u => new Vector3(u.X * 0.0125f * (0.7f + 0.4f * Mathf.Max(u.Y, -0.5f)), u.Y * 0.0085f, u.Z * 0.009f),
            14, 10, nose), nosePosition);

        // Pale chin, its top edge the dark lower lip.
        var chinPosition = new Vector3(0, -0.026f, -0.016f);
        Attach(muzzle, "Chin", Ellipsoid(new Vector3(0.018f, 0.011f, 0.019f), 14, 8, face,
            (_, n) => Colors.White.Lerp(Grey(0.15f), Mathf.SmoothStep(0.55f, 0.8f, n.Y))), chinPosition);

        foreach (float side in new[] { -1f, 1f })
        {
            Attach(muzzle, "Nostril", Ellipsoid(new Vector3(0.0028f, 0.0014f, 0.0022f), 8, 6, nostril),
                nosePosition + new Vector3(side * 0.0048f, -0.0042f, -0.0052f));

            // Whisker pads, pale and dotted with the spots the whiskers grow from, edged underneath by the black upper
            // lip and meeting in the middle at a dark philtrum running down from the nose.
            var pad = new Vector3(0.022f, 0.0145f, 0.022f);
            var padPosition = new Vector3(side * 0.016f, -0.008f, -0.027f);
            Attach(muzzle, "WhiskerPad", Ellipsoid(pad, 16, 10, face, (p, n) =>
            {
                float lip = Mathf.SmoothStep(-0.65f, -0.85f, n.Y);
                float philtrum = Mathf.SmoothStep(0.004f, 0.0015f, Mathf.Abs(p.X + padPosition.X)) * Mathf.SmoothStep(0f, 0.3f, -n.Z);
                return Colors.White.Lerp(Grey(0.15f), Mathf.Max(lip, philtrum));
            }), padPosition);
            Attach(muzzle, "Whiskers", Strands(rng, OnShape(rng, u => u * pad + padPosition, 14, u => u.X * side > 0.5f && u.Z < 0.2f),
                new Vector3(side * 0.4f, 0.2f, 0.3f), 0.06f, 0.09f, Cream, Colors.White, _hair, width: 0.003f));

            // Big, round, soft eyes look forward and a little outward, barely slanted, and sit flush in the face. A thin
            // black liner rings each one; the iris fills the eye, darkening to a ring at its edge where the eyeball
            // curves away, around a large round pupil that catches a bright point of light.
            var socket = Pivot(head, "Eye", new Vector3(side * 0.041f, 0.012f, -0.067f));
            socket.Rotation = new Vector3(0, -side * 0.3f, side * 0.04f);
            socket.Scale = Vector3.One * EyeScale;
            Attach(socket, "Rim", Ellipsoid(Almond(new Vector3(0.0192f, 0.0142f, 0.009f)), 20, 12, rim));
            Attach(socket, "Eyeball", Ellipsoid(Almond(new Vector3(0.0168f, 0.0124f, 0.009f)), 20, 12, limbus), new Vector3(0, 0, -0.0015f));
            Attach(socket, "Iris", Ellipsoid(Almond(new Vector3(0.0142f, 0.0112f, 0.009f)), 20, 12, iris), new Vector3(0, 0, -0.0025f));
            Attach(socket, "Pupil", Ellipsoid(new Vector3(0.0056f, 0.0058f, 0.0012f), 16, 10, pupil), new Vector3(0, 0, -0.0111f));
            Attach(socket, "Catchlight", Ellipsoid(new Vector3(0.0019f, 0.0019f, 0.0006f), 10, 6, catchlight), new Vector3(0.0035f, 0.004f, -0.0117f));
        }

        // Small, rounded ears set wide and turned a little outward, cupped forward, black around the back's rim with a
        // smoky grey centre, and pale grey, tufted insides.
        _ears = new Node3D[2];
        var flap = new Vector3(0.026f, 0.025f, 0.008f);
        Func<Vector3, Vector3> Cupped(Vector3 radii) => u => u * radii + new Vector3(0, 0, -0.008f * u.X * u.X);
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            _ears[i] = Pivot(head, "Ear", new Vector3(side * 0.056f, 0.05f, 0.022f));
            var tilt = Pivot(_ears[i], "Tilt", Vector3.Zero);
            tilt.Rotation = new Vector3(-0.15f, -side * 0.5f, -side * 0.45f);
            tilt.Scale = Vector3.One * EarScale;
            var flapPosition = new Vector3(0, 0.018f, 0);
            Attach(tilt, "Flap", Ellipsoid(Cupped(flap), 16, 10, earBack, (p, _) =>
            {
                float edge = Mathf.Sqrt(p.X * p.X / (flap.X * flap.X) + p.Y * p.Y / (flap.Y * flap.Y));
                return Grey(0.42f).Lerp(Grey(0.06f), Mathf.SmoothStep(0.45f, 0.75f, edge));
            }), flapPosition);
            var inner = new Vector3(0.02f, 0.019f, 0.004f);
            var innerPosition = flapPosition + new Vector3(0, -0.002f, -0.006f);
            Attach(tilt, "Inside", Ellipsoid(Cupped(inner), 12, 8, earInside), innerPosition);
            Attach(tilt, "Tuft", Strands(rng, OnShape(rng, u => Cupped(inner)(u) + innerPosition, 40, u => u.Z < 0f && u.Y < 0.4f),
                new Vector3(0, 0.3f, -0.5f), 0.01f, 0.016f, Cream, Colors.White, _hair, width: 0.006f));
        }
    }

    private static Color Grey(float value) => new(value, value, value);

    /// <summary>1 at <paramref name="centre"/>, falling off smoothly over about <paramref name="radii"/> in each direction.</summary>
    private static float Blob(Vector3 p, Vector3 centre, Vector3 radii) => Mathf.Exp(-((p - centre) / radii).LengthSquared());

    /// <summary>An ellipsoid that narrows towards its left and right ends, for the almond shape of a cat's eye.</summary>
    private static Func<Vector3, Vector3> Almond(Vector3 radii) =>
        u => new Vector3(u.X * radii.X, u.Y * radii.Y * (1f - 0.35f * u.X * u.X), u.Z * radii.Z);

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
