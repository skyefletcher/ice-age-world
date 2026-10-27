using Godot;

namespace IceAgeWorld;

/// <summary>
/// The player's needs, gathered in the bottom right corner as in Savannah Life: a big round badge for stamina, with
/// food and water (and air, while diving) in small ones round it, each a gold icon on a dark disc whose rim fills up
/// like a gauge; the health bar along the bottom, and the coins beneath it.
/// </summary>
public partial class NeedsPanel : Control
{
    /// <summary>Below this, a need's badge flashes red to warn the player.</summary>
    private const float LowThreshold = 25f;

    public static readonly Color Gold = new(1f, 0.82f, 0.25f);
    public static readonly Color Warning = new(1f, 0.35f, 0.35f);

    private const float Width = 260f;
    private const float Height = 300f;
    private const float BigSize = 120f;
    private const float SmallSize = 58f;

    /// <summary>The middle of the big stamina badge, which the small ones sit round.</summary>
    private static readonly Vector2 Hub = new(180f, 150f);
    private static readonly Rect2 Bar = new(20f, 240f, 230f, 18f);
    private static readonly Vector2 Coin = new(32f, 281f);

    private Badge _stamina = null!;
    private Badge _food = null!;
    private Badge _water = null!;
    private Badge _air = null!;
    private Label _name = null!;
    private Label _coins = null!;
    private readonly Texture2D _plants = GD.Load<Texture2D>("res://icons/leaf.svg");
    private readonly Texture2D _shellfish = GD.Load<Texture2D>("res://icons/clam.svg");
    private readonly Texture2D _meat = GD.Load<Texture2D>("res://icons/meat.svg");
    private float _health = 100f;

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.BottomRight);
        OffsetLeft = -Width - 16f;
        OffsetTop = -Height - 12f;
        OffsetRight = -16f;
        OffsetBottom = -12f;
        GrowHorizontal = GrowDirection.Begin;
        GrowVertical = GrowDirection.Begin;
        MouseFilter = MouseFilterEnum.Ignore;

        _stamina = Add("res://icons/paw.svg", BigSize, Hub);
        _food = Add("res://icons/meat.svg", SmallSize, Around(-95f));
        _air = Add("res://icons/bubble.svg", SmallSize, Around(-140f));
        _water = Add("res://icons/drop.svg", SmallSize, Around(175f));

        _name = Hud.Text("", 20);
        _name.HorizontalAlignment = HorizontalAlignment.Left;
        _name.Position = new Vector2(Bar.Position.X, Bar.Position.Y - 34f);
        AddChild(_name);

        _coins = Hud.Text("", 24);
        _coins.HorizontalAlignment = HorizontalAlignment.Left;
        _coins.AddThemeColorOverride("font_color", Gold);
        _coins.Position = new Vector2(Coin.X + 18f, Coin.Y - 17f);
        AddChild(_coins);
    }

    /// <summary>A small badge's middle, just clear of the big badge's rim, <paramref name="degrees"/> clockwise from the right.</summary>
    private static Vector2 Around(float degrees) =>
        Hub + Vector2.FromAngle(Mathf.DegToRad(degrees)) * (BigSize / 2f + SmallSize / 2f + 8f);

    private Badge Add(string icon, float size, Vector2 middle)
    {
        var badge = new Badge { Texture = GD.Load<Texture2D>(icon), Diameter = size, Position = middle - Vector2.One * size / 2f };
        AddChild(badge);
        return badge;
    }

    /// <summary>Shows how <paramref name="player"/> is doing; call every frame.</summary>
    public void Show(Player player)
    {
        _name.Text = player.Animal.DisplayName + (player.Animal.Stats.LeaderSize > 1f ? " (pack leader)" : "");
        _coins.Text = Wallet.Coins.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
        _health = player.Health;
        QueueRedraw();

        _food.Texture = FoodIcon(player.Animal.Stats);
        _food.Show(player.Hunger, player.Hunger < LowThreshold);
        _water.Show(player.Thirst, player.Thirst < LowThreshold);
        // Stamina runs low all the time in a chase, so it only warns once the animal is out of breath.
        _stamina.Show(player.Stamina, player.IsExhausted);
        _air.Visible = player.IsDiving || player.Air < 100f;
        _air.Show(player.Air, player.Air < LowThreshold);
    }

    /// <summary>
    /// What the animal eats: a leaf for the grazers, a clam for the otter, which dives for shellfish, and meat for the
    /// hunters.
    /// </summary>
    private Texture2D FoodIcon(AnimalStats stats) =>
        stats.CanGraze ? _plants : stats.Can(Ability.Dive) ? _shellfish : _meat;

    public override void _Draw()
    {
        // The health bar: a dark trough, and a fill that goes from green to yellow to red as it empties, flashing
        // once it is nearly gone.
        var trough = new StyleBoxFlat { BgColor = new Color(0f, 0f, 0f, 0.65f) };
        trough.SetCornerRadiusAll(6);
        DrawStyleBox(trough, Bar.Grow(3f));
        float fraction = Mathf.Clamp(_health / 100f, 0f, 1f);
        if (fraction > 0f)
        {
            var colour = HealthBar.ColourFor(_health);
            if (_health < HealthBar.Low)
                colour = colour.Lerp(Colors.White, Badge.Pulse(_health) * 0.5f);
            var level = new StyleBoxFlat { BgColor = colour };
            level.SetCornerRadiusAll(4);
            DrawStyleBox(level, Bar with { Size = Bar.Size with { X = Mathf.Max(Bar.Size.X * fraction, 8f) } });
        }

        // A gold coin beside the coins.
        DrawCircle(Coin, 12f, new Color(0.45f, 0.3f, 0.02f));
        DrawCircle(Coin, 10f, Gold);
        DrawArc(Coin, 6.5f, 0f, Mathf.Tau, 24, new Color(0.75f, 0.55f, 0.1f), 2f, true);
    }

    /// <summary>
    /// One need: a gold icon on a dark disc. The rim is a gauge that runs clockwise from the top, and the icon itself
    /// fills from the bottom, so either shows how full the need is; both flash red while it is running out.
    /// </summary>
    public partial class Badge : Control
    {
        private TextureRect _icon = null!;
        private float _value = 100f;
        private Color _rim = Gold;

        public float Diameter { get; init; } = 60f;

        public Texture2D? Texture
        {
            get => _icon?.Texture ?? field;
            set
            {
                field = value;
                if (_icon is not null)
                    _icon.Texture = value;
            }
        }

        public override void _Ready()
        {
            Size = Vector2.One * Diameter;
            MouseFilter = MouseFilterEnum.Ignore;
            var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/fill.gdshader") };
            material.SetShaderParameter("gold", true);
            float inset = Diameter * 0.2f;
            _icon = new TextureRect
            {
                Texture = Texture,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                Material = material,
                MouseFilter = MouseFilterEnum.Ignore,
                Position = Vector2.One * inset,
                Size = Vector2.One * (Diameter - inset * 2f),
            };
            AddChild(_icon);
        }

        /// <summary>How strongly a warning flashes right now, 0..1: faster once <paramref name="value"/> runs out.</summary>
        public static float Pulse(float value)
        {
            float speed = value <= 0f ? 12f : 6f;
            return 0.5f + 0.5f * Mathf.Sin(Time.GetTicksMsec() / 1000f * speed);
        }

        /// <summary>Shows <paramref name="value"/> out of 100, flashing red while <paramref name="warn"/> holds.</summary>
        public void Show(float value, bool warn)
        {
            _value = value;
            ((ShaderMaterial)_icon.Material).SetShaderParameter("fill", value / 100f);
            float pulse = warn ? Pulse(value) : 0f;
            _icon.Modulate = Colors.White.Lerp(Warning, pulse);
            _rim = Gold.Lerp(Warning, pulse);
            QueueRedraw();
        }

        public override void _Draw()
        {
            var middle = Size / 2f;
            float radius = Diameter / 2f;
            float rim = Mathf.Max(4f, radius * 0.1f);
            DrawCircle(middle, radius, new Color(0.08f, 0.07f, 0.05f, 0.75f));
            DrawArc(middle, radius - rim / 2f, 0f, Mathf.Tau, 64, _rim with { A = 0.25f }, rim, true);
            float fraction = Mathf.Clamp(_value / 100f, 0f, 1f);
            if (fraction > 0f)
                DrawArc(middle, radius - rim / 2f, -Mathf.Pi / 2f, -Mathf.Pi / 2f + Mathf.Tau * fraction, 64, _rim, rim, true);
        }
    }
}
