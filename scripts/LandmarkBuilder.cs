using System;
using System.Collections.Generic;
using Godot;

namespace IceAgeWorld;

/// <summary>
/// Builds the landmarks' meshes in code, as everything else is: Lookout Crag's slab, the mammoth bones, the frozen
/// waterfall, the Balanced Stone, the springs' steam, the Ice Arch and the basalt Pillars. Anything an animal could
/// stand on or bump into is solid.
/// </summary>
public static class LandmarkBuilder
{
    private static readonly Color SnowWhite = new(0.93f, 0.95f, 1f);
    private static readonly Color Bone = new(0.86f, 0.82f, 0.71f);
    private static readonly Color OldBone = new(0.74f, 0.68f, 0.55f);
    private static readonly Color PaleIce = new(0.74f, 0.88f, 0.96f);
    private static readonly Color DeepIce = new(0.32f, 0.58f, 0.8f);
    private static readonly Color Basalt = new(0.2f, 0.2f, 0.23f);

    /// <summary>Lumps and grain for rock and ice, so no two boulders are the same shape.</summary>
    private static readonly FastNoiseLite Lumps = new() { Seed = 31, NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth, Frequency = 1.2f };

    private static StandardMaterial3D Painted(float roughness = 0.9f, float metallic = 0f) => new()
    {
        VertexColorUseAsAlbedo = true,
        VertexColorIsSrgb = true,
        Roughness = roughness,
        Metallic = metallic,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
    };

    private static StandardMaterial3D Plain(Color colour, float roughness, float metallic = 0f) =>
        new() { AlbedoColor = colour, Roughness = roughness, Metallic = metallic };

    /// <summary>Adds a mesh that animals collide with, as its own shape exactly.</summary>
    private static void Solid(Node3D parent, string name, Mesh mesh, Transform3D at)
    {
        parent.AddChild(new MeshInstance3D { Name = name, Mesh = mesh, Transform = at });
        var body = new StaticBody3D { Name = name + "Body", Transform = at };
        body.AddChild(new CollisionShape3D { Shape = new ConcavePolygonShape3D { Data = mesh.GetFaces(), BackfaceCollision = true } });
        parent.AddChild(body);
    }

    /// <summary>Pushes a coordinate on the unit sphere out towards the unit cube, flattening the shape's faces.</summary>
    private static float Boxy(float v, float power) => Mathf.Sign(v) * Mathf.Pow(Mathf.Abs(v), power);

    /// <summary>A lumpy ellipsoid: a boulder, a block of ice, a half-buried skull.</summary>
    private static ArrayMesh Lumpy(Vector3 radii, float roughness, int seed, Material material, Func<Vector3, Vector3, Color>? colouring = null)
    {
        var shift = new Vector3(seed * 7.31f, seed * 3.17f, 0f);
        return Animal.Ellipsoid(u => u * radii * (1f + roughness * Lumps.GetNoise3Dv(u * 1.5f + shift)), 24, 14, material, colouring);
    }

    /// <summary>
    /// Grey granite, darker in its cracks, crusted with orange lichen on its sides and snow on its tops, where it lies flat
    /// enough to hold it.
    /// </summary>
    private static Color Granite(Vector3 p, Vector3 n)
    {
        float grain = Lumps.GetNoise3D(p.X * 1.5f, p.Y * 1.5f, p.Z * 1.5f);
        var rock = new Color(0.52f, 0.5f, 0.48f).Darkened(Mathf.Clamp(-grain, 0f, 1f) * 0.35f).Lightened(Mathf.Clamp(grain, 0f, 1f) * 0.1f);
        float lichen = Mathf.SmoothStep(0.3f, 0.5f, Lumps.GetNoise3D(p.X * 0.6f + 50f, p.Y * 0.6f, p.Z * 0.6f));
        rock = rock.Lerp(new Color(0.75f, 0.5f, 0.2f), lichen * 0.45f * (1f - Mathf.Abs(n.Y)));
        return rock.Lerp(SnowWhite, Mathf.SmoothStep(0.6f, 0.85f, n.Y));
    }

    /// <summary>
    /// Glacier ice: deep blue low down and in its depths, paler higher up, banded with the layers of old snow it was
    /// pressed from, and white where fresh snow lies on top.
    /// </summary>
    private static Color Ice(Vector3 p, Vector3 n, float height)
    {
        float band = 0.5f + 0.5f * Mathf.Sin(p.Y * 2.2f + Lumps.GetNoise3D(p.X * 0.4f, 0f, p.Z * 0.4f) * 3f);
        var ice = DeepIce.Lerp(PaleIce, Mathf.Clamp(height, 0f, 1f) * 0.6f + band * 0.3f);
        return ice.Lerp(SnowWhite, Mathf.SmoothStep(0.55f, 0.8f, n.Y));
    }

