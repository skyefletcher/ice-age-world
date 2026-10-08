using System;
using System.Collections.Generic;
using Godot;

namespace IceAgeWorld;

/// <summary>The kinds of tree that grow in the ice-age forest, all hardy species of the cold northern taiga.</summary>
public enum TreeKind
{
    /// <summary>Broad, dark conifer with drooping, ragged tiers of branches.</summary>
    Spruce,

    /// <summary>Scots pine: a tall, bare trunk turning orange near the top, under a flat, clumpy crown.</summary>
    Pine,

    /// <summary>The only conifer that drops its needles, caught here in autumn gold before they fall.</summary>
    Larch,

    /// <summary>White-barked and bare for winter, with a few last yellow leaves clinging on.</summary>
    Birch,

    /// <summary>Subalpine fir: a narrow, blue-grey spire whose stiff branches hold heavy snow.</summary>
    Fir,
}

/// <summary>
/// Builds randomised low-poly tree meshes of every <see cref="TreeKind"/>. Every call produces a different tree:
/// a tapering trunk that wanders slightly as it rises, branches and foliage in its own shade, and snow lying on the
/// upper surfaces. Bark, foliage and snow colours are baked into vertex colours so a single material draws every
/// tree. Each trunk runs right up into the crown so a snow leopard can climb all the way to the top.
/// </summary>
public static class TreeBuilder
{
    /// <summary>
    /// A built tree: its mesh, the base radius and height of its trunk, the height of its very top, where a climber
    /// comes out, and the branches strong enough to walk along. All in the tree's own space, before it is scaled
    /// into place.
    /// </summary>
    public sealed record Shape(ArrayMesh Mesh, float TrunkRadius, float TrunkHeight, float TopHeight,
        IReadOnlyList<Branch> Branches);

    /// <summary>
    /// A branch growing straight out of the trunk, as built by <see cref="BuildBranch"/>: from its root on the trunk's
    /// axis to its tip, sagging in the middle, with the given radius at the root. <see cref="Reach"/> is how far out,
    /// as a fraction of its length, a climber can walk before the wood gets too thin or the foliage too thick.
    /// </summary>
    public readonly record struct Branch(Vector3 From, Vector3 To, float Sag, float Radius, float Reach)
    {
        /// <summary>The top of the wood at fraction <paramref name="f"/> of the way from root to tip, where paws go.</summary>
        public Vector3 TopAt(float f) =>
            From.Lerp(To, f) + Vector3.Down * (Sag * 4f * f * (1f - f)) + Vector3.Up * (Radius * Mathf.Lerp(1f, 0.3f, f));
    }

    /// <summary>
    /// Branches thinner than this at the root bend under a snow leopard's weight, so it won't walk out on them.
    /// Fine birch twigs never qualify; the main limbs of every kind of tree do.
    /// </summary>
    private const float ClimbableRadius = 0.06f;

    /// <summary>How far out along a bare branch a climber goes: the last stretch is too thin and whippy to hold it.</summary>
    private const float DefaultReach = 0.85f;

    private const int Sides = 10;
    private const int BranchSides = 5;
    private const int BranchSegments = 3;

    /// <summary>How much thinner the top of every trunk is than its base; <see cref="Tree.RadiusAt"/> relies on it.</summary>
    public const float TrunkTaper = 0.15f;

    private static readonly Color Bark = new(0.33f, 0.24f, 0.17f);
    private static readonly Color DeadWood = new(0.4f, 0.36f, 0.32f);
    private static readonly Color Snow = new(0.93f, 0.95f, 1f);

