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
/// feeds with the leader the same way, and whenever a cub grows up, another is born. A wild snow leopard may challenge
/// the mate for its place and fight it to the death; if the mate falls, the cubs fight the newcomer for their lives.
/// A bald eagle's family (a mate and their eaglets) flies with the leader instead, and lands beside it when it lands.
/// </summary>
public partial class Herd : Node3D
{
    /// <summary>The player animal the companions follow.</summary>
    public Player Leader { get; init; } = null!;

    /// <summary>Builds one companion; called once per member.</summary>
    public Func<Animal> Breed { get; init; } = null!;

    public int Count { get; init; }

    /// <summary>
    /// True for a snow leopard's or bird's family, which starts empty, until the leader finds a mate, and has a new
    /// cub or eaglet each time the last one grows up, up to <see cref="MaxCubs"/>.
    /// </summary>
    public bool IsFamily { get; init; }

    /// <summary>
    /// True for a bird's family, whose young hatch from the eggs in the nest (see <see cref="Player"/>'s
    /// HatchEggs) instead of being born one after another as the last grows up.
    /// </summary>
    public bool LaysEggs { get; init; }

    /// <summary>
    /// The most cubs a family has, mate and leader not counted: a snow leopard litter is usually two or three, and a
    /// bald eagle's nest holds one to three eaglets.
    /// </summary>
    private const int MaxCubs = 3;

    /// <summary>Seconds a dead mate or cub lies where it fell before it is gone.</summary>
    private const float BodySeconds = 20f;

    /// <summary>Seconds a grown chick that has left the family flies off before it is out of sight and gone.</summary>
    private const float LeaveSeconds = 25f;

    /// <summary>
    /// How many clutches the leader has laid. Each new one sends the grown young of the clutches before off on their
    /// own, as eagles and owls drive off last year's young before they nest again; any still growing go once grown.
    /// </summary>
    private int _brood;

    /// <summary>True once there is anyone travelling with the leader.</summary>
    public bool Any => _members.Count > 0;

    /// <summary>The family's living mate, if it has one.</summary>
    private Member? LivingMate => _members.FirstOrDefault(m => m.IsMate && !m.IsDead);

    /// <summary>True while the family has a living mate: a snow leopard keeps only one.</summary>
    public bool HasMate => LivingMate is not null;

    /// <summary>Where the living mate is, if there is one.</summary>
    public Vector3? MatePosition => LivingMate?.Body.GlobalPosition;

    /// <summary>
    /// A wild snow leopard fighting the mate for its place, if any. The mate turns to face it and fights back; the bites
    /// both ways are dealt by <see cref="Wildlife"/>.
    /// </summary>
    public Vector3? ChallengerAt { get; set; }

    /// <summary>What an eagle's mate is doing to feed its brooding partner, for the screen, or null when nothing.</summary>
    public string? Errand { get; private set; }

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

        /// <summary>True for a snow leopard's mate; everyone else in a family is a cub.</summary>
        public bool IsMate;

        /// <summary>
        /// An eagle's mate hunting for its brooding partner: what it's after, what it's carrying home, the last catch it
        /// brought, and seconds left resting before it goes out again.
        /// </summary>
        public Animal? Quarry;
        public Animal? Bringing;
        public Animal? Delivered;
        public float ErrandRest;

        /// <summary>Which of the nest's clutches a bird's chick hatched from (see <see cref="NewBrood"/>).</summary>
        public int Brood;

        /// <summary>
        /// Which way a grown chick is flying off to live on its own, once it has left the family, and seconds since it
        /// set off; null while it's still one of the family.
        /// </summary>
        public Vector3? Away;
        public float AwayTime;

        /// <summary>True while it has leapt on a wild snow leopard fighting the family and clamped its jaws on it.</summary>
        public bool Latched;

        /// <summary>Its life, from 100; only a family's mate and cubs can lose it, in a fight over the leader.</summary>
        public float Health = 100f;

        public bool IsDead => Health <= 0f;
        public float DeadTime;
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
    /// Health a grown family snow leopard's ordinary bites take each second from a wild one fighting it, as the wild
    /// one's do (see <see cref="Wildlife.RivalBite"/>); its clamped jaws do <see cref="FamilyGripStrength"/>.
    /// </summary>
    private const float FightBiteStrength = 8f;

