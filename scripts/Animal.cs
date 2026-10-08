using System;
using System.Collections.Generic;
using Godot;

namespace IceAgeWorld;

/// <summary>
/// How good an animal is at each thing, out of 10: the design table every animal is balanced from. The speeds,
/// jumps, turning and stamina in <see cref="AnimalStats"/> are all worked out from these, so tuning an animal means
/// changing a score here rather than a dozen numbers.
/// </summary>
public sealed record AnimalScores
{
    public required int JumpHeight { get; init; }
    public required int JumpLength { get; init; }
    public required int LandSpeed { get; init; }
    public required int WaterSpeed { get; init; }

    /// <summary>How sharply the animal turns and how quickly it gets up to speed.</summary>
    public required int Agility { get; init; }

    /// <summary>How long the animal can keep running, swimming, flying or climbing, and how fast it gets its breath back.</summary>
    public required int Stamina { get; init; }

    /// <summary>Agility in the air, for animals that fly; they can be clumsy on the ground but nimble on the wing.</summary>
    public int? FlightAgility { get; init; }
}

/// <summary>Things only some animals can do.</summary>
[Flags]
public enum Ability
{
    None = 0,
    ClimbTrees = 1,
    Fly = 2,

    /// <summary>Travels with a pack that runs in file behind its leader.</summary>
    Pack = 4,

    /// <summary>Travels with a herd that ambles along in a loose crowd and grazes whenever it stops.</summary>
    Herd = 8,
}

/// <summary>How an animal moves, feeds and fills the player's body and camera, so <see cref="Player"/> can drive any of them.</summary>
public sealed record AnimalStats
{
    public required AnimalScores Scores { get; init; }

    public Ability Abilities { get; init; }

    /// <summary>How many computer-controlled animals of the same kind travel with the player's pack or herd.</summary>
    public int Companions { get; init; }

    /// <summary>Cruising airspeed, for animals that fly. Diving goes faster, and drifting without steering slower.</summary>
    public float FlySpeed { get; init; }

    // Top speed: a 9 (the snow leopard) sprints at about 15 m/s and a 6 (the mammoth) at about 10 m/s, close to
    // the real animals' bursts; a walk is a comfortable fraction of that.
    public float SprintSpeed => Scores.LandSpeed * 1.7f;
    public float WalkSpeed => SprintSpeed * 0.4f;
    public float SwimSpeed => 0.5f + Scores.WaterSpeed * 0.8f;

    /// <summary>How high a standing jump clears, in metres: about 2.5 m for a 10, and barely a hop for a 1.</summary>
    public float JumpHeight => 0.2f + Scores.JumpHeight * 0.23f;

    /// <summary>Extra forward speed a running jump launches with, so long jumpers carry further.</summary>
    public float JumpBoost => Scores.JumpLength * 0.45f;

    /// <summary>How quickly the body swings round to face a new heading.</summary>
    public float TurnSpeed => TurnSpeedFor(Scores.Agility);

    /// <summary>How quickly the animal reaches the speed it's asked for, or stops.</summary>
    public float Acceleration => 3f + Scores.Agility * 1.1f;

    /// <summary>How quickly a flying animal banks round to a new heading. Wide, sweeping turns even at best.</summary>
    public float FlightTurnSpeed => TurnSpeedFor(Scores.FlightAgility ?? Scores.Agility) * 0.35f;

    /// <summary>Seconds of flat-out effort a full stamina bar lasts.</summary>
    public float StaminaSeconds => Scores.Stamina * 4f;

    /// <summary>Seconds of rest to refill an empty stamina bar.</summary>
    public float RecoverySeconds => 30f - Scores.Stamina * 2f;

    /// <summary>
    /// How hard swimming is compared with sprinting: strong swimmers hardly tire in the water, while poor ones
    /// wear themselves out almost as fast as running.
    /// </summary>
    public float SwimEffort => 1f - Scores.WaterSpeed / 10f;

    public bool Can(Ability ability) => (Abilities & ability) != 0;

    private static float TurnSpeedFor(int agility) => 1.5f + agility * 0.85f;

    /// <summary>How far below the water surface the animal's feet hang while it floats.</summary>
    public required float FloatDepth { get; init; }

    /// <summary>Water deeper than this (measured at the feet) slows walking down to a wade.</summary>
    public required float WadeDepth { get; init; }

    /// <summary>How far in front of the body's centre the mouth reaches when the head is lowered.</summary>
    public required float MouthDistance { get; init; }

    /// <summary>How close grass must be to the mouth to be eaten.</summary>
    public required float EatReach { get; init; }

    /// <summary>Whether the animal eats grass. Meat eaters can only drink for now.</summary>
    public required bool CanGraze { get; init; }

    /// <summary>Radius and height of the upright capsule the animal collides with.</summary>
    public required float BodyRadius { get; init; }
    public required float BodyHeight { get; init; }

