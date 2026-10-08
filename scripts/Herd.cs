using System;
using System.Collections.Generic;
using Godot;

namespace IceAgeWorld;

/// <summary>
/// Computer-controlled companions that travel with the player: a wolf pack or a mammoth herd. Each member keeps to its
/// own place around the leader, turned with the leader's heading, and hurries to catch up when left behind. A pack
/// fans out in a V just behind the leader, flanking it on the hunt. A herd ambles in a loose,
/// drifting crowd with its youngest in the middle and grazes whenever it stands still. Members follow the ground and
/// swim across water, step round tree trunks and keep out of each other's way, but they are not solid.
/// </summary>
public partial class Herd : Node3D
{
    /// <summary>The player animal the companions follow.</summary>
    public Player Leader { get; init; } = null!;

    /// <summary>Builds one companion; called once per member.</summary>
    public Func<Animal> Breed { get; init; } = null!;

    public int Count { get; init; }

    private sealed class Member
    {
        public required Node3D Body { get; init; }
        public required Animal Animal { get; init; }

        /// <summary>Where the member keeps to, relative to the leader: +Z is behind, +X to the leader's right.</summary>
        public required Vector3 Slot { get; init; }

        public Vector3 Velocity;
        public float Yaw;
        public float StillTime;
        public float GrazeTimer;
    }

    private readonly List<Member> _members = [];
    private bool _isHerd;
    private float _spacing;
    private float _time;

    public override void _Ready()
    {
        // The members move about the world on their own, not with the player node they hang off.
        TopLevel = true;

        for (int i = 0; i < Count; i++)
        {
            var animal = Breed();
            var body = new Node3D { Name = animal.GetType().Name + i };
            body.AddChild(animal);
            AddChild(body);

            _isHerd = animal.Stats.Can(Ability.Herd);
            _spacing = animal.Stats.BodyRadius * 2f + (_isHerd ? 2.5f : 1.2f);

            // Real herds and packs are of mixed ages and sizes; the last of a herd is a calf.
            float size = _isHerd && i == Count - 1 ? 0.6f : 1f - 0.06f * (i % 3);
            body.Scale = Vector3.One * size;

            _members.Add(new Member { Body = body, Animal = animal, Slot = SlotFor(i) * _spacing });
        }
    }

    /// <summary>A pack fans out behind in a V, alternating sides; a herd spreads round and behind, with the calf tucked inside.</summary>
    private Vector3 SlotFor(int index)
    {
        if (!_isHerd)
            return new Vector3(index % 2 == 0 ? -0.9f : 0.9f, 0f, 0.4f + index * 0.8f);

        Vector3[] crowd = [new(-1.1f, 0f, 0.3f), new(1.1f, 0f, 0.5f), new(-0.5f, 0f, 1.5f), new(0.4f, 0f, 0.9f)];
        return index < crowd.Length ? crowd[index] : new Vector3((index % 2 - 0.5f) * 2f, 0f, index * 0.6f);
    }

    /// <summary>Brings every member to its place round the leader, e.g. when the player becomes this kind of animal.</summary>
    public void Gather()
    {
        float yaw = Leader.Animal.Rotation.Y;
        foreach (var member in _members)
        {
            var spot = Leader.GlobalPosition + member.Slot.Rotated(Vector3.Up, yaw);
            spot.Y = Leader.Terrain?.GetHeight(spot.X, spot.Z) ?? Leader.GlobalPosition.Y;
            member.Body.GlobalPosition = spot;
            member.Velocity = Vector3.Zero;
            member.Yaw = yaw;
            member.Animal.Rotation = new Vector3(0f, yaw, 0f);
            member.Animal.Sitting = member.Animal.Lying = 0f;
        }
    }

    public override void _Process(double delta)
    {
        if (!IsVisibleInTree())
            return;

        float dt = (float)delta;
        _time += dt;
        for (int i = 0; i < _members.Count; i++)
            Move(_members[i], i, dt);
    }

