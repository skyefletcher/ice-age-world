using System.Collections.Generic;
using System.Linq;
using Godot;

namespace IceAgeWorld;

/// <summary>
/// Wild bald eagles. Each keeps to a stretch of shore of its own, as real ones hold a territory along a lake or coast
/// for the fish in it. It soars in wide, slow circles high over the water on outstretched wings, then spirals down to
/// perch at the top of a tall tree by the shore (or on the shore itself where there is none) and sits watching a
/// while before taking off again. Eagles are fiercely territorial: the player's eagle flying close to a wild one's
/// perch is set upon, the wild one stooping at it again and again, raking it with its talons as it passes and
/// climbing away to come round for another pass, as eagles do to drive off a rival. Anything that attacks one gets the
/// same, in the air or on the ground, until the eagle is badly hurt and flies off. The player fights back with a
/// hunter's bite as it swoops low, or as an eagle with its own talons in the air (see <see cref="Player"/>'s Swoop).
/// A grown player's eagle of the other sex, with no mate yet, is courted instead: the wild one flies up and circles
/// round it, and the two can pair up (see <see cref="Player"/>'s Court).
/// Wild snowy owls live the same way out on the open tundra, away from the water: each sits on the ground, as snowy
/// owls perch on hummocks and boulders where there are no trees, and flies low, slow circles over its patch, quartering
/// it for hares. They drive the player's owl off their ground the same way.
/// A player's bird of any kind flying by with food in its talons isn't fought but robbed: the wild one chases close
/// behind it, talons reaching, and if the player lets go, dives after the food, catches it as it falls (or off the
/// ground), carries it home and eats it on its perch.
/// </summary>
public partial class Wildlife
{
    [Export] public int BaldEagles { get; set; } = 4;
    [Export] public int SnowyOwls { get; set; } = 4;

    /// <summary>How close the player's eagle may come to a wild one's perch before it is chased off.</summary>
    [Export] public float EagleTerritory { get; set; } = 40f;

    /// <summary>Health the player loses each time a wild eagle's talons rake it, before the player's size softens it.</summary>
    [Export] public float TalonStrike { get; set; } = 9f;

    /// <summary>
    /// Hurt this badly, once its health bar shows red, a fighting eagle gives up and flies off rather than fight to
    /// the death.
    /// </summary>
    private const float EagleGivesUp = HealthBar.Low;

    /// <summary>
    /// A wounded eagle doesn't go looking for a fight: it only drives the player's eagle off its territory once its health
    /// bar is green again, though it still fights back if attacked before then.
    /// </summary>
    private const float EagleDefends = HealthBar.High;

    /// <summary>How high over the ground or water an eagle soars, and how wide the circles it rides there.</summary>
    private const float SoarHeight = 28f;
    private const float SoarRadius = 35f;

    /// <summary>An owl hunts low, a few metres up, beating slowly round a smaller patch of ground.</summary>
    private const float OwlFlyHeight = 7f;
    private const float OwlFlyRadius = 22f;

    /// <summary>After a strike, an eagle climbs away this many seconds before turning to come round at its foe again.</summary>
    private const float PassSeconds = 2.5f;

    /// <summary>How far past the two bodies a stooping eagle's outstretched talons reach.</summary>
    private const float TalonsReach = 1.6f;

    /// <summary>How close its talons must come to food the player dropped to snatch it, past the food's own size.</summary>
    private const float LootReach = 1.5f;

    /// <summary>How much of a unit-bulk carcass an eagle eats a second on its perch: a giant hare lasts it a minute or so.</summary>
    private const float EagleMouthful = 0.003f;

    /// <summary>How long an eagle soars, and how long it then sits on its perch, in seconds, chosen afresh each time.</summary>
    private const float SoarMin = 40f, SoarMax = 90f;
    private const float PerchMin = 20f, PerchMax = 60f;