    /// <summary>
    /// Lookout Crag's ledge: a long, thin blade of granite jutting far out from the top of the tower over the meadow,
    /// narrowing to a point and kicking up at its tip, where a layer of rock harder than the rest under it has been left
    /// standing as the softer rock weathered away. An animal can walk out along it to its tip, high over the steppe.
    /// Great boulders broken off the tower lie heaped at its foot.
    /// </summary>
    public static void Crag(Node3D parent, Vector3 top, Vector2 facing, Func<float, float, float> ground)
    {
        var ledge = Animal.Ellipsoid(u =>
        {
            var b = new Vector3(Boxy(u.X, 0.5f), Boxy(u.Y, 0.6f), Boxy(u.Z, 0.4f));
            // 0 at the root, deep in the tower's top, to 1 at the tip, 26 m out over the meadow (the model faces -Z).
            float t = (1f - b.Z) / 2f;
            var p = new Vector3(b.X * Mathf.Lerp(5f, 0.6f, Mathf.Pow(t, 1.3f)), b.Y * Mathf.Lerp(1.6f, 0.4f, t), b.Z * 18f - 8f);
            p.Y += 4f * t * t * t;
            return p + Vector3.One * 0.25f * Lumps.GetNoise3Dv(p * 0.3f);
        }, 40, 18, Painted(), Granite);
        var basis = Basis.LookingAt(new Vector3(facing.X, 0f, facing.Y), Vector3.Up);
        Solid(parent, "Ledge", ledge, new Transform3D(basis, top + Vector3.Down * 1.4f));

        // Boulders heaped round the foot of the tower, the biggest the size of a mammoth.
        var across = facing.Orthogonal();
        var face = new Vector2(top.X, top.Z);
        for (int i = 0; i < 10; i++)
        {
            var at = face + facing * (3f + i % 3 * 2.5f) + across * (i - 4.5f) * 3.5f;
            float size = 2f + (i * 37 % 10) * 0.22f;
            var rock = Lumpy(new Vector3(size * 1.3f, size, size * 1.1f), 0.25f, i, Painted(), Granite);
            Solid(parent, $"Boulder{i}", rock, new Transform3D(new Basis(Vector3.Up, i * 1.3f), new Vector3(at.X, ground(at.X, at.Y) + size * 0.3f, at.Y)));
        }
    }

