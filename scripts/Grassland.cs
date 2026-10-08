using System.Collections.Generic;
using Godot;

namespace IceAgeWorld;

/// <summary>
/// Edible clumps of steppe grass. Clumps are flattened to stubble when eaten and grow back over time.
/// All clumps are drawn in one MultiMesh; eating or regrowing a clump just updates its instance.
/// </summary>
public partial class Grassland : Node3D
{
    [Export] public int ClumpCount { get; set; } = 2500;
    [Export] public int Seed { get; set; } = 99;

    /// <summary>Seconds a clump stays as stubble before it starts regrowing.</summary>
    [Export] public float RegrowDelay { get; set; } = 45f;

    /// <summary>Seconds a clump takes to grow from stubble back to full height.</summary>
    [Export] public float RegrowDuration { get; set; } = 30f;

    private const float StubbleHeight = 0.12f;
    private static readonly Color StubbleTint = new(0.75f, 0.62f, 0.45f);

    private readonly List<Vector3> _positions = [];
    private readonly List<Basis> _bases = [];
    private readonly List<float> _growth = [];
    private readonly Dictionary<int, double> _eatenAt = [];
    private MultiMesh _multiMesh = null!;

    /// <summary>Scatters grass over the grassy parts of the terrain.</summary>
    public void Populate(Terrain terrain)
    {
        var rng = new RandomNumberGenerator { Seed = (ulong)Seed };
        float range = terrain.HalfSize * 0.85f;

        for (int attempt = 0; _positions.Count < ClumpCount && attempt < ClumpCount * 20; attempt++)
        {
            float x = rng.RandfRange(-range, range);
            float z = rng.RandfRange(-range, range);
            if (!terrain.IsGrassy(x, z))
                continue;

            _positions.Add(new Vector3(x, terrain.GetHeight(x, z) - 0.05f, z));
            _bases.Add(Basis.Identity
                .Rotated(Vector3.Up, rng.Randf() * Mathf.Tau)
                .Scaled(Vector3.One * rng.RandfRange(0.8f, 1.3f)));
            _growth.Add(1f);
        }

        _multiMesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            Mesh = BuildClumpMesh(rng),
            InstanceCount = _positions.Count,
        };
        for (int i = 0; i < _positions.Count; i++)
            UpdateInstance(i);

        AddChild(new MultiMeshInstance3D { Multimesh = _multiMesh });
    }

    /// <summary>Index of the nearest uneaten clump within <paramref name="radius"/> of a point, or -1.</summary>
    public int FindEdible(Vector3 point, float radius)
    {
        int best = -1;
        float bestDistance = radius * radius;
        var flat = new Vector2(point.X, point.Z);

        for (int i = 0; i < _positions.Count; i++)
        {
            if (_growth[i] < 1f)
                continue;

            float distance = flat.DistanceSquaredTo(new Vector2(_positions[i].X, _positions[i].Z));
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }

        return best;
    }

    /// <summary>Flattens a clump to stubble. It regrows after <see cref="RegrowDelay"/>.</summary>
    public void Eat(int index)
    {
        _growth[index] = 0f;
        _eatenAt[index] = Now;
        UpdateInstance(index);
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

            _growth[index] = Mathf.Min(1f, _growth[index] + (float)delta / RegrowDuration);
            UpdateInstance(index);
            if (_growth[index] >= 1f)
                finished.Add(index);
        }

        foreach (var index in finished)
            _eatenAt.Remove(index);
    }

    private static double Now => Time.GetTicksMsec() / 1000.0;

    private void UpdateInstance(int index)
    {
        float growth = _growth[index];
        float height = Mathf.Lerp(StubbleHeight, 1f, growth);
        var basis = _bases[index].Scaled(new Vector3(1f, height, 1f));

        _multiMesh.SetInstanceTransform(index, new Transform3D(basis, _positions[index]));
        _multiMesh.SetInstanceColor(index, StubbleTint.Lerp(Colors.White, growth));
    }

    /// <summary>Builds one clump: a ring of thin, outward-leaning blades, darker at the root.</summary>
    private static ArrayMesh BuildClumpMesh(RandomNumberGenerator rng)
    {
        var root = new Color(0.32f, 0.42f, 0.2f);
        var tip = new Color(0.78f, 0.76f, 0.45f);

        var vertices = new List<Vector3>();
        var colors = new List<Color>();
        var normals = new List<Vector3>();

        for (int blade = 0; blade < 28; blade++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            var outward = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            var side = new Vector3(-outward.Z, 0, outward.X) * 0.07f;
            var basePoint = outward * rng.RandfRange(0f, 0.6f);
            var tipPoint = basePoint + outward * rng.RandfRange(0.2f, 0.5f) + Vector3.Up * rng.RandfRange(0.6f, 1.1f);

            vertices.AddRange([basePoint - side, basePoint + side, tipPoint]);
            colors.AddRange([root, root, tip]);
            // Upward normals light the blades like the ground beneath them, which reads well for grass.
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
