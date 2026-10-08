using System;
using System.Collections.Generic;
using Godot;

namespace IceAgeWorld;

/// <summary>
/// The player's woolly mammoth, built entirely in code: a humped body, domed head, curved tusks, ears, eyes,
/// a segmented trunk, a tail and thousands of hair strands, dressed in procedurally generated fur and ivory
/// textures. It also drives its own animation: walking legs, a bobbing body, a swaying trunk that curls to
/// graze, a swishing tail and flapping ears. The model faces -Z.
/// </summary>
public partial class Mammoth : Node3D
{
    private const int Seed = 7;
    private const int TrunkSegments = 4;
    private const float TrunkSegmentLength = 0.6f;
    private const float HeadDownAngle = -0.6f;

    private static readonly Color HairRoot = new(0.2f, 0.11f, 0.05f);
    private static readonly Color HairTip = new(0.52f, 0.33f, 0.17f);

    private Node3D _neck = null!;
    private Node3D _tail = null!;
    private Node3D[] _legs = [];
    private Node3D[] _trunk = [];
    private Node3D[] _ears = [];
    private ShaderMaterial _hair = null!;
    private float _walkCycle;
    private float _time;

    public override void _Ready() => Build();

    /// <summary>
    /// Advances the animation. <paramref name="speed"/> is ground speed, <paramref name="stride"/> is 0..1 how
    /// hard the animal is walking, and <paramref name="eat"/> is 0..1 how far the head is dipped to graze.
    /// </summary>
    public void Animate(float speed, float stride, float eat, float dt)
    {
        _time += dt;
        _walkCycle += speed * dt * 0.9f;

        // Diagonal pairs of legs swing together, like a real quadruped's walk.
        float swing = Mathf.Sin(_walkCycle) * stride * 0.5f;
        _legs[0].Rotation = new Vector3(swing, 0, 0);
        _legs[3].Rotation = new Vector3(swing, 0, 0);
        _legs[1].Rotation = new Vector3(-swing, 0, 0);
        _legs[2].Rotation = new Vector3(-swing, 0, 0);

        // The body rises a little on each step.
        Position = new Vector3(0, Mathf.Abs(Mathf.Sin(_walkCycle)) * 0.06f * stride, 0);

        // The head dips to graze and nods gently in step.
        _neck.Rotation = new Vector3(eat * HeadDownAngle + Mathf.Sin(_walkCycle * 2f) * 0.03f * stride, 0, 0);

        // The trunk swings side to side, lazily when idle and harder in step, each segment lagging the one
        // above like a chain of pendulums. While grazing it curls back under the head towards the mouth.
        for (int i = 0; i < _trunk.Length; i++)
        {
            float lag = i * 0.6f;
            float sway = Mathf.Sin(_time * 1.3f - lag) * 0.1f + Mathf.Sin(_walkCycle - 0.3f - lag) * 0.22f * stride;
            float hang = i == 0 ? 0.45f : -0.12f;
            float curl = eat * (i == 0 ? 0.3f : -0.5f);
            _trunk[i].Rotation = new Vector3(hang + curl, 0, sway);
        }

        // The tail hangs off the rump and swishes, more so when walking.
        float swish = Mathf.Sin(_time * 2.1f) * 0.2f + Mathf.Sin(_walkCycle * 0.5f) * 0.25f * stride;
        _tail.Rotation = new Vector3(-0.7f + Mathf.Sin(_time * 0.9f) * 0.1f, 0, swish);

        // Ears lie back against the head and flap now and then.
        float flap = Mathf.Sin(_time * 1.7f) * 0.1f + Mathf.Sin(_walkCycle * 2f) * 0.08f * stride;
        _ears[0].Rotation = new Vector3(0, 0.5f + flap, 0);
        _ears[1].Rotation = new Vector3(0, -(0.5f + flap), 0);

        // Loose hair bounces more the faster the animal moves.
        _hair.SetShaderParameter("sway_amount", 0.025f + 0.06f * stride);
    }

