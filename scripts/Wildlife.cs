using System.Collections.Generic;
using System.Linq;
using Godot;

namespace IceAgeWorld;

/// <summary>
/// The wild animals that live in the world on their own, apart from the player: herds of woolly mammoths that graze
/// the steppe, packs of arctic wolves that roam it and hunt them, and rafts of sea otters that float about the lakes
/// and haul out on the shore to rest. When a pack is hungry and sights a mammoth it runs it down, singling out the
/// weakest it can (calves first, as real wolves do), surrounds it and brings it down. The wolves feed on the carcass,
/// then lie up beside it to sleep off the meal before roaming again. Mammoths that see the hunters coming stampede away.
/// Every animal runs, swims and tires by its own scores, just as it would under the player's control, so a chase is won
/// by speed and stamina: the faster wolves close in, but a herd that keeps ahead until the pack is winded gets away.
/// The player can hunt them too, as a snow leopard: herds and rafts that see it stalking close, or rushing in, flee.
/// Any animal that falls is soon reborn as a youngster beside its group, and grows up.
/// </summary>
public partial class Wildlife : Node3D
{
    [Export] public int MammothHerds { get; set; } = 3;
    [Export] public int MammothsPerHerd { get; set; } = 4;
    [Export] public int WolfPacks { get; set; } = 2;
    [Export] public int WolvesPerPack { get; set; } = 5;
    [Export] public int OtterRafts { get; set; } = 2;
    [Export] public int OttersPerRaft { get; set; } = 3;
    [Export] public int Seed { get; set; } = 77;

    /// <summary>How far a hungry pack can spot a mammoth, and how far a mammoth can spot hunting wolves.</summary>
    [Export] public float SightRange { get; set; } = 70f;

    /// <summary>
    /// How close a hunting player can get before prey notices it: creeping up at a walk gets within this, but rushing in
    /// at a run is spotted from three times as far.
    /// </summary>
    [Export] public float WaryRange { get; set; } = 8f;

    /// <summary>Health a grown mammoth loses each second to every wolf biting it. A calf has less to lose.</summary>
    [Export] public float BiteDamage { get; set; } = 3f;

    /// <summary>Health a wounded animal that escaped gets back each second.</summary>
    [Export] public float HealPerSecond { get; set; } = 0.5f;

    /// <summary>Seconds the pack feeds on a kill, then sleeps beside it, before it hunts again.</summary>
    [Export] public float FeedSeconds { get; set; } = 30f;
    [Export] public float SleepSeconds { get; set; } = 45f;

    /// <summary>
    /// Most hunts end when the prey is down or the wolves are spent; this is the longest one lasts regardless, e.g. when
    /// the prey keeps out of reach in a lake.
    /// </summary>
    [Export] public float ChaseSeconds { get; set; } = 90f;

    /// <summary>Seconds after it falls before an animal is reborn, and how long the youngster takes to grow up.</summary>
    [Export] public float RebirthSeconds { get; set; } = 40f;
    [Export] public float GrowUpSeconds { get; set; } = 120f;

    /// <summary>The player, whom herds and rafts keep a wary eye on while it's an animal that hunts.</summary>
    public Player? Player { get; set; }

    private const float YoungSize = 0.55f;

    /// <summary>Once its stamina runs out, an animal must get this much back before it can run again, as the player must.</summary>
    private const float RecoveredStamina = 25f;

    /// <summary>Below this much left, a carcass is skin and bone with nothing more to eat.</summary>
    private const float PickedClean = 0.35f;

    private sealed class Beast
    {
        public required Node3D Body { get; init; }
        public required Animal Animal { get; init; }
        public required Group Group { get; init; }

        /// <summary>Where it keeps to around its group's centre: +Z is behind, +X to the right.</summary>
        public required Vector3 Slot { get; init; }

        public Vector3 Velocity;
        public float Yaw;
        public float Size = 1f;

        /// <summary>Breath from 0 to 100, worked out from the animal's stamina score the same way as the player's.</summary>
        public float Stamina = 100f;

        /// <summary>Run dry, it can only walk until it has partly got its breath back.</summary>
        public bool IsExhausted;

        public bool IsWading;

        /// <summary>Its life, from 100; it falls at 0.</summary>
        public float Health = 100f;

        public bool IsDead => Health <= 0f;
        public float DeadTime;
        public float GrazeTimer;

        /// <summary>0..1 how much of the carcass is left, shrinking as it is eaten and then as it rots away.</summary>
        public float Remains = 1f;

        /// <summary>
        /// Where a hunter carrying the carcass holds it in its jaws, if one is. It doesn't rot while carried, so a cat
        /// can take its time hauling it up a tree.
        /// </summary>
        public Vector3? HeldAt;

        /// <summary>True once it has been left up a tree, where it stays put rather than dropping to the ground.</summary>
        public bool Perched;

