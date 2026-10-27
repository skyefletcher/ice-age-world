using Godot;

namespace IceAgeWorld;

/// <summary>
/// On-screen display: the current animal's sex and name and how grown it is, the health, food, water and stamina bars
/// (and breath, while diving), the "Press E to eat/drink" prompt, the player's coins and chores, messages such as who has
/// joined the game, the map (see <see cref="WorldMap"/>), and, once it has died, the animal shop to choose what to be
/// reborn as.
/// </summary>
public partial class Hud : CanvasLayer
{
    /// <summary>Below this, a need's bar flashes red to warn the player.</summary>
    private const float LowThreshold = 25f;

    /// <summary>Seconds after dying before the rebirth choice comes up, so the player sees the animal fall.</summary>
    private const float DeathPause = 2f;

    private static readonly Color Warning = new(1f, 0.35f, 0.35f);
    private static readonly Color Gold = new(1f, 0.85f, 0.3f);

    public Player? Player { get; set; }

    /// <summary>The animal shop, which the dead choose what to be reborn as from.</summary>
    public AnimalShop? Shop { get; set; }

    private ProgressBar _healthBar = null!;
    private StyleBoxFlat _healthFill = null!;
    private ProgressBar _hungerBar = null!;
    private ProgressBar _thirstBar = null!;
    private ProgressBar _staminaBar = null!;
    private Control _breathRow = null!;
    private ProgressBar _breathBar = null!;

    // The food bar's icon shows what the animal eats.
    private TextureRect _foodIcon = null!;
    private readonly Texture2D _plants = GD.Load<Texture2D>("res://icons/leaf.svg");
    private readonly Texture2D _shellfish = GD.Load<Texture2D>("res://icons/clam.svg");
    private readonly Texture2D _meat = GD.Load<Texture2D>("res://icons/meat.svg");

    private Label _actionPrompt = null!;
    private Label _animalName = null!;
    private Label _coins = null!;
    private Label _chores = null!;
    private VBoxContainer _notices = null!;
    private float _deadFor;
    private bool _rebirthOffered;
    private WorldMap _map = null!;

    public override void _Ready()
    {
        _healthBar = GetNode<ProgressBar>("Needs/Health/Bar");
        _healthFill = (StyleBoxFlat)_healthBar.GetThemeStylebox("fill").Duplicate();
        _healthBar.AddThemeStyleboxOverride("fill", _healthFill);
        _hungerBar = GetNode<ProgressBar>("Needs/Hunger/Bar");
        _thirstBar = GetNode<ProgressBar>("Needs/Thirst/Bar");
        _staminaBar = GetNode<ProgressBar>("Needs/Stamina/Bar");
        _breathRow = GetNode<Control>("Needs/Breath");
        _breathBar = GetNode<ProgressBar>("Needs/Breath/Bar");
        _foodIcon = GetNode<TextureRect>("Needs/Hunger/Icon");
        _actionPrompt = GetNode<Label>("ActionPrompt");
        _animalName = GetNode<Label>("Needs/AnimalName");
        _map = new WorldMap { Name = "Map" };
        AddChild(_map);

        // Coins sit in the top right corner, below the controls line, as in Savannah Life, where they're always in view.
        _coins = Text("", 26);
        _coins.AddThemeColorOverride("font_color", Gold);
        _coins.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        _coins.GrowHorizontal = Control.GrowDirection.Begin;
        _coins.OffsetRight = -20;
        _coins.OffsetTop = 40;
        AddChild(_coins);

        // The chores that earn those coins, listed underneath.
        _chores = Text("", 18);
        _chores.HorizontalAlignment = HorizontalAlignment.Right;
        _chores.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        _chores.GrowHorizontal = Control.GrowDirection.Begin;
        _chores.OffsetRight = -20;
        _chores.OffsetTop = 80;
        AddChild(_chores);

        // Messages stack down from the top middle, e.g. who has joined the game.
        _notices = new VBoxContainer();
        _notices.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        _notices.GrowHorizontal = Control.GrowDirection.Both;
        _notices.OffsetTop = 80;
        AddChild(_notices);
    }

