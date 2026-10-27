using System.Collections.Generic;
using Godot;

namespace IceAgeWorld;

/// <summary>
/// The underwater world on the ocean floor, where the sea otter dives: forests of giant kelp swaying up from the reefs to
/// the surface, boulders crusted with weed, starfish, sea urchins and clams for an otter to gather, and schools of
/// cod wheeling through the open water that scatter from a diver and can be caught. Sea otters live on the kelp coasts of
/// the cold North Pacific, diving for urchins, clams, crabs and slow fish, and eat their catch floating on their backs.
/// </summary>
public partial class Seabed : Node3D
{
    /// <summary>What an otter can bring up from a dive to eat.</summary>
    public enum Catch { Urchin, Clam, Fish }

    /// <summary>
    /// Hunger a fish restores, a little more than a clam (see <see cref="Underwater.FoodIn"/>). Urchins and clams are the
    /// same here as in the lakes, and carried up to the surface to eat.
    /// </summary>
    public const float FishNourishment = 30f;

    public static string NameOf(Catch kind) => kind switch
    {
        Catch.Urchin => "sea urchin",
        Catch.Clam => "clam",
        _ => "fish",
    };

    /// <summary>The player, whom the fish scatter from.</summary>
    public Player? Player { get; set; }

    private const int Seed = 77;
    private const int KelpCount = 2600;
    private const int RockCount = 420;
    private const int UrchinCount = 260;
    private const int ClamCount = 200;
    private const int StarfishCount = 180;
    private const int SchoolCount = 16;
    private const int FishPerSchool = 36;

    /// <summary>Kelp is built in square tiles that are drawn, or skipped, together, and only near the camera: underwater the haze hides it long before.</summary>
    private const float KelpTileSize = 48f;
    private const float KelpViewDistance = 130f;

    /// <summary>Seconds before an urchin or clam that was taken is replaced, and a caught fish rejoins its school.</summary>
    private const float RegrowSeconds = 150f;

    /// <summary>How close the diver comes before the fish dart away, and how close it must get to catch one.</summary>
    private const float FleeRange = 3.5f;
    private const float CatchReach = 1.1f;

    private Terrain _terrain = null!;
    private readonly List<Shellfish> _shellfish = [];
    private readonly List<School> _schools = [];
    private MultiMesh _urchins = null!;
    private MultiMesh _clams = null!;

    private sealed class Shellfish
    {
        public required Catch Kind { get; init; }
        public required int Instance { get; init; }
        public required Transform3D Transform { get; init; }
        public float Gone { get; set; }
    }

    private sealed class School
    {
        public required Vector3 Home { get; init; }
        public required float Radius { get; init; }
        public required float Speed { get; init; }
        public required MultiMesh Fish { get; init; }
        public required Vector3[] Offsets { get; init; }
        public float Angle { get; set; }
        public Vector3[] Scatter { get; } = new Vector3[FishPerSchool];
        public Vector3[] Positions { get; } = new Vector3[FishPerSchool];
        public float[] Gone { get; } = new float[FishPerSchool];
    }

    public void Populate(Terrain terrain)
    {
        _terrain = terrain;
        var rng = new RandomNumberGenerator { Seed = Seed };
        PlantKelp(rng);
        var rocks = PlaceRocks(rng);
        PlaceShellfish(rng, rocks);
        PlaceStarfish(rng);
        PlaceSchools(rng);
    }

    /// <summary>How deep the water is over the sea floor at a point, or 0 out of the ocean.</summary>
    private float DepthAt(float x, float z) =>
        _terrain.IsInOcean(x, z) ? Mathf.Max(0f, _terrain.Ocean.Surface - _terrain.GetHeight(x, z)) : 0f;