        /// <summary>How much meat and muscle it has, by its build: a grown mammoth outweighs an otter some fifty times over.</summary>
        public float Bulk => Animal.Stats.BodyRadius * Animal.Stats.BodyHeight * Size * Size;
    }

    private enum Kind { Herd, Pack, Raft }

    private enum Activity { Roaming, Hunting, Feeding, Sleeping }

    private sealed class Group
    {
        public readonly List<Beast> Members = [];
        public required Kind Kind { get; init; }
        public Vector3 Centre;
        public Vector3 Goal;
        public float Heading;
        public float Spacing;

        public bool IsPack => Kind == Kind.Pack;

        /// <summary>Seconds left to linger where it arrived, grazing, sniffing about or resting, before moving on.</summary>
        public float Linger;

        // Packs only.
        public Activity Activity;
        public Beast? Prey;
        public float Timer;

        // Herds and rafts: how long since they last saw a hunter, so a fleeing group doesn't stop the moment it drops
        // out of view.
        public float Fleeing;
        public Vector3 FleeFrom;

        // Rafts only: the lake they live on, and whether they've hauled out on its shore to rest.
        public Lake Lake;
        public bool Ashore;
    }

    private readonly List<Group> _groups = [];
    private readonly Dictionary<Animal, Beast> _beasts = [];
    private readonly RandomNumberGenerator _rng = new();
    private Terrain _terrain = null!;
    private Water _water = null!;
    private float _time;

    private IEnumerable<Group> Herds => _groups.Where(g => g.Kind == Kind.Herd);
    private IEnumerable<Group> Packs => _groups.Where(g => g.Kind == Kind.Pack);

    /// <summary>
    /// Scatters the herds over the grassland and the packs between them, well away from the player's start, and puts the
    /// otters on the lakes, the first of them on the one nearest the start.
    /// </summary>
    public void Populate(Terrain terrain, Water water)
    {
        _terrain = terrain;
        _water = water;
        _rng.Seed = (ulong)Seed;

        for (int h = 0; h < MammothHerds; h++)
        {
            var herd = new Group { Kind = Kind.Herd, Spacing = 5.5f };
            herd.Centre = herd.Goal = Grazing(Vector3.Zero, 60f, terrain.HalfSize * 0.8f);
            for (int i = 0; i < MammothsPerHerd; i++)
            {
                // Like the player's herd, each has a calf among it.
                var mammoth = Add(new Mammoth(), herd, HerdSlot(i), i == MammothsPerHerd - 1 ? YoungSize : 1f - 0.06f * (i % 3));
                mammoth.Body.Name = $"Herd{h}Mammoth{i}";
            }
            _groups.Add(herd);
        }

        for (int p = 0; p < WolfPacks; p++)
        {
            var pack = new Group { Kind = Kind.Pack, Spacing = 2f };
            pack.Centre = pack.Goal = Grazing(Vector3.Zero, 60f, terrain.HalfSize * 0.8f);
            for (int i = 0; i < WolvesPerPack; i++)
            {
                var wolf = Add(new ArcticWolf(), pack, PackSlot(i), 1f - 0.06f * (i % 3));
                wolf.Body.Name = $"Pack{p}Wolf{i}";
            }
            // Packs start at different points in their day, so they don't both hunt at once.
            pack.Activity = Activity.Sleeping;
            pack.Timer = p * SleepSeconds * 0.6f;
            _groups.Add(pack);
        }

        for (int r = 0; r < OtterRafts && terrain.Lakes.Count > 0; r++)
        {
            var lake = terrain.Lakes[r % terrain.Lakes.Count];
            var raft = new Group { Kind = Kind.Raft, Spacing = 0.9f, Lake = lake };
            raft.Centre = raft.Goal = new Vector3(lake.Centre.X, lake.Surface, lake.Centre.Y);
            for (int i = 0; i < OttersPerRaft; i++)
            {
                var otter = Add(new SeaOtter(), raft, RaftSlot(i), 1f - 0.08f * (i % 2));
                otter.Body.Name = $"Raft{r}Otter{i}";
            }
            _groups.Add(raft);
        }
    }

    private Beast Add(Animal animal, Group group, Vector3 slot, float size)
    {
        var body = new Node3D();
        body.AddChild(animal);
        AddChild(body);
        var spot = group.Centre + slot * group.Spacing;
        body.Position = spot with { Y = Mathf.Max(_terrain.GetHeight(spot.X, spot.Z), _water.SurfaceAt(spot) ?? float.MinValue) };
        body.Scale = Vector3.One * size;

        var beast = new Beast { Body = body, Animal = animal, Group = group, Slot = slot * group.Spacing, Size = size };
        beast.Yaw = _rng.RandfRange(-Mathf.Pi, Mathf.Pi);
        animal.Rotation = new Vector3(0f, beast.Yaw, 0f);
        group.Members.Add(beast);
        _beasts[animal] = beast;
        return beast;
    }

