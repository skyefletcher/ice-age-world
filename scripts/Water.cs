using System.Collections.Generic;
using Godot;

namespace IceAgeWorld;

/// <summary>Draws the water surface of each lake and answers "is there water here?" for drinking and swimming.</summary>
public partial class Water : Node3D
{
    private IReadOnlyList<Lake> _lakes = [];

    public void Build(Terrain terrain)
    {
        _lakes = terrain.Lakes;

        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.2f, 0.4f, 0.5f, 0.78f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 0.08f,
            Metallic = 0.2f,
        };

        foreach (var lake in _lakes)
        {
            // The disc reaches part-way up the bank, so its edge is always hidden under the shore.
            float radius = lake.Radius + lake.Shore * 0.6f;
            AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh
                {
                    TopRadius = radius,
                    BottomRadius = radius,
                    Height = 0.02f,
                    RadialSegments = 64,
                    Material = material,
                },
                Position = new Vector3(lake.Centre.X, lake.Surface - 0.01f, lake.Centre.Y),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
        }
    }

    /// <summary>Height of the water surface above a point, or null if the point isn't over a lake.</summary>
    public float? SurfaceAt(Vector3 point)
    {
        var flat = new Vector2(point.X, point.Z);
        foreach (var lake in _lakes)
        {
            if (flat.DistanceTo(lake.Centre) < lake.Radius)
                return lake.Surface;
        }

        return null;
    }
}
