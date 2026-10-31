using System;
using System.Collections.Generic;
using Godot;

namespace IceAgeWorld;

/// <summary>
/// Exploring: the first time the player reaches one of the world's famous places (see <see cref="Terrain.Landmarks"/>),
/// it is found, and the HUD names it and says what there is to do there.
/// </summary>
public partial class Player
{
    /// <summary>Raised when the player first reaches a landmark.</summary>
    public event Action<Landmark>? PlaceFound;

    /// <summary>The landmarks already found, by name.</summary>
    private readonly HashSet<string> _found = [];

    /// <summary>Notices any landmark the player has just come to for the first time.</summary>
    private void Explore()
    {
        if (Terrain is null)
            return;
        var here = new Vector2(GlobalPosition.X, GlobalPosition.Z);
        foreach (var landmark in Terrain.Landmarks)
        {
            if (here.DistanceTo(landmark.Centre) > landmark.Radius || !_found.Add(landmark.Name))
                continue;
            PlaceFound?.Invoke(landmark);
        }
    }
}
