using System;
using System.Collections.Generic;
using Godot;

namespace IceAgeWorld;

/// <summary>
/// A cave dug into a mountainside: a winding tunnel running in from its mouth, its floor along <see cref="Path"/> at
/// <see cref="Floors"/>, opening out at the far end into a big chamber, as caves in limestone and granite do where water
/// once wore them out. Bears denned in caves like these through the ice-age winters, and cave lions and hyenas after them.
/// </summary>
public sealed record Cave(Vector2[] Path, float[] Floors, float Width, float Height, float ChamberRadius, float ChamberHeight)
{
    /// <summary>Where the tunnel starts, out in front of the mountainside.</summary>
    public Vector2 Mouth => Path[0];

    public Vector2 Chamber => Path[^1];
}

public partial class Terrain
{
    /// <summary>
    /// Half the width and the height of a cave's tunnel, and the radius and height of the chamber at the back: roomy
    /// enough for a woolly mammoth or the giant polar bear to walk in, and for the camera to follow.
    /// </summary>
    private const float CaveWidth = 6f;
    private const float CaveHeight = 9.5f;
    private const float ChamberRadius = 13f;
    private const float ChamberHeight = 13f;

    /// <summary>The thinnest the rock over a cave can be; anywhere thinner, the roof has fallen in and lets in the sky.</summary>
    private const float RoofThickness = 1.5f;

    private static readonly Color CaveRock = new(0.3f, 0.29f, 0.28f);
    private static readonly Color CaveDust = new(0.36f, 0.32f, 0.27f);

    private readonly List<Cave> _caves = [];

    /// <summary>Vertices dug out by a cave.</summary>
    private bool[] _caveMask = [];

    /// <summary>Height of the rock overhead at vertices in and around a cave (NaN elsewhere).</summary>
    private float[] _ceiling = [];

    /// <summary>Cells of the heightmap with a cave roof over them.</summary>
    private bool[] _roofed = [];

    public IReadOnlyList<Cave> Caves => _caves;

    /// <summary>True in a cave, or within <paramref name="margin"/> metres of one, where no tree or grass grows.</summary>
    public bool IsInCave(float x, float z, float margin = 0f)
    {
        var point = new Vector2(x, z);
        foreach (var cave in _caves)
        {
            // Most places are nowhere near a cave: rule them out cheaply before tracing its tunnel.
            float span = (cave.Path.Length - 1) * 3f + cave.ChamberRadius * 1.3f + cave.Width + margin;
            if (point.DistanceSquaredTo(cave.Chamber) < span * span && CaveShape(cave, point).Gap < margin)
                return true;
        }
        return false;
    }

    /// <summary>
    /// The shape of a cave at a point: <c>U</c> is how far out from its middle towards its walls the point lies (0 in the
    /// middle, 1 at the wall), <c>Gap</c> how far outside it is in metres (negative inside), and the floor and ceiling heights
    /// there. The tunnel is a rounded arch and the chamber a dome, their walls made uneven, and where they meet the higher
    /// ceiling wins, so the tunnel opens smoothly into the chamber.
    /// </summary>
    private (float U, float Gap, float Floor, float Ceiling) CaveShape(Cave cave, Vector2 point)
    {
        float wobble = 1f + 0.18f * _outline.GetNoise2D(point.X * 4f, point.Y * 4f);

        float best = float.MaxValue, floor = cave.Floors[0];
        for (int i = 0; i < cave.Path.Length - 1; i++)
        {
            var a = cave.Path[i];
            var along = cave.Path[i + 1] - a;
            float t = Mathf.Clamp((point - a).Dot(along) / along.LengthSquared(), 0f, 1f);
            float d = point.DistanceTo(a + along * t);
            if (d < best)
            {
                best = d;
                floor = Mathf.Lerp(cave.Floors[i], cave.Floors[i + 1], t);
            }
        }
        float width = cave.Width * wobble;
        float tunnelU = best / width;
        float tunnel = floor + cave.Height * Mathf.Sqrt(Mathf.Max(0f, 1f - tunnelU * tunnelU));

        float radius = cave.ChamberRadius * wobble;
        float chamberU = point.DistanceTo(cave.Chamber) / radius;
        float dome = cave.Floors[^1] + cave.ChamberHeight * Mathf.Pow(Mathf.Max(0f, 1f - chamberU * chamberU), 0.6f);

        bool inChamber = chamberU < tunnelU;
        if (inChamber)
            floor = cave.Floors[^1];

        // The ceiling is rough with jutting rock where it is high overhead, coming down to meet the floor at the walls.
        float ceiling = Mathf.Max(tunnel, dome);
        ceiling += _ridges.GetNoise2D(point.X * 3f, point.Y * 3f) * 0.8f * Mathf.Clamp((ceiling - floor) / 3f, 0f, 1f);
        ceiling = Mathf.Max(ceiling, floor + 0.35f);

        return inChamber
            ? (chamberU, (chamberU - 1f) * radius, floor, ceiling)
            : (tunnelU, best - width, floor, ceiling);
    }