    /// <summary>
    /// A wild eagle's own state: where its perch is, the middle of the circles it soars out over the water, whether
    /// it's in the air or coming in to land, seconds left until it does something else, how far round its circle it
    /// has got, and seconds left climbing away after a strike.
    /// </summary>
    private sealed class Flight
    {
        public required Vector3 Perch;
        public required Vector3 Over;
        public float Height = SoarHeight;
        public float Radius = SoarRadius;
        public bool Airborne;
        public bool Landing;
        public float Timer;
        public float Circle;
        public float PullAway;

        /// <summary>True while it's flying circles round the player's eagle, courting it.</summary>
        public bool Courting;

        /// <summary>
        /// True while it chases the player to steal the food in its talons, and what that food is, so it knows what to
        /// dive for if the player lets go.
        /// </summary>
        public bool Robbing;
        public Animal? Coveted;

        /// <summary>
        /// Food the player dropped, which it's diving to catch, carrying home in its talons, or eating on its perch.
        /// </summary>
        public Animal? Loot;
        public bool Holding;
        public bool Eating;

        /// <summary>True once the player has attacked it: then it fights, food or no food, until the fight is over.</summary>
        public bool Provoked;
    }

    private readonly Dictionary<Beast, Flight> _flights = [];

    /// <summary>Gives each eagle a territory on the shore of a lake or the ocean, taking turns between them.</summary>
    private void AddEagles()
    {
        var lakes = _terrain.Lakes;
        for (int e = 0; e < BaldEagles && lakes.Count > 0; e++)
        {
            var lake = lakes[e % lakes.Count];
            var middle = new Vector3(lake.Middle.X, lake.Surface, lake.Middle.Y);
            var shore = Shore(lake) ?? Grazing(middle, 20f, 60f);
            var tree = TreeNear(shore, 25f);
            var perch = tree?.Summit ?? shore;
            // It circles out over the water off its stretch of shore, where it would watch for fish.
            var seaward = (middle - shore) with { Y = 0f };
            var over = shore + (seaward.LengthSquared() > 1f ? seaward.Normalized() * Mathf.Min(SoarRadius * 0.6f, seaward.Length()) : Vector3.Zero);

            var group = new Group { Kind = Kind.Eagle, Spacing = 1f };
            group.Centre = group.Goal = shore;
            var eagle = Add(new BaldEagle(), group, Vector3.Zero, 1f);
            eagle.Body.Name = $"BaldEagle{e}";
            eagle.Body.GlobalPosition = perch;
            _flights[eagle] = new Flight { Perch = perch, Over = over, Timer = _rng.RandfRange(5f, PerchMax) };
            _groups.Add(group);
            // Every other one is a hen with a mate and a nest of eggs (see Wildlife.Nests.cs).
            if (e % 2 == 0)
                AddNest(eagle, tree);
        }

        for (int o = 0; o < SnowyOwls; o++)
        {
            var perch = Grazing(Vector3.Zero, 60f, _terrain.HalfSize * 0.85f);
            // It faces out over the ground it hunts, a little way off its perch.
            float angle = _rng.RandfRange(0f, Mathf.Tau);
            var over = perch + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * OwlFlyRadius * 0.5f;
            var group = new Group { Kind = Kind.Eagle, Spacing = 1f };
            group.Centre = group.Goal = perch;
            var owl = Add(new SnowyOwl(), group, Vector3.Zero, 1f);
            owl.Body.Name = $"SnowyOwl{o}";
            owl.Body.GlobalPosition = perch;
            _flights[owl] = new Flight
            {
                Perch = perch, Over = over, Height = OwlFlyHeight, Radius = OwlFlyRadius, Timer = _rng.RandfRange(5f, PerchMax),
            };
            _groups.Add(group);
            if (o % 2 == 0)
                AddNest(owl, null);
        }
    }

