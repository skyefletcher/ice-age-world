using Godot;

namespace IceAgeWorld;

/// <summary>
/// Tree climbing, for animals that can climb. Walking into a trunk starts up it: the animal clings on facing the
/// bark, W climbs (which costs stamina) and S climbs back down. It spirals round as it goes so that at the top it
/// comes out beside the tree's perching limb, then turns and steps out along it to rest, the way snow leopards
/// lie up on branches. Space leaps off, from the trunk or the limb.
/// </summary>
public partial class Player
{
    /// <summary>How close to a trunk a climber has to walk to grab hold of it, beyond touching its solid column.</summary>
    private const float ClimbReach = 0.4f;

    /// <summary>Seconds to step from the trunk onto the perching limb.</summary>
    private const float HopDuration = 0.5f;

    private Tree _tree;

    /// <summary>How far up the trunk the animal's feet are, above the tree's foot.</summary>
    private float _climbHeight;

    // Where round the trunk the animal started climbing, and where the perching limb is, as angles about the trunk.
    private float _startAngle;
    private float _perchAngle;

    /// <summary>0 while clinging to the trunk at the top, 1 once settled on the limb.</summary>
    private float _hop;

    /// <summary>Climbing speed up the trunk: agile animals scramble up fast.</summary>
    private float ClimbSpeed => 1f + Stats.Scores.Agility * 0.25f;

    /// <summary>Starts climbing if the animal is walking straight into a tree trunk.</summary>
    private bool TryStartClimb(Vector3 direction)
    {
        if (Terrain is null || IsExhausted || IsFeeding)
            return false;
        if (Terrain.NearestTree(GlobalPosition, Terrain.TrunkCollisionRadius + Stats.BodyRadius + ClimbReach) is not { } tree)
            return false;

        var toTrunk = (tree.Transform.Origin - GlobalPosition) with { Y = 0f };
        if (direction.Dot(toTrunk.Normalized()) < 0.7f)
            return false;

        _tree = tree;
        _mode = Mode.Climbing;
        _climbHeight = Mathf.Max(0f, GlobalPosition.Y - tree.Transform.Origin.Y);
        _startAngle = Mathf.Atan2(-toTrunk.Z, -toTrunk.X);
        _perchAngle = Mathf.Atan2(tree.PerchDirection.Z, tree.PerchDirection.X);
        _hop = 0f;
        Velocity = Vector3.Zero;
        Animal.IsClimbing = true;

        // Swing the camera down to look up the trunk from below, rather than from up in the needles.
        _springArm.Rotation = new Vector3(0.25f, 0, 0);
        return true;
    }

    private void Climb(float dt, Vector2 input)
    {
        float perch = _tree.PerchClimb;
        var outward = Outward(Mathf.LerpAngle(_startAngle, _perchAngle, Mathf.Clamp(_climbHeight / perch, 0f, 1f)));
        var onTrunk = _tree.AxisAt(_climbHeight) + outward * (_tree.RadiusAt(_climbHeight) + 0.02f);

        if (_mode == Mode.Perched)
        {
            Perch(dt, input, outward, onTrunk);
            return;
        }

        ActionPrompt = "W / S to climb up or down, Space to leap off";
        if (Input.IsActionJustPressed(InputSetup.Jump))
        {
            LeapOff(outward, Stats.JumpBoost);
            return;
        }

        // Forward on the stick climbs, back climbs down; going down is quicker, as gravity helps.
        float wanted = -input.Y;
        bool up = wanted > 0.1f && !IsExhausted;
        bool down = wanted < -0.1f;
        float rate = up ? ClimbSpeed : down ? -ClimbSpeed * 1.3f : 0f;
        _climbHeight += rate * dt;
        UpdateNeeds(dt, exerting: up);
        UpdateStamina(dt, effort: up ? 1f : 0f);

        // On a slope the ground is higher on one side of the trunk than the other, so stop wherever it meets the paws.
        float ground = Terrain?.GetHeight(onTrunk.X, onTrunk.Z) ?? float.MinValue;
        if (down && (_climbHeight <= 0f || onTrunk.Y <= ground + 0.05f))
        {
            StepOffBottom(outward);
            return;
        }
        if (_climbHeight >= perch)
        {
            _climbHeight = perch;
            _mode = Mode.Perched;
        }

        // Cling on facing the bark, body tipped up the trunk so the paws grip it.
        GlobalPosition = onTrunk;
        float pitch = Mathf.Lerp(Animal.Rotation.X, Mathf.Pi / 2f, Mathf.Min(1f, 8f * dt));
        Animal.Rotation = new Vector3(pitch, Yaw(-outward), 0);
        Animal.Animate(Mathf.Abs(rate), rate != 0f ? 1f : 0f, 0f, dt);
    }

