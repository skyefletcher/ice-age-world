using Godot;

namespace IceAgeWorld;

/// <summary>
/// Thin wrapper over SurfaceTool for building indexed, vertex-coloured meshes in code.
/// Every vertex carries a position, normal, colour and UV so one vertex format serves all callers.
/// </summary>
public sealed class MeshBuilder
{
    private readonly SurfaceTool _surface = new();
    private int _count;

    public MeshBuilder() => _surface.Begin(Mesh.PrimitiveType.Triangles);

    /// <summary>Adds a vertex and returns its index.</summary>
    public int Add(Vector3 position, Vector3 normal, Color colour, Vector2 uv = default)
    {
        _surface.SetNormal(normal);
        _surface.SetColor(colour);
        _surface.SetUV(uv);
        _surface.AddVertex(position);
        return _count++;
    }

    public void Tri(int a, int b, int c)
    {
        _surface.AddIndex(a);
        _surface.AddIndex(b);
        _surface.AddIndex(c);
    }

    public void Quad(int a, int b, int c, int d)
    {
        Tri(a, b, c);
        Tri(a, c, d);
    }

    public ArrayMesh Commit(Material material)
    {
        _surface.SetMaterial(material);
        return _surface.Commit();
    }
}
