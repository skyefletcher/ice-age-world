using Godot;

namespace IceAgeWorld;

/// <summary>
/// The ocean floor: not a flat bowl but a seabed of broad sandy deeps, long rocky reefs and pinnacles of rock reaching up
/// towards the light, as off a rocky cold-water coast, where the kelp forests grow on the reefs and the otters dive.
/// </summary>
public partial class Terrain
{
    /// <summary>The ocean filling the north-east corner, the one lake the underwater world lies in.</summary>
    public Lake Ocean { get; private set; }

    /// <summary>The shallowest the reefs come up under the ocean's level, so they never break the surface.</summary>
    private const float ReefTop = 5f;

    /// <summary>The sand at the bottom of the sea, pale where the light reaches it, and the darker rock of the reefs.</summary>
    private static readonly Color Sand = new(0.6f, 0.55f, 0.43f);
    private static readonly Color SeaRock = new(0.3f, 0.32f, 0.3f);

    private FastNoiseLite? _deeps;
    private FastNoiseLite? _reefs;

    /// <summary>True over the ocean, inside its water line.</summary>
    public bool IsInOcean(float x, float z) => new Vector2(x, z).DistanceTo(Ocean.Centre) < Ocean.Radius;

    /// <summary>
    /// How deep the open ocean is below its level at a point. Gentle swells of sand roll some ten metres either side of
    /// <see cref="OceanDepth"/>, and ridges of rock rise out of them, the crests of the reefs coming within a few metres of the surface.
    /// </summary>
    private float SeabedDepth(Vector2 point)
    {
        _deeps ??= new FastNoiseLite
        {
            Seed = Seed + 7,
            NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
            FractalOctaves = 3,
            Frequency = 0.006f,
        };
        _reefs ??= new FastNoiseLite
        {
            Seed = Seed + 8,
            NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
            FractalType = FastNoiseLite.FractalTypeEnum.Ridged,
            FractalOctaves = 4,
            Frequency = 0.008f,
        };

        float deeps = OceanDepth + 9f * _deeps.GetNoise2Dv(point) - 1f;
        // Ridged noise draws long, winding crests: the reefs. Only the highest ridges rise far, so they stand up out of
        // open sand rather than covering the whole floor.
        float ridge = _reefs.GetNoise2Dv(point) * 0.5f + 0.5f;
        float reef = Mathf.SmoothStep(0.55f, 0.95f, ridge);
        return Mathf.Max(ReefTop, deeps - reef * (deeps - ReefTop) * 1.05f);
    }

    /// <summary>
    /// The sea floor's colour, blended over the land's by how far under the ocean the ground lies: sand in the deeps, and
    /// bare rock wherever the reefs rise steeply.
    /// </summary>
    private Color SeabedColour(Color land, float x, float z, float h, float rock)
    {
        if (!IsInOcean(x, z))
            return land;
        float under = Mathf.SmoothStep(0.5f, 3f, Ocean.Surface - h);
        // The sand is rippled and patchy, darker where weed and silt gather.
        float patch = (_deeps?.GetNoise2D(x * 9f, z * 9f) ?? 0f) * 0.08f;
        // The reefs are rock along their crests, where the sand can't settle, as well as on their steep flanks.
        float crest = Mathf.SmoothStep(0.7f, 0.85f, (_reefs?.GetNoise2D(x, z) ?? 0f) * 0.5f + 0.5f);
        var floor = Sand.Lerp(SeaRock, Mathf.Max(rock, crest));
        return land.Lerp(floor * (1f + patch), under);
    }
}
