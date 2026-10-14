using Godot;

namespace IceAgeWorld;

/// <summary>
/// Diving, for animals that dive: the sea otter. Swimming at the surface, Shift takes it under. Down there Shift swims
/// deeper and Space swims up, and with neither held it drifts slowly up, buoyed by the air trapped in its thick fur.
/// It holds its breath while it's under. Once the air runs out it starts to drown, so it has to come up for more. On
/// the bed it finds sea urchins and clams. It picks one up with E and carries it to the surface, where it eats it
/// floating on its back, as sea otters do, using the belly as a table.
/// </summary>
public partial class Player
{
    /// <summary>Seconds the animal can stay under on one breath: sea otters usually dive for about a minute.</summary>
    [Export] public float BreathSeconds { get; set; } = 60f;

    /// <summary>Seconds at the surface to get a full breath back.</summary>
    private const float BreatheInSeconds = 3f;

    /// <summary>Health lost each second once out of air.</summary>
    private const float DrownDamage = 12f;

    /// <summary>How fast the animal drifts up when not swimming down, in metres a second.</summary>
    private const float Buoyancy = 0.8f;

    /// <summary>How close to the mouth an urchin or clam must be to pick it up.</summary>
    private const float ForageReach = 0.9f;

    /// <summary>How much air is left in the lungs, out of 100.</summary>
    public float Air { get; private set; } = 100f;

    public bool IsDiving => _mode == Mode.Diving;

    /// <summary>The world under the lakes, with its urchins and clams.</summary>
    public Underwater? Underwater { get; set; }

    /// <summary>The urchin or clam the animal is carrying in its paws, and the model of it there.</summary>
    private Underwater.ShellKind? _holding;
    private Node3D? _heldModel;

    /// <summary>
    /// Swimming at the surface: offers to dive, and goes under when Shift is held. Returns true once diving. Also eats
    /// anything carried up from the bed.
    /// </summary>
    private bool TryDive()
    {
        EatHeld();
        if (!Stats.Can(Ability.Dive) || IsFeeding)
            return false;
        ActionPrompt ??= "Shift to dive";
        if (!Input.IsActionPressed(InputSetup.Sprint) || Air < 10f)
            return false;
        _mode = Mode.Diving;
        Animal.IsDiving = true;
        return true;
    }

    /// <summary>Swims about under the water, steered by the camera, Shift to go deeper and Space to go up.</summary>
    private void Dive(float dt, Vector3 direction)
    {
        float? surface = Water?.SurfaceAt(GlobalPosition);
        if (surface is null)
        {
            EndDive();
            return;
        }

        bool down = Input.IsActionPressed(InputSetup.Sprint);
        bool up = Input.IsActionPressed(InputSetup.Jump);
        float speed = Stats.SwimSpeed * (IsExhausted ? 0.6f : 1f);
        float climb = down ? -speed * 0.8f : up ? speed * 0.8f : Buoyancy;

        var velocity = Velocity;
        var horizontal = new Vector3(velocity.X, 0, velocity.Z).Lerp(direction * speed, Mathf.Min(1f, Stats.Acceleration * 0.5f * dt));
        velocity = horizontal with { Y = Mathf.MoveToward(velocity.Y, climb, Stats.Acceleration * dt) };
        Velocity = velocity;
        MoveAndSlide();

        // Back at the surface, it swims on top again, unless it's still heading down.
        float top = surface.Value - Stats.FloatDepth;
        if (GlobalPosition.Y >= top && !down)
        {
            EndDive();
            return;
        }
        if (GlobalPosition.Y > top)
            GlobalPosition = GlobalPosition with { Y = top };

        IsSwimming = true;
        UpdateNeeds(dt, exerting: true);
        UpdateStamina(dt, effort: Stats.SwimEffort);
        Forage();

        // Turn to the way it swims, and tip nose-down to dive and nose-up to climb.
        float yaw = direction != Vector3.Zero
            ? Mathf.LerpAngle(Animal.Rotation.Y, Yaw(direction), Stats.TurnSpeed * dt)
            : Animal.Rotation.Y;
        // Drifting up on its own buoyancy it stays nearly level; only swimming up or down tips it right over.
        float pitch = Mathf.Clamp(Mathf.Atan2(velocity.Y, Mathf.Max(horizontal.Length(), 2f)), -1.2f, 1.2f);
        Animal.Rotation = new Vector3(Mathf.LerpAngle(Animal.Rotation.X, pitch, Mathf.Min(1f, 4f * dt)), yaw, 0);
        Animal.IsSwimming = true;
        AnimateAnimal(velocity.Length() + 2f, 1f, 0f, dt);
    }

