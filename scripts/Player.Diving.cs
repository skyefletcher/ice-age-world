using Godot;

namespace IceAgeWorld;

/// <summary>
/// Diving, for animals that dive (<see cref="Ability.Dive"/>: the sea otter and polar bear). From the surface, holding
/// Shift takes the animal down under the water, Space brings it up, and let be it drifts back up, buoyed by the air in
/// its fur and lungs. It holds its breath for as long as its kind can (<see cref="AnimalStats.BreathSeconds"/>), and
/// once that runs out it starts to drown, so it has to come up for more. In the ocean it swims through the kelp and over
/// the reefs (see <see cref="Seabed"/>), and in the lakes over the waterweed (see <see cref="Underwater"/>). On the bed
/// it finds sea urchins and clams: it picks one up with E and carries it to the surface, where it eats it floating on
/// its back, as sea otters do, using the belly as a table. A fish close enough it snaps up there and then.
/// </summary>
public partial class Player
{
    /// <summary>Health lost each second once the breath has run out.</summary>
    [Export] public float DrownDamagePerSecond { get; set; } = 8f;

    /// <summary>Seconds at the surface to get a full breath back after using it all up.</summary>
    private const float BreathRecoverySeconds = 4f;

    /// <summary>How fast a diver drifts up when it isn't swimming either way, buoyed by its fur and lungs.</summary>
    private const float BuoyantRise = 0.6f;

    /// <summary>How steeply a diver's body tips to swim down or up, at most.</summary>
    private const float DivePitch = 0.9f;

    /// <summary>The ocean floor, with its urchins, clams and schools of cod.</summary>
    public Seabed? Seabed { get; set; }

    /// <summary>The beds of the small lakes, with their urchins and clams.</summary>
    public Underwater? Underwater { get; set; }

    /// <summary>True while the animal is under the water, rather than floating on it.</summary>
    public bool IsDiving { get; private set; }

    /// <summary>Breath held, from 100 (a full lungful) down to 0 (drowning). Only used up underwater.</summary>
    public float Breath { get; private set; } = 100f;

    /// <summary>The urchin or clam the animal is carrying in its paws, and the model of it there.</summary>
    private Underwater.ShellKind? _holding;
    private Node3D? _heldModel;

    private bool CanDive => Stats.Can(Ability.Dive) && Stats.BreathSeconds > 0f;

    /// <summary>
    /// Swimming at the surface: eats anything carried up from the bed, offers to dive where the water is deep enough to
    /// dive into, and says whether to go under now, with Shift held.
    /// </summary>
    private bool StartsDive(float surface)
    {
        EatHeld();
        if (!CanDive || IsFeeding || Terrain is null)
            return false;
        float bottom = Terrain.GetHeight(GlobalPosition.X, GlobalPosition.Z);
        if (surface - bottom <= Stats.BodyHeight * 2f + Stats.FloatDepth)
            return false;
        ActionPrompt ??= "Shift to dive";
        return Input.IsActionPressed(InputSetup.Sprint) && Breath >= 10f;
    }

    /// <summary>Breath comes back while the animal's head is above water.</summary>
    private void Breathe(float dt) => Breath = Mathf.MoveToward(Breath, 100f, 100f / BreathRecoverySeconds * dt);

    /// <summary>Comes back to the surface, or stops diving for any other reason, levelling out the body.</summary>
    private void StopDiving()
    {
        if (IsDiving)
            Velocity = Velocity with { Y = 0f };
        IsDiving = false;
        Animal.IsDiving = false;
        Animal.Rotation = new Vector3(0f, Animal.Rotation.Y, 0f);
    }

