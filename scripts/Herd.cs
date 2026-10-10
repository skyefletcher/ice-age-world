using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace IceAgeWorld;

/// <summary>
/// Computer-controlled companions that travel with the player: a wolf pack or a mammoth herd. Each member keeps to its
/// own place around the leader, turned with the leader's heading, and hurries to catch up when left behind. A pack
/// crowds close about the leader in a loose bunch, at its sides and just behind. A herd ambles in a loose,
/// drifting crowd with its youngest in the middle and grazes whenever it stands still. Members follow the ground and
/// swim across water, step round tree trunks and keep out of each other's way, but they are not solid. When the
/// leader of a pack attacks a wild animal, the pack runs in, rings it and helps bring it down, and when the leader
/// eats from a kill, the pack crowds round and eats with it. A snow leopard's family (a mate and their cub) hunts and
/// feeds with the leader the same way, and whenever a cub grows up, another is born.
/// </summary>
public partial class Herd : Node3D
{
    /// <summary>The player animal the companions follow.</summary>
    public Player Leader { get; init; } = null!;

    /// <summary>Builds one companion; called once per member.</summary>
    public Func<Animal> Breed { get; init; } = null!;

    public int Count { get; init; }

    /// <summary>
    /// True for a snow leopard's family, which starts empty, until the leader finds a mate, and has a new cub each time
    /// the last one grows up, up to <see cref="MaxFamily"/>.
    /// </summary>
    public bool IsFamily { get; init; }

    /// <summary>
    /// The most a family grows to, mate and leader not counted among the cubs: every snow leopard is costly to draw, and
    /// real ones leave their mother at about two years old rather than staying on for good.
    /// </summary>
    private const int MaxFamily = 8;

    /// <summary>What the leader calls its companions in news about them.</summary>
    public string Word => IsFamily ? "family" : _isHerd ? "herd" : "pack";

    /// <summary>True once there is anyone travelling with the leader.</summary>
    public bool Any => _members.Count > 0;

    /// <summary>Where each companion is.</summary>
    public IEnumerable<Vector3> Positions => _members.Select(m => m.Body.GlobalPosition);

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

        /// <summary>How grown it is, as a fraction of full size; a newcomer starts small and grows.</summary>
        public float Size = 1f;

        /// <summary>The prey a snow leopard has its jaws clamped on, if any, and which side of it it hangs on.</summary>
        public Animal? Gripping;
        public Vector3 GripSide;

