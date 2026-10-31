using System.Collections.Generic;
using Godot;

namespace IceAgeWorld;

/// <summary>The famous places in the world, each built its own way (see <see cref="LandmarkBuilder"/>).</summary>
public enum LandmarkKind
{
    LookoutCrag,
    MammothGraveyard,
    FrozenFalls,
    BalancedStone,
    SteamingSprings,
    IceArch,
    Pillars,
    ElderTree,
}

/// <summary>
/// A famous place: the kind it is, its name, the middle of it, how far round that it reaches, the level direction it
/// faces (e.g. the way Lookout Crag juts out), and a line telling the player what to do there.
/// </summary>
public readonly record struct Landmark(LandmarkKind Kind, string Name, Vector2 Centre, float Radius, Vector2 Facing, string Blurb);

public partial class Terrain
{
    /// <summary>How high the top of Lookout Crag stands above the steppe.</summary>
    private const float CragHeight = 30f;

    /// <summary>How tall and wide the knoll is that the Balanced Stone stands on.</summary>
    private const float KnollHeight = 3.5f;
    private const float KnollRadius = 12f;

    /// <summary>
    /// The famous places, how big each is and how far from the start it may lie. Lookout Crag stands close by, so it
    /// is the first thing a player sees across the meadow; the rest are out on the steppe, to be found.
    /// </summary>
    private static readonly (LandmarkKind Kind, string Name, float Radius, float Near, float Far, string Blurb)[] Sites =
    [
        (LandmarkKind.LookoutCrag, "Lookout Crag", 62f, 150f, 240f, "Climb the ramp up its side and walk out along the ledge to look over the steppe"),
        (LandmarkKind.MammothGraveyard, "Mammoth Graveyard", 38f, 200f, 520f, "Where the old mammoths come to die; walk through the ribs of the oldest of all"),
        (LandmarkKind.BalancedStone, "Balanced Stone", 20f, 160f, 480f, "A boulder the glaciers left perched on a pillar of rock; climb the stepping stones to its top"),
        (LandmarkKind.SteamingSprings, "Steaming Springs", 34f, 200f, 560f, "Hot pools that never freeze, where animals come to drink all winter"),
        (LandmarkKind.IceArch, "Ice Arch", 28f, 260f, 620f, "An arch of old glacier ice, left standing as the ice melted back"),
        (LandmarkKind.Pillars, "The Pillars", 30f, 220f, 600f, "Columns of black basalt, stepping up like a giant's stair"),
        (LandmarkKind.ElderTree, "Elder Tree", 40f, 160f, 520f, "The oldest larch on the steppe, taller than any other tree"),
    ];

    private readonly List<Landmark> _landmarks = [];

    /// <summary>The pools of the Steaming Springs, which are small lakes like any other, only hot.</summary>
    private readonly List<Lake> _springs = [];

    public IReadOnlyList<Landmark> Landmarks => _landmarks;

    /// <summary>True for the hot pools of the Steaming Springs.</summary>
    public bool IsSpring(Lake lake) => _springs.Contains(lake);

    /// <summary>The landmark whose ground a point lies on, or within <paramref name="margin"/> metres of, if any.</summary>
    public Landmark? LandmarkAt(float x, float z, float margin = 0f)
    {
        var point = new Vector2(x, z);
        foreach (var landmark in _landmarks)
            if (point.DistanceTo(landmark.Centre) < landmark.Radius + margin)
                return landmark;
        return null;
    }

    /// <summary>
    /// Picks where every famous place stands, once the cliff and the hills are in place: each somewhere on open steppe
    /// at its own distance from the start, clear of the hills, the cliff, the ocean, the edge of the world and every other
    /// landmark (the lakes, dug afterwards, keep clear of them in turn). The Frozen Falls pour down the middle of the cliff.
    /// </summary>
    private void PlaceLandmarks()
    {
        _landmarks.Clear();
        var rng = new RandomNumberGenerator { Seed = (ulong)Seed + 9 };

        foreach (var (kind, name, radius, near, far, blurb) in Sites)
        {
            for (int attempt = 0; attempt < 600; attempt++)
            {
                var centre = Vector2.Right.Rotated(rng.Randf() * Mathf.Tau) * rng.RandfRange(near, far);
                if (!IsOpenGround(centre, radius))
                    continue;
                // Each faces the start, so the crag's slab juts out towards the meadow, where it is seen from.
                var facing = centre.LengthSquared() > 1f ? -centre.Normalized() : Vector2.Up;
                _landmarks.Add(new Landmark(kind, name, centre, radius, facing, blurb));
                break;
            }
        }

        // The middle of the cliff's face, which is turned towards the meadow.
        var foot = (Cliff.Start + Cliff.End) / 2f;
        _landmarks.Add(new Landmark(LandmarkKind.FrozenFalls, "Frozen Falls", foot - Cliff.Back * 6f, 18f, -Cliff.Back,
            "A waterfall over the great cliff, frozen solid all winter"));
    }

