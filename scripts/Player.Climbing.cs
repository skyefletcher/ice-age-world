using Godot;

namespace IceAgeWorld;

/// <summary>
/// Tree climbing, for animals that can climb, anywhere on the tree. Walking into a trunk starts up it: the animal
/// clings on facing the bark, W climbs (which costs stamina), S climbs back down and A / D work round the trunk.
/// Beside any sturdy branch, E steps out onto it, and W / S walk out along it and back, the way snow leopards lie up
/// on limbs. Near the top of the trunk it scrambles up through the crown and stands on the very top of the tree, a
/// high lookout over its range, where A / D turn it round. Space leaps off from anywhere.
/// </summary>
public partial class Player
{
    /// <summary>How close to a trunk a climber has to walk to grab hold of it, beyond touching its solid column.</summary>
    private const float ClimbReach = 0.4f;

    /// <summary>Seconds to scramble from the top of the trunk up onto the treetop.</summary>
    private const float HopDuration = 0.7f;

    /// <summary>How fast the animal turns on the spot while standing on a treetop, in radians a second.</summary>
    private const float TreetopTurnSpeed = 2.5f;

    private Tree _tree;

    /// <summary>How far up the trunk the animal's feet are, above the tree's foot.</summary>
    private float _climbHeight;

    /// <summary>Which side of the trunk the animal clings to, as an angle about the trunk.</summary>
    private float _climbAngle;

    /// <summary>0 while clinging to the top of the trunk, 1 once standing on the treetop.</summary>
    private float _hop;

    /// <summary>Which way the animal faces while on the treetop.</summary>
    private float _treetopYaw;

    /// <summary>Which of the tree's branches the animal is walking along, and how far out, from root (0) to tip (1).</summary>
    private int _branch;
    private float _along;

    /// <summary>True while facing out towards the branch tip, false while facing back to the trunk.</summary>
    private bool _facingOut;

    /// <summary>True from leaping out of a tree until the animal is down on the ground again.</summary>
    private bool _fromTree;

    /// <summary>Seconds after touching down, forepaws first, for the hind legs to come down too.</summary>
    private const float SettleDuration = 0.35f;

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
        _climbAngle = Mathf.Atan2(-toTrunk.Z, -toTrunk.X);
        _hop = 0f;
        Velocity = Vector3.Zero;
        Animal.IsClimbing = true;

