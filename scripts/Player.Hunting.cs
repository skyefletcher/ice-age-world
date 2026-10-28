using Godot;

namespace IceAgeWorld;

/// <summary>
/// Hunting, for animals that hunt. Close to a wild animal, F pounces and bites it. Beside a kill, E eats
/// from it and F picks it up, if it's light enough: the cat drags it along in its jaws, slowed by the weight, and can
/// climb a tree with it. Up on a branch or the treetop, F puts it down there, and E eats from it in peace, out of reach
/// of anything on the ground, the way big cats cache their kills in trees. It can also eat straight from its jaws.
/// The snow leopard, the arctic wolf and the bald eagle all hunt; the eagle can also snatch prey on the wing (see
/// <see cref="Swoop"/>) and carry it off in its talons.
/// </summary>
public partial class Player
{
    /// <summary>Hunger restored by one mouthful of meat, richer than grass, though it still takes a long feed to fill up.</summary>
    [Export] public float MeatPerMouthful { get; set; } = 8f;

    /// <summary>How much of a carcass of unit bulk one mouthful eats: an otter is a couple of mouthfuls, a mammoth a feast.</summary>
    [Export] public float MeatShare { get; set; } = 0.024f;

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

    /// <summary>
    /// What a flying bird has in its talons, a kill or live prey, if anything: what a wild eagle or owl chases it to
    /// steal (see <see cref="Wildlife"/>'s eagles).
    /// </summary>
    public Animal? InTalons => Animal is Bird && Animal.IsFlying ? _carrying ?? _lifting : null;

    /// <summary>Whether the player still has <paramref name="food"/>, in its jaws or talons, flying or not.</summary>
    public bool IsHolding(Animal food) => _carrying == food || _lifting == food;

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
    /// Works out what the hunter can do with the wild animals around it and does it when asked: pounce
    /// on prey, eat from a kill, or pick one up, carry it and put it down again. <paramref name="upTree"/> is true on a
    /// branch or treetop, where there is nothing to pounce on. The prompt says how to eat a kill and how to pick it up or
    /// put it down; attacks go unannounced.
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

        // A wild bird's nest within reach: eggs are an easy meal for any hunter, if it can face down the parents.
        if (Wildlife.WildNestNear(Mouth, Stats.EatReach + 0.5f) is { } nest)
        {
            ActionPrompt = $"E or click to eat an egg ({nest.Eggs} left)";
            if (Input.IsActionJustPressed(InputSetup.Eat))
            {
                Wildlife.EatWildEgg(nest);
                Hunger = Mathf.Min(100f, Hunger + Wildlife.EggNourishment);
                _headDip = 1f;
            }
            return;
        }

        if (Wildlife.CarcassNear(Mouth, Stats.EatReach + 0.5f) is not { } carcass)
        {
            if (!upTree)
                AttackThinAir();
            return;
        }