    /// <summary>
    /// Digs one cave into the foot of each mountain, works out where its roof is thick enough to stand, and lowers the
    /// ground underfoot to its floor. The top of the land over a roof stays where it was, so the mountainside above is
    /// whole and can be walked on; where the roof is too thin, near the mouth, the cave opens into a rocky cleft.
    /// </summary>
    private void DigCaves()
    {
        int r = Resolution;
        _surface = (float[])_heights.Clone();
        _caveMask = new bool[r * r];
        _ceiling = new float[r * r];
        Array.Fill(_ceiling, float.NaN);
        _roofed = new bool[(r - 1) * (r - 1)];

        var rng = new RandomNumberGenerator { Seed = (ulong)Seed + 6 };
        _caves.Clear();
        foreach (var mountain in _mountains)
            if (PlanCave(mountain, rng) is { } cave)
                _caves.Add(cave);

        foreach (var cave in _caves)
        {
            var (minX, maxX, minZ, maxZ) = CaveBounds(cave);
            for (int z = minZ; z <= maxZ; z++)
            for (int x = minX; x <= maxX; x++)
            {
                var shape = CaveShape(cave, new Vector2(x * CellSize - HalfSize, z * CellSize - HalfSize));
                if (shape.U >= 1.6f)
                    continue;
                int i = z * r + x;
                _ceiling[i] = float.IsNaN(_ceiling[i]) ? shape.Ceiling : Mathf.Max(_ceiling[i], shape.Ceiling);
                if (shape.U < 1f)
                {
                    // The floor dishes a little towards the walls, worn by the water that dug it.
                    _heights[i] = Mathf.Min(_heights[i], shape.Floor + 0.3f * shape.U * shape.U);
                    _caveMask[i] = true;
                }
            }

            for (int z = minZ; z < maxZ; z++)
            for (int x = minX; x < maxX; x++)
                _roofed[z * (r - 1) + x] = CanRoof(x, z);
        }

        // Where there is no roof, the top of the land is the ground itself, down in the cleft.
        for (int z = 0; z < r; z++)
        for (int x = 0; x < r; x++)
        {
            int i = z * r + x;
            if (_caveMask[i] && !TouchesRoof(x, z))
                _surface[i] = _heights[i];
        }
    }

    /// <summary>
    /// Finds a place for a cave at the foot of a mountain: walking in from its foothills to where the mountainside rears up,
    /// the tunnel starts a little out in front and winds in under the mountain. It is kept only if there is thick rock over
    /// all but its mouth, and it stays clear of water and of the edge of the world.
    /// </summary>
    private Cave? PlanCave(Mountain mountain, RandomNumberGenerator rng)
    {
        for (int attempt = 0; attempt < 24; attempt++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            var outward = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float foot = HeightAlong(mountain.Centre + outward * mountain.Radius * 1.1f);
            float face = mountain.Radius * 1.1f;
            while (face > mountain.Radius * 0.3f && HeightAlong(mountain.Centre + outward * face) < foot + 4f)
                face -= 2f;
            if (face <= mountain.Radius * 0.3f)
                continue;

            var at = mountain.Centre + outward * (face + 6f);
            float floor = HeightAlong(at) + 0.2f;
            float length = rng.RandfRange(45f, 70f);
            float phase = rng.Randf() * Mathf.Tau;
            var path = new List<Vector2>();
            var floors = new List<float>();
            for (float s = 0f; s <= length; s += 3f)
            {
                path.Add(at);
                // The floor climbs gently inwards, so meltwater drains out of the mouth.
                floors.Add(floor + s * 0.04f);
                at += (-outward).Rotated(0.5f * Mathf.Sin(s * 0.08f + phase)) * 3f;
            }

            var cave = new Cave([.. path], [.. floors], CaveWidth, CaveHeight, ChamberRadius, ChamberHeight);
            if (IsSound(cave))
                return cave;
        }
        return null;
    }

    private float HeightAlong(Vector2 point) => HeightAt(
        Mathf.RoundToInt((point.X + HalfSize) / CellSize), Mathf.RoundToInt((point.Y + HalfSize) / CellSize));