    /// <summary>
    /// The Mammoth Graveyard: the skeleton of a giant bull mammoth lying where it fell, its ribs arching high enough
    /// overhead to walk through, its skull still carrying its great curling tusks; the bones of four more mammoths, and
    /// tusks, ribs, skulls and leg bones strewn about, bleached by sun and frost, over bare black rock that juts up
    /// through the ground in fangs. Mammoths really did die in numbers in places like
    /// this, where their bones are found in heaps.
    /// </summary>
    public static void Graveyard(Node3D parent, Vector2 centre, Vector2 facing, Func<float, float, float> ground, RandomNumberGenerator rng)
    {
        var bleached = Plain(Bone, 0.8f);
        var aged = Plain(OldBone, 0.85f);
        var across = facing.Orthogonal();

        Vector3 On(Vector2 at, float lift = 0f) => new(at.X, ground(at.X, at.Y) + lift, at.Y);
        Basis Lying(Vector2 along) => Basis.LookingAt(new Vector3(along.X, 0f, along.Y), Vector3.Up);

        // The old bull lies across the way in, so a walker coming from the start goes in under its ribs.
        Skeleton(parent, "OldBull", new Transform3D(Lying(across), On(centre, -0.2f)), 1f, bleached);
        Skeleton(parent, "Cow", new Transform3D(Lying(across.Rotated(0.9f)), On(centre + facing * 16f + across * 10f, -0.2f)), 0.65f, aged);
        Skeleton(parent, "Calf", new Transform3D(Lying(across.Rotated(-1.4f)), On(centre - facing * 14f - across * 12f, -0.15f)), 0.4f, bleached);

        // More of the herd, long dead and half sunk into the ground.
        Skeleton(parent, "OldCow", new Transform3D(Lying(across.Rotated(2.3f)), On(centre - facing * 4f + across * 21f, -0.5f)), 0.75f, aged);
        Skeleton(parent, "YoungBull", new Transform3D(Lying(across.Rotated(-0.5f)), On(centre + facing * 12f - across * 19f, -0.4f)), 0.55f, bleached);

        // Fangs of black rock breaking up through the ground, leaning every way where the frost has heaved them. They
        // keep to the edges, clear of the old bull's ribs, so the way in under them stays open.
        var dark = Painted(0.55f);
        for (int i = 0; i < 24; i++)
        {
            var at = centre + Vector2.Right.Rotated(rng.Randf() * Mathf.Tau) * rng.RandfRange(12f, 34f);
            float tall = rng.RandfRange(1.5f, 6f), thick = tall * rng.RandfRange(0.22f, 0.4f);
            var fang = Lumpy(new Vector3(thick, tall, thick * rng.RandfRange(0.7f, 1.2f)), 0.3f, 40 + i, dark, BlackRock);
            var lean = new Basis(Vector3.Up, rng.Randf() * Mathf.Tau) * new Basis(Vector3.Right, rng.RandfRange(-0.35f, 0.35f));
            Solid(parent, $"BlackRock{i}", fang, new Transform3D(lean, On(at, tall * 0.3f)));
        }

        // Loose tusks curving up out of the ground, long leg bones, fallen ribs and lone skulls lying about.
        for (int i = 0; i < 10; i++)
        {
            var at = centre + Vector2.Right.Rotated(rng.Randf() * Mathf.Tau) * rng.RandfRange(8f, 32f);
            float size = rng.RandfRange(0.5f, 1f);
            var tusk = Tusk(size, rng.Randf() < 0.5f ? 1f : -1f, i % 2 == 0 ? aged : bleached);
            Solid(parent, $"Tusk{i}", tusk, new Transform3D(new Basis(Vector3.Up, rng.Randf() * Mathf.Tau) * new Basis(Vector3.Forward, -0.25f), On(at, -0.6f * size)));
        }
        for (int i = 0; i < 26; i++)
        {
            var at = centre + Vector2.Right.Rotated(rng.Randf() * Mathf.Tau) * rng.RandfRange(5f, 33f);
            float length = rng.RandfRange(1.4f, 3.2f);
            Solid(parent, $"LegBone{i}", LegBone(length, i % 2 == 0 ? bleached : aged),
                new Transform3D(new Basis(Vector3.Up, rng.Randf() * Mathf.Tau), On(at, 0.1f)));
        }
        for (int i = 0; i < 14; i++)
        {
            var at = centre + Vector2.Right.Rotated(rng.Randf() * Mathf.Tau) * rng.RandfRange(6f, 33f);
            // A rib fallen flat, its curve lying along the ground.
            var flat = new Basis(Vector3.Up, rng.Randf() * Mathf.Tau) * new Basis(Vector3.Right, Mathf.Pi / 2f - 0.15f);
            Solid(parent, $"LooseRib{i}", LooseRib(rng.RandfRange(2.5f, 5f), i % 2 == 0 ? aged : bleached), new Transform3D(flat, On(at, 0.15f)));
        }
        for (int i = 0; i < 6; i++)
        {
            var at = centre + Vector2.Right.Rotated(rng.Randf() * Mathf.Tau) * rng.RandfRange(10f, 32f);
            float size = rng.RandfRange(0.5f, 0.9f);
            Solid(parent, $"LoneSkull{i}", Lumpy(new Vector3(1.6f, 1.8f, 1.7f) * size, 0.12f, 60 + i, i % 2 == 0 ? bleached : aged),
                new Transform3D(new Basis(Vector3.Up, rng.Randf() * Mathf.Tau), On(at, 0.3f * size)));
        }
    }

    /// <summary>
    /// Black rock, like the dark shale and basalt that frost heaves up through thin ground: near black in its cracks, a
    /// little greyer on its weathered faces, with only a dusting of snow where it is flat enough to hold any.
    /// </summary>
    private static Color BlackRock(Vector3 p, Vector3 n)
    {
        float grain = Lumps.GetNoise3D(p.X * 2f, p.Y * 2f, p.Z * 2f);
        var rock = new Color(0.09f, 0.09f, 0.1f).Lightened(Mathf.Clamp(grain, 0f, 1f) * 0.12f);
        return rock.Lerp(SnowWhite, Mathf.SmoothStep(0.75f, 0.95f, n.Y) * 0.7f);
    }

