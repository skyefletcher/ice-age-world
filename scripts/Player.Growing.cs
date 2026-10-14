using Godot;

namespace IceAgeWorld;

/// <summary>
/// The animal's size. The player is always a grown animal, from the start and after every rebirth, with no growing up
/// to wait through; only a pack leader is bigger than its kind's usual size. The model, collision capsule, stats and
/// camera are all fitted to that size.
/// </summary>
public partial class Player
{
    /// <summary>Size as a fraction of its kind's usual grown size; a leader is bigger than 1.</summary>
    public float Size => Animal.Stats.LeaderSize;

    /// <summary>The current animal's speeds, reach and body, scaled to its size.</summary>
    public AnimalStats Stats { get; private set; } = null!;

    /// <summary>The size the body, collision capsule and camera were last fitted to; 0 when they need fitting afresh.</summary>
    private float _fittedSize;

    /// <summary>Scales the model, collision capsule, stats and camera to the animal's size.</summary>
    private void FitToSize()
    {
        float size = Size;
        Stats = Animal.Stats.GrownTo(size);
        Animal.Growth = size;

        _collision.Shape = new CapsuleShape3D { Radius = Stats.BodyRadius, Height = Stats.BodyHeight };
        _collision.Position = new Vector3(0, Stats.BodyHeight / 2f, 0);

        // Snow leopards live on cliffs and crags, so a climber walks up slopes that would stop anything else,
        // right up the steepest mountainsides.
        FloorMaxAngle = Mathf.DegToRad(Stats.Can(Ability.ClimbTrees) ? 85f : 45f);
        _cameraPivot.Position = new Vector3(0, Stats.CameraHeight, 0);
        _springArm.SpringLength = Stats.CameraDistance;
        _fittedSize = size;
    }
}
