using Godot;

namespace IceAgeWorld;

/// <summary>
/// Builds randomised low-poly conifer meshes. Every call produces a different tree: a tapering trunk that
/// wanders slightly as it rises, a random number of ragged, drooping foliage tiers in its own shade of
/// green, each held up by a whorl of bark branches, a few bare dead branches on the lower trunk, and snow
/// lying on the upper branches. Bark, needle and snow colours are baked into vertex colours
/// so a single material draws the whole tree.
/// </summary>
public static class PineTree
{
    private const int Sides = 10;
    private const int TrunkRings = 6;
    private const int BranchSides = 5;
    private const int BranchSegments = 3;

    private static readonly Color Bark = new(0.33f, 0.24f, 0.17f);
    private static readonly Color DeadWood = new(0.4f, 0.36f, 0.32f);
    private static readonly Color Snow = new(0.93f, 0.95f, 1f);

    public static ArrayMesh Build(RandomNumberGenerator rng, Material material)
    {
        var mesh = new MeshBuilder();

        float height = rng.RandfRange(11f, 18f);
        float crownBase = height * rng.RandfRange(0.15f, 0.28f);
        float crownRadius = height * rng.RandfRange(0.17f, 0.25f);
        int tiers = rng.RandiRange(4, 7);

        // Fraction of the way up the crown where snow starts to settle on the branches.
        float snowLine = rng.RandfRange(0.25f, 0.65f);

        // Each tree gets its own needle shade, somewhere between blue-green and yellow-green.
        var needles = new Color(
            rng.RandfRange(0.12f, 0.2f),
            rng.RandfRange(0.26f, 0.36f),
            rng.RandfRange(0.16f, 0.25f));

        float trunkHeight = height * 0.85f;
        float trunkRadius = height * rng.RandfRange(0.025f, 0.038f);
        BuildTrunk(mesh, rng, trunkHeight, trunkRadius);
        float TrunkRadiusAt(float y) => trunkRadius * Mathf.Lerp(1f, 0.15f, Mathf.Clamp(y / trunkHeight, 0f, 1f));

        // Lower branches shaded out by the crown above have died back to bare, grey stubs.
        int deadBranches = rng.RandiRange(2, 6);
        for (int b = 0; b < deadBranches; b++)
        {
            float y = crownBase * rng.RandfRange(0.35f, 0.95f);
            var to = Outward(rng.Randf() * Mathf.Tau) * rng.RandfRange(0.6f, 1.8f)
                     + Vector3.Up * (y + rng.RandfRange(-0.3f, 0.25f));
            BuildBranch(mesh, rng, Vector3.Up * y, to, 0.05f, TrunkRadiusAt(y) * 0.3f, DeadWood);
        }

        for (int t = 0; t < tiers; t++)
        {
            float f = t / (float)(tiers - 1);
            float tierBase = Mathf.Lerp(crownBase, height - 1f, f);
            float radius = crownRadius * Mathf.Lerp(1f, 0.3f, f) * rng.RandfRange(0.85f, 1.15f);
            float tierHeight = radius * rng.RandfRange(1.4f, 2f) + 0.3f;

            // Lower branches sit in shade, so they are darker than the tips.
            var colour = Shade(needles, Mathf.Lerp(0.7f, 1.1f, f));
            float snowCover = Mathf.SmoothStep(snowLine, 1f, f) * rng.RandfRange(0.6f, 1f);

            // A whorl of branches spreads out from the trunk just beneath the foliage, so they show from below.
            int branches = rng.RandiRange(4, 7);
            float turn = rng.Randf() * Mathf.Tau;
            for (int b = 0; b < branches; b++)
            {
                var to = Outward(turn + (b + rng.RandfRange(-0.3f, 0.3f)) * Mathf.Tau / branches) * radius * rng.RandfRange(0.85f, 1.05f)
                         + Vector3.Up * (tierBase - tierHeight * rng.RandfRange(0.08f, 0.16f));
                BuildBranch(mesh, rng, Vector3.Up * tierBase, to, radius * 0.08f,
                    Mathf.Max(TrunkRadiusAt(tierBase) * 0.45f, 0.05f), Bark);
            }

            BuildTier(mesh, rng, tierBase, radius, tierHeight, colour, snowCover);
        }

        return mesh.Commit(material);
    }

    private static void BuildTrunk(MeshBuilder mesh, RandomNumberGenerator rng, float height, float baseRadius)
    {
        float slope = baseRadius * 0.85f / height;
        var centre = Vector3.Zero;
        var drift = new Vector3(rng.RandfRange(-1f, 1f), 0f, rng.RandfRange(-1f, 1f)).Normalized();
        int[]? previous = null;

        for (int r = 0; r <= TrunkRings; r++)
        {
            float f = r / (float)TrunkRings;
            float radius = baseRadius * Mathf.Lerp(1f, 0.15f, f);
            if (r == 0)
                radius *= 1.5f; // root flare
            var shade = Shade(Bark, Mathf.Lerp(0.8f, 1.1f, f));

            var ring = new int[Sides];
            for (int i = 0; i < Sides; i++)
            {
                float a = i * Mathf.Tau / Sides;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var position = centre + dir * radius * rng.RandfRange(0.9f, 1.1f) + Vector3.Up * (f * height);
                ring[i] = mesh.Add(position, (dir + Vector3.Up * slope).Normalized(), Jitter(shade, rng));
            }

            if (previous != null)
                for (int i = 0; i < Sides; i++)
                {
                    int j = (i + 1) % Sides;
                    mesh.Quad(previous[i], ring[i], ring[j], previous[j]);
                }
            previous = ring;

            // The trunk wanders a little between rings so no tree is perfectly straight.
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
    /// with a dark underside and an optional cap of snow on its upper slope.
    /// </summary>
    private static void BuildTier(MeshBuilder mesh, RandomNumberGenerator rng, float baseY, float radius,
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
            return;

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
    }

    private static Vector3 Outward(float angle) => new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

    private static Color Shade(Color colour, float amount) =>
        new(colour.R * amount, colour.G * amount, colour.B * amount);

    /// <summary>Slight per-vertex brightness variation that breaks up flat colour.</summary>
    private static Color Jitter(Color colour, RandomNumberGenerator rng) =>
        Shade(colour, rng.RandfRange(0.92f, 1.08f));
}