    /// <summary>A single rib broken free of its skeleton: an arc, thick at the end that met the spine.</summary>
    private static ArrayMesh LooseRib(float length, Material bone)
    {
        var path = new Vector3[10];
        var radii = new float[10];
        for (int i = 0; i < path.Length; i++)
        {
            float t = i / (path.Length - 1f);
            path[i] = new Vector3((t - 0.5f) * length, length * 0.35f * Mathf.Sin(Mathf.Pi * t), 0f);
            radii[i] = Mathf.Lerp(0.16f, 0.07f, t);
        }
        return Animal.Tube(path, radii, 6, bone, capEnd: true);
    }

    /// <summary>
    /// A mammoth's skeleton lying on its side, <paramref name="size"/> times the size of the old bull: the spine along
    /// the ground, ribs arching up and over to come down again a few metres off, the skull at one end with its tusks, and
    /// the pelvis at the other. The ribs run along X, the skull towards -X.
    /// </summary>
    private static void Skeleton(Node3D parent, string name, Transform3D at, float size, Material bone)
    {
        var node = new Node3D { Name = name, Transform = at };
        parent.AddChild(node);

        var spine = new Vector3[16];
        var spineRadii = new float[16];
        for (int i = 0; i < spine.Length; i++)
        {
            float t = i / (spine.Length - 1f);
            spine[i] = new Vector3((t - 0.5f) * 15f, 0.7f + 0.3f * Mathf.Sin(t * Mathf.Pi), 0f) * size;
            spineRadii[i] = Mathf.Lerp(0.35f, 0.22f, t) * size;
        }
        Solid(node, "Spine", Animal.Tube(spine, spineRadii, 8, bone, capEnd: true), Transform3D.Identity);

        // The ribs are longest in the middle of the chest, where they arch nearly seven metres high.
        for (int r = 0; r < 11; r++)
        {
            float x = -5.5f + r * 1.1f;
            float big = Mathf.Sin(Mathf.Pi * (r + 0.5f) / 11f);
            float high = 2.5f + 4.2f * big, wide = 3f + 2.5f * big;
            var path = new Vector3[14];
            var radii = new float[14];
            for (int i = 0; i < path.Length; i++)
            {
                float t = i / (path.Length - 1f);
                path[i] = new Vector3(x + 0.6f * t, Mathf.Lerp(0.8f, 0f, t) + high * Mathf.Sin(Mathf.Pi * t) * 0.9f,
                    -wide * (1f - Mathf.Cos(Mathf.Pi * t)) / 2f) * size;
                radii[i] = Mathf.Lerp(0.22f, 0.1f, t) * size;
            }
            Solid(node, $"Rib{r}", Animal.Tube(path, radii, 6, bone, capEnd: true), Transform3D.Identity);
        }

        // The skull, domed and heavy, sitting upright with its tusks sweeping out and curling up and in.
        var skullAt = new Vector3(-9.2f, 1.5f, -1.4f) * size;
        Solid(node, "Skull", Lumpy(new Vector3(1.6f, 1.8f, 1.7f) * size, 0.12f, 3, bone), new Transform3D(Basis.Identity, skullAt));
        foreach (float side in new[] { -1f, 1f })
            Solid(node, side < 0f ? "TuskLeft" : "TuskRight", Tusk(size, side, bone),
                new Transform3D(new Basis(Vector3.Up, Mathf.Pi / 2f), skullAt + new Vector3(-1.1f, -0.6f, side * 0.7f) * size));

        Solid(node, "Pelvis", Lumpy(new Vector3(1.1f, 1.7f, 2.4f) * size, 0.15f, 5, bone),
            new Transform3D(Basis.Identity, new Vector3(8.6f, 1.1f, -1.2f) * size));
    }

    /// <summary>
    /// A mammoth's tusk, seven metres along its curve on the old bull: out forwards (-Z), sweeping away to one
    /// <paramref name="side"/> and up, and curling back in at the tip, thick at the root and tapering to a point.
    /// </summary>
    private static ArrayMesh Tusk(float size, float side, Material bone)
    {
        var path = new Vector3[18];
        var radii = new float[18];
        for (int i = 0; i < path.Length; i++)
        {
            float t = i / (path.Length - 1f);
            path[i] = new Vector3(side * (3.2f * t - 2.2f * t * t * t), 3.4f * Mathf.Pow(t, 1.6f), -(6f * t - 2.6f * t * t * t)) * size;
            radii[i] = Mathf.Lerp(0.38f, 0.06f, t) * size;
        }
        return Animal.Tube(path, radii, 8, bone, capEnd: true);
    }