    /// <summary>A herd mills about in a loose crowd, the calf in the middle where the cows can shield it.</summary>
    private static Vector3 HerdSlot(int index) => index switch
    {
        0 => new Vector3(-1f, 0f, -0.6f),
        1 => new Vector3(1f, 0f, -0.3f),
        2 => new Vector3(0.2f, 0f, 1f),
        3 => new Vector3(0f, 0f, 0f),
        _ => new Vector3(index % 2 == 0 ? -1.4f : 1.4f, 0f, index * 0.4f),
    };

    /// <summary>A pack travels in a V behind its lead wolf.</summary>
    private static Vector3 PackSlot(int index) =>
        index == 0 ? Vector3.Zero : new Vector3(index % 2 == 0 ? -1f : 1f, 0f, (index + 1) / 2 * 1.2f);

    /// <summary>Sea otters float in a tight huddle, side by side, as real rafts do so they don't drift apart.</summary>
    private static Vector3 RaftSlot(int index)
    {
        float angle = index * Mathf.Tau / 5f;
        return index == 0 ? Vector3.Zero : new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
    }

    /// <summary>A random spot on dry, open grassland between <paramref name="near"/> and <paramref name="far"/> metres from <paramref name="from"/>.</summary>
    private Vector3 Grazing(Vector3 from, float near, float far)
    {
        for (int attempt = 0; attempt < 60; attempt++)
        {
            float angle = _rng.RandfRange(0f, Mathf.Tau);
            float distance = _rng.RandfRange(near, far);
            var spot = from + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
            float limit = _terrain.HalfSize - 20f;
            spot.X = Mathf.Clamp(spot.X, -limit, limit);
            spot.Z = Mathf.Clamp(spot.Z, -limit, limit);
            if (_terrain.GrassAmount(spot.X, spot.Z) > 0.6f && _terrain.DistanceFromWater(spot.X, spot.Z) > 8f)
                return spot with { Y = _terrain.GetHeight(spot.X, spot.Z) };
        }
        return from;
    }

    /// <summary>The living wild animal whose body is within <paramref name="reach"/> of <paramref name="point"/>, nearest first.</summary>
    public Animal? PreyNear(Vector3 point, float reach) => Nearest(point, reach, b => !b.IsDead)?.Animal;

    /// <summary>A carcass within <paramref name="reach"/> of <paramref name="point"/> that still has meat on it.</summary>
    public Animal? CarcassNear(Vector3 point, float reach) => Nearest(point, reach, b => b.IsDead && b.Remains > PickedClean)?.Animal;

    private Beast? Nearest(Vector3 point, float reach, System.Func<Beast, bool> which)
    {
        Beast? best = null;
        float bestGap = reach;
        foreach (var beast in _beasts.Values)
        {
            if (!which(beast))
                continue;
            // Measured level from the edge of its body, so a mammoth can be reached at its flank, not just its middle;
            // but not from a branch overhead to the ground below, or the other way round.
            var offset = beast.Body.GlobalPosition - point;
            float radius = beast.Animal.Stats.BodyRadius * beast.Size;
            if (Mathf.Abs(offset.Y) > beast.Animal.Stats.BodyHeight * beast.Size + 1.5f)
                continue;
            float gap = (offset with { Y = 0f }).Length() - radius;
            if (gap < bestGap)
            {
                bestGap = gap;
                best = beast;
            }
        }
        return best;
    }

    /// <summary>
    /// The player bites a wild animal. <paramref name="strength"/> is how much health the bite takes from an animal of
    /// unit bulk; a big animal shrugs off more, so an otter dies to one bite, a wolf to a few and a grown mammoth only
    /// to dozens. Its herd or raft flees from <paramref name="from"/>. Returns true if the bite killed it.
    /// </summary>
    public bool Bite(Animal prey, float strength, Vector3 from)
    {
        if (!_beasts.TryGetValue(prey, out var beast) || beast.IsDead)
            return false;

        beast.Health -= strength / beast.Bulk;
        if (!beast.Group.IsPack)
            Scare(beast.Group, from);
        return beast.IsDead;
    }

    /// <summary>Takes one mouthful from a carcass. <paramref name="share"/> is how much of a unit-bulk carcass one mouthful eats.</summary>
    public bool EatFrom(Animal carcass, float share)
    {
        if (!_beasts.TryGetValue(carcass, out var beast) || !beast.IsDead || beast.Remains <= PickedClean)
            return false;

        beast.Remains = Mathf.Max(PickedClean, beast.Remains - share / Mathf.Max(beast.Bulk, 0.05f) * (1f - PickedClean));
        return true;
    }

    /// <summary>True while a carcass still has meat on it.</summary>
    public bool HasMeat(Animal carcass) => _beasts.TryGetValue(carcass, out var beast) && beast.IsDead && beast.Remains > PickedClean;

