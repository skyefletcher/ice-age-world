using System.Collections.Generic;
using Godot;

namespace IceAgeWorld;

/// <summary>
/// A bald eagle's nest with its clutch of eggs, built in code. Real ones are the biggest nests of any North American
/// bird: a broad, flat-topped heap of sticks a couple of metres across, crossed every which way, with a shallow cup in
/// the middle, and the eggs (dull, chalky white and about the size of a goose's) lying in it. The eggs go one by one
/// as they hatch. A snowy owl's is only a scrape: the hen hollows out a shallow bowl in the top of a hummock, where
/// the snow melts first and she can see all round, and lays her white eggs on the bare earth and moss.
/// </summary>
public partial class Nest : Node3D
{
    /// <summary>True for an owl's scrape on the ground, rather than an eagle's stick heap.</summary>
    public bool Scrape { get; init; }

    /// <summary>How big it is against a real one, to suit the eagles that build it.</summary>
    public float Size { get; init; } = 1f;

    /// <summary>How many eggs are laid in it.</summary>
    public int Clutch { get; init; } = 5;

    /// <summary>How many eggs are still unhatched.</summary>
    public int Eggs => _eggs.Count;

    /// <summary>The tree it's built in, if it's up one rather than on the ground: only a climber can raid it there.</summary>
    public Tree? Tree { get; init; }

    /// <summary>Across the stick heap, and how deep it is; a scrape's hummock stands only a little proud of the ground.</summary>
    private const float Width = 1.7f;
    private float Depth => Scrape ? 0.18f : 0.45f;

    private readonly List<Node3D> _eggs = [];

