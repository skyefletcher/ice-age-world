using Godot;

namespace IceAgeWorld;

/// <summary>
/// Starting a family. Snow leopards live alone and only seek each other out to mate, keeping company for a few days
/// before going their separate ways. Here the player's snow leopard, once grown up, stays beside the wild one for ten
/// seconds and the two pair up for good: they have a cub, and the mate and cub travel with the player from then on as
/// its family (see <see cref="Herd"/>). They hunt and feed with it, turn on anything that bites it, wolf pack or polar
/// bear, and whenever the cub grows up another is born, up to three. Sometimes the wild one wants no mate and fights
/// the player's cat off instead, with ordinary bites; beaten, it gives in and pairs up. With no mate yet, two wild ones
/// may fight over the player's cat, and the winner pairs up with it. The player keeps only one mate: once it has one,
/// any other wild snow leopard that comes close challenges the mate and they fight to the death. If the mate falls, the
/// newcomer goes after its cubs one by one, as male big cats do when they take over; the cubs fight back, and any it
/// fails to kill live on. Then it becomes the new mate, with cubs of its own. All of them fight with the special attack.
/// </summary>
public partial class Player
{
    /// <summary>The snow leopard's family, while the player is a snow leopard.</summary>
    public Herd? Family => Animal is SnowLeopard && _herds.TryGetValue(Animal, out var family) ? family : null;

    /// <summary>Seconds the player's snow leopard must stay beside the wild one to pair up with it.</summary>
    private const float CourtshipSeconds = 10f;

    /// <summary>How close, in metres, counts as staying beside it: a few body lengths for these big cats.</summary>
    private const float CourtshipRange = 8f;

    /// <summary>Seconds spent beside the wild snow leopard so far; leaving it starts the count again.</summary>
    private float _courtship;

    /// <summary>
    /// How things stand with a wild snow leopard close by, shown on screen so the player can tell why no cub comes yet:
    /// still too young, being fought off, or how long is left to stay beside it. Null when none is close.
    /// </summary>
    public string? CourtshipPrompt { get; private set; }

    private void Court(float dt)
    {
        CourtshipPrompt = null;
        if (Animal is not SnowLeopard || !_herds.TryGetValue(Animal, out var family) || Wildlife is null)
        {
            _courtship = 0f;
            return;
        }

        // Beaten in a fight, the wild snow leopard gives in and pairs up after all.
        if (Wildlife.BeatenRival() is { } beaten)
        {
            LetGo();
            _courtship = 0f;
            PairUp(family, beaten);
            return;
        }

        // A wild one fighting the mate, or two fighting each other over the player's cat: the winner comes to it once
        // the fight is over.
        if (Wildlife.FightingOverPlayer)
        {
            _courtship = 0f;
            CourtshipPrompt = family.HasMate ? "A snow leopard is fighting your mate to the death!"
                : Wildlife.KillingCubs ? "The new snow leopard is going after your cubs!"
                : IsGrownUp ? "Two snow leopards are fighting over you!" : null;
            return;
        }

        // A snow leopard keeps only one mate.
        if (family.HasMate)
        {
            _courtship = 0f;
            return;
        }

        // Only one of the other sex will do.
        if (Wildlife.MateNear(GlobalPosition, CourtshipRange, Sex) is not { } mate)
        {
            _courtship = 0f;
            return;
        }

        // Snow leopards only breed once they're grown, at two or three years old; a cub has to grow up first.
        if (!IsGrownUp)
        {
            _courtship = 0f;
            CourtshipPrompt = "Grow up first to have cubs: keep eating and drinking";
            return;
        }

        if (_mode != Mode.Ground || IsGripping)
        {
            _courtship = 0f;
            return;
        }

        // Sometimes it wants no mate, and turns on the player's cat instead.
        if (!Wildlife.WillMate(mate))
        {
            _courtship = 0f;
            CourtshipPrompt = "It wants to fight! Bite it until it gives in";
            return;
        }

        _courtship += dt;
        if (_courtship < CourtshipSeconds)
        {
            CourtshipPrompt = $"Stay close to start a family... {Mathf.CeilToInt(CourtshipSeconds - _courtship)}";
            return;
        }

        _courtship = 0f;
        PairUp(family, mate);
    }

    /// <summary>The wild snow leopard leaves the wild to join the player's family, and they have their first cub.</summary>
    private void PairUp(Herd family, Animal mate)
    {
        family.AddMate(Wildlife!.Leave(mate));
    }
}
