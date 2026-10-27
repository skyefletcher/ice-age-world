using System.Linq;
using Godot;

namespace IceAgeWorld;

/// <summary>
/// Raids on the player's eagle nest. Eggs are a meal for any hunter that can get at them, so every few minutes while
/// the nest still has eggs in it, the nearest wild hunter comes for it: on the ground anything from a wolf or hyena to
/// a bear or big cat; up a tree only a snow leopard, the one hunter here that climbs. It goes straight to the nest,
/// climbing the trunk if it must, and eats the eggs one after another until it's driven off. The player's mate and
/// grown eaglets fly at it and rake it with their talons, as eagles do to defend a nest, and the player can fight it
/// too; hurt badly enough, it gives up and runs off.
/// </summary>
public partial class Wildlife
{
    /// <summary>Seconds between one raid and the chance of the next, chosen afresh each time.</summary>
    private const float RaidMin = 120f, RaidMax = 300f;

    /// <summary>How far off a hunter may be and still come for the nest.</summary>
    private const float RaidRange = 200f;

    /// <summary>Seconds a raider takes over each egg, breaking it open and lapping it up.</summary>
    private const float EggSeconds = 6f;

    /// <summary>Hurt this badly, once its health bar is no longer green, a raider gives up on the nest and runs.</summary>
    private const float RaiderGivesUp = HealthBar.High;

    /// <summary>The hunter after the nest, the nest itself, how it's getting on, and whether it has climbed up to it.</summary>
    private sealed class Raid
    {
        public required Beast Raider;
        public required Nest Nest;
        public float Eating;
        public bool Up;
    }

    private Raid? _raid;
    private float _raidWait = -1f;

    /// <summary>The wild animal raiding the player's nest, if one is.</summary>
    public Animal? NestRaider => _raid?.Raider.Animal;

    /// <summary>Now and then sends a hunter for the nest, and ends the raid once it's beaten off, dead or the eggs are gone.</summary>
    private void WatchNest(float dt)
    {
        var nest = Player?.EagleNest;
        if (_raid is { } raid)
        {
            if (raid.Raider.IsDead || nest != raid.Nest || nest.Eggs == 0 || raid.Raider.Health < RaiderGivesUp)
                EndRaid(raid);
            return;
        }
        if (nest is not { Eggs: > 0 })
            return;
        if (_raidWait < 0f)
            _raidWait = _rng.RandfRange(RaidMin, RaidMax);
        _raidWait -= dt;
        if (_raidWait > 0f)
            return;
        _raidWait = _rng.RandfRange(RaidMin, RaidMax);

        // A hunter going about its own business, not already hunting, fleeing or fighting, and able to reach the nest.
        bool inTree = nest.Tree.HasValue;
        var raider = _beasts.Values
            .Where(b => !b.IsDead && !b.OnTree && b.Animal is not Bird && b.Animal.Stats.Can(Ability.Hunt)
                && (!inTree || b.Animal is SnowLeopard) && b.Group.Fleeing <= 0f && b.Group.Prey is null
                && !b.Group.HuntsPlayer && !b.Group.ChallengesMate && b.Health >= HealthBar.High
                && b.Body.GlobalPosition.DistanceTo(nest.GlobalPosition) < RaidRange)
            .MinBy(b => b.Body.GlobalPosition.DistanceSquaredTo(nest.GlobalPosition));
        if (raider is not null)
            _raid = new Raid { Raider = raider, Nest = nest };
    }

    /// <summary>The raid is over: a raider up the tree drops back to the ground, and it runs off.</summary>
    private void EndRaid(Raid raid)
    {
        var raider = raid.Raider;
        _raid = null;
        if (raider.IsDead)
            return;
        if (raider.OnTree)
            FallFromTree(raider);
        raider.Group.Centre = raider.Group.Goal = raider.Body.GlobalPosition;
        Scare(raider.Group, raid.Nest.GlobalPosition, seconds: 25f);
    }

