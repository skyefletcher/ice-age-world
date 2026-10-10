using Godot;

namespace IceAgeWorld;

/// <summary>
/// Spots of blood in the snow where drops have fallen, so a wounded animal leaves a trail a hunter can follow. They are
/// all drawn together as one multimesh; once there are as many as it holds, each new spot takes the place of the oldest.
/// </summary>
public partial class BloodTrail : MultiMeshInstance3D
{
    /// <summary>The most spots there are at once in the whole world.</summary>
    private const int MaxSpots = 600;

    private Terrain _terrain = null!;
    private int _next;
    private readonly RandomNumberGenerator _rng = new();

    public void Build(Terrain terrain)
    {
        _terrain = terrain;
        _rng.Randomize();
        TopLevel = true;
        CastShadow = ShadowCastingSetting.Off;
        Multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = Blood.Spot(),
            InstanceCount = MaxSpots,
            VisibleInstanceCount = 0,
        };
    }

    /// <summary>
    /// A drop lands somewhere around <paramref name="under"/>, within <paramref name="scatter"/> metres, and leaves a spot
    /// up to <paramref name="size"/> metres across. Nothing shows on water, where the blood washes away.
    /// </summary>
    public void Drip(Vector3 under, float scatter, float size, Water? water)
    {
        if (_terrain is null)
            return;
        var at = under + new Vector3(_rng.RandfRange(-scatter, scatter), 0f, _rng.RandfRange(-scatter, scatter));
        if (water?.SurfaceAt(at) is { } surface && surface > _terrain.GroundBelow(at))
            return;

        // Each spot turned its own way and stretched a little, as a drop that hits the snow at a slant splashes out longer.
        var place = Blood.OnGround(_terrain, at, _rng.RandfRange(0f, Mathf.Tau), 0.02f + _next % 7 * 0.001f);
        float across = size * _rng.RandfRange(0.25f, 0.5f);
        place.Basis *= Basis.FromScale(new Vector3(across * _rng.RandfRange(0.7f, 1.5f), 1f, across));
        Multimesh.SetInstanceTransform(_next, place);
        _next = (_next + 1) % MaxSpots;
        Multimesh.VisibleInstanceCount = Mathf.Max(Multimesh.VisibleInstanceCount, _next == 0 ? MaxSpots : _next);
    }
}
