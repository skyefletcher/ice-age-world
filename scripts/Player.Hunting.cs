using Godot;

namespace IceAgeWorld;

/// <summary>
/// Hunting, for animals that hunt. Close to a wild animal, F (or a click) pounces and bites it. Beside a kill, E eats
/// from it and F picks it up, if it's light enough: the cat drags it along in its jaws, slowed by the weight, and can
/// climb a tree with it. Up on a branch or the treetop, F puts it down there, and E eats from it in peace, out of reach
/// of anything on the ground, the way big cats cache their kills in trees. It can also eat straight from its jaws.
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
    private Vector3 Mouth => GlobalPosition - Animal.GlobalBasis.Z * Stats.MouthDistance;

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

        if (!upTree && Wildlife.PreyNear(Mouth, PounceReach) is { } prey)
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
            EatFrom(carcass);
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
        ActionPrompt = IsExhausted ? "Too winded to pounce" : $"Press F or click to attack the {prey.DisplayName.ToLower()}";
        if (IsExhausted || !Input.IsActionJustPressed(InputSetup.Attack))
            return;

        var toward = (prey.GlobalPosition - GlobalPosition) with { Y = 0f };
        if (toward.LengthSquared() > 0.0001f)
        {
            toward = toward.Normalized();
            Animal.Rotation = new Vector3(0, Yaw(toward), 0);
        }
        _lunge = toward * Stats.JumpBoost;
        _pounce = PounceSeconds;
        Stamina = Mathf.Max(0f, Stamina - PounceStamina);
        if (Stamina <= 0f)
            IsExhausted = true;

        Wildlife!.Bite(prey, BiteStrength, GlobalPosition);
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

    /// <summary>Keeps a carried kill in the jaws, wherever the hunter has got to this frame.</summary>
    private void HoldKill()
    {
        if (_carrying is null || Wildlife is null)
            return;
        var jaws = Animal.GlobalTransform * new Vector3(0f, Stats.BodyHeight * 0.5f, -Stats.MouthDistance);
        Wildlife.Hold(_carrying, jaws, Animal.GlobalRotation.Y);
    }

    /// <summary>Drops whatever the hunter has in its jaws and ends any pounce, e.g. when switching animal or respawning.</summary>
    private void StopHunting()
    {
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
