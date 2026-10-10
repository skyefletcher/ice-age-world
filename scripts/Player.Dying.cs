using Godot;

namespace IceAgeWorld;

/// <summary>
/// Being hunted, dying and being reborn. Wild wolves run down a player that is a mammoth, sea otter or bald eagle, the
/// way they would any of those animals, and bite it while they can reach it. Its health falls with every bite, and
/// slowly comes back once it gets away. If it falls, it goes limp where it lies, and the player chooses whether, and as
/// which animal, to be reborn: as a youngster at the start, to grow up all over again.
/// </summary>
public partial class Player
{
    /// <summary>Health a reborn or escaped animal gets back each second.</summary>
    [Export] public float HealPerSecond { get; set; } = 0.5f;

    /// <summary>Life from 0 (dead) to 100 (unhurt).</summary>
    public float Health { get; private set; } = 100f;

    public bool IsDead => Health <= 0f;

    /// <summary>
    /// True while the animal is one wolves prey on: anything but a fellow wolf, or a snow leopard or polar bear, which
    /// they fear.
    /// </summary>
    public bool IsWolfPrey => !IsDead && Animal is not (ArcticWolf or SnowLeopard or PolarBear);

    /// <summary>The animals the player can be reborn as, by name, in switching order.</summary>
    public string[] AnimalNames => System.Array.ConvertAll(_animals, a => a.DisplayName);

    /// <summary>
    /// Whether a wolf standing at <paramref name="from"/> can get its teeth into the animal: not when it's flying overhead
    /// or up a tree, only on the ground or in the water beside it.
    /// </summary>
    public bool WithinBite(Vector3 from) => OnTheGround && Mathf.Abs(GlobalPosition.Y - from.Y) < Stats.BodyHeight + 1f;

    /// <summary>True on foot or swimming, where wolves can get at it; false flying or up a tree.</summary>
    public bool OnTheGround => _mode == Mode.Ground;

    /// <summary>
    /// A wolf bites the animal, taking <paramref name="damage"/> from one the size of a wolf. A big animal shrugs off
    /// more, so a grown mammoth holds out a long while but an otter or eagle very little, and a youngster less again.
    /// </summary>
    public void Bitten(float damage, Animal? by = null)
    {
        if (IsDead)
            return;

        float toughness = Mathf.Max(0.75f, Mathf.Sqrt(Stats.BodyRadius * Stats.BodyHeight / 0.3f));
        Hurt(damage / toughness);
        Spurt();

        // The player's pack or family turns on whatever is biting it, however big a pack or bear it is.
        if (by is not null && !IsDead && _herds.TryGetValue(Animal, out var family) && family.Attack(by))
            Announce($"Your {family.Word} turns on the {by.DisplayName.ToLower()} to save you!", 3f);
    }

    /// <summary>Takes <paramref name="damage"/> off the animal's health, e.g. from a bite or a bad fall; a mammoth takes far less.</summary>
    private void Hurt(float damage)
    {
        if (IsDead)
            return;
        if (Animal is Mammoth)
            damage /= Mammoth.Toughness;
        Health = Mathf.Max(0f, Health - damage);
        _sinceBitten = 0f;
        if (IsDead)
            Die();
    }

    /// <summary>Seconds since the last bite; wounds only start to heal once the animal is safe.</summary>
    private float _sinceBitten = 100f;

    /// <summary>Blood that spurts from the animal while something is biting it.</summary>
    private CpuParticles3D? _blood;
    private float _dripTimer;

    /// <summary>Blood spurts from the animal for a moment, from where the teeth or claws went in.</summary>
    private void Spurt()
    {
        if (_blood is null)
        {
            // On the player, not the model, which may be drawn scaled up (the snow leopard's is), and that would throw
            // the drops far too high and wide.
            _blood = Blood.Drops(Stats);
            AddChild(_blood);
        }
        // Fitted to whichever animal the player is now, however grown, at its neck whichever way it faces.
        Blood.Fit(_blood, Stats);
        _blood.Position = _blood.Position.Rotated(Vector3.Up, Animal.Rotation.Y);
        _blood.Rotation = new Vector3(0f, Animal.Rotation.Y, 0f);
        _blood.Emitting = true;
    }

    private void Heal(float dt)
    {
        if (_blood is not null && _sinceBitten > 0.4f)
            _blood.Emitting = false;

        // While it bleeds, drops spot the snow below it, leaving a trail.
        _dripTimer += dt;
        if (_blood is { Emitting: true } && _dripTimer > 0.12f)
        {
            _dripTimer = 0f;
            Wildlife?.Drip(_blood.GlobalPosition, Stats);
        }
        _sinceBitten += dt;
        if (_sinceBitten > 3f)
            Health = Mathf.Min(100f, Health + HealPerSecond * dt);
    }

    /// <summary>The animal falls: it drops whatever it was doing, and from here on lies where it fell.</summary>
    private void Die()
    {
        LeaveTree();
        // Dead, it doesn't spring away from the tree: it just drops.
        Velocity = Velocity with { Y = Mathf.Min(Velocity.Y, 0f) };
        StopHunting();
        _courtship = 0f;
        Courtship = null;
        _mode = Mode.Ground;
        Animal.IsFlying = Animal.IsClimbing = false;
        _feeding = Feeding.None;
        _grassTarget = -1;
        Posture = Posture.Lying;
    }

    /// <summary>A dead animal goes limp, falling to the ground or floating where it lies, until the player is reborn.</summary>
    private void LieDead(float dt)
    {
        var velocity = Velocity with { X = 0f, Z = 0f };
        float? surface = Water?.SurfaceAt(GlobalPosition);
        bool afloat = surface.HasValue && surface.Value - GlobalPosition.Y > Stats.FloatDepth * 0.5f;
        if (afloat)
            velocity.Y = (surface!.Value - Stats.FloatDepth - GlobalPosition.Y) * 3f;
        else if (!IsOnFloor())
            velocity.Y -= Animal.GravityOn(velocity.Y) * dt;
        Velocity = velocity;
        MoveAndSlide();

        Animal.IsSwimming = afloat;
        Animal.Settle(Posture.Lying, 0.6f, dt);
        Animal.Dead = Mathf.MoveToward(Animal.Dead, 1f, dt / 0.8f);
        Animal.Animate(0f, 0f, 0f, dt);

        // The last of the blood runs out as it falls.
        if (_blood is not null && Animal.Dead >= 1f)
            _blood.Emitting = false;
    }

    /// <summary>
    /// Starts life again as a newborn of the animal at <paramref name="index"/>, at the start, hungry, thirsty and
    /// with a whole life ahead of it.
    /// </summary>
    public void Reborn(int index)
    {
        Animal.Dead = 0f;
        Animal.Visible = false;
        if (_herds.TryGetValue(Animal, out var oldHerd))
            oldHerd.Visible = false;

        Age = 0f;
        Health = 100f;
        _sinceBitten = 100f;
        Hunger = 75f;
        Thirst = 60f;
        Stamina = 100f;
        IsExhausted = false;
        BecomeAnimal(index);
        Respawn();
    }
}
