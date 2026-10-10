using Godot;

namespace IceAgeWorld;

/// <summary>
/// Starting a family. Snow leopards live alone and only seek each other out to mate, keeping company for a few days
/// before going their separate ways. Here the player's snow leopard, once grown up, stays beside the wild one for ten
/// seconds and the two pair up for good: they have a cub, and the mate and cub travel with the player from then on as
/// its family (see <see cref="Herd"/>). They hunt and feed with it, turn on anything that bites it, wolf pack or polar
/// bear, and whenever the cub grows up another is born, up to three. Sometimes the wild one wants no mate and fights
/// the player's cat off instead, with ordinary bites; beaten, it gives in and pairs up.
/// </summary>
public partial class Player
{
    /// <summary>Seconds the player's snow leopard must stay beside the wild one to pair up with it.</summary>
    private const float CourtshipSeconds = 10f;

    /// <summary>How close, in metres, counts as staying beside it: a few body lengths for these big cats.</summary>
    private const float CourtshipRange = 8f;

    /// <summary>Seconds spent beside the wild snow leopard so far; leaving it starts the count again.</summary>
    private float _courtship;

    /// <summary>How the courtship is going, shown on the HUD while it is under way, or null.</summary>
    public string? Courtship { get; private set; }

    private void Court(float dt)
    {
        Courtship = null;
        if (Animal is not SnowLeopard || !_herds.TryGetValue(Animal, out var family) || family.Any || Wildlife is null)
        {
            _courtship = 0f;
            return;
        }

        // Beaten in a fight, the wild snow leopard gives in and pairs up after all.
        if (Wildlife.BeatenRival() is { } beaten)
        {
            LetGo();
            _courtship = 0f;
            PairUp(family, beaten, "You won the fight!");
            return;
        }
        if (Wildlife.RivalFighting)
            Courtship = "The snow leopard doesn't want a mate and is fighting you! Beat it and it will pair up with you";

        if (_mode != Mode.Ground || IsGripping || Wildlife.MateNear(GlobalPosition, CourtshipRange) is not { } mate)
        {
            _courtship = 0f;
            return;
        }

        // Snow leopards only breed once they're grown, at two or three years old; a cub has to grow up first.
        if (!IsGrownUp)
        {
            _courtship = 0f;
            Courtship = "Grow up first, then stay with the snow leopard to start a family";
            return;
        }

        // Sometimes it wants no mate, and turns on the player's cat instead.
        if (!Wildlife.WillMate(mate))
        {
            _courtship = 0f;
            Courtship = "The snow leopard doesn't want a mate and is fighting you! Beat it and it will pair up with you";
            return;
        }

        _courtship += dt;
        if (_courtship < CourtshipSeconds)
        {
            Courtship = $"Stay with the snow leopard to start a family: {Mathf.CeilToInt(CourtshipSeconds - _courtship)}";
            return;
        }

        _courtship = 0f;
        PairUp(family, mate, "");
    }

    /// <summary>The wild snow leopard leaves the wild to join the player's family, and they have their first cub.</summary>
    private void PairUp(Herd family, Animal mate, string news)
    {
        family.Recruit(1f, Wildlife!.Leave(mate));
        family.Recruit();
        Announce($"{news} You have a {Animal.YoungName.ToLower()}! Your mate and cub will hunt with you.".TrimStart(), 6f);
    }
}
