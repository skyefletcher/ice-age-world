using System;
using Godot;

namespace IceAgeWorld;

/// <summary>
/// A round map of the land round the player, as in Savannah Life, in the bottom right corner unless hidden with M: green
/// steppe, white snowfields and grey crags, blue lakes and dark specks of forest, black marks for cave mouths, drawn
/// once from the terrain, with north at the top, and gold diamonds for the famous places, named. It follows the player,
/// the arrow in the middle pointing the way it faces, and shows only the land nearby. Dots show where every other animal
/// of its kind is: the wild ones in gold and the player's own pack, herd or family in green.
/// </summary>
public partial class WorldMap : Control
{
    public Player? Player { get; set; }

    /// <summary>Pixels across the map picture; each covers a few metres of ground.</summary>
    private const int Pixels = 512;

    /// <summary>How much of the screen's shorter side the map fills.</summary>
    private const float ScreenShare = 0.3f;

    /// <summary>How much of the world's width the map shows across, round the player: the land within a short run.</summary>
    private const float ViewShare = 0.3f;

    private static readonly Color Steppe = new(0.45f, 0.6f, 0.32f);
    private static readonly Color Tundra = new(0.78f, 0.78f, 0.7f);
    private static readonly Color Snow = new(0.95f, 0.96f, 0.98f);
    private static readonly Color Rock = new(0.5f, 0.5f, 0.52f);
    private static readonly Color Lake = new(0.25f, 0.45f, 0.75f);
    private static readonly Color Forest = new(0.16f, 0.3f, 0.18f);
    private static readonly Color Wild = new(1f, 0.8f, 0.2f);
    private static readonly Color Own = new(0.35f, 1f, 0.45f);
    private static readonly Color Ink = new(0.08f, 0.08f, 0.1f);
    private static readonly Color Gold = new(1f, 0.82f, 0.25f);

    private ImageTexture? _picture;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = true;
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

        // A small round map in the bottom right corner, clear of the needs bars in the bottom left, on a dark disc with a
        // gold rim, and the key on a dark strip beneath it.
        var screen = GetViewportRect().Size;
        float side = Mathf.Min(screen.X, screen.Y - KeyHeight) * ScreenShare;
        var area = new Rect2(new Vector2(screen.X - Margin - side, screen.Y - Margin - KeyHeight - side), new Vector2(side, side));
        var middle = area.GetCenter();
        float radius = side / 2f;
        var backing = new Color(0f, 0f, 0f, 0.8f);
        DrawCircle(middle, radius + 7f, backing);
        // The picture is cut round where the player is.
        var player = Player.GlobalPosition;
        var here = new Vector2(player.X + terrain.HalfSize, player.Z + terrain.HalfSize) / (terrain.HalfSize * 2f);
        var rim = new Vector2[RimPoints];
        var uvs = new Vector2[RimPoints];
        for (int i = 0; i < RimPoints; i++)
        {
            var round = Vector2.FromAngle(Mathf.Tau * i / RimPoints);
            rim[i] = middle + round * radius;
            uvs[i] = here + round * ViewShare / 2f;
        }
        DrawPolygon(rim, new[] { Colors.White }, uvs, _picture);
        DrawArc(middle, radius + 2f, 0f, Mathf.Tau, RimPoints, Gold, 4f, true);

        // World X runs left to right and Z top to bottom, so -Z, north, is up, and the player is in the middle.
        // Anything further off than the map reaches is shown on the rim, in the direction it lies.
        Vector2 ToMap(Vector3 at)
        {
            var spot = middle + new Vector2(at.X - player.X, at.Z - player.Z) / (terrain.HalfSize * ViewShare) * radius;
            var away = spot - middle;
            return away.Length() > radius - 6f ? middle + away.Normalized() * (radius - 6f) : spot;
        }

        // Every other animal of the player's kind: the wild ones first, then its own companions on top.
        var kind = Player.Animal.GetType();
        foreach (var at in Player.Wildlife?.WhereAre(kind) ?? [])
            Dot(ToMap(at), Wild);
        foreach (var at in Player.Companions)
            Dot(ToMap(at), Own);

