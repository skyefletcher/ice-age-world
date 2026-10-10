using System.Collections.Generic;
using Godot;

namespace IceAgeWorld;

/// <summary>
/// The woolly mammoth, built entirely in code: a humped body, domed head, curved tusks, ears, eyes,
/// a segmented trunk, a tail and thousands of hair strands, dressed in procedurally generated fur and ivory
/// textures. It also drives its own animation: walking legs, a bobbing body, a swaying trunk that curls to
/// graze, a swishing tail and flapping ears. The model faces -Z.
/// </summary>
public partial class Mammoth : Animal
{
    private const int Seed = 7;
    private const int TrunkSegments = 4;
    private const float TrunkSegmentLength = 0.6f;
    // The neck bends from deep in the shoulders, so a smaller angle than at the jaw brings the head just as low.
    private const float HeadDownAngle = -0.45f;

    private static readonly Vector3 BodyCentre = new(0, 2.15f, 0.1f);

    /// <summary>Top of the forelegs, which the body tips back about to sit so the front feet stay planted.</summary>
    private static readonly Vector3 Shoulder = new(0, 1.75f, -1.2f);

    // Sitting, the mammoth rocks back onto its rump like a dog, forelegs straight and hind legs stretched out in front.
    private const float SitPitch = 0.5f;

    // Lying, it rolls onto its side, as elephants do to sleep, its legs stacked and stretched out, its head and trunk on
    // the ground. The body's centre comes down to about its half-width in coat above the ground.
    private const float LieHeight = 1.3f;

    private static readonly Color HairRoot = new(0.2f, 0.11f, 0.05f);
    private static readonly Color HairTip = new(0.52f, 0.33f, 0.17f);

    private Node3D _frame = null!;
    private Node3D _neck = null!;
    private Node3D _tail = null!;
    private Node3D[] _legs = [];
    private Node3D[] _trunk = [];
    private Node3D[] _ears = [];
    private ShaderMaterial _hair = null!;
    private float _walkCycle;
    private float _time;

    public override string DisplayName => "Woolly mammoth";
    public override string YoungName => "Woolly mammoth calf";

    public override AnimalStats Stats { get; } = new()
    {
        Scores = new() { JumpHeight = 1, JumpLength = 1, LandSpeed = 6, WaterSpeed = 4, Agility = 3, Stamina = 9 },
        Abilities = Ability.Herd,
        Companions = 4,
        FloatDepth = 2.2f,
        WadeDepth = 0.6f,
        MouthDistance = 3f,
        EatReach = 2f,
        CanGraze = true,
        BodyRadius = 1.3f,
        BodyHeight = 3.6f,
        CameraHeight = 3f,
        CameraDistance = 11f,
    };

    public override void _Ready() => Build();

