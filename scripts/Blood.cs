using Godot;

namespace IceAgeWorld;

/// <summary>
/// Blood: drops that run and spurt from a bite and fall to spot the snow, leaving a trail behind a wounded animal, and
/// the ragged pool that spreads where an animal has fallen.
/// </summary>
public static class Blood
{
    /// <summary>Fresh blood is a deep crimson, darker where it lies thick; thin at the edges, it shows brighter.</summary>
    private static readonly Color Fresh = new(0.4f, 0.02f, 0.03f);
    private static readonly Color Dark = new(0.16f, 0.005f, 0.01f);

    /// <summary>Seconds a pool takes to spread out to its full size under a carcass.</summary>
    public const float PoolSeconds = 8f;

    private const float Gravity = 9.8f;

    /// <summary>
    /// Drops that fall from the neck and shoulders, where jaws close on. They are in the body's own space, so they scale
    /// with a youngster, and bigger for a big animal, which has more to lose and is seen from further off.
    /// </summary>
    public static CpuParticles3D Drops(AnimalStats stats)
    {
        float drop = 0.016f * Mathf.Max(1f, stats.BodyRadius / 0.4f);

        // Each drop a little different: some fresh crimson, some near black.
        var shades = new Gradient();
        shades.SetColor(0, Fresh.Lightened(0.08f));
        shades.SetColor(1, Dark);

        var drops = new CpuParticles3D
        {
            Emitting = false,
            Amount = 50,
            LocalCoords = false,
            // From a band round the coat of the neck and shoulders, where the wound is, not from inside the body.
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Ring,
            EmissionRingAxis = Vector3.Up,
            // Mostly it runs off the coat and drips straight down; now and then a drop is flung out sideways, as blood
            // pulses from a fresh wound, and arcs down.
            Direction = new Vector3(0f, -1f, -0.3f),
            Spread = 50f,
            Gravity = new Vector3(0f, -Gravity, 0f),
            InitialVelocityMin = 0.1f,
            InitialVelocityMax = 1.6f,
            // A falling drop draws out along its fall, so each is stretched along the way it's going.
            ParticleFlagAlignY = true,
            ScaleAmountMin = 0.4f,
            ScaleAmountMax = 1.6f,
            ColorInitialRamp = shades,
            Randomness = 0.6f,
            Mesh = new SphereMesh
            {
                Radius = drop,
                Height = drop * 5f,
                RadialSegments = 6,
                Rings = 4,
                Material = new StandardMaterial3D
                {
                    VertexColorUseAsAlbedo = true,
                    VertexColorIsSrgb = true,
                    AlbedoColor = Colors.White,
                    Roughness = 0.2f,
                    Metallic = 0.1f,
                },
            },
        };
        Fit(drops, stats);
        return drops;
    }

    /// <summary>
    /// Moves <paramref name="drops"/> to the neck and shoulders of an animal with <paramref name="stats"/>, and lets each
    /// drop live just long enough to fall to the ground, where it lands in the snow rather than sinking through it.
    /// </summary>
    public static void Fit(CpuParticles3D drops, AnimalStats stats)
    {
        float height = stats.BodyHeight * 0.65f;
        drops.Position = new Vector3(0f, height, -stats.BodyRadius * 0.6f);
        drops.EmissionRingRadius = stats.BodyRadius * 0.8f;
        drops.EmissionRingInnerRadius = stats.BodyRadius * 0.65f;
        drops.EmissionRingHeight = stats.BodyHeight * 0.25f;
        drops.Lifetime = Mathf.Sqrt(2f * height / Gravity);
    }


    /// <summary>
    /// A pool of blood, a little wider than the animal it ran from, to lay on the ground. Never a neat circle: a thick
    /// ragged middle with lobes where it ran out further, and splashes scattered round about, different every time.
    /// </summary>
    public static MeshInstance3D Pool(AnimalStats stats, RandomNumberGenerator rng)
    {
        var mesh = new MeshBuilder();
        float size = stats.BodyRadius * 2f;

        // The main pool, and two or three tongues where the blood ran out over the snow before it soaked in.
        Blob(mesh, rng, Vector2.Zero, size, 0.3f, 0f, size * 1.4f);
        int tongues = rng.RandiRange(2, 4);
        for (int i = 0; i < tongues; i++)
        {
            float angle = rng.RandfRange(0f, Mathf.Tau);
            float reach = rng.RandfRange(0.45f, 0.85f) * size;
            Blob(mesh, rng, Vector2.FromAngle(angle) * reach, size * rng.RandfRange(0.3f, 0.5f), 0.25f, 0.002f * (i + 1), size * 1.4f);
        }

        // Spatter flung out around it while the animal was still thrashing.
        int spots = rng.RandiRange(8, 16);
        for (int i = 0; i < spots; i++)
        {
            var at = Vector2.FromAngle(rng.RandfRange(0f, Mathf.Tau)) * size * rng.RandfRange(1.05f, 1.7f);
            Blob(mesh, rng, at, size * rng.RandfRange(0.03f, 0.1f), 0.3f, 0.01f, size * 0.5f);
        }

        return new MeshInstance3D
        {
            Visible = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Mesh = mesh.Commit(Surface()),
        };
    }