    /// <summary>A long leg bone: a shaft with a knob at each end.</summary>
    private static ArrayMesh LegBone(float length, Material bone)
    {
        var path = new Vector3[6];
        var radii = new float[6];
        for (int i = 0; i < path.Length; i++)
        {
            float t = i / (path.Length - 1f);
            path[i] = new Vector3((t - 0.5f) * length, 0f, 0f);
            radii[i] = t is 0f or 1f ? 0.05f : t < 0.2f || t > 0.8f ? 0.28f : 0.16f;
        }
        return Animal.Tube(path, radii, 8, bone, capEnd: true);
    }

    /// <summary>
    /// The Frozen Falls: a waterfall that froze where it poured over the cliff, a curtain of fluted blue-white ice from
    /// the lip of the cliff to its foot, spreading as it goes down and heaped at the bottom where the spray froze;
    /// icicles hang all along the lip, and the pool at its foot is frozen over. <paramref name="toe"/> is the foot of the
    /// rock face, <paramref name="top"/> how high the cliff top is, and <paramref name="outward"/> faces the meadow.
    /// </summary>
    public static void Falls(Node3D parent, Vector3 toe, float top, Vector3 outward, Func<float, float, float> ground)
    {
        var across = outward.Cross(Vector3.Up).Normalized();
        float bottom = toe.Y - 0.5f;
        const int columns = 20, rows = 28;

        var mesh = new MeshBuilder();
        var index = new int[columns + 1, rows + 2];
        for (int r = 0; r <= rows + 1; r++)
        {
            // The first row lies back over the top of the cliff, where the river ran over the edge; the rest hang down.
            float t = r == 0 ? 0f : (r - 1f) / rows;
            float halfWidth = Mathf.Lerp(4f, 8f, t * t);
            for (int c = 0; c <= columns; c++)
            {
                float s = c / (float)columns * 2f - 1f;
                float flute = 0.5f * Mathf.Abs(Mathf.Sin(c * 1.3f + r * 0.12f)) + 0.4f * Lumps.GetNoise2D(c * 2f, r * 0.7f);
                // Thickest in the middle, where most water ran, and bulging out into a cone of ice at the bottom.
                float out_ = r == 0 ? -4f : 0.8f + (1f - s * s) * (0.6f + 2.6f * t * t) + flute;
                float y = r == 0 ? top + 0.3f : Mathf.Lerp(top - 0.4f, bottom, t);
                var point = toe + outward * out_ + across * s * halfWidth;
                point.Y = y;
                var colour = PaleIce.Lerp(DeepIce, 0.25f + 0.35f * Mathf.Abs(Mathf.Sin(c * 1.3f))).Lerp(SnowWhite, r <= 1 ? 0.8f : 0f);
                index[c, r] = mesh.Add(point, (outward + across * s * 0.3f).Normalized(), colour);
            }
        }
        for (int r = 0; r <= rows; r++)
        for (int c = 0; c < columns; c++)
            mesh.Quad(index[c, r], index[c + 1, r], index[c + 1, r + 1], index[c, r + 1]);
        Solid(parent, "Curtain", mesh.Commit(Painted(0.12f, 0.15f)), Transform3D.Identity);

        // Icicles all along the lip on either side, longer near the falls, where the spray reached.
        var icicle = Plain(PaleIce, 0.1f, 0.1f);
        for (int i = 0; i < 34; i++)
        {
            float s = (i / 33f * 2f - 1f) * 16f;
            if (Mathf.Abs(s) < 4.5f)
                continue;
            float length = Mathf.Lerp(5f, 1.2f, Mathf.Abs(s) / 16f) * (0.6f + 0.4f * Mathf.Abs(Mathf.Sin(i * 2.7f)));
            var cone = new CylinderMesh { TopRadius = 0.22f + length * 0.04f, BottomRadius = 0.01f, Height = length, RadialSegments = 6, Rings = 1, Material = icicle };
            var at = toe + outward * 0.4f + across * s;
            at.Y = top - 0.3f - length / 2f;
            parent.AddChild(new MeshInstance3D { Name = $"Icicle{i}", Mesh = cone, Position = at });
        }

        // The frozen pool, and blocks of ice heaped at the foot of the curtain.
        var pool = toe + outward * 7f;
        parent.AddChild(new MeshInstance3D
        {
            Name = "FrozenPool",
            Mesh = new CylinderMesh { TopRadius = 7f, BottomRadius = 7f, Height = 0.6f, RadialSegments = 32, Material = Plain(new Color(0.62f, 0.82f, 0.93f), 0.05f, 0.2f) },
            Position = new Vector3(pool.X, ground(pool.X, pool.Z) - 0.2f, pool.Z),
        });
        for (int i = 0; i < 6; i++)
        {
            var at = toe + outward * (2.5f + i % 2 * 2f) + across * (i - 2.5f) * 2.4f;
            float size = 1.1f + i % 3 * 0.4f;
            var block = Lumpy(new Vector3(size, size * 0.8f, size), 0.3f, 10 + i, Painted(0.12f, 0.15f), (p, n) => Ice(p, n, 0.4f));
            Solid(parent, $"IceBlock{i}", block, new Transform3D(Basis.Identity, new Vector3(at.X, ground(at.X, at.Z) + size * 0.3f, at.Z)));
        }
    }

