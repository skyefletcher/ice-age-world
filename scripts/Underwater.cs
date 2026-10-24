using System.Collections.Generic;
using Godot;

namespace IceAgeWorld;

/// <summary>
/// The world under the small lakes, for a diver to swim down into (the ocean has its own, see <see cref="Seabed"/>).
/// Their beds are green with waterweed, with the odd boulder dropped by the glaciers. Schools of arctic char, the fish
/// of cold, deep ice-age lakes, cruise about and dart away from a diver. On the bed lie the otter's food: green sea
/// urchins, the spiny kind of the cold northern seas, and clams, which it carries up to eat floating on its back.
/// </summary>
public partial class Underwater : Node3D
{
    /// <summary>What lies on the bed for an otter to eat.</summary>
    public enum ShellKind { Urchin, Clam }

    /// <summary>A sea urchin or clam on the lake bed. Once taken, another grows in its place after a while.</summary>
    public sealed class Shellfish
    {
        public required ShellKind Kind { get; init; }
        public required Node3D Model { get; init; }
        public float Regrow { get; set; }
        public bool IsTaken => Regrow > 0f;
        public string Name => NameOf(Kind);
    }

    /// <summary>A school of fish wandering about one lake, every fish keeping its own place in the school.</summary>
    private sealed class School
    {
        public required Lake Lake { get; init; }
        public Vector3 Centre;
        public Vector3 Target;
        public Vector3 Velocity;
        public readonly List<(Node3D Fish, Node3D Tail, Vector3 Offset, float Phase)> Fish = [];
    }

    /// <summary>Seconds before a new urchin or clam turns up where one was taken.</summary>
    private const float RegrowSeconds = 90f;

    /// <summary>How close a diver can come before a school bolts, and how fast fish cruise and flee.</summary>
    private const float FishShyness = 6f;
    private const float CruiseSpeed = 1.2f;
    private const float FleeSpeed = 5f;

    /// <summary>The diving animal the fish keep away from.</summary>
    public Node3D? Diver { get; set; }

    private Terrain _terrain = null!;
    private readonly List<Shellfish> _shellfish = [];
    private readonly List<School> _schools = [];
    private RandomNumberGenerator _rng = new();
    private float _time;

    private static ArrayMesh? _urchinBody, _urchinSpines, _clamShell, _fishBody, _fishTail;

    public static string NameOf(ShellKind kind) => kind == ShellKind.Urchin ? "sea urchin" : "clam";

    /// <summary>How much an urchin or clam fills an otter: a clam has more meat in it than a spiny urchin.</summary>
    public static float FoodIn(ShellKind kind) => kind == ShellKind.Urchin ? 20f : 25f;

    public void Build(Terrain terrain)
    {
        _terrain = terrain;
        _rng = new RandomNumberGenerator { Seed = (ulong)terrain.Seed + 20 };
        var patches = new FastNoiseLite { Seed = terrain.Seed + 20, NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth, Frequency = 0.03f };

        // The ocean has a world of its own under it, its reefs and kelp forests (see Seabed).
        foreach (var lake in terrain.Lakes)
        {
            if (lake == terrain.Ocean)
                continue;
            Plant(lake, patches, 90, minDepth: 1f, reach: (0.25f, 0.55f), WeedMesh(), sway: 0.25f);
            Boulders(lake, 6);
            AddShellfishBed(lake, 6);
            for (int i = 0; i < 2; i++)
                AddSchool(lake, 7);
        }
    }

    /// <summary>The urchin or clam within <paramref name="reach"/> of <paramref name="point"/>, the nearest if several, or null.</summary>
    public Shellfish? ShellfishNear(Vector3 point, float reach)
    {
        Shellfish? nearest = null;
        float best = reach;
        foreach (var shellfish in _shellfish)
        {
            if (shellfish.IsTaken)
                continue;
            float distance = point.DistanceTo(shellfish.Model.GlobalPosition);
            if (distance < best)
            {
                best = distance;
                nearest = shellfish;
            }
        }
        return nearest;
    }

    /// <summary>Takes an urchin or clam off the bed; another grows there in time.</summary>
    public void Take(Shellfish shellfish)
    {
        shellfish.Model.Visible = false;
        shellfish.Regrow = RegrowSeconds;
    }