    /// <summary>A spot on dry land just back from the water's edge, out from the lake's open water in some direction.</summary>
    private Vector3? Shore(Lake lake)
    {
        var middle = new Vector3(lake.Middle.X, 0f, lake.Middle.Y);
        float limit = _terrain.HalfSize - 20f;
        for (int attempt = 0; attempt < 24; attempt++)
        {
            float angle = _rng.RandfRange(0f, Mathf.Tau);
            var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            for (float distance = lake.Reach; distance < lake.Reach + 400f; distance += 1f)
            {
                var spot = middle + direction * distance;
                if (Mathf.Abs(spot.X) > limit || Mathf.Abs(spot.Z) > limit)
                    break;
                if (_terrain.GetHeight(spot.X, spot.Z) > lake.Surface + 0.5f)
                {
                    spot += direction * 3f;
                    return spot with { Y = _terrain.GetHeight(spot.X, spot.Z) };
                }
            }
        }
        return null;
    }

    /// <summary>The tree whose foot is nearest <paramref name="spot"/>, if any stands within <paramref name="range"/>.</summary>
    private Tree? TreeNear(Vector3 spot, float range)
    {
        Tree? best = null;
        float bestGap = range;
        foreach (var tree in _terrain.Trees)
        {
            float gap = (tree.Transform.Origin - spot with { Y = tree.Transform.Origin.Y }).Length();
            if (gap < bestGap)
            {
                bestGap = gap;
                best = tree;
            }
        }
        return best;
    }

    /// <summary>
    /// Decides whether the eagle fights the player: one that has been attacked fights back, and one whose perch the
    /// player's eagle comes close to drives it off. Badly hurt, it gives up and flies away; and it stops once the player
    /// is dead or far away.
    /// </summary>
    private void ThinkEagle(Group group, float dt)
    {
        var eagle = group.Members[0];
        group.Fleeing = Mathf.Max(0f, group.Fleeing - dt);
        var flight = _flights[eagle];
        if (eagle.IsDead || Player is not { } player)
        {
            group.HuntsPlayer = false;
            flight.Robbing = false;
            LoseLoot(flight);
            return;
        }
        var perch = flight.Perch;

        // Food it was after that the player has snatched back, another bird has caught, or that is all eaten, it forgets.
        // (Once caught it's dead, so a live animal then is a new one, reborn when the last of the meal was eaten.)
        bool caught = flight.Holding || flight.Eating;
        if (flight.Loot is { } loot && (player.IsHolding(loot) || !(HasMeat(loot) || (!caught && IsAlive(loot)))
            || _flights.Any(f => f.Value != flight && f.Value.Loot == loot && (f.Value.Holding || f.Value.Eating))))
            LoseLoot(flight);

        // A grown eagle of the other sex with no mate yet is courted, not chased off: while it flies, the wild one flies up
        // to meet it anywhere in or near its territory, and keeps with it while it stays close.
        bool rival = player.Animal.GetType() == eagle.Animal.GetType();
        bool suitor = !player.IsDead && rival && player.IsGrownUp && player.Sex != eagle.Animal.Sex
            && player.Family is { HasMate: false } && NestOf(eagle) is null;
        float range = flight.Courting ? EagleTerritory * 2f : EagleTerritory * 1.5f;
        flight.Courting = suitor && player.Animal.IsFlying && !group.HuntsPlayer && group.Fleeing <= 0f && eagle.Health >= EagleDefends
            && (player.GlobalPosition.DistanceTo(perch) < range || player.GlobalPosition.DistanceTo(eagle.Body.GlobalPosition) < range);

        // Eagles are pirates: rather than catch their own, they chase down another bird carrying food, an osprey or a
        // rival eagle, and harry it until it lets go, then catch the food as it falls. So a player's bird with food in
        // its talons, of any kind, is chased for it rather than fought, unless it has picked a fight itself.
        var food = player.IsDead ? null : player.InTalons;
        bool far = eagle.Body.GlobalPosition.DistanceTo(player.GlobalPosition) > PlayerSightRange * 2.5f;
        if (flight.Robbing)
        {
            // It let go: dive for it.
            if (food is null && flight.Coveted is { } dropped && !player.IsHolding(dropped) && (HasMeat(dropped) || IsAlive(dropped)))
                flight.Loot = dropped;
            flight.Coveted = food;
            if (food is null || far || flight.Provoked || flight.Courting || group.Fleeing > 0f || eagle.Health < EagleDefends)
                flight.Robbing = false;
        }
        else if (food is not null && !flight.Provoked && !flight.Courting && flight.Loot is null && group.Fleeing <= 0f
            && eagle.Health >= EagleDefends
            && (player.GlobalPosition.DistanceTo(perch) < EagleTerritory * 1.5f
                || player.GlobalPosition.DistanceTo(eagle.Body.GlobalPosition) < EagleTerritory))
        {
            flight.Robbing = true;
            flight.Coveted = food;
            group.HuntsPlayer = false;
        }

        if (group.HuntsPlayer)
        {
            if (player.IsDead || far || eagle.Health < EagleGivesUp)
            {
                group.HuntsPlayer = false;
                flight.Provoked = false;
                if (eagle.Health < EagleGivesUp)
                    Scare(group, player.GlobalPosition, seconds: 20f);
            }
        }
        else if (!suitor && !flight.Robbing && food is null && group.Fleeing <= 0f && eagle.Health >= EagleDefends && !player.IsDead
            && rival && player.GlobalPosition.DistanceTo(perch) < EagleTerritory)
        {
            group.HuntsPlayer = true;
        }
    }

