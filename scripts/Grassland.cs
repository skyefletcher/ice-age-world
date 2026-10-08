using System;
using System.Collections.Generic;
using Godot;

namespace IceAgeWorld;

/// <summary>
/// A continuous, edible meadow covering the green steppe. The ground is divided into a grid of small,
/// overlapping grass patches; eating flattens the patches around the bite to stubble and they regrow over time.
/// Patches are drawn in chunks, one MultiMesh each, and distant chunks are hidden to keep rendering cheap.
/// </summary>
public partial class Grassland : Node3D
{
    /// <summary>World units between grass patches.</summary>
    [Export] public float Spacing { get; set; } = 1f;

    /// <summary>Size of each drawn chunk of grass, in world units.</summary>
    [Export] public float ChunkSize { get; set; } = 16f;

    /// <summary>Chunks further than this from the camera are not drawn.</summary>
    [Export] public float ViewDistance { get; set; } = 70f;

    /// <summary>Radius of grass flattened by one mouthful.</summary>
    [Export] public float BiteRadius { get; set; } = 5f;

    /// <summary>Seconds grass stays as stubble before it starts regrowing.</summary>
    [Export] public float RegrowDelay { get; set; } = 45f;

    /// <summary>Seconds grass takes to grow from stubble back to full height.</summary>
    [Export] public float RegrowDuration { get; set; } = 30f;

    [Export] public int Seed { get; set; } = 99;

    private const float StubbleHeight = 0.12f;
    private static readonly Color StubbleTint = new(0.75f, 0.62f, 0.45f);

    private struct Patch
    {
        public Vector3 Position;
        public Vector3 LocalPosition;
        public Basis Basis;
        public MultiMesh MultiMesh;
        public int Instance;
        public float Growth;
    }

    private Patch[] _patches = [];
    private int[] _cellToPatch = [];
    private int _cellsPerSide;
    private float _halfSize;
    private readonly Dictionary<int, double> _eatenAt = [];