    /// <summary>A new model of an urchin or clam, e.g. for an otter to hold in its paws.</summary>
    public static Node3D Model(ShellKind kind)
    {
        var model = new Node3D { Name = kind.ToString() };
        if (kind == ShellKind.Urchin)
        {
            model.AddChild(new MeshInstance3D { Mesh = _urchinBody ??= UrchinBody() });
            model.AddChild(new MeshInstance3D { Mesh = _urchinSpines ??= UrchinSpines() });
        }
        else
        {
            // Two valves, the top one a little open.
            _clamShell ??= ClamShell();
            model.AddChild(new MeshInstance3D { Mesh = _clamShell, Position = new Vector3(0, 0.03f, 0), Rotation = new Vector3(-0.12f, 0, 0) });
            model.AddChild(new MeshInstance3D { Mesh = _clamShell, Position = new Vector3(0, 0.03f, 0), Rotation = new Vector3(Mathf.Pi, 0, 0) });
        }
        return model;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _time += dt;

        foreach (var shellfish in _shellfish)
        {
            if (!shellfish.IsTaken)
                continue;
            shellfish.Regrow -= dt;
            if (!shellfish.IsTaken)
                shellfish.Model.Visible = true;
        }

        foreach (var school in _schools)
            Swim(school, dt);
    }

    /// <summary>
    /// A school cruises from one spot in its lake to another at mid-water, and bolts away from a diver that comes too
    /// close. Each fish holds its place in the school, weaving a little, its tail beating faster the faster it goes.
    /// </summary>
    private void Swim(School school, float dt)
    {
        var lake = school.Lake;
        bool fleeing = false;
        if (Diver is not null && Diver.GlobalPosition.DistanceTo(school.Centre) < FishShyness)
        {
            var away = (school.Centre - Diver.GlobalPosition) with { Y = 0f };
            school.Target = InLake(lake, school.Centre + (away.LengthSquared() > 0.01f ? away.Normalized() : Vector3.Forward) * 15f);
            fleeing = true;
        }
        else if (school.Centre.DistanceTo(school.Target) < 2f)
        {
            school.Target = MidWater(lake);
        }

        float speed = fleeing ? FleeSpeed : CruiseSpeed;
        var wanted = (school.Target - school.Centre).Normalized() * speed;
        school.Velocity = school.Velocity.MoveToward(wanted, (fleeing ? 12f : 1.5f) * dt);
        school.Centre += school.Velocity * dt;

        // Stay between the bed and the surface, or just under the surface in the shallows.
        float bed = _terrain.GetHeight(school.Centre.X, school.Centre.Z);
        float low = Mathf.Min(bed + 0.8f, lake.Surface - 0.8f);
        school.Centre.Y = Mathf.Clamp(school.Centre.Y, low, lake.Surface - 0.8f);

        var heading = school.Velocity with { Y = school.Velocity.Y * 0.3f };
        float beat = 6f + school.Velocity.Length() * 4f;
        foreach (var (fish, tail, offset, phase) in school.Fish)
        {
            var weave = new Vector3(Mathf.Sin(_time * 0.7f + phase), Mathf.Sin(_time * 0.5f + phase * 1.3f) * 0.4f, Mathf.Cos(_time * 0.6f + phase)) * 0.4f;
            fish.Position = school.Centre + offset + weave;
            if (new Vector2(heading.X, heading.Z).LengthSquared() > 0.0001f)
                fish.LookAt(fish.GlobalPosition + heading, Vector3.Up);
            tail.Rotation = new Vector3(0, Mathf.Sin(_time * beat + phase) * 0.45f, 0);
        }
    }

    /// <summary>
    /// Plants waterweed in patches over the lake bed, where it is deep enough, each plant reaching a share
    /// (<paramref name="reach"/>) of the way up to the surface, as it grows up towards the light.
    /// </summary>
    private void Plant(Lake lake, FastNoiseLite patches, int count, float minDepth, (float Low, float High) reach, ArrayMesh mesh, float sway)
    {
        var transforms = new List<Transform3D>();
        for (int attempt = 0; attempt < count * 4 && transforms.Count < count; attempt++)
        {
            var spot = SpotOnBed(lake);
            if (lake.Surface - spot.Y < minDepth || patches.GetNoise2D(spot.X, spot.Z) < 0.05f)
                continue;
            float height = (lake.Surface - spot.Y) * _rng.RandfRange(reach.Low, reach.High);
            var basis = Basis.FromEuler(new Vector3(0, _rng.Randf() * Mathf.Tau, 0)).Scaled(new Vector3(1f, height, 1f));
            transforms.Add(new Transform3D(basis, spot));
        }
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/weed.gdshader") };
        material.SetShaderParameter("sway_amount", sway);
        AddMultiMesh(mesh, transforms, material);
    }

