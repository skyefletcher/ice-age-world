using Godot;

namespace IceAgeWorld;

/// <summary>
/// Perching, for birds. Gliding in onto a sturdy branch lands on it, as eagles and owls drop onto a limb to rest,
/// watch for prey or eat. On the branch the bird walks along it with W / S, sits or lies down, and eats a kill as a
/// climber does (see Player.Climbing.cs), and Space takes off again. Flapping or diving flies straight past.
/// </summary>
public partial class Player
{
    /// <summary>How far round a branch's wood, beyond the bird's own body, its talons can reach out to grab hold.</summary>
    private const float PerchReach = 0.2f;

    /// <summary>
    /// How far above the wood the feet can be and still grab it, and how far below: a bird gliding down settles onto
    /// the branch from just above it, and can catch hold even if it has sunk a little past.
    /// </summary>
    private const float PerchAbove = 0.4f;
    private const float PerchBelow = 0.25f;

    /// <summary>No branch reaches further from its trunk than this, so trees further off can be skipped.</summary>
    private const float LongestBranch = 20f;

    /// <summary>Lands on a sturdy branch if the bird's feet are gliding onto one.</summary>
    private bool TryPerch()
    {
        if (Terrain is null || Velocity.Y > 0f)
            return false;

        var feet = GlobalPosition;
        float reach = Stats.BodyRadius + PerchReach;
        foreach (var tree in Terrain.Trees)
        {
            var offset = feet - tree.Transform.Origin;
            if (Mathf.Abs(offset.X) > LongestBranch || Mathf.Abs(offset.Z) > LongestBranch
                || offset.Y < 0f || feet.Y > tree.Summit.Y + PerchAbove)
                continue;

            for (int i = 0; i < tree.Branches.Count; i++)
            {
                // Step out along the branch to where it passes closest to the feet.
                float start = BranchStart(tree, i);
                float end = tree.Branches[i].Reach;
                int steps = Mathf.CeilToInt(tree.BranchLength(i) * (end - start) / (reach * 0.5f));
                for (int s = 0; s <= steps; s++)
                {
                    float along = Mathf.Lerp(start, end, s / (float)Mathf.Max(steps, 1));
                    var top = tree.BranchTopAt(i, along);
                    float above = feet.Y - top.Y;
                    if (above > PerchAbove || above < -PerchBelow)
                        continue;
                    if (new Vector2(feet.X - top.X, feet.Z - top.Z).Length() > reach)
                        continue;
                    Perch(tree, i, along);
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>Folds the wings and settles on the branch, facing whichever way along it is nearer the way it flew in.</summary>
    private void Perch(Tree tree, int branch, float along)
    {
        _tree = tree;
        _branch = branch;
        _along = along;
        _climbHeight = tree.BranchHeight(branch);
        _facingOut = Forward(Animal.Rotation.Y).Dot(tree.BranchDirection(branch)) >= 0f;
        _mode = Mode.Branch;

        Animal.IsFlying = false;
        Animal.Flap = Animal.Dive = 0f;
        Animal.Rotation = new Vector3(0, Animal.Rotation.Y, 0);
        Velocity = Vector3.Zero;
        GlobalPosition = tree.BranchTopAt(branch, along);
    }
}