    public override void Animate(float speed, float stride, float eat, float dt)
    {
        _time += dt;
        _walkCycle += speed * dt * 0.9f;

        // Sitting tips the body back about the shoulders; lying rolls it onto its left side. Either way the frame moves
        // so the body's centre ends up where the pose puts it, rather than swinging round the frame's origin.
        float pitch = Pose(0f, SitPitch, 0f);
        float roll = Pose(0f, 0f, Mathf.Pi / 2f);
        var basis = Basis.FromEuler(new Vector3(pitch, 0, roll));
        var sitCentre = BodyCentre.Rotated(Vector3.Right, SitPitch) + TipAbout(Shoulder, SitPitch);
        var centre = Pose(BodyCentre, sitCentre, new Vector3(0, LieHeight, BodyCentre.Z));

        // The body rises a little on each step.
        _frame.Position = centre - basis * BodyCentre + Vector3.Up * Mathf.Abs(Mathf.Sin(_walkCycle)) * 0.06f * stride;
        _frame.Rotation = new Vector3(pitch, 0, roll);

        // Diagonal pairs of legs swing together, like a real quadruped's walk. Sitting, the forelegs stand straight
        // and the hind legs lie along the ground in front; lying, the legs stretch out a little fore and aft, the
        // upper pair sagging onto the lower, whose feet rest on the ground.
        float swing = Mathf.Sin(_walkCycle) * stride * 0.5f;
        float front = Pose(0f, -SitPitch, -0.2f);
        float back = Pose(0f, 1.5f - SitPitch, 0.25f);
        float under = Pose(0f, 0f, -0.2f);
        float over = Pose(0f, 0f, -0.4f);
        _legs[0].Rotation = new Vector3(swing + front, 0, under);
        _legs[3].Rotation = new Vector3(swing + back, 0, over);
        _legs[1].Rotation = new Vector3(-swing + front, 0, over);
        _legs[2].Rotation = new Vector3(-swing + back, 0, under);

        // The head dips to graze and nods gently in step. Sitting, it tips forward to look ahead; lying, it rests on
        // the ground.
        _neck.Rotation = new Vector3(eat * HeadDownAngle + Mathf.Sin(_walkCycle * 2f) * 0.03f * stride + Pose(0f, -SitPitch * 0.7f, 0f),
            Pose(0f, 0f, Limp(0.3f, 0.45f)), 0);

        // The trunk swings side to side, lazily when idle and harder in step, each segment lagging the one
        // above like a chain of pendulums. While grazing it curls back under the head towards the mouth.
        // Lying, it flops down onto the ground, still stirring until it dies.
        for (int i = 0; i < _trunk.Length; i++)
        {
            float lag = i * 0.6f;
            float sway = (Mathf.Sin(_time * 1.3f - lag) * 0.1f + Mathf.Sin(_walkCycle - 0.3f - lag) * 0.22f * stride) * Alive;
            float hang = i == 0 ? 0.45f : -0.12f;
            float curl = eat * (i == 0 ? 0.3f : -0.5f);
            _trunk[i].Rotation = new Vector3(hang + curl, 0, sway + Pose(0f, 0f, i == 0 ? -1.1f : -0.15f));
        }

        // The tail hangs off the rump and swishes, more so when walking. At rest it lies out along the ground.
        float swish = (Mathf.Sin(_time * 2.1f) * 0.2f + Mathf.Sin(_walkCycle * 0.5f) * 0.25f * stride) * Alive;
        _tail.Rotation = new Vector3(Pose(-0.7f, -1.4f - SitPitch, -0.7f) + Mathf.Sin(_time * 0.9f) * 0.1f * Alive, 0, swish + Pose(0f, 0f, -1.1f));

        // Ears lie back against the head and flap now and then.
        float flap = (Mathf.Sin(_time * 1.7f) * 0.1f + Mathf.Sin(_walkCycle * 2f) * 0.08f * stride) * Alive;
        _ears[0].Rotation = new Vector3(0, 0.5f + flap, 0);
        _ears[1].Rotation = new Vector3(0, -(0.5f + flap), 0);

        // Loose hair bounces more the faster the animal moves.
        _hair.SetShaderParameter("sway_amount", (0.025f + 0.06f * stride) * Alive);
    }