    /// <summary>
    /// A small spot where one drop landed in the snow: a ragged blot with a fleck or two splashed off it. One shape for
    /// all of them; each is turned and stretched differently, so no two look alike.
    /// </summary>
    public static ArrayMesh Spot()
    {
        var rng = new RandomNumberGenerator { Seed = 5 };
        var mesh = new MeshBuilder();
        Blob(mesh, rng, Vector2.Zero, 1f, 0.3f, 0f, 1.5f);
        for (int i = 0; i < 3; i++)
            Blob(mesh, rng, Vector2.FromAngle(rng.RandfRange(0f, Mathf.Tau)) * rng.RandfRange(1.3f, 2f), rng.RandfRange(0.15f, 0.3f), 0.3f, 0f, 1.5f);
        return mesh.Commit(Surface());
    }

    /// <summary>Wet, glossy blood, coloured by its vertices: dark where it lies thick, brighter at the thin edges.</summary>
    private static StandardMaterial3D Surface() => new()
    {
        VertexColorUseAsAlbedo = true,
        VertexColorIsSrgb = true,
        AlbedoColor = Colors.White,
        Roughness = 0.12f,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
    };

    /// <summary>
    /// Adds a flat, irregular blot of <paramref name="radius"/> lying in the XZ plane at <paramref name="centre"/>. Its
    /// edge bulges and dips by up to <paramref name="ragged"/> of the radius, from a few gentle random waves laid over
    /// each other, the way a spreading pool rounds out into lobes rather than points. It is darkest at the middle of the
    /// whole pool, where the blood lies thickest, and brightens out to <paramref name="extent"/> from it.
    /// </summary>
    private static void Blob(MeshBuilder mesh, RandomNumberGenerator rng, Vector2 centre, float radius, float ragged, float lift,
        float extent)
    {
        const int sides = 48;
        var waves = new (int Count, float Size, float Phase)[3];
        for (int i = 0; i < waves.Length; i++)
            waves[i] = (rng.RandiRange(2 + i * 2, 3 + i * 3), ragged * rng.RandfRange(0.5f, 1f) / (1 + i * i), rng.RandfRange(0f, Mathf.Tau));

        Color Shade(Vector2 at) => Dark.Lerp(Fresh, Mathf.Clamp(at.Length() / extent, 0f, 1f));
        int middle = mesh.Add(new Vector3(centre.X, lift, centre.Y), Vector3.Up, Shade(centre));
        int first = -1, last = -1;
        for (int s = 0; s < sides; s++)
        {
            float angle = s * Mathf.Tau / sides;
            float r = 1f;
            foreach (var (count, size, phase) in waves)
                r += size * Mathf.Sin(angle * count + phase);
            var edge = centre + Vector2.FromAngle(angle) * radius * Mathf.Max(0.4f, r);
            int at = mesh.Add(new Vector3(edge.X, lift, edge.Y), Vector3.Up, Shade(edge));
            if (last >= 0)
                mesh.Tri(middle, last, at);
            else
                first = at;
            last = at;
        }
        mesh.Tri(middle, last, first);
    }

    /// <summary>
    /// How to lay something flat on the ground at <paramref name="at"/>: tilted to the slope, so it lies on a hillside
    /// rather than cutting into it, and turned by <paramref name="turn"/>.
    /// </summary>
    public static Transform3D OnGround(Terrain terrain, Vector3 at, float turn = 0f, float lift = 0.03f)
    {
        float d = 0.5f;
        float dx = terrain.GroundBelow(at + new Vector3(d, 0f, 0f)) - terrain.GroundBelow(at - new Vector3(d, 0f, 0f));
        float dz = terrain.GroundBelow(at + new Vector3(0f, 0f, d)) - terrain.GroundBelow(at - new Vector3(0f, 0f, d));
        var up = new Vector3(-dx, 2f * d, -dz).Normalized();
        var across = up.Cross(new Vector3(Mathf.Sin(turn), 0f, Mathf.Cos(turn))).Normalized();
        var basis = new Basis(across, up, across.Cross(up));
        return new Transform3D(basis, at with { Y = terrain.GroundBelow(at) + lift });
    }

    /// <summary>Lays <paramref name="pool"/> on the ground at <paramref name="at"/>, just begun to spread.</summary>
    public static void Spill(MeshInstance3D pool, Terrain terrain, Vector3 at)
    {
        pool.GlobalTransform = OnGround(terrain, at, (float)GD.RandRange(0.0, Mathf.Tau));
        pool.Scale = Vector3.One * 0.05f;
        pool.Visible = true;
    }

    /// <summary>Spreads a pool out as the blood keeps running, quickly at first, for <paramref name="seconds"/> since it began.</summary>
    public static void Spread(MeshInstance3D pool, float seconds)
    {
        float grown = Mathf.Max(0.05f, Mathf.Sqrt(Mathf.Clamp(seconds / PoolSeconds, 0f, 1f)));
        pool.Scale = new Vector3(grown, 1f, grown);
    }
}