    private void Move(Member member, int index, float dt)
    {
        var stats = member.Animal.Stats;
        var terrain = Leader.Terrain;
        float leaderYaw = Leader.Animal.Rotation.Y;
        var position = member.Body.GlobalPosition;

        // Herd members drift slowly about their places, so the crowd never looks drilled.
        var slot = member.Slot;
        if (_isHerd)
            slot += new Vector3(Mathf.Sin(_time * 0.13f + index * 2f), 0f, Mathf.Cos(_time * 0.11f + index * 3f)) * _spacing * 0.3f;
        var target = Leader.GlobalPosition + slot.Rotated(Vector3.Up, leaderYaw);

        // Too far behind to catch up (e.g. the leader respawned): turn up at its place.
        var toTarget = (target - position) with { Y = 0f };
        float distance = toTarget.Length();
        if (distance > 60f)
        {
            Gather();
            return;
        }

        // Stroll when nearly there, and run flat out when well behind.
        float speed = distance < 0.8f ? 0f : Mathf.Min(stats.SprintSpeed, (distance - 0.8f) * 1.5f);
        var wanted = distance > 0.01f ? toTarget / distance * speed : Vector3.Zero;

        // Once they've stood about for a while, members sit or lie down when the leader does, one after another rather
        // than all at once, and get up again with it. A member stays put until it is back on its feet.
        var posture = member.StillTime > 1f + index * 0.7f ? Leader.Posture : Posture.Standing;
        member.Animal.Settle(posture, Leader.PostureChangeSeconds, dt);
        if (member.Animal.IsResting)
            wanted = Vector3.Zero;

        // Keep a body's length from the others.
        foreach (var other in _members)
        {
            if (other == member)
                continue;
            var away = (position - other.Body.GlobalPosition) with { Y = 0f };
            float gap = away.Length();
            if (gap < _spacing * 0.7f && gap > 0.001f)
                wanted += away / gap * (_spacing * 0.7f - gap) * 3f;
        }

        member.Velocity = member.Velocity.Lerp(wanted, Mathf.Min(1f, stats.Acceleration * dt));
        position += member.Velocity * dt;

        // Step round tree trunks rather than through them.
        if (terrain?.NearestTree(position, Terrain.TrunkCollisionRadius + stats.BodyRadius) is { } tree)
        {
            var fromTrunk = (position - tree.Transform.Origin) with { Y = 0f };
            position += fromTrunk.Normalized() * (Terrain.TrunkCollisionRadius + stats.BodyRadius - fromTrunk.Length());
        }

        // Walk on the ground, or float at swimming depth over deep water.
        float ground = terrain?.GetHeight(position.X, position.Z) ?? position.Y;
        float? surface = Leader.Water?.SurfaceAt(position);
        bool swimming = surface.HasValue && surface.Value - ground > stats.FloatDepth;
        float height = swimming ? surface!.Value - stats.FloatDepth : ground;
        position.Y = Mathf.Lerp(position.Y, height, Mathf.Min(1f, 10f * dt));
        member.Body.GlobalPosition = position;

        // Face the way it's going, or idly turn to match the leader when standing about.
        float moving = member.Velocity.Length();
        float facing = moving > 0.3f ? Mathf.Atan2(-member.Velocity.X, -member.Velocity.Z) : leaderYaw;
        member.Yaw = Mathf.LerpAngle(member.Yaw, facing, Mathf.Min(1f, (moving > 0.3f ? stats.TurnSpeed : 0.5f) * dt));
        member.Animal.Rotation = new Vector3(0f, member.Yaw, 0f);

        member.Animal.IsSwimming = swimming;
        float stride = swimming ? 1f : Mathf.Clamp(moving / stats.WalkSpeed, 0f, 1f);
        member.Animal.Animate(swimming ? moving + 2f : moving, stride, Graze(member, moving, swimming, dt), dt);
    }

    /// <summary>
    /// Grazers standing about lower their heads for a mouthful every few seconds. Returns 0..1 how far the head is dipped.
    /// Each member keeps its own rhythm so they don't all bob in unison.
    /// </summary>
    private float Graze(Member member, float moving, bool swimming, float dt)
    {
        member.StillTime = moving < 0.3f && !swimming ? member.StillTime + dt : 0f;
        if (!member.Animal.Stats.CanGraze || member.StillTime < 1.5f || member.Animal.IsResting)
        {
            member.GrazeTimer = 0f;
            return 0f;
        }

        // A 1.4 s mouthful, then a pause that differs from member to member.
        float period = 3.5f + member.Slot.Length() % 2f;
        member.GrazeTimer = (member.GrazeTimer + dt) % period;
        return member.GrazeTimer < 1.4f ? Mathf.Min(1f, Mathf.Sin(member.GrazeTimer / 1.4f * Mathf.Pi) * 1.6f) : 0f;
    }
}