    private void Build()
    {
        var rng = new RandomNumberGenerator { Seed = Seed };
        var hide = ProceduralTextures.Fur(Seed, new Color(0.2f, 0.11f, 0.05f), new Color(0.5f, 0.32f, 0.17f), 0.7f);
        var ivory = ProceduralTextures.Ivory(Seed + 1);
        var dark = new StandardMaterial3D { AlbedoColor = new Color(0.07f, 0.05f, 0.04f), Roughness = 0.3f };
        _hair = HairMaterial();

        // Body: a barrel with a shoulder hump, long guard hairs hanging from the flanks and belly
        // and a shorter coat lying back over the spine.
        // Everything hangs off a frame that can bob, tip back to sit and roll over to lie, without disturbing the yaw Player sets.
        _frame = Pivot(this, "Frame", Vector3.Zero);

        var bodyPosition = BodyCentre;
        var body = Pivot(_frame, "Body", bodyPosition);
        Attach(body, "Hide", Ellipsoid(BodyShape, 40, 24, hide));
        Attach(body, "Skirt", Hair(rng, OnShape(rng, BodyShape, 2200, u => u.Y < 0.45f), new Vector3(0, -1f, 0), 0.5f, 0.9f));
        Attach(body, "Coat", Hair(rng, OnShape(rng, BodyShape, 1000, u => u.Y >= 0.45f), new Vector3(0, -0.7f, 0.7f), 0.35f, 0.55f));

        // Head on a neck that bends at the shoulders to graze. The pivot sits inside the body, so the base of
        // the neck stays buried in the shoulders however far the head dips, and only the head end swings down.
        _neck = Pivot(_frame, "Neck", new Vector3(0, 2.7f, -1f));
        var throatEnd = new Vector3(0, 0.35f, -1.25f);
        Attach(_neck, "Throat", Tube([throatEnd, Vector3.Zero], [0.6f, 0.8f], 14, hide, capEnd: true));
        Attach(_neck, "ThroatHair", Hair(rng, OnSegment(rng, 500, Vector3.Zero, throatEnd, 0.8f, 0.6f),
            new Vector3(0, -0.8f, 0.4f), 0.3f, 0.5f));
        var head = Pivot(_neck, "Head", new Vector3(0, 0.45f, -1.35f));
        Attach(head, "Skull", Ellipsoid(HeadShape, 32, 20, hide));
        Attach(head, "Mane", Hair(rng, OnShape(rng, HeadShape, 700, u => u.Y > -0.2f && u.Z > -0.55f), new Vector3(0, -0.9f, 0.5f), 0.25f, 0.45f));

        foreach (float side in new[] { -1f, 1f })
        {
            AsEye(Attach(head, "Eye", new SphereMesh { Radius = 0.07f, Height = 0.14f, Material = dark },
                new Vector3(side * 0.5f, 0.12f, -0.4f)));
            Attach(head, "Tusk", Tusk(side, ivory));
        }

        _ears = new Node3D[2];
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            _ears[i] = Pivot(head, "Ear", new Vector3(side * 0.48f, 0.2f, 0.1f));
            Attach(_ears[i], "Flap", Ellipsoid(u => new Vector3(u.X * 0.05f, u.Y * 0.28f, u.Z * 0.2f), 16, 10, hide),
                new Vector3(side * 0.1f, -0.2f, 0.1f));
        }

        // Trunk: a chain of tapering segments, each hanging from the end of the one above.
        _trunk = new Node3D[TrunkSegments];
        Node3D parent = head;
        var position = new Vector3(0, -0.55f, -0.5f);
        for (int i = 0; i < TrunkSegments; i++)
        {
            float top = Mathf.Lerp(0.3f, 0.13f, i / (float)TrunkSegments);
            float bottom = Mathf.Lerp(0.3f, 0.13f, (i + 1) / (float)TrunkSegments);
            _trunk[i] = Pivot(parent, "Trunk", position);
            // A ball at each joint hides the hinge when the segment below bends.
            Attach(_trunk[i], "Joint", new SphereMesh { Radius = top, Height = top * 2f, Material = hide });
            Attach(_trunk[i], "Segment", Tube(
                [Vector3.Zero, Vector3.Down * TrunkSegmentLength], [top, bottom], 12, hide, capEnd: i == TrunkSegments - 1));
            parent = _trunk[i];
            position = Vector3.Down * TrunkSegmentLength;
        }
        Attach(_trunk[^1], "Nostrils", new SphereMesh { Radius = 0.07f, Height = 0.14f, Material = dark },
            new Vector3(0, -TrunkSegmentLength, -0.06f));

