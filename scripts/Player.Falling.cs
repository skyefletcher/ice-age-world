using Godot;

namespace IceAgeWorld;

/// <summary>
/// Falling and landing. Gravity pulls harder on the way down than up, so falls are quick and heavy. Leaping out of a
/// tree, a cat twists upright in the air within a body length, reaches its forepaws down for the ground and, in a long
/// fall, spreads its legs out wide to slow itself, as real cats do. It lands forepaws first and sinks into a crouch
/// that soaks up the blow, the deeper the harder it hits. A drop higher than the animal can take hurts it, and a long
/// enough one kills it.
/// </summary>
public partial class Player
{
    /// <summary>True from leaping out of a tree until the animal is down on the ground again.</summary>
    private bool _fromTree;

    /// <summary>Seconds after touching down, forepaws first, for the hind legs to come down too.</summary>
    private const float SettleDuration = 0.35f;

    /// <summary>Seconds a cat takes to twist upright in the air; a real one has done it within about a metre of falling.</summary>
    private const float RightingDuration = 0.35f;

    /// <summary>Seconds to rise back up out of the crouch after the hardest landing.</summary>
    private const float ImpactRecovery = 0.6f;

    /// <summary>
    /// Falling faster than this, in m/s, counts as a drop (off a cliff, say), so the animal reaches down to land as it
    /// would leaping out of a tree, rather than the end of an ordinary jump.
    /// </summary>
    private const float DropSpeed = 9f;

    /// <summary>
    /// Falling faster than this, in m/s, a cat spreads its legs out like a parachute, all the way by 10 m/s faster. Cats
    /// falling from high up do this once they stop speeding up, which is why they survive falls that shorter ones don't.
    /// </summary>
    private const float SpreadSpeed = 14f;

    /// <summary>How the animal was turned as it left the tree, clinging to the bark say, and which way it twists round to face.</summary>
    private Quaternion _rightingFrom;
    private float _rightingYaw;

    /// <summary>0..1 how far the animal has twisted upright since leaving the tree; 1 once it is level.</summary>
    private float _righting = 1f;

    /// <summary>Starts twisting the animal upright, from however it was turned, to face <paramref name="yaw"/> as it falls.</summary>
    private void StartRighting(float yaw)
    {
        _rightingFrom = Animal.Quaternion;
        _rightingYaw = yaw;
        _righting = 0f;
    }

    /// <summary>Puts an end to any fall in progress, e.g. on switching animal or respawning.</summary>
    private void StopFalling()
    {
        _fromTree = false;
        _righting = 1f;
        Animal.Landing = Animal.Impact = Animal.Spread = 0f;
    }

    /// <summary>
    /// The animal has hit the ground at <paramref name="speed"/> m/s. It crouches to take the blow, all the way after a
    /// drop of three times its own jump, and a drop beyond what it can take hurts it.
    /// </summary>
    private void TouchDown(float speed)
    {
        float drop = Animal.DropHeight(speed);
        Animal.Impact = Mathf.Max(Animal.Impact, Mathf.Clamp(drop / (Stats.JumpHeight * 3f), 0f, 1f));

        float damage = Stats.FallDamage(drop);
        if (damage <= 0f)
            return;
        Hurt(damage);
        if (!IsDead)
        {
            Announce(damage > 40f ? "Ouch! That fall hurt badly" : "Ouch! A hard landing", 3f);
        }
    }

    /// <summary>
    /// Poses the animal for falling and landing. After a leap out of a tree, or any drop faster than a jump, it tips
    /// forward as it falls, the faster the further, so it lands on its forepaws, then lets its hindquarters down once
    /// it is on the ground. A climber falling fast spreads its legs; every animal rises back out of its landing crouch.
    /// </summary>
    private void UpdateLanding(float dt)
    {
        bool falling = !IsOnFloor() && !IsSwimming;
        if (falling && (_fromTree || -Velocity.Y > DropSpeed))
        {
            float target = Mathf.Clamp(-Velocity.Y / 6f, 0f, 1f);
            Animal.Landing = Mathf.MoveToward(Animal.Landing, target, 3f * dt);
        }
        else
        {
            if (!falling)
                _fromTree = false;
            Animal.Landing = Mathf.MoveToward(Animal.Landing, 0f, dt / SettleDuration);
        }

        float spread = falling && Stats.Can(Ability.ClimbTrees) ? Mathf.Clamp((-Velocity.Y - SpreadSpeed) / 10f, 0f, 1f) : 0f;
        Animal.Spread = Mathf.MoveToward(Animal.Spread, spread, (spread > Animal.Spread ? 2f : 6f) * dt);
        Animal.Impact = Mathf.MoveToward(Animal.Impact, 0f, dt / ImpactRecovery);

        // Leaving a tree, the animal twists round from however it clung to the bark until it is level, facing the
        // way it leapt: head first, the way a falling cat turns.
        if (_righting < 1f)
        {
            _righting = Mathf.Min(1f, _righting + dt / RightingDuration);
            var upright = new Quaternion(Vector3.Up, _rightingYaw);
            Animal.Quaternion = _rightingFrom.Slerp(upright, Mathf.SmoothStep(0f, 1f, _righting));
        }
    }
}