    /// <summary>Height the camera orbits around, and its starting distance from there.</summary>
    public required float CameraHeight { get; init; }
    public required float CameraDistance { get; init; }
}

/// <summary>
/// A playable animal model, built entirely in code and facing -Z, that animates itself from the speed, stride
/// and head-dip <see cref="Player"/> gives it each frame. Also holds the mesh-building helpers the animals share.
/// </summary>
public abstract partial class Animal : Node3D
{
    /// <summary>Name shown on the HUD.</summary>
    public abstract string DisplayName { get; }

    public abstract AnimalStats Stats { get; }

    /// <summary>
    /// Advances the animation. <paramref name="speed"/> is ground speed, <paramref name="stride"/> is 0..1 how
    /// hard the animal is walking, and <paramref name="eat"/> is 0..1 how far the head is dipped to eat or drink.
    /// </summary>
    public abstract void Animate(float speed, float stride, float eat, float dt);

    // What the animal is doing besides walking, set before each Animate so the model can take the right pose.
    public bool IsSwimming { get; set; }
    public bool IsFlying { get; set; }
    public bool IsClimbing { get; set; }

    /// <summary>0..1 how hard a flying animal is beating its wings; 0 glides on outstretched wings.</summary>
    public float Flap { get; set; }

    /// <summary>0..1 how far a flying animal has tucked its wings in to dive.</summary>
    public float Dive { get; set; }

    /// <summary>
    /// How far each joint of a two-segment leg of the given length must bend for the leg to reach
    /// <paramref name="shorten"/> less far, with the foot staying under the hip.
    /// </summary>
    protected static float FoldAngle(float shorten, float length) =>
        Mathf.Acos(Mathf.Clamp(1f - Mathf.Max(0f, shorten) / length, -1f, 1f));

    protected static ShaderMaterial HairMaterial() => new() { Shader = GD.Load<Shader>("res://shaders/fur.gdshader") };

    protected static Node3D Pivot(Node3D parent, string name, Vector3 position)
    {
        var pivot = new Node3D { Name = name, Position = position };
        parent.AddChild(pivot);
        return pivot;
    }

    protected static void Attach(Node3D parent, string name, Mesh mesh, Vector3 position = default) =>
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

