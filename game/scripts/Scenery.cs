using System;
using System.Collections.Generic;
using Godot;
using TideToll.Sim;

namespace TideToll.Game;

/// <summary>
/// Decoration that the simulation does not know about, placed from the seed so the same seed always gives the
/// same picture: trees in copses (round the map edge, a few small ones inside, willows near the water), reed clumps on
/// the river banks, a moored rowboat, and stepping stones on the ford. One MultiMesh per model, so a hundred trees
/// cost three draw calls. Rules: nothing sits on a door or ford cell (the ford stones are the ford itself), trees
/// keep clear of buildings, the water and the likely paths (straight lines from every house to the well, the field
/// and, via the ford, the market), and reeds and the boat step aside where the player builds a footbridge.
/// </summary>
public partial class Scenery : Node3D
{
    private static readonly string[] TreeModels = { "tree-oak", "tree-poplar", "tree-willow" };
    private SimDriver _sim = null!;
    private readonly MultiMeshInstance3D[] _trees = new MultiMeshInstance3D[3];
    private MultiMeshInstance3D _reeds = null!, _stones = null!;
    private MeshInstance3D _boat = null!;
    private readonly List<(Transform3D xf, int row)> _reedList = new();
    private int _boatRow;

    /// <summary>Trees placed for the current map (tests check that none sits on a door or ford).</summary>
    public readonly List<(int cell, int kind)> TreeCells = new();

    public void Init(SimDriver sim)
    {
        _sim = sim;
        Name = "Scenery";
        for (int i = 0; i < 3; i++)
        {
            _trees[i] = ModelLib.Multi(TreeModels[i], "Trees" + i, 160);
            _trees[i].Multimesh.Mesh = TreeMesh(TreeModels[i]);
            AddChild(_trees[i]);
        }
        _reeds = ModelLib.Multi("reeds", "Reeds", 256); AddChild(_reeds);
        _stones = ModelLib.Multi("ford-stones", "FordStones", 64); AddChild(_stones);
        _stones.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        _boat = ModelLib.Instance("rowboat", "Rowboat"); AddChild(_boat);
        sim.WorldReplaced += Rebuild;
        sim.Edited += OnEdited;
        sim.HouseBuilt += OnHouseBuilt;
        Rebuild();
    }

    private List<Transform3D>[] _treeLists = { new(), new(), new() };
    /// <summary>Trees taken down for cottages the town grew (tests).</summary>
    public int Felled { get; private set; }

    /// <summary>
    /// A cottage grew where trees stand (the simulation does not know the scenery): the trees on its plot and the cell
    /// round it are taken down, so no house stands in a copse. Once per new house.
    /// </summary>
    private void OnHouseBuilt(int index)
    {
        var b = _sim.World.Buildings[index];
        float cell = (float)_sim.World.Params.CellMeters;
        float x0 = (b.X - 1.2f) * cell, x1 = (b.X + b.W + 1.2f) * cell, z0 = (b.Y - 1.2f) * cell, z1 = (b.Y + b.H + 2.2f) * cell;
        for (int k = 0; k < 3; k++)
        {
            var list = _treeLists[k];
            int before = list.Count;
            list.RemoveAll(t => t.Origin.X > x0 && t.Origin.X < x1 && t.Origin.Z > z0 && t.Origin.Z < z1);
            if (list.Count == before) continue;
            Felled += before - list.Count;
            Fill(_trees[k], list);
        }
        int n = _sim.World.Grid.Width;
        TreeCells.RemoveAll(t => t.cell % n >= b.X - 1 && t.cell % n < b.X + b.W + 1 && t.cell / n >= b.Y - 1 && t.cell / n < b.Y + b.H + 1);
    }

    // Crown centre and the height the crown starts, per model (assets/src/models/tree.py), for the round shading.
    private static readonly Dictionary<string, (Vector3 centre, float baseY)> Crowns = new()
    {
        ["tree-oak"] = (new Vector3(0, 4.8f, 0), 3.2f),
        ["tree-poplar"] = (new Vector3(0, 5.4f, 0), 2.2f),
        ["tree-willow"] = (new Vector3(0, 3.6f, 0), 1.4f),
    };

    /// <summary>A private copy of a tree model with the round-clump tree shader (shaders/tree.gdshader).</summary>
    private static ArrayMesh TreeMesh(string model)
    {
        var src = ModelLib.Mesh(model);
        var mesh = (ArrayMesh)src.Duplicate();
        var mat = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/tree.gdshader") };
        mat.SetShaderParameter("atlas", ((StandardMaterial3D)src.SurfaceGetMaterial(0)).AlbedoTexture);
        var (centre, baseY) = Crowns[model];
        mat.SetShaderParameter("crown_centre", centre);
        mat.SetShaderParameter("crown_base", baseY);
        mesh.SurfaceSetMaterial(0, mat);
        return mesh;
    }

