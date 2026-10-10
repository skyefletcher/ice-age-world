using Godot;

namespace IceAgeWorld;

/// <summary>
/// Hunting, for animals that hunt. Close to a wild animal, F (or a click) pounces and bites it. Beside a kill, E eats
/// from it and F picks it up, if it's light enough: the cat drags it along in its jaws, slowed by the weight, and can
/// climb a tree with it. Up on a branch or the treetop, F puts it down there, and E eats from it in peace, out of reach
/// of anything on the ground, the way big cats cache their kills in trees. It can also eat straight from its jaws.
/// The snow leopard, the arctic wolf and the bald eagle all hunt; the eagle can also snatch prey on the wing (see
/// <see cref="Swoop"/>) and carry it off in its talons.
/// </summary>
public partial class Player
{
    /// <summary>Hunger restored by one mouthful of meat, richer than grass.</summary>
    [Export] public float MeatPerMouthful { get; set; } = 20f;

    /// <summary>How much of a carcass of unit bulk one mouthful eats: an otter is a couple of mouthfuls, a mammoth a feast.</summary>
    [Export] public float MeatShare { get; set; } = 0.06f;

    /// <summary>
    /// How much a hunter's bite takes from prey of unit bulk (see <see cref="Wildlife.Bite"/>): enough to kill an otter
    /// outright, a wolf in a few bites, and a mammoth calf in several, while a grown mammoth is all but too big to take.
    /// </summary>
    [Export] public float BiteStrength { get; set; } = 10f;

    /// <summary>How far a flying hunter's talons reach to snatch prey from below it.</summary>
    [Export] public float TalonReach { get; set; } = 1.5f;

    /// <summary>How far beyond the mouth a hunter's pounce can reach its prey.</summary>
    [Export] public float PounceReach { get; set; } = 1.2f;

    /// <summary>Seconds one pounce takes, from springing to landing the bite and recovering for the next.</summary>
    [Export] public float PounceSeconds { get; set; } = 0.6f;

    /// <summary>Stamina each pounce costs.</summary>
    [Export] public float PounceStamina { get; set; } = 6f;

    /// <summary>How fast a hunter goes, walking, running or climbing, while it carries a kill, as a fraction of its usual pace.</summary>
    [Export] public float CarrySpeed { get; set; } = 0.6f;

    /// <summary>
    /// How heavy a kill the hunter can lift, compared with its own bulk. Leopards haul kills up to about their own
    /// weight into trees, so a cat can carry an otter, a wolf or even a mammoth calf, but not a grown mammoth.
    /// </summary>
    [Export] public float CarryStrength { get; set; } = 1.5f;

    /// <summary>True while the animal has a kill in its jaws.</summary>
    public bool IsCarrying => _carrying is not null;

    /// <summary>Where the mouth reaches to, just in front of the body.</summary>
    private Vector3 Mouth => GlobalPosition - Animal.GlobalBasis.Z.Normalized() * Stats.MouthDistance;

    /// <summary>The carcass being eaten from, while a hunter feeds.</summary>
    private Animal? _carcass;

    /// <summary>The kill the hunter has in its jaws, if any.</summary>
    private Animal? _carrying;

    /// <summary>Seconds left of the current pounce, and the extra speed it springs forward with.</summary>
    private float _pounce;
    private Vector3 _lunge;

    /// <summary>
    /// Works out what the hunter can do with the wild animals around it, prompts for it and does it when asked: pounce
    /// on prey, eat from a kill, or pick one up, carry it and put it down again. <paramref name="upTree"/> is true on a
    /// branch or treetop, where there is nothing to pounce on and the prompts are added to the climbing ones.
    /// </summary>
    private void UpdateHunting(float dt, bool resting, bool upTree)
    {
        if (_pounce > 0f)
        {
            // The head snaps down to bite as the cat lands on its prey.
            _pounce = Mathf.Max(0f, _pounce - dt);
            _headDip = Mathf.Max(_headDip, Mathf.Sin((1f - _pounce / PounceSeconds) * Mathf.Pi) * 0.8f);
            return;
        }
        if (IsFeeding || resting || IsSwimming || Wildlife is null)
            return;

        if (_carrying is { } held)
        {
            Carry(held, upTree);
            return;
        }

        if (!upTree && Wildlife.PreyNear(Mouth, PounceReach * Size) is { } prey)
        {
            Pounce(prey);
            return;
        }

        if (Wildlife.CarcassNear(Mouth, Stats.EatReach + 0.5f) is not { } carcass)
            return;

        string name = carcass.DisplayName.ToLower();
        bool light = Wildlife.CanCarry(carcass, Stats.BodyRadius * Stats.BodyHeight * CarryStrength);
        Prompt(light ? $"E to eat the {name}, F to pick it up" : $"E to eat the {name} (too heavy to carry)", upTree);
        if (Input.IsActionJustPressed(InputSetup.Eat))
        {
            EatFrom(carcass);

            // A kill on the ground is shared: the pack crowds in to eat too. Up a tree, it's the cat's alone.
            if (!upTree && _herds.TryGetValue(Animal, out var pack) && pack.Feast(carcass))
            {
                Announce($"Your {pack.Word} eats the {name} with you", 3f);
            }
        }
        else if (light && Input.IsActionJustPressed(InputSetup.Attack))
            _carrying = carcass;
    }