        // Only at the poles, where dTheta vanishes, is the cross product too short to trust. The test is relative
        // to the shape's size so small shapes, whose differences are tiny anyway, still get proper normals.
        float reach = dPhi.LengthSquared();
        return n.LengthSquared() > reach * reach * 1e-6f ? n.Normalized() : (phi > 0 ? Vector3.Up : Vector3.Down);
    }

    /// <summary>
    /// A sphere pushed through <paramref name="shape"/>, which maps unit directions to surface points. When
    /// <paramref name="colouring"/> is given it paints each vertex from its position and normal, for materials that
    /// use vertex colour.
    /// </summary>
    protected static ArrayMesh Ellipsoid(Func<Vector3, Vector3> shape, int segments, int rings, Material material,
        Func<Vector3, Vector3, Color>? colouring = null)
    {
        var mesh = new MeshBuilder();
        var index = new int[rings + 1, segments + 1];
        for (int r = 0; r <= rings; r++)
        {
            float phi = -Mathf.Pi / 2f + Mathf.Pi * r / rings;
            for (int s = 0; s <= segments; s++)
            {
                float theta = Mathf.Tau * s / segments;
                var point = shape(Direction(theta, phi));
                var normal = ShapeNormal(shape, theta, phi);
                index[r, s] = mesh.Add(point, normal, colouring?.Invoke(point, normal) ?? Colors.White);
            }
        }
        for (int r = 0; r < rings; r++)
        for (int s = 0; s < segments; s++)
            mesh.Quad(index[r, s], index[r, s + 1], index[r + 1, s + 1], index[r + 1, s]);
        return mesh.Commit(material);
    }

    /// <summary>A plain axis-aligned ellipsoid with the given half-extents.</summary>
    protected static ArrayMesh Ellipsoid(Vector3 radii, int segments, int rings, Material material,
        Func<Vector3, Vector3, Color>? colouring = null) =>
        Ellipsoid(u => u * radii, segments, rings, material, colouring);

    /// <summary>A tube of varying radius swept along a path, with the cross-section frame carried along the curve.</summary>
    protected static ArrayMesh Tube(Vector3[] path, float[] radii, int sides, Material material, bool capEnd)
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
    protected static List<(Vector3 Root, Vector3 Normal)> OnShape(RandomNumberGenerator rng, Func<Vector3, Vector3> shape, int count, Func<Vector3, bool> where)
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
    protected static List<(Vector3 Root, Vector3 Normal)> OnTube(RandomNumberGenerator rng, int count, float radius, float topY, float bottomY, float zOffset = 0f)
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

    /// <summary>Random points around a straight tube from <paramref name="from"/> to <paramref name="to"/>, whose radius tapers between the ends.</summary>
    protected static List<(Vector3 Root, Vector3 Normal)> OnSegment(RandomNumberGenerator rng, int count, Vector3 from, Vector3 to, float fromRadius, float toRadius)
    {
        var axis = (to - from).Normalized();
        var across = axis.Cross(Mathf.Abs(axis.X) < 0.9f ? Vector3.Right : Vector3.Up).Normalized();
        var roots = new List<(Vector3, Vector3)>(count);
        for (int i = 0; i < count; i++)
        {
            float t = rng.Randf();
            var outward = across.Rotated(axis, rng.Randf() * Mathf.Tau);
            roots.Add((from.Lerp(to, t) + outward * Mathf.Lerp(fromRadius, toRadius, t), outward));
        }
        return roots;
    }

    /// <summary>
    /// Hair: a thin, two-segment strand at each root, drifting in <paramref name="drift"/>'s direction and
    /// bending down under its own weight, shading from <paramref name="rootColour"/> to <paramref name="tipColour"/>.
    /// Strands take the normal of the surface beneath them so they are lit like that surface rather than as
    /// individual slivers. When <paramref name="colouring"/> is given, both colours are multiplied by its colour at
    /// each root, so the hair can carry the coat's pattern.
    /// </summary>
    protected static ArrayMesh Strands(RandomNumberGenerator rng, List<(Vector3 Root, Vector3 Normal)> roots, Vector3 drift,
        float minLength, float maxLength, Color rootColour, Color tipColour, Material hair, float width = 0.07f,
        Func<Vector3, Vector3, Color>? colouring = null)
    {
        var mesh = new MeshBuilder();
        foreach (var (root, normal) in roots)
        {
            var under = colouring?.Invoke(root, normal) ?? Colors.White;
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
            var rootShade = Shade(rootColour * under, shade);
            var midShade = Shade(rootColour.Lerp(tipColour, 0.5f) * under, shade);
            var tipShade = Shade(tipColour * under, shade);

            int r0 = mesh.Add(root - side * width * 0.5f, normal, rootShade, new Vector2(0, 0));
            int r1 = mesh.Add(root + side * width * 0.5f, normal, rootShade, new Vector2(0, 0));
            int m0 = mesh.Add(mid - side * width * 0.36f, normal, midShade, new Vector2(0.5f, 0));
            int m1 = mesh.Add(mid + side * width * 0.36f, normal, midShade, new Vector2(0.5f, 0));
            int t = mesh.Add(tip, normal, tipShade, new Vector2(1, 0));
            mesh.Quad(r0, r1, m1, m0);
            mesh.Tri(m0, m1, t);
        }
        return mesh.Commit(hair);
    }

    /// <summary>
    /// A round eye set into the face at <paramref name="position"/>, looking along -Z after <paramref name="rotation"/>:
    /// a thin dark rim, a coloured iris under a glossy cornea, a round pupil filling <paramref name="pupil"/> of the
    /// iris, and a bright point of reflected light.
    /// </summary>
    protected static void Eye(Node3D parent, Vector3 position, Vector3 rotation, float radius, Color iris, float pupil)
    {
        var rim = new StandardMaterial3D { AlbedoColor = new Color(0.04f, 0.035f, 0.035f), Roughness = 0.5f };
        StandardMaterial3D Glossy(Color colour) => new()
        {
            AlbedoColor = colour, Roughness = 0.3f, ClearcoatEnabled = true, Clearcoat = 1f, ClearcoatRoughness = 0f,
        };
        var catchlight = new StandardMaterial3D { AlbedoColor = Colors.White, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };

        var socket = Pivot(parent, "Eye", position);
        socket.Rotation = rotation;
        float depth = radius * 0.5f;
        Attach(socket, "Rim", Ellipsoid(new Vector3(radius * 1.15f, radius * 1.15f, depth), 16, 10, rim));
        Attach(socket, "Iris", Ellipsoid(new Vector3(radius, radius, depth), 16, 10, Glossy(iris)), new Vector3(0, 0, -radius * 0.12f));
        Attach(socket, "Pupil", Ellipsoid(new Vector3(radius * pupil, radius * pupil, radius * 0.1f), 12, 8, Glossy(new Color(0.01f, 0.01f, 0.01f))),
            new Vector3(0, 0, -depth - radius * 0.06f));
        Attach(socket, "Catchlight", Ellipsoid(new Vector3(radius * 0.14f, radius * 0.14f, radius * 0.04f), 8, 6, catchlight),
            new Vector3(radius * 0.25f, radius * 0.3f, -depth - radius * 0.12f));
    }

    private static Color Shade(Color colour, float amount) =>
        new(colour.R * amount, colour.G * amount, colour.B * amount);
}
