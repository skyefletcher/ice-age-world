using System;
using System.Collections.Generic;
using Godot;

namespace IceAgeWorld;

/// <summary>
/// Generates seamless textures from noise at startup so the project needs no image assets.
/// Each material gets an albedo map and a matching normal map, applied triplanar so meshes built in code
/// need no UV layout.
/// </summary>
public static class ProceduralTextures
{
    private const int Size = 256;

    /// <summary>Shaggy hide: long vertical streaks of hair over a fine grain, between two shades of brown.</summary>
    public static StandardMaterial3D Fur(int seed, Color dark, Color light, float scale)
    {
        var heights = HeightField(seed, stretch: 6, streakFrequency: 0.05f, grainWeight: 0.3f);
        var (albedo, normal) = Textures(heights, (_, h) => dark.Lerp(light, h), bumpStrength: 6f);
        return new StandardMaterial3D
        {
            AlbedoTexture = albedo,
            NormalEnabled = true,
            NormalTexture = normal,
            NormalScale = 0.8f,
            Uv1Triplanar = true,
            Uv1Scale = Vector3.One * scale,
            Roughness = 1f,
        };
    }

    /// <summary>Tusk ivory: cream with faint growth lines and a slight sheen.</summary>
    public static StandardMaterial3D Ivory(int seed)
    {
        var dark = new Color(0.78f, 0.72f, 0.58f);
        var light = new Color(0.95f, 0.92f, 0.82f);
        var heights = HeightField(seed, stretch: 4, streakFrequency: 0.12f, grainWeight: 0.2f);
        var (albedo, normal) = Textures(heights, (_, h) => dark.Lerp(light, h), bumpStrength: 2f);
        return new StandardMaterial3D
        {
            AlbedoTexture = albedo,
            NormalEnabled = true,
            NormalTexture = normal,
            NormalScale = 0.4f,
            Uv1Triplanar = true,
            Uv1Scale = Vector3.One * 1.5f,
            Roughness = 0.55f,
        };
    }

    /// <summary>
    /// Snow leopard coat: pale smoky fur scattered with broken dark rosettes around tawny centres, and small
    /// solid spots between them. Without <paramref name="rosettes"/> it has only solid spots, as on the head and legs.
    /// </summary>
    public static StandardMaterial3D SpottedFur(int seed, Color dark, Color light, Color centre, Color spot, float scale,
        bool rosettes = true)
    {
        var heights = HeightField(seed, stretch: 6, streakFrequency: 0.06f, grainWeight: 0.4f);
        var (ring, inside) = Rosettes(seed, rosettes);
        var (albedo, normal) = Textures(heights,
            (i, h) => dark.Lerp(light, h).Lerp(centre, inside[i] * 0.7f).Lerp(spot, ring[i] * 0.8f), bumpStrength: 4f);
        return new StandardMaterial3D
        {
            AlbedoTexture = albedo,
            NormalEnabled = true,
            NormalTexture = normal,
            NormalScale = 0.6f,
            Uv1Triplanar = true,
            Uv1Scale = Vector3.One * scale,
            // Blend sharply between the three projections so rosettes on curved flanks are not smeared into streaks.
            Uv1TriplanarSharpness = 6f,
            Roughness = 1f,
        };
    }

    /// <summary>
    /// The colour a triplanar material's albedo shows at a point on a mesh with the given normal, worked out the
    /// way the material's shader does, so hair grown from that point can take the colour of the hide beneath it.
    /// </summary>
    public static Func<Vector3, Vector3, Color> Colouring(StandardMaterial3D material)
    {
        var image = material.AlbedoTexture.GetImage();
        int width = image.GetWidth(), height = image.GetHeight();
        var scale = material.Uv1Scale;
        float sharpness = material.Uv1TriplanarSharpness;

        Color At(float u, float v) =>
            image.GetPixel(Mathf.PosMod(Mathf.FloorToInt(u * width), width), Mathf.PosMod(Mathf.FloorToInt(v * height), height));

        return (position, normal) =>
        {
            var p = position * scale * new Vector3(1, -1, 1);
            var w = new Vector3(Mathf.Pow(Mathf.Abs(normal.X), sharpness), Mathf.Pow(Mathf.Abs(normal.Y), sharpness), Mathf.Pow(Mathf.Abs(normal.Z), sharpness));
            w /= w.X + w.Y + w.Z;
            return At(p.X, p.Y) * w.Z + At(p.Z, p.Y) * w.X + At(p.X, -p.Z) * w.Y;
        };
    }

