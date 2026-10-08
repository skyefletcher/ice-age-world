using System.Collections.Generic;
using Godot;

namespace IceAgeWorld;

/// <summary>A round lake carved into the terrain. <see cref="Radius"/> is where the water meets the shore.</summary>
public readonly record struct Lake(Vector2 Centre, float Radius, float Surface);

/// <summary>
/// Procedurally generates the ice-age landscape: a noise-based heightmap mesh, matching collision,
/// a ring of mountains around the edge to keep players in, lake basins and patches of randomised pine forest.
/// </summary>
public partial class Terrain : Node3D
{
    /// <summary>Vertices along each side of the heightmap.</summary>
    [Export] public int Resolution { get; set; } = 257;

    /// <summary>World units between neighbouring vertices.</summary>
    [Export] public float CellSize { get; set; } = 2f;

    [Export] public float HeightScale { get; set; } = 28f;
    [Export] public int Seed { get; set; } = 1234;
    [Export] public int TreeCount { get; set; } = 400;
    [Export] public int LakeCount { get; set; } = 6;

    /// <summary>Depth of water at the centre of each lake.</summary>
    [Export] public float LakeDepth { get; set; } = 5f;

    /// <summary>Width of the sloping bank between a lake's water line and the surrounding land.</summary>
    public const float ShoreWidth = 6f;

    /// <summary>Number of distinct tree shapes generated; each placed tree is one of these, further varied.</summary>
    [Export] public int TreeVariants { get; set; } = 8;

    private static readonly Color Steppe = new(0.58f, 0.6f, 0.42f);
    private static readonly Color Snow = new(0.93f, 0.95f, 1f);
    private static readonly Color Rock = new(0.45f, 0.45f, 0.48f);
    private static readonly Color Mud = new(0.42f, 0.37f, 0.28f);

    private float[] _heights = [];
    private readonly List<Lake> _lakes = [];

    /// <summary>Distance from the centre of the map to its edge.</summary>
    public float HalfSize => (Resolution - 1) * CellSize / 2f;

    public IReadOnlyList<Lake> Lakes => _lakes;

    public override void _Ready()
    {
        GenerateHeights();
        BuildMesh();
        BuildCollision();
        ScatterTrees();
    }

    /// <summary>Ground height at a world-space X/Z position.</summary>
    public float GetHeight(float x, float z)
    {
        float gx = Mathf.Clamp((x + HalfSize) / CellSize, 0, Resolution - 1.001f);
        float gz = Mathf.Clamp((z + HalfSize) / CellSize, 0, Resolution - 1.001f);
        int x0 = (int)gx, z0 = (int)gz;
        float tx = gx - x0, tz = gz - z0;

        float top = Mathf.Lerp(HeightAt(x0, z0), HeightAt(x0 + 1, z0), tx);
        float bottom = Mathf.Lerp(HeightAt(x0, z0 + 1), HeightAt(x0 + 1, z0 + 1), tx);
        return Mathf.Lerp(top, bottom, tz);
    }

    /// <summary>True where the ground is too steep for trees or grass.</summary>
    public bool IsSteep(float x, float z) =>
        Mathf.Abs(GetHeight(x + 2, z) - GetHeight(x - 2, z)) > 2.5f
        || Mathf.Abs(GetHeight(x, z + 2) - GetHeight(x, z - 2)) > 2.5f;

    /// <summary>
    /// How much grass grows at a point: 1 on the green steppe, fading to 0 towards the snow line,
    /// and 0 on steep rocky ground, in lakes and on their muddy shores.
    /// </summary>
    public float GrassAmount(float x, float z)
    {
        if (IsSteep(x, z) || DistanceFromWater(x, z) < 2.5f)
            return 0f;

        return 1f - Mathf.SmoothStep(HeightScale * 0.18f, HeightScale * 0.28f, GetHeight(x, z));
    }

    /// <summary>Distance to the nearest lake's water line; negative inside a lake.</summary>
    public float DistanceFromWater(float x, float z)
    {
        float nearest = float.MaxValue;
        foreach (var lake in _lakes)
            nearest = Mathf.Min(nearest, new Vector2(x, z).DistanceTo(lake.Centre) - lake.Radius);
        return nearest;
    }

    private float HeightAt(int x, int z) =>
        _heights[Mathf.Clamp(z, 0, Resolution - 1) * Resolution + Mathf.Clamp(x, 0, Resolution - 1)];

