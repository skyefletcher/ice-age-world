using System.Collections.Generic;
using System.Linq;
using Godot;

namespace IceAgeWorld;

/// <summary>
/// Wild birds' nests. Every other wild bald eagle and snowy owl is one of a pair, with a nest of eggs: the eagles' a
/// great stick heap up the tree they perch in (or on the shore where there is none), the owls' a scrape on a tundra
/// hummock. The hen broods on the eggs, sitting tight on them, and the cock perches close by and soars over their
/// ground. A paired bird has no eye for the player's, but both fly at anything that comes near the eggs, whatever
/// kind of animal it is. A hungry hunter can raid the nest and eat the eggs, if it can reach them: any hunter on the
/// ground, but up a tree only a climbing cat, or a bird. If the eggs are all gone, the pair lays again a few minutes
/// later, as birds that lose a clutch early often do.
/// </summary>
public partial class Wildlife
{
    /// <summary>
    /// How many eggs a wild pair lays: a real bald eagle one to three, a snowy owl three to eleven, more when lemmings
    /// swarm.
    /// </summary>
    private static int WildClutch(Animal bird) => bird is SnowyOwl ? 5 : 3;

    /// <summary>How close to the eggs the player may come before the pair flies at it.</summary>
    private const float NestGuard = 22f;

    /// <summary>Seconds after a nest is emptied before the pair lays again.</summary>
    private const float RelaySeconds = 240f;

    /// <summary>How much an egg feeds a raider, out of 100: rich food, but only a mouthful or two for the big hunters.</summary>
    public const float EggNourishment = 12f;

    /// <summary>A wild pair's nest, the two birds that keep it, and seconds left until they lay again once it's empty.</summary>
    private sealed class WildNest
    {
        public required Nest Nest;
        public required Beast Hen;
        public required Beast Cock;
        public float Relay;
    }

    private readonly List<WildNest> _wildNests = [];

    /// <summary>
    /// The nest of the pair <paramref name="bird"/> belongs to, if it's one of a pair, so that it neither courts the
    /// player's bird nor lets anything near its eggs.
    /// </summary>
    private Nest? NestOf(Beast bird) => _wildNests.FirstOrDefault(n => n.Hen == bird || n.Cock == bird)?.Nest;

    /// <summary>
    /// Makes <paramref name="hen"/> one of a pair: builds her nest on her perch, up <paramref name="tree"/> if there is
    /// one, and adds her mate, perched close by.
    /// </summary>
    private void AddNest(Beast hen, Tree? tree)
    {
        var flight = _flights[hen];
        bool owl = hen.Animal is SnowyOwl;
        var nest = new Nest
        {
            Name = owl ? "WildOwlNest" : "WildEagleNest", Clutch = WildClutch(hen.Animal), Size = owl ? 1.6f : 3f,
            Scrape = owl, Tree = tree,
        };
        AddChild(nest);
        // Up a tree it's built up under her perch at the top; on the ground she climbs up onto it. Either way she sits
        // on top of it, over her eggs.
        nest.GlobalPosition = tree.HasValue ? flight.Perch + Vector3.Down * nest.CupHeight : flight.Perch;
        nest.Rotation = new Vector3(0f, _rng.RandfRange(0f, Mathf.Tau), 0f);
        hen.Animal.Sex = Sex.Female;
        flight.Perch = nest.Cup;
        hen.Body.GlobalPosition = flight.Perch;

        // Her mate keeps watch from a tree close by, or the ground beside the nest where there is none.
        Vector3 perch;
        if (!owl && _terrain.Trees.Where(t => t != tree).MinBy(t => t.Transform.Origin.DistanceSquaredTo(nest.GlobalPosition)) is { } other
            && other.Transform.Origin.DistanceTo(nest.GlobalPosition) < 40f)
        {
            perch = other.Summit;
        }
        else
        {
            float angle = _rng.RandfRange(0f, Mathf.Tau);
            perch = nest.GlobalPosition + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (owl ? 4f : 6f);
            perch.Y = _terrain.GetHeight(perch.X, perch.Z);
        }
        var group = new Group { Kind = Kind.Eagle, Spacing = 1f };
        group.Centre = group.Goal = perch;
        var cock = Add(owl ? new SnowyOwl() : new BaldEagle(), group, Vector3.Zero, 1f);
        cock.Body.Name = hen.Body.Name + "Mate";
        cock.Animal.Sex = Sex.Male;
        cock.Body.GlobalPosition = perch;
        _flights[cock] = new Flight
        {
            Perch = perch, Over = flight.Over, Height = flight.Height, Radius = flight.Radius,
            Timer = _rng.RandfRange(5f, PerchMax),
        };
        _groups.Add(group);
        _wildNests.Add(new WildNest { Nest = nest, Hen = hen, Cock = cock });
    }

    /// <summary>
    /// The pair flies at the player if it comes near the eggs; and a pair whose nest has been emptied lays again a few
    /// minutes later, while the hen lives.
    /// </summary>
    private void WatchWildNests(float dt)
    {
        foreach (var wild in _wildNests)
        {
            var nest = wild.Nest;
            if (nest.Eggs == 0)
            {
                wild.Relay += dt;
                if (wild.Relay > RelaySeconds && !wild.Hen.IsDead)
                {
                    wild.Relay = 0f;
                    nest.Lay();
                }
                continue;
            }
            if (Player is not { IsDead: false } player || player.GlobalPosition.DistanceTo(nest.Cup) > NestGuard)
                continue;
            foreach (var parent in new[] { wild.Hen, wild.Cock })
            {
                if (parent.IsDead || parent.Group.Fleeing > 0f || parent.Health < EagleDefends || parent.Group.HuntsPlayer)
                    continue;
                parent.Group.HuntsPlayer = true;
                var flight = _flights[parent];
                flight.Courting = flight.Robbing = false;
                LoseLoot(flight);
            }
        }
    }

    /// <summary>
    /// A wild nest with eggs in it whose cup is within <paramref name="reach"/> of <paramref name="mouth"/>, if any: the
    /// player can eat them.
    /// </summary>
    public Nest? WildNestNear(Vector3 mouth, float reach) =>
        _wildNests.Select(n => n.Nest).Where(n => n.Eggs > 0 && n.Cup.DistanceTo(mouth) < reach + n.Radius * 0.5f)
            .MinBy(n => n.Cup.DistanceSquaredTo(mouth));

    /// <summary>
    /// The player eats an egg from a wild nest: it's gone, and the pair, if they weren't already, are on the player in
    /// a fury, even ones that had been hurt.
    /// </summary>
    public void EatWildEgg(Nest nest)
    {
        if (_wildNests.FirstOrDefault(n => n.Nest == nest) is not { } wild)
            return;
        nest.Lose();
        foreach (var parent in new[] { wild.Hen, wild.Cock })
        {
            if (parent.IsDead || parent.Health < EagleGivesUp)
                continue;
            parent.Group.HuntsPlayer = true;
            parent.Group.Fleeing = 0f;
            _flights[parent].Provoked = true;
        }
    }

    /// <summary>True while <paramref name="bird"/> is sitting on its eggs: it lies down over them rather than stand.</summary>
    private bool IsBrooding(Beast bird) =>
        _wildNests.FirstOrDefault(n => n.Hen == bird) is { } wild && wild.Nest.Eggs > 0;
}
