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
/// The wolves hunt the player in turn while it's a mammoth, sea otter or bald eagle on the ground, just as they would a
/// wild one. A lone snow leopard prowls the steppe, resting up between its wanderings, and wolves give any snow leopard,
/// wild or the player, a wide berth: a pack that finds one close by breaks off whatever it's doing, even a meal, and runs.
/// A big pack, though, turns the tables and hunts the cat, which runs for the nearest tree and climbs out of reach.
/// Any animal that falls is soon reborn as a youngster beside its group, and grows up.
/// </summary>
public partial class Wildlife : Node3D
{
    [Export] public int MammothHerds { get; set; } = 6;
    [Export] public int MammothsPerHerd { get; set; } = 5;
    /// <summary>How many wolves are in each pack. Real packs run from a handful to a dozen or more.</summary>
    [Export] public int[] PackSizes { get; set; } = [8, 5, 7];

    /// <summary>
    /// A pack this big no longer fears a snow leopard but hunts it, as big wolf packs drive off and even kill big cats.
    /// The cat runs from them, or climbs a tree out of their reach. Lose wolves and the pack loses its nerve again.
    /// </summary>
    [Export] public int BravePackSize { get; set; } = 7;
    [Export] public int OtterRafts { get; set; } = 2;
    [Export] public int OttersPerRaft { get; set; } = 3;

    /// <summary>Snow leopards live alone, each over a huge range, so there is only one in the world.</summary>
    [Export] public int SnowLeopards { get; set; } = 1;

    /// <summary>Little groups of arctic hares that nibble the steppe: quick, easy prey for anything that can catch them.</summary>
    [Export] public int HareGroups { get; set; } = 8;
    [Export] public int HaresPerGroup { get; set; } = 4;

    /// <summary>Reindeer herds, each with a calf among it, and moose cows with their calves: more prey for the wolves.</summary>
    [Export] public int ReindeerHerds { get; set; } = 3;
    [Export] public int ReindeerPerHerd { get; set; } = 6;
    [Export] public int MooseFamilies { get; set; } = 3;

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

    /// <summary>
    /// Health the player loses each second to every wolf biting it, if it's the size of a wolf. A grown mammoth holds
    /// out several times as long, an otter or eagle not so long (see <see cref="Player.Bitten"/>).
    /// </summary>
    [Export] public float PlayerBiteDamage { get; set; } = 1.5f;

    /// <summary>How close the wolves let the player come, as a mammoth, otter or eagle, before they set on it.</summary>
    [Export] public float PlayerSightRange { get; set; } = 35f;

    /// <summary>How close a snow leopard can come before a pack takes fright and runs.</summary>
    [Export] public float FearRange { get; set; } = 25f;

    /// <summary>
    /// Health the polar bear takes each second from prey of unit bulk while mauling it: a wolf or reindeer falls almost
    /// at once, a moose in a few seconds, a grown mammoth, tough as it is, only after nearly a minute. And from the player, if it's the size of a wolf.
    /// </summary>
    [Export] public float BearMaul { get; set; } = 60f;
    [Export] public float BearPlayerMaul { get; set; } = 15f;

    /// <summary>
    /// Chance that the wild snow leopard wants no mate and fights the player's off instead. Snow leopards are solitary
    /// and only tolerate each other in the mating season, so a meeting can as easily end in a scrap.
    /// </summary>
    [Export] public float RefuseChance { get; set; } = 0.5f;

    /// <summary>
    /// Health a fighting snow leopard's bites take from the player each second, if it's the size of a wolf: only its
    /// ordinary bites, never the leap-and-hold, so a player that fights back wins, and one that doesn't loses.
    /// </summary>
    [Export] public float RivalBite { get; set; } = 8f;

    /// <summary>Health a fighting snow leopard is worn down to before it gives in, rather than fighting to the death: half.</summary>
    private const float RivalGivesIn = 50f;

    /// <summary>
    /// Health a second a wound left by a grown snow leopard's clamped jaws first bleeds from prey of unit bulk. A big
    /// animal has more blood to lose, so a mammoth barely notices while a reindeer can bleed to death.
    /// </summary>
    [Export] public float BleedStrength { get; set; } = 6f;

    /// <summary>Seconds for a wound to bleed down to about a third as fast; it stops altogether a little after that.</summary>
    [Export] public float BleedSeconds { get; set; } = 4f;

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

        /// <summary>How fast it is falling, in m/s upward (so negative while it falls), once it has gone over an edge.</summary>
        public float Fall;
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

        /// <summary>How many snow leopards have their jaws clamped on it; any at all slow it to a stagger.</summary>
        public int Grips;

        public bool Gripped => Grips > 0;

        /// <summary>How many of those are pinning it down, being too big for it to drag; any at all hold it still.</summary>
        public int Pins;

        public bool Pinned => Pins > 0;

        /// <summary>Health lost each second to a wound left by a snow leopard's jaws; it ebbs away as the wound closes.</summary>
        public float Bleeding;

        /// <summary>Blood spurting from a bite and dripping from the wound while it bleeds, made the first time it's hurt.</summary>
        public CpuParticles3D? Drops;

        /// <summary>Seconds more blood spurts from the last bite or maul, on top of any steady bleeding.</summary>
        public float Spurting;

        /// <summary>Seconds since a drop last spotted the snow below it.</summary>
        public float DripTimer;

        /// <summary>The pool of blood spreading where it fell, made the first time it falls.</summary>
        public MeshInstance3D? Pool;

        /// <summary>True for a wolf the player killed as a wolf, which is reborn into the player's pack.</summary>
        public bool JoinsPlayer;

        /// <summary>The tree a snow leopard is making for, or up, to get away from a wolf pack.</summary>
        public Tree? Refuge;

        /// <summary>True from grabbing the trunk until back on the ground.</summary>
        public bool OnTree;

        /// <summary>How far up the trunk it is, which side of it, and 0..1 how far it has scrambled onto the top.</summary>
        public float ClimbHeight;
        public float ClimbAngle;
        public float Hop;

        /// <summary>How much meat and muscle it has, by its build: a grown mammoth outweighs an otter some fifty times over.</summary>
        public float Bulk => Animal.Stats.BodyRadius * Animal.Stats.BodyHeight * Size * Size;

        /// <summary>How many times over it shrugs off any harm: a mammoth's hide, hair and fat are a match for anything.</summary>
        public float Toughness => Animal is Mammoth ? Mammoth.Toughness : 1f;

