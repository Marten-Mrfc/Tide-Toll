using System;
using System.Collections.Generic;
using Godot;
using TideToll.Sim;

namespace TideToll.Game;

/// <summary>
/// Decoration that the simulation does not know about, placed from the seed so the same seed always gives the
/// same picture: trees (mostly along the map edge, a few copses inside, willows near the water), reed clumps on
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
        for (int i = 0; i < 3; i++) { _trees[i] = ModelLib.Multi(TreeModels[i], "Trees" + i, 160); AddChild(_trees[i]); }
        _reeds = ModelLib.Multi("reeds", "Reeds", 256); AddChild(_reeds);
        _stones = ModelLib.Multi("ford-stones", "FordStones", 64); AddChild(_stones);
        _stones.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        _boat = ModelLib.Instance("rowboat", "Rowboat"); AddChild(_boat);
        sim.WorldReplaced += Rebuild;
        sim.Edited += OnEdited;
        Rebuild();
    }

    public override void _ExitTree()
    {
        if (_sim == null) return;
        _sim.WorldReplaced -= Rebuild;
        _sim.Edited -= OnEdited;
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

    private void PlaceTrees(World w, ref Mulberry32 rng)
    {
        int n = w.Grid.Width;
        var clear = TreeClearance(w);
        var lists = new List<Transform3D>[3] { new(), new(), new() };
        TreeCells.Clear();
        for (int attempt = 0; attempt < 1400 && TreeCells.Count < 150; attempt++)
        {
            int x = 1 + (int)(rng.NextDouble() * (n - 2)), y = 1 + (int)(rng.NextDouble() * (n - 2));
            int edge = Math.Min(Math.Min(x, y), Math.Min(n - 1 - x, n - 1 - y));
            // Dense along the edge of the plan, sparse inside: the map reads as a clearing with a wooded border.
            double keep = edge < 6 ? 0.85 : edge < 14 ? 0.35 : 0.05;
            if (rng.NextDouble() > keep) continue;
            TryTree(w, x, y, clear, lists, ref rng);
            // Sometimes a copse: two or three more close by.
            if (edge >= 6 && rng.NextDouble() < 0.6)
                for (int k = 0; k < 3; k++)
                    TryTree(w, x + (int)(rng.NextDouble() * 7) - 3, y + (int)(rng.NextDouble() * 7) - 3, clear, lists, ref rng);
        }
        for (int i = 0; i < 3; i++) Fill(_trees[i], lists[i]);
    }

    private void TryTree(World w, int x, int y, byte[] clear, List<Transform3D>[] lists, ref Mulberry32 rng)
    {
        int n = w.Grid.Width;
        if (x < 1 || y < 1 || x >= n - 1 || y >= n - 1 || clear[y * n + x] != 0) return;
        foreach (var (c, _) in TreeCells)
        {
            int dx = c % n - x, dy = c / n - y;
            if (dx * dx + dy * dy < 7) return; // canopies may touch but not stack
        }
        float riverDist = MathF.Abs(x + 0.5f - w.RiverX[y]) - (float)w.Map.RiverHalfW;
        int kind = riverDist < 9 ? 2 : rng.NextDouble() < 0.68 ? 0 : 1;
        float cell = (float)w.Params.CellMeters;
        float jx = (float)rng.NextDouble() - 0.5f, jz = (float)rng.NextDouble() - 0.5f;
        float s = 0.85f + 0.3f * (float)rng.NextDouble();
        var basis = new Basis(Vector3.Up, (float)(rng.NextDouble() * Math.Tau)).Scaled(new Vector3(s, s, s));
        lists[kind].Add(new Transform3D(basis, new Vector3((x + 0.5f + jx) * cell, 0, (y + 0.5f + jz) * cell)));
        TreeCells.Add((y * n + x, kind));
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
                if (rng.NextDouble() > 0.22) continue;
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
        _boat.Visible = !NearBridge(w, _boatRow, 3);
    }

    private static bool NearBridge(World w, int row, int rows)
    {
        foreach (var b in w.Bridges) if (Math.Abs(b.Row - row) <= rows) return true;
        return false;
    }

    private static void Fill(MultiMeshInstance3D mmi, List<Transform3D> xfs)
    {
        var mm = mmi.Multimesh;
        int n = Math.Min(xfs.Count, mm.InstanceCount);
        for (int i = 0; i < n; i++) mm.SetInstanceTransform(i, xfs[i]);
        mm.VisibleInstanceCount = n;
    }
}
