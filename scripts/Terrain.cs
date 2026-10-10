using System;
using System.Collections.Generic;
using Godot;

namespace IceAgeWorld;

/// <summary>
/// A round lake carved into the terrain. <see cref="Radius"/> is where the water meets the shore, and <see cref="Shore"/>
/// how wide the bank is that slopes back up to the land around it.
/// </summary>
public readonly record struct Lake(Vector2 Centre, float Radius, float Surface, float Shore = Terrain.ShoreWidth);

/// <summary>
/// A placed tree, for animals that climb: where its trunk runs, where its branches reach and where its top is.
/// <see cref="Transform"/> places the tree's <see cref="Shape"/> in the world; heights up the trunk are measured in
/// world units above the tree's foot.
/// </summary>
public readonly record struct Tree(Transform3D Transform, TreeBuilder.Shape Shape)
{
    private float HeightScale => Transform.Basis.Y.Length();
    private float WidthScale => Transform.Basis.X.Length();
    private float TrunkRadius => Shape.TrunkRadius;
    private float TrunkHeight => Shape.TrunkHeight;
    private float TopHeight => Shape.TopHeight;

    public IReadOnlyList<TreeBuilder.Branch> Branches => Shape.Branches;

    /// <summary>How high up the trunk a branch grows, above the tree's foot.</summary>
    public float BranchHeight(int branch) => Branches[branch].From.Y * HeightScale;

    /// <summary>Level direction a branch points out from the trunk.</summary>
    public Vector3 BranchDirection(int branch)
    {
        var b = Branches[branch];
        return (Transform.Basis * (b.To - b.From) with { Y = 0f }).Normalized();
    }

    /// <summary>Length of a branch from root to tip.</summary>
    public float BranchLength(int branch)
    {
        var b = Branches[branch];
        return (Transform.Basis * (b.To - b.From)).Length();
    }

    /// <summary>The top of a branch's wood at fraction <paramref name="f"/> of the way out, where a walker's paws go.</summary>
    public Vector3 BranchTopAt(int branch, float f) => Transform * Branches[branch].TopAt(f);

    /// <summary>The middle of the trunk at the given height above the tree's foot. Trees lean a little, so it drifts.</summary>
    public Vector3 AxisAt(float height) => Transform * (Vector3.Up * (height / HeightScale));

    /// <summary>How thick the trunk is at the given height above the tree's foot; it tapers towards the top.</summary>
    public float RadiusAt(float height) =>
        TrunkRadius * WidthScale * Mathf.Lerp(1f, TreeBuilder.TrunkTaper, Mathf.Clamp(height / HeightScale / TrunkHeight, 0f, 1f));

    /// <summary>
    /// How far up the trunk, above the tree's foot, a climber clings before scrambling out onto the top: high up,
    /// where the trunk is still thick enough to grip.
    /// </summary>
    public float ClimbTop => Mathf.Min(TrunkHeight * 0.85f, TopHeight - 0.6f) * HeightScale;

    /// <summary>The very top of the tree, where a climber stands to look out over the forest.</summary>
    public Vector3 Summit => Transform * (Vector3.Up * TopHeight);

    /// <summary>Radius of the solid column round the foot of the trunk that stops animals walking through it.</summary>
    public float CollisionRadius => RadiusAt(0f) * 1.1f;
}

/// <summary>
/// Procedurally generates the ice-age landscape: a noise-based heightmap mesh, matching collision,
/// a ring of mountains around the edge to keep players in, a few great snowy mountains rising out of the steppe, with
/// caves dug into their flanks (see Terrain.Caves.cs), lake basins and patches of mixed, randomised forest.
/// </summary>
public partial class Terrain : Node3D
{
    /// <summary>Vertices along each side of the heightmap: 513 at 2 m apart makes a world just over a kilometre across.</summary>
    [Export] public int Resolution { get; set; } = 513;

    /// <summary>World units between neighbouring vertices.</summary>
    [Export] public float CellSize { get; set; } = 2f;

    [Export] public float HeightScale { get; set; } = 28f;
    [Export] public int Seed { get; set; } = 1234;
    [Export] public int TreeCount { get; set; } = 1600;
    [Export] public int LakeCount { get; set; } = 6;

    /// <summary>
    /// Radius of the one big lake that fills the north-east corner of the world, big enough to swim a long way out into,
    /// with a wide, gentle shore so it lies in a broad basin rather than a pit.
    /// </summary>
    [Export] public float BigLakeRadius { get; set; } = 100f;

