using System;
using System.Collections.Generic;

namespace TideToll.Sim;

/// <summary>What the Gate tool would do at a point: the cells, whether it removes a gate, or why it cannot (the toy's GateTarget).</summary>
public readonly struct GateTarget
{
    /// <summary>The gate's cells (one, or a stair corner's two); empty when no gate can go here.</summary>
    public int[] Cells { get; init; }
    /// <summary>The cells are an existing gate (the Gate tool takes it out).</summary>
    public bool Remove { get; init; }
    /// <summary>Why not, when Cells is empty (one of <see cref="FenceGates"/>' reasons).</summary>
    public string Reason { get; init; }
}

/// <summary>
/// Where a gate can go in a fence (the toy's gates.ts). A fence drawn at an angle is a 4-connected staircase, so most of
/// its cells have fence on two adjacent sides and open ground only on the other two or across a corner walkers may not
/// cut. The rule asks the real question instead: does opening these cells join the open ground on one side of the fence
/// to the open ground on the other, under the walkers' own step rule (8 neighbours, no cutting a blocked corner)? It looks
/// only at the cells round the gate. A single cell is tried first; otherwise the cell and one fence neighbour (a stair's
/// corner pair) become one gate of two cells, each with half the delay. Tools only: these allocate.
/// </summary>
public static class FenceGates
{
    public const string NoFence = "Gates go on fences: there is no fence here.";
    public const string LonePost = "A gate needs fence beside it: this is a lone post.";
    public const string NoWay = "No way through here: both sides are blocked.";

