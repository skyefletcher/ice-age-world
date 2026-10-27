using System.Collections.Generic;
using Godot;

namespace IceAgeWorld;

/// <summary>
/// A bald eagle's nest with its clutch of eggs, built in code. Real ones are the biggest nests of any North American
/// bird: a broad, flat-topped heap of sticks a couple of metres across, crossed every which way, with a shallow cup in
/// the middle, and the eggs (dull, chalky white and about the size of a goose's) lying in it. The eggs go one by one
/// as they hatch.
/// </summary>
public partial class Nest : Node3D
{
    /// <summary>How big it is against a real one, to suit the eagles that build it.</summary>
    public float Size { get; init; } = 1f;

    /// <summary>How many eggs are laid in it.</summary>
    public int Clutch { get; init; } = 5;

    /// <summary>How many eggs are still unhatched.</summary>
    public int Eggs => _eggs.Count;

    /// <summary>The tree it's built in, if it's up one rather than on the ground: only a climber can raid it there.</summary>
    public Tree? Tree { get; init; }

    /// <summary>Across the stick heap, and how deep it is.</summary>
    private const float Width = 1.7f;
    private const float Depth = 0.45f;

    private readonly List<Node3D> _eggs = [];

    public override void _Ready()
    {
        Scale = Vector3.One * Size;
        var rng = new RandomNumberGenerator { Seed = (ulong)GetInstanceId() };
        var bark = new StandardMaterial3D { AlbedoColor = new Color(0.36f, 0.27f, 0.18f), Roughness = 1f };
        var pale = new StandardMaterial3D { AlbedoColor = new Color(0.55f, 0.47f, 0.34f), Roughness = 1f };

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
        // Solid, so the eagle can stand on it over her eggs.
        var solid = new StaticBody3D { Position = heap.Position };
        solid.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = Width * 0.4f, Height = Depth } });
        AddChild(solid);

        // The eggs, together in the middle of the cup, each lying on its side.
        var shell = new StandardMaterial3D { AlbedoColor = new Color(0.93f, 0.91f, 0.84f), Roughness = 0.9f };
        for (int e = 0; e < Clutch; e++)
        {
            float angle = e * Mathf.Tau / Clutch + 0.3f;
            var egg = new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 0.04f, Height = 0.08f, RadialSegments = 12, Rings = 6 },
                MaterialOverride = shell,
                Position = new Vector3(Mathf.Cos(angle) * 0.11f, Depth + 0.03f, Mathf.Sin(angle) * 0.11f),
                Rotation = new Vector3(Mathf.Pi / 2f, angle, 0f),
                // An eagle's egg is about 7 cm long and 5.5 cm across.
                Scale = new Vector3(0.7f, 0.9f, 0.7f),
            };
            AddChild(egg);
            _eggs.Add(egg);
        }
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