    /// <summary>
    /// How hard a family eagle's talons rake whatever it's attacking, per second it's within reach, against the leader's
    /// single strikes: a raking eagle does about what a biting wolf does.
    /// </summary>
    private const float TalonRakeStrength = 4f;

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
    private bool _flies;

    /// <summary>True while the family is fighting off a raider at the nest, wherever the leader is.</summary>
    private bool _guarding;
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
            // Of both sexes too, so a herd of moose is a mix of antlered bulls and bare-headed cows.
            animal.Sex = i % 2 == 0 ? Sex.Female : Sex.Male;

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
        _flies = animal is Bird;
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
    /// A grown snow leopard or eagle joins the family as a mate, coming from <paramref name="at"/>, and they have a cub
    /// or an eaglet.
    /// </summary>
    public void AddMate(Vector3 at)
    {
        Recruit(1f, at);
        _members[^1].IsMate = true;
        MateWith(Leader.Sex);
        if (!LaysEggs)
            Recruit();
    }

    /// <summary>
    /// The leader lays another clutch: the young of the last one, once grown, leave the family to make their own way,
    /// and the mate stays.
    /// </summary>
    public void NewBrood() => _brood++;

    /// <summary>Makes the family's mate, if it has one, the other sex from a leader of <paramref name="sex"/>.</summary>
    public void MateWith(Sex sex)
    {
        foreach (var member in _members.Where(m => m.IsMate))
            member.Animal.Sex = sex == Sex.Male ? Sex.Female : Sex.Male;
    }

    /// <summary>
    /// The challenger's bites take <paramref name="damage"/> from the mate. Returns true if that killed it.
    /// </summary>
    public bool HurtMate(float damage)
    {
        if (LivingMate is not { } mate)
            return false;
        mate.Health -= damage;
        return mate.IsDead;
    }

    /// <summary>
    /// Where the living cub nearest <paramref name="at"/> is, if any. A newcomer that has killed the mate goes after the
    /// cubs one by one, as male big cats do when they take over, so that the leader will have cubs with it instead; any
    /// it doesn't kill live on.
    /// </summary>
    public Vector3? NearestCub(Vector3 at) => NearestLivingCub(at)?.Body.GlobalPosition;

    /// <summary>The newcomer's bites take <paramref name="damage"/> from the cub nearest it, at <paramref name="at"/>.</summary>
    public void HurtNearestCub(Vector3 at, float damage)
    {
        if (NearestLivingCub(at) is { } cub)
            cub.Health -= damage;
    }

    private Member? NearestLivingCub(Vector3 at) =>
        _members.Where(m => !m.IsMate && !m.IsDead).MinBy(m => m.Body.GlobalPosition.DistanceSquaredTo(at));

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
        animal.Sex = GD.Randi() % 2 == 0 ? Sex.Female : Sex.Male;
        if (_members.Count == 0)
            Fit(animal);

