using Godot;

namespace IceAgeWorld;

/// <summary>
/// The snow leopard's special attack. Close to prey, G leaps onto it and clamps the jaws on, as big
/// cats seize the back of the neck and hang on. On anything big enough to ride, it springs right up onto the prey's
/// back, as snow leopards do to bring down blue sheep and ibex; something smaller it pins down from the side, held flat
/// where it is with no way to get away. While it holds on, the cat does far more damage than biting and letting go,
/// and big prey can only stagger along under it.
/// Holding on is hard work: it drains stamina, and once the cat is spent it has to let go. Space or G lets go sooner.
/// Letting go leaves the prey bleeding, losing health for a while after (see <see cref="Wildlife.Bleed"/>).
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

    /// <summary>True while riding on the prey's back, rather than hanging on at its side.</summary>
    private bool _onBack;

    /// <summary>Where the leap onto the prey started, and 0..1 how far through it the cat is.</summary>
    private Vector3 _leapFrom;
    private float _leap = 1f;

    /// <summary>Seconds the spring up onto the prey takes.</summary>
    private const float LeapSeconds = 0.35f;

    /// <summary>
    /// Seconds since the prey the cat is riding fell dead under it, while it rides the body down; below zero otherwise.
    /// </summary>
    private float _ridingDown = -1f;

    /// <summary>The least time the cat rides its kill down, so it stays on a moment after the body hits the ground.</summary>
    private const float RideDownSeconds = 1.1f;

    /// <summary>
    /// How tall prey has to be, against the cat's own height, for it to ride on its back: a reindeer, moose, mammoth,
    /// polar bear or another snow leopard, but not a wolf, otter or hare, which it pins from the side.
    /// </summary>
    private const float RideableHeight = 0.6f;

    /// <summary>Leaps onto the prey and clamps on, landing a hard first bite.</summary>
    private void StartGrip(Animal prey)
    {
        var side = (GlobalPosition - prey.GlobalPosition) with { Y = 0f };
        _gripSide = side.LengthSquared() > 0.0001f ? side.Normalized() : Vector3.Back;
        _onBack = prey.Stats.BodyHeight * Wildlife!.SizeOf(prey) >= Stats.BodyHeight * RideableHeight;
        _leapFrom = GlobalPosition;
        _leap = 0f;
        _gripping = prey;
        _lunge = Vector3.Zero;
        Stamina = Mathf.Max(0f, Stamina - PounceStamina);
        Wildlife!.Grip(prey, true, pins: !_onBack);
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

        if (_ridingDown >= 0f)
        {
            // Riding the kill down: the cat keeps its hold until the body is on the ground, then springs off it.
            _ridingDown += dt;
            UpdateNeeds(dt, exerting: false);
            ActionPrompt = $"The {name} is down!";
            if ((prey.Dead >= 1f && _ridingDown > RideDownSeconds) || _ridingDown > RideDownSeconds * 4f)
            {
                LetGo();
                return;
            }
        }
        else
        {
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
                // Up on the back of prey it has just killed, the cat hangs on and goes down with it; otherwise (pinning
                // it from the side, or a rival snow leopard that gave in) it simply lets go.
                if (_onBack && !Wildlife.IsAlive(prey))
                    _ridingDown = 0f;
                else
                {
                    LetGo();
                    return;
                }
            }
        }

        Vector3 place;
        float yaw;
        float size = Wildlife!.SizeOf(prey);
        if (_onBack)
        {
            // Up on its back, facing the way it goes, a little forward of its middle so the jaws reach the scruff.
            // As the prey falls, its back comes down to its flank lying on the ground, about its own width up.
            var forward = -prey.GlobalBasis.Z.Normalized();
            float back = Mathf.Lerp(prey.Stats.BodyHeight * prey.Stats.BackHeight, prey.Stats.BodyRadius * 2f,
                Mathf.SmoothStep(0f, 1f, prey.Lying));
            place = prey.GlobalPosition + forward * prey.Stats.BodyRadius * size * 0.5f + Vector3.Up * back * size;
            yaw = prey.GlobalRotation.Y;

            // Flattened down onto the back, legs bent to grip its sides, rather than standing up on top of it.
            Animal.Impact = 1f;
        }
        else
        {
            // Pressed against the prey's flank, facing in to it, on the ground or the water beside it.
            float reach = prey.Stats.BodyRadius * size + Stats.BodyRadius * 0.6f;
            var spot = prey.GlobalPosition + _gripSide * reach;
            float ground = Terrain?.GroundBelow(spot) ?? spot.Y;
            float? surface = Water?.SurfaceAt(spot);
            float height = surface.HasValue && surface.Value - ground > Stats.FloatDepth ? surface.Value - Stats.FloatDepth : ground;
            place = spot with { Y = height };
            yaw = Yaw(-_gripSide);
        }

        // The spring up: an arc from where the cat stood to its hold, rising above it on the way.
        if (_leap < 1f)
        {
            _leap = Mathf.Min(1f, _leap + dt / LeapSeconds);
            place = _leapFrom.Lerp(place, _leap) + Vector3.Up * Mathf.Sin(_leap * Mathf.Pi) * Stats.BodyHeight * 0.4f;
        }

        var moved = place - GlobalPosition;
        GlobalPosition = place;
        Velocity = Vector3.Zero;
        Animal.Rotation = new Vector3(0, Mathf.LerpAngle(Animal.Rotation.Y, yaw, Mathf.Min(1f, 12f * dt)), 0);

        // Head down and jaws locked, legs scrabbling to keep up as the prey drags it along.
        float speed = (moved with { Y = 0f }).Length() / Mathf.Max(dt, 0.001f);
        Animal.IsSwimming = false;
        Animal.Animate(speed, Mathf.Clamp(speed / Stats.WalkSpeed, 0f, 1f), 0.9f, dt);
    }

    /// <summary>
    /// Lets go of the prey, if the cat has hold of any. The wound keeps bleeding after, and a cat on the prey's back
    /// springs down off it to one side.
    /// </summary>
    private void LetGo()
    {
        if (_gripping is null)
            return;
        Wildlife?.Grip(_gripping, false, pins: !_onBack);
        Wildlife?.Bleed(_gripping, Size * Size);
        if (_onBack)
        {
            // Off a fallen kill it leaps clear in a bound; off live prey it just springs down.
            bool fromKill = _ridingDown >= 0f;
            var side = _gripping.GlobalBasis.X.Normalized();
            Velocity = side * (fromKill ? 6f : 4f) + Vector3.Up * (fromKill ? 6f : 3f);
            _onBack = false;
        }
        _ridingDown = -1f;
        _gripping = null;
    }
}
