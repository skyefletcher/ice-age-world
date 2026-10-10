using System.Collections.Generic;
using Godot;

namespace IceAgeWorld;

/// <summary>A great mountain rising out of the steppe: where it stands, how far its foothills spread and how tall it is.</summary>
public readonly record struct Mountain(Vector2 Centre, float Radius, float Peak);

public partial class Terrain
{
    private readonly List<Mountain> _mountains = [];

    /// <summary>Ridges and gullies running down the mountainsides, and the ragged spurs that break up their outlines.</summary>
    private FastNoiseLite _ridges = null!;
    private FastNoiseLite _outline = null!;

    public IReadOnlyList<Mountain> Mountains => _mountains;

    /// <summary>
    /// Picks where the mountains stand: well away from the meadow in the middle, where the player starts, clear of the big
    /// lake, and not piled on top of one another, though their foothills may run together or into the ring round the edge.
    /// </summary>
    private void PlaceMountains()
    {
        _ridges = new FastNoiseLite
        {
            Seed = Seed + 4,
            NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
            FractalType = FastNoiseLite.FractalTypeEnum.Ridged,
            FractalOctaves = 4,
            Frequency = 0.012f,
        };
        _outline = new FastNoiseLite { Seed = Seed + 5, NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth, Frequency = 0.01f };

        var rng = new RandomNumberGenerator { Seed = (ulong)Seed + 3 };
        _mountains.Clear();
        for (int attempt = 0; _mountains.Count < MountainCount && attempt < 400; attempt++)
        {
            float radius = rng.RandfRange(MountainRadii.X, MountainRadii.Y);
            float peak = rng.RandfRange(MountainHeights.X, MountainHeights.Y);
            float reach = HalfSize - MountainWidth - radius * 0.5f;
            var centre = new Vector2(rng.RandfRange(-reach, reach), rng.RandfRange(-reach, reach));

            if (centre.Length() < radius + 90f || centre.DistanceTo(BigLakeCentre) < radius + BigLakeRadius + BigLakeShore + 10f)
                continue;
            bool crowded = false;
            foreach (var other in _mountains)
                crowded |= centre.DistanceTo(other.Centre) < (radius + other.Radius) * 0.8f;
            if (!crowded)
                _mountains.Add(new Mountain(centre, radius, peak));
        }
    }

    /// <summary>
    /// How high the mountains raise the land at a point. Each rises from foothills that start almost flat, so they blend
    /// into the steppe, to a sharp peak, its flanks scored by ridges that stand out more the higher they go: gentle enough
    /// low down for an animal to walk up, but too steep and craggy near the top for any but a sure-footed climber.
    /// </summary>
    private float MountainHeight(float x, float z)
    {
        float height = 0f;
        foreach (var mountain in _mountains)
        {
            float d = new Vector2(x, z).DistanceTo(mountain.Centre) / mountain.Radius;
            d *= 1f + 0.3f * _outline.GetNoise2D(x, z);
            if (d >= 1f)
                continue;
            float shape = Mathf.Pow(1f - d, 1.7f);
            float ridge = _ridges.GetNoise2D(x, z) * 0.5f + 0.5f;
            float crags = 1f + (ridge - 0.5f) * 0.7f * Mathf.Sqrt(shape);
            height = Mathf.Max(height, mountain.Peak * shape * crags);
        }
        return height;
    }
}