    public override void _ExitTree()
    {
        if (_sim == null) return;
        _sim.WorldReplaced -= Rebuild;
        _sim.Edited -= OnEdited;
        _sim.HouseBuilt -= OnHouseBuilt;
    }

    public void Rebuild()
    {
        var w = _sim.World;
        var rng = new Mulberry32(unchecked((uint)(w.Seed * 2654435761u + 977u)));
        PlaceTrees(w, ref rng);
        PlaceFord(w, ref rng);
        PlaceReedsAndBoat(w, ref rng);
        OnEdited(w.Version);
    }

    // ------------------------------------------------------------------ trees

    /// <summary>Tree crowns are drawn a little wider and flatter than the models (clumps from above, not cones).</summary>
    public static readonly Vector3 CrownScale = new(1.18f, 0.92f, 1.18f);

    /// <summary>
    /// Copses, not an even scatter: clusters of 5 to 12 trees spaced round the map's border (a wooded edge to the plan),
    /// a few small copses of 3 to 5 inside, and a handful of lone trees; all from the seed, all clear of buildings,
    /// doors, water, the ford and the likely paths (<see cref="TreeClearance"/>).
    /// </summary>
    private void PlaceTrees(World w, ref Mulberry32 rng)
    {
        int n = w.Grid.Width;
        var clear = TreeClearance(w);
        var lists = new List<Transform3D>[3] { new(), new(), new() };
        TreeCells.Clear();
        Copses = 0;
        // Border copses: centres every 18 to 30 cells round the perimeter, 3 to 9 cells in from the edge.
        int perimeter = 4 * (n - 1);
        for (double t = rng.NextDouble() * 12; t < perimeter; t += 18 + rng.NextDouble() * 12)
        {
            int along = (int)t, side = along / (n - 1), off = along % (n - 1);
            int inset = 3 + (int)(rng.NextDouble() * 7);
            (int x, int y) c = side switch
            {
                0 => (off, inset),
                1 => (n - 1 - inset, off),
                2 => (n - 1 - off, n - 1 - inset),
                _ => (inset, n - 1 - off),
            };
            Copse(w, c.x, c.y, 5 + (int)(rng.NextDouble() * 8), clear, lists, ref rng);
        }
        // A few small copses inside, where the clearance allows (tried at random spots).
        for (int k = 0, made = 0; k < 60 && made < 4; k++)
        {
            int x = 16 + (int)(rng.NextDouble() * (n - 32)), y = 16 + (int)(rng.NextDouble() * (n - 32));
            if (clear[y * n + x] != 0) continue;
            int before = TreeCells.Count;
            Copse(w, x, y, 3 + (int)(rng.NextDouble() * 3), clear, lists, ref rng);
            if (TreeCells.Count > before) made++;
        }
        // And a few lone trees in the open.
        for (int k = 0, made = 0; k < 80 && made < 5; k++)
        {
            int x = 8 + (int)(rng.NextDouble() * (n - 16)), y = 8 + (int)(rng.NextDouble() * (n - 16));
            if (TryTree(w, x, y, clear, lists, ref rng)) made++;
        }
        for (int i = 0; i < 3; i++) Fill(_trees[i], lists[i]);
        _treeLists = lists;
    }

    /// <summary>Copses placed for the current map (tests and stats).</summary>
    public int Copses { get; private set; }

    // Trees of one copse: scattered round a centre, densest in the middle, never on top of each other.
    private void Copse(World w, int cx, int cy, int count, byte[] clear, List<Transform3D>[] lists, ref Mulberry32 rng)
    {
        float r = 2.2f + count * 0.45f;
        int placed = 0;
        for (int k = 0; k < count * 5 && placed < count; k++)
        {
            double a = rng.NextDouble() * Math.Tau, d = r * Math.Sqrt(rng.NextDouble()) * (0.55 + 0.45 * rng.NextDouble());
            if (TryTree(w, cx + (int)Math.Round(Math.Cos(a) * d), cy + (int)Math.Round(Math.Sin(a) * d), clear, lists, ref rng)) placed++;
        }
        if (placed > 0) Copses++;
    }

