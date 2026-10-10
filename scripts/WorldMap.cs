using Godot;

namespace IceAgeWorld;

/// <summary>
/// A map of the whole world, shown and hidden with M: green steppe, white snowfields and grey crags, blue lakes and
/// dark specks of forest, drawn once from the terrain, with north at the top. Over it, an arrow shows where the player
/// is and which way it faces, and dots show where every other animal of its kind is: the wild ones in gold and the
/// player's own pack, herd or family in green.
/// </summary>
public partial class WorldMap : Control
{
    public Player? Player { get; set; }

    /// <summary>Pixels across the map picture; each covers a few metres of ground.</summary>
    private const int Pixels = 256;

    /// <summary>How much of the screen's shorter side the map fills.</summary>
    private const float ScreenShare = 0.85f;

    private static readonly Color Steppe = new(0.45f, 0.6f, 0.32f);
    private static readonly Color Tundra = new(0.78f, 0.78f, 0.7f);
    private static readonly Color Snow = new(0.95f, 0.96f, 0.98f);
    private static readonly Color Rock = new(0.5f, 0.5f, 0.52f);
    private static readonly Color Lake = new(0.25f, 0.45f, 0.75f);
    private static readonly Color Forest = new(0.16f, 0.3f, 0.18f);
    private static readonly Color Wild = new(1f, 0.8f, 0.2f);
    private static readonly Color Own = new(0.35f, 1f, 0.45f);
    private static readonly Color Ink = new(0.08f, 0.08f, 0.1f);

    private ImageTexture? _picture;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
    }

    public override void _Process(double delta)
    {
        if (Input.IsActionJustPressed(InputSetup.Map))
            Visible = !Visible;
        if (Visible)
            QueueRedraw();
    }

    public override void _Draw()
    {
        if (Player?.Terrain is not { } terrain)
            return;
        _picture ??= Paint(terrain);

        // A square map in the middle of the screen, with a strip for the key beneath it, all on a dark backing.
        var screen = GetViewportRect().Size;
        float side = Mathf.Min(screen.X, screen.Y - KeyHeight) * ScreenShare;
        var area = new Rect2((screen - new Vector2(side, side + KeyHeight)) / 2f, new Vector2(side, side));
        DrawRect(new Rect2(area.Position - new Vector2(8f, 8f), new Vector2(side + 16f, side + KeyHeight + 12f)), new Color(0f, 0f, 0f, 0.8f));
        DrawTextureRect(_picture, area, false);

        // World X runs left to right and Z top to bottom, so -Z, north, is up.
        Vector2 ToMap(Vector3 at) =>
            area.Position + new Vector2(at.X + terrain.HalfSize, at.Z + terrain.HalfSize) / (terrain.HalfSize * 2f) * side;

        // Every other animal of the player's kind: the wild ones first, then its own companions on top.
        var kind = Player.Animal.GetType();
        foreach (var at in Player.Wildlife?.WhereAre(kind) ?? [])
            Dot(ToMap(at), Wild);
        foreach (var at in Player.Companions)
            Dot(ToMap(at), Own);

        // The player: an arrow pointing the way it faces.
        Arrow(ToMap(Player.GlobalPosition), Player.Animal.Rotation.Y);

        // North, and a key to the marks.
        var font = ThemeDB.FallbackFont;
        DrawString(font, new Vector2(area.GetCenter().X - 6f, area.Position.Y + 22f), "N", fontSize: 20, modulate: Ink);
        var key = area.Position + new Vector2(14f, side + 30f);
        Arrow(key + new Vector2(0f, -6f), 0f);
        DrawString(font, key + new Vector2(16f, 0f), "You", fontSize: 18);
        Dot(key + new Vector2(80f, -6f), Wild);
        DrawString(font, key + new Vector2(92f, 0f), $"Wild {Plural(Player.Animal.DisplayName.ToLower())}", fontSize: 18);
        Dot(key + new Vector2(290f, -6f), Own);
        DrawString(font, key + new Vector2(302f, 0f), "With you", fontSize: 18);
        DrawString(font, key + new Vector2(side - 120f, 0f), "M to close", fontSize: 18);
    }

    /// <summary>Height of the strip under the map that holds the key.</summary>
    private const float KeyHeight = 44f;

    private void Dot(Vector2 at, Color colour)
    {
        DrawCircle(at, 6f, Ink);
        DrawCircle(at, 4.5f, colour);
    }

    /// <summary>A white arrowhead at <paramref name="at"/>, pointing the way a heading of <paramref name="yaw"/> faces.</summary>
    private void Arrow(Vector2 at, float yaw)
    {
        var forward = new Vector2(-Mathf.Sin(yaw), -Mathf.Cos(yaw));
        var across = new Vector2(-forward.Y, forward.X);
        Vector2[] arrow = [at + forward * 13f, at - forward * 8f + across * 8f, at - forward * 4f, at - forward * 8f - across * 8f];
        DrawColoredPolygon(arrow, Colors.White);
        DrawPolyline([.. arrow, arrow[0]], Ink, 2f);
    }

    /// <summary>More than one of an animal: wolves, but moose and reindeer stay as they are.</summary>
    private static string Plural(string name) =>
        name.EndsWith("moose") || name.EndsWith("reindeer") ? name
        : name.EndsWith('f') ? name[..^1] + "ves"
        : name + "s";

    /// <summary>
    /// Paints the land once, from above: lakes blue, steep crags grey, high ground white with snow, the green steppe
    /// fading to pale tundra towards the snow line, and every tree a dark speck, so forests show as dark patches.
    /// Shading by height, lit from the north-west as maps are, brings out the hills.
    /// </summary>
    private static ImageTexture Paint(Terrain terrain)
    {
        var image = Image.CreateEmpty(Pixels, Pixels, false, Image.Format.Rgb8);
        float step = terrain.HalfSize * 2f / Pixels;
        for (int py = 0; py < Pixels; py++)
        {
            for (int px = 0; px < Pixels; px++)
            {
                float x = -terrain.HalfSize + (px + 0.5f) * step;
                float z = -terrain.HalfSize + (py + 0.5f) * step;
                Color colour;
                if (terrain.DistanceFromWater(x, z) < 0f)
                {
                    colour = Lake;
                }
                else
                {
                    float height = terrain.GetHeight(x, z);
                    float snow = Mathf.SmoothStep(terrain.HeightScale * 0.28f, terrain.HeightScale * 0.4f, height);
                    colour = Steppe.Lerp(Tundra, 1f - terrain.GrassAmount(x, z)).Lerp(Snow, snow);
                    if (terrain.IsSteep(x, z))
                        colour = colour.Lerp(Rock, 0.6f);
                    float slope = terrain.GetHeight(x - step, z - step) - height;
                    colour = colour.Darkened(Mathf.Clamp(slope * 0.08f, 0f, 0.3f)).Lightened(Mathf.Clamp(-slope * 0.05f, 0f, 0.2f));
                }
                image.SetPixel(px, py, colour);
            }
        }

        foreach (var tree in terrain.Trees)
        {
            var at = tree.Transform.Origin;
            int px = (int)((at.X + terrain.HalfSize) / step), py = (int)((at.Z + terrain.HalfSize) / step);
            if (px is >= 0 and < Pixels && py is >= 0 and < Pixels)
                image.SetPixel(px, py, Forest);
        }
        return ImageTexture.CreateFromImage(image);
    }
}
