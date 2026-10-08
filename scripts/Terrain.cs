using System.Collections.Generic;
using Godot;

namespace IceAgeWorld;

/// <summary>A round lake carved into the terrain. <see cref="Radius"/> is where the water meets the shore.</summary>
public readonly record struct Lake(Vector2 Centre, float Radius, float Surface);

/// <summary>
/// Procedurally generates the ice-age landscape: a noise-based heightmap mesh, matching collision,
/// a ring of mountains around the edge to keep players in, lake basins and scattered pine trees.
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
        var trees = new List<Transform3D>();
        float range = HalfSize * 0.85f;

        for (int attempt = 0; trees.Count < TreeCount && attempt < TreeCount * 20; attempt++)
        {
            float x = rng.RandfRange(-range, range);
            float z = rng.RandfRange(-range, range);
            float h = GetHeight(x, z);

            bool nearSpawn = new Vector2(x, z).Length() < 20f;
            bool tooHigh = h > HeightScale * 0.4f;
            if (nearSpawn || tooHigh || IsSteep(x, z) || DistanceFromWater(x, z) < 3f)
                continue;

            var basis = Basis.Identity
                .Rotated(Vector3.Up, rng.Randf() * Mathf.Tau)
                .Scaled(Vector3.One * rng.RandfRange(0.7f, 1.4f));
            trees.Add(new Transform3D(basis, new Vector3(x, h - 0.2f, z)));
        }

        var bark = new StandardMaterial3D { AlbedoColor = new Color(0.33f, 0.24f, 0.17f) };
        var needles = new StandardMaterial3D { AlbedoColor = new Color(0.16f, 0.3f, 0.22f) };
        var snow = new StandardMaterial3D { AlbedoColor = Snow };

        AddTreePart(trees, new CylinderMesh { TopRadius = 0.2f, BottomRadius = 0.3f, Height = 2f, Material = bark }, 1f);
        AddTreePart(trees, new CylinderMesh { TopRadius = 0f, BottomRadius = 1.8f, Height = 3.5f, Material = needles }, 3.2f);
        AddTreePart(trees, new CylinderMesh { TopRadius = 0f, BottomRadius = 1.3f, Height = 2.8f, Material = needles }, 5f);
        AddTreePart(trees, new CylinderMesh { TopRadius = 0f, BottomRadius = 0.7f, Height = 1.4f, Material = snow }, 6.3f);

        // Trunks are solid so animals can't walk through trees.
        var trunkShape = new CylinderShape3D { Radius = 0.45f, Height = 4f };
        var body = new StaticBody3D { Name = "TreeBodies" };
        foreach (var tree in trees)
            body.AddChild(new CollisionShape3D { Shape = trunkShape, Position = tree.Origin + Vector3.Up * 2f });
        AddChild(body);
    }

    /// <summary>Draws one piece of every tree in a single draw call using a MultiMesh.</summary>
    private void AddTreePart(List<Transform3D> trees, Mesh mesh, float heightOffset)
    {
        var multiMesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = mesh,
            InstanceCount = trees.Count,
        };

        var offset = new Transform3D(Basis.Identity, Vector3.Up * heightOffset);
        for (int i = 0; i < trees.Count; i++)
            multiMesh.SetInstanceTransform(i, trees[i] * offset);

        AddChild(new MultiMeshInstance3D { Multimesh = multiMesh });
    }
}
