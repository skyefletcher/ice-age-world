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
        var (albedo, normal) = Textures(heights, h => dark.Lerp(light, h), bumpStrength: 6f);
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
        var (albedo, normal) = Textures(heights, h => dark.Lerp(light, h), bumpStrength: 2f);
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

    private static (ImageTexture Albedo, ImageTexture Normal) Textures(float[] heights, System.Func<float, Color> colourOf, float bumpStrength)
    {
        var rgb = new byte[Size * Size * 3];
        var bump = new byte[Size * Size];
        for (int i = 0; i < heights.Length; i++)
        {
            var c = colourOf(heights[i]);
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