    /// <summary>Covers the grassy parts of the terrain with patches of grass.</summary>
    public void Populate(Terrain terrain)
    {
        var rng = new RandomNumberGenerator { Seed = (ulong)Seed };
        _halfSize = terrain.HalfSize;
        _cellsPerSide = (int)(2f * _halfSize / Spacing);
        _cellToPatch = new int[_cellsPerSide * _cellsPerSide];
        Array.Fill(_cellToPatch, -1);

        var patches = new List<Patch>();
        var chunks = new Dictionary<Vector2I, List<int>>();

        for (int cz = 0; cz < _cellsPerSide; cz++)
        for (int cx = 0; cx < _cellsPerSide; cx++)
        {
            // Jitter each patch inside its cell so the meadow doesn't look like a grid.
            float x = -_halfSize + (cx + rng.Randf()) * Spacing;
            float z = -_halfSize + (cz + rng.Randf()) * Spacing;
            float amount = terrain.GrassAmount(x, z);
            if (amount < 0.2f)
                continue;

            var chunk = new Vector2I(
                Mathf.FloorToInt((x + _halfSize) / ChunkSize),
                Mathf.FloorToInt((z + _halfSize) / ChunkSize));
            if (!chunks.TryGetValue(chunk, out var members))
                chunks[chunk] = members = [];

            _cellToPatch[cz * _cellsPerSide + cx] = patches.Count;
            members.Add(patches.Count);
            patches.Add(new Patch
            {
                Position = new Vector3(x, terrain.GetHeight(x, z) - 0.05f, z),
                Basis = Basis.Identity
                    .Rotated(Vector3.Up, rng.Randf() * Mathf.Tau)
                    .Scaled(new Vector3(1f, amount * rng.RandfRange(0.8f, 1.2f), 1f)),
                Growth = 1f,
            });
        }

        _patches = patches.ToArray();
        var mesh = BuildPatchMesh(rng);

        foreach (var (chunk, members) in chunks)
        {
            var origin = new Vector3(
                (chunk.X + 0.5f) * ChunkSize - _halfSize, 0f,
                (chunk.Y + 0.5f) * ChunkSize - _halfSize);
            var multiMesh = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                UseColors = true,
                Mesh = mesh,
                InstanceCount = members.Count,
            };

            for (int k = 0; k < members.Count; k++)
            {
                ref var patch = ref _patches[members[k]];
                patch.MultiMesh = multiMesh;
                patch.Instance = k;
                patch.LocalPosition = patch.Position - origin;
                UpdateInstance(members[k]);
            }

            AddChild(new MultiMeshInstance3D
            {
                Multimesh = multiMesh,
                Position = origin,
                VisibilityRangeEnd = ViewDistance,
                // Thousands of blades casting shadows would be expensive and barely visible.
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
        }
    }

    /// <summary>Index of the nearest fully grown grass patch within <paramref name="radius"/> of a point, or -1.</summary>
    public int FindEdible(Vector3 point, float radius)
    {
        int best = -1;
        float bestDistance = radius * radius;

        foreach (int index in PatchesNear(point, radius))
        {
            if (_patches[index].Growth < 1f)
                continue;

            float distance = FlatDistanceSquared(point, _patches[index].Position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = index;
            }
        }

        return best;
    }

    /// <summary>Flattens the grass within <see cref="BiteRadius"/> of a patch. It regrows after <see cref="RegrowDelay"/>.</summary>
    public void Eat(int index)
    {
        var centre = _patches[index].Position;
        foreach (int i in PatchesNear(centre, BiteRadius))
        {
            if (FlatDistanceSquared(centre, _patches[i].Position) > BiteRadius * BiteRadius)
                continue;

            _patches[i].Growth = 0f;
            _eatenAt[i] = Now;
            UpdateInstance(i);
        }
    }

    public override void _Process(double delta)
    {
        if (_eatenAt.Count == 0)
            return;

        var finished = new List<int>();
        foreach (var (index, eatenAt) in _eatenAt)
        {
            if (Now - eatenAt < RegrowDelay)
                continue;

            ref var patch = ref _patches[index];
            patch.Growth = Mathf.Min(1f, patch.Growth + (float)delta / RegrowDuration);
            UpdateInstance(index);
            if (patch.Growth >= 1f)
                finished.Add(index);
        }

        foreach (var index in finished)
            _eatenAt.Remove(index);
    }

    private static double Now => Time.GetTicksMsec() / 1000.0;

    private static float FlatDistanceSquared(Vector3 a, Vector3 b) =>
        new Vector2(a.X, a.Z).DistanceSquaredTo(new Vector2(b.X, b.Z));

    /// <summary>Patches in the grid cells overlapping a circle; callers still check the exact distance.</summary>
    private IEnumerable<int> PatchesNear(Vector3 point, float radius)
    {
        int minX = Math.Max(0, Mathf.FloorToInt((point.X - radius + _halfSize) / Spacing));
        int maxX = Math.Min(_cellsPerSide - 1, Mathf.FloorToInt((point.X + radius + _halfSize) / Spacing));
        int minZ = Math.Max(0, Mathf.FloorToInt((point.Z - radius + _halfSize) / Spacing));
        int maxZ = Math.Min(_cellsPerSide - 1, Mathf.FloorToInt((point.Z + radius + _halfSize) / Spacing));

        for (int cz = minZ; cz <= maxZ; cz++)
        for (int cx = minX; cx <= maxX; cx++)
        {
            int index = _cellToPatch[cz * _cellsPerSide + cx];
            if (index >= 0)
                yield return index;
        }
    }

    private void UpdateInstance(int index)
    {
        ref var patch = ref _patches[index];
        float height = Mathf.Lerp(StubbleHeight, 1f, patch.Growth);
        var basis = patch.Basis.Scaled(new Vector3(1f, height, 1f));

        patch.MultiMesh.SetInstanceTransform(patch.Instance, new Transform3D(basis, patch.LocalPosition));
        patch.MultiMesh.SetInstanceColor(patch.Instance, StubbleTint.Lerp(Colors.White, patch.Growth));
    }

    /// <summary>Builds one patch: thin blades spread wide enough to overlap neighbouring patches, darker at the root.</summary>
    private static ArrayMesh BuildPatchMesh(RandomNumberGenerator rng)
    {
        var root = new Color(0.36f, 0.42f, 0.24f);
        var tips = new[] { new Color(0.62f, 0.66f, 0.4f), new Color(0.72f, 0.7f, 0.44f), new Color(0.5f, 0.6f, 0.34f) };

        var vertices = new List<Vector3>();
        var colors = new List<Color>();
        var normals = new List<Vector3>();

        for (int blade = 0; blade < 32; blade++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            var lean = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            var side = new Vector3(-lean.Z, 0, lean.X) * 0.05f;
            var basePoint = new Vector3(rng.RandfRange(-0.75f, 0.75f), 0, rng.RandfRange(-0.75f, 0.75f));
            var tipPoint = basePoint + lean * rng.RandfRange(0.1f, 0.3f) + Vector3.Up * rng.RandfRange(0.35f, 0.8f);
            var tip = tips[rng.RandiRange(0, tips.Length - 1)];

            vertices.AddRange([basePoint - side, basePoint + side, tipPoint]);
            colors.AddRange([root, root, tip]);
            // Upward normals light the blades like the ground beneath them, so the meadow blends into the terrain.
            normals.AddRange([Vector3.Up, Vector3.Up, Vector3.Up]);
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = normals.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = colors.ToArray();

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.SurfaceSetMaterial(0, new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            VertexColorIsSrgb = true,
            Roughness = 1f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        });
        return mesh;
    }
}