    /// <summary>Lets go of any food it has, and forgets about it.</summary>
    private void LoseLoot(Flight flight)
    {
        if (flight.Loot is { } loot && flight.Holding)
            Drop(loot);
        flight.Loot = null;
        flight.Holding = flight.Eating = false;
    }

    /// <summary>
    /// The eagle hit by the player, or the player's family, rounds on it, unless it's too badly hurt to want any more,
    /// when it flies off.
    /// </summary>
    private void EagleAttacked(Beast eagle, Vector3 from)
    {
        var flight = _flights.GetValueOrDefault(eagle);
        if (flight is not null)
        {
            // Attacked, it drops whatever it has and turns on its attacker.
            flight.Robbing = false;
            LoseLoot(flight);
        }
        if (eagle.Health >= EagleGivesUp)
        {
            eagle.Group.HuntsPlayer = true;
            if (flight is not null)
                flight.Provoked = true;
            return;
        }
        // In the red, it breaks off at once and flies off high and away.
        eagle.Group.HuntsPlayer = false;
        if (flight is not null)
        {
            flight.PullAway = 0f;
            flight.Provoked = false;
        }
        Scare(eagle.Group, from, seconds: 20f);
    }

    /// <summary>Sits on its perch, or flies: soaring, coming in to land, stooping at the player, or flying off.</summary>
    private void MoveEagle(Beast eagle, float dt)
    {
        var flight = _flights[eagle];
        var group = eagle.Group;
        var animal = eagle.Animal;
        var stats = animal.Stats;
        var player = group.HuntsPlayer ? Player : null;
        bool fleeing = group.Fleeing > 0f;
        eagle.Stamina = 100f;

        if (!flight.Airborne)
        {
            flight.Timer -= dt;
            // It sits on over a meal until the last of it is gone.
            if (flight.Eating && flight.Loot is { } meal)
            {
                EatFrom(meal, EagleMouthful * dt);
                flight.Timer = Mathf.Max(flight.Timer, 1f);
            }
            bool diving = flight.Loot is not null && !flight.Eating;
            // A hen on her eggs sits tight on them, and leaves them only to drive something off.
            if (player is null && !fleeing && !flight.Courting && !flight.Robbing && !diving && (flight.Timer > 0f || IsBrooding(eagle)))
            {
                SitOnPerch(eagle, flight, dt);
                return;
            }
            TakeOff(eagle, flight);
        }

        var position = eagle.Body.GlobalPosition;
        float speed = stats.FlySpeed * 0.65f;
        float steering = 1.2f;
        bool reaching = false;
        Vector3 target;
        if (player is not null)
        {
            // At the player's body, fast, talons out; once it has struck, it climbs on past and comes round again.
            var at = player.GlobalPosition + Vector3.Up * player.Stats.BodyHeight * 0.5f;
            steering = 2.5f;
            if (flight.PullAway > 0f)
            {
                flight.PullAway -= dt;
                var on = eagle.Velocity with { Y = 0f };
                target = position + (on.LengthSquared() > 0.01f ? on.Normalized() : Vector3.Forward) * 20f + Vector3.Up * 6f;
                speed = stats.FlySpeed;
            }
            else
            {
                target = at;
                speed = stats.FlySpeed * 1.2f;
                float gap = position.DistanceTo(at);
                reaching = gap < 10f;
                if (gap < stats.BodyRadius * eagle.Size + player.Stats.BodyRadius + TalonsReach)
                {
                    player.Bitten(TalonStrike * eagle.Size, animal);
                    flight.PullAway = PassSeconds;
                }
            }
        }
        else if (flight.Courting && Player is { } suitor)
        {
            // Courting, it rides the air in close circles round the player's eagle, a little above it, now and then
            // swinging its talons out to touch, as a pair of eagles reach to lock talons in their courtship flights.
            var at = suitor.GlobalPosition;
            flight.Circle += 1.1f * dt;
            target = at + new Vector3(Mathf.Cos(flight.Circle), 0.4f, Mathf.Sin(flight.Circle)) * 7f;
            speed = Mathf.Min(stats.FlySpeed * 1.3f, suitor.Velocity.Length() + position.DistanceTo(target) * 0.8f + 3f);
            steering = 2.5f;
            reaching = position.DistanceTo(at) < 9f && Mathf.Sin(flight.Circle * 3f) > 0.7f;
            flight.Landing = false;
        }
        else if (flight.Loot is { } loot && !flight.Holding && _beasts.TryGetValue(loot, out var dropped))
        {
            // Diving after the dropped food, talons out, to snatch it as it falls or off the ground where it lands.
            target = dropped.Body.GlobalPosition;
            speed = stats.FlySpeed * 1.3f;
            steering = 3f;
            reaching = true;
            flight.Landing = false;
            if (position.DistanceTo(target) < LootReach + dropped.Animal.Stats.BodyRadius * dropped.Size)
            {
                // Live prey gets the talons driven home first.
                if (!dropped.IsDead)
                    Bite(loot, 1000f, position);
                flight.Holding = true;
                flight.Landing = true;
            }
        }
        else if (flight.Robbing && Player is { } robbed)
        {
            // Close behind and a little above the player's bird, talons reaching for what it carries, matching its
            // twists and turns and waiting for it to let go.
            var behind = robbed.Velocity with { Y = 0f };
            behind = behind.LengthSquared() > 0.25f ? behind.Normalized() : Vector3.Zero;
            var at = robbed.GlobalPosition;
            target = at - behind * 3f + Vector3.Up * 1.5f;
            speed = Mathf.Min(stats.FlySpeed * 1.3f, robbed.Velocity.Length() + position.DistanceTo(target) * 0.8f + 3f);
            steering = 3f;
            reaching = position.DistanceTo(at) < 7f;
            flight.Landing = false;
        }
        else if (fleeing)
        {
            var away = (position - group.FleeFrom) with { Y = 0f };
            away = away.LengthSquared() > 0.01f ? away.Normalized() : Vector3.Forward;
            target = position + away * 30f;
            target.Y = Surface(target) + flight.Height * 1.5f;
            speed = stats.FlySpeed;
            flight.Landing = false;
        }
        else if (flight.Landing)
        {
            // Spiralling in, it slows as it reaches the perch, and sets down on it.
            target = flight.Perch;
            float gap = position.DistanceTo(target);
            speed = Mathf.Clamp(gap * 0.4f, 2.5f, speed);
            if (gap < 1.2f)
            {
                Land(eagle, flight);
                return;
            }
        }
        else
        {
            // Rides wide circles over the water (an owl, low ones over its ground), aiming a little ahead round the circle.
            flight.Timer -= dt;
            flight.Circle += speed / flight.Radius * dt;
            var on = flight.Over + new Vector3(Mathf.Cos(flight.Circle + 0.4f), 0f, Mathf.Sin(flight.Circle + 0.4f)) * flight.Radius;
            target = on with { Y = Surface(on) + flight.Height };
            if (flight.Timer <= 0f)
                flight.Landing = true;
        }

        var toward = target - position;
        var wanted = toward.LengthSquared() > 0.01f ? toward.Normalized() * speed : Vector3.Zero;
        // Wings can climb only so steeply, but a stoop drops like a stone.
        wanted.Y = Mathf.Clamp(wanted.Y, -14f, 5f);
        eagle.Velocity = eagle.Velocity.Lerp(wanted, Mathf.Min(1f, steering * dt));
        position += eagle.Velocity * dt;

        // It keeps clear of the ground and the water, except when it comes down on its perch or snatches food off the ground.
        bool low = flight.Landing || (flight.Loot is not null && !flight.Holding);
        float floor = Surface(position) + (low ? 0f : 1f);
        if (position.Y < floor)
        {
            position.Y = floor;
            eagle.Velocity.Y = Mathf.Max(eagle.Velocity.Y, 0f);
        }
        eagle.Body.GlobalPosition = position;
        Pose(eagle, dt, reaching || flight.Holding, flapping: flight.PullAway > 0f || flight.Holding);

        // Food it has caught hangs from its talons all the way home.
        if (flight.Holding && flight.Loot is { } caught && eagle.Animal is Bird bird)
        {
            var heading = eagle.Velocity with { Y = 0f };
            Hold(caught, bird.Talons, heading.LengthSquared() > 0.01f ? heading.Normalized() : Vector3.Forward, inJaws: false);
        }
    }