    /// <summary>
    /// The Balanced Stone: a huge boulder the retreating ice left perched on a narrow pillar of rock, as glacial
    /// erratics are, on top of a knoll. Three tall stones lean against one side, each higher than the last, so a good
    /// jumper can climb from one to the next and up onto the boulder's top.
    /// </summary>
    public static void BalancedStone(Node3D parent, Vector3 top, Vector2 facing, Func<float, float, float> ground)
    {
        var rock = Painted();
        // A slim pillar six metres high, and the boulder, far wider than it, perched on its top.
        Solid(parent, "Pedestal", Lumpy(new Vector3(1.7f, 3.8f, 1.7f), 0.15f, 20, rock, Granite), new Transform3D(Basis.Identity, top + Vector3.Up * 2.6f));
        Solid(parent, "Boulder", Lumpy(new Vector3(7f, 4.4f, 6f), 0.18f, 21, rock, Granite),
            new Transform3D(new Basis(new Vector3(facing.X, 0f, facing.Y), 0.12f), top + Vector3.Up * 10.4f));

        // Each stepping stone reaches from below the ground up to its own height; the boulder's top is near 15 m. They
        // stand round the back, so the boulder is seen perched alone from the front.
        var side = (facing.Orthogonal() - facing).Normalized();
        var steps = new[] { (Out: 13f, Top: 3.5f), (Out: 10f, Top: 6.5f), (Out: 7.2f, Top: 9.5f) };
        for (int i = 0; i < steps.Length; i++)
        {
            var at = new Vector2(top.X, top.Z) + side * steps[i].Out;
            float floor = ground(at.X, at.Y) - 0.6f;
            float peak = top.Y + steps[i].Top;
            float half = (peak - floor) / 2f;
            var stone = Lumpy(new Vector3(2.4f, half, 2.2f), 0.15f, 22 + i, rock, Granite);
            Solid(parent, $"Step{i}", stone, new Transform3D(new Basis(Vector3.Up, i * 0.8f), new Vector3(at.X, floor + half, at.Y)));
        }
    }

