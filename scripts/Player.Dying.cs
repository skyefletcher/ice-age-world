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
    public void Bitten(float damage)
    {
        if (IsDead)
            return;

        float toughness = Mathf.Max(0.75f, Mathf.Sqrt(Stats.BodyRadius * Stats.BodyHeight / 0.3f));
        Health = Mathf.Max(0f, Health - damage / toughness);
        _sinceBitten = 0f;
        if (IsDead)
            Die();
    }

    /// <summary>Seconds since the last bite; wounds only start to heal once the animal is safe.</summary>
    private float _sinceBitten = 100f;

    private void Heal(float dt)
    {
        _sinceBitten += dt;
        if (_sinceBitten > 3f)
            Health = Mathf.Min(100f, Health + HealPerSecond * dt);
    }

    /// <summary>The animal falls: it drops whatever it was doing, and from here on lies where it fell.</summary>
    private void Die()
    {
        LeaveTree();
        StopHunting();
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
            velocity.Y -= _gravity * dt;
        Velocity = velocity;
        MoveAndSlide();

        Animal.IsSwimming = afloat;
        Animal.Settle(Posture.Lying, 0.6f, dt);
        Animal.Dead = Mathf.MoveToward(Animal.Dead, 1f, dt / 0.8f);
        Animal.Animate(0f, 0f, 0f, dt);
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