    /// <summary>Whether a carcass is light enough for a hunter that can lift <paramref name="maxBulk"/> to carry.</summary>
    public bool CanCarry(Animal carcass, float maxBulk) => _beasts.TryGetValue(carcass, out var beast) && beast.Bulk <= maxBulk;

    /// <summary>
    /// A hunter has the carcass in its jaws at <paramref name="mouth"/>, facing <paramref name="yaw"/>; call every frame
    /// while carrying it. It hangs below the jaws, held crosswise as big cats carry a kill, and drags along the ground
    /// when the jaws are low.
    /// </summary>
    public void Hold(Animal carcass, Vector3 mouth, float yaw)
    {
        if (!_beasts.TryGetValue(carcass, out var beast))
            return;
        beast.HeldAt = mouth;
        beast.Perched = false;
        beast.Yaw = yaw + Mathf.Pi / 2f;
    }

    /// <summary>
    /// The hunter lets go of the carcass: on the ground it falls to the ground, or, given <paramref name="lodged"/>, it
    /// stays there, e.g. draped over the branch or treetop the cat is standing on.
    /// </summary>
    public void Drop(Animal carcass, Vector3? lodged = null)
    {
        if (!_beasts.TryGetValue(carcass, out var beast))
            return;
        beast.HeldAt = null;
        beast.Perched = lodged.HasValue;
        if (lodged is { } spot)
            beast.Body.GlobalPosition = spot;
    }

    private static void Scare(Group group, Vector3 from)
    {
        group.Fleeing = 8f;
        group.FleeFrom = from;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _time += dt;

        foreach (var group in _groups)
        {
            switch (group.Kind)
            {
                case Kind.Pack:
                    ThinkPack(group, dt);
                    break;
                case Kind.Herd:
                    ThinkHerd(group, dt);
                    break;
                case Kind.Raft:
                    ThinkRaft(group, dt);
                    break;
            }
            Recover(group, dt);
        }

        foreach (var group in _groups)
            foreach (var beast in group.Members)
                Move(beast, dt);
    }

    /// <summary>Decides what a pack is doing: roaming, hunting a mammoth, feeding on a kill or sleeping it off.</summary>
    private void ThinkPack(Group group, float dt)
    {
        var living = group.Members.Where(w => !w.IsDead).ToList();
        group.Timer += dt;
        switch (group.Activity)
        {
            case Activity.Sleeping when group.Timer > SleepSeconds:
            case Activity.Feeding when group.Timer > FeedSeconds:
                group.Activity = group.Activity == Activity.Feeding ? Activity.Sleeping : Activity.Roaming;
                group.Timer = 0f;
                if (group.Activity == Activity.Roaming)
                    group.Prey = null;
                break;

            case Activity.Roaming:
                Wander(group, dt, pace: 0.55f, range: 90f);
                if (living.Count > 0 && FindPrey(group) is { } prey)
                {
                    group.Prey = prey;
                    group.Activity = Activity.Hunting;
                    group.Timer = 0f;
                }
                break;

            case Activity.Hunting:
                var target = group.Prey!;
                if (target.IsDead)
                {
                    group.Activity = Activity.Feeding;
                    group.Timer = 0f;
                    group.Centre = target.Body.GlobalPosition;
                }
                else if (living.Count(w => w.IsExhausted) * 2 >= living.Count || group.Timer > ChaseSeconds)
                {
                    // It outran them, or they're spent: rest a while before trying again.
                    group.Activity = Activity.Sleeping;
                    group.Timer = SleepSeconds * 0.6f;
                    group.Prey = null;
                }
                else
                {
                    group.Centre = target.Body.GlobalPosition;
                    group.Goal = group.Centre;
                }
                break;
        }
    }