    private void GenerateHeights()
    {
        var noise = new FastNoiseLite
        {
            Seed = Seed,
            NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
            FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
            FractalOctaves = 5,
            Frequency = 0.003f,
        };

        _heights = new float[Resolution * Resolution];
        for (int z = 0; z < Resolution; z++)
        for (int x = 0; x < Resolution; x++)
        {
            float wx = x * CellSize - HalfSize;
            float wz = z * CellSize - HalfSize;

            // Raise the noise to a power so valleys are broad and flat and peaks are sharper.
            float n = Mathf.Clamp(noise.GetNoise2D(wx, wz) * 0.5f + 0.5f, 0f, 1f);
            float h = Mathf.Pow(n, 1.6f) * HeightScale;

            // Flatten the spawn area in the middle of the map.
            float fromCentre = new Vector2(wx, wz).Length();
            h *= Mathf.Lerp(0.3f, 1f, Mathf.SmoothStep(0f, 60f, fromCentre));

            // Steep mountains near the edge form a natural world boundary.
            float edge = Mathf.Max(Mathf.Abs(wx), Mathf.Abs(wz)) / HalfSize;
            if (edge > 0.85f)
                h += Mathf.Pow((edge - 0.85f) / 0.15f, 2f) * 50f;

            _heights[z * Resolution + x] = h;
        }

        CarveLakes();
    }

    private void CarveLakes()
    {
        var rng = new RandomNumberGenerator { Seed = (ulong)Seed + 1 };
        _lakes.Clear();

        for (int attempt = 0; _lakes.Count < LakeCount && attempt < 500; attempt++)
        {
            float radius = rng.RandfRange(14f, 24f);
            // The first lake goes near the spawn point so there's always water close by.
            float distance = _lakes.Count == 0 ? radius + 30f : rng.RandfRange(60f, HalfSize * 0.7f);
            float angle = rng.Randf() * Mathf.Tau;
            var centre = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;

            if (GetHeight(centre.X, centre.Y) > HeightScale * 0.2f)
                continue;

            bool overlaps = false;
            foreach (var other in _lakes)
                overlaps |= centre.DistanceTo(other.Centre) < radius + other.Radius + ShoreWidth * 2f;
            if (overlaps)
                continue;

            // Set the water just below the lowest point of the surrounding bank, so the lake never spills.
            float surface = float.MaxValue;
            foreach (float ring in new[] { radius, radius + ShoreWidth * 0.5f, radius + ShoreWidth })
            for (int k = 0; k < 32; k++)
            {
                float a = k * Mathf.Tau / 32f;
                surface = Mathf.Min(surface, GetHeight(centre.X + Mathf.Cos(a) * ring, centre.Y + Mathf.Sin(a) * ring));
            }

            var lake = new Lake(centre, radius, surface - 0.4f);
            _lakes.Add(lake);
            Carve(lake);
        }
    }

    /// <summary>Digs a bowl below the water line and blends a sloping bank back up to the original ground.</summary>
    private void Carve(Lake lake)
    {
        float reach = lake.Radius + ShoreWidth;
        int minX = Mathf.Max(0, Mathf.FloorToInt((lake.Centre.X - reach + HalfSize) / CellSize));
        int maxX = Mathf.Min(Resolution - 1, Mathf.CeilToInt((lake.Centre.X + reach + HalfSize) / CellSize));
        int minZ = Mathf.Max(0, Mathf.FloorToInt((lake.Centre.Y - reach + HalfSize) / CellSize));
        int maxZ = Mathf.Min(Resolution - 1, Mathf.CeilToInt((lake.Centre.Y + reach + HalfSize) / CellSize));

        for (int z = minZ; z <= maxZ; z++)
        for (int x = minX; x <= maxX; x++)
        {
            var point = new Vector2(x * CellSize - HalfSize, z * CellSize - HalfSize);
            float d = point.DistanceTo(lake.Centre);
            if (d > reach)
                continue;

            int i = z * Resolution + x;
            float original = _heights[i];
            float edge = lake.Surface - 0.3f;

            if (d < lake.Radius)
            {
                float bowl = edge - (LakeDepth - 0.3f) * (1f - Mathf.SmoothStep(0f, lake.Radius, d));
                _heights[i] = Mathf.Min(original, bowl);
            }
            else
            {
                _heights[i] = Mathf.Lerp(edge, original, Mathf.SmoothStep(lake.Radius, reach, d));
            }
        }
    }

    private void BuildMesh()
    {
        int r = Resolution;
        var vertices = new Vector3[r * r];
        var normals = new Vector3[r * r];
        var colors = new Color[r * r];

        for (int z = 0; z < r; z++)
        for (int x = 0; x < r; x++)
        {
            int i = z * r + x;
            float h = HeightAt(x, z);
            vertices[i] = new Vector3(x * CellSize - HalfSize, h, z * CellSize - HalfSize);

            var normal = new Vector3(
                HeightAt(x - 1, z) - HeightAt(x + 1, z),
                2f * CellSize,
                HeightAt(x, z - 1) - HeightAt(x, z + 1)).Normalized();
            normals[i] = normal;

            // Grassy steppe in the valleys, snow higher up, bare rock on steep slopes, mud around lakes.
            float snow = Mathf.SmoothStep(HeightScale * 0.25f, HeightScale * 0.45f, h);
            float rock = 1f - Mathf.SmoothStep(0.65f, 0.85f, normal.Y);
            float mud = 1f - Mathf.SmoothStep(1f, 3.5f, DistanceFromWater(vertices[i].X, vertices[i].Z));
            colors[i] = Steppe.Lerp(Snow, snow).Lerp(Rock, rock).Lerp(Mud, mud);
        }

        var indices = new int[(r - 1) * (r - 1) * 6];
        int k = 0;
        for (int z = 0; z < r - 1; z++)
        for (int x = 0; x < r - 1; x++)
        {
            int a = z * r + x, b = a + 1, c = a + r, d = c + 1;
            indices[k++] = a; indices[k++] = b; indices[k++] = c;
            indices[k++] = b; indices[k++] = d; indices[k++] = c;
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.Color] = colors;
        arrays[(int)Mesh.ArrayType.Index] = indices;

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.SurfaceSetMaterial(0, new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            VertexColorIsSrgb = true,
            Roughness = 0.95f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        });