        bool light = Wildlife.CanCarry(carcass, Stats.BodyRadius * Stats.BodyHeight * CarryStrength);
        string name = carcass.DisplayName.ToLower();
        ActionPrompt = light
            ? $"E or click to eat the {name}\nF to pick it up"
            : $"E or click to eat the {name}\n(too heavy to pick up)";
        if (Input.IsActionJustPressed(InputSetup.Eat))
        {
            EatFrom(carcass);

            // A kill on the ground is shared: the pack crowds in to eat too. Up a tree, it's the cat's alone.
            if (!upTree && _herds.TryGetValue(Animal, out var pack))
                pack.Feast(carcass);
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
        // Attacking has no prompt: F bites (and G leaps on) without a word on screen, and so does being too winded to
        // pounce.
        bool grip = Stats.Can(Ability.Grip);
        if (IsExhausted)
            return;
        if (Animal is Bird && Input.IsActionJustPressed(InputSetup.Grip))
        {
            TalonAttack(prey);
            return;
        }
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
            && _herds.TryGetValue(Animal, out var pack))
            pack.Attack(prey);
    }

    /// <summary>
    /// With nothing in reach, attacking still does something: F springs forward and snaps the jaws, as a young hunter
    /// practises or a cat bats at a hare that got away, and G leaps (a cat) or slashes with the talons (a bird). It
    /// costs stamina all the same.
    /// </summary>
    private void AttackThinAir()
    {
        bool special = Input.IsActionJustPressed(InputSetup.Grip) && (Stats.Can(Ability.Grip) || Animal is Bird);
        if (IsExhausted || (!special && !Input.IsActionJustPressed(InputSetup.Attack)))
            return;
        if (special && Animal is Bird)
        {
            TalonAttack(null);
            return;
        }

        // A leap carries further than a snap of the jaws.
        _lunge = Forward(Animal.Rotation.Y) * Stats.JumpBoost * (special ? 1f : 0.6f);
        _pounce = PounceSeconds;
        Stamina = Mathf.Max(0f, Stamina - PounceStamina);
        if (Stamina <= 0f)
            IsExhausted = true;
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

        ActionPrompt = $"E or click to eat the {held.DisplayName.ToLower()}\nF to put it down";
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

        if (_lifting is not null)
        {
            Lifting();
            return;
        }

        if (_carrying is { } held)
        {
            ActionPrompt = $"F to drop the {held.DisplayName.ToLower()}, or land to eat it";
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
        {
            // With nothing below, the bird can still strike out with its talons.
            if (Input.IsActionJustPressed(InputSetup.Attack) || Input.IsActionJustPressed(InputSetup.Grip))
                TalonAttack(null);
            return;
        }

        // As on the ground, the strike itself isn't announced, but picking something up is: F snatches it, and G lifts
        // it alive, or slashes at it if it's too heavy.
        if (CanLift(prey) && !IsExhausted)
            ActionPrompt = $"G to pick up the {prey.DisplayName.ToLower()}";
        if (Input.IsActionJustPressed(InputSetup.Grip))
        {
            TalonAttack(prey);
            return;
        }
        if (!Input.IsActionJustPressed(InputSetup.Attack))
            return;

        // Talons driven home at the speed of a stoop strike harder than any bite.
        bool killed = Wildlife.Bite(prey, BiteStrength * 2f * Size * Size, GlobalPosition);
        if (killed && Wildlife.CanCarry(prey, Stats.BodyRadius * Stats.BodyHeight * CarryStrength))
            _carrying = prey;
        // Unless the strike killed it, the mate and grown young stoop in to help bring it down.
        else if (!killed && _herds.TryGetValue(Animal, out var family))
            family.Attack(prey);
    }

    /// <summary>Keeps a carried kill in the jaws, or a flying bird's talons, wherever the hunter has got to this frame.</summary>
    private void HoldKill()
    {
        // In flight a bird holds what it carries in its talons, legs reaching down under it.
        if (Animal is Bird bird)
            bird.Clutching = Animal.IsFlying && (_carrying is not null || _lifting is not null);
        if (_carrying is null || Wildlife is null)
            return;
        // On the ground (an eagle too, in its beak) the kill is clamped crosswise in the jaws.
        var facing = -Animal.GlobalBasis.Z with { Y = 0f };
        facing = facing.LengthSquared() > 0.0001f ? facing.Normalized() : Vector3.Forward;
        Wildlife.Hold(_carrying, Animal.IsFlying ? Talons : Animal.Bite, facing, inJaws: !Animal.IsFlying);
    }

    /// <summary>Drops whatever the hunter has in its jaws (or an otter in its paws) and ends any pounce, e.g. when switching animal or respawning.</summary>
    private void StopHunting()
    {
        LetGo();
        DropHeld();
        DropLifted();
        if (_carrying is not null)
            Wildlife?.Drop(_carrying);
        _carrying = null;
        _carcass = null;
        _pounce = 0f;
        _lunge = Vector3.Zero;
    }
}