        /// <summary>The bar over its head showing how much health it has left.</summary>
        public required HealthBar Bar { get; init; }
    }

    private enum Kind { Herd, Pack, Raft, Loner, Hares }

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

        /// <summary>
        /// True while the pack is hunting the player rather than a wild <see cref="Prey"/>; for a lone snow leopard,
        /// while it is fighting the player's snow leopard off rather than pairing up with it.
        /// </summary>
        public bool HuntsPlayer;

        /// <summary>
        /// A lone snow leopard's mind about the player's: null until they first meet, then true if it wants no mate and
        /// fights instead, every time the player comes close, until it is beaten.
        /// </summary>
        public bool? Refuses;

        /// <summary>True once a lone snow leopard has lost a fight with the player's and gives in to pairing up.</summary>
        public bool Beaten;

        /// <summary>Seconds the prey has been out of reach, up a tree or in the air, while the pack waits below.</summary>
        public float OutOfReach;

        // How long since they last saw danger (a hunter, or for wolves a snow leopard), so a fleeing group doesn't stop
        // the moment it drops out of view.
        public float Fleeing;
        public Vector3 FleeFrom;

        // Rafts only: the lake they live on, and whether they've hauled out on its shore to rest.
        public Lake Lake;
        public bool Ashore;
    }

    private readonly List<Group> _groups = [];

    /// <summary>
    /// Beyond this far from the camera an animal is lost in the fog, so it isn't drawn or animated: furry models are
    /// costly, and with a hundred of them about, most are out of sight at any moment.
    /// </summary>
    private const float DrawDistance = 250f;

    /// <summary>Where the camera is this frame, if there is one.</summary>
    private Vector3? _eye;

    /// <summary>Shows the animal if it's near enough to the camera to see, hides it if not, and says which.</summary>
    private bool Seen(Beast beast)
    {
        bool seen = _eye is not { } eye || beast.Body.GlobalPosition.DistanceSquaredTo(eye) < DrawDistance * DrawDistance;
        beast.Body.Visible = seen;
        if (seen)
            beast.Bar.Show(beast.Health, _eye);
        return seen;
    }
    private readonly Dictionary<Animal, Beast> _beasts = [];
    private readonly RandomNumberGenerator _rng = new();
    private Terrain _terrain = null!;
    private Water _water = null!;
    private BloodTrail _trail = null!;

    /// <summary>Seconds between drops of blood landing in the snow under a bleeding animal.</summary>
    private const float DripSeconds = 0.12f;

    /// <summary>
    /// A drop of blood from an animal with <paramref name="stats"/> lands in the snow somewhere under
    /// <paramref name="wound"/>, e.g. from the player while it is bitten.
    /// </summary>
    public void Drip(Vector3 wound, AnimalStats stats) =>
        _trail.Drip(wound, stats.BodyRadius * 0.5f, 0.15f * Mathf.Max(1f, stats.BodyRadius / 0.4f), _water);
    private float _time;

    private IEnumerable<Group> Herds => _groups.Where(g => g.Kind == Kind.Herd);
    private IEnumerable<Group> Packs => _groups.Where(g => g.Kind == Kind.Pack);

    /// <summary>True while a wolf pack is hunting the player.</summary>
    public bool WolvesHuntingPlayer => Packs.Any(p => p.Activity == Activity.Hunting && p.HuntsPlayer);

    /// <summary>
    /// Scatters the herds over the grassland and the packs between them, well away from the player's start, and puts the
    /// otters on the lakes, the first of them on the one nearest the start.
    /// </summary>
    public void Populate(Terrain terrain, Water water)
    {
        _terrain = terrain;
        _water = water;
        _trail = new BloodTrail { Name = "BloodTrail" };
        AddChild(_trail);
        _trail.Build(terrain);
        _rng.Seed = (ulong)Seed;

        for (int h = 0; h < MammothHerds; h++)
        {
            var herd = new Group { Kind = Kind.Herd, Spacing = 5.5f };
            herd.Centre = herd.Goal = Grazing(Vector3.Zero, 60f, terrain.HalfSize * 0.8f);
            for (int i = 0; i < MammothsPerHerd; i++)
            {
                // Like the player's herd, each has a calf among it.
                bool calf = i == MammothsPerHerd - 1;
                var mammoth = Add(new Mammoth(), herd, calf ? Vector3.Zero : HerdSlot(i), calf ? YoungSize : 1f - 0.06f * (i % 3));
                mammoth.Body.Name = $"Herd{h}Mammoth{i}";
            }
            _groups.Add(herd);
        }

        for (int p = 0; p < PackSizes.Length; p++)
        {
            var pack = new Group { Kind = Kind.Pack, Spacing = 2f };
            pack.Centre = pack.Goal = Grazing(Vector3.Zero, 60f, terrain.HalfSize * 0.8f);
            for (int i = 0; i < PackSizes[p]; i++)
            {
                var wolf = Add(new ArcticWolf(), pack, PackSlot(i), 1f - 0.06f * (i % 3));
                wolf.Body.Name = $"Pack{p}Wolf{i}";
            }
            // Packs start at different points in their day, so they don't all hunt at once.
            pack.Activity = Activity.Sleeping;
            pack.Timer = p * SleepSeconds * 0.3f;
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

        for (int g = 0; g < HareGroups; g++)
        {
            var hares = new Group { Kind = Kind.Hares, Spacing = 1.6f };
            hares.Centre = hares.Goal = Grazing(Vector3.Zero, 30f, terrain.HalfSize * 0.85f);
            for (int i = 0; i < HaresPerGroup; i++)
            {
                var hare = Add(new ArcticHare(), hares, HerdSlot(i), 1f - 0.07f * (i % 3));
                hare.Body.Name = $"Hares{g}Hare{i}";
            }
            _groups.Add(hares);
        }

        for (int h = 0; h < ReindeerHerds; h++)
        {
            var herd = new Group { Kind = Kind.Herd, Spacing = 3.2f };
            herd.Centre = herd.Goal = Grazing(Vector3.Zero, 60f, terrain.HalfSize * 0.85f);
            for (int i = 0; i < ReindeerPerHerd; i++)
            {
                bool calf = i == ReindeerPerHerd - 1;
                var deer = Add(new Reindeer(), herd, calf ? Vector3.Zero : HerdSlot(i), calf ? YoungSize : 1f - 0.05f * (i % 3));
                deer.Body.Name = $"Reindeer{h}Deer{i}";
            }
            _groups.Add(herd);
        }

        // A moose cow keeps her calf at her side for its first year; they browse the forest edges together.
        for (int m = 0; m < MooseFamilies; m++)
        {
            var family = new Group { Kind = Kind.Herd, Spacing = 3.5f };
            family.Centre = family.Goal = Grazing(Vector3.Zero, 60f, terrain.HalfSize * 0.85f);
            var cow = Add(new Moose(), family, new Vector3(-0.8f, 0f, 0f), 1f);
            cow.Body.Name = $"Moose{m}Cow";
            var calf = Add(new Moose(), family, new Vector3(0.6f, 0f, 0.4f), YoungSize);
            calf.Body.Name = $"Moose{m}Calf";
            _groups.Add(family);
        }

        for (int c = 0; c < SnowLeopards; c++)
        {
            var loner = new Group { Kind = Kind.Loner, Spacing = 1f };
            loner.Centre = loner.Goal = Grazing(Vector3.Zero, 80f, terrain.HalfSize * 0.8f);
            var cat = Add(new SnowLeopard(), loner, Vector3.Zero, 1f);
            cat.Body.Name = $"SnowLeopard{c}";
            _groups.Add(loner);
        }

        // Polar bears live along cold coasts, so this one keeps to the shores of the big lake.
        if (terrain.Lakes.Count > 1)
        {
            var big = terrain.Lakes[1];
            var shore = new Vector3(big.Centre.X, 0f, big.Centre.Y);
            var loner = new Group { Kind = Kind.Loner, Spacing = 1f };
            loner.Centre = loner.Goal = Grazing(shore, big.Radius + 15f, big.Radius + 80f);
            var bear = Add(new PolarBear(), loner, Vector3.Zero, 1f);
            bear.Body.Name = "PolarBear0";
            _groups.Add(loner);
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

        var bar = new HealthBar { Position = new Vector3(0f, animal.Stats.BodyHeight + 1f, 0f) };
        body.AddChild(bar);

        var beast = new Beast { Body = body, Animal = animal, Group = group, Slot = slot * group.Spacing, Size = size, Bar = bar };
        beast.Yaw = _rng.RandfRange(-Mathf.Pi, Mathf.Pi);
        animal.Rotation = new Vector3(0f, beast.Yaw, 0f);
        group.Members.Add(beast);
        _beasts[animal] = beast;
        return beast;
    }

    /// <summary>
    /// A herd mills about in a loose crowd round its middle, which is kept for the calf, where the cows can shield it.
    /// </summary>
    private static Vector3 HerdSlot(int index) => index switch
    {
        0 => new Vector3(-1f, 0f, -0.6f),
        1 => new Vector3(1f, 0f, -0.3f),
        2 => new Vector3(0.2f, 0f, 1f),
        3 => new Vector3(-0.9f, 0f, 0.8f),
        _ => new Vector3(index % 2 == 0 ? -1.4f : 1.4f, 0f, index * 0.25f - 0.6f),
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
    /// unit bulk; a big animal shrugs off more, so an otter dies to one bite, a wolf to a few and a grown mammoth, tougher
    /// still, only to a great many. Its herd or raft flees from <paramref name="from"/>. Returns true if the bite killed it, or beat a
    /// snow leopard that was fighting the player, so the player stops attacking it either way.
    /// </summary>
    public bool Bite(Animal prey, float strength, Vector3 from)
    {
        if (!_beasts.TryGetValue(prey, out var beast) || beast.IsDead || beast.Group.Beaten)
            return false;

        if (Wound(beast, strength / beast.Bulk))
            return true;
        // A snow leopard fighting the player's stands its ground rather than running.
        if (!beast.Group.IsPack && !beast.Group.HuntsPlayer)
            Scare(beast.Group, from);
        return beast.IsDead;
    }

    /// <summary>
    /// Takes <paramref name="damage"/> from a wild animal's health on the player's account, by bite or by bleeding.
    /// Returns true if it beat a snow leopard that was fighting the player: once worn down to half, it gives in rather
    /// than fighting to the death.
    /// </summary>
    private bool Wound(Beast beast, float damage)
    {
        beast.Health -= damage / beast.Toughness;
        Spill(beast);
        if (beast.Group.Kind == Kind.Loner && beast.Group.HuntsPlayer && beast.Health <= RivalGivesIn)
        {
            beast.Health = RivalGivesIn;
            beast.Group.HuntsPlayer = false;
            beast.Group.Beaten = true;
            beast.Bleeding = 0f;
            return true;
        }
        if (beast.IsDead && beast.Group.IsPack && Player?.Animal is ArcticWolf)
            beast.JoinsPlayer = true;
        return false;
    }

    /// <summary>
    /// The player's snow leopard has let go of a wild animal, leaving a wound that bleeds for a while after, the worse
    /// the bigger the cat that made it (<paramref name="strength"/> is its size squared).
    /// </summary>
    public void Bleed(Animal prey, float strength)
    {
        if (!_beasts.TryGetValue(prey, out var beast) || beast.IsDead || beast.Group.Beaten)
            return;
        beast.Bleeding = Mathf.Max(beast.Bleeding, BleedStrength * strength / beast.Bulk);
        Spill(beast);
    }

    /// <summary>Blood spurts from a wild animal for a moment, as it does from every bite, maul or wound.</summary>
    private static void Spill(Beast beast)
    {
        if (beast.Drops is null)
        {
            beast.Drops = Blood.Drops(beast.Animal.Stats);
            beast.Body.AddChild(beast.Drops);
        }
        beast.Spurting = Mathf.Max(beast.Spurting, 0.4f);
    }

    /// <summary>The wound bleeds, slower and slower, until it stops; blood drips while it does.</summary>
    private void Bleed(Beast beast, float dt)
    {
        if (beast.Bleeding > 0f && !beast.IsDead && !beast.Group.Beaten)
        {
            Wound(beast, beast.Bleeding * dt);
            beast.Bleeding *= Mathf.Exp(-dt / BleedSeconds);
            if (beast.Bleeding < 0.3f)
                beast.Bleeding = 0f;
        }
        else
        {
            beast.Bleeding = 0f;
        }
        // Blood runs while the wound bleeds, while jaws are clamped on it, and for a moment after every bite.
        beast.Spurting = Mathf.Max(0f, beast.Spurting - dt);
        if (beast.Drops is not { } drops)
            return;
        drops.Emitting = beast.Bleeding > 0f || beast.Spurting > 0f || (beast.Gripped && !beast.IsDead);

        // Every so often while it bleeds, a drop spots the snow below, leaving a trail behind it.
        beast.DripTimer += dt;
        if (drops.Emitting && beast.DripTimer > DripSeconds)
        {
            beast.DripTimer = 0f;
            Drip(drops.GlobalPosition, beast.Animal.Stats.GrownTo(beast.Size));
        }
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

    private static void Scare(Group group, Vector3 from, float seconds = 8f)
    {
        group.Fleeing = seconds;
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
                case Kind.Hares:
                    ThinkHerd(group, dt);
                    break;
                case Kind.Raft:
                    ThinkRaft(group, dt);
                    break;
                case Kind.Loner:
                    if (group.Members[0].Animal is PolarBear)
                        ThinkBear(group, dt);
                    else
                        ThinkLoner(group, dt);
                    break;
            }
            Recover(group, dt);
        }

        // Animals far off in the fog are neither drawn nor animated, though they go on living their lives out there.
        _eye = GetViewport().GetCamera3D()?.GlobalPosition;
        foreach (var group in _groups)
            foreach (var beast in group.Members)
                Move(beast, dt);
    }

    /// <summary>
    /// Decides what a pack is doing: roaming, hunting a mammoth or the player, feeding on a kill or sleeping it off, or
    /// running from a snow leopard.
    /// </summary>
    private void ThinkPack(Group group, float dt)
    {
        var living = group.Members.Where(w => !w.IsDead).ToList();

        // A polar bear close by, or for a small pack a snow leopard, sends the pack running, whatever it was doing, and it
        // stays clear a while after.
        if (FearedNear(living, IsBrave(group)) is { } danger)
        {
            Scare(group, danger, seconds: 15f);
            group.Activity = Activity.Roaming;
            group.Prey = null;
            group.HuntsPlayer = false;
            group.Timer = 0f;
            group.Linger = 0f;
        }
        group.Fleeing = Mathf.Max(0f, group.Fleeing - dt);
        if (group.Fleeing > 0f)
        {
            RunFrom(group, dt);
            return;
        }

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
                if (living.Count > 0 && PlayerInSight(group))
                {
                    // A lone stranger on the steppe is the easiest prey there is.
                    group.HuntsPlayer = true;
                    group.Prey = null;
                    group.Activity = Activity.Hunting;
                    group.Timer = 0f;
                }
                else if (living.Count > 0 && FindPrey(group) is { } prey)
                {
                    group.Prey = prey;
                    group.Activity = Activity.Hunting;
                    group.Timer = 0f;
                }
                break;

            case Activity.Hunting when group.HuntsPlayer:
                var player = Player!;
                if (player.IsDead)
                {
                    // They've brought the player down: feed where it fell.
                    group.HuntsPlayer = false;
                    group.Activity = Activity.Feeding;
                    group.Timer = 0f;
                    group.Centre = player.GlobalPosition;
                }
                else
                {
                    // Up a tree or in the air, the player is out of reach; the wolves wait below a while, then give up.
                    group.OutOfReach = player.OnTheGround ? 0f : group.OutOfReach + dt;
                    if (!IsPlayerPrey(group) || Spent(group, living)
                        || living.Min(w => w.Body.GlobalPosition.DistanceTo(player.GlobalPosition)) > SightRange)
                    {
                        // It got away (outran them, flew off, climbed out of reach, or became something they won't
                        // take on), or they're spent.
                        GiveUp(group, living);
                    }
                    else
                    {
                        group.Centre = player.GlobalPosition;
                        group.Goal = group.Centre;
                    }
                }
                break;

            case Activity.Hunting:
                var target = group.Prey!;
                group.OutOfReach = target.OnTree ? group.OutOfReach + dt : 0f;
                if (target.IsDead)
                {
                    group.Activity = Activity.Feeding;
                    group.Timer = 0f;
                    group.Centre = target.Body.GlobalPosition;
                }
                else if (Spent(group, living))
                {
                    // It outran them, or got up a tree, or they're spent: rest a while before trying again.
                    GiveUp(group, living);
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
    /// Whether the hunt is over: half the pack is winded, it has gone on too long, or the prey has been up a tree long
    /// enough for the wolves to see they can't get at it.
    /// </summary>
    private bool Spent(Group pack, List<Beast> living) =>
        living.Count(w => w.IsExhausted) * 2 >= living.Count || pack.Timer > ChaseSeconds || pack.OutOfReach > 15f;

    /// <summary>The pack abandons the hunt and flops down where it stopped, rather than trailing after the prey.</summary>
    private void GiveUp(Group pack, List<Beast> living)
    {
        pack.HuntsPlayer = false;
        pack.Prey = null;
        pack.OutOfReach = 0f;
        pack.Activity = Activity.Sleeping;
        pack.Timer = SleepSeconds * 0.6f;
        if (living.Count > 0)
            pack.Centre = pack.Goal = living.Aggregate(Vector3.Zero, (sum, w) => sum + w.Body.GlobalPosition) / living.Count;
    }

    /// <summary>Whether a pack is big enough to take on a snow leopard rather than run from it.</summary>
    private bool IsBrave(Group pack) => pack.Members.Count(w => !w.IsDead) >= BravePackSize;

    /// <summary>
    /// Whether the player is an animal this pack takes on: a mammoth, otter or eagle, or, for a big enough pack, a snow
    /// leopard too.
    /// </summary>
    private bool IsPlayerPrey(Group pack) =>
        Player is { } player && (player.IsWolfPrey || (!player.IsDead && player.Animal is SnowLeopard && IsBrave(pack)));

    /// <summary>
    /// The pack picks the easiest mammoth in sight: the smaller and nearer the better, so a calf is singled out first.
    /// A big enough pack goes after a snow leopard on the ground before anything else, to be rid of its rival; a small
    /// one won't hunt anywhere near one.
    /// </summary>
    private Beast? FindPrey(Group pack)
    {
        bool brave = IsBrave(pack);
        var quarry = Herds.SelectMany(h => h.Members).Select(m => (beast: m, weight: 0.4f + m.Size));
        if (brave)
            quarry = quarry.Concat(_groups.Where(g => g.Kind == Kind.Loner).SelectMany(g => g.Members)
                .Where(c => c.Animal is SnowLeopard).Select(c => (beast: c, weight: 0.3f)));

        Beast? best = null;
        float bestScore = float.MaxValue;
        foreach (var (beast, weight) in quarry)
        {
            if (beast.IsDead || beast.OnTree || NearFeared(beast.Body.GlobalPosition, brave))
                continue;
            float distance = beast.Body.GlobalPosition.DistanceTo(pack.Centre);
            if (distance > SightRange)
                continue;
            float score = distance * weight;
            if (score < bestScore)
            {
                bestScore = score;
                best = beast;
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
        WatchForBears(herd);

        herd.Fleeing = Mathf.Max(0f, herd.Fleeing - dt);
        if (herd.Fleeing > 0f)
            RunFrom(herd, dt);
        else
            Wander(herd, dt, pace: 0.25f, range: 60f);
    }

    /// <summary>A group runs straight away from whatever scared it.</summary>
    private void RunFrom(Group group, float dt)
    {
        var away = (group.Centre - group.FleeFrom) with { Y = 0f };
        group.Goal = group.Centre + (away.LengthSquared() > 0.01f ? away.Normalized() : Vector3.Forward) * 40f;
        group.Heading = Yaw(away);
        // The group keeps together, so it runs only as fast as its slowest or most winded member can.
        Advance(group, group.Members.Where(m => !m.IsDead).Select(TopSpeed).DefaultIfEmpty(0f).Min(), dt);
    }

    /// <summary>
    /// The lone snow leopard prowls from one spot to the next at a slow, padding walk, and lies up a good while at each,
    /// as the big cats spend most of the day resting. Bitten, it bolts. When a big wolf pack comes near it makes for the
    /// nearest tree and climbs it, as leopards escape wild dogs and hyenas, and only comes down once the wolves are gone;
    /// it does the same when the polar bear comes after it, since the great bear can't climb;
    /// with no tree to hand, it runs.
    /// </summary>
    private void ThinkLoner(Group loner, float dt)
    {
        var cat = loner.Members[0];
        if (!cat.IsDead && cat.Animal is SnowLeopard && (BravePackNear(cat) ?? BearNear(cat)) is { } wolves)
        {
            Scare(loner, wolves, seconds: 10f);
            cat.Refuge ??= _terrain.NearestTree(cat.Body.GlobalPosition, 30f);
        }
        loner.Fleeing = Mathf.Max(0f, loner.Fleeing - dt);

        // Up a tree, or on its way to one, it leaves the rest to Move until it is back on the ground.
        if (cat.OnTree)
            return;
        if (cat.Refuge is not null)
        {
            if (loner.Fleeing > 0f)
                return;
            cat.Refuge = null;
        }

        if (loner.Fleeing > 0f)
        {
            RunFrom(loner, dt);
        }
        else if (loner.HuntsPlayer)
        {
            // Fighting the player's snow leopard: it goes for it until it gets away, up a tree or out of sight, or
            // falls, or stops being a snow leopard. Move does the biting.
            var player = Player!;
            if (player.IsDead || player.Animal is not SnowLeopard || !player.OnTheGround
                || cat.Body.GlobalPosition.DistanceTo(player.GlobalPosition) > PlayerSightRange)
            {
                loner.HuntsPlayer = false;
                loner.Centre = loner.Goal = cat.Body.GlobalPosition;
                loner.Linger = 0f;
            }
            else
            {
                loner.Centre = loner.Goal = player.GlobalPosition;
            }
        }
        else
        {
            Wander(loner, dt, pace: 0.4f, range: 80f);
        }
    }

    /// <summary>
    /// The polar bear, the biggest hunter on the steppe, fears nothing. It roams a while, then, hungry, goes after the
    /// nearest animal it sees: a reindeer, moose, mammoth, wolf, hare, otter or snow leopard, or the player as any of
    /// those. It runs its quarry down, mauls it until it falls, eats its fill and sleeps it off before roaming again.
    /// A chase ends when the bear is winded, the quarry gets well away or climbs a tree, or it has gone on too long.
    /// </summary>
    private void ThinkBear(Group bear, float dt)
    {
        var me = bear.Members[0];
        bear.Fleeing = 0f;
        if (me.IsDead)
            return;

        bear.Timer += dt;
        switch (bear.Activity)
        {
            case Activity.Sleeping when bear.Timer > SleepSeconds:
                bear.Activity = Activity.Roaming;
                bear.Timer = 0f;
                bear.Prey = null;
                break;

            case Activity.Feeding when bear.Timer > FeedSeconds * 1.5f:
                bear.Activity = Activity.Sleeping;
                bear.Timer = 0f;
                break;

            case Activity.Roaming:
                Wander(bear, dt, pace: 0.4f, range: 90f);
                if (bear.Timer < BearRoamSeconds)
                    break;
                if (BearSeesPlayer(me))
                {
                    bear.HuntsPlayer = true;
                    bear.Prey = null;
                    bear.Activity = Activity.Hunting;
                    bear.Timer = 0f;
                }
                else if (BearPrey(me) is { } prey)
                {
                    bear.Prey = prey;
                    bear.Activity = Activity.Hunting;
                    bear.Timer = 0f;
                }
                break;

            case Activity.Hunting when bear.HuntsPlayer:
                var player = Player!;
                bear.OutOfReach = player.OnTheGround ? 0f : bear.OutOfReach + dt;
                if (player.IsDead)
                {
                    bear.HuntsPlayer = false;
                    bear.Activity = Activity.Feeding;
                    bear.Timer = 0f;
                    bear.Centre = player.GlobalPosition;
                }
                else if (!IsBearPrey(player) || me.IsExhausted || bear.Timer > ChaseSeconds || bear.OutOfReach > 10f
                         || me.Body.GlobalPosition.DistanceTo(player.GlobalPosition) > SightRange)
                {
                    BearGivesUp(bear, me);
                }
                else
                {
                    bear.Centre = bear.Goal = player.GlobalPosition;
                }
                break;

            case Activity.Hunting:
                var target = bear.Prey!;
                bear.OutOfReach = target.OnTree ? bear.OutOfReach + dt : 0f;
                if (target.IsDead)
                {
                    bear.Activity = Activity.Feeding;
                    bear.Timer = 0f;
                    bear.Centre = target.Body.GlobalPosition;
                }
                else if (me.IsExhausted || bear.Timer > ChaseSeconds || bear.OutOfReach > 10f
                         || me.Body.GlobalPosition.DistanceTo(target.Body.GlobalPosition) > SightRange)
                {
                    BearGivesUp(bear, me);
                }
                else
                {
                    bear.Centre = bear.Goal = target.Body.GlobalPosition;
                }
                break;
        }
    }

    /// <summary>The quarry got away: the bear goes back to roaming from where it stopped, and soon looks for another.</summary>
    private static void BearGivesUp(Group bear, Beast me)
    {
        bear.HuntsPlayer = false;
        bear.Prey = null;
        bear.OutOfReach = 0f;
        bear.Activity = Activity.Roaming;
        bear.Timer = BearRoamSeconds * 0.5f;
        bear.Centre = bear.Goal = me.Body.GlobalPosition;
        bear.Linger = 0f;
    }

    /// <summary>Seconds the bear roams after waking before it is hungry enough to hunt.</summary>
    private const float BearRoamSeconds = 30f;

    /// <summary>
    /// The nearest wild animal the bear can see that it would take: anything but another bear, so long as it isn't out of
    /// reach up a tree.
    /// </summary>
    private Beast? BearPrey(Beast bear)
    {
        Beast? best = null;
        float bestDistance = SightRange;
        foreach (var group in _groups)
        {
            if (group == bear.Group)
                continue;
            foreach (var beast in group.Members)
            {
                if (beast.IsDead || beast.OnTree || beast.Animal is PolarBear)
                    continue;
                float distance = beast.Body.GlobalPosition.DistanceTo(bear.Body.GlobalPosition);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = beast;
                }
            }
        }
        return best;
    }

    /// <summary>Whether the bear will go after the player: on the ground, as any animal it hunts.</summary>
    private static bool IsBearPrey(Player player) =>
        !player.IsDead && player.OnTheGround && player.Animal is not (PolarBear or BaldEagle);

    private bool BearSeesPlayer(Beast bear) =>
        Player is { } player && IsBearPrey(player) && bear.Body.GlobalPosition.DistanceTo(player.GlobalPosition) < PlayerSightRange * 1.5f;

    /// <summary>True while the polar bear is hunting the player.</summary>
    public bool BearHuntingPlayer => Bears.Any(b => b.Activity == Activity.Hunting && b.HuntsPlayer);

    private IEnumerable<Group> Bears => _groups.Where(g => g.Kind == Kind.Loner && g.Members[0].Animal is PolarBear);

    /// <summary>
    /// Where a polar bear is, if one is close to this animal or hunting it: a snow leopard takes to a tree, since the bear
    /// can't follow it up there.
    /// </summary>
    private Vector3? BearNear(Beast prey)
    {
        foreach (var bear in Bears)
        {
            var at = bear.Members[0].Body.GlobalPosition;
            float wary = bear.Prey == prey ? SightRange : FearRange * 1.4f;
            if (!bear.Members[0].IsDead && at.DistanceTo(prey.Body.GlobalPosition) < wary)
                return at;
        }
        return null;
    }

    /// <summary>A hunting polar bear close by sends herds, rafts and hares running, as hunting wolves do.</summary>
    private void WatchForBears(Group group)
    {
        foreach (var bear in Bears)
        {
            var at = bear.Members[0].Body.GlobalPosition;
            if (bear.Activity == Activity.Hunting && at.DistanceTo(group.Centre) < SightRange * 0.6f)
                Scare(group, at);
        }
    }

    /// <summary>
    /// The nearest wolf of a pack big enough to threaten a snow leopard, if one is close, or is hunting this cat.
    /// </summary>
    private Vector3? BravePackNear(Beast cat)
    {
        var at = cat.Body.GlobalPosition;
        Vector3? nearest = null;
        float best = float.MaxValue;
        foreach (var pack in Packs.Where(IsBrave))
        {
            float wary = pack.Prey == cat ? SightRange : FearRange * 1.4f;
            foreach (var wolf in pack.Members)
            {
                float distance = wolf.Body.GlobalPosition.DistanceTo(at);
                if (!wolf.IsDead && distance < wary && distance < best)
                {
                    best = distance;
                    nearest = wolf.Body.GlobalPosition;
                }
            }
        }
        return nearest;
    }

    /// <summary>
    /// Whether the player is close enough, on the ground, and an animal this pack takes on (see <see cref="IsPlayerPrey"/>).
    /// A pack won't go near it while something it fears is about.
    /// </summary>
    private bool PlayerInSight(Group pack) =>
        Player is { OnTheGround: true } player && IsPlayerPrey(pack) && !NearFeared(player.GlobalPosition, IsBrave(pack))
        && pack.Members.Any(w => !w.IsDead && w.Body.GlobalPosition.DistanceTo(player.GlobalPosition) < PlayerSightRange);

    /// <summary>
    /// Where every animal a pack fears is, wild or the player. Any pack fears a polar bear, which can kill a wolf with
    /// one swipe; only a small pack fears a snow leopard, which a big one hunts instead.
    /// </summary>
    private IEnumerable<Vector3> Feared(bool brave)
    {
        bool Fears(Animal animal) => animal is PolarBear || (!brave && animal is SnowLeopard);
        var wild = _groups.Where(g => g.Kind == Kind.Loner).SelectMany(g => g.Members)
            .Where(c => !c.IsDead && Fears(c.Animal)).Select(c => c.Body.GlobalPosition);
        return Player is { IsDead: false } player && Fears(player.Animal) ? wild.Append(player.GlobalPosition) : wild;
    }

    /// <summary>Where something the pack fears has come within fright range of any of these wolves, if it has.</summary>
    private Vector3? FearedNear(List<Beast> wolves, bool brave)
    {
        foreach (var danger in Feared(brave))
            if (wolves.Any(w => w.Body.GlobalPosition.DistanceTo(danger) < FearRange))
                return danger;
        return null;
    }

    /// <summary>Whether something the pack fears is about near <paramref name="point"/>, so it won't go after anything there.</summary>
    private bool NearFeared(Vector3 point, bool brave) => Feared(brave).Any(d => d.DistanceTo(point) < FearRange * 1.5f);
    /// <summary>
    /// A raft floats about its lake, now and then hauling out on the shore to rest, as otters do to groom and sleep.
    /// That is when a snow leopard can catch them: in the water they far outswim it. Disturbed, they make for open water.
    /// </summary>
    private void ThinkRaft(Group raft, float dt)
    {
        WatchForPlayer(raft);
        WatchForBears(raft);
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
        if (Player is null || Player.IsDead || !Player.Animal.Stats.Can(Ability.Hunt))
            return;

        // Only a hunter that could take one of them is worth running from: mammoths pay an eagle no mind, but hares and
        // otters do.
        var them = group.Members[0].Animal.Stats;
        if (Player.Stats.BodyRadius * Player.Stats.BodyHeight * 20f < them.BodyRadius * them.BodyHeight)
            return;

        // A hunter on the ground that breaks into a run is spotted from far off. One in the air or up a tree is easy to
        // miss until it is right on top of them, which is how eagles catch hares.
        var hunter = Player.GlobalPosition;
        float ground = _terrain.GroundBelow(hunter);
        if (!Player.OnTheGround && hunter.Y - ground > 12f)
            return;
        bool rushing = Player.OnTheGround && (Player.Velocity with { Y = 0f }).Length() > Player.Stats.WalkSpeed * 1.2f;
        float wary = WaryRange * (rushing ? 3f : Player.OnTheGround ? 1f : 0.5f);
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
            Bleed(beast, dt);
            if (!beast.IsDead)
            {
                // A wound only starts to heal once it has stopped bleeding.
                if (beast.Bleeding <= 0f)
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
            {
                // A wolf the player beat in a fight as a wolf is reborn twice over: as a pup in the player's pack, the one
                // that beat it, and back in its own pack as well.
                if (beast.JoinsPlayer)
                    Player?.Recruit(beast.Animal);
                Rebirth(beast);
            }
        }
    }

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
        beast.Grips = 0;
        beast.Pins = 0;
        beast.JoinsPlayer = false;
        beast.Spurting = 0f;
        if (beast.Pool is { } pool)
            pool.Visible = false;
        // Reborn, a snow leopard makes up its own mind about the player's afresh.
        group.Refuses = null;
        group.HuntsPlayer = false;
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
            // Wolves are always on the move; a snow leopard lies up for long spells.
            group.Linger = _rng.RandfRange(8f, 25f) * (group.IsPack ? 0.4f : group.Kind == Kind.Loner ? 2.5f : 1f);
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
            if (beast.OnTree)
                FallFromTree(beast);
            Lie(beast, dt);
            return;
        }
        if (beast.OnTree)
        {
            ClimbTree(beast, dt);
            return;
        }

        // Where it's heading, and how hurried it is to get there: 1 runs flat out.
        var target = group.Centre + beast.Slot.Rotated(Vector3.Up, group.Heading);
        float hurry = 0.4f;
        float keepUp = 0f;
        Posture posture = Posture.Standing;
        float eat = 0f;
        int index = group.Members.IndexOf(beast);

        if (beast.Refuge is { } refuge)
        {
            // Racing for a tree: once at the trunk, it springs up it.
            var fromTrunk = (position - refuge.Transform.Origin) with { Y = 0f };
            if (fromTrunk.Length() < refuge.CollisionRadius + stats.BodyRadius * beast.Size + 0.4f)
            {
                beast.OnTree = true;
                beast.ClimbHeight = Mathf.Max(0f, position.Y - refuge.Transform.Origin.Y);
                beast.ClimbAngle = Mathf.Atan2(fromTrunk.Z, fromTrunk.X);
                beast.Hop = 0f;
                beast.Velocity = Vector3.Zero;
                beast.Animal.IsClimbing = true;
                return;
            }
            target = refuge.Transform.Origin;
            hurry = 1f;
        }
        else if (group.Fleeing > 0f)
        {
            hurry = 1f;
            keepUp = TopSpeed(beast);
        }
        else if (group.Kind == Kind.Loner && group.HuntsPlayer)
        {
            // A snow leopard fighting the player's closes in from whichever side it's on and bites whenever it's in
            // reach: ordinary bites only, never leaping on to hold on.
            var player = Player!;
            var fromPlayer = (position - player.GlobalPosition) with { Y = 0f };
            float ring = player.Stats.BodyRadius + stats.BodyRadius * beast.Size * 0.8f;
            target = player.GlobalPosition + (fromPlayer.LengthSquared() > 0.01f ? fromPlayer.Normalized() : Vector3.Back) * ring;
            hurry = 1f;
            keepUp = (player.Velocity with { Y = 0f }).Length();
            if (fromPlayer.Length() < ring + 0.8f && player.WithinBite(position))
            {
                player.Bitten(RivalBite * dt, beast.Animal);
                eat = 0.6f + 0.4f * Mathf.Sin(_time * 10f);
            }
        }
        else if (beast.Animal is PolarBear && group.Activity != Activity.Roaming)
        {
            float reach = stats.BodyRadius * beast.Size;
            switch (group.Activity)
            {
                case Activity.Hunting:
                    // Close in from whichever side it's on and maul the quarry once in reach of a swipe.
                    var prey = group.Prey;
                    var player = Player!;
                    var preyAt = prey?.Body.GlobalPosition ?? player.GlobalPosition;
                    float preyRadius = prey is not null ? prey.Animal.Stats.BodyRadius * prey.Size : player.Stats.BodyRadius;
                    var fromPrey = (position - preyAt) with { Y = 0f };
                    float ring = preyRadius + reach * 0.7f;
                    target = preyAt + (fromPrey.LengthSquared() > 0.01f ? fromPrey.Normalized() : Vector3.Back) * ring;
                    hurry = 1f;
                    keepUp = (prey?.Velocity ?? player.Velocity with { Y = 0f }).Length();
                    if (fromPrey.Length() < ring + reach * 0.6f && (prey is not null ? !prey.OnTree : player.WithinBite(position)))
                    {
                        if (prey is not null)
                        {
                            prey.Health -= BearMaul / Mathf.Max(0.05f, prey.Bulk) / prey.Toughness * dt;
                            Spill(prey);
                            if (!prey.Group.IsPack)
                                Scare(prey.Group, position);
                        }
                        else
                        {
                            player.Bitten(BearPlayerMaul * dt, beast.Animal);
                        }
                        eat = 0.6f + 0.4f * Mathf.Sin(_time * 8f);
                    }
                    break;

                case Activity.Feeding:
                case Activity.Sleeping:
                    // Feed at the kill, tearing at it, then lie down beside it to sleep.
                    var meal = group.Prey;
                    float beside = (meal?.Animal.Stats.BodyRadius ?? 0.5f) * (meal?.Size ?? 1f) + reach * 0.7f
                        + (group.Activity == Activity.Sleeping ? reach * 2f : 0f);
                    var away = (position - group.Centre) with { Y = 0f };
                    target = group.Centre + (away.LengthSquared() > 0.01f ? away.Normalized() : Vector3.Back) * beside;
                    hurry = 0.4f;
                    if ((target - position).Length() < reach)
                    {
                        if (group.Activity == Activity.Feeding)
                        {
                            eat = Tearing(beast, dt);
                            if (meal is not null)
                                meal.Remains = Mathf.Max(PickedClean, meal.Remains - dt / (FeedSeconds * 1.5f) * (1f - PickedClean));
                        }
                        else
                        {
                            posture = Posture.Lying;
                        }
                    }
                    break;
            }
        }
        else if (group.IsPack)
        {
            switch (group.Activity)
            {
                case Activity.Hunting:
                    // Each wolf closes in on its own side of the prey, ringing it so it can't turn on them all at once.
                    var prey = group.Prey;
                    var player = Player!;
                    var preyAt = prey?.Body.GlobalPosition ?? player.GlobalPosition;
                    float preyRadius = prey is not null ? prey.Animal.Stats.BodyRadius * prey.Size : player.Stats.BodyRadius;
                    float ring = preyRadius + stats.BodyRadius + 0.3f;
                    float angle = index * Mathf.Tau / group.Members.Count + _time * 0.3f;
                    target = preyAt + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ring;
                    hurry = 1f;
                    keepUp = (prey?.Velocity ?? player.Velocity with { Y = 0f }).Length();

                    // Close enough, it bites, and the more wolves on it the faster it falls.
                    // Measured level, since a mammoth wading or swimming sits lower in the water than the wolves.
                    if (((position - preyAt) with { Y = 0f }).Length() < ring + 0.8f && (prey is not null ? !prey.OnTree : player.WithinBite(position)))
                    {
                        if (prey is not null)
                        {
                            prey.Health -= BiteDamage / Mathf.Max(0.3f, prey.Size * prey.Size) / prey.Toughness * dt;
                            Spill(prey);
                        }
                        else
                            player.Bitten(PlayerBiteDamage * dt, beast.Animal);
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
        else if (group.Kind == Kind.Raft && group.Ashore && group.Linger > 0f && (target - position).Length() < 1.5f)
        {
            // Hauled out, the otters lie on their backs on the bank to rest.
            posture = Posture.Lying;
        }
        else if (group.Kind == Kind.Loner && group.Linger > 0f && (target - position).Length() < 1.5f)
        {
            // Arrived, the snow leopard lies up to rest until it moves on.
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

        // Pinned down by a snow leopard, it is held flat where it is.
        if (beast.Pinned && !beast.Animal.IsSwimming)
            posture = Posture.Lying;

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
        if (_terrain.TrunkNear(position, radius) is { } tree)
        {
            var fromTrunk = (position - tree.Transform.Origin) with { Y = 0f };
            position += fromTrunk.Normalized() * (tree.CollisionRadius + radius - fromTrunk.Length());
        }

        // Walk on the ground, or float at swimming depth over deep water.
        float ground = _terrain.GroundBelow(position);
        float? surface = _water.SurfaceAt(position);
        float floatDepth = stats.FloatDepth * beast.Size;
        bool swimming = surface.HasValue && surface.Value - ground > floatDepth;
        float height = swimming ? surface!.Value - floatDepth : ground;
        position.Y = Drop(beast, position.Y, height, OverEdge, soft: swimming, dt);
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
        if (eat > 0f && (group.IsPack || beast.Animal is PolarBear))
            facing = Yaw(group.Centre - position);
        beast.Yaw = Mathf.LerpAngle(beast.Yaw, facing, Mathf.Min(1f, (moving > 0.3f || eat > 0f ? stats.TurnSpeed : 0.5f) * dt));
        beast.Animal.Rotation = new Vector3(0f, beast.Yaw, 0f);

        // Grazers lower their heads for a mouthful now and then when standing about.
        if (stats.CanGraze && moving < 0.3f && !swimming)
            eat = Graze(beast, dt);

        // A bigger animal takes longer strides, so a youngster's legs patter faster to keep up.
        float gait = moving / beast.Size;
        beast.Animal.IsSwimming = swimming;
        float stride = swimming ? 1f : Mathf.Clamp(gait / stats.WalkSpeed, 0f, 1f);
        if (Seen(beast))
            beast.Animal.Animate(swimming ? gait + 2f : gait, stride, eat, dt);
    }

    /// <summary>
    /// The fastest the animal can go just now, from its scores as for the player: a sprint on land, a walk once it's
    /// exhausted, its own swimming speed in deep water, and slowed to a wade in the shallows.
    /// </summary>
    private static float TopSpeed(Beast beast)
    {
        var stats = beast.Animal.Stats;
        // With a snow leopard hanging off it, an animal can only stagger along, dragging the cat; pinned down, not at all.
        float gripped = beast.Pinned ? 0f : beast.Gripped ? GrippedSlowing : 1f;
        if (beast.Animal.IsSwimming)
            return stats.SwimSpeed * (beast.IsExhausted ? 0.6f : 1f) * gripped;
        float speed = beast.IsExhausted ? stats.WalkSpeed : stats.SprintSpeed;
        return (beast.IsWading ? speed * 0.6f : speed) * gripped;
    }

    /// <summary>How fast an animal with a snow leopard clamped onto it can still go, as a fraction of its usual speed.</summary>
    private const float GrippedSlowing = 0.3f;

    /// <summary>How grown a wild animal is, as a fraction of its full size.</summary>
    public float SizeOf(Animal animal) => _beasts.TryGetValue(animal, out var beast) ? beast.Size : 1f;

    /// <summary>
    /// The wild snow leopard within <paramref name="range"/> of <paramref name="at"/>, alive and on the ground, if there
    /// is one: a mate for the player's snow leopard.
    /// </summary>
    public Animal? MateNear(Vector3 at, float range) =>
        _groups.Where(g => g.Kind == Kind.Loner).SelectMany(g => g.Members)
            .FirstOrDefault(c => c.Animal is SnowLeopard && !c.IsDead && !c.OnTree && c.Body.GlobalPosition.DistanceTo(at) < range)
            ?.Animal;

    /// <summary>
    /// Whether the wild snow leopard <paramref name="mate"/> will pair up with the player's. It makes up its mind the
    /// first time they meet; one that wants no mate turns on the player's cat whenever it comes close, until beaten.
    /// </summary>
    public bool WillMate(Animal mate)
    {
        var group = _beasts[mate].Group;
        if (group.Beaten)
            return true;
        group.Refuses ??= _rng.Randf() < RefuseChance;
        if (group.Refuses == true && !group.HuntsPlayer)
        {
            group.HuntsPlayer = true;
            group.Fleeing = 0f;
        }
        return group.Refuses == false;
    }

    /// <summary>True while a wild snow leopard is fighting the player's.</summary>
    public bool RivalFighting => _groups.Any(g => g.Kind == Kind.Loner && g.HuntsPlayer);

    /// <summary>A wild snow leopard the player's has beaten in a fight, ready to pair up with it, if there is one.</summary>
    public Animal? BeatenRival() =>
        _groups.FirstOrDefault(g => g.Kind == Kind.Loner && g.Beaten)?.Members.FirstOrDefault(c => !c.IsDead)?.Animal;

    /// <summary>
    /// A wild animal leaves the wild for good to travel with the player, e.g. a snow leopard pairing up with the
    /// player's. Anything hunting it gives up. Returns where it was.
    /// </summary>
    public Vector3 Leave(Animal animal)
    {
        var beast = _beasts[animal];
        _beasts.Remove(animal);
        beast.Group.Members.Remove(beast);
        if (beast.Group.Members.Count == 0)
            _groups.Remove(beast.Group);
        foreach (var group in _groups.Where(g => g.Prey == beast))
        {
            group.Prey = null;
            group.Activity = Activity.Roaming;
        }
        var at = beast.Body.GlobalPosition;
        beast.Body.QueueFree();
        return at;
    }

    /// <summary>Where every living wild animal of the given kind is, e.g. for the map.</summary>
    public IEnumerable<Vector3> WhereAre(System.Type kind) =>
        _beasts.Values.Where(b => !b.IsDead && b.Animal.GetType() == kind).Select(b => b.Body.GlobalPosition);

    /// <summary>True while a wild animal is alive.</summary>
    public bool IsAlive(Animal animal) => _beasts.TryGetValue(animal, out var beast) && !beast.IsDead;

    /// <summary>
    /// A hunter clamps its jaws onto a wild animal and holds on, or lets go of it; several can hold on at once. One
    /// that <paramref name="pins"/> it, something too small to ride, holds it down where it is so it can't get away.
    /// </summary>
    public void Grip(Animal prey, bool holding, bool pins = false)
    {
        if (!_beasts.TryGetValue(prey, out var beast))
            return;
        beast.Grips = holding && !beast.IsDead ? beast.Grips + 1 : Mathf.Max(0, beast.Grips - 1);
        if (pins)
            beast.Pins = holding && !beast.IsDead ? beast.Pins + 1 : Mathf.Max(0, beast.Pins - 1);
    }

    /// <summary>
    /// A snow leopard up a tree: it scrambles up the trunk, as the player's cat climbs, and out onto the very top,
    /// where it lies watching the wolves below. Once they've been gone a while it climbs back down, faster than it went
    /// up, and goes on its way.
    /// </summary>
    private void ClimbTree(Beast cat, float dt)
    {
        var tree = cat.Refuge!.Value;
        var stats = cat.Animal.Stats;
        float speed = 1f + stats.Scores.Agility * 0.25f;
        bool safe = cat.Group.Fleeing <= 0f;
        float before = cat.ClimbHeight + cat.Hop;

        if (!safe)
        {
            if (cat.ClimbHeight < tree.ClimbTop)
                cat.ClimbHeight = Mathf.Min(tree.ClimbTop, cat.ClimbHeight + speed * dt);
            else
                cat.Hop = Mathf.MoveToward(cat.Hop, 1f, dt / 0.7f);
        }
        else if (cat.Animal.Lying > 0f)
        {
            // Get up before climbing down.
        }
        else if (cat.Hop > 0f)
        {
            cat.Hop = Mathf.MoveToward(cat.Hop, 0f, dt / 0.7f);
        }
        else
        {
            cat.ClimbHeight -= speed * 1.3f * dt;
            if (cat.ClimbHeight <= 0f)
            {
                FallFromTree(cat);
                var group = cat.Group;
                group.Centre = group.Goal = cat.Body.GlobalPosition;
                group.Linger = 0f;
                return;
            }
        }

        var outward = new Vector3(Mathf.Cos(cat.ClimbAngle), 0f, Mathf.Sin(cat.ClimbAngle));
        var onTrunk = tree.AxisAt(cat.ClimbHeight) + outward * (tree.RadiusAt(cat.ClimbHeight) + 0.02f);
        float t = Mathf.SmoothStep(0f, 1f, cat.Hop);
        cat.Body.GlobalPosition = onTrunk.Lerp(tree.Summit, t);
        cat.Animal.IsClimbing = t < 0.5f;

        // Clinging to the bark it faces the trunk, body tipped up it; on the top it stands level, then lies down.
        cat.Yaw = Yaw(-outward);
        cat.Animal.Rotation = new Vector3(Mathf.Pi / 2f * (1f - t), cat.Yaw, 0f);
        cat.Animal.Settle(cat.Hop >= 1f && !safe ? Posture.Lying : Posture.Standing, 1f, dt);
        bool moving = !Mathf.IsEqualApprox(before, cat.ClimbHeight + cat.Hop);
        if (Seen(cat))
            cat.Animal.Animate(moving ? speed : 0f, moving ? 1f : 0f, 0f, dt);
    }

    /// <summary>
    /// Back on the ground at the foot of its tree, or, if it dies up there, dropping out of it: it slips off clear of
    /// the trunk and falls, limp, the whole way down.
    /// </summary>
    private void FallFromTree(Beast cat)
    {
        if (cat.Refuge is { } tree)
        {
            var outward = new Vector3(Mathf.Cos(cat.ClimbAngle), 0f, Mathf.Sin(cat.ClimbAngle));
            var foot = tree.Transform.Origin + outward * (tree.CollisionRadius + cat.Animal.Stats.BodyRadius * cat.Size + 0.05f);
            float ground = _terrain.GetHeight(foot.X, foot.Z);
            cat.Body.GlobalPosition = foot with { Y = cat.IsDead ? Mathf.Max(ground, cat.Body.GlobalPosition.Y) : ground };
            cat.Fall = 0f;
            cat.Yaw = Yaw(outward);
        }
        cat.Refuge = null;
        cat.OnTree = false;
        cat.Hop = 0f;
        cat.ClimbHeight = 0f;
        cat.Animal.IsClimbing = false;
        cat.Animal.Rotation = new Vector3(0f, cat.Yaw, 0f);
    }

    /// <summary>
    /// How far above the ground an animal on the move has to find itself, in metres, before it is falling rather than
    /// just running downhill: off a cliff edge, say. Below that it keeps its feet on the slope.
    /// </summary>
    private const float OverEdge = 1.5f;

    /// <summary>
    /// Brings an animal at height <paramref name="y"/> down to <paramref name="height"/> (the ground, or where it floats).
    /// More than <paramref name="edge"/> above it, it falls under gravity, and a drop beyond what it can take hurts it,
    /// unless it lands <paramref name="soft"/>ly in the water; closer, it just follows the lie of the land. Returns its new height.
    /// </summary>
    private static float Drop(Beast beast, float y, float height, float edge, bool soft, float dt)
    {
        if (y - height <= edge && beast.Fall >= 0f)
        {
            beast.Fall = 0f;
            return Mathf.Lerp(y, height, Mathf.Min(1f, 10f * dt));
        }

        beast.Fall -= Animal.GravityOn(beast.Fall) * dt;
        y += beast.Fall * dt;
        if (y > height)
            return y;

        if (!soft && !beast.IsDead)
            beast.Health -= beast.Animal.Stats.FallDamage(Animal.DropHeight(-beast.Fall)) / beast.Toughness;
        beast.Fall = 0f;
        return height;
    }

    /// <summary>A fallen animal goes limp where it fell, shrinking as it is eaten and sinking away before it is reborn.</summary>
    private void Lie(Beast beast, float dt)
    {
        beast.Animal.Settle(Posture.Lying, 0.6f, dt);
        beast.Animal.Dead = Mathf.MoveToward(beast.Animal.Dead, 1f, dt / 0.8f);
        beast.Velocity = Vector3.Zero;

        // A body in the water floats; on land it lies on the ground, unless it's in a hunter's jaws or lodged up a tree.
        var position = beast.HeldAt is { } mouth ? mouth + Vector3.Down * beast.Animal.Stats.BodyRadius * beast.Size : beast.Body.GlobalPosition;
        float ground = _terrain.GroundBelow(position);
        float? surface = _water.SurfaceAt(position);
        bool afloat = surface.HasValue && surface.Value - ground > beast.Animal.Stats.FloatDepth * beast.Size;
        if (beast.HeldAt.HasValue)
            position.Y = Mathf.Max(position.Y, ground);
        else if (!beast.Perched)
            position.Y = Drop(beast, position.Y, afloat ? surface!.Value - beast.Animal.Stats.FloatDepth * beast.Size : ground, 0.05f,
                soft: afloat, dt);
        beast.Body.GlobalPosition = position;
        beast.Animal.Rotation = new Vector3(0f, beast.Yaw, 0f);
        beast.Animal.IsSwimming = afloat && !beast.Perched && !beast.HeldAt.HasValue;
        Pool(beast, afloat);

        // The last of it sinks into the snow as it rots away, just before the rebirth.
        float sink = Mathf.Clamp((beast.DeadTime - (RebirthSeconds - 6f)) / 6f, 0f, 1f);
        float remains = beast.Remains * (1f - sink);
        beast.Body.Scale = new Vector3(beast.Size, beast.Size * remains, beast.Size);
        if (Seen(beast))
            beast.Animal.Animate(0f, 0f, 0f, dt);
    }

    /// <summary>
    /// Blood pools in the snow where the animal fell, and goes on spreading a while. It stays where it fell even if a
    /// hunter carries the carcass off, but it washes away in water, and there is none under a carcass up a tree.
    /// </summary>
    private void Pool(Beast beast, bool afloat)
    {
        if (beast.Pool is null)
        {
            var rng = new RandomNumberGenerator();
            rng.Randomize();
            beast.Pool = Blood.Pool(beast.Animal.Stats, rng);
            AddChild(beast.Pool);
        }
        var pool = beast.Pool;
        if (!pool.Visible && beast.DeadTime < 1f && !afloat && !beast.Perched && !beast.HeldAt.HasValue
            && beast.Body.GlobalPosition.Y - _terrain.GroundBelow(beast.Body.GlobalPosition) < 0.5f)
            Blood.Spill(pool, _terrain, beast.Body.GlobalPosition);
        if (pool.Visible)
        {
            // It shrinks back into the snow with the last of the carcass, just before the rebirth.
            float fade = 1f - Mathf.Clamp((beast.DeadTime - (RebirthSeconds - 6f)) / 6f, 0f, 1f);
            Blood.Spread(pool, beast.DeadTime);
            pool.Scale *= new Vector3(fade, 1f, fade);
            pool.Visible = fade > 0f;
        }
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