        AddChild(new MeshInstance3D { Name = "Ground", Mesh = mesh });
    }

    private void BuildCollision()
    {
        // HeightMapShape3D uses 1-unit spacing centred on the origin; scaling X/Z sets the cell size.
        var shape = new HeightMapShape3D { MapWidth = Resolution, MapDepth = Resolution, MapData = _heights };
        var body = new StaticBody3D { Name = "GroundBody" };
        body.AddChild(new CollisionShape3D { Shape = shape, Scale = new Vector3(CellSize, 1f, CellSize) });
        AddChild(body);
    }

    private void ScatterTrees()
    {
        var rng = new RandomNumberGenerator { Seed = (ulong)Seed };

        // Forests grow in patches rather than as an even sprinkle across the whole map.
        var forestNoise = new FastNoiseLite
        {
            Seed = Seed + 1,
            NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
            Frequency = 0.012f,
        };

        var material = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            VertexColorIsSrgb = true,
            Roughness = 1f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };

        // A handful of base shapes, each one a different tree; every placed tree then varies further.
        var variants = new ArrayMesh[TreeVariants];
        var placements = new List<(Transform3D Transform, Color Tint)>[TreeVariants];
        for (int v = 0; v < TreeVariants; v++)
        {
            variants[v] = PineTree.Build(rng, material);
            placements[v] = [];
        }

        // Trunks are solid so animals can't walk through trees.
        var trunkShape = new CylinderShape3D { Radius = 0.6f, Height = 6f };
        var bodies = new StaticBody3D { Name = "TreeBodies" };
        float range = HalfSize * 0.85f;
        int placed = 0;

        for (int attempt = 0; placed < TreeCount && attempt < TreeCount * 40; attempt++)
        {
            float x = rng.RandfRange(-range, range);
            float z = rng.RandfRange(-range, range);
            float h = GetHeight(x, z);

            bool nearSpawn = new Vector2(x, z).Length() < 20f;
            bool tooHigh = h > HeightScale * 0.4f;
            if (nearSpawn || tooHigh || IsSteep(x, z) || DistanceFromWater(x, z) < 3f)
                continue;

            float density = forestNoise.GetNoise2D(x, z) * 0.5f + 0.5f;
            if (rng.Randf() > 0.05f + Mathf.SmoothStep(0.4f, 0.75f, density))
                continue;

            // Each tree gets its own height, girth, slight lean and brightness on top of its base shape.
            float height = rng.RandfRange(0.75f, 1.3f);
            float width = height * rng.RandfRange(0.85f, 1.15f);
            float leanDirection = rng.Randf() * Mathf.Tau;
            var leanAxis = new Vector3(Mathf.Cos(leanDirection), 0f, Mathf.Sin(leanDirection));
            var basis = new Basis(leanAxis, rng.RandfRange(0f, 0.07f))
                        * new Basis(Vector3.Up, rng.Randf() * Mathf.Tau)
                        * Basis.FromScale(new Vector3(width, height, width));
            float brightness = rng.RandfRange(0.8f, 1.1f);

            placements[rng.RandiRange(0, TreeVariants - 1)].Add((
                new Transform3D(basis, new Vector3(x, h - 0.2f, z)),
                new Color(brightness, brightness, brightness)));
            bodies.AddChild(new CollisionShape3D { Shape = trunkShape, Position = new Vector3(x, h + 3f, z) });
            placed++;
        }

        AddChild(bodies);
        for (int v = 0; v < TreeVariants; v++)
            AddTreeVariant(variants[v], placements[v]);
    }

    /// <summary>Draws every tree that shares one base shape in a single draw call using a MultiMesh.</summary>
    private void AddTreeVariant(Mesh mesh, List<(Transform3D Transform, Color Tint)> instances)
    {
        if (instances.Count == 0)
            return;

        var multiMesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            Mesh = mesh,
            InstanceCount = instances.Count,
        };

        for (int i = 0; i < instances.Count; i++)
        {
            multiMesh.SetInstanceTransform(i, instances[i].Transform);
            multiMesh.SetInstanceColor(i, instances[i].Tint);
        }

        AddChild(new MultiMeshInstance3D { Multimesh = multiMesh });
    }
}