    /// <summary>The height of the ground, or of the water where there is any, below a point.</summary>
    private float Surface(Vector3 at) => Mathf.Max(_terrain.GroundBelow(at), _water.SurfaceAt(at) ?? float.MinValue);

    /// <summary>
    /// Tips and banks the bird with its flight as the player's eagle does, and flaps when climbing, flying slowly or
    /// beating away after a strike, but soars on still wings otherwise. Close to its foe, it swings its talons forward.
    /// </summary>
    private void Pose(Beast eagle, float dt, bool reaching, bool flapping)
    {
        var animal = eagle.Animal;
        var level = eagle.Velocity with { Y = 0f };
        float flat = level.Length();
        float before = eagle.Yaw;
        if (flat > 0.5f)
            eagle.Yaw = Mathf.LerpAngle(eagle.Yaw, Mathf.Atan2(-level.X, -level.Z), Mathf.Min(1f, 4f * dt));
        float turn = Mathf.AngleDifference(before, eagle.Yaw) / Mathf.Max(dt, 0.001f);
        float bank = Mathf.Clamp(turn * 0.35f, -0.9f, 0.9f);
        float pitch = Mathf.Clamp(eagle.Velocity.Y / Mathf.Max(flat, 1f), -0.9f, 0.5f);
        float smooth = Mathf.Min(1f, 6f * dt);
        var rotation = animal.Rotation;
        animal.Rotation = new Vector3(Mathf.Lerp(rotation.X, pitch, smooth), eagle.Yaw, Mathf.Lerp(rotation.Z, bank, smooth));

        flapping |= eagle.Velocity.Y > 0.8f || flat < animal.Stats.FlySpeed * 0.45f;
        animal.Flap = Mathf.MoveToward(animal.Flap, flapping ? 1f : 0f, 4f * dt);
        animal.Dive = Mathf.MoveToward(animal.Dive, eagle.Velocity.Y < -4f ? 1f : 0f, 3f * dt);
        animal.IsFlying = true;
        animal.IsSwimming = false;
        if (animal is Bird bird)
            bird.Clutching = reaching;
        if (Seen(eagle))
            animal.Animate(flat, 0f, 0f, dt);
    }