    /// <summary>Steps from the top of the trunk onto the limb and lies up there, resting, until told to leave.</summary>
    private void Perch(float dt, Vector2 input, Vector3 outward, Vector3 onTrunk)
    {
        _hop = Mathf.MoveToward(_hop, 1f, dt / HopDuration);
        float t = Mathf.SmoothStep(0f, 1f, _hop);
        var along = _tree.PerchDirection;
        GlobalPosition = onTrunk.Lerp(_tree.PerchSpot, t);
        Animal.Rotation = new Vector3(Mathf.Pi / 2f * (1f - t), Mathf.LerpAngle(Yaw(-outward), Yaw(along), t), 0);
        Animal.IsClimbing = t < 0.5f;

        UpdateNeeds(dt, exerting: false);
        UpdateStamina(dt, effort: 0f);
        Animal.Animate(_hop < 1f ? ClimbSpeed : 0f, _hop < 1f ? 1f : 0f, 0f, dt);
        if (_hop < 1f)
            return;

        ActionPrompt = "Space to leap down, S to climb down";
        if (Input.IsActionJustPressed(InputSetup.Jump))
        {
            LeapOff(along, Stats.JumpBoost);
        }
        else if (input.Y > 0.5f)
        {
            // Back onto the trunk, just below the limb so it doesn't step straight back out.
            _mode = Mode.Climbing;
            _hop = 0f;
            _climbHeight = _tree.PerchClimb - 0.05f;
            Animal.IsClimbing = true;
        }
    }

    /// <summary>Springs away from the tree, landing on the ground beyond its trunk.</summary>
    private void LeapOff(Vector3 direction, float boost)
    {
        // Start clear of the trunk's solid column so the body doesn't snag on it on the way down.
        var axis = _tree.AxisAt(_climbHeight);
        var from = new Vector3(GlobalPosition.X, 0f, GlobalPosition.Z) - new Vector3(axis.X, 0f, axis.Z);
        float clear = Terrain.TrunkCollisionRadius + Stats.BodyRadius + 0.05f;
        if (from.Length() < clear)
            GlobalPosition = new Vector3(axis.X, GlobalPosition.Y, axis.Z) + direction * clear;

        _mode = Mode.Ground;
        Animal.IsClimbing = false;
        Animal.Rotation = new Vector3(0, Yaw(direction), 0);
        Velocity = direction * (2f + boost) + Vector3.Up * Mathf.Sqrt(2f * _gravity * Stats.JumpHeight) * 0.5f;
    }

    /// <summary>Back on the ground at the foot of the trunk, facing away from it.</summary>
    private void StepOffBottom(Vector3 outward)
    {
        var foot = _tree.Transform.Origin + outward * (Terrain.TrunkCollisionRadius + Stats.BodyRadius + 0.05f);
        float ground = Terrain?.GetHeight(foot.X, foot.Z) ?? foot.Y;
        GlobalPosition = new Vector3(foot.X, ground + 0.05f, foot.Z);
        _mode = Mode.Ground;
        Animal.IsClimbing = false;
        Animal.Rotation = new Vector3(0, Yaw(outward), 0);
        Velocity = Vector3.Zero;
    }

    /// <summary>Drops out of a tree on the spot, e.g. when switching to another animal mid-climb.</summary>
    private void LeaveTree()
    {
        if (_mode is not (Mode.Climbing or Mode.Perched))
            return;
        var axis = _tree.AxisAt(_climbHeight);
        LeapOff((GlobalPosition - (axis with { Y = GlobalPosition.Y })).Normalized(), 0f);
    }

    private static Vector3 Outward(float angle) => new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
}
