using System.Collections.Generic;
using Godot;

namespace IceAgeWorld;

/// <summary>
/// A bird's nest. Once the player's bald eagle or snowy owl is a grown female with a mate, she can build a nest where she
/// stands and lay her clutch in it (N): the eagle up a tree as real eagles do, or on the ground, and the owl only ever
/// on the ground, in a scrape on a hummock of the open tundra. Every five minutes one of the eggs hatches and the
/// chick joins her family, until all have hatched. Then she can lay another clutch, in the same nest or a new one,
/// with the same mate; the grown chicks of the last clutch fly off to live on their own as she does. The eggs go on
/// hatching while she is away, or playing as another animal.
/// </summary>
public partial class Player
{
    /// <summary>
    /// Eggs in the clutch: real bald eagles lay one to three, but here she lays five. A snowy owl lays more than any
    /// other owl, as many as eleven in a year when lemmings swarm; here she lays seven.
    /// </summary>
    private static int ClutchSize(Animal bird) => bird is SnowyOwl ? 7 : 5;

    /// <summary>Seconds between one egg hatching and the next, starting from when they're laid.</summary>
    private const float HatchSeconds = 300f;

    /// <summary>
    /// How big the nest is against a real one: the game's eagles are twice life size, and their nest is bigger still,
    /// about five metres across, a great heap that stands out from far off as the oldest real ones do. The owl's scrape
    /// is sized to the owl sitting in it.
    /// </summary>
    private static float NestSize(Animal bird) => bird is SnowyOwl ? 1.6f : 3f;

    /// <summary>Each bird's nest, once she has built it, and seconds until its next egg hatches.</summary>
    private readonly Dictionary<Animal, Nest> _nests = [];
    private readonly Dictionary<Animal, float> _hatchTimers = [];

    /// <summary>The nest of the bird the player is now, once she has built it.</summary>
    public Nest? BirdNest => _nests.GetValueOrDefault(Animal);

    /// <summary>Every nest the player's birds have built, whichever animal the player is now.</summary>
    public IEnumerable<Nest> Nests => _nests.Values;

    /// <summary>
    /// Where her eggs are, while she lies on them brooding, as a sitting bird does nearly all day and night until they
    /// hatch; her mate goes hunting and brings her food there meanwhile (see <see cref="Herd"/>).
    /// </summary>
    public Vector3? Brooding =>
        BirdNest is { Eggs: > 0 } nest && Posture == Posture.Lying
        && ((GlobalPosition - nest.Cup) with { Y = 0f }).Length() < nest.Radius && Mathf.Abs(GlobalPosition.Y - nest.Cup.Y) < 1.5f
            ? nest.Cup : null;

    /// <summary>
    /// Counts down to the next egg hatching in each nest; the chick climbs out and joins that bird's family, whichever
    /// animal the player is now.
    /// </summary>
    private void HatchEggs(float dt)
    {
        foreach (var (bird, nest) in _nests)
        {
            if (nest.Eggs == 0 || !_herds.TryGetValue(bird, out var family))
                continue;
            float timer = _hatchTimers[bird] - dt;
            if (timer <= 0f)
            {
                nest.Hatch();
                family.Recruit(at: nest.Cup);
                timer = HatchSeconds;
            }
            _hatchTimers[bird] = timer;
        }
    }

    /// <summary>
    /// Offers a grown female bird with a mate the chance to build her nest and lay, and once she has, says how long
    /// until the next egg hatches.
    /// </summary>
    private void TendNest()
    {
        if (Animal is not Bird)
            return;
        // Something is after her eggs: the family flies at it, however far the player is from the nest.
        var raider = Wildlife?.NestRaider;
        bool raided = raider is not null && Wildlife!.RaidedNest == BirdNest;
        if (raided)
            Family?.Defend(raider!);
        if (CourtshipPrompt is not null)
            return;
        string? errand = raided
            ? $"{Article(raider!.DisplayName)} {raider.DisplayName.ToLower()} is raiding your nest! Your mate is fighting it off"
            : Family?.Errand;
        if (BirdNest is { Eggs: > 0 } nest)
        {
            // How long until the next egg hatches, and, while she broods, what her mate is up to.
            int seconds = Mathf.CeilToInt(_hatchTimers[Animal]);
            string hatching = $"Next egg hatches in {seconds / 60}:{seconds % 60:00} ({nest.Eggs} {(nest.Eggs == 1 ? "egg" : "eggs")} left)";
            if (Brooding is null && Family is { HasMate: true })
                hatching += "\nLie down on the nest (X) and your mate will hunt for you";
            CourtshipPrompt = errand is not null ? $"{hatching}\n{errand}" : hatching;
            return;
        }
        CourtshipPrompt = errand;
        if (Sex != Sex.Female || !IsGrownUp || Family is not { HasMate: true } || _mode == Mode.Flying || IsSwimming)
            return;

        // Once the last clutch has hatched (or been eaten), she can nest again with the same mate. Standing on her old
        // nest she lays in it again; anywhere else she builds a new one, and the old one is abandoned.
        var old = BirdNest;
        bool onOld = old is not null && ((GlobalPosition - old.Cup) with { Y = 0f }).Length() < old.Radius
            && Mathf.Abs(GlobalPosition.Y - old.Cup.Y) < 1.5f;

        // A snowy owl nests only on the ground: there are no trees on the tundra where she breeds.
        bool owl = Animal is SnowyOwl;
        if (owl && _mode != Mode.Ground && !onOld)
        {
            CourtshipPrompt ??= "Snowy owls nest on the ground: land on open ground to build a nest";
            return;
        }

        CourtshipPrompt ??= onOld ? "Press N to lay another clutch of eggs in your nest"
            : old is not null ? "Press N to build a new nest and lay another clutch (or land on your old nest to use it again)"
            : "Press N to build a nest and lay your eggs";
        if (!Input.IsActionJustPressed(InputSetup.Nest))
            return;
        // A new clutch: the grown young of the last one leave to make their own way, and the mate stays.
        _herds[Animal].NewBrood();
        _hatchTimers[Animal] = HatchSeconds;
        if (onOld)
        {
            old!.Lay();
            return;
        }
        old?.QueueFree();
        var built = new Nest
        {
            Name = owl ? "OwlNest" : "EagleNest", Clutch = ClutchSize(Animal), Size = NestSize(Animal), Scrape = owl,
            Tree = PerchedIn?.Tree,
        };
        _nests[Animal] = built;
        GetParent().AddChild(built);
        built.Rotation = new Vector3(0f, Animal.Rotation.Y, 0f);
        // She sits on top of the nest, over her eggs: on the ground she climbs up onto it, and up a tree, where she
        // stays put on her branch or the top, it's built up under her.
        if (_mode == Mode.Ground)
        {
            built.GlobalPosition = GlobalPosition;
            GlobalPosition = built.Cup;
        }
        else
        {
            built.GlobalPosition = GlobalPosition + Vector3.Down * built.CupHeight;
        }
    }

    /// <summary>"An" before a vowel, as in an arctic wolf; "A" otherwise.</summary>
    private static string Article(string name) => "aeiou".Contains(char.ToLower(name[0])) ? "An" : "A";
}