    public static Shape Build(TreeKind kind, RandomNumberGenerator rng, Material material) => kind switch
    {
        TreeKind.Spruce => BuildSpruce(rng, material),
        TreeKind.Pine => BuildPine(rng, material),
        TreeKind.Larch => BuildLarch(rng, material),
        TreeKind.Birch => BuildBirch(rng, material),
        TreeKind.Fir => BuildFir(rng, material),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static Shape BuildSpruce(RandomNumberGenerator rng, Material material)
    {
        // Each spruce gets its own needle shade, somewhere between blue-green and yellow-green.
        var needles = new Color(
            rng.RandfRange(0.12f, 0.2f),
            rng.RandfRange(0.26f, 0.36f),
            rng.RandfRange(0.16f, 0.25f));

        return BuildConifer(rng, material, needles,
            height: rng.RandfRange(11f, 18f),
            crownBase: rng.RandfRange(0.15f, 0.28f),
            crownRadius: rng.RandfRange(0.17f, 0.25f),
            tiers: rng.RandiRange(4, 7),
            topTaper: 0.3f,
            tierHeight: (1.4f, 2f),
            snowLine: rng.RandfRange(0.25f, 0.65f));
    }

    private static Shape BuildFir(RandomNumberGenerator rng, Material material)
    {
        // Subalpine fir needles have a waxy bloom that makes them look blue-grey from a distance.
        var needles = new Color(
            rng.RandfRange(0.11f, 0.15f),
            rng.RandfRange(0.23f, 0.29f),
            rng.RandfRange(0.25f, 0.31f));

        // Firs grow as narrow spires that shed snow, keeping their branches almost to the ground. Their tiers are
        // short and close together, and stay wide near the top, so the whole tree is nearly a column. Its stiff
        // branches still carry snow from top to bottom.
        return BuildConifer(rng, material, needles,
            height: rng.RandfRange(9f, 15f),
            crownBase: rng.RandfRange(0.06f, 0.12f),
            crownRadius: rng.RandfRange(0.09f, 0.12f),
            tiers: rng.RandiRange(8, 11),
            topTaper: 0.45f,
            tierHeight: (1.8f, 2.6f),
            snowLine: rng.RandfRange(0f, 0.2f));
    }

    /// <summary>
    /// A spruce-like conifer: stacked, drooping cones of foliage, each held up by a whorl of bark branches, with a
    /// few bare dead branches on the lower trunk. Sizes are fractions of <paramref name="height"/>.
    /// </summary>
    private static Shape BuildConifer(RandomNumberGenerator rng, Material material, Color needles, float height,
        float crownBase, float crownRadius, int tiers, float topTaper, (float Min, float Max) tierHeight, float snowLine)
    {
        var mesh = new MeshBuilder();
        var branches = new List<Branch>();
        crownBase *= height;
        crownRadius *= height;

        float trunkHeight = height * 0.85f;
        float trunkRadius = height * rng.RandfRange(0.025f, 0.038f);
        BuildTrunk(mesh, rng, trunkHeight, trunkRadius, 6, _ => Bark);

        DeadBranches(mesh, branches, rng, crownBase, trunkHeight, trunkRadius, rng.RandiRange(2, 6));

        float top = trunkHeight;
        for (int t = 0; t < tiers; t++)
        {
            float f = t / (float)(tiers - 1);
            float tierBase = Mathf.Lerp(crownBase, height - 1f, f);
            float radius = crownRadius * Mathf.Lerp(1f, topTaper, f) * rng.RandfRange(0.85f, 1.15f);
            float tall = radius * rng.RandfRange(tierHeight.Min, tierHeight.Max) + 0.3f;

            // Lower branches sit in shade, so they are darker than the tips.
            var colour = Shade(needles, Mathf.Lerp(0.7f, 1.1f, f));
            float snowCover = Mathf.SmoothStep(snowLine, 1f, f) * rng.RandfRange(0.6f, 1f);

            // A whorl of branches spreads out from the trunk just beneath the foliage, so they show from below. They
            // are buried in needles, so they are no place for a climber to walk: it would vanish from sight.
            int whorl = rng.RandiRange(4, 7);
            float turn = rng.Randf() * Mathf.Tau;
            for (int b = 0; b < whorl; b++)
            {
                var to = Outward(turn + (b + rng.RandfRange(-0.3f, 0.3f)) * Mathf.Tau / whorl) * radius * rng.RandfRange(0.85f, 1.05f)
                         + Vector3.Up * (tierBase - tall * rng.RandfRange(0.08f, 0.16f));
                BuildBranch(mesh, rng, Vector3.Up * tierBase, to, radius * 0.08f,
                    Mathf.Max(TrunkRadiusAt(tierBase, trunkHeight, trunkRadius) * 0.45f, 0.05f), Bark);
            }

            top = Mathf.Max(top, BuildTier(mesh, rng, tierBase, radius, tall, colour, snowCover));
        }

        // A climber stands on the spire's tip, with its paws sunk a little way into the topmost needles.
        return new Shape(mesh.Commit(material), trunkRadius, trunkHeight, top - 0.25f, branches);
    }

    /// <summary>
    /// Scots pine sheds its lower branches as it grows, leaving a long bare trunk, rough and brown at the foot and
    /// flaking orange higher up. Its crown is a few flat, snow-topped clumps of needles held out on upswept limbs.
    /// </summary>
    private static Shape BuildPine(RandomNumberGenerator rng, Material material)
    {
        var mesh = new MeshBuilder();
        var branches = new List<Branch>();
        float height = rng.RandfRange(12f, 18f);
        float trunkHeight = height * 0.88f;
        float trunkRadius = height * rng.RandfRange(0.028f, 0.038f);
        var orange = new Color(0.7f, 0.4f, 0.22f);
        BuildTrunk(mesh, rng, trunkHeight, trunkRadius, 8,
            f => Bark.Lerp(orange, Mathf.SmoothStep(0.35f, 0.7f, f)));

        float crownBase = height * rng.RandfRange(0.58f, 0.7f);
        DeadBranches(mesh, branches, rng, crownBase, trunkHeight, trunkRadius, rng.RandiRange(1, 4));

        var needles = new Color(
            rng.RandfRange(0.13f, 0.18f),
            rng.RandfRange(0.25f, 0.32f),
            rng.RandfRange(0.15f, 0.2f));
        float snow = rng.RandfRange(0.5f, 0.9f);

        int limbs = rng.RandiRange(5, 8);
        float turn = rng.Randf() * Mathf.Tau;
        for (int l = 0; l < limbs; l++)
        {
            float f = l / (float)(limbs - 1);
            float y = Mathf.Lerp(crownBase, trunkHeight * 0.92f, f);
            float length = height * rng.RandfRange(0.15f, 0.24f) * Mathf.Lerp(1f, 0.55f, f);
            var outward = Outward(turn + l * 2.4f + rng.RandfRange(-0.4f, 0.4f));
            var end = outward * length + Vector3.Up * (y + length * rng.RandfRange(0.35f, 0.7f));

            // A climber can walk out along the limb as far as the clump of needles at its end.
            float spread = rng.RandfRange(1.1f, 1.8f);
            float clear = Mathf.Clamp(1f - spread * 0.85f / (end - Vector3.Up * y).Length(), 0.3f, DefaultReach);
            BuildLimb(mesh, branches, rng, Vector3.Up * y, end, 0.1f,
                Mathf.Max(TrunkRadiusAt(y, trunkHeight, trunkRadius) * 0.55f, 0.06f), orange, clear);
            BuildClump(mesh, rng, end + Vector3.Up * 0.2f, new Vector3(spread, spread * 0.38f, spread), needles, snow);

            // Longer limbs carry a second, smaller clump part-way out, on a side shoot beside the limb.
            if (length > height * 0.17f)
            {
                float small = spread * 0.7f;
                var side = outward.Rotated(Vector3.Up, rng.Randf() < 0.5f ? Mathf.Pi / 2f : -Mathf.Pi / 2f);
                BuildClump(mesh, rng, Vector3.Up * y + (end - Vector3.Up * y) * 0.55f + side * small * 1.05f,
                    new Vector3(small, small * 0.4f, small), Shade(needles, 0.9f), snow);
            }
        }

        // The leader ends in a last, flat clump: the treetop a climber stands on.
        var crown = new Vector3(1.3f, 0.5f, 1.3f);
        BuildClump(mesh, rng, Vector3.Up * (trunkHeight + 0.15f), crown, Shade(needles, 1.1f), snow);

        return new Shape(mesh.Commit(material), trunkRadius, trunkHeight, trunkHeight + 0.15f + crown.Y * 0.8f, branches);
    }

    /// <summary>
    /// The larch is a deciduous conifer: before winter its soft needles turn gold and fall. Its crown is an open,
    /// airy cone of light branches, each strung with tufts of golden needles, on a reddish-brown trunk.
    /// </summary>
    private static Shape BuildLarch(RandomNumberGenerator rng, Material material)
    {
        var mesh = new MeshBuilder();
        var branches = new List<Branch>();
        float height = rng.RandfRange(10f, 16f);
        float trunkHeight = height * 0.92f;
        float trunkRadius = height * rng.RandfRange(0.024f, 0.033f);
        var bark = new Color(0.4f, 0.25f, 0.18f);
        BuildTrunk(mesh, rng, trunkHeight, trunkRadius, 7, _ => bark);

        float crownBase = height * rng.RandfRange(0.15f, 0.25f);
        float crownRadius = height * rng.RandfRange(0.16f, 0.22f);
        DeadBranches(mesh, branches, rng, crownBase, trunkHeight, trunkRadius, rng.RandiRange(1, 3));

        // Some larches have only just turned and are yellow; others are deep orange and about to drop.
        var gold = new Color(
            rng.RandfRange(0.72f, 0.85f),
            rng.RandfRange(0.45f, 0.62f),
            rng.RandfRange(0.12f, 0.2f));
        float snow = rng.RandfRange(0f, 0.3f);

        int limbs = rng.RandiRange(11, 15);
        float turn = rng.Randf() * Mathf.Tau;
        for (int l = 0; l < limbs; l++)
        {
            float f = l / (float)(limbs - 1);
            float y = Mathf.Lerp(crownBase, trunkHeight * 0.95f, f);
            float length = crownRadius * Mathf.Lerp(1f, 0.25f, f) * rng.RandfRange(0.85f, 1.1f);

            // Larch branches dip from the trunk and sweep up again at the tips.
            var end = Outward(turn + l * 2.4f) * length + Vector3.Up * (y + length * rng.RandfRange(0.05f, 0.25f));
            BuildLimb(mesh, branches, rng, Vector3.Up * y, end, length * 0.12f,
                Mathf.Max(TrunkRadiusAt(y, trunkHeight, trunkRadius) * 0.4f, 0.04f), bark);

            var colour = Shade(gold, Mathf.Lerp(0.75f, 1.1f, f));
            int tufts = length > 1.2f ? 3 : 2;
            for (int t = 0; t < tufts; t++)
            {
                float along = Mathf.Lerp(0.4f, 1f, t / (float)(tufts - 1));
                float size = Mathf.Lerp(0.5f, 0.85f, 1f - f) * rng.RandfRange(0.8f, 1.2f);
                // The tufts hang beneath the branch on drooping shoots, leaving its top clear to walk along.
                var at = Vector3.Up * y + (end - Vector3.Up * y) * along + Vector3.Down * size * 0.8f;
                BuildClump(mesh, rng, at, new Vector3(size, size * 0.7f, size), Jitter(colour, rng), snow);
            }
        }

        var tip = new Vector3(0.5f, 0.6f, 0.5f);
        BuildClump(mesh, rng, Vector3.Up * trunkHeight, tip, Shade(gold, 1.1f), snow);

        return new Shape(mesh.Commit(material), trunkRadius, trunkHeight, trunkHeight + tip.Y * 0.6f, branches);
    }

    /// <summary>
    /// Birch bark is chalk white, scored with black marks and dark and rugged at the foot. In winter the tree stands
    /// bare, its upswept limbs splitting into fine purple-brown twigs, with a few last yellow leaves hanging on.
    /// </summary>
    private static Shape BuildBirch(RandomNumberGenerator rng, Material material)
    {
        var mesh = new MeshBuilder();
        var branches = new List<Branch>();
        float height = rng.RandfRange(9f, 14f);
        float trunkHeight = height * 0.9f;
        float trunkRadius = height * rng.RandfRange(0.026f, 0.034f);
        var white = new Color(0.86f, 0.85f, 0.8f);
        var black = new Color(0.12f, 0.11f, 0.1f);
        BuildTrunk(mesh, rng, trunkHeight, trunkRadius, 12,
            f => f < 0.1f ? black.Lerp(white, f / 0.1f * 0.6f) : rng.Randf() < 0.18f ? black : white);

        var twigs = new Color(0.3f, 0.2f, 0.2f);
        var leaves = new Color(0.85f, 0.68f, 0.2f);

        int limbs = rng.RandiRange(8, 11);
        float turn = rng.Randf() * Mathf.Tau;
        for (int l = 0; l < limbs; l++)
        {
            float f = l / (float)(limbs - 1);
            float y = Mathf.Lerp(height * 0.35f, trunkHeight * 0.9f, f);
            float length = height * rng.RandfRange(0.18f, 0.27f) * Mathf.Lerp(1f, 0.5f, f);
            var outward = Outward(turn + l * 2.4f + rng.RandfRange(-0.3f, 0.3f));
            var end = outward * length + Vector3.Up * (y + length * rng.RandfRange(0.6f, 1f));
            BuildLimb(mesh, branches, rng, Vector3.Up * y, end, 0.05f,
                Mathf.Max(TrunkRadiusAt(y, trunkHeight, trunkRadius) * 0.5f, 0.05f), white);

            // Each limb forks into finer twigs that fan out from its outer half.
            int forks = rng.RandiRange(3, 4);
            for (int k = 0; k < forks; k++)
            {
                var from = Vector3.Up * y + (end - Vector3.Up * y) * rng.RandfRange(0.5f, 0.95f);
                var fan = outward.Rotated(Vector3.Up, rng.RandfRange(-1f, 1f));
                float reach = length * rng.RandfRange(0.35f, 0.55f);
                var to = from + fan * reach + Vector3.Up * reach * rng.RandfRange(0.2f, 0.7f);
                BuildBranch(mesh, rng, from, to, 0.04f, 0.05f, twigs);

                if (rng.Randf() < 0.55f)
                {
                    float size = rng.RandfRange(0.45f, 0.8f);
                    BuildClump(mesh, rng, to, new Vector3(size, size * 0.8f, size), Jitter(leaves, rng), 0f);
                }
            }
        }

        // The climber comes out on the slender leader, just below its tip.
        return new Shape(mesh.Commit(material), trunkRadius, trunkHeight, trunkHeight * 0.97f, branches);
    }

    /// <summary>Lower branches shaded out by the crown above have died back to bare, grey stubs.</summary>
    private static void DeadBranches(MeshBuilder mesh, List<Branch> branches, RandomNumberGenerator rng, float crownBase, float trunkHeight,
        float trunkRadius, int count)
    {
        for (int b = 0; b < count; b++)
        {
            float y = crownBase * rng.RandfRange(0.35f, 0.95f);
            var to = Outward(rng.Randf() * Mathf.Tau) * rng.RandfRange(0.6f, 1.8f)
                     + Vector3.Up * (y + rng.RandfRange(-0.3f, 0.25f));
            BuildLimb(mesh, branches, rng, Vector3.Up * y, to, 0.05f, TrunkRadiusAt(y, trunkHeight, trunkRadius) * 0.3f, DeadWood);
        }
    }

    /// <summary>A branch growing out of the trunk, remembered as somewhere to climb if it is sturdy enough.</summary>
    private static void BuildLimb(MeshBuilder mesh, List<Branch> branches, RandomNumberGenerator rng, Vector3 from,
        Vector3 to, float sag, float radius, Color wood, float reach = DefaultReach)
    {
        BuildBranch(mesh, rng, from, to, sag, radius, wood);
        if (radius >= ClimbableRadius && (to - from).Length() > 0.8f)
            branches.Add(new Branch(from, to, sag, radius, reach));
    }

    private static float TrunkRadiusAt(float y, float trunkHeight, float trunkRadius) =>
        trunkRadius * Mathf.Lerp(1f, TrunkTaper, Mathf.Clamp(y / trunkHeight, 0f, 1f));

    /// <summary>
    /// A tapering trunk of <paramref name="rings"/> rings, coloured by <paramref name="bark"/> from the foot (0) to
    /// the top (1). It wanders a little between rings so no tree is perfectly straight.
    /// </summary>
    private static void BuildTrunk(MeshBuilder mesh, RandomNumberGenerator rng, float height, float baseRadius,
        int rings, Func<float, Color> bark)
    {
        float slope = baseRadius * 0.85f / height;
        var centre = Vector3.Zero;
        var drift = new Vector3(rng.RandfRange(-1f, 1f), 0f, rng.RandfRange(-1f, 1f)).Normalized();
        int[]? previous = null;

        for (int r = 0; r <= rings; r++)
        {
            float f = r / (float)rings;
            float radius = baseRadius * Mathf.Lerp(1f, TrunkTaper, f);
            if (r == 0)
                radius *= 1.5f; // root flare
            float shade = Mathf.Lerp(0.8f, 1.1f, f);

            var ring = new int[Sides];
            for (int i = 0; i < Sides; i++)
            {
                float a = i * Mathf.Tau / Sides;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var position = centre + dir * radius * rng.RandfRange(0.9f, 1.1f) + Vector3.Up * (f * height);
                ring[i] = mesh.Add(position, (dir + Vector3.Up * slope).Normalized(), Jitter(Shade(bark(f), shade), rng));
            }

            if (previous != null)
                for (int i = 0; i < Sides; i++)
                {
                    int j = (i + 1) % Sides;
                    mesh.Quad(previous[i], ring[i], ring[j], previous[j]);
                }
            previous = ring;

            centre += drift * rng.RandfRange(0f, 0.04f);
            drift = drift.Rotated(Vector3.Up, rng.RandfRange(-0.6f, 0.6f));
        }
    }

    /// <summary>
    /// A tapering branch from <paramref name="from"/> to <paramref name="to"/> that sags by
    /// <paramref name="sag"/> in the middle under its own weight and ends in a point.
    /// </summary>
    private static void BuildBranch(MeshBuilder mesh, RandomNumberGenerator rng, Vector3 from, Vector3 to,
        float sag, float radius, Color wood)
    {
        var axis = (to - from).Normalized();
        var side = axis.Cross(Vector3.Up).Normalized();
        var up = side.Cross(axis);
        int[]? previous = null;

        for (int s = 0; s < BranchSegments; s++)
        {
            float f = s / (float)BranchSegments;
            var centre = from.Lerp(to, f) + Vector3.Down * (sag * 4f * f * (1f - f));
            float r = radius * Mathf.Lerp(1f, 0.3f, f);
            var shade = Shade(wood, Mathf.Lerp(0.75f, 1f, f));

            var ring = new int[BranchSides];
            for (int i = 0; i < BranchSides; i++)
            {
                float a = i * Mathf.Tau / BranchSides;
                var dir = side * Mathf.Cos(a) + up * Mathf.Sin(a);
                ring[i] = mesh.Add(centre + dir * r, dir, Jitter(shade, rng));
            }

            if (previous != null)
                for (int i = 0; i < BranchSides; i++)
                {
                    int j = (i + 1) % BranchSides;
                    mesh.Quad(previous[i], ring[i], ring[j], previous[j]);
                }
            previous = ring;
        }

        int tip = mesh.Add(to, axis, Jitter(wood, rng));
        for (int i = 0; i < BranchSides; i++)
            mesh.Tri(previous![i], tip, previous[(i + 1) % BranchSides]);
    }

    /// <summary>
    /// One whorl of branches: a cone that bows outward in the middle and droops at a ragged rim,
    /// with a dark underside and an optional cap of snow on its upper slope. Returns the height of its tip.
    /// </summary>
    private static float BuildTier(MeshBuilder mesh, RandomNumberGenerator rng, float baseY, float radius,
        float tierHeight, Color colour, float snowCover)
    {
        float apexY = baseY + tierHeight;
        float midY = baseY + tierHeight * 0.5f;
        var underside = Shade(colour, 0.45f);

        int apex = mesh.Add(new Vector3(0f, apexY, 0f), Vector3.Up, Shade(colour, 0.9f));
        int underCentre = mesh.Add(new Vector3(0f, baseY + tierHeight * 0.3f, 0f), Vector3.Down, underside);

        var mid = new int[Sides];
        var rim = new int[Sides];
        var under = new int[Sides];
        var midRadius = new float[Sides];
        var normals = new Vector3[Sides];

        for (int i = 0; i < Sides; i++)
        {
            float a = (i + rng.RandfRange(-0.25f, 0.25f)) * Mathf.Tau / Sides;
            var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            normals[i] = (dir * tierHeight + Vector3.Up * radius).Normalized();

            // The middle ring sits outside a straight cone so the branches bow outward...
            midRadius[i] = radius * rng.RandfRange(0.55f, 0.68f);
            mid[i] = mesh.Add(dir * midRadius[i] + Vector3.Up * midY, normals[i], Jitter(colour, rng));

            // ...and the rim droops below the base with a ragged edge.
            float rimRadius = radius * rng.RandfRange(0.8f, 1.15f);
            float rimY = baseY - tierHeight * rng.RandfRange(0f, 0.18f);
            var rimPosition = dir * rimRadius + Vector3.Up * rimY;
            rim[i] = mesh.Add(rimPosition, normals[i], Jitter(Shade(colour, 1.1f), rng));
            under[i] = mesh.Add(rimPosition, (Vector3.Down - dir * 0.3f).Normalized(), underside);
        }

        for (int i = 0; i < Sides; i++)
        {
            int j = (i + 1) % Sides;
            mesh.Tri(apex, mid[i], mid[j]);
            mesh.Quad(mid[i], rim[i], rim[j], mid[j]);
            mesh.Tri(underCentre, under[j], under[i]);
        }

        if (snowCover <= 0f)
            return apexY;

        // Snow lies on the upper slope of the tier, a little above the needles so it never z-fights.
        const float lift = 0.07f;
        float reach = Mathf.Lerp(0.35f, 1f, snowCover);
        int snowApex = mesh.Add(new Vector3(0f, apexY + lift, 0f), Vector3.Up, Snow);
        var snowRim = new int[Sides];
        for (int i = 0; i < Sides; i++)
        {
            float a = (i + 0.5f) * Mathf.Tau / Sides;
            var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            float f = reach * rng.RandfRange(0.75f, 1.05f);
            var position = dir * (midRadius[i] * f + 0.03f) + Vector3.Up * (Mathf.Lerp(apexY, midY, f) + lift);
            snowRim[i] = mesh.Add(position, normals[i], Snow);
        }
        for (int i = 0; i < Sides; i++)
            mesh.Tri(snowApex, snowRim[i], snowRim[(i + 1) % Sides]);
        return apexY;
    }

    /// <summary>
    /// A lumpy, squashed ball of foliage with the given half-sizes: lit on top and shaded beneath, with patchy snow
    /// on its upper surface where <paramref name="snow"/> is above 0.
    /// </summary>
    private static void BuildClump(MeshBuilder mesh, RandomNumberGenerator rng, Vector3 centre, Vector3 radii,
        Color colour, float snow)
    {
        const int around = 7;
        const int bands = 4;
        var inverse = new Vector3(1f / radii.X, 1f / radii.Y, 1f / radii.Z);

        int top = mesh.Add(centre + Vector3.Up * radii.Y * rng.RandfRange(0.9f, 1.1f), Vector3.Up,
            snow > 0.3f ? Snow : Shade(colour, 1.15f));
        int bottom = mesh.Add(centre + Vector3.Down * radii.Y * 0.8f, Vector3.Down, Shade(colour, 0.45f));

        var rings = new int[bands - 1][];
        for (int b = 1; b < bands; b++)
        {
            float phi = b * Mathf.Pi / bands;
            var ring = rings[b - 1] = new int[around];
            for (int i = 0; i < around; i++)
            {
                float a = (i + rng.RandfRange(-0.2f, 0.2f)) * Mathf.Tau / around;
                var dir = new Vector3(Mathf.Cos(a) * Mathf.Sin(phi), Mathf.Cos(phi), Mathf.Sin(a) * Mathf.Sin(phi));
                var shade = b == 1 && rng.Randf() < snow
                    ? Snow
                    : Jitter(Shade(colour, Mathf.Lerp(1.1f, 0.55f, b / (float)bands)), rng);
                ring[i] = mesh.Add(centre + dir * radii * rng.RandfRange(0.8f, 1.15f), (dir * inverse).Normalized(), shade);
            }
        }

        for (int i = 0; i < around; i++)
        {
            int j = (i + 1) % around;
            mesh.Tri(top, rings[0][i], rings[0][j]);
            for (int b = 0; b < bands - 2; b++)
                mesh.Quad(rings[b][i], rings[b + 1][i], rings[b + 1][j], rings[b][j]);
            mesh.Tri(bottom, rings[bands - 2][j], rings[bands - 2][i]);
        }
    }

    private static Vector3 Outward(float angle) => new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

    private static Color Shade(Color colour, float amount) =>
        new(colour.R * amount, colour.G * amount, colour.B * amount);

    /// <summary>Slight per-vertex brightness variation that breaks up flat colour.</summary>
    private static Color Jitter(Color colour, RandomNumberGenerator rng) =>
        Shade(colour, rng.RandfRange(0.92f, 1.08f));
}
