using Godot;

namespace IceAgeWorld;

/// <summary>
/// Growing up. The player is born a youngster, a calf, cub, pup or eaglet a fraction of its grown size, and grows into
/// an adult over several minutes of play. While small it is slower, jumps lower, wades out of its depth sooner, bites
/// softer and carries less, and the camera sits low and close beside it. It only grows while it is fed and watered.
/// Every animal the player switches to is the same age.
/// </summary>
public partial class Player
{
    /// <summary>Seconds of being fed and watered it takes a newborn to grow up.</summary>
    [Export] public float GrowUpSeconds { get; set; } = 600f;

    /// <summary>
    /// Size at birth, as a fraction of grown size. Real newborns are far smaller still by weight (a mammoth calf weighed
    /// about a twentieth of its mother), but at under half her height the youngster already reads as a baby.
    /// </summary>
    [Export] public float NewbornSize { get; set; } = 0.45f;

    /// <summary>How far the animal has grown up, from 0 (newborn) to 1 (adult).</summary>
    public float Age { get; private set; }

    public bool IsGrownUp => Age >= 1f;

    /// <summary>Current size as a fraction of its kind's usual grown size; a leader grows past 1.</summary>
    public float Size => Mathf.Lerp(NewbornSize, 1f, Age) * Animal.Stats.LeaderSize;

    /// <summary>The current animal's speeds, reach and body, scaled to how far it has grown.</summary>
    public AnimalStats Stats { get; private set; } = null!;

    /// <summary>The size the body, collision capsule and camera were last fitted to; 0 when they need fitting afresh.</summary>
    private float _fittedSize;

    /// <summary>Ages the animal, as long as it isn't starving or parched, and refits it every time it has grown a little.</summary>
    private void Grow(float dt)
    {
        if (IsGrownUp || IsWeak)
            return;

        Age = Mathf.Min(1f, Age + dt / GrowUpSeconds);

        // Refitting rebuilds the collision shape, so do it in small steps rather than every frame.
        if (IsGrownUp || Size - _fittedSize >= 0.005f)
            FitToSize();
    }

    /// <summary>Scales the model, collision capsule, stats and camera to the animal's current size.</summary>
    private void FitToSize()
    {
        float size = Size;
        Stats = Animal.Stats.GrownTo(size);
        Animal.Growth = size;

        _collision.Shape = new CapsuleShape3D { Radius = Stats.BodyRadius, Height = Stats.BodyHeight };
        _collision.Position = new Vector3(0, Stats.BodyHeight / 2f, 0);
        _cameraPivot.Position = new Vector3(0, Stats.CameraHeight, 0);

        // A fresh animal starts at its usual camera distance; one that is growing keeps the player's zoom, pulled back with it.
        _springArm.SpringLength = _fittedSize > 0f ? _springArm.SpringLength * size / _fittedSize : Stats.CameraDistance;
        _fittedSize = size;
    }
}