    /// <summary>Whether a landmark this big could stand here, on open steppe away from everything else.</summary>
    private bool IsOpenGround(Vector2 centre, float radius)
    {
        float limit = HalfSize - MountainWidth - radius - 10f;
        if (Mathf.Abs(centre.X) > limit || Mathf.Abs(centre.Y) > limit)
            return false;
        if (centre.Length() < radius + 50f || centre.DistanceTo(BigLakeCentre) < BigLakeRadius + BigLakeShore + radius + 25f)
            return false;
        // Clear of the cliff's face and of the long slope up behind it.
        if (DistanceFromCliff(centre) < radius + CliffDipLength + CliffWander)
            return false;
        foreach (var mountain in _mountains)
            if (centre.DistanceTo(mountain.Centre) < mountain.Radius * 1.1f + radius)
                return false;
        foreach (var other in _landmarks)
            if (centre.DistanceTo(other.Centre) < other.Radius + radius + 40f)
                return false;
        return true;
    }

    /// <summary>How far the landmarks raise the ground at a point: the rock of Lookout Crag, and the Balanced Stone's knoll.</summary>
    private float LandmarkHeight(float x, float z)
    {
        float height = 0f;
        var point = new Vector2(x, z);
        foreach (var landmark in _landmarks)
        {
            if (point.DistanceSquaredTo(landmark.Centre) > landmark.Radius * landmark.Radius * 1.5f)
                continue;
            if (landmark.Kind == LandmarkKind.LookoutCrag)
                height += CragRock(landmark, point);
            else if (landmark.Kind == LandmarkKind.BalancedStone)
            {
                float d = point.DistanceTo(landmark.Centre) / KnollRadius;
                height += d < 1f ? KnollHeight * (1f - d * d) * (1f - d * d) : 0f;
            }
        }
        return height;
    }

    /// <summary>
    /// Lookout Crag's rock: a tower of granite standing alone on the steppe, an oval thirty metres high with sheer sides
    /// all round and a domed top, like the kopjes and inselbergs that rise out of open plains. A ramp of rock climbs up
    /// along its right flank from behind, so any animal can walk to the top. The slab that juts out from the top over the
    /// meadow is built separately (see <see cref="LandmarkBuilder.Crag"/>), since the ground can't overhang.
    /// </summary>
    private float CragRock(Landmark crag, Vector2 point)
    {
        var offset = point - crag.Centre;
        float rough = _cliffEdge.GetNoise2D(point.X * 3f, point.Y * 3f);
        float forward = offset.Dot(crag.Facing) + 1.5f * rough;
        float side = offset.Dot(crag.Facing.Orthogonal()) + 1.5f * rough;

        // The tower: an oval from 30 m behind the face to the face itself, 13 m either side of the middle.
        float along = (forward + 14f) / 16f;
        float across = side / 13f;
        float round = Mathf.Sqrt(along * along + across * across);
        float tower = CragHeight * (1f - Mathf.SmoothStep(0.86f, 1f, round)) * (1f - 0.12f * round * round);

        // The ramp: a broad ledge from the steppe 60 m behind, up the right flank to the top near the front.
        float climb = Mathf.Clamp((forward + 62f) / 54f, 0f, 1f) * CragHeight * 0.97f;
        float onRamp = Mathf.SmoothStep(8f, 11f, side) * (1f - Mathf.SmoothStep(18f, 20f, side)) * (1f - Mathf.SmoothStep(-6f, -3f, forward));
        float height = Mathf.Max(tower, climb * onRamp);

        // Weathered into rough ledges and cracks.
        return height + _ridges.GetNoise2D(point.X * 2f, point.Y * 2f) * 0.8f * height / CragHeight;
    }

