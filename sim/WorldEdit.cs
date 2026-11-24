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
        for (int t = 0; t < _urgent.Length; t++) _urgent[t] = true;
        // Rebuild costs now so walkers stop stepping onto new fences this tick.
        BuildCost(_cost);
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
        return true;
    }

    /// <summary>Places fence cells (cell indices; out-of-range ones are ignored). Returns how many were new.</summary>
    public int PlaceFence(ReadOnlySpan<int> cells)
    {
        int n = 0;
        foreach (int c in cells)
            if (c >= 0 && c < _nc && _fence[c] == 0 && CanFence(c)) { _fence[c] = 1; n++; }
        if (n > 0) Edited();
        return n;
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
        if (!BridgeCellsAt(x, y, _bridgeScratch)) return null;
        // Reuse the lowest free id so bridge ids stay small.
        int id = 0;
        while (_bridges.Exists(b => b.Id == id)) id++;
        var br = new Bridge(id, (int)Math.Floor(y), _bridgeScratch.ToArray());
        _bridges.Add(br);
        foreach (int c in br.Cells) _bridgeAt[c] = (short)id;
        Edited();
        return br;
    }

    /// <summary>Removes fences and whole bridges touching these cells. Returns how many things were removed.</summary>
    public int Erase(ReadOnlySpan<int> cells)
    {
        int n = 0;
        foreach (int c in cells)
        {
            if (c < 0 || c >= _nc) continue;
            if (_fence[c] != 0) { _fence[c] = 0; n++; }
            int b = _bridgeAt[c];
            if (b >= 0)
            {
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