        // The famous places nearby, each a gold diamond with its name over it, so the player can make for them.
        var font = ThemeDB.FallbackFont;
        foreach (var landmark in terrain.Landmarks)
        {
            var spot = middle + (landmark.Centre - new Vector2(player.X, player.Z)) / (terrain.HalfSize * ViewShare) * radius;
            if (spot.DistanceTo(middle) > radius - 8f)
                continue;
            Diamond(spot);
            var size = font.GetStringSize(landmark.Name, fontSize: LabelFont);
            var label = spot + new Vector2(-size.X / 2f, -10f);
            DrawStringOutline(font, label, landmark.Name, fontSize: LabelFont, size: 4, modulate: Ink);
            DrawString(font, label, landmark.Name, fontSize: LabelFont);
        }

        // The player: an arrow pointing the way it faces.
        Arrow(ToMap(Player.GlobalPosition), Player.Animal.Rotation.Y);

        // North, and a key to the marks, laid out one after another on a dark strip beneath the map, as wide as it
        // needs to be (wider than the little map itself).
        DrawString(font, new Vector2(middle.X - 5f, area.Position.Y + 18f), "N", fontSize: 14, modulate: Ink);
        (string Text, Action<Vector2> Mark)[] entries =
        [
            ("You", at => Arrow(at, 0f)),
            ($"Wild {Plural(Player.Animal.DisplayName.ToLower())}", at => Dot(at, Wild)),
            ("With you", at => Dot(at, Own)),
            ("Famous place", Diamond),
        ];
        float EntryWidth(string text) => 12f + font.GetStringSize(text, fontSize: KeyFont).X + 16f;
        float width = 0f;
        foreach (var (text, _) in entries)
            width += EntryWidth(text);
        var strip = new StyleBoxFlat { BgColor = backing };
        strip.SetCornerRadiusAll(8);
        // The strip lines up with the right of the map, so a long key runs off to the left rather than off the screen.
        float stripWidth = Mathf.Max(side + 16f, width + 8f);
        DrawStyleBox(strip, new Rect2(area.End.X + 8f - stripWidth, area.End.Y + 6f, stripWidth, KeyHeight));
        var key = new Vector2(area.End.X + 8f - stripWidth + 14f, area.Position.Y + side + 6f + KeyHeight / 2f + 5f);
        foreach (var (text, mark) in entries)
        {
            mark(key + new Vector2(0f, -5f));
            DrawString(font, key + new Vector2(12f, 0f), text, fontSize: KeyFont);
            key.X += EntryWidth(text);
        }
    }

    /// <summary>Gap between the map and the right and bottom edges of the screen.</summary>
    private const float Margin = 28f;

    /// <summary>Points round the edge of the round map: enough that it looks smooth.</summary>
    private const int RimPoints = 96;

    /// <summary>Height of the strip under the map that holds the key.</summary>
    private const float KeyHeight = 36f;

    /// <summary>Size of the key's writing.</summary>
    private const int KeyFont = 14;

    /// <summary>Size of the landmarks' names on the map.</summary>
    private const int LabelFont = 13;

    /// <summary>A gold diamond marking a famous place.</summary>
    private void Diamond(Vector2 at)
    {
        Vector2[] outline = [at + new Vector2(0f, -7f), at + new Vector2(7f, 0f), at + new Vector2(0f, 7f), at + new Vector2(-7f, 0f)];
        DrawColoredPolygon(outline, Gold);
        DrawPolyline([.. outline, outline[0]], Ink, 2f);
    }

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

        // Each cave shows as a black mouth at the foot of its mountain, so it can be found again.
        foreach (var cave in terrain.Caves)
        {
            int cx = (int)((cave.Mouth.X + terrain.HalfSize) / step), cy = (int)((cave.Mouth.Y + terrain.HalfSize) / step);
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
                if (cx + dx is >= 0 and < Pixels && cy + dy is >= 0 and < Pixels)
                    image.SetPixel(cx + dx, cy + dy, Ink);
        }
        return ImageTexture.CreateFromImage(image);
    }
}