    /// <summary>Comes back up to swim at the surface, level again.</summary>
    private void EndDive()
    {
        _mode = Mode.Ground;
        Animal.IsDiving = false;
        Animal.Rotation = new Vector3(0, Animal.Rotation.Y, 0);
        Velocity = Velocity with { Y = 0f };
    }

    /// <summary>Uses up air while under the water and drowns once it's gone; breathes back in above it.</summary>
    private void Breathe(float dt)
    {
        if (!IsDiving)
        {
            Air = Mathf.Min(100f, Air + 100f / BreatheInSeconds * dt);
            return;
        }
        Air = Mathf.Max(0f, Air - 100f / BreathSeconds * dt);
        if (Air > 0f)
            return;
        Health = Mathf.Max(0f, Health - DrownDamage * dt);
        // Drowning stops it healing, as a bite does.
        _sinceBitten = 0f;
        if (IsDead)
            Die();
    }

    /// <summary>On the bed: picks up an urchin or clam within reach with E.</summary>
    private void Forage()
    {
        if (_holding is { } held)
        {
            ActionPrompt = $"Swim up to eat the {Underwater.NameOf(held)}";
            return;
        }
        if (Underwater?.ShellfishNear(Mouth, ForageReach) is not { } found)
            return;
        ActionPrompt = $"E or click to pick up the {found.Name}";
        if (!Input.IsActionJustPressed(InputSetup.Eat))
            return;

        Underwater.Take(found);
        _holding = found.Kind;
        // Clutched between the forepaws, as an otter carries food up from the bottom. It hangs off the left paw, halfway
        // across to the right one, which always moves with it, so the food goes wherever the paws do: tucked to the
        // chest swimming down, and held up over the belly, ready to eat, when the otter rolls over to float.
        _heldModel = Underwater.Model(found.Kind);
        if (Animal.GetNodeOrNull<Node3D>("Frame/LegFrontLeft/Foot") is { } paw)
        {
            float between = -paw.GetParent<Node3D>().Position.X;
            // A big urchin is wider than the otter's head; it carries a smaller one, pressed into both paws.
            _heldModel.Scale = Vector3.One * 0.6f;
            _heldModel.Position = new Vector3(between, -0.01f, -0.02f);
            paw.AddChild(_heldModel);
        }
        else
        {
            _heldModel.Position = new Vector3(0, Stats.BodyHeight * 0.4f, -Stats.MouthDistance * 0.6f);
            Animal.AddChild(_heldModel);
        }
    }

    /// <summary>At the surface, eats the urchin or clam it carried up, with E.</summary>
    private void EatHeld()
    {
        if (_holding is not { } held)
            return;
        ActionPrompt = $"E or click to eat the {Underwater.NameOf(held)}";
        if (!Input.IsActionJustPressed(InputSetup.Eat))
            return;
        Hunger = Mathf.Min(100f, Hunger + Underwater.FoodIn(held));
        DropHeld();
        DoChore(ChoreKind.Shellfish, 1f);
    }

    /// <summary>Lets go of anything in the paws, e.g. on switching animal or dying.</summary>
    private void DropHeld()
    {
        _heldModel?.QueueFree();
        _heldModel = null;
        _holding = null;
    }
}