    [Export] public float BigLakeShore { get; set; } = 20f;

    /// <summary>
    /// How many great mountains rise out of the steppe inside the world, and how tall and broad they are: craggy, snowy
    /// massifs towering over the hills, like the ranges that stood above the ice-age mammoth steppe.
    /// </summary>
    [Export] public int MountainCount { get; set; } = 4;

    [Export] public Vector2 MountainHeights { get; set; } = new(60f, 95f);
    [Export] public Vector2 MountainRadii { get; set; } = new(85f, 125f);

    /// <summary>Depth of water at the centre of each lake.</summary>
    [Export] public float LakeDepth { get; set; } = 5f;

    /// <summary>Width of the sloping bank between a lake's water line and the surrounding land.</summary>
    public const float ShoreWidth = 6f;

    /// <summary>How deep the wall of mountains round the edge of the world is, however big the world.</summary>
    public const float MountainWidth = 38f;

    /// <summary>Width of the square tiles of forest that are drawn, or skipped, together.</summary>
    private const float TreeTileSize = 96f;

    /// <summary>
    /// How far off a tile of trees is still drawn. By then the fog has all but swallowed them, so the pop is hard to see.
    /// </summary>
    private const float TreeViewDistance = 450f;

    /// <summary>Distinct shapes generated for each kind of tree; each placed tree is one of these, further varied.</summary>
    [Export] public int VariantsPerKind { get; set; } = 3;

    private static readonly Color Steppe = new(0.58f, 0.6f, 0.42f);
    private static readonly Color Snow = new(0.93f, 0.95f, 1f);
    private static readonly Color Rock = new(0.45f, 0.45f, 0.48f);
    private static readonly Color Mud = new(0.42f, 0.37f, 0.28f);

    /// <summary>
    /// Height of the ground underfoot at each vertex: the floor of a cave where one has been dug, so this is what the
    /// collision follows.
    /// </summary>
    private float[] _heights = [];

    /// <summary>
    /// Height of the top of the land at each vertex: the same as <see cref="_heights"/>, except over a cave, where it is
    /// the mountainside on top of the cave's roof. The two are one array until the caves are dug.
    /// </summary>
    private float[] _surface = [];
    private readonly List<Lake> _lakes = [];
    private readonly List<Tree> _trees = [];

    /// <summary>Radius of the thickest trunk in the forest, so trunk searches can skip far-off trees quickly.</summary>
    private float _thickestTrunk;

    /// <summary>
    /// How big the trees grow, on top of each tree's own variation. At 2, the old-growth taiga's spruces tower 25 to 45
    /// metres, as the biggest real ones do, with trunks a metre and more across at the foot.
    /// </summary>
    [Export] public float TreeScale { get; set; } = 2f;

    /// <summary>Distance from the centre of the map to its edge.</summary>
    public float HalfSize => (Resolution - 1) * CellSize / 2f;

    public IReadOnlyList<Lake> Lakes => _lakes;

    public IReadOnlyList<Tree> Trees => _trees;

    /// <summary>The tree whose trunk is closest to a point (ignoring height), if any is within <paramref name="within"/>.</summary>
    public Tree? NearestTree(Vector3 point, float within)
    {
        Tree? nearest = null;
        float best = within * within;
        foreach (var tree in _trees)
        {
            var offset = tree.Transform.Origin - point;
            float distance = offset.X * offset.X + offset.Z * offset.Z;
            if (distance < best)
            {
                best = distance;
                nearest = tree;
            }
        }
        return nearest;
    }

    /// <summary>
    /// The tree whose trunk comes within <paramref name="clearance"/> of a point (ignoring height), the closest if
    /// several do: e.g. one an animal of that radius would bump into.
    /// </summary>
    public Tree? TrunkNear(Vector3 point, float clearance)
    {
        Tree? nearest = null;
        float best = clearance;
        float within = clearance + _thickestTrunk;
        foreach (var tree in _trees)
        {
            // Most trees are nowhere near: rule them out cheaply before working out how thick this one is.
            var offset = tree.Transform.Origin - point;
            if (Mathf.Abs(offset.X) > within || Mathf.Abs(offset.Z) > within)
                continue;
            float gap = new Vector2(offset.X, offset.Z).Length() - tree.CollisionRadius;
            if (gap < best)
            {
                best = gap;
                nearest = tree;
            }
        }
        return nearest;
    }