    /// <summary>Boulders dropped by the glaciers, half sunk in the silt and greened with algae.</summary>
    private void Boulders(Lake lake, int count)
    {
        var transforms = new List<Transform3D>();
        for (int i = 0; i < count; i++)
        {
            var spot = SpotOnBed(lake);
            if (lake.Surface - spot.Y < 1.5f)
                continue;
            float size = _rng.RandfRange(0.4f, 2.2f);
            var scale = new Vector3(size * _rng.RandfRange(0.8f, 1.3f), size * _rng.RandfRange(0.5f, 0.8f), size * _rng.RandfRange(0.8f, 1.3f));
            var basis = Basis.FromEuler(new Vector3(_rng.RandfRange(-0.2f, 0.2f), _rng.Randf() * Mathf.Tau, _rng.RandfRange(-0.2f, 0.2f))).Scaled(scale);
            transforms.Add(new Transform3D(basis, spot + Vector3.Down * scale.Y * 0.3f));
        }
        var material = new StandardMaterial3D { AlbedoColor = new Color(0.36f, 0.4f, 0.36f), Roughness = 0.95f };
        AddMultiMesh(new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 12, Rings = 6 }, transforms, material);
    }

    private void AddMultiMesh(Mesh mesh, List<Transform3D> transforms, Material material)
    {
        var multi = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = mesh, InstanceCount = transforms.Count };
        for (int i = 0; i < transforms.Count; i++)
            multi.SetInstanceTransform(i, transforms[i]);
        AddChild(new MultiMeshInstance3D { Multimesh = multi, MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    /// <summary>
    /// A bed of urchins and clams: they gather together on the bottom rather than spreading out evenly, so a diver that
    /// finds one finds a meal. Beds lie below the shallows, out of reach of anything that only wades.
    /// </summary>
    private void AddShellfishBed(Lake lake, int count)
    {
        Vector3 centre;
        int tries = 0;
        do
            centre = SpotOnBed(lake);
        while (lake.Surface - centre.Y < 2.5f && ++tries < 20);

        for (int i = 0; i < count; i++)
        {
            var kind = _rng.Randf() < 0.5f ? ShellKind.Urchin : ShellKind.Clam;
            float a = _rng.Randf() * Mathf.Tau, r = _rng.RandfRange(0.3f, 4f);
            float x = centre.X + Mathf.Cos(a) * r, z = centre.Z + Mathf.Sin(a) * r;
            var model = Model(kind);
            model.Position = new Vector3(x, _terrain.GetHeight(x, z), z);
            model.Rotation = new Vector3(0, _rng.Randf() * Mathf.Tau, 0);
            AddChild(model);
            _shellfish.Add(new Shellfish { Kind = kind, Model = model });
        }
    }

    private void AddSchool(Lake lake, int size)
    {
        var school = new School { Lake = lake };
        school.Centre = MidWater(lake);
        school.Target = MidWater(lake);
        for (int i = 0; i < size; i++)
        {
            var fish = new Node3D { Name = "Char" };
            fish.AddChild(new MeshInstance3D { Mesh = _fishBody ??= FishBody() });
            var tail = new Node3D { Position = new Vector3(0, 0, 0.2f) };
            tail.AddChild(new MeshInstance3D { Mesh = _fishTail ??= FishTail() });
            fish.AddChild(tail);
            AddChild(fish);
            // A school is loose and longer than it is wide, every fish at least a body-length from the next.
            var offset = new Vector3(_rng.RandfRange(-1.5f, 1.5f), _rng.RandfRange(-0.6f, 0.6f), _rng.RandfRange(-2.5f, 2.5f));
            school.Fish.Add((fish, tail, offset, _rng.Randf() * Mathf.Tau));
        }
        _schools.Add(school);
    }

    /// <summary>A random spot on the bed of a lake, within its water line.</summary>
    private Vector3 SpotOnBed(Lake lake)
    {
        float r = lake.Radius * 0.95f * Mathf.Sqrt(_rng.Randf());
        float a = _rng.Randf() * Mathf.Tau;
        float x = lake.Centre.X + Mathf.Cos(a) * r, z = lake.Centre.Y + Mathf.Sin(a) * r;
        return new Vector3(x, _terrain.GetHeight(x, z), z);
    }

    /// <summary>A random spot partway between the bed and the surface, where fish cruise.</summary>
    private Vector3 MidWater(Lake lake)
    {
        // Fish keep out of the shallows, where there isn't room for a school.
        Vector3 bed;
        int tries = 0;
        do
            bed = SpotOnBed(lake);
        while (lake.Surface - bed.Y < 3f && ++tries < 20);
        return bed with { Y = Mathf.Lerp(bed.Y + 1f, lake.Surface - 1f, _rng.RandfRange(0.2f, 0.8f)) };
    }

    /// <summary>The point pulled back inside the lake if it has strayed past the water line.</summary>
    private static Vector3 InLake(Lake lake, Vector3 point)
    {
        var flat = new Vector2(point.X, point.Z) - lake.Centre;
        float limit = lake.Radius * 0.9f;
        if (flat.Length() > limit)
            flat = flat.Normalized() * limit;
        return new Vector3(lake.Centre.X + flat.X, point.Y, lake.Centre.Y + flat.Y);
    }

    // ---- Meshes. Plants are built one unit tall and stretched to height, so the shader knows how far up a vertex is.

    /// <summary>Waterweed: a bushy stem of short green leaves, like the pondweeds of cold lakes.</summary>
    private static ArrayMesh WeedMesh() => PlantMesh(blades: 20, bladeLength: 0.25f, bladeWidth: 0.1f,
        stalk: new Color(0.16f, 0.3f, 0.12f), tip: new Color(0.35f, 0.55f, 0.22f), stalkWidth: 0.03f);

    private static ArrayMesh PlantMesh(int blades, float bladeLength, float bladeWidth, Color stalk, Color tip, float stalkWidth)
    {
        var mesh = new MeshBuilder();
        // The stalk is two crossed ribbons, so it looks solid from any side.
        foreach (var side in new[] { Vector3.Right, Vector3.Back })
        {
            var normal = side.Cross(Vector3.Up);
            int a = mesh.Add(-side * stalkWidth, normal, stalk);
            int b = mesh.Add(side * stalkWidth, normal, stalk);
            int c = mesh.Add(side * stalkWidth * 0.5f + Vector3.Up, normal, tip);
            int d = mesh.Add(-side * stalkWidth * 0.5f + Vector3.Up, normal, tip);
            mesh.Quad(a, b, c, d);
        }

        // Blades spiral round the stalk at the golden angle, so they don't stack up on one side.
        for (int k = 0; k < blades; k++)
        {
            float y = (k + 1f) / (blades + 1f);
            float angle = k * 2.4f;
            var outward = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            var across = outward.Cross(Vector3.Up);
            var colour = stalk.Lerp(tip, y);
            // Each blade rises as it reaches out, its length measured in the stretched height too.
            float rise = 0.6f / (blades + 1f);
            var root = new Vector3(0, y, 0);
            var end = outward * bladeLength + Vector3.Up * (y + rise);
            int a = mesh.Add(root - across * bladeWidth * 0.2f, Vector3.Up, colour);
            int b = mesh.Add(root + across * bladeWidth * 0.2f, Vector3.Up, colour);
            int c = mesh.Add(end + across * bladeWidth * 0.5f, Vector3.Up, colour.Lightened(0.1f));
            int d = mesh.Add(end - across * bladeWidth * 0.5f, Vector3.Up, colour.Lightened(0.1f));
            mesh.Quad(a, b, c, d);
        }
        return mesh.Commit(new StandardMaterial3D());
    }

    private static StandardMaterial3D Painted(float roughness = 0.6f) =>
        new() { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = roughness };

    /// <summary>A green sea urchin's test: a flattened ball, olive green.</summary>
    private static ArrayMesh UrchinBody() =>
        Animal.Ellipsoid(new Vector3(0.12f, 0.08f, 0.12f), 14, 8, Painted(),
            (p, _) => new Color(0.3f, 0.36f, 0.12f).Darkened(Mathf.Clamp(-p.Y * 4f, 0f, 0.4f)));

    /// <summary>The urchin's spines, sticking straight out all over its top and sides.</summary>
    private static ArrayMesh UrchinSpines()
    {
        var rng = new RandomNumberGenerator { Seed = 77 };
        var mesh = new MeshBuilder();
        var colour = new Color(0.4f, 0.45f, 0.18f);
        for (int i = 0; i < 120; i++)
        {
            // Spread over the upper part of the body; the underside rests on the bed.
            var dir = new Vector3(rng.RandfRange(-1f, 1f), rng.RandfRange(-0.3f, 1f), rng.RandfRange(-1f, 1f)).Normalized();
            var root = dir * new Vector3(0.12f, 0.08f, 0.12f);
            var tipPoint = root + dir * 0.1f;
            var across = dir.Cross(Mathf.Abs(dir.Y) < 0.9f ? Vector3.Up : Vector3.Right).Normalized() * 0.006f;
            var up = dir.Cross(across).Normalized() * 0.006f;
            // Two crossed slivers make each spine.
            foreach (var side in new[] { across, up })
            {
                int a = mesh.Add(root - side, dir, colour);
                int b = mesh.Add(root + side, dir, colour);
                int c = mesh.Add(tipPoint, dir, colour.Lightened(0.25f));
                mesh.Tri(a, b, c);
            }
        }
        var material = Painted();
        material.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        return mesh.Commit(material);
    }

    /// <summary>One valve of a clam: a shallow, rounded dome, banded cream and brown with its growth rings.</summary>
    private static ArrayMesh ClamShell() =>
        Animal.Ellipsoid(u => u.Y < 0f ? u * new Vector3(0.13f, 0.005f, 0.1f) : u * new Vector3(0.13f, 0.045f, 0.1f), 18, 8, Painted(0.5f),
            (p, _) =>
            {
                float ring = new Vector2(p.X, p.Z + 0.1f).Length();
                float band = 0.5f + 0.5f * Mathf.Sin(ring * 120f);
                return new Color(0.82f, 0.76f, 0.62f).Lerp(new Color(0.48f, 0.38f, 0.28f), band * 0.6f);
            });

    /// <summary>
    /// An arctic char, half a metre long: a slim, tapering body, dark olive above, silvery on the flanks, with the
    /// orange-red belly the males wear. Faces -Z.
    /// </summary>
    private static ArrayMesh FishBody() =>
        Animal.Ellipsoid(u =>
            {
                var p = u * new Vector3(0.05f, 0.08f, 0.25f);
                float taper = 1f - 0.65f * Mathf.SmoothStep(-0.05f, 0.25f, p.Z);
                return new Vector3(p.X * taper, p.Y * taper, p.Z);
            }, 14, 10, Painted(0.3f),
            (p, _) =>
            {
                var back = new Color(0.18f, 0.24f, 0.18f);
                var flank = new Color(0.68f, 0.7f, 0.68f);
                var belly = new Color(0.86f, 0.38f, 0.16f);
                return p.Y > 0f ? flank.Lerp(back, Mathf.SmoothStep(0f, 0.05f, p.Y)) : flank.Lerp(belly, Mathf.SmoothStep(-0.01f, -0.05f, p.Y));
            });

    /// <summary>The char's forked tail fin, hanging from a pivot at the base of the tail so it can beat.</summary>
    private static ArrayMesh FishTail()
    {
        var mesh = new MeshBuilder();
        var colour = new Color(0.25f, 0.28f, 0.22f);
        int root = mesh.Add(Vector3.Zero, Vector3.Right, colour);
        int top = mesh.Add(new Vector3(0, 0.07f, 0.13f), Vector3.Right, colour);
        int fork = mesh.Add(new Vector3(0, 0, 0.08f), Vector3.Right, colour);
        int bottom = mesh.Add(new Vector3(0, -0.07f, 0.13f), Vector3.Right, colour);
        mesh.Tri(root, top, fork);
        mesh.Tri(root, fork, bottom);
        var material = Painted();
        material.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        return mesh.Commit(material);
    }
}