    /// <summary>
    /// True if a planned cave has thick rock over it past its first few metres, and over its chamber all round, and keeps
    /// away from lakes, the edge of the world and every other cave.
    /// </summary>
    private bool IsSound(Cave cave)
    {
        float limit = HalfSize - MountainWidth * 0.5f - ChamberRadius;
        for (int i = 0; i < cave.Path.Length; i++)
        {
            var point = cave.Path[i];
            if (Mathf.Abs(point.X) > limit || Mathf.Abs(point.Y) > limit || DistanceFromWater(point.X, point.Y) < ChamberRadius + 10f)
                return false;
            if (i * 3f > 15f && HeightAlong(point) < cave.Floors[i] + cave.Height + 3f)
                return false;
        }
        for (int k = 0; k < 8; k++)
        {
            var round = cave.Chamber + Vector2.Right.Rotated(k * Mathf.Tau / 8f) * cave.ChamberRadius * 0.7f;
            if (HeightAlong(round) < cave.Floors[^1] + cave.ChamberHeight + 3f)
                return false;
        }
        foreach (var other in _caves)
        foreach (var a in other.Path)
        foreach (var b in cave.Path)
            if (a.DistanceTo(b) < ChamberRadius * 2.5f)
                return false;
        return true;
    }

    private (int MinX, int MaxX, int MinZ, int MaxZ) CaveBounds(Cave cave)
    {
        var low = cave.Path[0];
        var high = cave.Path[0];
        foreach (var point in cave.Path)
        {
            low = new Vector2(Mathf.Min(low.X, point.X), Mathf.Min(low.Y, point.Y));
            high = new Vector2(Mathf.Max(high.X, point.X), Mathf.Max(high.Y, point.Y));
        }
        float reach = Mathf.Max(cave.Width, cave.ChamberRadius) * 1.3f * 1.6f + 2f * CellSize;
        int Cell(float w) => Mathf.Clamp(Mathf.RoundToInt((w + HalfSize) / CellSize), 0, Resolution - 1);
        return (Cell(low.X - reach), Cell(high.X + reach), Cell(low.Y - reach), Cell(high.Y + reach));
    }

    /// <summary>
    /// A cell gets a roof if it has been dug and the land over every corner stands clear of the cave's ceiling by at least
    /// <see cref="RoofThickness"/> (or, at corners outside the cave, at least clear of it).
    /// </summary>
    private bool CanRoof(int x, int z)
    {
        bool dug = false;
        foreach (int i in Corners(x, z))
        {
            if (float.IsNaN(_ceiling[i]) || _surface[i] < _ceiling[i] + (_caveMask[i] ? RoofThickness : 0.3f))
                return false;
            dug |= _caveMask[i];
        }
        return dug;
    }

    private int[] Corners(int x, int z)
    {
        int i = z * Resolution + x;
        return [i, i + 1, i + Resolution, i + Resolution + 1];
    }

    private bool IsRoofed(int x, int z) =>
        x >= 0 && z >= 0 && x < Resolution - 1 && z < Resolution - 1 && _roofed[z * (Resolution - 1) + x];

    private bool IsDug(int x, int z) =>
        x >= 0 && z >= 0 && x < Resolution - 1 && z < Resolution - 1 && Array.Exists(Corners(x, z), i => _caveMask[i]);

    private bool TouchesRoof(int x, int z) =>
        IsRoofed(x - 1, z - 1) || IsRoofed(x, z - 1) || IsRoofed(x - 1, z) || IsRoofed(x, z);

    /// <summary>Dusty rock underfoot, darker the deeper under the mountain, where no daylight has bleached it.</summary>
    private Color CaveFloor(Vector3 vertex, Vector3 normal)
    {
        int i = Mathf.RoundToInt((vertex.Z + HalfSize) / CellSize) * Resolution + Mathf.RoundToInt((vertex.X + HalfSize) / CellSize);
        float under = Mathf.SmoothStep(0f, 3f, _surface[i] - _heights[i]);
        float grit = _ridges.GetNoise2D(vertex.X * 5f, vertex.Z * 5f) * 0.06f;
        return GroundColour(vertex.X, vertex.Z, vertex.Y, normal).Lerp(CaveDust, under).Lightened(grit);
    }

