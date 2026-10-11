using System.Collections.Generic;
using Godot;

namespace IceAgeWorld;

/// <summary>One rocky hill in a mountain range: where it stands, how far its slopes spread and how tall it is.</summary>
public readonly record struct Mountain(Vector2 Centre, float Radius, float Peak);

public partial class Terrain
{
    private readonly List<Mountain> _mountains = [];

    /// <summary>
    /// Gullies running down the hillsides, the ragged spurs that break up their outlines, the tors and boulders
    /// heaped on them, and where bare rock shows through the snow.
    /// </summary>
    private FastNoiseLite _ridges = null!;
    private FastNoiseLite _outline = null!;
    private FastNoiseLite _tors = null!;
    private FastNoiseLite _bareRock = null!;

    /// <summary>How sharply the slopes of neighbouring hills run together into saddles, rather than meeting in a crease.</summary>
    private const float SaddleBlend = 12f;

    /// <summary>Height of the rock ledges the hillsides step down in, like weathered granite.</summary>
    private const float LedgeHeight = 5f;

    public IReadOnlyList<Mountain> Mountains => _mountains;

    /// <summary>
    /// Picks where the ranges stand, well away from the meadow in the middle, where the player starts, clear of the big
    /// lake and of each other, then heaps rocky hills together in each: the tallest in the middle, lower ones crowding
    /// round it so their slopes run into one another, as hills do in a range worn down by the ice.
    /// </summary>
    private void PlaceMountains()
    {
        _ridges = new FastNoiseLite
        {
            Seed = Seed + 4,
            NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
            FractalType = FastNoiseLite.FractalTypeEnum.Ridged,
            FractalOctaves = 4,
            Frequency = 0.02f,
        };
        _outline = new FastNoiseLite { Seed = Seed + 5, NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth, Frequency = 0.015f };
        // Cells about 20 m across, each heaping up one tor, so the hills are strewn with knobbly outcrops.
        _tors = new FastNoiseLite
        {
            Seed = Seed + 7,
            NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular,
            CellularReturnType = FastNoiseLite.CellularReturnTypeEnum.Distance,
            CellularJitter = 0.9f,
            Frequency = 0.05f,
        };
        _bareRock = new FastNoiseLite
        {
            Seed = Seed + 8,
            NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
            FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
            FractalOctaves = 3,
            Frequency = 0.03f,
        };

        var rng = new RandomNumberGenerator { Seed = (ulong)Seed + 3 };
        _mountains.Clear();
        var ranges = new List<Vector2>();
        float reach = HalfSize - MountainWidth - RangeSpread * 0.6f;
        for (int attempt = 0; ranges.Count < RangeCount && attempt < 400; attempt++)
        {
            var centre = new Vector2(rng.RandfRange(-reach, reach), rng.RandfRange(-reach, reach));
            bool clear = centre.Length() > RangeSpread + 110f
                && centre.DistanceTo(BigLakeCentre) > RangeSpread + BigLakeRadius + BigLakeShore + 20f;
            foreach (var other in ranges)
                clear &= centre.DistanceTo(other) > RangeSpread * 2.6f;
            if (clear)
                ranges.Add(centre);
        }

        foreach (var range in ranges)
        {
            int placed = 0;
            for (int attempt = 0; placed < HillsPerRange && attempt < 300; attempt++)
            {
                // The first hill is the heart of the range; the rest scatter round it, lower the further out they stand.
                var offset = placed == 0
                    ? Vector2.Zero
                    : Vector2.Right.Rotated(rng.Randf() * Mathf.Tau) * RangeSpread * Mathf.Sqrt(rng.RandfRange(0.15f, 1f));
                var centre = range + offset;
                float outward = offset.Length() / RangeSpread;
                float radius = rng.RandfRange(MountainRadii.X, MountainRadii.Y) * (1f - 0.3f * outward);
                float peak = rng.RandfRange(MountainHeights.X, MountainHeights.Y) * (1f - 0.45f * outward);

                float limit = HalfSize - MountainWidth - radius * 0.4f;
                if (Mathf.Abs(centre.X) > limit || Mathf.Abs(centre.Y) > limit)
                    continue;
                if (centre.Length() < radius + 80f || centre.DistanceTo(BigLakeCentre) < radius + BigLakeRadius + BigLakeShore)
                    continue;
                // Close enough that their slopes merge, but not so close that two hills stand as one.
                bool crowded = false;
                foreach (var other in _mountains)
                    crowded |= centre.DistanceTo(other.Centre) < (radius + other.Radius) * 0.4f;
                if (crowded)
                    continue;

                _mountains.Add(new Mountain(centre, radius, peak));
                placed++;
            }
        }
    }