    /// <summary>
    /// Moves the raider, if <paramref name="beast"/> is one: to the nest, up the trunk if it's in a tree, and there it
    /// eats the eggs. Returns false for anything else, which moves as usual.
    /// </summary>
    private bool MoveRaider(Beast beast, float dt)
    {
        if (_raid is not { } raid || raid.Raider != beast)
            return false;
        var nest = raid.Nest;
        var stats = beast.Animal.Stats;
        float reach = nest.Radius + stats.BodyRadius * beast.Size * 0.5f;

        if (nest.Tree is { } tree)
        {
            if (!raid.Up)
            {
                // To the foot of the tree, then up the trunk on the side facing the nest.
                var fromTrunk = (beast.Body.GlobalPosition - tree.Transform.Origin) with { Y = 0f };
                if (fromTrunk.Length() > tree.CollisionRadius + stats.BodyRadius * beast.Size + 0.4f)
                {
                    RunTo(beast, tree.Transform.Origin, dt);
                    return true;
                }
                raid.Up = true;
                beast.Refuge = tree;
                beast.OnTree = true;
                beast.ClimbHeight = 0f;
                beast.ClimbAngle = Mathf.Atan2(fromTrunk.Z, fromTrunk.X);
                beast.Hop = 0f;
                beast.Velocity = Vector3.Zero;
            }

            float top = nest.GlobalPosition.Y - tree.Transform.Origin.Y;
            if (beast.ClimbHeight < top)
            {
                // Clinging to the bark, facing the trunk with its body tipped up it, as it climbs to a refuge.
                float speed = 1f + stats.Scores.Agility * 0.25f;
                beast.ClimbHeight = Mathf.Min(top, beast.ClimbHeight + speed * dt);
                var outward = new Vector3(Mathf.Cos(beast.ClimbAngle), 0f, Mathf.Sin(beast.ClimbAngle));
                beast.Body.GlobalPosition = tree.AxisAt(beast.ClimbHeight) + outward * (tree.RadiusAt(beast.ClimbHeight) + 0.02f);
                beast.Yaw = Yaw(-outward);
                beast.Animal.IsClimbing = true;
                var cling = Quaternion.FromEuler(new Vector3(Mathf.Pi / 2f, beast.Yaw, 0f));
                beast.Animal.Quaternion = beast.Animal.Quaternion.Normalized().Slerp(cling, Mathf.Min(1f, 8f * dt));
                if (Seen(beast))
                    beast.Animal.Animate(speed, 1f, 0f, dt);
                return true;
            }

            // Up on the edge of the nest itself.
            var side = (beast.Body.GlobalPosition - nest.GlobalPosition) with { Y = 0f };
            side = side.LengthSquared() > 0.0001f ? side.Normalized() : Vector3.Back;
            beast.Body.GlobalPosition = nest.Cup + side * nest.Radius * 0.5f;
            beast.Animal.IsClimbing = false;
            EatEggs(raid, beast, -side, dt);
            return true;
        }

        var toNest = (nest.GlobalPosition - beast.Body.GlobalPosition) with { Y = 0f };
        if (toNest.Length() > reach)
        {
            RunTo(beast, nest.GlobalPosition, dt);
            return true;
        }
        EatEggs(raid, beast, toNest.Normalized(), dt);
        return true;
    }

    /// <summary>Runs flat out over the ground towards <paramref name="goal"/>.</summary>
    private void RunTo(Beast beast, Vector3 goal, float dt)
    {
        var stats = beast.Animal.Stats;
        var position = beast.Body.GlobalPosition;
        var toward = (goal - position) with { Y = 0f };
        float speed = TopSpeed(beast) * 0.8f;
        var wanted = toward.LengthSquared() > 0.01f ? toward.Normalized() * speed : Vector3.Zero;
        beast.Velocity = beast.Velocity.Lerp(wanted, Mathf.Min(1f, stats.Acceleration * dt));
        position += beast.Velocity * dt;
        position.Y = _terrain.GroundBelow(position);
        beast.Body.GlobalPosition = position;
        float moving = (beast.Velocity with { Y = 0f }).Length();
        if (moving > 0.3f)
            beast.Yaw = Mathf.LerpAngle(beast.Yaw, Yaw(beast.Velocity), Mathf.Min(1f, stats.TurnSpeed * dt));
        beast.Animal.Rotation = new Vector3(0f, beast.Yaw, 0f);
        beast.Animal.Settle(Posture.Standing, 1f, dt);
        if (Seen(beast))
            beast.Animal.Animate(moving, Mathf.Clamp(moving / stats.WalkSpeed, 0f, 1f), 0f, dt);
    }

    /// <summary>Head down in the nest, it eats the eggs one by one, facing <paramref name="facing"/>.</summary>
    private void EatEggs(Raid raid, Beast beast, Vector3 facing, float dt)
    {
        beast.Velocity = Vector3.Zero;
        beast.Yaw = Mathf.LerpAngle(beast.Yaw, Yaw(facing), Mathf.Min(1f, 4f * dt));
        beast.Animal.Rotation = new Vector3(0f, beast.Yaw, 0f);
        beast.Animal.Settle(Posture.Standing, 1f, dt);
        raid.Eating += dt;
        if (raid.Eating >= EggSeconds)
        {
            raid.Eating = 0f;
            raid.Nest.Lose();
        }
        if (Seen(beast))
            beast.Animal.Animate(0f, 0f, 0.75f + 0.25f * Mathf.Sin(_time * 7f), dt);
    }
}