    /// <summary>Sits up on its perch, turning to look out over the water.</summary>
    private void SitOnPerch(Beast eagle, Flight flight, float dt)
    {
        var animal = eagle.Animal;
        eagle.Body.GlobalPosition = flight.Perch;
        eagle.Velocity = Vector3.Zero;
        eagle.Yaw = Mathf.LerpAngle(eagle.Yaw, Yaw(flight.Over - flight.Perch), Mathf.Min(1f, 0.5f * dt));
        animal.Rotation = new Vector3(0f, eagle.Yaw, 0f);
        animal.IsFlying = false;
        animal.IsSwimming = false;
        animal.Flap = animal.Dive = 0f;
        animal.Settle(IsBrooding(eagle) && !flight.Eating ? Posture.Lying : Posture.Standing, 1f, dt);
        if (Seen(eagle))
            animal.Animate(0f, 0f, flight.Eating ? 1f : 0f, dt);
    }

    /// <summary>Springs off its perch with a few hard wingbeats, out the way it faces, to soar a while.</summary>
    private void TakeOff(Beast eagle, Flight flight)
    {
        // Any meal it's in the middle of stays where it lies on the perch.
        if (flight.Eating)
            LoseLoot(flight);
        flight.Airborne = true;
        flight.Landing = false;
        flight.Timer = _rng.RandfRange(SoarMin, SoarMax);
        var from = (eagle.Body.GlobalPosition - flight.Over) with { Y = 0f };
        flight.Circle = Mathf.Atan2(from.Z, from.X);
        var forward = new Vector3(-Mathf.Sin(eagle.Yaw), 0f, -Mathf.Cos(eagle.Yaw));
        eagle.Velocity = forward * 6f + Vector3.Up * 5f;
        eagle.Animal.Flap = 1f;
    }