    /// <summary>
    /// Masks for a tileable rosette pattern: <c>Ring</c> is 1 on the dark rosette rims and solid spots, and
    /// <c>Inside</c> is 1 within each rosette. Distances wrap around the tile's edges so the pattern stays seamless.
    /// </summary>
    private static (float[] Ring, float[] Inside) Rosettes(int seed, bool rosettes)
    {
        var rng = new RandomNumberGenerator { Seed = (ulong)seed };
        var spots = new List<(float X, float Y, float Radius, float Phase, bool Rosette)>();

        // Rosettes (or, without them, larger solid spots) on a jittered grid so they spread evenly, with small
        // spots scattered between them.
        const int grid = 5;
        const float cell = Size / (float)grid;
        for (int gy = 0; gy < grid; gy++)
        for (int gx = 0; gx < grid; gx++)
        {
            float x = (gx + rng.RandfRange(0.2f, 0.8f)) * cell, y = (gy + rng.RandfRange(0.2f, 0.8f)) * cell;
            float radius = rng.RandfRange(13f, 19f), phase = rng.Randf() * Mathf.Tau;
            spots.Add(rosettes ? (x, y, radius, phase, true) : (x, y, radius * 0.4f, 0f, false));
            spots.Add(((gx + rng.Randf()) * cell, (gy + rng.Randf()) * cell, rng.RandfRange(2.5f, 5f), 0f, false));
        }

        var ring = new float[Size * Size];
        var inside = new float[Size * Size];
        const float rim = 5f;
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            int i = y * Size + x;
            foreach (var s in spots)
            {
                float dx = Mathf.PosMod(x - s.X + Size / 2f, Size) - Size / 2f;
                float dy = Mathf.PosMod(y - s.Y + Size / 2f, Size) - Size / 2f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > s.Radius + rim)
                    continue;

                if (!s.Rosette)
                {
                    ring[i] = Mathf.Max(ring[i], 1f - Mathf.SmoothStep(s.Radius - 1.5f, s.Radius, d));
                    continue;
                }

                // The rim is broken into a few blotches by gaps around the circle, as real rosettes are.
                float angle = Mathf.Atan2(dy, dx);
                float gaps = Mathf.Sin(angle * 3f + s.Phase) + 0.5f * Mathf.Sin(angle * 5f + s.Phase * 2f);
                float present = Mathf.SmoothStep(-0.9f, -0.5f, gaps);
                float onRim = 1f - Mathf.SmoothStep(rim * 0.5f, rim * 0.5f + 1.5f, Mathf.Abs(d - s.Radius));
                ring[i] = Mathf.Max(ring[i], onRim * present);
                inside[i] = Mathf.Max(inside[i], 1f - Mathf.SmoothStep(s.Radius - rim, s.Radius - rim * 0.5f, d));
            }
        }
        return (ring, inside);
    }

    /// <summary>
    /// A tileable height field made of noise stretched along V (so it reads as strands) plus fine grain.
    /// Stretching a seamless image that is <paramref name="stretch"/> times shorter keeps the result tileable.
    /// </summary>
    private static float[] HeightField(int seed, int stretch, float streakFrequency, float grainWeight)
    {
        var streakNoise = new FastNoiseLite
        {
            Seed = seed,
            NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
            FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
            FractalOctaves = 4,
            Frequency = streakFrequency,
        };
        var grainNoise = new FastNoiseLite
        {
            Seed = seed + 1,
            NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex,
            FractalOctaves = 2,
            Frequency = 0.3f,
        };

        var streaks = Luminance(streakNoise.GetSeamlessImage(Size, Size / stretch));
        var grain = Luminance(grainNoise.GetSeamlessImage(Size, Size));

        var heights = new float[Size * Size];
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            float s = Sample(streaks, Size, Size / stretch, x, (float)y / stretch);
            float g = grain[y * Size + x];
            heights[y * Size + x] = Mathf.Clamp(s * (1f - grainWeight) + g * grainWeight, 0f, 1f);
        }
        return heights;
    }

    private static (ImageTexture Albedo, ImageTexture Normal) Textures(float[] heights, System.Func<int, float, Color> colourOf, float bumpStrength)
    {
        var rgb = new byte[Size * Size * 3];
        var bump = new byte[Size * Size];
        for (int i = 0; i < heights.Length; i++)
        {
            var c = colourOf(i, heights[i]);
            rgb[i * 3] = (byte)(Mathf.Clamp(c.R, 0f, 1f) * 255f);
            rgb[i * 3 + 1] = (byte)(Mathf.Clamp(c.G, 0f, 1f) * 255f);
            rgb[i * 3 + 2] = (byte)(Mathf.Clamp(c.B, 0f, 1f) * 255f);
            bump[i] = (byte)(heights[i] * 255f);
        }

        var albedo = Image.CreateFromData(Size, Size, false, Image.Format.Rgb8, rgb);
        albedo.GenerateMipmaps();

        var normal = Image.CreateFromData(Size, Size, false, Image.Format.L8, bump);
        normal.BumpMapToNormalMap(bumpStrength);
        normal.GenerateMipmaps();

        return (ImageTexture.CreateFromImage(albedo), ImageTexture.CreateFromImage(normal));
    }

    /// <summary>Reads a greyscale image into a 0..1 float array.</summary>
    private static float[] Luminance(Image image)
    {
        image.Convert(Image.Format.L8);
        var data = image.GetData();
        var result = new float[data.Length];
        for (int i = 0; i < data.Length; i++)
            result[i] = data[i] / 255f;
        return result;
    }

    /// <summary>Bilinear sample with wrap-around, so stretched images stay seamless.</summary>
    private static float Sample(float[] pixels, int width, int height, float x, float y)
    {
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float tx = x - x0, ty = y - y0;

        float At(int px, int py) => pixels[Mathf.PosMod(py, height) * width + Mathf.PosMod(px, width)];

        return Mathf.Lerp(
            Mathf.Lerp(At(x0, y0), At(x0 + 1, y0), tx),
            Mathf.Lerp(At(x0, y0 + 1), At(x0 + 1, y0 + 1), tx),
            ty);
    }
}