    private bool TryTree(World w, int x, int y, byte[] clear, List<Transform3D>[] lists, ref Mulberry32 rng)
    {
        int n = w.Grid.Width;
        if (x < 1 || y < 1 || x >= n - 1 || y >= n - 1 || clear[y * n + x] != 0) return false;
        foreach (var (c, _) in TreeCells)
        {
            int dx = c % n - x, dy = c / n - y;
            if (dx * dx + dy * dy < 5) return false; // crowns overlap into a clump but never stack
        }
        float riverDist = MathF.Abs(x + 0.5f - w.RiverX[y]) - (float)w.Map.RiverHalfW;
        int kind = riverDist < 7 ? 2 : rng.NextDouble() < 0.68 ? 0 : 1;
        float cell = (float)w.Params.CellMeters;
        float jx = (float)rng.NextDouble() - 0.5f, jz = (float)rng.NextDouble() - 0.5f;
        float s = 0.85f + 0.3f * (float)rng.NextDouble();
        var basis = new Basis(Vector3.Up, (float)(rng.NextDouble() * Math.Tau)).Scaled(CrownScale * s);
        lists[kind].Add(new Transform3D(basis, new Vector3((x + 0.5f + jx) * cell, 0, (y + 0.5f + jz) * cell)));
        TreeCells.Add((y * n + x, kind));
        return true;
    }

    /// <summary>1 where no tree may stand: water and banks, buildings and their margin, doors, the ford, likely paths.</summary>
    public static byte[] TreeClearance(World w)
    {
        int n = w.Grid.Width;
        var m = new byte[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int c = y * n + x;
                if (MathF.Abs(x + 0.5f - w.RiverX[y]) < w.Map.RiverHalfW + 2.5 || w.Ford[c] != 0) m[c] = 1;
            }
        foreach (var b in w.Buildings)
        {
            Mark(m, n, b.X - 3, b.Y - 3, b.X + b.W + 3, b.Y + b.H + 3);
            Mark(m, n, b.Door % n - 4, b.Door / n - 4, b.Door % n + 5, b.Door / n + 5);
        }
        // Likely paths: house door to each destination's door, by the ford when the river lies between them.
        int fordCell = w.Map.FordY * n + (int)w.RiverX[w.Map.FordY];
        foreach (int h in w.Houses)
            foreach (var d in w.Buildings)
            {
                if (d.Kind == BuildingKind.House) continue;
                int a = w.Buildings[h].Door, b = d.Door;
                if (w.Map.Bank[a] != w.Map.Bank[b] && w.Map.Bank[a] != 0 && w.Map.Bank[b] != 0)
                {
                    Corridor(m, n, a, fordCell, 6);
                    Corridor(m, n, fordCell, b, 6);
                }
                else Corridor(m, n, a, b, 6);
            }
        return m;
    }

    private static void Mark(byte[] m, int n, int x0, int y0, int x1, int y1)
    {
        for (int y = Math.Max(0, y0); y < Math.Min(n, y1); y++)
            for (int x = Math.Max(0, x0); x < Math.Min(n, x1); x++) m[y * n + x] = 1;
    }

    private static void Corridor(byte[] m, int n, int a, int b, float r)
    {
        float ax = a % n, ay = a / n, bx = b % n, by = b / n;
        float vx = bx - ax, vy = by - ay, len2 = Math.Max(1e-6f, vx * vx + vy * vy);
        int x0 = (int)(Math.Min(ax, bx) - r), x1 = (int)(Math.Max(ax, bx) + r), y0 = (int)(Math.Min(ay, by) - r), y1 = (int)(Math.Max(ay, by) + r);
        for (int y = Math.Max(0, y0); y <= Math.Min(n - 1, y1); y++)
            for (int x = Math.Max(0, x0); x <= Math.Min(n - 1, x1); x++)
            {
                float t = Math.Clamp(((x - ax) * vx + (y - ay) * vy) / len2, 0f, 1f);
                float dx = x - (ax + vx * t), dy = y - (ay + vy * t);
                if (dx * dx + dy * dy <= r * r) m[y * n + x] = 1;
            }
    }

    // ------------------------------------------------------------------ water's edge

    private void PlaceFord(World w, ref Mulberry32 rng)
    {
        int n = w.Grid.Width;
        float cell = (float)w.Params.CellMeters;
        var list = new List<Transform3D>();
        for (int c = 0; c < n * n; c++)
        {
            if (w.Ford[c] == 0) continue;
            int x = c % n, y = c / n;
            // Stepping stones run across the river (along X); the outer rows are patchier than the middle one.
            if (y != w.Map.FordY && rng.NextDouble() < 0.45) continue;
            float jz = ((float)rng.NextDouble() - 0.5f) * 0.6f;
            var basis = new Basis(Vector3.Up, MathF.PI / 2f + ((float)rng.NextDouble() - 0.5f) * 0.4f);
            list.Add(new Transform3D(basis, new Vector3((x + 0.5f) * cell, -0.12f, (y + 0.5f + jz) * cell)));
        }
        Fill(_stones, list);
    }