    /// <summary>Sets down on its perch, folds its wings and sits a while.</summary>
    private void Land(Beast eagle, Flight flight)
    {
        flight.Airborne = false;
        flight.Landing = false;
        flight.Timer = _rng.RandfRange(PerchMin, PerchMax);
        eagle.Velocity = Vector3.Zero;
        eagle.Animal.Rotation = new Vector3(0f, eagle.Yaw, 0f);
        if (eagle.Animal is Bird bird)
            bird.Clutching = false;
        // It lays what it caught on the perch in front of its feet, and settles down to eat.
        if (flight.Holding && flight.Loot is { } loot)
        {
            var ahead = new Vector3(-Mathf.Sin(eagle.Yaw), 0f, -Mathf.Cos(eagle.Yaw));
            Drop(loot, lodged: flight.Perch + ahead * eagle.Animal.Stats.MouthDistance * eagle.Size);
            flight.Holding = false;
            flight.Eating = true;
        }
        SitOnPerch(eagle, flight, 0f);
    }

    /// <summary>Reborn, an eaglet starts out sitting on its parent's perch.</summary>
    private void EagleReborn(Beast eagle)
    {
        if (!_flights.TryGetValue(eagle, out var flight))
            return;
        flight.Airborne = flight.Landing = false;
        flight.PullAway = 0f;
        flight.Robbing = flight.Provoked = false;
        LoseLoot(flight);
        flight.Timer = _rng.RandfRange(PerchMin, PerchMax);
        eagle.Body.GlobalPosition = flight.Perch;
    }
}