    /// <summary>
    /// The pack picks the easiest mammoth in sight: the smaller and nearer the better, so a calf is singled out first.
    /// </summary>
    private Beast? FindPrey(Group pack)
    {
        Beast? best = null;
        float bestScore = float.MaxValue;
        foreach (var herd in Herds)
            foreach (var mammoth in herd.Members)
            {
                if (mammoth.IsDead)
                    continue;
                float distance = mammoth.Body.GlobalPosition.DistanceTo(pack.Centre);
                if (distance > SightRange)
                    continue;
                float score = distance * (0.4f + mammoth.Size);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = mammoth;
                }
            }
        return best;
    }

    /// <summary>
    /// A herd grazes its way slowly across the steppe. When wolves are hunting nearby, or a hunting player gets too close,
    /// it stampedes away, and keeps running a while after the hunters drop out of sight.
    /// </summary>
    private void ThinkHerd(Group herd, float dt)
    {
        foreach (var pack in Packs)
        {
            if (pack.Activity != Activity.Hunting)
                continue;
            var wolves = pack.Members[0].Body.GlobalPosition;
            if (wolves.DistanceTo(herd.Centre) < SightRange * 0.6f)
                Scare(herd, wolves);
        }
        WatchForPlayer(herd);

        herd.Fleeing = Mathf.Max(0f, herd.Fleeing - dt);
        if (herd.Fleeing > 0f)
        {
            var away = (herd.Centre - herd.FleeFrom) with { Y = 0f };
            herd.Goal = herd.Centre + (away.LengthSquared() > 0.01f ? away.Normalized() : Vector3.Forward) * 40f;
            herd.Heading = Yaw(away);
            // The herd keeps together, so it runs only as fast as its slowest or most winded member can.
            Advance(herd, herd.Members.Where(m => !m.IsDead).Select(TopSpeed).DefaultIfEmpty(0f).Min(), dt);
        }
        else
        {
            Wander(herd, dt, pace: 0.25f, range: 60f);
        }
    }

    /// <summary>
    /// A raft floats about its lake, now and then hauling out on the shore to rest, as otters do to groom and sleep.
    /// That is when a snow leopard can catch them: in the water they far outswim it. Disturbed, they make for open water.
    /// </summary>
    private void ThinkRaft(Group raft, float dt)
    {
        WatchForPlayer(raft);
        var lake = raft.Lake;
        var middle = new Vector3(lake.Centre.X, lake.Surface, lake.Centre.Y);

        raft.Fleeing = Mathf.Max(0f, raft.Fleeing - dt);
        if (raft.Fleeing > 0f)
        {
            // Out to the far side of the lake from the danger.
            var away = ((middle - raft.FleeFrom) with { Y = 0f }).Normalized();
            raft.Goal = middle + away * lake.Radius * 0.5f;
            raft.Ashore = false;
            raft.Linger = 0f;
            var toSafety = (raft.Goal - raft.Centre) with { Y = 0f };
            if (toSafety.Length() > 1f)
            {
                raft.Heading = Yaw(toSafety);
                Advance(raft, raft.Members.Where(m => !m.IsDead).Select(TopSpeed).DefaultIfEmpty(0f).Min(), dt);
            }
            return;
        }

        if (raft.Linger > 0f)
        {
            // Rested: pick the next spot, now and then a stretch of shore to haul out on.
            raft.Linger -= dt;
            if (raft.Linger <= 0f)
            {
                float angle = _rng.RandfRange(0f, Mathf.Tau);
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                raft.Ashore = _rng.Randf() < 0.35f;
                raft.Goal = middle + direction * (raft.Ashore ? lake.Radius + 2.5f : _rng.RandfRange(0f, lake.Radius * 0.6f));
            }
            return;
        }

        if ((raft.Goal - raft.Centre).Length() < 1f)
        {
            // Arrived: rest here a while, longer on the bank.
            raft.Linger = _rng.RandfRange(10f, 30f) * (raft.Ashore ? 1.5f : 1f);
            return;
        }

        var toGoal = (raft.Goal - raft.Centre) with { Y = 0f };
        raft.Heading = Mathf.LerpAngle(raft.Heading, Yaw(toGoal), Mathf.Min(1f, 1.5f * dt));
        Advance(raft, Mathf.Min(raft.Members[0].Animal.Stats.SwimSpeed * 0.3f, toGoal.Length() * 2f), dt);
    }

    /// <summary>
    /// Herds and rafts keep an eye on a player that hunts: one creeping up at a walk gets close, but one that breaks
    /// into a run is spotted from much further off.
    /// </summary>
    private void WatchForPlayer(Group group)
    {
        if (Player is null || !Player.Animal.Stats.Can(Ability.Hunt))
            return;

        var hunter = Player.GlobalPosition;
        bool rushing = (Player.Velocity with { Y = 0f }).Length() > Player.Animal.Stats.WalkSpeed * 1.2f;
        float wary = WaryRange * (rushing ? 3f : 1f);
        foreach (var beast in group.Members)
            if (!beast.IsDead && ((beast.Body.GlobalPosition - hunter) with { Y = 0f }).Length() < wary + beast.Animal.Stats.BodyRadius * beast.Size)
            {
                Scare(group, hunter);
                return;
            }
    }

    /// <summary>
    /// Wounded animals that got away slowly heal, youngsters grow up, and the fallen are reborn once their carcass is gone.
    /// </summary>
    private void Recover(Group group, float dt)
    {
        foreach (var beast in group.Members)
        {
            if (!beast.IsDead)
            {
                beast.Health = Mathf.Min(100f, beast.Health + HealPerSecond * dt);

                // A youngster grows to full size over a couple of minutes.
                if (beast.Size < 1f && beast.Health >= 100f)
                {
                    beast.Size = Mathf.Min(1f, beast.Size + (1f - YoungSize) / GrowUpSeconds * dt);
                    beast.Body.Scale = Vector3.One * beast.Size;
                }
                continue;
            }

            if (!beast.HeldAt.HasValue)
                beast.DeadTime += dt;
            if (beast.DeadTime > RebirthSeconds)
                Rebirth(beast);
        }
    }

    /// <summary>The fallen animal's carcass is gone; it's born again as a youngster, at its group's side.</summary>
    private void Rebirth(Beast beast)
    {
        var group = beast.Group;
        var spot = group.Centre + beast.Slot.Rotated(Vector3.Up, group.Heading) * 0.5f;
        beast.Body.GlobalPosition = spot with { Y = Mathf.Max(_terrain.GetHeight(spot.X, spot.Z), _water.SurfaceAt(spot) ?? float.MinValue) };
        beast.Health = 100f;
        beast.DeadTime = 0f;
        beast.Remains = 1f;
        beast.HeldAt = null;
        beast.Perched = false;
        beast.Stamina = 100f;
        beast.IsExhausted = false;
        beast.Size = YoungSize;
        beast.Body.Scale = Vector3.One * YoungSize;
        beast.Velocity = Vector3.Zero;
        beast.Animal.Sitting = beast.Animal.Lying = 0f;
        beast.Animal.Dead = 0f;
        beast.Animal.Rotation = new Vector3(0f, beast.Yaw, 0f);

        // Any pack still dozing by the old carcass has nothing left to come back to.
        foreach (var pack in Packs)
            if (pack.Prey == beast)
                pack.Prey = null;
    }

    /// <summary>Drifts the group's centre towards its goal at a fraction of walking pace, picking a new goal on arrival.</summary>
    private void Wander(Group group, float dt, float pace, float range)
    {
        if (group.Linger > 0f)
        {
            group.Linger -= dt;
            return;
        }
        if ((group.Goal - group.Centre).Length() < 3f)
        {
            group.Goal = Grazing(group.Centre, range * 0.3f, range);
            group.Linger = _rng.RandfRange(8f, 25f) * (group.IsPack ? 0.4f : 1f);
            return;
        }

        var stats = group.Members[0].Animal.Stats;
        var toGoal = (group.Goal - group.Centre) with { Y = 0f };
        group.Heading = Mathf.LerpAngle(group.Heading, Yaw(toGoal), Mathf.Min(1f, 0.5f * dt));
        Advance(group, stats.WalkSpeed * pace, dt);
    }

    private void Advance(Group group, float speed, float dt)
    {
        var forward = new Vector3(-Mathf.Sin(group.Heading), 0f, -Mathf.Cos(group.Heading));
        group.Centre += forward * speed * dt;
        float limit = _terrain.HalfSize - 15f;
        group.Centre.X = Mathf.Clamp(group.Centre.X, -limit, limit);
        group.Centre.Z = Mathf.Clamp(group.Centre.Z, -limit, limit);
    }

    /// <summary>Works out where each animal wants to be and how fast, then walks or swims it there.</summary>
    private void Move(Beast beast, float dt)
    {
        var stats = beast.Animal.Stats;
        var group = beast.Group;
        var position = beast.Body.GlobalPosition;

        if (beast.IsDead)
        {
            Lie(beast, dt);
            return;
        }

        // Where it's heading, and how hurried it is to get there: 1 runs flat out.
        var target = group.Centre + beast.Slot.Rotated(Vector3.Up, group.Heading);
        float hurry = 0.4f;
        float keepUp = 0f;
        Posture posture = Posture.Standing;
        float eat = 0f;
        int index = group.Members.IndexOf(beast);

        if (group.IsPack)
        {
            switch (group.Activity)
            {
                case Activity.Hunting:
                    // Each wolf closes in on its own side of the prey, ringing it so it can't turn on them all at once.
                    var prey = group.Prey!;
                    float ring = prey.Animal.Stats.BodyRadius * prey.Size + stats.BodyRadius + 0.3f;
                    float angle = index * Mathf.Tau / group.Members.Count + _time * 0.3f;
                    target = prey.Body.GlobalPosition + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ring;
                    hurry = 1f;
                    keepUp = prey.Velocity.Length();

                    // Close enough, it bites, and the more wolves on it the faster it falls.
                    // Measured level, since a mammoth wading or swimming sits lower in the water than the wolves.
                    if (((position - prey.Body.GlobalPosition) with { Y = 0f }).Length() < ring + 0.8f)
                    {
                        prey.Health -= BiteDamage / Mathf.Max(0.3f, prey.Size * prey.Size) * dt;
                        eat = 0.6f + 0.4f * Mathf.Sin(_time * 12f + index);
                    }
                    break;

                case Activity.Feeding:
                case Activity.Sleeping:
                    // Crowd round the carcass to feed, then flop down near it to sleep.
                    var carcass = group.Prey;
                    var centre = group.Centre;
                    float reach = (carcass?.Animal.Stats.BodyRadius ?? 1f) * (carcass?.Size ?? 1f) + stats.BodyRadius + 0.6f;
                    if (group.Activity == Activity.Sleeping)
                        reach += 2.5f;
                    float around = index * Mathf.Tau / group.Members.Count;
                    target = centre + new Vector3(Mathf.Cos(around), 0f, Mathf.Sin(around)) * reach;
                    hurry = 0.5f;
                    if ((target - position).Length() < 1f)
                    {
                        if (group.Activity == Activity.Feeding)
                        {
                            eat = Tearing(beast, dt);
                            if (carcass is not null)
                                carcass.Remains = Mathf.Max(PickedClean, carcass.Remains - dt / (FeedSeconds * group.Members.Count) * (1f - PickedClean));
                        }
                        else
                        {
                            posture = Posture.Lying;
                        }
                    }
                    break;
            }
        }
        else if (group.Fleeing > 0f)
        {
            hurry = 1f;
            keepUp = TopSpeed(beast);
        }
        else if (group.Kind == Kind.Raft && group.Ashore && group.Linger > 0f && (target - position).Length() < 1.5f)
        {
            // Hauled out, the otters lie on their backs on the bank to rest.
            posture = Posture.Lying;
        }

        var toTarget = (target - position) with { Y = 0f };
        float distance = toTarget.Length();

        // Strays far from their group (e.g. left behind by a stampede) find their way back rather than being left alone.
        float top = TopSpeed(beast);
        float speed = distance < 0.5f ? 0f : Mathf.Min(stats.SprintSpeed * hurry + Mathf.Max(0f, distance - 10f) * 0.2f, top);
        // Ease off on arrival, but keep pace with a moving target (fleeing prey, or a stampeding herd) rather than
        // dropping behind it.
        speed = Mathf.Min(speed, distance * 2f + keepUp);
        var wanted = distance > 0.01f ? toTarget / distance * speed : Vector3.Zero;

        // Settle down to sleep or rest once stopped; get up before going anywhere.
        beast.Animal.Settle(posture, 1f, dt);
        if (beast.Animal.IsResting)
            wanted = Vector3.Zero;

        // Keep a body's length from the others in the group.
        foreach (var other in group.Members)
        {
            if (other == beast || other.IsDead)
                continue;
            var away = (position - other.Body.GlobalPosition) with { Y = 0f };
            float gap = away.Length();
            float room = (stats.BodyRadius * (beast.Size + other.Size)) + 0.3f;
            if (gap < room && gap > 0.001f)
                wanted += away / gap * (room - gap) * 3f;
        }

        beast.Velocity = beast.Velocity.Lerp(wanted, Mathf.Min(1f, stats.Acceleration * dt));
        position += beast.Velocity * dt;

        // Step round tree trunks rather than through them.
        float radius = stats.BodyRadius * beast.Size;
        if (_terrain.NearestTree(position, Terrain.TrunkCollisionRadius + radius) is { } tree)
        {
            var fromTrunk = (position - tree.Transform.Origin) with { Y = 0f };
            position += fromTrunk.Normalized() * (Terrain.TrunkCollisionRadius + radius - fromTrunk.Length());
        }

        // Walk on the ground, or float at swimming depth over deep water.
        float ground = _terrain.GetHeight(position.X, position.Z);
        float? surface = _water.SurfaceAt(position);
        float floatDepth = stats.FloatDepth * beast.Size;
        bool swimming = surface.HasValue && surface.Value - ground > floatDepth;
        float height = swimming ? surface!.Value - floatDepth : ground;
        position.Y = Mathf.Lerp(position.Y, height, Mathf.Min(1f, 10f * dt));
        beast.Body.GlobalPosition = position;
        beast.IsWading = !swimming && surface.HasValue && surface.Value - ground > stats.WadeDepth * beast.Size;

        // Running and swimming wind it as they do the player's animal; standing about, and above all lying down, gets its breath back.
        float moving = beast.Velocity.Length();
        float effort = swimming ? stats.SwimEffort * Mathf.Clamp(moving / stats.SwimSpeed, 0f, 1f)
            : Mathf.Clamp((moving - stats.WalkSpeed) / (stats.SprintSpeed - stats.WalkSpeed), 0f, 1f);
        if (effort > 0.05f)
            beast.Stamina = Mathf.Max(0f, beast.Stamina - 100f / stats.StaminaSeconds * effort * dt);
        else
            beast.Stamina = Mathf.Min(100f, beast.Stamina + 100f / stats.RecoverySeconds * (1f + beast.Animal.Lying) * dt);
        if (beast.Stamina <= 0f)
            beast.IsExhausted = true;
        else if (beast.Stamina >= RecoveredStamina)
            beast.IsExhausted = false;

        // Face the way it's going, or the group's way when standing about; feeding wolves face the carcass.
        float facing = moving > 0.3f ? Mathf.Atan2(-beast.Velocity.X, -beast.Velocity.Z) : group.Heading;
        if (eat > 0f && group.IsPack)
            facing = Yaw((group.Activity == Activity.Hunting ? group.Prey!.Body.GlobalPosition : group.Centre) - position);
        beast.Yaw = Mathf.LerpAngle(beast.Yaw, facing, Mathf.Min(1f, (moving > 0.3f || eat > 0f ? stats.TurnSpeed : 0.5f) * dt));
        beast.Animal.Rotation = new Vector3(0f, beast.Yaw, 0f);

        // Grazers lower their heads for a mouthful now and then when standing about.
        if (stats.CanGraze && moving < 0.3f && !swimming)
            eat = Graze(beast, dt);

        // A bigger animal takes longer strides, so a youngster's legs patter faster to keep up.
        float gait = moving / beast.Size;
        beast.Animal.IsSwimming = swimming;
        float stride = swimming ? 1f : Mathf.Clamp(gait / stats.WalkSpeed, 0f, 1f);
        beast.Animal.Animate(swimming ? gait + 2f : gait, stride, eat, dt);
    }

    /// <summary>
    /// The fastest the animal can go just now, from its scores as for the player: a sprint on land, a walk once it's
    /// exhausted, its own swimming speed in deep water, and slowed to a wade in the shallows.
    /// </summary>
    private static float TopSpeed(Beast beast)
    {
        var stats = beast.Animal.Stats;
        if (beast.Animal.IsSwimming)
            return stats.SwimSpeed * (beast.IsExhausted ? 0.6f : 1f);
        float speed = beast.IsExhausted ? stats.WalkSpeed : stats.SprintSpeed;
        return beast.IsWading ? speed * 0.6f : speed;
    }

    /// <summary>A fallen animal goes limp where it fell, shrinking as it is eaten and sinking away before it is reborn.</summary>
    private void Lie(Beast beast, float dt)
    {
        beast.Animal.Settle(Posture.Lying, 0.6f, dt);
        beast.Animal.Dead = Mathf.MoveToward(beast.Animal.Dead, 1f, dt / 0.8f);
        beast.Velocity = Vector3.Zero;

        // A body in the water floats; on land it lies on the ground, unless it's in a hunter's jaws or lodged up a tree.
        var position = beast.HeldAt is { } mouth ? mouth + Vector3.Down * beast.Animal.Stats.BodyRadius * beast.Size : beast.Body.GlobalPosition;
        float ground = _terrain.GetHeight(position.X, position.Z);
        float? surface = _water.SurfaceAt(position);
        bool afloat = surface.HasValue && surface.Value - ground > beast.Animal.Stats.FloatDepth * beast.Size;
        if (beast.HeldAt.HasValue)
            position.Y = Mathf.Max(position.Y, ground);
        else if (!beast.Perched)
            position.Y = afloat ? surface!.Value - beast.Animal.Stats.FloatDepth * beast.Size : ground;
        beast.Body.GlobalPosition = position;
        beast.Animal.Rotation = new Vector3(0f, beast.Yaw, 0f);
        beast.Animal.IsSwimming = afloat && !beast.Perched && !beast.HeldAt.HasValue;

        // The last of it sinks into the snow as it rots away, just before the rebirth.
        float sink = Mathf.Clamp((beast.DeadTime - (RebirthSeconds - 6f)) / 6f, 0f, 1f);
        float remains = beast.Remains * (1f - sink);
        beast.Body.Scale = new Vector3(beast.Size, beast.Size * remains, beast.Size);
        beast.Animal.Animate(0f, 0f, 0f, dt);
    }

    /// <summary>A wolf tugs and tears at the carcass, head down, in short jerks.</summary>
    private float Tearing(Beast wolf, float dt)
    {
        wolf.GrazeTimer += dt;
        return 0.75f + 0.25f * Mathf.Sin(wolf.GrazeTimer * 7f + wolf.Slot.X);
    }

    /// <summary>A 1.4 s mouthful of grass, then a pause that differs from mammoth to mammoth so they don't bob in unison.</summary>
    private static float Graze(Beast mammoth, float dt)
    {
        float period = 3.5f + mammoth.Slot.Length() % 2f;
        mammoth.GrazeTimer = (mammoth.GrazeTimer + dt) % period;
        return mammoth.GrazeTimer < 1.4f ? Mathf.Min(1f, Mathf.Sin(mammoth.GrazeTimer / 1.4f * Mathf.Pi) * 1.6f) : 0f;
    }

    /// <summary>Heading that faces a level direction, as a rotation about Y (the models face -Z).</summary>
    private static float Yaw(Vector3 direction) => Mathf.Atan2(-direction.X, -direction.Z);
}