    /// <summary>Steam rising off a hot pool in soft, slow puffs that swell and fade as they drift up into the cold air.</summary>
    public static void Steam(Node3D parent, Vector3 surface, float radius)
    {
        var puff = new GradientTexture2D
        {
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(1f, 0.5f),
            Width = 64,
            Height = 64,
            Gradient = new Gradient { Offsets = [0f, 1f], Colors = [new Color(1f, 1f, 1f, 1f), new Color(1f, 1f, 1f, 0f)] },
        };
        var material = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
            VertexColorUseAsAlbedo = true,
            AlbedoTexture = puff,
        };
        var grow = new Curve();
        grow.AddPoint(new Vector2(0f, 0.4f));
        grow.AddPoint(new Vector2(1f, 1f));
        parent.AddChild(new CpuParticles3D
        {
            Name = "Steam",
            Position = surface + Vector3.Up * 0.3f,
            Amount = Mathf.RoundToInt(radius * 10f),
            Lifetime = 7f,
            Preprocess = 7f,
            Mesh = new QuadMesh { Size = new Vector2(4f, 4f), Material = material },
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(radius * 0.7f, 0.1f, radius * 0.7f),
            Direction = Vector3.Up,
            Spread = 20f,
            InitialVelocityMin = 0.4f,
            InitialVelocityMax = 1f,
            Gravity = new Vector3(0.1f, 0.25f, 0f),
            ScaleAmountMin = 1f,
            ScaleAmountMax = 2.5f,
            ScaleAmountCurve = grow,
            ColorRamp = new Gradient
            {
                Offsets = [0f, 0.2f, 1f],
                Colors = [new Color(1f, 1f, 1f, 0f), new Color(0.95f, 0.97f, 1f, 0.6f), new Color(1f, 1f, 1f, 0f)],
            },
        });
    }

    /// <summary>
    /// The Ice Arch: a span of old glacier ice twenty metres across and thirteen high, left standing as the glacier
    /// melted back around it, thick at its feet and thinner over the top, with snow lying along its crown. Its legs are
    /// sheer, so only a climber gets up onto it. Blocks and pinnacles of ice stand round about.
    /// </summary>
    public static void IceArch(Node3D parent, Vector2 centre, Vector2 along, Func<float, float, float> ground, RandomNumberGenerator rng)
    {
        const float span = 22f, rise = 13f;
        const int steps = 44, sides = 18;
        var a = centre - along * span / 2f;
        var b = centre + along * span / 2f;
        float low = Mathf.Min(ground(a.X, a.Y), ground(b.X, b.Y)) - 2f;
        float high = Mathf.Max(ground(a.X, a.Y), ground(b.X, b.Y)) + rise;
        var across = new Vector3(along.Y, 0f, -along.X);

        Vector3 Centre(float t) => new(
            Mathf.Lerp(a.X, b.X, t),
            low + (high - low) * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.Pi * t)), 0.55f),
            Mathf.Lerp(a.Y, b.Y, t));

        var mesh = new MeshBuilder();
        var index = new int[steps + 1, sides + 1];
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            var point = Centre(t);
            var tangent = (Centre(Mathf.Min(1f, t + 0.01f)) - Centre(Mathf.Max(0f, t - 0.01f))).Normalized();
            var up = tangent.Cross(across).Normalized();
            // Sin(Pi) comes out a hair below zero, which Pow would turn into NaN.
            float arc = Mathf.Max(0f, Mathf.Sin(Mathf.Pi * t));
            float thick = Mathf.Lerp(4.5f, 2.4f, Mathf.Pow(arc, 1.5f));
            float wide = Mathf.Lerp(5.5f, 3.8f, arc);
            for (int s = 0; s <= sides; s++)
            {
                float angle = Mathf.Tau * s / sides;
                float cx = Boxy(Mathf.Cos(angle), 0.6f), cy = Boxy(Mathf.Sin(angle), 0.6f);
                var offset = across * wide * cx + up * thick * cy;
                var vertex = point + offset * (1f + 0.08f * Lumps.GetNoise3Dv(point * 0.2f + offset));
                var normal = (across * cx / wide + up * cy / thick).Normalized();
                index[i, s] = mesh.Add(vertex, normal, Ice(vertex, normal, (vertex.Y - low) / (high - low)));
            }
        }
        for (int i = 0; i < steps; i++)
        for (int s = 0; s < sides; s++)
            mesh.Quad(index[i, s], index[i, s + 1], index[i + 1, s + 1], index[i + 1, s]);
        Solid(parent, "Arch", mesh.Commit(Painted(0.15f, 0.1f)), Transform3D.Identity);

        // Seracs: leaning pinnacles and blocks of ice, all that is left of the rest of the glacier.
        var material = Painted(0.15f, 0.1f);
        for (int i = 0; i < 7; i++)
        {
            Vector2 at;
            do
                at = centre + Vector2.Right.Rotated(rng.Randf() * Mathf.Tau) * rng.RandfRange(9f, 22f);
            // Off beyond the feet, out of the way through the arch.
            while (Mathf.Abs((at - centre).Dot(along)) < span / 2f + 2f);
            float tall = rng.RandfRange(2f, 6f);
            var radii = new Vector3(rng.RandfRange(1.5f, 3f), tall, rng.RandfRange(1.5f, 2.5f));
            var block = Lumpy(radii, 0.25f, 40 + i, material, (p, n) => Ice(p, n, (p.Y + tall) / (2f * tall)));
            var tilt = new Basis(Vector3.Up, rng.Randf() * Mathf.Tau) * new Basis(Vector3.Right, rng.RandfRange(-0.2f, 0.2f));
            Solid(parent, $"Serac{i}", block, new Transform3D(tilt, new Vector3(at.X, ground(at.X, at.Y) + tall * 0.6f, at.Y)));
        }
    }

    /// <summary>
    /// The Pillars: hundreds of six-sided columns of black basalt packed side by side, as lava cracks when it cools
    /// slowly, broken off at different heights so they rise in a rough stair to a crest at the back. Each is solid, so
    /// an animal can hop from column to column and climb to the top; snow lies on their flat tops.
    /// </summary>
    public static void Pillars(Node3D parent, Vector2 centre, Vector2 facing, float radius, Func<float, float, float> ground, RandomNumberGenerator rng)
    {
        const float corner = 1.25f;
        float flat = corner * Mathf.Sqrt(3f);
        // The crest stands at the back, away from the start, so the stair climbs as a visitor walks in.
        var crest = centre - facing * radius * 0.35f;
        float floor = ground(centre.X, centre.Y);

        var column = HexColumn();
        var placements = new List<(Transform3D Transform, Color Tint)>();
        var body = new StaticBody3D { Name = "Columns" };
        int reach = Mathf.CeilToInt(radius / flat) + 1;
        var e1 = new Vector2(Mathf.Cos(Mathf.Pi / 6f), Mathf.Sin(Mathf.Pi / 6f)) * flat;
        var e2 = new Vector2(0f, flat);
        for (int q = -reach * 2; q <= reach * 2; q++)
        for (int r = -reach * 2; r <= reach * 2; r++)
        {
            var at = centre + e1 * q + e2 * r;
            var offset = at - centre;
            float edge = radius * (1f + 0.18f * Lumps.GetNoise2D(Mathf.Atan2(offset.Y, offset.X) * 3f, 0f));
            if (offset.Length() > edge || rng.Randf() < 0.05f)
                continue;

            // Broken off in steps of about knee height, so neighbours differ by a hop.
            float rise = 12f * Mathf.Pow(Mathf.Clamp(1f - at.DistanceTo(crest) / (radius * 1.25f), 0f, 1f), 1.2f);
            rise = Mathf.Snapped(rise + rng.RandfRange(0f, 0.7f), 0.45f);
            float g = ground(at.X, at.Y);
            float topY = Mathf.Max(floor + 0.3f + rise, g + 0.3f);
            float baseY = g - 1.5f;
            float height = topY - baseY;

            float tint = rng.RandfRange(0.85f, 1.15f);
            placements.Add((new Transform3D(Basis.FromScale(new Vector3(corner * 0.98f, height, corner * 0.98f)), new Vector3(at.X, baseY, at.Y)),
                new Color(tint, tint, tint)));
            body.AddChild(new CollisionShape3D
            {
                Shape = new CylinderShape3D { Radius = corner * 0.92f, Height = height },
                Position = new Vector3(at.X, baseY + height / 2f, at.Y),
            });
        }

        var multi = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            Mesh = column,
            InstanceCount = placements.Count,
        };
        for (int i = 0; i < placements.Count; i++)
        {
            multi.SetInstanceTransform(i, placements[i].Transform);
            multi.SetInstanceColor(i, placements[i].Tint);
        }
        parent.AddChild(new MultiMeshInstance3D { Name = "Basalt", Multimesh = multi });
        parent.AddChild(body);
    }

    /// <summary>One basalt column: a six-sided prism a unit across its corners and a unit tall, its top white with snow.</summary>
    private static ArrayMesh HexColumn()
    {
        var mesh = new MeshBuilder();
        var corners = new Vector3[6];
        for (int k = 0; k < 6; k++)
            corners[k] = new Vector3(Mathf.Cos(k * Mathf.Pi / 3f), 0f, Mathf.Sin(k * Mathf.Pi / 3f));

        int middle = mesh.Add(Vector3.Up, Vector3.Up, SnowWhite);
        var rim = new int[6];
        for (int k = 0; k < 6; k++)
            rim[k] = mesh.Add(corners[k] + Vector3.Up, Vector3.Up, SnowWhite);
        for (int k = 0; k < 6; k++)
            mesh.Tri(middle, rim[(k + 1) % 6], rim[k]);

        for (int k = 0; k < 6; k++)
        {
            var a = corners[k];
            var b = corners[(k + 1) % 6];
            var normal = ((a + b) / 2f).Normalized();
            // A rim of snow just over the edge, then dark rock all the way down.
            int a0 = mesh.Add(a, normal, Basalt), b0 = mesh.Add(b, normal, Basalt);
            int a1 = mesh.Add(a + Vector3.Up, normal, Basalt.Lightened(0.15f)), b1 = mesh.Add(b + Vector3.Up, normal, Basalt.Lightened(0.15f));
            mesh.Quad(a0, a1, b1, b0);
        }
        return mesh.Commit(Painted(0.85f));
    }
}