    /// <summary>
    /// How high the hills raise the land at a point. Each is a rounded dome whose foot blends into the steppe, and where
    /// hills meet their slopes run together in saddles. The slopes step down in rough ledges, scored by gullies and
    /// heaped with tors, so there is always a way up for a sure-footed animal, scrambling from ledge to ledge.
    /// </summary>
    private float MountainHeight(float x, float z)
    {
        float height = 0f;
        float shape = 0f;
        foreach (var mountain in _mountains)
        {
            float d = new Vector2(x, z).DistanceTo(mountain.Centre) / mountain.Radius;
            d *= 1f + 0.25f * _outline.GetNoise2D(x, z);
            if (d >= 1f)
                continue;
            // A dome, flat on top and fading out flat at the foot, rather than a sharp peak.
            float dome = (1f - d * d) * (1f - d * d);
            height = SmoothMax(height, mountain.Peak * dome, SaddleBlend);
            shape = Mathf.Max(shape, dome);
        }
        if (shape <= 0f)
            return 0f;

        // Gullies cut into the slopes, deepest half way up where the meltwater runs fastest.
        float gully = _ridges.GetNoise2D(x, z) * 0.5f + 0.5f;
        height *= 1f + (gully - 0.5f) * 0.25f * Mathf.Sqrt(shape);

        // Rock ledges: the slopes step down in treads and short steep risers, rounded off so they can be climbed.
        // The beds of rock tilt and wander, so the ledges break off and run into one another rather than ringing the hill.
        float tilt = _bareRock.GetNoise2D(x * 0.4f, z * 0.4f) * LedgeHeight * 1.5f;
        float steps = (height + tilt) / LedgeHeight;
        float tread = Mathf.Floor(steps);
        float stepped = (tread + Mathf.SmoothStep(0.25f, 0.75f, steps - tread)) * LedgeHeight - tilt;
        height = Mathf.Lerp(height, stepped, 0.55f * Mathf.SmoothStep(0.05f, 0.3f, shape));

        // Tors: knobbly heaps of boulders standing up out of the hillsides, a few even down on the foothills.
        height += Tor(x, z) * 7f * Mathf.SmoothStep(0f, 0.4f, shape);
        return height;
    }

    /// <summary>How much a tor stands up at a point: 1 at the top of a heap of boulders, 0 between them.</summary>
    private float Tor(float x, float z)
    {
        // Distance to the nearest cell centre is about -1 there and rises towards 0 between cells.
        float centre = Mathf.Clamp(-_tors.GetNoise2D(x, z), 0f, 1f);
        return Mathf.SmoothStep(0.45f, 0.95f, centre);
    }

    /// <summary>
    /// How much bare rock shows through on the hills at a point: on the tors and in patches where the wind keeps the
    /// snow from lying, fading out down on the steppe.
    /// </summary>
    private float HillRock(float x, float z)
    {
        float shape = 0f;
        foreach (var mountain in _mountains)
        {
            float d = new Vector2(x, z).DistanceTo(mountain.Centre) / mountain.Radius;
            if (d < 1.2f)
                shape = Mathf.Max(shape, 1f - d / 1.2f);
        }
        if (shape <= 0f)
            return 0f;
        float patches = Mathf.SmoothStep(-0.05f, 0.2f, _bareRock.GetNoise2D(x, z));
        return Mathf.Max(patches, Tor(x, z)) * Mathf.SmoothStep(0.05f, 0.35f, shape);
    }

    /// <summary>
    /// The larger of two heights, eased into each other where they are close, so overlapping hills meet in a saddle. The
    /// easing shrinks to nothing as either height does, so a hill's foot still fades out flat with no lip.
    /// </summary>
    private static float SmoothMax(float a, float b, float blend)
    {
        blend = Mathf.Min(blend, Mathf.Min(a, b));
        if (blend <= 0.001f)
            return Mathf.Max(a, b);
        float h = Mathf.Clamp(0.5f + 0.5f * (a - b) / blend, 0f, 1f);
        return Mathf.Lerp(b, a, h) + blend * h * (1f - h);
    }
}