    /// <summary>One step of swimming underwater. Called instead of walking for as long as the animal is down.</summary>
    private void Dive(float dt, Vector3 direction, float? surface)
    {
        IsDiving = true;
        IsSwimming = true;
        Animal.IsSwimming = Animal.IsDiving = true;

        bool down = Input.IsActionPressed(InputSetup.Sprint);
        bool up = Input.IsActionPressed(InputSetup.Jump);
        float speed = Stats.SwimSpeed * (IsExhausted ? 0.6f : 1f);
        float rise = down ? -speed * 0.7f : up ? speed * 0.7f : BuoyantRise;

        var velocity = Velocity;
        var horizontal = new Vector3(velocity.X, 0f, velocity.Z).Lerp(direction * speed, Mathf.Min(1f, Stats.Acceleration * 0.5f * dt));
        velocity = new Vector3(horizontal.X, Mathf.Lerp(velocity.Y, rise, Mathf.Min(1f, 3f * dt)), horizontal.Z);
        Velocity = velocity;
        MoveAndSlide();

        // Back at floating height, the animal comes up for air, unless it is still being taken down.
        if (surface is not { } top || (!down && GlobalPosition.Y >= top - Stats.FloatDepth))
        {
            StopDiving();
            return;
        }

        // Holding its breath costs it, and once it has none left it drowns.
        Breath = Mathf.MoveToward(Breath, 0f, 100f / Stats.BreathSeconds * dt);
        if (Breath <= 0f)
            Hurt(DrownDamagePerSecond * dt);
        UpdateNeeds(dt, exerting: true);
        UpdateStamina(dt, effort: Stats.SwimEffort);

        Forage();

        // Face the way it swims, tipping head down to dive and head up to rise, as an otter porpoises down through the kelp.
        float pitch = Mathf.Clamp(velocity.Y / Mathf.Max(speed, 0.1f), -1f, 1f) * DivePitch;
        float yaw = direction != Vector3.Zero
            ? Mathf.LerpAngle(Animal.Rotation.Y, Yaw(direction), Stats.TurnSpeed * dt)
            : Animal.Rotation.Y;
        Animal.Rotation = new Vector3(Mathf.Lerp(Animal.Rotation.X, pitch, Mathf.Min(1f, 4f * dt)), yaw, 0f);

        _headDip = Mathf.MoveToward(_headDip, 0f, dt / HeadLiftSeconds);
        // As on the surface, a little extra keeps the paws paddling while it hangs in the water.
        AnimateAnimal(velocity.Length() + 2f, 1f, _headDip, dt);
    }

    /// <summary>
    /// Offers to take whatever urchin, clam or fish is within reach of the mouth, in the ocean or a lake, when E is
    /// pressed. A fish is eaten at once; shellfish is carried up to the surface to eat.
    /// </summary>
    private void Forage()
    {
        if (_holding is { } held)
        {
            ActionPrompt = $"Swim up to eat the {Underwater.NameOf(held)}";
            return;
        }

        var mouth = Mouth + Animal.GlobalBasis.Y.Normalized() * Stats.BodyHeight * 0.5f;
        float reach = Stats.EatReach + 0.6f;
        if (Seabed?.WithinReach(mouth, reach) is { } kind)
        {
            ActionPrompt = $"E or click to catch the {Seabed.NameOf(kind)}";
            if (!Input.IsActionJustPressed(InputSetup.Eat) || Seabed.Take(mouth, reach) is not { } caught)
                return;
            if (caught == Seabed.Catch.Fish)
                Hunger = Mathf.Min(100f, Hunger + Seabed.FishNourishment);
            else
                Hold(caught == Seabed.Catch.Urchin ? Underwater.ShellKind.Urchin : Underwater.ShellKind.Clam);
            return;
        }

        if (Underwater?.ShellfishNear(mouth, reach) is not { } found)
            return;
        ActionPrompt = $"E or click to pick up the {found.Name}";
        if (!Input.IsActionJustPressed(InputSetup.Eat))
            return;
        Underwater.Take(found);
        Hold(found.Kind);
    }

    /// <summary>Takes an urchin or clam in the paws to carry up to the surface.</summary>
    private void Hold(Underwater.ShellKind kind)
    {
        _holding = kind;
        // Clutched between the forepaws, as an otter carries food up from the bottom. It hangs off the left paw, halfway
        // across to the right one, which always moves with it, so the food goes wherever the paws do: tucked to the
        // chest swimming down, and held up over the belly, ready to eat, when the otter rolls over to float.
        _heldModel = Underwater.Model(kind);
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
