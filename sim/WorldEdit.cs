using System;
using System.Collections.Generic;

namespace TideToll.Sim;

// Tools: fence, footbridge and erase, with the edit and urgent-refresh logic (world.ts tools section).
public sealed partial class World
{
    private readonly List<int> _bridgeScratch = new();

    private void Edited()
    {
        _version++;
        GroundChanged();
    }

    /// <summary>
    /// What an edit does to the town apart from bumping Version (the player's edit count): costs rebuilt now, every
    /// field refreshed urgently, walkers on newly blocked cells moved off. The river's flood and ice call this alone.
    /// </summary>
    private void GroundChanged()
    {
        for (int t = 0; t < _urgent.Length; t++) _urgent[t] = true;
        // Rebuild costs now so walkers stop stepping onto new fences this tick.
        RebuildCosts();
        // Walkers standing on a cell that just became blocked are moved off it.
        for (int i = 0; i < _walkerCount; i++)
        {
            ref Walker w = ref _walkers[i];
            if (!w.Visible) continue;
            if (IsBlocked(w.Cell)) Evict(ref w);
            else w.HasAim = false; // re-aim against the new obstacles
        }
    }

    private void Evict(ref Walker w)
    {
        // Nearest free cell within 6 rings, preferring the one closest to the walker's goal.
        int n = _n, cx = w.Cell % n, cy = w.Cell / n;
        float[] f = _fields[w.Target];
        for (int r = 1; r <= 6; r++)
        {
            int best = -1;
            double bv = double.PositiveInfinity;
            for (int y = cy - r; y <= cy + r; y++)
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (Math.Max(Math.Abs(x - cx), Math.Abs(y - cy)) != r || !_g.Inside(x, y)) continue;
                    int c = y * n + x;
                    if (IsBlocked(c)) continue;
                    double v = f[c] < float.PositiveInfinity ? f[c] : 1e12 + r;
                    if (best < 0 || v < bv) { bv = v; best = c; }
                }
            if (best >= 0)
            {
                _here[w.Cell]--; _here[best]++;
                w.Cell = best; w.HasAim = false;
                w.X = w.Px = best % n + 0.5; w.Y = w.Py = best / n + 0.5;
                if (_river != null) RiverPlaced(ref w);
                return;
            }
        }
        SendHome(ref w);
    }

    /// <summary>Whether a fence may stand on cell c: not water, a footprint, a bridge or a door.</summary>
    public bool CanFence(int c)
    {
        if (_map.Water[c] != 0 || _map.Footprint[c] != 0 || _bridgeAt[c] >= 0) return false;
        foreach (var b in _map.Buildings) if (b.Door == c) return false;
        if (_river != null) foreach (var x in _crossings) if (x.WestEnd == c || x.EastEnd == c) return false;
        return true;
    }

    /// <summary>Places fence cells (cell indices; out-of-range ones are ignored). Returns how many were new. A fence on gravel takes the gravel up (the trail beneath stays).</summary>
    public int PlaceFence(ReadOnlySpan<int> cells)
    {
        int n = 0;
        foreach (int c in cells)
            if (c >= 0 && c < _nc && _fence[c] == 0 && CanFence(c)) { _fence[c] = 1; _gravel[c] = 0; n++; }
        if (n > 0) Edited();
        return n;
    }

    /// <summary>
    /// Whether gravel may go on cell c: any land cell that is not fenced. With the river rules, not on a crossing's end, a
    /// ferry stage or the landing (sync plan, Conflict C). Gravel costs nothing, also under the river's money rules.
    /// </summary>
    public bool CanGravel(int c)
    {
        if (c < 0 || c >= _nc || _land[c] != 1 || _fence[c] != 0) return false;
        if (_river != null)
        {
            if (_crossAt[c] >= 0 || c == LandingCellValue) return false;
            foreach (var x in _crossings) if (x.WestEnd == c || x.EastEnd == c) return false;
        }
        return true;
    }

    /// <summary>
    /// Where a gravel brush at cell c lands: on the trail's centreline if a worn trail is within one cell (the busiest-worn
    /// cell of the 3 x 3 round it, when that is trail), otherwise on c itself.
    /// </summary>
    public int GravelSnap(int c)
    {
        int n = _n, x = c % n, y = c / n;
        double thr = _p.ThresholdTrail;
        int best = c;
        double bw = _wear[c];
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (!_g.Inside(x + dx, y + dy)) continue;
                int v = (y + dy) * n + x + dx;
                if (_wear[v] > bw + 2 && CanGravel(v)) { bw = _wear[v]; best = v; }
            }
        return best != c && bw >= thr ? best : c;
    }

    /// <summary>Lays gravel on these cells; returns how many were new.</summary>
    public int PlaceGravel(ReadOnlySpan<int> cells)
    {
        int n = 0;
        foreach (int c in cells) if (CanGravel(c) && _gravel[c] == 0) { _gravel[c] = 1; n++; }
        if (n > 0) Edited();
        return n;
    }

    /// <summary>What the Gate tool does at (x, y) in cell units: the gate cells on the nearest fence within 2 cells, an existing gate (to remove), or why not.</summary>
    public GateTarget GateTarget(double x, double y) => FenceGates.Target(this, x, y);

    /// <summary>The cells a gate placed at fence cell c takes (one or two), or null if no gate can go there.</summary>
    public int[]? GateCellsFor(int c) => FenceGates.GateCellsFor(this, c);

    /// <summary>Puts a gate in the fence at cell c (and its stair partner if it needs one); false if it cannot go there. Free, also under the river's money rules.</summary>
    public bool PlaceGate(int c)
    {
        var cells = GateCellsFor(c);
        if (cells == null) return false;
        foreach (int g in cells) { _gate[g] = 1; _gatePair[g] = (byte)(cells.Length == 2 ? 1 : 0); }
        Edited();
        return true;
    }

    /// <summary>Turns a gate (both cells of a two-cell gate) back into plain fence.</summary>
    public bool RemoveGate(int c)
    {
        if (c < 0 || c >= _nc || _gate[c] == 0) return false;
        foreach (int g in _gatePair[c] != 0 ? FenceGates.GateGroup(this, c) : new[] { c }) { _gate[g] = 0; _gatePair[g] = 0; }
        Edited();
        return true;
    }

    /// <summary>
    /// The water cells a footbridge would cover for a click at (x, y), appended to <paramref name="cells"/>;
    /// false if none fits there (outside the river band, too near the ford, already bridged, or an end blocked).
    /// </summary>
    public bool BridgeCellsAt(double x, double y, List<int> cells)
    {
        cells.Clear();
        int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
        if (!_g.Inside(ix, iy)) return false;
        var m = _map;
        int rx = (int)Math.Floor(m.RiverX[iy]);
        if (Math.Abs(ix - rx) > m.RiverHalfW + 4) return false;
        if (Math.Abs(iy - m.FordY) <= 2) return false;
        for (int xx = rx - 8; xx <= rx + 8; xx++)
        {
            if (!_g.Inside(xx, iy)) continue;
            int c = iy * _n + xx;
            if (m.Water[c] != 0) cells.Add(c);
        }
        bool ok = cells.Count > 0;
        if (ok) foreach (int c in cells) if (_bridgeAt[c] >= 0 || m.Ford[c] != 0) { ok = false; break; }
        // Both ends must land on open ground. An end beyond the first or last column is off the map:
        // cells[0] - 1 would otherwise read the previous row (or index -1 on row 0).
        if (ok)
        {
            int west = cells[0], east = cells[^1];
            if (west % _n == 0 || east % _n == _n - 1 || IsBlocked(west - 1) || IsBlocked(east + 1)) ok = false;
        }
        if (!ok) cells.Clear();
        return ok;
    }

    /// <summary>The footbridge cells for a click at (x, y), or null if none fits (allocates; for tools and tests).</summary>
    public int[]? BridgeCellsAt(double x, double y)
    {
        var list = new List<int>();
        return BridgeCellsAt(x, y, list) ? list.ToArray() : null;
    }

    /// <summary>Builds a footbridge across the river at the row of (x, y). Returns it, or null if none fits.</summary>
    public Bridge? PlaceBridge(double x, double y)
    {
        if (_river != null)
        {
            // With the river rules a footbridge costs coin and keeps clear of the landing and other crossings.
            var r = PlaceFootbridge(x, y);
            return r.Ok ? _bridges[^1] : null;
        }
        if (!BridgeCellsAt(x, y, _bridgeScratch)) return null;
        var br = AddBridgeFromScratch((int)Math.Floor(y));
        Edited();
        return br;
    }

    /// <summary>Adds a bridge over the cells in the scratch list (no checks, no rebuild).</summary>
    private Bridge AddBridgeFromScratch(int row)
    {
        // Reuse the lowest free id so bridge ids stay small.
        int id = 0;
        while (_bridges.Exists(b => b.Id == id)) id++;
        var br = new Bridge(id, row, _bridgeScratch.ToArray());
        _bridges.Add(br);
        foreach (int c in br.Cells) _bridgeAt[c] = (short)id;
        return br;
    }

    /// <summary>
    /// Removes fences (with their gates), gravel and whole bridges touching these cells. Returns how many things were removed. With the river rules a
    /// crossing whose erase cost would take the purse below the debt limit stays (reason in LastRiverRefusal).
    /// </summary>
    public int Erase(ReadOnlySpan<int> cells)
    {
        int n = 0;
        _eraseRefused = -1;
        foreach (int c in cells)
        {
            if (c < 0 || c >= _nc) continue;
            if (_fence[c] != 0)
            {
                // Half of a two-cell gate: its partner stays a gate on its own.
                if (_gatePair[c] != 0) foreach (int g in FenceGates.GateGroup(this, c)) _gatePair[g] = 0;
                _fence[c] = 0; _gate[c] = 0; n++;
            }
            if (_gravel[c] != 0) { _gravel[c] = 0; n++; }
            if (_river != null) n += RiverEraseAt(c);
            int b = _bridgeAt[c];
            if (b >= 0)
            {
                // With the river rules taking a bridge down costs coin; a purse at the debt limit keeps it standing.
                if (_river != null && !RiverBridgeErased(b)) continue;
                int k = _bridges.FindIndex(q => q.Id == b);
                foreach (int bc in _bridges[k].Cells) _bridgeAt[bc] = -1;
                _bridges.RemoveAt(k); // keeps the order of the others, as the toy's filter does
                n++;
            }
        }
        if (n > 0) Edited();
        return n;
    }
}