    private void Build()
    {
        var rng = new RandomNumberGenerator { Seed = Seed };
        var hide = ProceduralTextures.Fur(Seed, new Color(0.2f, 0.11f, 0.05f), new Color(0.5f, 0.32f, 0.17f), 0.7f);
        var ivory = ProceduralTextures.Ivory(Seed + 1);
        var dark = new StandardMaterial3D { AlbedoColor = new Color(0.07f, 0.05f, 0.04f), Roughness = 0.3f };
        _hair = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/fur.gdshader") };

        // Body: a barrel with a shoulder hump, long guard hairs hanging from the flanks and belly
        // and a shorter coat lying back over the spine.
        var bodyPosition = new Vector3(0, 2.15f, 0.1f);
        var body = Pivot(this, "Body", bodyPosition);
        Attach(body, "Hide", Ellipsoid(BodyShape, 40, 24, hide));
        Attach(body, "Skirt", Strands(rng, OnShape(rng, BodyShape, 2200, u => u.Y < 0.45f), new Vector3(0, -1f, 0), 0.5f, 0.9f));
        Attach(body, "Coat", Strands(rng, OnShape(rng, BodyShape, 1000, u => u.Y >= 0.45f), new Vector3(0, -0.7f, 0.7f), 0.35f, 0.55f));

        // Head on a neck pivot that dips to graze.
        _neck = Pivot(this, "Neck", new Vector3(0, 2.9f, -1.75f));
        Attach(_neck, "Throat", Tube([new Vector3(0, -0.2f, 0.7f), new Vector3(0, 0.15f, -0.5f)], [0.75f, 0.6f], 14, hide, capEnd: false));
        var head = Pivot(_neck, "Head", new Vector3(0, 0.25f, -0.6f));
        Attach(head, "Skull", Ellipsoid(HeadShape, 32, 20, hide));
        Attach(head, "Mane", Strands(rng, OnShape(rng, HeadShape, 700, u => u.Y > -0.2f && u.Z > -0.55f), new Vector3(0, -0.9f, 0.5f), 0.25f, 0.45f));

        foreach (float side in new[] { -1f, 1f })
        {
            Attach(head, "Eye", new SphereMesh { Radius = 0.07f, Height = 0.14f, Material = dark },
                new Vector3(side * 0.5f, 0.12f, -0.4f));
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
            _legs[i] = Pivot(this, legNames[i], new Vector3(side * 0.68f, 1.75f, front));
            Attach(_legs[i], "Column", Tube(
                [Vector3.Zero, new Vector3(0, -0.8f, 0), new Vector3(0, -1.55f, 0), new Vector3(0, -1.75f, 0)],
                [0.45f, 0.36f, 0.33f, 0.37f], 14, hide, capEnd: true));
            Attach(_legs[i], "Hair", Strands(rng, OnTube(rng, 280, 0.4f, -0.1f, -1.15f), new Vector3(0, -1f, 0), 0.35f, 0.6f));
            foreach (float a in new[] { -0.7f, 0f, 0.7f })
                Attach(_legs[i], "Nail", new SphereMesh { Radius = 0.09f, Height = 0.14f, Material = ivory },
                    new Vector3(Mathf.Sin(a) * 0.33f, -1.65f, -Mathf.Cos(a) * 0.33f));
        }

        // Tail rooted on the rump (a point on the body surface, just below its top rear edge), with a tuft of hair at the end.
        var tailRoot = bodyPosition + BodyShape(new Vector3(0, 0.45f, 0.89f).Normalized()) - Vector3.Up * 0.05f;
        _tail = Pivot(this, "Tail", tailRoot);
        Attach(_tail, "Joint", new SphereMesh { Radius = 0.14f, Height = 0.28f, Material = hide });
        Attach(_tail, "Tail", Tube(
            [Vector3.Zero, new Vector3(0, -0.6f, 0.08f), new Vector3(0, -1.2f, 0.12f)], [0.12f, 0.08f, 0.045f], 8, hide, capEnd: true));
        Attach(_tail, "Tuft", Strands(rng, OnTube(rng, 110, 0.06f, -0.95f, -1.2f, 0.1f), new Vector3(0, -1f, 0.15f), 0.35f, 0.6f));
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

    private static Node3D Pivot(Node3D parent, string name, Vector3 position)
    {
        var pivot = new Node3D { Name = name, Position = position };
        parent.AddChild(pivot);
        return pivot;
    }

    private static void Attach(Node3D parent, string name, Mesh mesh, Vector3 position = default) =>
        parent.AddChild(new MeshInstance3D { Name = name, Mesh = mesh, Position = position });

    private static Vector3 Direction(float theta, float phi) =>
        new(Mathf.Cos(phi) * Mathf.Cos(theta), Mathf.Sin(phi), Mathf.Cos(phi) * Mathf.Sin(theta));

    /// <summary>Outward normal of a deformed sphere, from finite differences of the shape function.</summary>
    private static Vector3 ShapeNormal(Func<Vector3, Vector3> shape, float theta, float phi)
    {
        const float e = 0.002f;
        var p = shape(Direction(theta, phi));
        var dTheta = shape(Direction(theta + e, phi)) - p;
        var dPhi = shape(Direction(theta, phi + e)) - p;
        var n = dPhi.Cross(dTheta);
        return n.LengthSquared() > 1e-12f ? n.Normalized() : (phi > 0 ? Vector3.Up : Vector3.Down);
    }

    /// <summary>A sphere pushed through <paramref name="shape"/>, which maps unit directions to surface points.</summary>
    private static ArrayMesh Ellipsoid(Func<Vector3, Vector3> shape, int segments, int rings, Material material)
    {
        var mesh = new MeshBuilder();
        var index = new int[rings + 1, segments + 1];
        for (int r = 0; r <= rings; r++)
        {
            float phi = -Mathf.Pi / 2f + Mathf.Pi * r / rings;
            for (int s = 0; s <= segments; s++)
            {
                float theta = Mathf.Tau * s / segments;
                index[r, s] = mesh.Add(shape(Direction(theta, phi)), ShapeNormal(shape, theta, phi), Colors.White);
            }
        }
        for (int r = 0; r < rings; r++)
        for (int s = 0; s < segments; s++)
            mesh.Quad(index[r, s], index[r, s + 1], index[r + 1, s + 1], index[r + 1, s]);
        return mesh.Commit(material);
    }

    /// <summary>A tube of varying radius swept along a path, with the cross-section frame carried along the curve.</summary>
    private static ArrayMesh Tube(Vector3[] path, float[] radii, int sides, Material material, bool capEnd)
    {
        var mesh = new MeshBuilder();
        var rings = new int[path.Length, sides + 1];
        var normal = Vector3.Zero;
        var tangent = Vector3.Zero;

        for (int i = 0; i < path.Length; i++)
        {
            tangent = (path[Math.Min(i + 1, path.Length - 1)] - path[Math.Max(i - 1, 0)]).Normalized();
            var helper = i == 0 ? (Mathf.Abs(tangent.Y) < 0.9f ? Vector3.Up : Vector3.Right) : normal;
            normal = (helper - tangent * helper.Dot(tangent)).Normalized();
            var binormal = tangent.Cross(normal);

            for (int s = 0; s <= sides; s++)
            {
                float a = Mathf.Tau * s / sides;
                var outward = normal * Mathf.Cos(a) + binormal * Mathf.Sin(a);
                rings[i, s] = mesh.Add(path[i] + outward * radii[i], outward, Colors.White);
            }
        }

        for (int i = 0; i < path.Length - 1; i++)
        for (int s = 0; s < sides; s++)
            mesh.Quad(rings[i, s], rings[i + 1, s], rings[i + 1, s + 1], rings[i, s + 1]);

        if (capEnd)
        {
            int last = path.Length - 1;
            int centre = mesh.Add(path[last], tangent, Colors.White);
            for (int s = 0; s < sides; s++)
                mesh.Tri(centre, rings[last, s + 1], rings[last, s]);
        }

        return mesh.Commit(material);
    }

    /// <summary>Random points on a deformed sphere's surface that satisfy <paramref name="where"/> (tested on the unit direction).</summary>
    private static List<(Vector3 Root, Vector3 Normal)> OnShape(RandomNumberGenerator rng, Func<Vector3, Vector3> shape, int count, Func<Vector3, bool> where)
    {
        var roots = new List<(Vector3, Vector3)>(count);
        while (roots.Count < count)
        {
            float phi = Mathf.Asin(rng.RandfRange(-1f, 1f));
            float theta = rng.Randf() * Mathf.Tau;
            var u = Direction(theta, phi);
            if (where(u))
                roots.Add((shape(u), ShapeNormal(shape, theta, phi)));
        }
        return roots;
    }

    /// <summary>Random points around a vertical cylinder between two heights.</summary>
    private static List<(Vector3 Root, Vector3 Normal)> OnTube(RandomNumberGenerator rng, int count, float radius, float topY, float bottomY, float zOffset = 0f)
    {
        var roots = new List<(Vector3, Vector3)>(count);
        for (int i = 0; i < count; i++)
        {
            float a = rng.Randf() * Mathf.Tau;
            var outward = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            roots.Add((outward * radius + new Vector3(0, rng.RandfRange(bottomY, topY), zOffset), outward));
        }
        return roots;
    }

    /// <summary>
    /// Hair: a thin, two-segment strand at each root, drifting in <paramref name="drift"/>'s direction and
    /// bending down under its own weight, darker at the root than the tip. Strands take the normal of the
    /// surface beneath them so they are lit like that surface rather than as individual slivers.
    /// </summary>
    private ArrayMesh Strands(RandomNumberGenerator rng, List<(Vector3 Root, Vector3 Normal)> roots, Vector3 drift, float minLength, float maxLength)
    {
        var mesh = new MeshBuilder();
        foreach (var (root, normal) in roots)
        {
            float length = rng.RandfRange(minLength, maxLength);
            var jitter = new Vector3(rng.RandfRange(-1f, 1f), rng.RandfRange(-1f, 1f), rng.RandfRange(-1f, 1f)) * 0.2f;
            var direction = (normal * 0.35f + drift + jitter).Normalized();
            var bent = (direction + Vector3.Down * 0.7f).Normalized();

            var mid = root + direction * length * 0.5f;
            var tip = mid + bent * length * 0.5f;
            var side = direction.Cross(normal).Normalized();
            if (side.LengthSquared() < 0.5f)
                side = direction.Cross(Vector3.Right).Normalized();

            float shade = rng.RandfRange(0.8f, 1.2f);
            var rootColour = Shade(HairRoot, shade);
            var midColour = Shade(HairRoot.Lerp(HairTip, 0.5f), shade);
            var tipColour = Shade(HairTip, shade);

            int r0 = mesh.Add(root - side * 0.035f, normal, rootColour, new Vector2(0, 0));
            int r1 = mesh.Add(root + side * 0.035f, normal, rootColour, new Vector2(0, 0));
            int m0 = mesh.Add(mid - side * 0.025f, normal, midColour, new Vector2(0.5f, 0));
            int m1 = mesh.Add(mid + side * 0.025f, normal, midColour, new Vector2(0.5f, 0));
            int t = mesh.Add(tip, normal, tipColour, new Vector2(1, 0));
            mesh.Quad(r0, r1, m1, m0);
            mesh.Tri(m0, m1, t);
        }
        return mesh.Commit(_hair);
    }

    private static Color Shade(Color colour, float amount) =>
        new(colour.R * amount, colour.G * amount, colour.B * amount);
}