    public override void _Ready()
    {
        Scale = Vector3.One * Size;
        var rng = new RandomNumberGenerator { Seed = (ulong)GetInstanceId() };
        var bark = new StandardMaterial3D { AlbedoColor = new Color(0.36f, 0.27f, 0.18f), Roughness = 1f };
        var pale = new StandardMaterial3D { AlbedoColor = new Color(0.55f, 0.47f, 0.34f), Roughness = 1f };
        if (Scrape)
            BuildScrape(rng);
        else
            BuildHeap(rng, bark, pale);

        // Solid, so the bird can stand on it over her eggs.
        var solid = new StaticBody3D { Position = new Vector3(0f, Depth / 2f, 0f) };
        solid.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = Width * 0.4f, Height = Depth } });
        AddChild(solid);
        Lay();
    }

    /// <summary>
    /// A clutch of <see cref="Clutch"/> eggs is laid in the cup: the first as soon as the nest is built, and another
    /// whenever she comes back to nest in it again once the last clutch has hatched, as real eagles return to the same
    /// nest year after year.
    /// </summary>
    public void Lay()
    {
        // The eggs, together in the middle of the cup, each lying on its side; a big clutch spreads out round two rings.
        var shell = new StandardMaterial3D { AlbedoColor = new Color(0.93f, 0.91f, 0.84f), Roughness = 0.9f };
        int inner = Mathf.Min(Clutch, 5);
        for (int e = 0; e < Clutch; e++)
        {
            bool outer = e >= inner;
            int ring = outer ? Clutch - inner : inner;
            float angle = (outer ? e - inner + 0.5f : e) * Mathf.Tau / ring + 0.3f;
            float spread = outer ? 0.3f : 0.17f;
            var egg = new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 0.04f, Height = 0.08f, RadialSegments = 12, Rings = 6 },
                MaterialOverride = shell,
                Position = new Vector3(Mathf.Cos(angle) * spread, Depth + 0.055f, Mathf.Sin(angle) * spread),
                Rotation = new Vector3(Mathf.Pi / 2f, angle, 0f),
                // Twice the size of a real eagle's egg (about 7 cm long and 5.5 cm across), so they show up in the nest.
                Scale = new Vector3(1.4f, 1.8f, 1.4f),
            };
            AddChild(egg);
            _eggs.Add(egg);
        }
    }

    /// <summary>A low, rounded hummock of peaty earth under a skin of moss, with a shallow bowl scraped in its top.</summary>
    private void BuildScrape(RandomNumberGenerator rng)
    {
        var earth = new StandardMaterial3D { AlbedoColor = new Color(0.3f, 0.24f, 0.17f), Roughness = 1f };
        var moss = new StandardMaterial3D { AlbedoColor = new Color(0.42f, 0.45f, 0.28f), Roughness = 1f };
        AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = Width * 0.42f, BottomRadius = Width * 0.55f, Height = Depth, RadialSegments = 20 },
            MaterialOverride = moss,
            Position = new Vector3(0f, Depth / 2f, 0f),
        });
        // The bare bowl in the middle, where she has scraped the moss away with her feet and breast.
        AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.42f, BottomRadius = 0.36f, Height = 0.02f, RadialSegments = 16 },
            MaterialOverride = earth,
            Position = new Vector3(0f, Depth + 0.005f, 0f),
        });
        // Tufts of moss and old grass, and a few of her own moulted feathers, round the rim.
        var feather = new StandardMaterial3D { AlbedoColor = new Color(0.95f, 0.95f, 0.93f), Roughness = 1f };
        for (int t = 0; t < 14; t++)
        {
            float angle = (t + rng.Randf()) * Mathf.Tau / 14;
            bool down = t % 4 == 0;
            var at = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * rng.RandfRange(0.5f, 0.68f);
            AddChild(new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = down ? 0.05f : 0.08f, Height = down ? 0.03f : 0.1f, RadialSegments = 8, Rings = 4 },
                MaterialOverride = down ? feather : moss,
                Position = at with { Y = Depth + 0.01f },
                Rotation = new Vector3(0f, rng.RandfRange(0f, Mathf.Tau), 0f),
            });
        }
    }

    /// <summary>The eagle's great heap of sticks.</summary>
    private void BuildHeap(RandomNumberGenerator rng, StandardMaterial3D bark, StandardMaterial3D pale)
    {
        // Sticks laid round and round in layers, each crossing the last, the heap narrowing a little towards the top
        // where the rim is.
        const int layers = 6;
        for (int layer = 0; layer < layers; layer++)
        {
            float height = Depth * layer / (layers - 1);
            float ring = Width / 2f * (1f - 0.15f * layer / (layers - 1));
            int sticks = 16;
            for (int s = 0; s < sticks; s++)
            {
                float angle = (s + rng.Randf()) * Mathf.Tau / sticks + layer * 0.4f;
                float length = rng.RandfRange(0.7f, 1.1f);
                var stick = new MeshInstance3D
                {
                    Mesh = new CylinderMesh
                    {
                        TopRadius = rng.RandfRange(0.012f, 0.022f),
                        BottomRadius = rng.RandfRange(0.018f, 0.03f),
                        Height = length,
                        RadialSegments = 5,
                        Rings = 1,
                    },
                    MaterialOverride = rng.Randf() < 0.25f ? pale : bark,
                };
                AddChild(stick);
                // Lying round the rim, but each skewed in or out, and tipped a little, as a bird jams them in.
                var at = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (ring - 0.15f);
                stick.Position = at with { Y = height };
                stick.Rotation = new Vector3(Mathf.Pi / 2f + rng.RandfRange(-0.25f, 0.25f), -angle + rng.RandfRange(-0.5f, 0.5f), 0f);
            }
        }

        // The heap itself, filled in beneath the sticks, the eggs lying on bare sticks on top.
        var heap = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = Width * 0.4f, BottomRadius = Width * 0.35f, Height = Depth, RadialSegments = 16 },
            MaterialOverride = bark,
            Position = new Vector3(0f, Depth / 2f, 0f),
        };
        AddChild(heap);
    }

    /// <summary>Where the eggs lie, in the cup on top of the heap.</summary>
    public Vector3 Cup => GlobalPosition + Vector3.Up * CupHeight;

    /// <summary>How far out from its middle the top of the heap reaches, where a bird can stand on it.</summary>
    public float Radius => Width * 0.4f * Size;

    /// <summary>How far above the foot of the nest the eggs lie.</summary>
    public float CupHeight => (Depth + 0.05f) * Size;

    /// <summary>One egg hatches, and its shell is gone from the nest.</summary>
    public void Hatch() => RemoveEgg();

    /// <summary>One egg is eaten by a raider, and is gone from the nest.</summary>
    public void Lose() => RemoveEgg();

    private void RemoveEgg()
    {
        if (_eggs.Count == 0)
            return;
        _eggs[^1].QueueFree();
        _eggs.RemoveAt(_eggs.Count - 1);
    }
}