    /// <summary>Whether opening the cells <paramref name="g"/> joins two separate pieces of open ground next to them.</summary>
    internal static bool GatePassage(World w, int[] g)
    {
        int n = w.Grid.Width;
        int x0 = n, y0 = n, x1 = -1, y1 = -1;
        foreach (int c in g)
        {
            int x = c % n, y = c / n;
            x0 = Math.Min(x0, x - 1); y0 = Math.Min(y0, y - 1); x1 = Math.Max(x1, x + 1); y1 = Math.Max(y1, y + 1);
        }
        bool Inside(int x, int y) => x >= 0 && y >= 0 && x < n && y < n;
        bool InG(int c) => Array.IndexOf(g, c) >= 0;
        bool InWin(int x, int y) => x >= x0 && x <= x1 && y >= y0 && y <= y1 && Inside(x, y);
        // Open as the walkers see it, with or without the gate cells opened.
        bool Open(int x, int y, bool withG) => Inside(x, y) && (InG(y * n + x) ? withG : !w.IsBlocked(y * n + x));
        bool Step(int ux, int uy, int k, bool withG)
        {
            int vx = ux + Grid.DX[k], vy = uy + Grid.DY[k];
            if (!Open(vx, vy, withG)) return false;
            return k < 4 || (Open(vx, uy, withG) && Open(ux, vy, withG));
        }
        // Label the open ground in the window, gate cells shut.
        int W = x1 - x0 + 1;
        var label = new sbyte[W * (y1 - y0 + 1)];
        Array.Fill(label, (sbyte)-1);
        int At(int x, int y) => (y - y0) * W + (x - x0);
        sbyte next = 0;
        var stack = new Stack<(int, int)>();
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                if (!InWin(x, y) || !Open(x, y, false) || label[At(x, y)] >= 0) continue;
                stack.Push((x, y));
                label[At(x, y)] = next;
                while (stack.Count > 0)
                {
                    var (ux, uy) = stack.Pop();
                    for (int k = 0; k < 8; k++)
                    {
                        int vx = ux + Grid.DX[k], vy = uy + Grid.DY[k];
                        if (!InWin(vx, vy) || label[At(vx, vy)] >= 0 || !Step(ux, uy, k, false)) continue;
                        label[At(vx, vy)] = next;
                        stack.Push((vx, vy));
                    }
                }
                next++;
            }
        // The gate cells form one opening (a pair is 4-adjacent); which pieces does it reach?
        int seen = -1;
        foreach (int c in g)
        {
            int ux = c % n, uy = c / n;
            for (int k = 0; k < 8; k++)
            {
                int vx = ux + Grid.DX[k], vy = uy + Grid.DY[k];
                if (!InWin(vx, vy) || InG(vy * n + vx) || !Step(ux, uy, k, true)) continue;
                int l = label[At(vx, vy)];
                if (l < 0) continue;
                if (seen < 0) seen = l;
                else if (l != seen) return true;
            }
        }
        return false;
    }

    /// <summary>Whether any of the 8 neighbours of the cells <paramref name="g"/> (not in g) is fence.</summary>
    private static bool HasRail(World w, int[] g)
    {
        int n = w.Grid.Width;
        var fence = w.Fence;
        foreach (int c in g)
        {
            int x = c % n, y = c / n;
            for (int k = 0; k < 8; k++)
            {
                int vx = x + Grid.DX[k], vy = y + Grid.DY[k];
                if (vx >= 0 && vy >= 0 && vx < n && vy < n && fence[vy * n + vx] != 0 && Array.IndexOf(g, vy * n + vx) < 0) return true;
            }
        }
        return false;
    }

    /// <summary>The gate an existing gate cell belongs to: it and any 4-adjacent gate cell.</summary>
    public static int[] GateGroup(World w, int c)
    {
        int n = w.Grid.Width, x = c % n, y = c / n;
        var gate = w.Gate;
        var o = new List<int> { c };
        for (int k = 0; k < 4; k++)
        {
            int vx = x + Grid.DX[k], vy = y + Grid.DY[k];
            if (vx >= 0 && vy >= 0 && vx < n && vy < n && gate[vy * n + vx] != 0) o.Add(vy * n + vx);
        }
        return o.ToArray();
    }

    /// <summary>
    /// The cells a gate at fence cell c would take: [c] if a gate there opens a way through, else [c, n] for the first
    /// fence neighbour n that makes a two-cell gate with it, else null. Lone posts never take a gate.
    /// </summary>
    public static int[]? GateCellsFor(World w, int c)
    {
        int n = w.Grid.Width;
        if (c < 0 || c >= w.Grid.Count) return null;
        var fence = w.Fence; var gate = w.Gate;
        if (fence[c] == 0 || gate[c] != 0) return null;
        if (!HasRail(w, new[] { c })) return null;
        if (GatePassage(w, new[] { c })) return new[] { c };
        int x = c % n, y = c / n;
        for (int k = 0; k < 4; k++)
        {
            int vx = x + Grid.DX[k], vy = y + Grid.DY[k];
            if (vx < 0 || vy < 0 || vx >= n || vy >= n) continue;
            int nb = vy * n + vx;
            if (fence[nb] == 0 || gate[nb] != 0) continue;
            var pair = new[] { c, nb };
            if (HasRail(w, pair) && GatePassage(w, pair)) return pair;
        }
        return null;
    }

    /// <summary>
    /// What a Gate click at (x, y) (cell units) does: the nearest fence cell within 2 cells (by distance from the point to
    /// the cell centre, ties by cell index) that is a gate (to remove) or can take one (to place); otherwise why not.
    /// </summary>
    public static GateTarget Target(World w, double x, double y)
    {
        int n = w.Grid.Width;
        int cx = (int)Math.Floor(x), cy = (int)Math.Floor(y);
        var near = new List<(int C, double D)>();
        var fence = w.Fence;
        for (int dy = -2; dy <= 2; dy++)
            for (int dx = -2; dx <= 2; dx++)
            {
                int ax = cx + dx, ay = cy + dy;
                if (ax < 0 || ay < 0 || ax >= n || ay >= n) continue;
                int c = ay * n + ax;
                double ex = ax + 0.5 - x, ey = ay + 0.5 - y;
                if (fence[c] != 0) near.Add((c, ex * ex + ey * ey));
            }
        if (near.Count == 0) return new GateTarget { Cells = Array.Empty<int>(), Remove = false, Reason = NoFence };
        near.Sort((a, b) => a.D != b.D ? a.D.CompareTo(b.D) : a.C.CompareTo(b.C)); // a total order: any sort gives the toy's
        bool rail = false;
        var gate = w.Gate;
        foreach (var (c, _) in near)
        {
            if (gate[c] != 0) return new GateTarget { Cells = GateGroup(w, c), Remove = true, Reason = "" };
            var cells = GateCellsFor(w, c);
            if (cells != null) return new GateTarget { Cells = cells, Remove = false, Reason = "" };
            if (HasRail(w, new[] { c })) rail = true;
        }
        return new GateTarget { Cells = Array.Empty<int>(), Remove = false, Reason = rail ? NoWay : LonePost };
    }
}
