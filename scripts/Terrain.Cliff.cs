using Godot;

namespace IceAgeWorld;

/// <summary>
/// A great cliff: a sheer wall of rock along the line from <see cref="Start"/> to <see cref="End"/>, rising
/// <see cref="Height"/> metres to the high ground on the side <see cref="Back"/> points to.
/// </summary>
public readonly record struct Cliff(Vector2 Start, Vector2 End, float Height)
{
    /// <summary>Level direction from the foot of the cliff in towards the high ground behind its top.</summary>
    public Vector2 Back => (End - Start).Normalized().Orthogonal();
}

public partial class Terrain
{
    /// <summary>
    /// How tall the cliff is, and how long. It is a cuesta, like the escarpments the ice-age rivers and glaciers cut into
    /// tilted beds of rock: a sheer scarp face on one side, and on the other a long, gentle dip slope that runs back
    /// down to the steppe, so an animal can walk up round the back and look out over the edge.
    /// </summary>
    [Export] public float CliffHeight { get; set; } = 26f;

    [Export] public float CliffLength { get; set; } = 380f;

    /// <summary>How far the dip slope runs back from the top of the cliff before it is down to the steppe again.</summary>
    private const float CliffDipLength = 170f;

    /// <summary>
    /// How wide the scarp face is from foot to top: on 2 m ground cells, a few metres makes it all but sheer, too steep
    /// for anything but a snow leopard to climb.
    /// </summary>
    private const float CliffFace = 3f;

    /// <summary>How far the cliff fades away to nothing at each end, so it rises out of the land rather than starting as a wall.</summary>
    private const float CliffTaper = 60f;

    /// <summary>How far the edge of the cliff wanders in and out of a straight line, as weathering breaks it into bays and buttresses.</summary>
    private const float CliffWander = 14f;

    private FastNoiseLite _cliffEdge = null!;

    public Cliff Cliff { get; private set; }

    /// <summary>
    /// Places the cliff to the south-west of the meadow in the middle, its face turned towards it so it stands up out of
    /// the fog as you look across the steppe, and the high ground behind it away from the start.
    /// </summary>
    private void PlaceCliff()
    {
        _cliffEdge = new FastNoiseLite { Seed = Seed + 6, NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth, Frequency = 0.02f };

        var outward = new Vector2(-0.6f, 0.8f);
        var middle = outward * 260f;
        var along = new Vector2(0.8f, 0.6f) * (CliffLength / 2f);
        // Back points from the start of the line round to its left, so run it the way that leaves the high ground outward.
        Cliff = new Cliff(middle + along, middle - along, CliffHeight);
    }

    /// <summary>How far from the line along the foot of the cliff a point is, ignoring the high ground's depth.</summary>
    private float DistanceFromCliff(Vector2 point)
    {
        var line = Cliff.End - Cliff.Start;
        float t = Mathf.Clamp((point - Cliff.Start).Dot(line) / line.LengthSquared(), 0f, 1f);
        return point.DistanceTo(Cliff.Start + line * t);
    }

    /// <summary>
    /// How high the cliff raises the land at a point: nothing in front of it, the full height just behind its edge, then
    /// sloping gently away down the back, and fading out at both ends.
    /// </summary>
    private float CliffHeightAt(float x, float z)
    {
        var point = new Vector2(x, z);
        var line = Cliff.End - Cliff.Start;
        float length = line.Length();
        float along = (point - Cliff.Start).Dot(line) / length;
        if (along < -CliffTaper || along > length + CliffTaper)
            return 0f;

        float behind = (point - Cliff.Start).Dot(Cliff.Back) + CliffWander * _cliffEdge.GetNoise2D(x, z);
        if (behind < -CliffFace || behind > CliffDipLength)
            return 0f;

        float ends = Mathf.SmoothStep(-CliffTaper, 0f, along) * (1f - Mathf.SmoothStep(length, length + CliffTaper, along));
        float face = Mathf.SmoothStep(-CliffFace, 0f, behind);
        // The dip slope falls away slowly at first, so there is a broad, level top to stand on at the edge.
        float dip = 1f - Mathf.SmoothStep(CliffDipLength * 0.15f, CliffDipLength, behind);
        return Cliff.Height * ends * face * dip;
    }
}