    /// <summary>A random point on the sea floor at least <paramref name="minDepth"/> under water, inside the world.</summary>
    private Vector3? RandomSpot(RandomNumberGenerator rng, float minDepth, float maxDepth = float.MaxValue)
    {
        var ocean = _terrain.Ocean;
        float edge = _terrain.HalfSize - Terrain.MountainWidth * 0.5f;
        for (int attempt = 0; attempt < 30; attempt++)
        {
            float x = rng.RandfRange(Mathf.Max(-edge, ocean.Centre.X - ocean.Radius), edge);
            float z = rng.RandfRange(-edge, Mathf.Min(edge, ocean.Centre.Y + ocean.Radius));
            float depth = DepthAt(x, z);
            if (depth >= minDepth && depth <= maxDepth)
                return new Vector3(x, _terrain.GetHeight(x, z), z);
        }
        return null;
    }

    /// <summary>
    /// Giant kelp, anchored to the rock by its holdfast and buoyed up by gas floats, grows from the reefs, where it can
    /// reach the light, up to the surface, where the fronds trail out along it in a canopy. It grows in forests, thick in
    /// places and absent in others; otters raft up in the canopy, wrapping themselves in the fronds to sleep.
    /// </summary>
    private void PlantKelp(RandomNumberGenerator rng)
    {
        var forests = new FastNoiseLite { Seed = Seed, Frequency = 0.012f, FractalOctaves = 2 };
        var tiles = new Dictionary<Vector2I, MeshBuilder>();
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/kelp.gdshader") };

        int planted = 0;
        for (int attempt = 0; planted < KelpCount && attempt < KelpCount * 40; attempt++)
        {
            // Kelp needs light, so it only grows up from the shallower ground: the reefs and the slopes up to the shore.
            if (RandomSpot(rng, 3f, 26f) is not { } foot || forests.GetNoise2D(foot.X, foot.Z) < 0.1f)
                continue;

            var tile = new Vector2I(Mathf.FloorToInt(foot.X / KelpTileSize), Mathf.FloorToInt(foot.Z / KelpTileSize));
            if (!tiles.TryGetValue(tile, out var mesh))
                tiles[tile] = mesh = new MeshBuilder();
            Stalk(mesh, rng, foot, _terrain.Ocean.Surface - foot.Y);
            planted++;
        }

        foreach (var (tile, mesh) in tiles)
            AddChild(new MeshInstance3D
            {
                Name = $"Kelp{tile.X}_{tile.Y}",
                Mesh = mesh.Commit(material),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                VisibilityRangeEnd = KelpViewDistance,
                VisibilityRangeEndMargin = 10f,
                // Swaying moves the kelp a couple of metres from where it was built.
                ExtraCullMargin = 3f,
            });
    }