        /// <summary>Seconds it has held on so far, and seconds left getting its breath back before it can leap on again.</summary>
        public float GripTime;
        public float GripRest;
    }

    private readonly List<Member> _members = [];

    /// <summary>
    /// The wild animal the pack is helping the leader bring down, if any. Wolves hunt together: once the leader goes
    /// for something, the rest of the pack piles in.
    /// </summary>
    public Animal? Target { get; private set; }

    /// <summary>
    /// How hard each wolf in the pack bites, per second it worries at the prey, against the leader's single bites (see
    /// <see cref="Wildlife.Bite"/>): a few of them together make short work of a calf.
    /// </summary>
    private const float PackBiteStrength = 4f;

    /// <summary>
    /// How hard a family snow leopard's clamped jaws do damage, per second, using the special attack as the player's cat
    /// does (see <see cref="Player.GripStrength"/>), a little weaker than the leader's own.
    /// </summary>
    private const float FamilyGripStrength = 20f;

    /// <summary>
    /// Seconds a family snow leopard can hold on before it tires and lets go, about as long as the player's cat lasts on
    /// a full stamina bar, then seconds it bites and harries instead while it gets its breath back.
    /// </summary>
    private const float GripHoldSeconds = 7f;
    private const float GripRestSeconds = 4f;

    /// <summary>
    /// The pack gives up on its prey this many seconds after the leader last went for it, or once the leader is this
    /// far from it, so they don't chase on alone.
    /// </summary>
    private const float AttackPatience = 20f;
    private const float AttackLeash = 50f;

    private float _sinceOrder;

    /// <summary>The kill the pack is sharing with the leader, if it is eating from one.</summary>
    public Animal? Meal { get; private set; }

    /// <summary>
    /// How much of a carcass of unit bulk each wolf eats per second of feeding, about a third as fast as the leader takes
    /// mouthfuls, so the pack eats a kill down a good deal quicker than the leader would alone.
    /// </summary>
    private const float PackMealShare = 0.015f;

    /// <summary>Once the leader is this far from the kill, the pack leaves it and follows.</summary>
    private const float MealLeash = 6f;

    /// <summary>How fast the prey is running, and where it was last frame to work that out.</summary>
    private float _preySpeed;
    private Vector3 _preyWas;

    /// <summary>Size a newcomer is born at, and the seconds it takes to grow up.</summary>
    private const float YoungSize = 0.55f;
    private const float GrowUpSeconds = 120f;
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
            Fit(animal);

            // Real herds and packs are of mixed ages and sizes; the last of a herd is a calf.
            float size = _isHerd && i == Count - 1 ? 0.6f : 1f - 0.06f * (i % 3);
            body.Scale = Vector3.One * size;

            _members.Add(new Member { Body = body, Animal = animal, Slot = SlotFor(i) * _spacing });
        }
    }

    /// <summary>Works out how the companions keep together from what kind of animal they are.</summary>
    private void Fit(Animal animal)
    {
        _isHerd = animal.Stats.Can(Ability.Herd);
        // A pack keeps tight about its leader, close enough to touch; a herd spreads out more.
        _spacing = animal.Stats.BodyRadius * 2f + (_isHerd ? 2.5f : 0.6f);
    }

    /// <summary>
    /// A pack crowds in a loose bunch round its leader, at its sides and just behind, each wolf a different distance off
    /// in a spiral so they never fall into a line; a herd spreads round and behind,
    /// with the calf tucked inside.
    /// </summary>
    private Vector3 SlotFor(int index)
    {
        if (!_isHerd)
        {
            // Step round by the golden ratio each time and a little further out, as seeds pack a sunflower head, so each
            // wolf finds its own gap. They spread round the sides and back, up to the leader's shoulders but no further,
            // so nobody gets ahead of it.
            float angle = (index * 0.618f % 1f - 0.5f) * 3.8f;
            float reach = 1.1f + 0.45f * Mathf.Sqrt(index);
            return new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * reach;
        }

        Vector3[] crowd = [new(-1.1f, 0f, 0.3f), new(1.1f, 0f, 0.5f), new(-0.5f, 0f, 1.5f), new(0.4f, 0f, 0.9f)];
        return index < crowd.Length ? crowd[index] : new Vector3((index % 2 - 0.5f) * 2f, 0f, index * 0.6f);
    }

    /// <summary>
    /// A new member is born into the pack or herd, as a youngster at the leader's side, and grows up over a couple of
    /// minutes. A wolf the player killed in a fight is reborn this way, as one of the player's own pack. A grown animal
    /// can join too, <paramref name="size"/> 1, e.g. a snow leopard's mate, walking up from <paramref name="at"/>.
    /// </summary>
    public void Recruit(float size = YoungSize, Vector3? at = null)
    {
        var animal = Breed();
        var body = new Node3D { Name = animal.GetType().Name + _members.Count };
        body.AddChild(animal);
        AddChild(body);
        body.Scale = Vector3.One * size;
        if (_members.Count == 0)
            Fit(animal);

        var member = new Member { Body = body, Animal = animal, Slot = SlotFor(_members.Count) * _spacing, Size = size };
        float yaw = Leader.Animal.Rotation.Y;
        var spot = at ?? Leader.GlobalPosition + member.Slot.Rotated(Vector3.Up, yaw);
        spot.Y = Leader.Terrain?.GetHeight(spot.X, spot.Z) ?? Leader.GlobalPosition.Y;
        body.GlobalPosition = spot;
        member.Yaw = yaw;
        animal.Rotation = new Vector3(0f, yaw, 0f);
        _members.Add(member);
    }

    /// <summary>
    /// The leader has gone for a wild animal: a pack joins in and helps bring it down. A herd of grazers doesn't hunt.
    /// Returns true if the pack has only now joined in.
    /// </summary>
    public bool Attack(Animal prey)
    {
        if (_isHerd || !Any)
            return false;
        bool joining = Target != prey;
        Target = prey;
        _sinceOrder = 0f;
        return joining;
    }

    /// <summary>
    /// The leader has started eating from a kill: a pack crowds round and eats with it. Returns true if the pack has
    /// only now joined in.
    /// </summary>
    public bool Feast(Animal carcass)
    {
        if (_isHerd || !Any)
            return false;
        // The leader feeding means the hunt is over.
        Target = null;
        bool joining = Meal != carcass;
        Meal = carcass;
        return joining;
    }

    /// <summary>Brings every member to its place round the leader, e.g. when the player becomes this kind of animal.</summary>
    public void Gather()
    {
        Target = null;
        Meal = null;
        float yaw = Leader.Animal.Rotation.Y;
        foreach (var member in _members)
        {
            LetGo(member);
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
        // Out of sight (the player has become another animal), nobody keeps hold of anything.
        if (!IsVisibleInTree())
        {
            foreach (var member in _members)
                LetGo(member);
            return;
        }

        float dt = (float)delta;
        _time += dt;

        // The prey is down, or got away from the leader, or the leader lost interest: back to following.
        if (Target is { } prey)
        {
            _sinceOrder += dt;
            if (Leader.Wildlife?.IsAlive(prey) != true || Leader.IsDead || _sinceOrder > AttackPatience
                || Leader.GlobalPosition.DistanceTo(prey.GlobalPosition) > AttackLeash)
                Target = null;
            _preySpeed = _sinceOrder > dt ? ((prey.GlobalPosition - _preyWas) with { Y = 0f }).Length() / dt : 0f;
            _preyWas = prey.GlobalPosition;
        }

        // The kill is eaten, the leader has walked off, or a new hunt is on: the meal is over.
        if (Meal is { } meal && (Leader.Wildlife?.HasMeat(meal) != true || Leader.IsDead || Target is not null
            || Leader.GlobalPosition.DistanceTo(meal.GlobalPosition) > MealLeash))
            Meal = null;

        for (int i = 0; i < _members.Count; i++)
            Move(_members[i], i, dt);
    }

    private void Move(Member member, int index, float dt)
    {
        var stats = member.Animal.Stats;
        var terrain = Leader.Terrain;
        float leaderYaw = Leader.Animal.Rotation.Y;

        // A newcomer grows up as it travels with the others. In a family, a cub growing up means a new one is born.
        if (member.Size < 1f)
        {
            member.Size = Mathf.Min(1f, member.Size + (1f - YoungSize) / GrowUpSeconds * dt);
            member.Body.Scale = Vector3.One * member.Size;
            if (member.Size >= 1f && IsFamily && _members.Count <= MaxFamily)
            {
                Recruit();
                Leader.Announce($"Your cub is all grown up, and a new {Leader.Animal.YoungName.ToLower()} is born!");
            }
        }
        var position = member.Body.GlobalPosition;

        // Herd members drift slowly about their places, so the crowd never looks drilled; pack wolves mill about theirs,
        // a little quicker and closer, nosing about round the leader.
        var slot = member.Slot;
        if (_isHerd)
            slot += new Vector3(Mathf.Sin(_time * 0.13f + index * 2f), 0f, Mathf.Cos(_time * 0.11f + index * 3f)) * _spacing * 0.3f;
        else
            slot += new Vector3(Mathf.Sin(_time * 0.3f + index * 2f), 0f, Mathf.Cos(_time * 0.25f + index * 3f)) * _spacing * 0.25f;
        var target = Leader.GlobalPosition + slot.Rotated(Vector3.Up, leaderYaw);

        // On the attack, each wolf closes in on its own side of the prey, ringing it as wild packs do, and bites
        // whenever it's in reach.
        float bite = 0f;
        Vector3? faceAt = null;
        var prey = Target;
        member.GripRest = Mathf.Max(0f, member.GripRest - dt);
        if (member.Gripping is not null && (member.Gripping != prey || member.GripTime > GripHoldSeconds))
        {
            LetGo(member);
            member.GripRest = GripRestSeconds;
        }
        if (member.Gripping is not null)
        {
            HoldOn(member, dt);
            return;
        }
        if (prey is not null)
        {
            var wildlife = Leader.Wildlife!;
            float ring = prey.Stats.BodyRadius * wildlife.SizeOf(prey) + stats.BodyRadius * member.Size + 0.3f;
            float angle = index * Mathf.Tau / _members.Count + _time * 0.3f;
            target = prey.GlobalPosition + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ring;
            if (((position - prey.GlobalPosition) with { Y = 0f }).Length() < ring + 0.8f)
            {
                // A snow leopard that has its breath leaps on and clamps its jaws, as the player's cat can; anything
                // else, or a cat still winded, bites and lets go. A youngster bites with a youngster's jaws.
                if (stats.Can(Ability.Grip) && member.GripRest <= 0f)
                {
                    var side = (position - prey.GlobalPosition) with { Y = 0f };
                    member.GripSide = side.LengthSquared() > 0.0001f ? side.Normalized() : Vector3.Back;
                    member.Gripping = prey;
                    member.GripTime = 0f;
                    wildlife.Grip(prey, true);
                    HoldOn(member, dt);
                    return;
                }
                wildlife.Bite(prey, PackBiteStrength * member.Size * member.Size * dt, position);
                bite = 0.6f + 0.4f * Mathf.Sin(_time * 12f + index);
                faceAt = prey.GlobalPosition;
            }
        }
        else if (Meal is { } meal)
        {
            // Sharing a kill with the leader, each wolf takes its own place round the carcass and tears at it, head
            // down, in short tugs, as a pack crowds in to feed together.
            var wildlife = Leader.Wildlife!;
            float ring = meal.Stats.BodyRadius * wildlife.SizeOf(meal) + stats.BodyRadius * member.Size + 0.2f;
            float angle = (index + 1) * Mathf.Tau / (_members.Count + 1) + Leader.Animal.Rotation.Y;
            target = meal.GlobalPosition + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ring;
            if (((position - meal.GlobalPosition) with { Y = 0f }).Length() < ring + 0.6f)
            {
                // A youngster eats a youngster's share.
                wildlife.EatFrom(meal, PackMealShare * member.Size * dt);
                bite = 0.75f + 0.25f * Mathf.Sin(_time * 7f + index * 1.7f);
                faceAt = meal.GlobalPosition;
            }
        }

        // Too far behind to catch up (e.g. the leader respawned): turn up at its place.
        var toTarget = (target - position) with { Y = 0f };
        float distance = toTarget.Length();
        if (distance > 60f && prey is null)
        {
            Gather();
            return;
        }

        // Keep pace with the leader on the move, so members stay at their places rather than trailing behind, and run
        // flat out to catch up when well behind.
        float leaderSpeed = (Leader.Velocity with { Y = 0f }).Length();
        float speed = distance < 0.3f ? 0f : Mathf.Min(stats.SprintSpeed, Mathf.Max(0f, distance - 0.3f) * 1.5f + leaderSpeed);

        // On the attack, close right in and keep pace with the prey as it runs, rather than easing off short of it.
        if (prey is not null)
            speed = Mathf.Min(stats.SprintSpeed, distance * 3f + _preySpeed);
        var wanted = distance > 0.01f ? toTarget / distance * speed : Vector3.Zero;

        // Once they've stood about for a while, members sit or lie down when the leader does, one after another rather
        // than all at once, and get up again with it. A member stays put until it is back on its feet.
        var posture = member.StillTime > 1f + index * 0.7f && prey is null && Meal is null ? Leader.Posture : Posture.Standing;
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
        if (terrain?.TrunkNear(position, stats.BodyRadius) is { } tree)
        {
            var fromTrunk = (position - tree.Transform.Origin) with { Y = 0f };
            position += fromTrunk.Normalized() * (tree.CollisionRadius + stats.BodyRadius - fromTrunk.Length());
        }

        // Walk on the ground, or float at swimming depth over deep water.
        float ground = terrain?.GetHeight(position.X, position.Z) ?? position.Y;
        float? surface = Leader.Water?.SurfaceAt(position);
        bool swimming = surface.HasValue && surface.Value - ground > stats.FloatDepth;
        float height = swimming ? surface!.Value - stats.FloatDepth : ground;
        position.Y = Mathf.Lerp(position.Y, height, Mathf.Min(1f, 10f * dt));
        member.Body.GlobalPosition = position;

        // Face the way it's going, or idly turn to match the leader when standing about; biting or feeding, it faces
        // what it's got its teeth into.
        float moving = member.Velocity.Length();
        float facing = moving > 0.3f ? Mathf.Atan2(-member.Velocity.X, -member.Velocity.Z) : leaderYaw;
        if (faceAt is { } at)
        {
            var toward = at - position;
            facing = Mathf.Atan2(-toward.X, -toward.Z);
        }
        member.Yaw = Mathf.LerpAngle(member.Yaw, facing, Mathf.Min(1f, (moving > 0.3f || bite > 0f ? stats.TurnSpeed : 0.5f) * dt));
        member.Animal.Rotation = new Vector3(0f, member.Yaw, 0f);

        member.Animal.IsSwimming = swimming;
        float stride = swimming ? 1f : Mathf.Clamp(moving / stats.WalkSpeed, 0f, 1f);
        float graze = Graze(member, moving, swimming, dt);
        member.Animal.Animate(swimming ? moving + 2f : moving, stride, Mathf.Max(graze, bite), dt);
    }

    /// <summary>
    /// A snow leopard hanging on to the prey, jaws clamped, rides along pressed against its flank, facing in, doing far
    /// more damage than biting, until the prey falls or the cat tires and lets go.
    /// </summary>
    private void HoldOn(Member member, float dt)
    {
        var prey = member.Gripping!;
        var stats = member.Animal.Stats;
        var wildlife = Leader.Wildlife!;
        member.GripTime += dt;
        if (wildlife.Bite(prey, FamilyGripStrength * member.Size * member.Size * dt, member.Body.GlobalPosition))
        {
            LetGo(member);
            return;
        }

        float reach = prey.Stats.BodyRadius * wildlife.SizeOf(prey) + stats.BodyRadius * member.Size * 0.6f;
        var spot = prey.GlobalPosition + member.GripSide * reach;
        float ground = Leader.Terrain?.GetHeight(spot.X, spot.Z) ?? spot.Y;
        float? surface = Leader.Water?.SurfaceAt(spot);
        float floatDepth = stats.FloatDepth * member.Size;
        bool swimming = surface.HasValue && surface.Value - ground > floatDepth;
        spot.Y = swimming ? surface!.Value - floatDepth : ground;

        // Legs scrabbling to keep up as the prey drags it along, head down and jaws locked.
        float speed = ((spot - member.Body.GlobalPosition) with { Y = 0f }).Length() / Mathf.Max(dt, 0.001f);
        member.Body.GlobalPosition = spot;
        member.Velocity = Vector3.Zero;
        member.Yaw = Mathf.Atan2(member.GripSide.X, member.GripSide.Z);
        member.Animal.Rotation = new Vector3(0f, member.Yaw, 0f);
        member.Animal.Settle(Posture.Standing, Leader.PostureChangeSeconds, dt);
        member.Animal.IsSwimming = swimming;
        member.Animal.Animate(speed, Mathf.Clamp(speed / stats.WalkSpeed, 0f, 1f), 0.9f, dt);
    }

    /// <summary>Lets go of the prey, if the member has hold of any.</summary>
    private void LetGo(Member member)
    {
        if (member.Gripping is { } prey)
            Leader.Wildlife?.Grip(prey, false);
        member.Gripping = null;
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
