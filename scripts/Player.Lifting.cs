using Godot;

namespace IceAgeWorld;

/// <summary>
/// The eagle's special attack. Skimming low over small prey, G seizes it alive in the talons and carries it up into
/// the air, kicking, the way golden eagles take hares and foxes. G again lets go, and it falls: dropped from high
/// enough, the fall kills it, as eagles drop tortoises and bones onto rocks. Carrying the extra weight is hard work:
/// it drains stamina even while gliding, the bird climbs more slowly, and once it is spent it has to let go. Landing
/// sets the prey down unhurt. Standing beside small prey on the ground, G seizes it and takes off with it.
///
/// Anything too heavy to lift gets the talons instead: G slashes at it, in the air or springing up off the ground,
/// raking it with both feet. That strikes hard and leaves a deep wound that bleeds for a while after, as eagles strike
/// at deer and foxes too big to carry off, and owls rake anything that comes near their nest.
/// </summary>
public partial class Player
{
    /// <summary>How hard a talon slash strikes, compared with a hunter's bite.</summary>
    [Export] public float SlashStrength { get; set; } = 1.5f;

    /// <summary>How badly the wound a slash leaves bleeds, compared with one from a snow leopard's clamped jaws.</summary>
    [Export] public float SlashBleed { get; set; } = 1f;

    /// <summary>When the last talon slash began, in milliseconds, so the next waits for the feet to come back.</summary>
    private ulong _lastSlash;

    /// <summary>
    /// The bird's special attack on <paramref name="prey"/>: seizes it alive if it is light enough to lift, otherwise
    /// slashes at it with the talons. In the air the bird carries on flying; on the ground it takes off with prey it
    /// seizes. With no prey it slashes at the air. Does nothing while the bird is spent or its feet are still coming
    /// back from the last slash.
    /// </summary>
    private void TalonAttack(Animal? prey)
    {
        if (IsExhausted || Time.GetTicksMsec() - _lastSlash < (ulong)(Bird.SlashSeconds * 1000f))
            return;

        if (prey is not null && CanLift(prey))
        {
            if (!Animal.IsFlying)
                TakeOff((prey.GlobalPosition - GlobalPosition) with { Y = 0f });
            StartLift(prey);
            return;
        }

        _lastSlash = Time.GetTicksMsec();
        (Animal as Bird)?.Slash();
        Stamina = Mathf.Max(0f, Stamina - PounceStamina * 1.5f);
        if (Stamina <= 0f)
            IsExhausted = true;

        // On the ground the bird springs at its target, wings flared, to reach it with its feet.
        if (!Animal.IsFlying)
        {
            var toward = prey is null ? Forward(Animal.Rotation.Y) : (prey.GlobalPosition - GlobalPosition) with { Y = 0f };
            if (toward.LengthSquared() > 0.0001f)
            {
                toward = toward.Normalized();
                Animal.Rotation = new Vector3(0, Yaw(toward), 0);
                _lunge = toward * Stats.JumpBoost * 0.5f;
            }
        }

        // Unless the strike killed it, the talons leave it bleeding, and the mate and grown young stoop in to help.
        if (prey is not null && !Wildlife!.Bite(prey, BiteStrength * SlashStrength * Size * Size, GlobalPosition))
        {
            Wildlife.Bleed(prey, SlashBleed * Size * Size);
            if (_herds.TryGetValue(Animal, out var family))
                family.Attack(prey);
        }
    }
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
    private Vector3 Talons => Animal is Bird bird ? bird.Talons : GlobalPosition;

    /// <summary>Lets go of the prey in the talons, if any: it falls from wherever the bird is.</summary>
    private void DropLifted()
    {
        if (_lifting is null)
            return;
        Wildlife?.Lift(_lifting, null);
        _lifting = null;
    }
}