    /// <summary>
    /// One kelp plant: a thin stipe leaning a little with the current, blades sprouting from it on alternate sides and
    /// lifting up at an angle on their floats, and, on a full-grown plant, its top trailing out along the surface.
    /// </summary>
    private static void Stalk(MeshBuilder mesh, RandomNumberGenerator rng, Vector3 foot, float depth)
    {
        bool grown = rng.Randf() < 0.65f;
        float length = grown ? depth - 0.5f : depth * rng.RandfRange(0.3f, 0.8f);
        float lean = rng.Randf() * Mathf.Tau;
        var drift = new Vector3(Mathf.Cos(lean), 0f, Mathf.Sin(lean));
        // The canopy all trails one way, down-current.
        var current = new Vector3(-0.7f, 0f, 0.7f);
        float trail = grown ? rng.RandfRange(2f, 5f) : 0f;
        float shade = rng.RandfRange(0.85f, 1.15f);
        var stipe = new Color(0.32f, 0.22f, 0.06f) * shade;
        var blade = new Color(0.45f, 0.34f, 0.08f) * shade;

        // Points up the stalk, then out along the surface, with how far along it each lies.
        var points = new List<(Vector3 Point, float Along)>();
        for (float along = 0f; along < length; along += 1f)
        {
            float t = along / Mathf.Max(length, 1f);
            points.Add((foot + Vector3.Up * along + drift * t * t * length * 0.15f, along));
        }
        var top = foot + Vector3.Up * length + drift * length * 0.15f;
        points.Add((top, length));
        for (float along = 1f; along <= trail; along += 1f)
            points.Add((top + current * along + Vector3.Up * 0.2f * Mathf.Min(along, 1f), length + along));

        // The stipe: a narrow ribbon, facing a random way.
        var side = new Vector3(Mathf.Cos(lean + 1.3f), 0f, Mathf.Sin(lean + 1.3f)) * 0.025f;
        var normal = side.Cross(Vector3.Up).Normalized();
        int previous = -1;
        foreach (var (point, along) in points)
        {
            int a = mesh.Add(point - side, normal, stipe, new Vector2(0f, along));
            mesh.Add(point + side, normal, stipe, new Vector2(1f, along));
            if (previous >= 0)
                mesh.Quad(previous, previous + 1, a + 1, a);
            previous = a;
        }

        // Blades every two-thirds of a metre, alternating sides and turning round the stalk as it goes.
        float turn = rng.Randf() * Mathf.Tau;
        for (int i = 2; i < points.Count; i++)
        {
            var (point, along) = points[i];
            for (int k = 0; k < 2; k++)
            {
                turn += 2.4f;
                var outward = new Vector3(Mathf.Cos(turn), 0f, Mathf.Sin(turn));
                // On the surface the blades lie flat, trailing with the current; below, they lift up on their floats.
                bool onTop = along > length;
                var reach = onTop ? (current + outward * 0.6f).Normalized() : (outward * 0.45f + Vector3.Up).Normalized();
                float bladeLength = rng.RandfRange(0.7f, 1.1f);
                var across = reach.Cross(onTop ? Vector3.Up : outward.Cross(Vector3.Up)).Normalized() * 0.07f;
                var faceNormal = reach.Cross(across).Normalized();
                var root = point + Vector3.Up * (k * 0.33f);
                float up = along + k * 0.33f;
                int r = mesh.Add(root, faceNormal, blade, new Vector2(0f, up));
                int left = mesh.Add(root + reach * bladeLength * 0.4f - across, faceNormal, blade, new Vector2(0f, up + bladeLength * 0.4f));
                int tip = mesh.Add(root + reach * bladeLength, faceNormal, blade, new Vector2(0f, up + bladeLength));
                int right = mesh.Add(root + reach * bladeLength * 0.4f + across, faceNormal, blade, new Vector2(1f, up + bladeLength * 0.4f));
                mesh.Quad(r, left, tip, right);
            }
        }
    }

