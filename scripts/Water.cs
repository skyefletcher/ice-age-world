using System.Collections.Generic;
using Godot;

namespace IceAgeWorld;

/// <summary>
/// Draws the water surface of each lake and the ocean, rolling with waves (see water.gdshader) and seen from above and
/// from below, and answers "is there water here, and how high?" for drinking, swimming and diving, waves and all.
/// </summary>
public partial class Water : Node3D
{
    /// <summary>
    /// The waves the ocean's swell is made of: direction of travel, wavelength in metres and share of the height. They
    /// roll in from the open sea in the corner towards the beach, the long swell straight in and shorter waves at angles
    /// across it, so the crests break up rather than marching in like ruled lines.
    /// </summary>
    private static readonly (Vector2 Direction, float Length, float Share)[] Waves =
    [
        (new Vector2(-1f, 1f).Normalized(), 30f, 0.4f),
        (new Vector2(-0.45f, 0.9f).Normalized(), 18f, 0.25f),
        (new Vector2(-0.9f, 0.45f).Normalized(), 12f, 0.2f),
        (new Vector2(-0.2f, 1f).Normalized(), 7f, 0.15f),
    ];

    /// <summary>How high the ocean's waves rise above its level, at their highest.</summary>
    private const float OceanWaves = 0.9f;

    private IReadOnlyList<Lake> _lakes = [];
    private Terrain _terrain = null!;
    private readonly List<float> _waveHeights = [];
    private readonly List<ShaderMaterial> _materials = [];
    private float _time;

    public void Build(Terrain terrain)
    {
        _terrain = terrain;
        _lakes = terrain.Lakes;
        var shader = GD.Load<Shader>("res://shaders/water.gdshader");
        var waves = new Godot.Collections.Array();
        foreach (var wave in Waves)
            waves.Add(new Vector4(wave.Direction.X, wave.Direction.Y, wave.Length, wave.Share));

        foreach (var lake in _lakes)
        {
            // The water reaches part-way up the bank, so its edge is always hidden under the shore. Only the ocean
            // reaches beyond the world, its true centre out in the corner.
            float radius = lake.Radius + lake.Shore * 0.6f;
            bool ocean = Mathf.Max(Mathf.Abs(lake.Centre.X), Mathf.Abs(lake.Centre.Y)) + radius > terrain.HalfSize;
            // The small lakes are sheltered and lie still.
            float height = ocean ? OceanWaves : 0f;
            _waveHeights.Add(height);

            var material = new ShaderMaterial { Shader = shader };
            material.SetShaderParameter("waves", waves);
            material.SetShaderParameter("wave_height", height);
            material.SetShaderParameter("open_sea", ocean ? 1f : 0f);
            _materials.Add(material);

            AddChild(new MeshInstance3D
            {
                // Fine enough to carry the shortest waves.
                Mesh = Sheet(lake, radius, terrain.HalfSize, 2f, material),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                ExtraCullMargin = height * 2f + 1f,
            });
        }
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        foreach (var material in _materials)
            material.SetShaderParameter("wave_time", _time);
    }

    /// <summary>
    /// A flat grid of water over a lake, <paramref name="cell"/> metres to a square so the waves can heave it, cut to
    /// the circle and to the world: drawn whole, three quarters of the ocean would hang out over nothing beyond the
    /// mountains. Points just beyond the circle are pulled in onto it, so the edge runs round smoothly, not in steps.
    /// </summary>
    private static ArrayMesh Sheet(Lake lake, float radius, float halfSize, float cell, Material material)
    {
        float minX = Mathf.Max(lake.Centre.X - radius, -halfSize), maxX = Mathf.Min(lake.Centre.X + radius, halfSize);
        float minZ = Mathf.Max(lake.Centre.Y - radius, -halfSize), maxZ = Mathf.Min(lake.Centre.Y + radius, halfSize);
        int across = Mathf.CeilToInt((maxX - minX) / cell), down = Mathf.CeilToInt((maxZ - minZ) / cell);
        float level = lake.Surface - 0.01f;

        var mesh = new MeshBuilder();
        var index = new int[across + 1, down + 1];
        for (int z = 0; z <= down; z++)
        for (int x = 0; x <= across; x++)
        {
            var point = new Vector2(Mathf.Min(minX + x * cell, maxX), Mathf.Min(minZ + z * cell, maxZ));
            if (point.DistanceTo(lake.Centre) > radius)
                point = lake.Centre + (point - lake.Centre).Normalized() * radius;
            index[x, z] = mesh.Add(new Vector3(point.X, level, point.Y), Vector3.Up, Colors.White);
        }

        for (int z = 0; z < down; z++)
        for (int x = 0; x < across; x++)
        {
            var middle = new Vector2(minX + (x + 0.5f) * cell, minZ + (z + 0.5f) * cell);
            if (middle.DistanceTo(lake.Centre) < radius + cell * 1.5f)
                mesh.Quad(index[x, z], index[x + 1, z], index[x + 1, z + 1], index[x, z + 1]);
        }
        return mesh.Commit(material);
    }

    /// <summary>How far the waves lift the water above lake <paramref name="i"/>'s level at a point: the shader's sum.</summary>
    private float Swell(int i, float x, float z)
    {
        float height = _waveHeights[i];
        float rise = 0f;
        foreach (var wave in Waves)
        {
            float k = Mathf.Tau / wave.Length;
            rise += wave.Share * height * Mathf.Sin(k * wave.Direction.Dot(new Vector2(x, z)) - Mathf.Sqrt(9.8f * k) * _time);
        }
        return rise;
    }

    /// <summary>
    /// Height of the water surface above a point, waves and all, or null if the point isn't over a lake. The mountains
    /// standing in the ocean are dry land, however far inside its circle.
    /// </summary>
    public float? SurfaceAt(Vector3 point)
    {
        var flat = new Vector2(point.X, point.Z);
        for (int i = 0; i < _lakes.Count; i++)
        {
            var lake = _lakes[i];
            if (flat.DistanceTo(lake.Centre) < lake.Radius && _terrain.GetHeight(point.X, point.Z) < lake.Surface)
                return lake.Surface + Swell(i, point.X, point.Z);
        }

        return null;
    }

    /// <summary>Whether a point is under the water of a lake.</summary>
    public bool IsUnderwater(Vector3 point) => SurfaceAt(point) is { } surface && point.Y < surface;
}
