using System;
using System.Collections.Generic;
using Godot;

namespace IceAgeWorld;

/// <summary>
/// Chores: how the player earns coins. There are always a few on the HUD, such as "Travel 500 metres" or "Hunt an
/// animal", each paying coins once done and then swapped for a new one. Only what the current animal can do is set:
/// switching to an animal that can't do a chore (a reindeer can't hunt) swaps that chore for another.
/// </summary>
public partial class Player
{
    /// <summary>How many chores the player has on the go at once.</summary>
    private const int ChoreCount = 3;

    /// <summary>
    /// The furthest the animal can really move in one physics step, flying flat out. Anything further is a jump in
    /// place, e.g. a respawn, and doesn't count as travelling.
    /// </summary>
    private const float MaxStep = 5f;

    /// <summary>The chores the player is working on.</summary>
    public IReadOnlyList<Chore> Chores => _chores;

    /// <summary>A chore has just been finished, and its coins paid.</summary>
    public event Action<Chore>? ChoreDone;

    private readonly List<Chore> _chores = [];
    private Vector3 _lastPlace;

    /// <summary>Counts what the animal has done this step towards its chores: ground covered, swimming, flying, drinking and resting.</summary>
    private void TrackChores(float dt)
    {
        // Only ground covered counts, not height, so falling off a cliff or climbing a tree isn't travelling.
        float step = ((GlobalPosition - _lastPlace) with { Y = 0f }).Length();
        _lastPlace = GlobalPosition;
        if (step <= MaxStep)
        {
            DoChore(ChoreKind.Travel, step);
            if (IsSwimming)
                DoChore(ChoreKind.Swim, step);
            if (Animal.IsFlying)
                DoChore(ChoreKind.Fly, step);
        }
        if (_feeding == Feeding.Drinking && !_stopFeeding)
            DoChore(ChoreKind.Drink, dt);
        if (Animal.IsResting)
            DoChore(ChoreKind.Rest, dt);
        if (IsDiving)
            DoChore(ChoreKind.Underwater, dt);
    }

    /// <summary>A wild animal the player (or its pack) wounded has died.</summary>
    public void Hunted() => DoChore(ChoreKind.Hunt, 1f);

    /// <summary>Adds <paramref name="amount"/> to every chore of that kind, paying out and replacing any it finishes.</summary>
    private void DoChore(ChoreKind kind, float amount)
    {
        for (int i = 0; i < _chores.Count; i++)
        {
            var chore = _chores[i];
            if (chore.Kind != kind)
                continue;
            chore.Progress += amount;
            if (!chore.IsDone)
                continue;
            Wallet.Earn(chore.Reward);
            _chores.RemoveAt(i--);
            ChoreDone?.Invoke(chore);
        }
        FillChores();
    }

    /// <summary>Drops chores the current animal can't do and tops the list back up with new ones it can.</summary>
    private void FillChores()
    {
        _chores.RemoveAll(chore => !chore.CanDo(Stats));
        while (_chores.Count < ChoreCount && Chore.Pick(Stats, task => _chores.Exists(c => c.Task == task)) is { } chore)
            _chores.Add(chore);
    }
}
