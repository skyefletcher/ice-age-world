using Godot;

namespace IceAgeWorld;

/// <summary>
/// The snow leopard's special attack. Close to prey, G (or a right-click) leaps onto it and clamps the jaws on, as big
/// cats seize the throat or the back of the neck and hang on. While it holds on, the cat rides along at the prey's
/// side and does far more damage than biting and letting go, and the prey can only stagger along, dragging it. Holding
/// on is hard work: it drains stamina, and once the cat is spent it has to let go. Space or G lets go sooner.
/// </summary>
public partial class Player
{
    /// <summary>
    /// Damage a second the clamped jaws do to prey of unit bulk, against <see cref="BiteStrength"/> for a single bite:
    /// enough to bring down a wolf in a moment, a mammoth calf in a few seconds and even a grown mammoth, given time.
    /// </summary>
    [Export] public float GripStrength { get; set; } = 30f;

    /// <summary>The first bite as the cat lands on its prey, as a share of an ordinary bite.</summary>
    [Export] public float GripLandingBite { get; set; } = 1.5f;

    /// <summary>How hard holding on is, as a share of running flat out.</summary>
    [Export] public float GripEffort { get; set; } = 0.6f;

    /// <summary>True while the cat has its jaws clamped on prey.</summary>
    public bool IsGripping => _gripping is not null;

    /// <summary>The animal the cat is holding on to, if any.</summary>
    private Animal? _gripping;

    /// <summary>Which side of the prey the cat hangs on, as a level direction out from the prey's middle.</summary>
    private Vector3 _gripSide;

    /// <summary>Leaps onto the prey and clamps on, landing a hard first bite.</summary>
    private void StartGrip(Animal prey)
    {
        var side = (GlobalPosition - prey.GlobalPosition) with { Y = 0f };
        _gripSide = side.LengthSquared() > 0.0001f ? side.Normalized() : Vector3.Back;
        _gripping = prey;
        _lunge = Vector3.Zero;
        Stamina = Mathf.Max(0f, Stamina - PounceStamina);
        Wildlife!.Grip(prey, true);
        if (Wildlife.Bite(prey, BiteStrength * GripLandingBite * Size * Size, GlobalPosition))
            LetGo();
        else if (_herds.TryGetValue(Animal, out var family) && family.Attack(prey))
            Announce($"Your {family.Word} joins the attack on the {prey.DisplayName.ToLower()}!", 3f);
    }

    /// <summary>
    /// Hangs on to the prey: rides along at its side, jaws clamped, doing damage every moment, until it falls, the cat
    /// tires, or the player lets go.
    /// </summary>
    private void HoldOn(float dt)
    {
        var prey = _gripping!;
        string name = prey.DisplayName.ToLower();

        if (Input.IsActionJustPressed(InputSetup.Grip) || Input.IsActionJustPressed(InputSetup.Jump) || IsExhausted)
        {
            LetGo();
            return;
        }

        UpdateNeeds(dt, exerting: true);
        UpdateStamina(dt, effort: GripEffort);
        ActionPrompt = $"Holding on to the {name}! G or Space to let go";

        // The more of the cat there is, the harder its jaws clamp, as with a bite.
        if (Wildlife!.Bite(prey, GripStrength * Size * Size * dt, GlobalPosition))
        {
            LetGo();
            return;
        }

        // Ride along pressed against the prey's flank, facing in to it, on the ground or the water beside it.
        float reach = prey.Stats.BodyRadius * Wildlife!.SizeOf(prey) + Stats.BodyRadius * 0.6f;
        var spot = prey.GlobalPosition + _gripSide * reach;
        float ground = Terrain?.GroundBelow(spot) ?? spot.Y;
        float? surface = Water?.SurfaceAt(spot);
        float height = surface.HasValue && surface.Value - ground > Stats.FloatDepth ? surface.Value - Stats.FloatDepth : ground;
        var moved = (spot with { Y = height }) - GlobalPosition;
        GlobalPosition = spot with { Y = height };
        Velocity = Vector3.Zero;
        Animal.Rotation = new Vector3(0, Yaw(-_gripSide), 0);

        // Head down and jaws locked, legs scrabbling to keep up as the prey drags it along.
        float speed = (moved with { Y = 0f }).Length() / Mathf.Max(dt, 0.001f);
        Animal.IsSwimming = false;
        Animal.Animate(speed, Mathf.Clamp(speed / Stats.WalkSpeed, 0f, 1f), 0.9f, dt);
    }

    /// <summary>Lets go of the prey, if the cat has hold of any.</summary>
    private void LetGo()
    {
        if (_gripping is not null)
            Wildlife?.Grip(_gripping, false);
        _gripping = null;
    }
}
