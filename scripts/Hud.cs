using Godot;

namespace IceAgeWorld;

/// <summary>
/// On-screen display: the current animal's name, its needs and the player's coins (see <see cref="NeedsPanel"/>), the
/// "Press E to eat/drink" prompt, the chores, messages such as who has joined the game, the map (see
/// <see cref="WorldMap"/>), and, once it has died, the animal shop to choose what to be reborn as.
/// </summary>
public partial class Hud : CanvasLayer
{
    /// <summary>Seconds after dying before the rebirth choice comes up, so the player sees the animal fall.</summary>
    private const float DeathPause = 2f;

    public Player? Player { get; set; }

    /// <summary>The animal shop, which the dead choose what to be reborn as from.</summary>
    public AnimalShop? Shop { get; set; }

    private NeedsPanel _needs = null!;
    private Label _actionPrompt = null!;
    private Label _chores = null!;
    private VBoxContainer _notices = null!;
    private float _deadFor;
    private bool _rebirthOffered;
    private WorldMap _map = null!;

    public override void _Ready()
    {
        _actionPrompt = GetNode<Label>("ActionPrompt");
        _needs = new NeedsPanel { Name = "Needs" };
        AddChild(_needs);
        _map = new WorldMap { Name = "Map" };
        AddChild(_map);

        // The chores that earn coins sit in the top right corner, below the controls line.
        _chores = Text("", 18);
        _chores.HorizontalAlignment = HorizontalAlignment.Right;
        _chores.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        _chores.GrowHorizontal = Control.GrowDirection.Begin;
        _chores.OffsetRight = -20;
        _chores.OffsetTop = 40;
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
        _chores.Text = "Chores\n" + string.Join("\n", Player.Chores);
        _needs.Show(Player);

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
    /// A moment after the player dies, opens the animal shop to choose what to be reborn as: any animal it owns, or one
    /// it can buy with its coins. Until it chooses, the animal lies where it fell.
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
            canClose: false, index => Player.Reborn(index));
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
