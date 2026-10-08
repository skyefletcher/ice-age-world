using System.Collections.Generic;
using Godot;

namespace IceAgeWorld;

/// <summary>
/// Procedurally generates the ice-age landscape: a noise-based heightmap mesh, matching collision,
/// a ring of mountains around the edge to keep players in, and scattered pine trees.
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

    private static readonly Color Steppe = new(0.58f, 0.6f, 0.42f);
    private static readonly Color Snow = new(0.93f, 0.95f, 1f);
    private static readonly Color Rock = new(0.45f, 0.45f, 0.48f);

    private float[] _heights = [];

    /// <summary>Distance from the centre of the map to its edge.</summary>
    public float HalfSize => (Resolution - 1) * CellSize / 2f;

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

    /// <summary>True on the low, gentle steppe below the snow line, where grass grows.</summary>
    public bool IsGrassy(float x, float z) =>
        GetHeight(x, z) < HeightScale * 0.22f && !IsSteep(x, z);

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

            // Grassy steppe in the valleys, snow higher up, bare rock on steep slopes.
            float snow = Mathf.SmoothStep(HeightScale * 0.25f, HeightScale * 0.45f, h);
            float rock = 1f - Mathf.SmoothStep(0.65f, 0.85f, normal.Y);
            colors[i] = Steppe.Lerp(Snow, snow).Lerp(Rock, rock);
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
            if (nearSpawn || tooHigh || IsSteep(x, z))
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