        var member = new Member
        {
            Body = body, Animal = animal, Slot = SlotFor(_members.Count) * _spacing, Size = size, Brood = _brood,
        };
        float yaw = Leader.Animal.Rotation.Y;
        var spot = at ?? Leader.GlobalPosition + member.Slot.Rotated(Vector3.Up, yaw);
        // An eagle mate joins in the air, where it was courting; anything else, and every youngster, starts on the ground.
        if (!(_flies && at.HasValue))
            spot.Y = Leader.Terrain?.GroundBelow(spot with { Y = Leader.GlobalPosition.Y }) ?? Leader.GlobalPosition.Y;
        body.GlobalPosition = spot;
        member.Yaw = yaw;
        animal.Rotation = new Vector3(0f, yaw, 0f);
        _members.Add(member);
    }

    /// <summary>
    /// Something is raiding the leader's nest: the family goes for it and keeps at it however far the leader is, until
    /// it's driven off.
    /// </summary>
    public void Defend(Animal raider)
    {
        if (!Any)
            return;
        Target = raider;
        _sinceOrder = 0f;
        _guarding = true;
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
        _guarding = false;
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
        foreach (var member in _members.Where(m => !m.IsDead))
        {
            LetGo(member);
            var spot = Leader.GlobalPosition + member.Slot.Rotated(Vector3.Up, yaw);
            spot.Y = Leader.Terrain?.GroundBelow(spot with { Y = Leader.GlobalPosition.Y }) ?? Leader.GlobalPosition.Y;
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
            {
                LetGo(member);
                if (member.Bringing is { } food)
                    Leader.Wildlife?.Drop(food);
                member.Bringing = member.Quarry = null;
            }
            Errand = null;
            return;
        }

        float dt = (float)delta;
        _time += dt;

        // The prey is down, or got away from the leader, or the leader lost interest: back to following.
        if (Target is { } prey)
        {
            _sinceOrder += dt;
            if (Leader.Wildlife?.IsAlive(prey) != true || Leader.IsDead || _sinceOrder > AttackPatience
                || (_guarding && Leader.Wildlife?.NestRaider != prey)
                || (!_guarding && Leader.GlobalPosition.DistanceTo(prey.GlobalPosition) > AttackLeash))
                Target = null;
            // A carcass that has rotted away is freed, so once the hunt is off it mustn't be looked at again.
            else
            {
                _preySpeed = _sinceOrder > dt ? ((prey.GlobalPosition - _preyWas) with { Y = 0f }).Length() / dt : 0f;
                _preyWas = prey.GlobalPosition;
            }
        }
        if (Target is null)
            _guarding = false;

        // The kill is eaten, the leader has walked off, or a new hunt is on: the meal is over.
        if (Meal is { } meal && (Leader.Wildlife?.HasMeat(meal) != true || Leader.IsDead || Target is not null
            || Leader.GlobalPosition.DistanceTo(meal.GlobalPosition) > MealLeash))
            Meal = null;

        // The dead lie where they fell a while, then are gone.
        foreach (var dead in _members.Where(m => m.IsDead && m.DeadTime > BodySeconds).ToList())
        {
            dead.Body.QueueFree();
            _members.Remove(dead);
        }

        // A grown chick of an earlier clutch leaves the family, and flies off until it's out of sight.
        foreach (var member in _members.Where(m => LaysEggs && !m.IsMate && !m.IsDead && m.Away is null && m.Size >= 1f && m.Brood < _brood))
        {
            float heading = (float)GD.RandRange(0.0, Mathf.Tau);
            member.Away = new Vector3(Mathf.Cos(heading), 0f, Mathf.Sin(heading));
        }
        foreach (var gone in _members.Where(m => m.AwayTime > LeaveSeconds).ToList())
        {
            LetGo(gone);
            gone.Body.QueueFree();
            _members.Remove(gone);
        }

        Errand = null;
        for (int i = 0; i < _members.Count; i++)
            Move(_members[i], i, dt);
    }

    /// <summary>A dead mate or cub goes limp and lies still where it fell.</summary>
    private void LieDead(Member member, float dt)
    {
        LetGo(member);
        member.DeadTime += dt;
        member.Velocity = Vector3.Zero;
        member.Animal.Settle(Posture.Lying, 0.6f, dt);
        member.Animal.Dead = Mathf.MoveToward(member.Animal.Dead, 1f, dt / 0.8f);
        member.Animal.IsSwimming = false;
        member.Animal.Animate(0f, 0f, 0f, dt);
    }

    private void Move(Member member, int index, float dt)
    {
        if (member.IsDead)
        {
            LieDead(member, dt);
            return;
        }
        var stats = member.Animal.Stats;
        var terrain = Leader.Terrain;
        float leaderYaw = Leader.Animal.Rotation.Y;

        // A newcomer grows up as it travels with the others. In a family, a cub growing up means a new one is born, as
        // long as the mate is alive.
        if (member.Size < 1f)
        {
            member.Size = Mathf.Min(1f, member.Size + (1f - YoungSize) / GrowUpSeconds * dt);
            member.Body.Scale = Vector3.One * member.Size;
            if (member.Size >= 1f && IsFamily && !LaysEggs && HasMate && _members.Count(m => !m.IsMate && !m.IsDead) < MaxCubs)
                Recruit();
        }
        if (_flies)
        {
            Fly(member, index, dt);
            return;
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
        // The mate fights a challenger; once the mate is dead, the cubs fight for their lives.
        bool defending = ChallengerAt.HasValue && (member.IsMate || !HasMate);
        if (!defending)
            member.Latched = false;
        if (member.Gripping is not null && (member.Gripping != prey || member.GripTime > GripHoldSeconds || defending))
        {
            LetGo(member);
            member.GripRest = GripRestSeconds;
        }
        if (member.Gripping is not null)
        {
            HoldOn(member, dt);
            return;
        }
        if (defending)
        {
            // Challenged for its place, the mate squares up to the rival and they fight to the death; a cub fights the
            // newcomer for its life. With its breath, each leaps on and clamps its jaws with the special attack, pressed
            // against the rival, then bites while it gets its breath back. A cub has a cub's jaws.
            var challenger = ChallengerAt!.Value;
            var away = (position - challenger) with { Y = 0f };
            float ring = stats.BodyRadius * (1f + 0.6f * member.Size);
            if (away.Length() < ring + 0.8f)
            {
                if (!member.Latched && member.GripRest <= 0f)
                {
                    member.Latched = true;
                    member.GripTime = 0f;
                }
                if (member.Latched && (member.GripTime += dt) > GripHoldSeconds)
                {
                    member.Latched = false;
                    member.GripRest = GripRestSeconds;
                }
                float strength = member.Latched ? FamilyGripStrength : FightBiteStrength;
                Leader.Wildlife?.HurtChallenger(strength * member.Size * member.Size * dt);
                bite = member.Latched ? 0.9f : 0.6f + 0.4f * Mathf.Sin(_time * 10f + index);
                faceAt = challenger;
                if (member.Latched)
                    ring = stats.BodyRadius * member.Size * 0.9f;
            }
            else
            {
                member.Latched = false;
            }
            target = challenger + (away.LengthSquared() > 0.01f ? away.Normalized() : Vector3.Back) * ring;
        }
        else if (prey is not null)
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
        if (distance > 60f && prey is null && !defending)
        {
            Gather();
            return;
        }

        // Keep pace with the leader on the move, so members stay at their places rather than trailing behind, and run
        // flat out to catch up when well behind.
        float leaderSpeed = (Leader.Velocity with { Y = 0f }).Length();
        float speed = distance < 0.3f ? 0f : Mathf.Min(stats.SprintSpeed, Mathf.Max(0f, distance - 0.3f) * 1.5f + leaderSpeed);

        // On the attack, close right in and keep pace with the prey as it runs, rather than easing off short of it.
        if (defending)
            speed = Mathf.Min(stats.SprintSpeed, distance * 3f);
        else if (prey is not null)
            speed = Mathf.Min(stats.SprintSpeed, distance * 3f + _preySpeed);
        var wanted = distance > 0.01f ? toTarget / distance * speed : Vector3.Zero;

        // Once they've stood about for a while, members sit or lie down when the leader does, one after another rather
        // than all at once, and get up again with it. A member stays put until it is back on its feet.
        var posture = member.StillTime > 1f + index * 0.7f && prey is null && Meal is null && !defending ? Leader.Posture : Posture.Standing;
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
        float ground = terrain?.GroundBelow(position) ?? position.Y;
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
        float ground = Leader.Terrain?.GroundBelow(spot) ?? spot.Y;
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

    /// <summary>
    /// An eagle's mate and eaglets fly with it, spread out behind and to either side as birds flying together keep
    /// clear of each other's wings, rising and falling a little out of step. When the leader comes down they glide in
    /// and land beside it, and stand about on the ground with it; they share its kills there. Anything the leader goes
    /// for, or anything that goes for the leader, they stoop at and rake with their talons, in the air or on the ground.
    /// </summary>
    private void Fly(Member member, int index, float dt)
    {
        var animal = member.Animal;
        var stats = animal.Stats;
        var wildlife = Leader.Wildlife;
        float leaderYaw = Leader.Animal.Rotation.Y;
        bool aloft = Leader.Animal.IsFlying;
        var position = member.Body.GlobalPosition;
        bool leaving = member.Away.HasValue;

        var slot = member.Slot * (aloft ? 2.5f : 1f)
            + new Vector3(Mathf.Sin(_time * 0.4f + index * 2f), 0f, Mathf.Cos(_time * 0.35f + index * 3f)) * _spacing * 0.3f;
        var target = Leader.GlobalPosition + slot.Rotated(Vector3.Up, leaderYaw);
        if (aloft)
            target.Y += Mathf.Sin(_time * 0.5f + index * 1.3f) * 0.8f;
        else
            target.Y = Footing(target);

        float eat = 0f;
        bool reaching = false;
        Vector3? faceAt = null;
        if (!leaving && Target is { } prey && wildlife is not null)
        {
            // Straight at it, talons out, raking it while within reach.
            target = prey.GlobalPosition + Vector3.Up * prey.Stats.BodyHeight * wildlife.SizeOf(prey) * 0.5f;
            float reach = prey.Stats.BodyRadius * wildlife.SizeOf(prey) + stats.BodyRadius * member.Size + 1.2f;
            reaching = position.DistanceTo(target) < reach + 6f;
            if (position.DistanceTo(target) < reach)
                wildlife.Bite(prey, TalonRakeStrength * member.Size * member.Size * dt, position);
        }
        else if (!leaving && !aloft && Meal is { } meal && wildlife is not null)
        {
            // Round the kill with the leader, tearing at it.
            float ring = meal.Stats.BodyRadius * wildlife.SizeOf(meal) + stats.BodyRadius * member.Size + 0.2f;
            float angle = (index + 1) * Mathf.Tau / (_members.Count + 1) + leaderYaw;
            target = meal.GlobalPosition + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ring;
            target.Y = Footing(target);
            if (((position - meal.GlobalPosition) with { Y = 0f }).Length() < ring + 0.6f)
            {
                wildlife.EatFrom(meal, PackMealShare * member.Size * dt);
                eat = 0.75f + 0.25f * Mathf.Sin(_time * 7f + index * 1.7f);
                faceAt = meal.GlobalPosition;
            }
        }

        // Up a tree with the leader, each takes a branch of its own and flies in to sit on it.
        Vector3? seat = null;
        if (!leaving && !aloft && Target is null && Meal is null && Leader.PerchedIn is { } perch)
            seat = BranchFor(perch.Tree, perch.Branch, index);
        if (seat is { } branch)
            target = branch;

        // The mate of a brooding eagle hunts for her, and brings what it catches back to the nest.
        bool errand = false;
        if (member.IsMate && RunErrand(member, position, dt) is { } goal)
        {
            target = goal;
            seat = null;
            errand = true;
            reaching = member.Quarry is not null && position.DistanceTo(goal) < 8f;
        }
        // A grown chick leaving the family climbs away high over the land and is soon out of sight.
        if (member.Away is { } heading)
        {
            member.AwayTime += dt;
            target = position + heading * 40f;
            target.Y = Below(position) + 30f;
        }
        bool chasing = Target is not null || member.Quarry is not null || leaving;

        // Too far behind to catch up (e.g. the leader respawned): turn up at its place.
        var toward = target - position;
        float flatGap = (toward with { Y = 0f }).Length();
        if (flatGap > 120f && Target is null && !errand && !leaving)
        {
            Gather();
            return;
        }

        // On the ground, a short way is walked; anything further is flown, in a low hop over the ground. Up a tree, it
        // flies until it's on its branch.
        float floor = Footing(position);
        bool flying = seat.HasValue ? position.DistanceTo(seat.Value) > 0.4f
            : errand || leaving || aloft || Target is not null || position.Y - floor > 0.3f || flatGap > 6f;
        if (flying && !aloft && Target is null && !errand && flatGap > 3f)
            target.Y += Mathf.Min(flatGap * 0.3f, 6f);
        toward = target - position;
        float gap = toward.Length();

        // Keep pace with the leader, and fly flat out to catch up when well behind, or when after something; on foot,
        // walk.
        float leaderSpeed = Leader.Velocity.Length();
        float top = flying ? stats.FlySpeed * 1.3f : stats.WalkSpeed;
        float speed = gap < 0.3f ? 0f : Mathf.Min(top, Mathf.Max(0f, gap - 0.3f) * 1.5f + (chasing ? stats.FlySpeed : leaderSpeed));
        var wanted = gap > 0.01f ? toward / gap * speed : Vector3.Zero;

        // Keep a wingspan from the others.
        foreach (var other in _members)
        {
            if (other == member)
                continue;
            var away = position - other.Body.GlobalPosition;
            float apart = away.Length();
            float room = _spacing * (flying ? 1.2f : 0.7f);
            if (apart < room && apart > 0.001f)
                wanted += away / apart * (room - apart) * 3f;
        }

        member.Velocity = member.Velocity.Lerp(wanted, Mathf.Min(1f, (flying ? 3f : stats.Acceleration) * dt));
        position += member.Velocity * dt;
        floor = Footing(position);
        if (seat is { } sitting && !flying)
        {
            position = sitting;
            member.Velocity = Vector3.Zero;
        }
        else if (position.Y < floor || !flying)
        {
            position.Y = floor;
            member.Velocity.Y = Mathf.Max(member.Velocity.Y, 0f);
        }
        member.Body.GlobalPosition = position;

        // Tip and bank with the flight, flapping when climbing or slow and gliding otherwise, as the wild ones do; on
        // the ground, stand level and face the way it's walking, or what it's eating.
        var level = member.Velocity with { Y = 0f };
        float flat = level.Length();
        float before = member.Yaw;
        float facing = flat > 0.3f ? Mathf.Atan2(-level.X, -level.Z) : leaderYaw;
        if (faceAt is { } at)
            facing = Mathf.Atan2(-(at - position).X, -(at - position).Z);
        member.Yaw = Mathf.LerpAngle(member.Yaw, facing, Mathf.Min(1f, (flat > 0.3f || eat > 0f ? 4f : 0.5f) * dt));
        float smooth = Mathf.Min(1f, 6f * dt);
        var rotation = animal.Rotation;
        float bank = 0f, pitch = 0f;
        if (flying)
        {
            float turn = Mathf.AngleDifference(before, member.Yaw) / Mathf.Max(dt, 0.001f);
            bank = Mathf.Clamp(turn * 0.35f, -0.9f, 0.9f);
            pitch = Mathf.Clamp(member.Velocity.Y / Mathf.Max(flat, 1f), -0.9f, 0.5f);
        }
        animal.Rotation = new Vector3(Mathf.Lerp(rotation.X, pitch, smooth), member.Yaw, Mathf.Lerp(rotation.Z, bank, smooth));
        animal.IsFlying = flying;
        animal.IsSwimming = false;
        bool flapping = flying && (member.Velocity.Y > 0.8f || flat < stats.FlySpeed * 0.45f);
        animal.Flap = Mathf.MoveToward(animal.Flap, flapping ? 1f : 0f, 4f * dt);
        animal.Dive = Mathf.MoveToward(animal.Dive, flying && member.Velocity.Y < -4f ? 1f : 0f, 3f * dt);
        if (animal is Bird bird)
            bird.Clutching = reaching;
        animal.Settle(Posture.Standing, Leader.PostureChangeSeconds, dt);
        animal.Animate(flat, flying ? 0f : Mathf.Clamp(flat / stats.WalkSpeed, 0f, 1f), eat, dt);
    }

    /// <summary>
    /// Where the member at <paramref name="index"/> sits in <paramref name="tree"/>: on the branch nearest the leader,
    /// other than the leader's own (<paramref name="taken"/>), the next nearest for the next member, and so on,
    /// two to a branch, one further out than the other, if there are more of them than branches.
    /// </summary>
    private Vector3? BranchFor(Tree tree, int taken, int index)
    {
        var branches = Enumerable.Range(0, tree.Branches.Count).Where(b => b != taken)
            .OrderBy(b => tree.BranchTopAt(b, 0.6f).DistanceSquaredTo(Leader.GlobalPosition)).ToList();
        if (branches.Count == 0)
            return null;
        float along = index / branches.Count % 2 == 0 ? 0.6f : 0.85f;
        return tree.BranchTopAt(branches[index % branches.Count], along);
    }

    /// <summary>The height of the ground, or of the water where there is any, below a point.</summary>
    private float Below(Vector3 at) =>
        Mathf.Max(Leader.Terrain?.GroundBelow(at) ?? at.Y, Leader.Water?.SurfaceAt(at) ?? float.MinValue);

    /// <summary>Where a bird stands below a point: on top of its nest where that is, or else the ground or water.</summary>
    private float Footing(Vector3 at)
    {
        float below = Below(at);
        if (Leader.BirdNest is { } nest && ((at - nest.Cup) with { Y = 0f }).Length() < nest.Radius && at.Y > nest.Cup.Y - 1f)
            return Mathf.Max(below, nest.Cup.Y);
        return below;
    }

    /// <summary>How far an eagle hunting for its brooding mate goes looking for something to catch.</summary>
    private const float HuntRange = 250f;

    /// <summary>
    /// How heavy a catch an eagle can carry home, for its size, as the player's eagle can (see
    /// <see cref="Player.LiftStrength"/>): a hare, not a reindeer calf.
    /// </summary>
    private const float MateLiftStrength = 0.4f;

    /// <summary>Seconds the mate rests at the nest after bringing food, or after finding nothing to hunt, before going out again.</summary>
    private const float ErrandRestSeconds = 40f;

    /// <summary>
    /// What an eagle's mate is doing for its brooding partner, as the male does for the female sitting on their eggs:
    /// it flies off and hunts the nearest thing light enough to carry, strikes it dead, carries it back in its talons
    /// and lays it down on the nest beside her for her to eat, then rests a while before going out again, once she
    /// has eaten it. Once it has a catch in its talons it brings it home even if she has got up. Returns where it's
    /// flying to, or null when it has no errand and keeps with the family as usual.
    /// </summary>
    private Vector3? RunErrand(Member mate, Vector3 position, float dt)
    {
        var wildlife = Leader.Wildlife;
        if (wildlife is null)
            return null;
        mate.ErrandRest = Mathf.Max(0f, mate.ErrandRest - dt);
        var bird = mate.Animal as Bird;
        var talons = bird?.Talons ?? position;

        if (mate.Bringing is { } food)
        {
            if (!wildlife.HasMeat(food) || Leader.BirdNest is not { } home)
            {
                if (wildlife.HasMeat(food))
                    wildlife.Drop(food);
                mate.Bringing = null;
                return null;
            }
            var heading = mate.Velocity with { Y = 0f };
            wildlife.Hold(food, talons, heading.LengthSquared() > 0.01f ? heading.Normalized() : Vector3.Forward, inJaws: false);

            // Down on the nest beside her, a little out from where she lies.
            var beside = Vector3.Right.Rotated(Vector3.Up, Leader.Animal.Rotation.Y) * Mathf.Min(1.4f, home.Radius * 0.55f);
            var spot = home.Cup + beside;
            var above = spot + Vector3.Up * (position - talons).Y;
            if (position.DistanceTo(above) < 0.8f)
            {
                wildlife.Drop(food, lodged: spot);
                mate.Delivered = food;
                mate.Bringing = null;
                mate.ErrandRest = ErrandRestSeconds;
                return null;
            }
            Errand = "Your mate is bringing you food";
            return above;
        }

        // She's still eating what it brought last time, or she's up and about: no hunting.
        if (Leader.Brooding is null || Target is not null)
        {
            mate.Quarry = null;
            return null;
        }
        if (mate.Delivered is { } last && wildlife.HasMeat(last))
        {
            Errand = "Your mate brought you food: press E to eat it";
            return null;
        }

        if (mate.Quarry is { } prey)
        {
            if (!wildlife.IsAlive(prey) || prey.GlobalPosition.DistanceTo(Leader.GlobalPosition) > HuntRange * 1.5f)
            {
                mate.Quarry = null;
                return null;
            }
            // A stoop at it from above, talons first, and a single strike kills it.
            var at = prey.GlobalPosition + Vector3.Up * prey.Stats.BodyHeight * wildlife.SizeOf(prey) * 0.5f;
            float reach = prey.Stats.BodyRadius * wildlife.SizeOf(prey) + mate.Animal.Stats.BodyRadius + 1f;
            if (talons.DistanceTo(at) < reach || position.DistanceTo(at) < reach)
            {
                wildlife.Bite(prey, 1000f, position);
                if (!wildlife.IsAlive(prey))
                {
                    mate.Quarry = null;
                    mate.Bringing = prey;
                    return null;
                }
            }
            Errand = "Your mate is out hunting for you";
            return at;
        }

        if (mate.ErrandRest > 0f)
            return null;
        var stats = mate.Animal.Stats;
        mate.Quarry = wildlife.QuarryNear(Leader.GlobalPosition, HuntRange, stats.BodyRadius * stats.BodyHeight * MateLiftStrength);
        if (mate.Quarry is null)
            mate.ErrandRest = ErrandRestSeconds / 4f;
        return null;
    }
}
