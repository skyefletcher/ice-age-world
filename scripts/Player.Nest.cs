using System.Linq;
using Godot;

namespace IceAgeWorld;

/// <summary>
/// A bald eagle's nest. Once the player's eagle is a grown female with a mate, she can build a nest where she stands,
/// up a tree as real eagles do or on the ground, and lay her clutch in it (N). Every five minutes one of the eggs
/// hatches and the eaglet joins her family, until all five have hatched. She lays only the one clutch. The eggs go on
/// hatching while she is away, or playing as another animal.
/// </summary>
public partial class Player
{
    /// <summary>Eggs in the clutch: real bald eagles lay one to three, but here she lays five.</summary>
    private const int ClutchSize = 5;

    /// <summary>Seconds between one egg hatching and the next, starting from when they're laid.</summary>
    private const float HatchSeconds = 300f;

    /// <summary>
    /// How big the nest is against a real one: the game's eagles are twice life size, and their nest is bigger still,
    /// about five metres across, a great heap that stands out from far off as the oldest real ones do.
    /// </summary>
    private const float NestSize = 3f;

    private Nest? _nest;
    private float _hatchTimer;

    /// <summary>The eagle's nest, once she has built it.</summary>
    public Nest? EagleNest => _nest;

    /// <summary>
    /// Where her eggs are, while she lies on them brooding, as a sitting eagle does nearly all day and night until they
    /// hatch; her mate goes hunting and brings her food there meanwhile (see <see cref="Herd"/>).
    /// </summary>
    public Vector3? Brooding =>
        _nest is { } nest && Animal is BaldEagle && Posture == Posture.Lying
        && ((GlobalPosition - nest.Cup) with { Y = 0f }).Length() < nest.Radius && Mathf.Abs(GlobalPosition.Y - nest.Cup.Y) < 1.5f
            ? nest.Cup : null;

    /// <summary>The eagle's family, whichever animal the player is now, so eaglets can join it while she's away.</summary>
    private Herd? EagleFamily => _herds.FirstOrDefault(h => h.Key is BaldEagle).Value;

    /// <summary>Counts down to the next egg hatching; the eaglet climbs out of the nest and joins the family.</summary>
    private void HatchEggs(float dt)
    {
        if (_nest is not { Eggs: > 0 } nest || EagleFamily is not { } family)
            return;
        _hatchTimer -= dt;
        if (_hatchTimer > 0f)
            return;
        nest.Hatch();
        family.Recruit(at: nest.Cup);
        _hatchTimer = HatchSeconds;
    }

    /// <summary>
    /// Offers a grown female eagle with a mate the chance to build her nest and lay, and once she has, says how long
    /// until the next egg hatches.
    /// </summary>
    private void TendNest()
    {
        if (Animal is not BaldEagle)
            return;
        // Something is after the eggs: the family flies at it, however far the player is from the nest.
        var raider = Wildlife?.NestRaider;
        if (raider is not null)
            Family?.Defend(raider);
        if (CourtshipPrompt is not null)
            return;
        if (_nest is { } nest)
        {
            // How long until the next egg hatches, and, while she broods, what her mate is up to.
            string? hatching = null;
            if (nest.Eggs > 0)
            {
                int seconds = Mathf.CeilToInt(_hatchTimer);
                hatching = $"Next egg hatches in {seconds / 60}:{seconds % 60:00} ({nest.Eggs} {(nest.Eggs == 1 ? "egg" : "eggs")} left)";
                if (Brooding is null && Family is { HasMate: true })
                    hatching += "\nLie down on the nest (X) and your mate will hunt for you";
            }
            string? errand = raider is not null
                ? $"{Article(raider.DisplayName)} {raider.DisplayName.ToLower()} is raiding your nest! Your mate is fighting it off"
                : Family?.Errand;
            CourtshipPrompt = hatching is not null && errand is not null ? $"{hatching}\n{errand}" : hatching ?? errand;
            return;
        }
        if (Sex != Sex.Female || !IsGrownUp || Family is not { HasMate: true } || _mode == Mode.Flying || IsSwimming)
            return;

        CourtshipPrompt = "Press N to build a nest and lay your eggs";
        if (!Input.IsActionJustPressed(InputSetup.Nest))
            return;
        _nest = new Nest { Name = "EagleNest", Clutch = ClutchSize, Size = NestSize, Tree = PerchedIn?.Tree };
        GetParent().AddChild(_nest);
        _nest.Rotation = new Vector3(0f, Animal.Rotation.Y, 0f);
        _hatchTimer = HatchSeconds;
        // She sits on top of the nest, over her eggs: on the ground she climbs up onto it, and up a tree, where she
        // stays put on her branch or the top, it's built up under her.
        if (_mode == Mode.Ground)
        {
            _nest.GlobalPosition = GlobalPosition;
            GlobalPosition = _nest.Cup;
        }
        else
        {
            _nest.GlobalPosition = GlobalPosition + Vector3.Down * _nest.CupHeight;
        }
    }

    /// <summary>"An" before a vowel, as in an arctic wolf; "A" otherwise.</summary>
    private static string Article(string name) => "aeiou".Contains(char.ToLower(name[0])) ? "An" : "A";
}