    public override void _Ready()
    {
        GenerateHeights();
        DigCaves();
        BuildMesh();
        BuildCollision();
        BuildCaveRoofs();
        ScatterTrees();
    }

    /// <summary>Height of the top of the land at a world-space X/Z position: over a cave, the top of its roof.</summary>
    public float GetHeight(float x, float z) => Sample(_surface, x, z);

    /// <summary>Height of the ground underfoot at a world-space X/Z position: inside a cave, its floor.</summary>
    public float FloorHeight(float x, float z) => Sample(_heights, x, z);

    /// <summary>
    /// The ground an animal at <paramref name="position"/> stands on: the cave floor if it is down inside a cave, or the
    /// top of the land if it is up on the mountainside over it (or anywhere else).
    /// </summary>
    public float GroundBelow(Vector3 position) =>
        IsUnderRoof(position) ? FloorHeight(position.X, position.Z) : GetHeight(position.X, position.Z);

    /// <summary>True down inside a cave, under its roof, where the rock overhead keeps out the sky.</summary>
    public bool IsUnderRoof(Vector3 position)
    {
        float top = GetHeight(position.X, position.Z);
        return top - FloorHeight(position.X, position.Z) > 1f && position.Y < top - 1f;
    }

    private float Sample(float[] heights, float x, float z)
    {
        float gx = Mathf.Clamp((x + HalfSize) / CellSize, 0, Resolution - 1.001f);
        float gz = Mathf.Clamp((z + HalfSize) / CellSize, 0, Resolution - 1.001f);
        int x0 = (int)gx, z0 = (int)gz;
        float tx = gx - x0, tz = gz - z0;

        float top = Mathf.Lerp(At(heights, x0, z0), At(heights, x0 + 1, z0), tx);
        float bottom = Mathf.Lerp(At(heights, x0, z0 + 1), At(heights, x0 + 1, z0 + 1), tx);
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
        if (IsSteep(x, z) || DistanceFromWater(x, z) < 2.5f || IsInCave(x, z))
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

    private float HeightAt(int x, int z) => At(_heights, x, z);

    private float At(float[] heights, int x, int z) =>
        heights[Mathf.Clamp(z, 0, Resolution - 1) * Resolution + Mathf.Clamp(x, 0, Resolution - 1)];

    /// <summary>
    /// The normal of the ground at a vertex, worked out from its neighbours' heights. Normals round a cave are taken from
    /// the top of the land, so the mountainside shades the same on the cave roof as off it, with no seam.
    /// </summary>
    private Vector3 NormalAt(float[] heights, int x, int z) =>
        new Vector3(At(heights, x - 1, z) - At(heights, x + 1, z), 2f * CellSize, At(heights, x, z - 1) - At(heights, x, z + 1)).Normalized();

    /// <summary>Grassy steppe in the valleys, snow higher up, bare rock on steep slopes, mud around lakes.</summary>
    private Color GroundColour(float x, float z, float h, Vector3 normal)
    {
        float snow = Mathf.SmoothStep(HeightScale * 0.25f, HeightScale * 0.45f, h);
        float rock = 1f - Mathf.SmoothStep(0.65f, 0.85f, normal.Y);
        // High on the mountains the wind scours the snow off the ridges and steeper faces, baring dark crags.
        float crags = Mathf.SmoothStep(HeightScale * 1.1f, HeightScale * 1.6f, h) * (1f - Mathf.SmoothStep(0.86f, 0.97f, normal.Y));
        rock = Mathf.Max(rock, crags);
        float mud = 1f - Mathf.SmoothStep(1f, 3.5f, DistanceFromWater(x, z));
        return Steppe.Lerp(Snow, snow).Lerp(Rock, rock).Lerp(Mud, mud);
    }

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

        PlaceMountains();
        _heights = new float[Resolution * Resolution];
        _surface = _heights;
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
            h += MountainHeight(wx, wz);

            // Steep mountains near the edge form a natural world boundary.
            float toEdge = HalfSize - Mathf.Max(Mathf.Abs(wx), Mathf.Abs(wz));
            if (toEdge < MountainWidth)
                h += Mathf.Pow(1f - toEdge / MountainWidth, 2f) * 50f;

            _heights[z * Resolution + x] = h;
        }

