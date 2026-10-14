using System;
using Godot;

namespace IceAgeWorld;

/// <summary>What a chore asks the player to do, and so what counts towards it.</summary>
public enum ChoreKind { Travel, Swim, Fly, Graze, Drink, EatMeat, Hunt, Climb, Rest, Underwater, Shellfish }

/// <summary>
/// A small job that pays coins when it's done, the way Savannah Life pays its players for getting on with life as an
/// animal: roam, drink, graze, hunt. The player always has a few on the go (see <see cref="Player.Chores"/>), and
/// each one finished is swapped for a new one.
/// </summary>
public sealed class Chore
{
    public required ChoreKind Kind { get; init; }

    /// <summary>What to do, e.g. "Travel 500 metres".</summary>
    public required string Task { get; init; }

    /// <summary>How much of it to do, in <see cref="Unit"/>s.</summary>
    public required float Target { get; init; }

    /// <summary>What the target is counted in, shown after the numbers: "m", "s", or nothing for a plain count.</summary>
    public string Unit { get; init; } = "";

    /// <summary>Coins paid once it's done.</summary>
    public required int Reward { get; init; }

    /// <summary>Which animals can do it: only hunters hunt, only fliers fly.</summary>
    public Func<AnimalStats, bool> CanDo { get; init; } = _ => true;

    /// <summary>How much has been done so far.</summary>
    public float Progress { get; set; }

    public bool IsDone => Progress >= Target;

    /// <summary>The chore as the HUD lists it, e.g. "Travel 500 metres  230/500 m  (60 coins)".</summary>
    public override string ToString() =>
        $"{Task}  {Mathf.FloorToInt(Mathf.Min(Progress, Target))}/{Target}{(Unit.Length > 0 ? " " + Unit : "")}  ({Reward} coins)";

    /// <summary>
    /// Every chore there is. Rewards go with how long a chore takes, about a coin a second, with a little extra for
    /// the harder ones like hunting, so ten or so chores buy a mammoth.
    /// </summary>
    private static readonly Func<Chore>[] All =
    [
        () => new Chore { Kind = ChoreKind.Travel, Task = "Travel 500 metres", Target = 500, Unit = "m", Reward = 60 },
        () => new Chore { Kind = ChoreKind.Travel, Task = "Travel 1 kilometre", Target = 1000, Unit = "m", Reward = 120 },
        // Fliers cross water overhead, so swimming is for everything else.
        () => new Chore { Kind = ChoreKind.Swim, Task = "Swim 100 metres", Target = 100, Unit = "m", Reward = 70, CanDo = s => !s.Can(Ability.Fly) },
        () => new Chore { Kind = ChoreKind.Fly, Task = "Fly 1 kilometre", Target = 1000, Unit = "m", Reward = 100, CanDo = s => s.Can(Ability.Fly) },
        () => new Chore { Kind = ChoreKind.Graze, Task = "Eat 20 mouthfuls of grass", Target = 20, Reward = 60, CanDo = s => s.CanGraze },
        () => new Chore { Kind = ChoreKind.Drink, Task = "Drink water for 20 seconds", Target = 20, Unit = "s", Reward = 50 },
        () => new Chore { Kind = ChoreKind.EatMeat, Task = "Eat 15 mouthfuls of meat", Target = 15, Reward = 80, CanDo = s => s.Can(Ability.Hunt) },
        () => new Chore { Kind = ChoreKind.Hunt, Task = "Hunt an animal", Target = 1, Reward = 100, CanDo = s => s.Can(Ability.Hunt) },
        () => new Chore { Kind = ChoreKind.Hunt, Task = "Hunt 3 animals", Target = 3, Reward = 250, CanDo = s => s.Can(Ability.Hunt) },
        () => new Chore { Kind = ChoreKind.Climb, Task = "Climb 3 trees", Target = 3, Reward = 90, CanDo = s => s.Can(Ability.ClimbTrees) },
        () => new Chore { Kind = ChoreKind.Underwater, Task = "Stay underwater for 30 seconds", Target = 30, Unit = "s", Reward = 60, CanDo = s => s.Can(Ability.Dive) },
        () => new Chore { Kind = ChoreKind.Shellfish, Task = "Dive for 3 shellfish and eat them", Target = 3, Reward = 90, CanDo = s => s.Can(Ability.Dive) },
        () => new Chore { Kind = ChoreKind.Rest, Task = "Sit or lie down for 30 seconds", Target = 30, Unit = "s", Reward = 30 },
    ];

    /// <summary>A new chore, picked at random, that an animal with <paramref name="stats"/> can do and that isn't <paramref name="taken"/> already.</summary>
    public static Chore? Pick(AnimalStats stats, Func<string, bool> taken)
    {
        var choices = Array.FindAll(Array.ConvertAll(All, make => make()), c => c.CanDo(stats) && !taken(c.Task));
        return choices.Length == 0 ? null : choices[GD.RandRange(0, choices.Length - 1)];
    }
}