        // Legs: thick columns that flare into round feet, with hair over the upper half and toenails in front.
        _legs = new Node3D[4];
        string[] legNames = ["LegFrontLeft", "LegFrontRight", "LegBackLeft", "LegBackRight"];
        for (int i = 0; i < 4; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            float front = i < 2 ? -1.2f : 1.25f;
            _legs[i] = Pivot(_frame, legNames[i], new Vector3(side * 0.68f, 1.75f, front));
            Attach(_legs[i], "Column", Tube(
                [Vector3.Zero, new Vector3(0, -0.8f, 0), new Vector3(0, -1.55f, 0), new Vector3(0, -1.75f, 0)],
                [0.45f, 0.36f, 0.33f, 0.37f], 14, hide, capEnd: true));
            Attach(_legs[i], "Hair", Hair(rng, OnTube(rng, 280, 0.4f, -0.1f, -1.15f), new Vector3(0, -1f, 0), 0.35f, 0.6f));
            foreach (float a in new[] { -0.7f, 0f, 0.7f })
                Attach(_legs[i], "Nail", new SphereMesh { Radius = 0.09f, Height = 0.14f, Material = ivory },
                    new Vector3(Mathf.Sin(a) * 0.33f, -1.65f, -Mathf.Cos(a) * 0.33f));
        }

        // Tail rooted on the rump (a point on the body surface, just below its top rear edge), with a tuft of hair at the end.
        var tailRoot = bodyPosition + BodyShape(new Vector3(0, 0.45f, 0.89f).Normalized()) - Vector3.Up * 0.05f;
        _tail = Pivot(_frame, "Tail", tailRoot);
        Attach(_tail, "Joint", new SphereMesh { Radius = 0.14f, Height = 0.28f, Material = hide });
        Attach(_tail, "Tail", Tube(
            [Vector3.Zero, new Vector3(0, -0.6f, 0.08f), new Vector3(0, -1.2f, 0.12f)], [0.12f, 0.08f, 0.045f], 8, hide, capEnd: true));
        Attach(_tail, "Tuft", Hair(rng, OnTube(rng, 110, 0.06f, -0.95f, -1.2f, 0.1f), new Vector3(0, -1f, 0.15f), 0.35f, 0.6f));
    }

    /// <summary>Barrel body: a stretched sphere with a shoulder hump and a back that slopes down to the rump.</summary>
    private static Vector3 BodyShape(Vector3 u)
    {
        var p = new Vector3(u.X * 1.15f, u.Y * 1.2f, u.Z * 2.1f);
        if (u.Y > 0)
        {
            float hump = Mathf.Exp(-Mathf.Pow((p.Z + 0.9f) / 1f, 2f)) * 0.55f;
            float slope = Mathf.SmoothStep(-0.5f, 2.1f, p.Z) * 0.35f;
            p.Y += (hump - slope) * u.Y;
        }
        return p;
    }

    /// <summary>Tall head with the high dome a mammoth has above its forehead.</summary>
    private static Vector3 HeadShape(Vector3 u)
    {
        var p = new Vector3(u.X * 0.62f, u.Y * 0.85f, u.Z * 0.72f);
        if (u.Y > 0)
            p.Y += 0.12f * u.Y * (1f - Mathf.SmoothStep(-0.2f, 0.6f, p.Z));
        return p;
    }

    private ArrayMesh Hair(RandomNumberGenerator rng, List<(Vector3 Root, Vector3 Normal)> roots, Vector3 drift, float minLength, float maxLength) =>
        Strands(rng, roots, drift, minLength, maxLength, HairRoot, HairTip, _hair);

    private static ArrayMesh Tusk(float side, Material ivory)
    {
        // A cubic curve that sweeps out, down and forward from the jaw, then up and inward at the tip.
        Vector3 p0 = new(side * 0.3f, -0.45f, -0.45f);
        Vector3 p1 = new(side * 0.65f, -1.4f, -1f);
        Vector3 p2 = new(side * 0.6f, -1.5f, -2.2f);
        Vector3 p3 = new(side * 0.2f, -0.7f, -2.55f);

        const int steps = 14;
        var path = new Vector3[steps + 1];
        var radii = new float[steps + 1];
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            path[i] = p0.BezierInterpolate(p1, p2, p3, t);
            radii[i] = Mathf.Lerp(0.12f, 0.03f, Mathf.Pow(t, 1.5f));
        }
        return Tube(path, radii, 10, ivory, capEnd: true);
    }
}
