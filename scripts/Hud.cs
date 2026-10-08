using Godot;

namespace IceAgeWorld;

/// <summary>On-screen display: the current animal's name, the hunger, thirst and stamina bars and the "Press E to ..." prompt.</summary>
public partial class Hud : CanvasLayer
{
    /// <summary>Below this, a need's bar flashes red to warn the player.</summary>
    private const float LowThreshold = 25f;

    private static readonly Color Warning = new(1f, 0.35f, 0.35f);

    public Player? Player { get; set; }

    private ProgressBar _hungerBar = null!;
    private ProgressBar _thirstBar = null!;
    private ProgressBar _staminaBar = null!;
    private Label _actionPrompt = null!;
    private Label _animalName = null!;

    public override void _Ready()
    {
        _hungerBar = GetNode<ProgressBar>("Needs/Hunger/Bar");
        _thirstBar = GetNode<ProgressBar>("Needs/Thirst/Bar");
        _staminaBar = GetNode<ProgressBar>("Needs/Stamina/Bar");
        _actionPrompt = GetNode<Label>("ActionPrompt");
        _animalName = GetNode<Label>("Needs/AnimalName");
    }

    public override void _Process(double delta)
    {
        if (Player is null)
            return;

        _animalName.Text = Player.Animal.DisplayName;
        UpdateBar(_hungerBar, Player.Hunger, Player.Hunger < LowThreshold);
        UpdateBar(_thirstBar, Player.Thirst, Player.Thirst < LowThreshold);

        // Stamina runs low all the time in a chase, so it only warns once the animal is out of breath.
        UpdateBar(_staminaBar, Player.Stamina, Player.IsExhausted);

        _actionPrompt.Visible = Player.ActionPrompt is not null;
        _actionPrompt.Text = Player.ActionPrompt ?? "";
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
}