    /// <summary>
    /// Digs the pools of the Steaming Springs, after the ordinary lakes: three small, shallow pools at different levels,
    /// each a lake of its own.
    /// </summary>
    private void DigSprings()
    {
        _springs.Clear();
        foreach (var landmark in _landmarks)
        {
            if (landmark.Kind != LandmarkKind.SteamingSprings)
                continue;
            var across = landmark.Facing.Orthogonal();
            foreach (var (along, aside, radius) in new[] { (0f, 0f, 7f), (3f, 19f, 5f), (17f, -10f, 5.5f) })
            {
                var centre = landmark.Centre + landmark.Facing * along + across * aside;
                var pool = new Lake(centre, radius, WaterLine(centre, radius, 3f), 3f);
                _lakes.Add(pool);
                _springs.Add(pool);
                Carve(pool, 2.5f);
            }
        }
    }

    /// <summary>The pale crust and rust-orange stains that hot, mineral-laden water leaves round the springs.</summary>
    private static readonly Color Crust = new(0.92f, 0.9f, 0.82f);
    private static readonly Color Rust = new(0.78f, 0.45f, 0.18f);

    /// <summary>Black basalt gravel, broken from the Pillars, and the clean snow heaped round the Ice Arch.</summary>
    private static readonly Color Gravel = new(0.2f, 0.2f, 0.22f);

    /// <summary>The bare black rock the Mammoth Graveyard lies on, where nothing grows.</summary>
    private static readonly Color BlackRock = new(0.07f, 0.07f, 0.08f);

    /// <summary>
    /// The ground's colour on and round the landmarks: mineral rims round the springs, gravel under the Pillars, black
    /// rock under the bones, snow round the ice.
    /// </summary>
    private Color LandmarkGround(Color ground, float x, float z)
    {
        foreach (var spring in _springs)
        {
            float d = new Vector2(x, z).DistanceTo(spring.Centre) - spring.Radius;
            if (d is < -1f or > 6f)
                continue;
            // A white crust at the water's edge, then rings of orange and rust fading out into the steppe.
            var stain = Crust.Lerp(Rust, Mathf.SmoothStep(0.5f, 2.5f, d));
            return ground.Lerp(stain, 1f - Mathf.SmoothStep(3f, 6f, d));
        }
        if (LandmarkAt(x, z) is not { } landmark)
            return ground;
        float within = 1f - Mathf.SmoothStep(landmark.Radius * 0.6f, landmark.Radius, new Vector2(x, z).DistanceTo(landmark.Centre));
        return landmark.Kind switch
        {
            LandmarkKind.Pillars => ground.Lerp(Gravel, within * 0.85f),
            LandmarkKind.IceArch => ground.Lerp(Snow, within),
            // Black right out to where the outermost bones and rocks lie, fading into the steppe only at the very edge.
            LandmarkKind.MammothGraveyard => ground.Lerp(BlackRock,
                1f - Mathf.SmoothStep(landmark.Radius * 0.9f, landmark.Radius, new Vector2(x, z).DistanceTo(landmark.Centre))),
            _ => ground,
        };
    }

    /// <summary>No grass grows on the gravel, the graveyard's black rock, the snow round the ice, or the frozen pool under the falls.</summary>
    private bool IsBareLandmark(float x, float z) =>
        LandmarkAt(x, z) is { Kind: LandmarkKind.Pillars or LandmarkKind.IceArch or LandmarkKind.MammothGraveyard } or { Kind: LandmarkKind.FrozenFalls };

