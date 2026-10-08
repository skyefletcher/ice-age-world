using Godot;

namespace IceAgeWorld;

/// <summary>On-screen display: the hunger and thirst bars and the "Press E to ..." prompt.</summary>
public partial class Hud : CanvasLayer
{
    /// <summary>Below this, a bar flashes red to warn the player.</summary>
    private const float LowThreshold = 25f;

    private static readonly Color Warning = new(1f, 0.35f, 0.35f);

    public Player? Player { get; set; }

    private ProgressBar _hungerBar = null!;
    private ProgressBar _thirstBar = null!;
    private Label _actionPrompt = null!;

    public override void _Ready()
    {
        _hungerBar = GetNode<ProgressBar>("Needs/Hunger/Bar");
        _thirstBar = GetNode<ProgressBar>("Needs/Thirst/Bar");
        _actionPrompt = GetNode<Label>("ActionPrompt");
    }

    public override void _Process(double delta)
    {
        if (Player is null)
            return;

        UpdateBar(_hungerBar, Player.Hunger);
        UpdateBar(_thirstBar, Player.Thirst);

        _actionPrompt.Visible = Player.ActionPrompt is not null;
        _actionPrompt.Text = Player.ActionPrompt ?? "";
    }

    /// <summary>Shows a need's value, flashing red when it's low and faster still when it's empty.</summary>
    private static void UpdateBar(ProgressBar bar, float value)
    {
        bar.Value = value;

        if (value >= LowThreshold)
        {
            bar.Modulate = Colors.White;
            return;
        }

        float speed = value <= 0f ? 12f : 6f;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.GetTicksMsec() / 1000f * speed);
        bar.Modulate = Colors.White.Lerp(Warning, pulse);
    }
}