    /// <summary>
    /// Builds the rock over the caves: the mountainside on top, matching the ground round it vertex for vertex, the rough
    /// ceiling underneath, and the face of rock over each opening where the roof ends. It is all solid, so animals can walk
    /// on top and bump their heads inside, and it casts shadow on both sides, so the cave is dark within. The cave floor
    /// under it is drawn here too, in its own dusty colours (the heightmap's collision is what is walked on).
    /// </summary>
    private void BuildCaveRoofs()
    {
        int r = Resolution;
        var mesh = new SurfaceTool();
        mesh.Begin(Mesh.PrimitiveType.Triangles);
        var faces = new List<Vector3>();

        Vector3 Top(int x, int z) => new(x * CellSize - HalfSize, At(_surface, x, z), z * CellSize - HalfSize);
        Vector3 Floor(int x, int z) => new(x * CellSize - HalfSize, At(_heights, x, z), z * CellSize - HalfSize);
        Vector3 Under(int x, int z) => new(x * CellSize - HalfSize, _ceiling[z * r + x], z * CellSize - HalfSize);

        void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 facing, Func<Vector3, Vector3>? normal, Func<Vector3, Vector3, Color> colour, bool solid = true)
        {
            // Godot's front faces wind clockwise as seen, so wind each triangle to face the way it should.
            var flat = -(b - a).Cross(c - a).Normalized();
            if (flat.Dot(facing) < 0f)
            {
                (b, c) = (c, b);
                flat = -flat;
            }
            foreach (var v in new[] { a, b, c })
            {
                var n = normal?.Invoke(v) ?? flat;
                mesh.SetNormal(n);
                mesh.SetColor(colour(v, n));
                mesh.AddVertex(v);
                if (solid)
                    faces.Add(v);
            }
        }

        Vector3 TopNormal(Vector3 v) => NormalAt(_surface, Mathf.RoundToInt((v.X + HalfSize) / CellSize), Mathf.RoundToInt((v.Z + HalfSize) / CellSize));
        Color TopColour(Vector3 v, Vector3 n) => GroundColour(v.X, v.Z, v.Y, n);
        // The floor is dust and grit, turning to bare rock where it climbs into the walls.
        Vector3 FloorNormal(Vector3 v) => NormalAt(_heights, Mathf.RoundToInt((v.X + HalfSize) / CellSize), Mathf.RoundToInt((v.Z + HalfSize) / CellSize));
        Color FloorColour(Vector3 v, Vector3 n) =>
            CaveDust.Lerp(CaveRock, 1f - Mathf.SmoothStep(0.6f, 0.9f, n.Y)).Lightened(_ridges.GetNoise2D(v.X * 5f, v.Z * 5f) * 0.06f);
        Color RockColour(Vector3 v, Vector3 n) => CaveRock.Lightened(_ridges.GetNoise2D(v.X * 4f, v.Z * 4f) * 0.08f + 0.04f);

        for (int z = 0; z < r - 1; z++)
        for (int x = 0; x < r - 1; x++)
        {
            if (!IsRoofed(x, z))
                continue;

            Triangle(Floor(x, z), Floor(x + 1, z), Floor(x, z + 1), Vector3.Up, FloorNormal, FloorColour, solid: false);
            Triangle(Floor(x + 1, z), Floor(x + 1, z + 1), Floor(x, z + 1), Vector3.Up, FloorNormal, FloorColour, solid: false);
            Triangle(Top(x, z), Top(x + 1, z), Top(x, z + 1), Vector3.Up, TopNormal, TopColour);
            Triangle(Top(x + 1, z), Top(x + 1, z + 1), Top(x, z + 1), Vector3.Up, TopNormal, TopColour);
            Triangle(Under(x, z), Under(x + 1, z), Under(x, z + 1), Vector3.Down, null, RockColour);
            Triangle(Under(x + 1, z), Under(x + 1, z + 1), Under(x, z + 1), Vector3.Down, null, RockColour);

            // A face of rock closes off the roof's edge where it overhangs an open cleft.
            foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                if (IsRoofed(x + dx, z + dz) || !IsDug(x + dx, z + dz))
                    continue;
                var (ax, az, bx, bz) = (dx, dz) switch
                {
                    (1, 0) => (x + 1, z, x + 1, z + 1),
                    (-1, 0) => (x, z, x, z + 1),
                    (0, 1) => (x, z + 1, x + 1, z + 1),
                    _ => (x, z, x + 1, z),
                };
                var outward = new Vector3(dx, 0f, dz);
                Triangle(Top(ax, az), Top(bx, bz), Under(ax, az), outward, null, RockColour);
                Triangle(Top(bx, bz), Under(bx, bz), Under(ax, az), outward, null, RockColour);
            }
        }

        if (faces.Count == 0)
            return;

        mesh.SetMaterial(new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            VertexColorIsSrgb = true,
            Roughness = 0.95f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        });
        AddChild(new MeshInstance3D
        {
            Name = "CaveRoofs",
            Mesh = mesh.Commit(),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.DoubleSided,
        });

        var body = new StaticBody3D { Name = "CaveRoofBodies" };
        body.AddChild(new CollisionShape3D { Shape = new ConcavePolygonShape3D { Data = [.. faces], BackfaceCollision = true } });
        AddChild(body);
    }
}