        CarveLakes();
    }

    private void CarveLakes()
    {
        var rng = new RandomNumberGenerator { Seed = (ulong)Seed + 1 };
        _lakes.Clear();

        // The big lake goes in first, filling the north-east corner just inside the mountains, so the small ones keep clear of it.
        var bigCentre = BigLakeCentre;
        var big = new Lake(bigCentre, BigLakeRadius, WaterLine(bigCentre, BigLakeRadius, BigLakeShore), BigLakeShore);
        _lakes.Add(big);
        Carve(big);

        int placed = 0;
        for (int attempt = 0; placed < LakeCount && attempt < 500; attempt++)
        {
            float radius = rng.RandfRange(14f, 24f);
            // The first lake goes near the spawn point so there's always water close by.
            float distance = placed == 0 ? radius + 30f : rng.RandfRange(60f, HalfSize - MountainWidth - radius - ShoreWidth);
            float angle = rng.Randf() * Mathf.Tau;
            var centre = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;

            if (GetHeight(centre.X, centre.Y) > HeightScale * 0.2f)
                continue;

            bool overlaps = false;
            foreach (var other in _lakes)
                overlaps |= centre.DistanceTo(other.Centre) < radius + other.Radius + ShoreWidth + other.Shore;
            if (overlaps)
                continue;

            var lake = new Lake(centre, radius, WaterLine(centre, radius, ShoreWidth));
            _lakes.Add(lake);
            Carve(lake);
            placed++;
        }

        // The lake by the spawn point comes first, and the big lake second, so the otter rafts live on those two.
        _lakes.RemoveAt(0);
        _lakes.Insert(Mathf.Min(1, _lakes.Count), big);
    }

    private Vector2 BigLakeCentre
    {
        get
        {
            float corner = HalfSize - MountainWidth - BigLakeRadius - BigLakeShore;
            return new Vector2(corner, -corner);
        }
    }

    /// <summary>
    /// Where to set a lake's water: just below the lowest point of the bank round it, so it never spills. A big lake
    /// is checked at more points round its rim, so no dip in its bank is missed.
    /// </summary>
    private float WaterLine(Vector2 centre, float radius, float shore)
    {
        float lowest = float.MaxValue;
        int samples = Mathf.Max(32, Mathf.CeilToInt((radius + shore) * 0.8f));
        foreach (float ring in new[] { radius, radius + shore * 0.5f, radius + shore })
        for (int k = 0; k < samples; k++)
        {
            float a = k * Mathf.Tau / samples;
            lowest = Mathf.Min(lowest, GetHeight(centre.X + Mathf.Cos(a) * ring, centre.Y + Mathf.Sin(a) * ring));
        }
        return lowest - 0.4f;
    }

    /// <summary>Digs a bowl below the water line and blends a sloping bank back up to the original ground.</summary>
    private void Carve(Lake lake)
    {
        float reach = lake.Radius + lake.Shore;
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

            // Inside a cave the floor is bare, dusty rock; everywhere else the ground shades as the top of the land does.
            bool dug = _caveMask[i];
            var normal = NormalAt(dug ? _heights : _surface, x, z);
            normals[i] = normal;
            colors[i] = dug ? CaveFloor(vertices[i], normal) : GroundColour(vertices[i].X, vertices[i].Z, h, normal);
        }

        // Ground under a cave roof is left out: the cave's own floor is built with the roof (see BuildCaveRoofs), coloured
        // as the inside of a cave, and the mountainside over it with the roof's top.
        int roofed = Array.FindAll(_roofed, cell => cell).Length;
        var indices = new int[((r - 1) * (r - 1) - roofed) * 6];
        int k = 0;
        for (int z = 0; z < r - 1; z++)
        for (int x = 0; x < r - 1; x++)
        {
            if (IsRoofed(x, z))
                continue;
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

        // Each kind of tree grows in stands of its own, which blend into one another at their edges.
        var kindNoise = new FastNoiseLite
        {
            Seed = Seed + 2,
            NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
            Frequency = 0.02f,
        };

        var material = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            VertexColorIsSrgb = true,
            Roughness = 1f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };

        // A handful of base shapes of every kind, each one a different tree; every placed tree then varies further.
        var kinds = Enum.GetValues<TreeKind>();
        int variantCount = kinds.Length * VariantsPerKind;
        var variants = new TreeBuilder.Shape[variantCount];
        // Trees are drawn a tile of forest at a time, so tiles out of view or far off in the fog are skipped.
        var placements = new Dictionary<(int Variant, Vector2I Tile), List<(Transform3D Transform, Color Tint)>>();
        for (int v = 0; v < variantCount; v++)
        {
            variants[v] = TreeBuilder.Build(kinds[v / VariantsPerKind], rng, material);
        }

        var bodies = new StaticBody3D { Name = "TreeBodies" };
        float range = HalfSize - MountainWidth;
        int placed = 0;

        for (int attempt = 0; placed < TreeCount && attempt < TreeCount * 40; attempt++)
        {
            float x = rng.RandfRange(-range, range);
            float z = rng.RandfRange(-range, range);
            float h = GetHeight(x, z);

            bool nearSpawn = new Vector2(x, z).Length() < 20f;
            bool tooHigh = h > HeightScale * 0.4f;
            if (nearSpawn || tooHigh || IsSteep(x, z) || DistanceFromWater(x, z) < 3f || IsInCave(x, z, margin: 4f))
                continue;

            float density = forestNoise.GetNoise2D(x, z) * 0.5f + 0.5f;
            if (rng.Randf() > 0.05f + Mathf.SmoothStep(0.4f, 0.75f, density))
                continue;

            // Each tree gets its own height, girth, slight lean and brightness on top of its base shape.
            float height = rng.RandfRange(0.75f, 1.3f) * TreeScale;
            float width = height * rng.RandfRange(0.85f, 1.15f);
            float leanDirection = rng.Randf() * Mathf.Tau;
            var leanAxis = new Vector3(Mathf.Cos(leanDirection), 0f, Mathf.Sin(leanDirection));
            var basis = new Basis(leanAxis, rng.RandfRange(0f, 0.07f))
                        * new Basis(Vector3.Up, rng.Randf() * Mathf.Tau)
                        * Basis.FromScale(new Vector3(width, height, width));
            float brightness = rng.RandfRange(0.8f, 1.1f);

            // Mostly the kind whose stand this is, with the odd tree of another kind seeded in among them.
            float stand = Mathf.InverseLerp(0.25f, 0.75f, kindNoise.GetNoise2D(x, z) * 0.5f + 0.5f);
            int kind = rng.Randf() < 0.2f
                ? rng.RandiRange(0, kinds.Length - 1)
                : Mathf.Clamp((int)(stand * kinds.Length), 0, kinds.Length - 1);
            int variant = kind * VariantsPerKind + rng.RandiRange(0, VariantsPerKind - 1);

            var transform = new Transform3D(basis, new Vector3(x, h - 0.2f, z));
            var tile = new Vector2I(Mathf.FloorToInt(x / TreeTileSize), Mathf.FloorToInt(z / TreeTileSize));
            if (!placements.TryGetValue((variant, tile), out var inTile))
                placements[(variant, tile)] = inTile = [];
            inTile.Add((transform, new Color(brightness, brightness, brightness)));
            var tree = new Tree(transform, variants[variant]);
            _trees.Add(tree);
            _thickestTrunk = Mathf.Max(_thickestTrunk, tree.CollisionRadius);

            // Trunks are solid so animals can't walk through trees, each as thick as its own trunk.
            var trunk = new CylinderShape3D { Radius = tree.CollisionRadius, Height = 8f * height };
            bodies.AddChild(new CollisionShape3D { Shape = trunk, Position = new Vector3(x, h + 4f * height, z) });
            placed++;
        }

        AddChild(bodies);
        foreach (var ((variant, tile), instances) in placements)
            AddTreeTile(variants[variant].Mesh, tile, instances);
    }

    /// <summary>
    /// Draws every tree in one tile of forest that shares one base shape in a single draw call using a MultiMesh,
    /// hidden once the tile is so far off it would be lost in the fog anyway.
    /// </summary>
    private void AddTreeTile(Mesh mesh, Vector2I tile, List<(Transform3D Transform, Color Tint)> instances)
    {
        // Place the tile at its middle, so the view distance is measured from there rather than the world's centre.
        var centre = new Vector3((tile.X + 0.5f) * TreeTileSize, 0f, (tile.Y + 0.5f) * TreeTileSize);
        var multiMesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            Mesh = mesh,
            InstanceCount = instances.Count,
        };

        for (int i = 0; i < instances.Count; i++)
        {
            multiMesh.SetInstanceTransform(i, instances[i].Transform.Translated(-centre));
            multiMesh.SetInstanceColor(i, instances[i].Tint);
        }

        AddChild(new MultiMeshInstance3D { Multimesh = multiMesh, Position = centre, VisibilityRangeEnd = TreeViewDistance });
    }
}