    private void PlaceReedsAndBoat(World w, ref Mulberry32 rng)
    {
        int n = w.Grid.Width;
        float cell = (float)w.Params.CellMeters, half = (float)w.Map.RiverHalfW;
        _reedList.Clear();
        for (int y = 1; y < n - 1; y++)
        {
            if (Math.Abs(y - w.Map.FordY) <= 4) continue; // the ford's approaches stay open
            for (int side = -1; side <= 1; side += 2)
            {
                if (rng.NextDouble() > 0.13) continue;
                float x = w.RiverX[y] + side * (half + 0.35f);
                int cx = (int)MathF.Floor(x);
                if (cx < 0 || cx >= n || IsDoor(w, y * n + cx)) continue;
                var basis = new Basis(Vector3.Up, (float)(rng.NextDouble() * Math.Tau)).Scaled(Vector3.One * (0.8f + 0.4f * (float)rng.NextDouble()));
                _reedList.Add((new Transform3D(basis, new Vector3(x * cell, 0, (y + (float)rng.NextDouble()) * cell)), y));
            }
        }
        // The rowboat: moored against one bank, well away from the ford, pointing along the stream.
        int row = 0;
        for (int t = 0; t < 40; t++)
        {
            row = 12 + (int)(rng.NextDouble() * (n - 24));
            if (Math.Abs(row - w.Map.FordY) > 14) break;
        }
        _boatRow = row;
        int bside = rng.NextDouble() < 0.5 ? -1 : 1;
        float bx = (w.RiverX[row] + bside * Math.Max(0.2f, half - 0.75f)) * cell;
        float tx = (w.RiverX[Math.Min(n - 1, row + 1)] - w.RiverX[Math.Max(0, row - 1)]) / 2f;
        _boat.Position = new Vector3(bx, 0.05f, (row + 0.5f) * cell);
        _boat.Rotation = new Vector3(0, MathF.Atan2(tx, 1f) + (rng.NextDouble() < 0.5 ? 0 : MathF.PI), 0);
    }

    private static bool IsDoor(World w, int c)
    {
        foreach (var b in w.Buildings) if (b.Door == c) return true;
        return false;
    }

    /// <summary>Reeds and the boat give way to footbridges: none within a row or so of a bridge.</summary>
    private void OnEdited(int version)
    {
        var w = _sim.World;
        var list = new List<Transform3D>(_reedList.Count);
        foreach (var (xf, row) in _reedList)
            if (!NearBridge(w, row, 1)) list.Add(xf);
        Fill(_reeds, list);
        // Never shown now (25 Nov 2026, the lead's chapters): Chapter I has no boats, so a moored rowboat there reads as a
        // boat that does nothing, and in Chapter II real boats sail the river and the decoration would read as one of
        // them. The node stays (placed from the seed) for a later chapter that wants it.
        _boat.Visible = false;
    }

    private static bool NearBridge(World w, int row, int rows)
    {
        foreach (var b in w.Bridges) if (Math.Abs(b.Row - row) <= rows) return true;
        return false;
    }

    /// <summary>Instances dropped because a MultiMesh could not grow (never expected; tests check it stays 0).</summary>
    public int Dropped { get; private set; }

    private void Fill(MultiMeshInstance3D mmi, List<Transform3D> xfs)
    {
        var mm = mmi.Multimesh;
        if (xfs.Count > mm.InstanceCount)
        {
            // Grow rather than drop: a seed with more trees or reeds than the first guess still shows all of them.
            // Setting InstanceCount clears the buffer, which is refilled just below. Rare (a new map), never per frame.
            GD.Print($"Scenery: {mmi.Name} grows from {mm.InstanceCount} to {xfs.Count + 32} instances");
            mm.VisibleInstanceCount = -1;
            mm.InstanceCount = xfs.Count + 32;
        }
        int n = Math.Min(xfs.Count, mm.InstanceCount);
        Dropped += xfs.Count - n;
        for (int i = 0; i < n; i++) mm.SetInstanceTransform(i, xfs[i]);
        mm.VisibleInstanceCount = n;
    }
}