    /// <summary>
    /// Boulders strewn over the floor, thickest on the reefs, grey rock crusted with pink coralline algae and green weed
    /// on top where the light falls. The big ones are solid, so a diver swims round them.
    /// </summary>
    private List<(Vector3 Position, float Size)> PlaceRocks(RandomNumberGenerator rng)
    {
        var material = new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 0.95f };
        var shapes = new MultiMesh[5];
        var placements = new List<Transform3D>[shapes.Length];
        for (int v = 0; v < shapes.Length; v++)
        {
            var lumps = new FastNoiseLite { Seed = Seed + 10 + v, Frequency = 1.6f };
            var stretch = new Vector3(rng.RandfRange(0.8f, 1.3f), rng.RandfRange(0.5f, 0.85f), rng.RandfRange(0.8f, 1.3f));
            var mesh = Animal.Ellipsoid(u => u * stretch * (1f + 0.25f * lumps.GetNoise3Dv(u)), 20, 12, material,
                (p, n) => new Color(0.36f, 0.36f, 0.34f)
                    .Lerp(new Color(0.55f, 0.35f, 0.38f), Mathf.Clamp(lumps.GetNoise3Dv(p * 3f) + 0.2f, 0f, 1f) * 0.6f)
                    .Lerp(new Color(0.25f, 0.35f, 0.15f), Mathf.SmoothStep(0.5f, 0.9f, n.Y) * 0.7f));
            shapes[v] = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = mesh };
            placements[v] = [];
        }

        var rocks = new List<(Vector3, float)>();
        var body = new StaticBody3D { Name = "Rocks" };
        AddChild(body);
        for (int i = 0; i < RockCount; i++)
        {
            if (RandomSpot(rng, 2f) is not { } spot)
                continue;
            float size = Mathf.Pow(rng.Randf(), 2.5f) * 3.5f + 0.4f;
            var basis = new Basis(Vector3.Up, rng.Randf() * Mathf.Tau).Scaled(Vector3.One * size);
            // Half sunk into the sand, as boulders settle.
            var position = spot + Vector3.Down * size * 0.25f;
            placements[rng.RandiRange(0, shapes.Length - 1)].Add(new Transform3D(basis, position));
            rocks.Add((position, size));
            if (size > 1f)
                body.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = size * 0.6f }, Position = position });
        }

        for (int v = 0; v < shapes.Length; v++)
        {
            shapes[v].InstanceCount = placements[v].Count;
            for (int i = 0; i < placements[v].Count; i++)
                shapes[v].SetInstanceTransform(i, placements[v][i]);
            AddChild(new MultiMeshInstance3D { Name = $"Boulders{v}", Multimesh = shapes[v] });
        }
        return rocks;
    }

    /// <summary>
    /// Sea urchins graze the rock round the boulders and the kelp's holdfasts, and clams lie half buried in the open sand.
    /// Both are a sea otter's daily bread: it dives for them and brings them up to eat on its back.
    /// </summary>
    private void PlaceShellfish(RandomNumberGenerator rng, List<(Vector3 Position, float Size)> rocks)
    {
        _urchins = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = UrchinMesh(rng), InstanceCount = UrchinCount };
        _clams = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = ClamMesh(), InstanceCount = ClamCount };

        for (int i = 0; i < UrchinCount; i++)
        {
            // Mostly clustered at the foot of a boulder, some out on their own.
            Vector3? spot = null;
            if (rng.Randf() < 0.7f && rocks.Count > 0)
            {
                var (rock, size) = rocks[rng.RandiRange(0, rocks.Count - 1)];
                float a = rng.Randf() * Mathf.Tau;
                float x = rock.X + Mathf.Cos(a) * size * 0.75f, z = rock.Z + Mathf.Sin(a) * size * 0.75f;
                if (DepthAt(x, z) > 1.5f)
                    spot = new Vector3(x, _terrain.GetHeight(x, z), z);
            }
            spot ??= RandomSpot(rng, 1.5f) ?? Vector3.Zero;
            AddShellfish(Catch.Urchin, _urchins, i, new Transform3D(new Basis(Vector3.Up, rng.Randf() * Mathf.Tau), spot.Value));
        }
        for (int i = 0; i < ClamCount; i++)
        {
            var spot = RandomSpot(rng, 1.5f) ?? Vector3.Zero;
            var tilt = new Basis(Vector3.Up, rng.Randf() * Mathf.Tau) * new Basis(Vector3.Right, rng.RandfRange(-0.4f, 0.4f));
            AddShellfish(Catch.Clam, _clams, i, new Transform3D(tilt, spot + Vector3.Down * 0.01f));
        }

        AddChild(new MultiMeshInstance3D { Name = "Urchins", Multimesh = _urchins });
        AddChild(new MultiMeshInstance3D { Name = "Clams", Multimesh = _clams });
    }

    private void AddShellfish(Catch kind, MultiMesh mesh, int instance, Transform3D transform)
    {
        mesh.SetInstanceTransform(instance, transform);
        _shellfish.Add(new Shellfish { Kind = kind, Instance = instance, Transform = transform });
    }

    /// <summary>A purple urchin: a flattened ball bristling with long spines.</summary>
    private static ArrayMesh UrchinMesh(RandomNumberGenerator rng)
    {
        var material = new StandardMaterial3D { AlbedoColor = new Color(0.32f, 0.1f, 0.35f), Roughness = 0.6f };
        var radii = new Vector3(0.07f, 0.045f, 0.07f);
        var body = Animal.Ellipsoid(radii, 14, 8, material);

        var spines = new MeshBuilder();
        for (int i = 0; i < 90; i++)
        {
            var d = new Vector3(rng.RandfRange(-1f, 1f), rng.RandfRange(-0.1f, 1f), rng.RandfRange(-1f, 1f)).Normalized();
            var root = d * radii;
            var a = d.Cross(Mathf.Abs(d.Y) < 0.9f ? Vector3.Up : Vector3.Right).Normalized() * 0.004f;
            var b = d.Cross(a).Normalized() * 0.004f;
            var tip = root + d * rng.RandfRange(0.05f, 0.08f);
            int t = spines.Add(tip, d, Colors.White);
            int p = spines.Add(root + a, d, Colors.White);
            int q = spines.Add(root - a * 0.5f + b, d, Colors.White);
            int r = spines.Add(root - a * 0.5f - b, d, Colors.White);
            spines.Tri(t, p, q);
            spines.Tri(t, q, r);
            spines.Tri(t, r, p);
        }
        return spines.Commit(material, body);
    }

    /// <summary>A clam: a fat, ridged shell, pale with darker growth rings, lying hinge-down in the sand.</summary>
    private static ArrayMesh ClamMesh()
    {
        var material = new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 0.5f };
        return Animal.Ellipsoid(u => new Vector3(u.X * 0.08f, u.Y * 0.035f, u.Z * 0.065f), 18, 10, material,
            (p, _) => new Color(0.78f, 0.72f, 0.62f).Lerp(new Color(0.45f, 0.38f, 0.3f),
                0.5f + 0.5f * Mathf.Sin(new Vector2(p.X, p.Z + 0.06f).Length() * 160f)));
    }

    /// <summary>Starfish dotted over the rock and sand in orange, red and purple: the sunflower stars that eat urchins, and ochre stars.</summary>
    private void PlaceStarfish(RandomNumberGenerator rng)
    {
        var material = new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 0.8f };
        var star = new MeshBuilder();
        int centre = star.Add(new Vector3(0f, 0.025f, 0f), Vector3.Up, Colors.White);
        for (int i = 0; i < 10; i++)
        {
            float a = i * Mathf.Tau / 10f;
            float r = i % 2 == 0 ? 0.16f : 0.05f;
            star.Add(new Vector3(Mathf.Cos(a) * r, 0.005f, Mathf.Sin(a) * r), Vector3.Up, Colors.White);
        }
        for (int i = 0; i < 10; i++)
            star.Tri(centre, 1 + (i + 1) % 10, 1 + i);

        var colours = new[] { new Color(0.85f, 0.35f, 0.1f), new Color(0.7f, 0.12f, 0.1f), new Color(0.45f, 0.15f, 0.45f) };
        var mesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            Mesh = star.Commit(material),
            InstanceCount = StarfishCount,
        };
        for (int i = 0; i < StarfishCount; i++)
        {
            var spot = RandomSpot(rng, 1.5f) ?? Vector3.Zero;
            float size = rng.RandfRange(0.7f, 1.5f);
            mesh.SetInstanceTransform(i, new Transform3D(new Basis(Vector3.Up, rng.Randf() * Mathf.Tau).Scaled(Vector3.One * size), spot));
            mesh.SetInstanceColor(i, colours[rng.RandiRange(0, colours.Length - 1)]);
        }
        AddChild(new MultiMeshInstance3D { Name = "Starfish", Multimesh = mesh });
    }

    /// <summary>
    /// Schools of Arctic cod, each a few dozen fish milling in a loose ball that wheels round and round over the reef in
    /// mid-water, silvery below and dark above, as fish are, so they are hard to see from either side.
    /// </summary>
    private void PlaceSchools(RandomNumberGenerator rng)
    {
        var material = new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 0.3f, Metallic = 0.5f };
        var body = Animal.Ellipsoid(u =>
        {
            // Deepest just behind the head, narrowing to the tail.
            float taper = 1f - Mathf.SmoothStep(-0.3f, 1f, u.Z) * 0.75f;
            return new Vector3(u.X * 0.022f * taper, u.Y * 0.04f * taper, u.Z * 0.13f);
        }, 12, 8, material, (p, _) => new Color(0.75f, 0.78f, 0.8f).Lerp(new Color(0.2f, 0.26f, 0.24f), Mathf.SmoothStep(-0.01f, 0.02f, p.Y)));
        var tail = new MeshBuilder();
        var grey = new Color(0.3f, 0.34f, 0.33f);
        int root = tail.Add(new Vector3(0f, 0f, 0.11f), Vector3.Right, grey);
        int upper = tail.Add(new Vector3(0f, 0.045f, 0.18f), Vector3.Right, grey);
        int lower = tail.Add(new Vector3(0f, -0.045f, 0.18f), Vector3.Right, grey);
        tail.Tri(root, upper, lower);
        tail.Tri(root, lower, upper);
        var fish = tail.Commit(material, body);

        for (int s = 0; s < SchoolCount; s++)
        {
            if (RandomSpot(rng, 10f) is not { } floor)
                continue;
            float depth = _terrain.Ocean.Surface - floor.Y;
            var home = floor + Vector3.Up * rng.RandfRange(2f, Mathf.Min(depth - 2f, 12f));
            var offsets = new Vector3[FishPerSchool];
            for (int i = 0; i < FishPerSchool; i++)
                offsets[i] = new Vector3(rng.RandfRange(-1f, 1f), rng.RandfRange(-0.5f, 0.5f), rng.RandfRange(-1f, 1f)) * 1.8f;
            var mesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = fish, InstanceCount = FishPerSchool };
            var school = new School
            {
                Home = home,
                Radius = rng.RandfRange(5f, 12f),
                Speed = rng.RandfRange(0.12f, 0.22f) * (rng.Randf() < 0.5f ? -1f : 1f),
                Fish = mesh,
                Offsets = offsets,
                Angle = rng.Randf() * Mathf.Tau,
            };
            _schools.Add(school);
            AddChild(new MultiMeshInstance3D { Name = $"School{s}", Multimesh = mesh, ExtraCullMargin = 16f });
            Swim(school, 0f, null);
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        foreach (var shellfish in _shellfish)
        {
            if (shellfish.Gone <= 0f)
                continue;
            shellfish.Gone -= dt;
            if (shellfish.Gone <= 0f)
                MeshOf(shellfish.Kind).SetInstanceTransform(shellfish.Instance, shellfish.Transform);
        }

        var camera = GetViewport().GetCamera3D()?.GlobalPosition;
        Vector3? diver = Player is { IsDiving: true } ? Player.GlobalPosition : null;
        foreach (var school in _schools)
        {
            // Only schools near enough to see are moved; the rest are lost in the haze, and just carry on wheeling.
            school.Angle += school.Speed * dt;
            if (camera is { } eye && eye.DistanceTo(school.Home) < 100f)
                Swim(school, dt, diver);
        }
    }

    private MultiMesh MeshOf(Catch kind) => kind == Catch.Urchin ? _urchins : _clams;

    /// <summary>
    /// The school wheels round its home, each fish keeping its place in the ball and facing the way the school swims,
    /// with a little wobble of its own. Fish near the diver dart away from it, and drift back into place once it has gone.
    /// </summary>
    private static void Swim(School school, float dt, Vector3? diver)
    {
        float direction = Mathf.Sign(school.Speed);
        var centre = school.Home + new Vector3(Mathf.Cos(school.Angle), 0f, Mathf.Sin(school.Angle)) * school.Radius;
        var heading = new Vector3(-Mathf.Sin(school.Angle), 0f, Mathf.Cos(school.Angle)) * direction;
        var basis = Basis.LookingAt(heading, Vector3.Up);
        float time = Time.GetTicksMsec() / 1000f;

        for (int i = 0; i < FishPerSchool; i++)
        {
            if (school.Gone[i] > 0f)
            {
                school.Gone[i] -= dt;
                if (school.Gone[i] > 0f)
                    continue;
            }

            var wobble = new Vector3(Mathf.Sin(time * 0.9f + i), Mathf.Sin(time * 1.3f + i * 2f) * 0.4f, Mathf.Cos(time * 0.7f + i)) * 0.3f;
            var rest = centre + basis * school.Offsets[i] + wobble;
            var scatter = school.Scatter[i];
            if (diver is { } d && (rest + scatter).DistanceTo(d) < FleeRange)
            {
                var away = (rest + scatter - d).Normalized();
                scatter += away * 8f * dt;
            }
            else
            {
                scatter = scatter.MoveToward(Vector3.Zero, 1.5f * dt);
            }
            school.Scatter[i] = scatter.LimitLength(6f);

            var position = rest + school.Scatter[i];
            // A fague darting away turns to face where it is going.
            var facing = scatter.LengthSquared() > 0.5f ? (heading + scatter.Normalized() * 2f).Normalized() : heading;
            school.Positions[i] = position;
            school.Fish.SetInstanceTransform(i, new Transform3D(Basis.LookingAt(facing, Vector3.Up), position));
        }
    }

    /// <summary>
    /// What the diver could take from where its mouth is: a fish near enough to snap up, or an urchin or clam on the
    /// floor within <paramref name="reach"/>, or nothing.
    /// </summary>
    public Catch? WithinReach(Vector3 mouth, float reach)
    {
        if (NearestFish(mouth) is not null)
            return Catch.Fish;
        return NearestShellfish(mouth, reach)?.Kind;
    }

    /// <summary>Takes whatever <see cref="WithinReach"/> found: it vanishes, to be replaced in a while.</summary>
    public Catch? Take(Vector3 mouth, float reach)
    {
        if (NearestFish(mouth) is var (school, i))
        {
            school.Gone[i] = RegrowSeconds;
            school.Fish.SetInstanceTransform(i, new Transform3D(Basis.Identity.Scaled(Vector3.Zero), school.Positions[i]));
            return Catch.Fish;
        }
        if (NearestShellfish(mouth, reach) is { } shellfish)
        {
            shellfish.Gone = RegrowSeconds;
            MeshOf(shellfish.Kind).SetInstanceTransform(shellfish.Instance, new Transform3D(Basis.Identity.Scaled(Vector3.Zero), shellfish.Transform.Origin));
            return shellfish.Kind;
        }
        return null;
    }

    private (School, int)? NearestFish(Vector3 mouth)
    {
        foreach (var school in _schools)
        {
            if (school.Home.DistanceTo(mouth) > school.Radius + 12f)
                continue;
            for (int i = 0; i < FishPerSchool; i++)
                if (school.Gone[i] <= 0f && school.Positions[i].DistanceTo(mouth) < CatchReach)
                    return (school, i);
        }
        return null;
    }

    private Shellfish? NearestShellfish(Vector3 mouth, float reach)
    {
        Shellfish? nearest = null;
        float best = reach;
        foreach (var shellfish in _shellfish)
        {
            float distance = shellfish.Transform.Origin.DistanceTo(mouth);
            if (shellfish.Gone <= 0f && distance < best)
            {
                best = distance;
                nearest = shellfish;
            }
        }
        return nearest;
    }
}
