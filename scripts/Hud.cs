using Godot;

namespace IceAgeWorld;

/// <summary>
/// On-screen display: the current animal's name and how grown it is, the health, hunger, thirst and stamina bars, the
/// "Press E to ..." prompt, a warning while wolves are hunting the player, the map (see <see cref="WorldMap"/>), and, once
/// it has died, the choice to be reborn.
/// </summary>
public partial class Hud : CanvasLayer
{
    /// <summary>Below this, a need's bar flashes red to warn the player.</summary>
    private const float LowThreshold = 25f;

    /// <summary>Seconds after dying before the rebirth choice comes up, so the player sees the animal fall.</summary>
    private const float DeathPause = 2f;

    private static readonly Color Warning = new(1f, 0.35f, 0.35f);

    public Player? Player { get; set; }

    private ProgressBar _healthBar = null!;
    private StyleBoxFlat _healthFill = null!;
    private ProgressBar _hungerBar = null!;
    private ProgressBar _thirstBar = null!;
    private ProgressBar _staminaBar = null!;
    private Label _actionPrompt = null!;
    private Label _animalName = null!;
    private Control _rebirth = null!;
    private Label _rebirthTitle = null!;
    private float _deadFor;
    private WorldMap _map = null!;

    public override void _Ready()
    {
        _healthBar = GetNode<ProgressBar>("Needs/Health/Bar");
        _healthFill = (StyleBoxFlat)_healthBar.GetThemeStylebox("fill").Duplicate();
        _healthBar.AddThemeStyleboxOverride("fill", _healthFill);
        _hungerBar = GetNode<ProgressBar>("Needs/Hunger/Bar");
        _thirstBar = GetNode<ProgressBar>("Needs/Thirst/Bar");
        _staminaBar = GetNode<ProgressBar>("Needs/Stamina/Bar");
        _actionPrompt = GetNode<Label>("ActionPrompt");
        _animalName = GetNode<Label>("Needs/AnimalName");
        _map = new WorldMap { Name = "Map" };
        AddChild(_map);
    }

    public override void _Process(double delta)
    {
        if (Player is null)
            return;
        _map.Player = Player;

        // A youngster shows how far it has grown, e.g. "Snow leopard cub (40% grown)".
        _animalName.Text = Player.IsGrownUp
            ? Player.Animal.DisplayName + (Player.Animal.Stats.LeaderSize > 1f ? " (pack leader)" : "")
            : $"{Player.Animal.YoungName} ({Mathf.FloorToInt(Player.Age * 100f)}% grown)";
        UpdateBar(_healthBar, Player.Health, Player.Health < HealthBar.Low);
        // Health is coloured like the bars over the wild animals: green, then yellow, then red.
        _healthFill.BgColor = HealthBar.ColourFor(Player.Health);
        UpdateBar(_hungerBar, Player.Hunger, Player.Hunger < LowThreshold);
        UpdateBar(_thirstBar, Player.Thirst, Player.Thirst < LowThreshold);

        // Stamina runs low all the time in a chase, so it only warns once the animal is out of breath.
        UpdateBar(_staminaBar, Player.Stamina, Player.IsExhausted);

        // Anything the animal can do comes first, then any news; otherwise warn it when the polar bear or the wolves
        // are after it.
        string? prompt = Player.ActionPrompt ?? Player.Message;
        if (Player.Courtship is { } courtship)
            prompt = prompt is null ? courtship : courtship + "\n" + prompt;
        bool climber = Player.Animal.Stats.Can(Ability.ClimbTrees);
        if (prompt is null && !Player.IsDead && Player.Wildlife?.BearHuntingPlayer == true)
            prompt = climber ? "The polar bear is after you! Climb a tree!" : "The polar bear is after you! Run!";
        else if (prompt is null && !Player.IsDead && Player.Wildlife?.WolvesHuntingPlayer == true)
            prompt = climber
                ? "The wolf pack is after you! Run, or climb a tree!"
                : "The wolves are after you! Run!";
        _actionPrompt.Visible = prompt is not null;
        _actionPrompt.Text = prompt ?? "";

        UpdateRebirth((float)delta);
    }

    /// <summary>Shows a value, flashing red while <paramref name="warn"/> holds and faster still when it's empty.</summary>
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
    /// A moment after the player dies, offers to be reborn as any of the animals, and frees the mouse to choose.
    /// Until it chooses, the animal lies where it fell.
    /// </summary>
    private void UpdateRebirth(float dt)
    {
        _deadFor = Player!.IsDead ? _deadFor + dt : 0f;
        bool show = _deadFor > DeathPause;
        if (show && _rebirth is null)
            BuildRebirth();
        if (_rebirth is null || _rebirth.Visible == show)
            return;

        _rebirth.Visible = show;
        if (show)
        {
            _rebirthTitle.Text = $"Your {Player.Animal.DisplayName.ToLower()} has died";
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
    }

    private void Reborn(int index)
    {
        Player!.Reborn(index);
        _rebirth.Visible = false;
        _deadFor = 0f;
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    /// <summary>The rebirth screen: a dimmed view, the news, and a button for each animal the player can be born as.</summary>
    private void BuildRebirth()
    {
        var shade = new ColorRect { Color = new Color(0f, 0f, 0f, 0.55f), Visible = false, Name = "Rebirth" };
        shade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(shade);
        _rebirth = shade;

        var centre = new CenterContainer();
        centre.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        shade.AddChild(centre);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 18);
        centre.AddChild(column);

        _rebirthTitle = Text("", 40);
        column.AddChild(_rebirthTitle);
        column.AddChild(Text("Do you want to be reborn? Choose what to be born as:", 22));

        // A button for each animal, wrapping onto a second row when there are too many for one.
        var buttons = new HFlowContainer { Alignment = FlowContainer.AlignmentMode.Center, CustomMinimumSize = new Vector2(680, 0) };
        buttons.AddThemeConstantOverride("h_separation", 12);
        buttons.AddThemeConstantOverride("v_separation", 12);
        column.AddChild(buttons);
        var names = Player!.AnimalNames;
        for (int i = 0; i < names.Length; i++)
        {
            int index = i;
            var button = new Button { Text = names[i], CustomMinimumSize = new Vector2(150, 48) };
            button.AddThemeFontSizeOverride("font_size", 20);
            button.Pressed += () => Reborn(index);
            buttons.AddChild(button);
        }
    }

    private static Label Text(string text, int size)
    {
        var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", 6);
        return label;
    }
}