    /// <summary>Builds every landmark's rock, bones, ice and steam over the finished ground, and plants the Elder Tree.</summary>
    private void BuildLandmarks()
    {
        var root = new Node3D { Name = "Landmarks" };
        AddChild(root);
        var rng = new RandomNumberGenerator { Seed = (ulong)Seed + 10 };
        float Ground(float x, float z) => GetHeight(x, z);

        foreach (var landmark in _landmarks)
        {
            var place = new Node3D { Name = landmark.Name.Replace(" ", "") };
            root.AddChild(place);
            var c = landmark.Centre;
            switch (landmark.Kind)
            {
                case LandmarkKind.LookoutCrag:
                    // The slab grows out of the level top, just behind the face.
                    var top = c - landmark.Facing * 5f;
                    LandmarkBuilder.Crag(place, new Vector3(c.X, GetHeight(top.X, top.Y), c.Y), landmark.Facing, Ground);
                    break;
                case LandmarkKind.MammothGraveyard:
                    LandmarkBuilder.Graveyard(place, c, landmark.Facing, Ground, rng);
                    break;
                case LandmarkKind.FrozenFalls:
                    BuildFalls(place);
                    break;
                case LandmarkKind.BalancedStone:
                    LandmarkBuilder.BalancedStone(place, new Vector3(c.X, GetHeight(c.X, c.Y), c.Y), landmark.Facing, Ground);
                    break;
                case LandmarkKind.SteamingSprings:
                    foreach (var spring in _springs)
                        if (spring.Centre.DistanceTo(c) < landmark.Radius)
                            LandmarkBuilder.Steam(place, new Vector3(spring.Centre.X, spring.Surface, spring.Centre.Y), spring.Radius);
                    break;
                case LandmarkKind.IceArch:
                    LandmarkBuilder.IceArch(place, c, landmark.Facing.Orthogonal(), Ground, rng);
                    break;
                case LandmarkKind.Pillars:
                    LandmarkBuilder.Pillars(place, c, landmark.Facing, landmark.Radius * 0.8f, Ground, rng);
                    break;
                case LandmarkKind.ElderTree:
                    PlantElderTree(place, landmark, rng);
                    break;
            }
        }
    }

    /// <summary>
    /// Finds the foot of the cliff's face in the middle, where the land starts to leap up from the meadow, and hangs the
    /// frozen waterfall down it from the top.
    /// </summary>
    private void BuildFalls(Node3D place)
    {
        var back = Cliff.Back;
        var line = (Cliff.Start + Cliff.End) / 2f;
        // Walk in from the meadow until the ground starts to rise: that is the foot of the face, wherever weathering has
        // put it.
        var toe = line - back * 30f;
        float meadow = GetHeight(toe.X, toe.Y);
        for (float s = -30f; s < 30f && GetHeight(toe.X, toe.Y) < meadow + 0.8f; s += 0.5f)
            toe = line + back * s;
        var top = toe + back * 8f;
        LandmarkBuilder.Falls(place, new Vector3(toe.X, GetHeight(toe.X, toe.Y), toe.Y), GetHeight(top.X, top.Y),
            new Vector3(-back.X, 0f, -back.Y), (x, z) => GetHeight(x, z));
    }

    /// <summary>How much bigger the Elder Tree is than the forest's trees: four times as tall and as wide as an average one.</summary>
    private float ElderTreeScale => 4f * TreeScale;

    /// <summary>
    /// The Elder Tree: one golden larch grown to a giant in a clearing of its own, seen from far across the steppe, with
    /// a trunk thick enough for a snow leopard to climb all the way to the top, like any other tree.
    /// </summary>
    private void PlantElderTree(Node3D place, Landmark landmark, RandomNumberGenerator rng)
    {
        var material = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            VertexColorIsSrgb = true,
            Roughness = 1f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        var shape = TreeBuilder.Build(TreeKind.Larch, rng, material);
        var c = landmark.Centre;
        float ground = GetHeight(c.X, c.Y);
        var basis = new Basis(Vector3.Up, rng.Randf() * Mathf.Tau) * Basis.FromScale(Vector3.One * ElderTreeScale);
        var transform = new Transform3D(basis, new Vector3(c.X, ground - 0.5f, c.Y));
        place.AddChild(new MeshInstance3D { Name = "Larch", Mesh = shape.Mesh, Transform = transform });

        var tree = new Tree(transform, shape);
        _trees.Add(tree);
        _thickestTrunk = Mathf.Max(_thickestTrunk, tree.CollisionRadius);
        var body = new StaticBody3D { Name = "Trunk" };
        body.AddChild(new CollisionShape3D
        {
            Shape = new CylinderShape3D { Radius = tree.CollisionRadius, Height = 8f * ElderTreeScale },
            Position = new Vector3(c.X, ground + 4f * ElderTreeScale, c.Y),
        });
        place.AddChild(body);
    }
}