        // Swing the camera down to look up the trunk from below, rather than from up in the needles.
        _springArm.Rotation = new Vector3(0.25f, 0, 0);
        return true;
    }

    private void Climb(float dt, Vector2 input)
    {
        if (_mode == Mode.Branch)
        {
            WalkBranch(dt, input);
            return;
        }

        var outward = Outward(_climbAngle);
        var onTrunk = _tree.AxisAt(_climbHeight) + outward * (_tree.RadiusAt(_climbHeight) + 0.02f);

        if (_mode == Mode.Treetop)
        {
            Treetop(dt, input, onTrunk);
            return;
        }

        int? branch = BranchBeside();
        ActionPrompt = branch is null
            ? "W / S to climb up or down, A / D to go round, Space to leap off"
            : "E to step out onto the branch, W / S / A / D to climb, Space to leap off";
        if (Input.IsActionJustPressed(InputSetup.Jump))
        {
            LeapOff(outward, Stats.JumpBoost);
            return;
        }
        if (branch is { } stepOnto && Input.IsActionJustPressed(InputSetup.Eat))
        {
            StepOntoBranch(stepOnto);
            return;
        }

        // Forward on the stick climbs, back climbs down; going down is quicker, as gravity helps.
        float wanted = -input.Y;
        bool up = wanted > 0.1f && !IsExhausted;
        bool down = wanted < -0.1f;
        float rate = up ? ClimbSpeed : down ? -ClimbSpeed * 1.3f : 0f;
        _climbHeight += rate * dt;

        // Sideways works round the trunk, at the same pace whether it is a thick bole or a thin top, so it takes
        // longer to get round the wide base. Facing the bark, the animal's right is the way the angle shrinks.
        float sideways = IsExhausted ? 0f : input.X;
        float radius = _tree.RadiusAt(_climbHeight) + 0.3f;
        _climbAngle -= sideways * ClimbSpeed * 0.6f / radius * dt;

        UpdateNeeds(dt, exerting: up || sideways != 0f);
        UpdateStamina(dt, effort: up ? 1f : Mathf.Abs(sideways) * 0.5f);

        // On a slope the ground is higher on one side of the trunk than the other, so stop wherever it meets the paws.
        float ground = Terrain?.GetHeight(onTrunk.X, onTrunk.Z) ?? float.MinValue;
        if (down && (_climbHeight <= 0f || onTrunk.Y <= ground + 0.05f))
        {
            StepOffBottom(outward);
            return;
        }
        if (_climbHeight >= _tree.ClimbTop)
        {
            _climbHeight = _tree.ClimbTop;
            _mode = Mode.Treetop;
            _treetopYaw = Yaw(-outward);
        }

        // Cling on facing the bark, body tipped up the trunk so the paws grip it.
        GlobalPosition = onTrunk;
        float pitch = Mathf.Lerp(Animal.Rotation.X, Mathf.Pi / 2f, Mathf.Min(1f, 8f * dt));
        Animal.Rotation = new Vector3(pitch, Yaw(-outward), 0);
        bool moving = rate != 0f || sideways != 0f;
        Animal.Animate(moving ? Mathf.Max(Mathf.Abs(rate), ClimbSpeed * 0.6f) : 0f, moving ? 1f : 0f, 0f, dt);
    }

    /// <summary>The sturdy branch growing out of the trunk right beside the animal's paws, if there is one.</summary>
    private int? BranchBeside()
    {
        int? best = null;
        float bestScore = float.MaxValue;
        for (int i = 0; i < _tree.Branches.Count; i++)
        {
            float rise = Mathf.Abs(_tree.BranchHeight(i) - _climbHeight);
            var direction = _tree.BranchDirection(i);
            float turn = Mathf.Abs(Mathf.AngleDifference(_climbAngle, Mathf.Atan2(direction.Z, direction.X)));
            if (rise > 0.6f || turn > 0.7f || BranchStart(i) > _tree.Branches[i].Reach - 0.1f)
                continue;
            float score = rise + turn;
            if (score < bestScore)
            {
                bestScore = score;
                best = i;
            }
        }
        return best;
    }

    /// <summary>Fraction of the way along a branch where it comes out of the bark, so the walker starts clear of the trunk.</summary>
    private float BranchStart(int branch) =>
        Mathf.Min(0.4f, (_tree.RadiusAt(_tree.BranchHeight(branch)) + 0.15f) / _tree.BranchLength(branch));

    private void StepOntoBranch(int branch)
    {
        _mode = Mode.Branch;
        _branch = branch;
        _along = BranchStart(branch);
        _facingOut = true;
        Animal.IsClimbing = false;

        // Look out along the branch rather than up the trunk.
        _springArm.Rotation = new Vector3(-0.2f, 0, 0);
    }

    /// <summary>Walks out along a branch and back, balanced on top of the wood.</summary>
    private void WalkBranch(float dt, Vector2 input)
    {
        float length = _tree.BranchLength(_branch);
        float start = BranchStart(_branch);
        float reach = _tree.Branches[_branch].Reach;
        _climbHeight = _tree.BranchHeight(_branch);

        ActionPrompt = "W / S to walk out or back along the branch, Space to leap off";

        // Slow and careful: a narrow branch high up is no place to hurry.
        float wanted = -input.Y;
        float pace = Stats.WalkSpeed * 0.5f;
        _along = Mathf.Min(reach, _along + wanted * pace / length * dt);
        if (Mathf.Abs(wanted) > 0.1f)
            _facingOut = wanted > 0f;

        var direction = _tree.BranchDirection(_branch);
        if (Input.IsActionJustPressed(InputSetup.Jump))
        {
            LeapOff(_facingOut ? direction : -direction, Stats.JumpBoost);
            return;
        }

        // Back at the trunk, grab hold of it again just where the branch grows out.
        if (_along <= start && wanted < 0f)
        {
            _mode = Mode.Climbing;
            _climbAngle = Mathf.Atan2(direction.Z, direction.X);
            Animal.IsClimbing = true;
            _springArm.Rotation = new Vector3(0.25f, 0, 0);
            return;
        }
        _along = Mathf.Max(_along, start);

        UpdateNeeds(dt, exerting: false);
        UpdateStamina(dt, effort: 0f);

        // Branches rise or droop, so tip the body to follow the slope of the wood under the paws.
        var here = _tree.BranchTopAt(_branch, _along);
        var ahead = _tree.BranchTopAt(_branch, _along + 0.02f) - here;
        float slope = Mathf.Atan2(ahead.Y, new Vector2(ahead.X, ahead.Z).Length());
        float yaw = Yaw(_facingOut ? direction : -direction);

        GlobalPosition = here;
        Animal.Rotation = new Vector3(
            _facingOut ? slope : -slope,
            Mathf.LerpAngle(Animal.Rotation.Y, yaw, Mathf.Min(1f, Stats.TurnSpeed * dt)),
            0);
        bool moving = Mathf.Abs(wanted) > 0.1f && _along < reach;
        Animal.Animate(moving ? pace : 0f, moving ? 1f : 0f, 0f, dt);
    }

    /// <summary>Scrambles from the top of the trunk onto the very top of the tree and stands there until told to leave.</summary>
    private void Treetop(float dt, Vector2 input, Vector3 onTrunk)
    {
        bool arriving = _hop < 1f;
        _hop = Mathf.MoveToward(_hop, 1f, dt / HopDuration);
        float t = Mathf.SmoothStep(0f, 1f, _hop);
        GlobalPosition = onTrunk.Lerp(_tree.Summit, t);
        Animal.IsClimbing = t < 0.5f;

        // Once up, look out over the treetops rather than up the trunk.
        if (arriving && _hop >= 1f)
            _springArm.Rotation = new Vector3(-0.35f, 0, 0);

        float turn = _hop < 1f ? 0f : -input.X;
        _treetopYaw += turn * TreetopTurnSpeed * dt;
        Animal.Rotation = new Vector3(Mathf.Pi / 2f * (1f - t), _treetopYaw, 0);

        UpdateNeeds(dt, exerting: false);
        UpdateStamina(dt, effort: 0f);
        bool moving = _hop < 1f || turn != 0f;
        Animal.Animate(moving ? ClimbSpeed * 0.5f : 0f, moving ? 1f : 0f, 0f, dt);
        if (_hop < 1f)
            return;

        ActionPrompt = "A / D to turn, Space to leap down, S to climb down";
        if (Input.IsActionJustPressed(InputSetup.Jump))
        {
            LeapOff(new Vector3(-Mathf.Sin(_treetopYaw), 0f, -Mathf.Cos(_treetopYaw)), Stats.JumpBoost);
        }
        else if (input.Y > 0.5f)
        {
            // Back onto the trunk, just below the top so it doesn't scramble straight back up.
            _mode = Mode.Climbing;
            _hop = 0f;
            _climbHeight = _tree.ClimbTop - 0.05f;
            Animal.IsClimbing = true;
            _springArm.Rotation = new Vector3(0.25f, 0, 0);
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
        _fromTree = true;
        Animal.IsClimbing = false;
        Animal.Rotation = new Vector3(0, Yaw(direction), 0);
        Velocity = direction * (2f + boost) + Vector3.Up * Mathf.Sqrt(2f * _gravity * Stats.JumpHeight) * 0.5f;
    }

    /// <summary>
    /// After a leap out of a tree, tips the animal forward as it falls, the faster the further, so it lands on its
    /// forepaws, then lets its hindquarters down once it is on the ground.
    /// </summary>
    private void UpdateLanding(float dt)
    {
        if (_fromTree && !IsOnFloor() && !IsSwimming)
        {
            float target = Mathf.Clamp(-Velocity.Y / 6f, 0f, 1f);
            Animal.Landing = Mathf.MoveToward(Animal.Landing, target, 3f * dt);
            return;
        }
        _fromTree = false;
        Animal.Landing = Mathf.MoveToward(Animal.Landing, 0f, dt / SettleDuration);
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
        if (_mode is not (Mode.Climbing or Mode.Treetop or Mode.Branch))
            return;
        var axis = _tree.AxisAt(_climbHeight);
        var away = (GlobalPosition - (axis with { Y = GlobalPosition.Y })) with { Y = 0f };
        LeapOff(away.LengthSquared() > 0.0001f ? away.Normalized() : Outward(_climbAngle), 0f);
    }

    private static Vector3 Outward(float angle) => new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
}