    public override void _Process(double delta)
    {
        if (Player is null)
            return;
        _map.Player = Player;
        _coins.Text = $"Coins: {Wallet.Coins}";
        _chores.Text = "Chores\n" + string.Join("\n", Player.Chores);

        // A youngster shows how far it has grown, e.g. "Female snow leopard cub (40% grown)".
        _animalName.Text = Player.Sex + " " + (Player.IsGrownUp
            ? Player.Animal.DisplayName.ToLower() + (Player.Animal.Stats.LeaderSize > 1f ? " (pack leader)" : "")
            : $"{Player.Animal.YoungName.ToLower()} ({Mathf.FloorToInt(Player.Age * 100f)}% grown)");
        UpdateBar(_healthBar, Player.Health, Player.Health < HealthBar.Low);
        // Health is coloured like the bars over the wild animals: green, then yellow, then red.
        _healthFill.BgColor = HealthBar.ColourFor(Player.Health);
        _foodIcon.Texture = FoodIcon(Player.Animal.Stats);
        UpdateBar(_hungerBar, Player.Hunger, Player.Hunger < LowThreshold);
        UpdateBar(_thirstBar, Player.Thirst, Player.Thirst < LowThreshold);

        // Stamina runs low all the time in a chase, so it only warns once the animal is out of breath.
        UpdateBar(_staminaBar, Player.Stamina, Player.IsExhausted);

        // Breath only matters to a diver, so it shows while it is under, and until it has its breath back.
        _breathRow.Visible = Player.IsDiving || Player.Breath < 100f;
        UpdateBar(_breathBar, Player.Breath, Player.Breath < LowThreshold);

        // The prompt says what can be done right here (eat, drink, pick something up or put it down), and how pairing
        // up with a wild snow leopard is going.
        string? prompt = (Player.ActionPrompt, Player.CourtshipPrompt) switch
        {
            ({ } action, { } courtship) => $"{courtship}\n{action}",
            var (action, courtship) => action ?? courtship,
        };
        _actionPrompt.Visible = prompt is not null;
        _actionPrompt.Text = prompt ?? "";

        UpdateRebirth((float)delta);
    }

    /// <summary>
    /// Shows a message across the top of the screen for a few seconds, then fades it out. Several at once stack up,
    /// newest at the bottom.
    /// </summary>
    public void Notify(string message, float seconds = 6f)
    {
        var label = Text(message, 22);
        _notices.AddChild(label);
        var fade = label.CreateTween();
        fade.TweenInterval(seconds);
        fade.TweenProperty(label, "modulate:a", 0f, 1f);
        fade.TweenCallback(Callable.From(label.QueueFree));
    }

    /// <summary>
    /// What the animal eats: a leaf for the grazers, meat for the hunters (the polar bear too, though it dives), and a
    /// clam for the otter, which dives for shellfish.
    /// </summary>
    private Texture2D FoodIcon(AnimalStats stats) =>
        stats.CanGraze ? _plants : stats.Can(Ability.Dive) && !stats.Can(Ability.Hunt) ? _shellfish : _meat;

    /// <summary>
    /// Fills a bar to a value out of 100, flashing it red while <paramref name="warn"/> holds and faster still when
    /// it's empty.
    /// </summary>
    private static void UpdateBar(ProgressBar bar, float value, bool warn)
    {
        bar.Value = value;

        if (!warn)
        {
            bar.Modulate = Colors.White;
            return;
        }

        float speed = value <= 0f ? 12f : 6f;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.GetTicksMsec() / 1000f * speed);
        bar.Modulate = Colors.White.Lerp(Warning, pulse);
    }

    /// <summary>
    /// A moment after the player dies, opens the animal shop to choose what to be reborn as: any animal it owns, or one
    /// it can buy with its coins. It is born a newborn of the same sex as the one that died (V changes it afterwards).
    /// Until it chooses, the animal lies where it fell.
    /// </summary>
    private void UpdateRebirth(float dt)
    {
        _deadFor = Player!.IsDead ? _deadFor + dt : 0f;
        if (!Player.IsDead)
            _rebirthOffered = false;
        if (_deadFor <= DeathPause || _rebirthOffered || Shop is null)
            return;

        _rebirthOffered = true;
        Shop.Open($"Your {Player.Animal.DisplayName.ToLower()} has died", "Do you want to be reborn? Choose what to be born as:",
            canClose: false, index => Player.Reborn(index, Player.Sex));
    }

    /// <summary>A centred line of outlined text, for the HUD and the menus over it.</summary>
    public static Label Text(string text, int size)
    {
        var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", 6);
        return label;
    }
}
