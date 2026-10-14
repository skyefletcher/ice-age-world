using Godot;

namespace IceAgeWorld;

/// <summary>
/// The eagle's special attack. Skimming low over small prey, G seizes it alive in the talons and carries it up into
/// the air, kicking, the way golden eagles take hares and foxes. G again lets go, and it falls: dropped from high
/// enough, the fall kills it, as eagles drop tortoises and bones onto rocks. Carrying the extra weight is hard work:
/// it drains stamina even while gliding, the bird climbs more slowly, and once it is spent it has to let go. Landing
/// sets the prey down unhurt.
/// </summary>
public partial class Player
{
    /// <summary>
    /// How heavy an animal the eagle can lift alive, compared with its own bulk: a hare, an otter or a youngster, but
    /// not a grown wolf or reindeer, which are far too heavy to carry off kicking.
    /// </summary>
    [Export] public float LiftStrength { get; set; } = 0.4f;

    /// <summary>How hard carrying live prey is, as a share of flapping flat out, on top of any flapping.</summary>
    [Export] public float LiftEffort { get; set; } = 0.8f;

    /// <summary>How fast the bird can climb with prey in its talons, as a share of its usual climb.</summary>
    private const float LiftClimb = 0.55f;

    /// <summary>True while the bird has a living animal in its talons.</summary>
    public bool IsLifting => _lifting is not null;

    /// <summary>The living animal the bird is carrying, if any.</summary>
    private Animal? _lifting;

    /// <summary>Whether the bird can seize <paramref name="prey"/> alive and lift it.</summary>
    private bool CanLift(Animal prey) =>
        Wildlife?.CanLift(prey, Stats.BodyRadius * Stats.BodyHeight * LiftStrength * Size * Size) == true;

    /// <summary>Seizes the prey in the talons and starts carrying it up, at a cost in stamina for the grab.</summary>
    private void StartLift(Animal prey)
    {
        _lifting = prey;
        Stamina = Mathf.Max(0f, Stamina - PounceStamina);
    }

    /// <summary>
    /// While carrying live prey: keeps it in the talons, and lets go when asked, once the bird is spent, or if the prey
    /// dies in its grip.
    /// </summary>
    private void Lifting()
    {
        var prey = _lifting!;
        if (Wildlife?.IsAlive(prey) != true)
        {
            _lifting = null;
            return;
        }

        if (Input.IsActionJustPressed(InputSetup.Grip) || IsExhausted)
        {
            DropLifted();
            return;
        }
        ActionPrompt = $"G to drop the {prey.DisplayName.ToLower()}";
        Wildlife.Lift(prey, Talons, Animal.GlobalRotation.Y);
    }

    /// <summary>Where a flying bird's talons close on what it carries: between its feet, or its middle for any other flier.</summary>
    private Vector3 Talons => Animal is BaldEagle eagle ? eagle.Talons : GlobalPosition;

    /// <summary>Lets go of the prey in the talons, if any: it falls from wherever the bird is.</summary>
    private void DropLifted()
    {
        if (_lifting is null)
            return;
        Wildlife?.Lift(_lifting, null);
        _lifting = null;
    }
}