    /// <summary>
    /// A hunter close enough to a wild animal can pounce on it: it turns on the prey, springs forward and bites, and the
    /// head snaps down as it does. Snow leopards ambush from close range in a few explosive bounds rather than running
    /// prey down, so each pounce costs a good gulp of stamina, and a winded cat can't pounce at all.
    /// </summary>
    private void Pounce(Animal prey)
    {
        string name = prey.DisplayName.ToLower();
        bool grip = Stats.Can(Ability.Grip);
        ActionPrompt = IsExhausted ? "Too winded to pounce"
            : grip ? $"F or click to attack the {name}, G or right-click to leap on and hold it"
            : $"Press F or click to attack the {name}";
        if (IsExhausted)
            return;
        if (grip && Input.IsActionJustPressed(InputSetup.Grip))
        {
            StartGrip(prey);
            return;
        }
        if (!Input.IsActionJustPressed(InputSetup.Attack))
            return;

        var toward = (prey.GlobalPosition - GlobalPosition) with { Y = 0f };
        float gap = toward.Length() - prey.Stats.BodyRadius * Wildlife!.SizeOf(prey) - Stats.BodyRadius * 0.5f;
        if (toward.LengthSquared() > 0.0001f)
        {
            toward = toward.Normalized();
            Animal.Rotation = new Vector3(0, Yaw(toward), 0);
        }
        // Spring only as far as the prey, so the hunter lands on it rather than sailing past something small like a
        // hare. A spring carries the body about as many metres as its speed in metres a second.
        _lunge = toward * Mathf.Clamp(gap, 0f, Stats.JumpBoost);
        _pounce = PounceSeconds;
        Stamina = Mathf.Max(0f, Stamina - PounceStamina);
        if (Stamina <= 0f)
            IsExhausted = true;

        // A cub bites with a cub's jaws: bite strength goes with bulk, so a newborn takes a few bites to kill an otter and a dozen or more for a wolf.
        // Unless the bite finished it, the leader's pack piles in to help.
        if (!Wildlife!.Bite(prey, BiteStrength * Stats.Strength * Size * Size, GlobalPosition)
            && _herds.TryGetValue(Animal, out var pack) && pack.Attack(prey))
        {
            Announce($"Your {pack.Word} joins the attack on the {prey.DisplayName.ToLower()}!", 3f);
        }
    }

    /// <summary>With a kill in its jaws, the hunter can eat from it where it stands, or put it down: up a tree it stays put there.</summary>
    private void Carry(Animal held, bool upTree)
    {
        // Picked clean: leave the bones.
        if (!Wildlife!.HasMeat(held))
        {
            PutDown(upTree);
            return;
        }

        Prompt($"E to eat the {held.DisplayName.ToLower()}, F to put it down", upTree);
        if (Input.IsActionJustPressed(InputSetup.Eat))
            EatFrom(held);
        else if (Input.IsActionJustPressed(InputSetup.Attack))
            PutDown(upTree);
    }

    private void EatFrom(Animal carcass)
    {
        StartFeeding(Feeding.Eating, EatDuration);
        _carcass = carcass;
    }

    /// <summary>Lets go of the kill: on the ground it drops at the cat's feet; up a tree it's laid on the wood in front.</summary>
    private void PutDown(bool upTree)
    {
        if (_carrying is null)
            return;
        Wildlife?.Drop(_carrying, upTree ? Mouth with { Y = GlobalPosition.Y } : null);
        _carrying = null;
    }

    /// <summary>
    /// A bird of prey in flight strikes with its talons: skimming low over a wild animal, F snatches at it. A small one
    /// dies in the grip and, if it's light enough, is carried off in the talons, to be eaten once the bird lands. This
    /// is how eagles take hares, swooping in low and fast from behind.
    /// </summary>
    private void Swoop()
    {
        if (Wildlife is null)
            return;

        if (_carrying is { } held)
        {
            ActionPrompt = $"Land to eat the {held.DisplayName.ToLower()}, F to let go";
            if (Input.IsActionJustPressed(InputSetup.Attack))
            {
                Wildlife.Drop(held);
                _carrying = null;
            }
            return;
        }

        // The talons reach down below the body as the bird swings its feet forward to strike.
        var talons = GlobalPosition + Vector3.Down * TalonReach * 0.5f * Size;
        if (Wildlife.PreyNear(talons, TalonReach * Size) is not { } prey)
            return;

        ActionPrompt = $"Press F or click to snatch the {prey.DisplayName.ToLower()}";
        if (!Input.IsActionJustPressed(InputSetup.Attack))
            return;

        // Talons driven home at the speed of a stoop strike harder than any bite.
        bool killed = Wildlife.Bite(prey, BiteStrength * 2f * Size * Size, GlobalPosition);
        if (killed && Wildlife.CanCarry(prey, Stats.BodyRadius * Stats.BodyHeight * CarryStrength))
            _carrying = prey;
    }

    /// <summary>Keeps a carried kill in the jaws, or a flying bird's talons, wherever the hunter has got to this frame.</summary>
    private void HoldKill()
    {
        if (_carrying is null || Wildlife is null)
            return;
        var grip = Animal.IsFlying
            ? GlobalPosition
            : GlobalPosition + Animal.GlobalBasis.Orthonormalized() * new Vector3(0f, Stats.BodyHeight * 0.5f, -Stats.MouthDistance);
        Wildlife.Hold(_carrying, grip, Animal.GlobalRotation.Y);
    }

    /// <summary>Drops whatever the hunter has in its jaws and ends any pounce, e.g. when switching animal or respawning.</summary>
    private void StopHunting()
    {
        LetGo();
        if (_carrying is not null)
            Wildlife?.Drop(_carrying);
        _carrying = null;
        _carcass = null;
        _pounce = 0f;
        _lunge = Vector3.Zero;
    }

    /// <summary>On the ground a hunting prompt replaces any other; up a tree it goes under the climbing prompt.</summary>
    private void Prompt(string text, bool below) =>
        ActionPrompt = below && ActionPrompt is not null ? ActionPrompt + "\n" + text : text;
}
