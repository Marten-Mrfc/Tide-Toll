using System;

namespace TideToll.Sim;

// Walkers: trips, dwell, the any-angle aim rule and footsteps (world.ts walkers section and path.ts walking).
public sealed partial class World
{
    /// <summary>Sets the number of walkers (clamped to 0..MaxWalkers). Never allocates: the array is pre-sized.</summary>
    public void SetWalkerCount(int n)
    {
        n = Math.Max(0, Math.Min(_p.MaxWalkers, n));
        int[] houses = _byKind[(int)BuildingKind.House];
        while (_walkerCount > n)
        {
            ref Walker w = ref _walkers[--_walkerCount];
            if (w.Visible) _here[w.Cell]--;
        }
        while (_walkerCount < n && houses.Length > 0)
        {
            int id = _nextId++;
            int home = houses[id % houses.Length];
            int door = _map.Buildings[home].Door;
            double x = door % _n + 0.5, y = door / _n + 0.5;
            // Timer first, taste second: the toy's order of draws.
            double timer = _rng.NextDouble() * _p.DwellSimSec * 2;
            double taste = (_rng.NextDouble() * 2 - 1) * _p.TasteNoise;
            _walkers[_walkerCount++] = new Walker
            {
                Id = id, Home = home, State = WalkState.Home, Cell = door, AimX = x, AimY = y, HasAim = false,
                Timer = timer, Target = home, TripStart = door, TripLen = 0, Stuck = 0, Taste = taste,
                Explorer = Hash.Hash2(id, 77) < _p.ExplorerShare,
                X = x, Y = y, Px = x, Py = y,
            };
        }
    }

    private double Dwell()
    {
        double a = _p.DwellJitterLow, b = _p.DwellJitterHigh;
        return _p.DwellSimSec * (a + _rng.NextDouble() * (b - a));
    }

    private int PickDestination(ref Walker w)
    {
        double well = _p.TripMixWell, field = _p.TripMixField, market = _p.TripMixMarket;
        double r = _rng.NextDouble() * (well + field + market);
        int first = r < well ? 0 : r < well + field ? 1 : 2;
        // Try the chosen kind first, then the others, nearest by travel cost.
        for (int k = 0; k < 3; k++)
        {
            int best = -1;
            double bv = double.PositiveInfinity;
            foreach (int b in _byKind[1 + (first + k) % 3]) // well, field, market
            {
                double v = _fields[b][w.Cell];
                if (v < bv) { bv = v; best = b; }
            }
            if (best >= 0) return best;
        }
        return -1;
    }

    private void Depart(ref Walker w, int target, WalkState state)
    {
        w.State = state;
        w.Target = target;
        w.TripStart = w.Cell;
        w.TripLen = 0;
        w.TripT0 = _time;
        w.Stuck = 0;
        w.HasAim = false;
        w.Frugal = w.Explorer || OverDetourCap(w.Cell, target);
        _here[w.Cell]++;
        if (state == WalkState.Out) _tripRate[w.Home * _map.Buildings.Length + target] += 1;
    }

    /// <summary>Whether a trip from <paramref name="start"/> to building <paramref name="target"/> is long enough a detour to be walked frugally.</summary>
    internal bool OverDetourCap(int start, int target)
    {
        double cap = _p.DetourCap;
        if (!(cap > 0)) return false;
        int door = _map.Buildings[target].Door, n = _n;
        double d = JsMath.Hypot(start % n - door % n, start / n - door / n);
        return d > 4 && RouteLength(_fields[target], _cost, start, door) > cap * d;
    }

    private void Arrive(ref Walker w)
    {
        int s = w.TripStart, e = w.Cell, n = _n;
        _metrics.Trip(_time, w.TripLen, JsMath.Hypot(s % n - e % n, s / n - e / n));
        _here[w.Cell]--;
        if (_river != null) RiverArrive(ref w);
        w.State = w.State == WalkState.Out ? WalkState.Away : WalkState.Home;
        w.Timer = Dwell();
        w.HasAim = false;
    }

    /// <summary>Sends a walker home at once (stuck, walled in, or its cell was fenced).</summary>
    private void SendHome(ref Walker w)
    {
        if (w.Visible) _here[w.Cell]--;
        w.Hold = WalkerHold.None;
        if (w.Cargo != 0 && _river != null) _rs!.GrainLost += w.Cargo;
        w.Cargo = 0;
        w.Cell = _map.Buildings[w.Home].Door;
        w.State = WalkState.Home;
        w.Timer = _p.RetrySimSec;
        w.HasAim = false;
        w.X = w.Px = w.Cell % _n + 0.5;
        w.Y = w.Py = w.Cell / _n + 0.5;
        if (_river != null) { w.Waited = false; RiverPlaced(ref w); }
    }

    private bool ComputeAim(ref Walker w)
    {
        bool fr = w.Frugal;
        bool ok = ChooseAim(fr ? _fieldsX[w.Target] : _fields[w.Target], fr ? _costX : _cost, w.X, w.Y, w.Cell, _map.Buildings[w.Target].Door,
            fr ? 0 : w.Taste, true, w.Id, _aimPath, out double ax, out double ay);
        if (ok) { w.AimX = ax; w.AimY = ay; w.HasAim = true; }
        return ok;
    }

    private double SpeedOn(int c)
    {
        double s = _p.WalkSpeedCellsPerSec;
        if (_bridgeAt[c] >= 0) s *= _p.BridgeSpeedMul;
        else if (_river != null && _frozen && _map.Water[c] != 0) s *= _river.IceSpeedMul;
        else
        {
            s *= _gravel[c] != 0 ? _p.GravelSpeedMul : _stateSpeedMul[(int)_p.GroundOf(_wear[c], _hollow[c] == 1)];
            if (_map.Ford[c] != 0) s *= _p.FordSpeedMul;
            if (_gate[c] != 0) s *= _gatePair[c] != 0 ? 1 / (1 + (1 / _p.GateSpeedMul - 1) / 2) : _p.GateSpeedMul;
            if (_river != null && _flooded && _band[c] != 0) s *= _river.FloodWadeSpeedMul;
        }
        double rho = _here[c] / _p.CellAreaM2;
        if (rho > 0.3) s *= Math.Max(0.1, 1 - Math.Exp(-_p.WeidmannGamma * (1 / rho - 1 / _p.WeidmannRhoMax)));
        return s;
    }

    /// <summary>Wear for entering cell c at point (x, y) while heading (dx, dy) / d.</summary>
    private void Footstep(int c, double x, double y, double dx, double dy, double d)
    {
        if (_river != null && (_flooded || _frozen) && RiverFootstep(c, x, y, dx, dy, d)) { }
        else if (_land[c] != 0 && _bridgeAt[c] < 0)
        {
            double I = _p.WearPerStep;
            _wear[c] = (float)(_wear[c] + I * (1 - _wear[c] / 100.0));
            // Side neighbours, perpendicular to the heading, measured from the walker's actual point.
            if (d > 1e-9)
            {
                double px = (-dy / d) * 0.8, py = (dx / d) * 0.8;
                for (int s = 1; s >= -1; s -= 2)
                {
                    int sx = (int)Math.Floor(x + px * s), sy = (int)Math.Floor(y + py * s);
                    if (!_g.Inside(sx, sy)) continue;
                    int si = sy * _n + sx;
                    if (si == c || _land[si] == 0 || _fence[si] != 0) continue;
                    if (_river != null && _flooded && _band[si] != 0) continue; // flood water does not wear
                    _wear[si] = (float)(_wear[si] + I * _p.SideShare * (1 - _wear[si] / 100.0));
                }
            }
        }
        _flow[c] = (float)(_flow[c] + 1.0);
        _metrics.Footstep(c);
    }

    private void StepWalker(ref Walker w, double dt)
    {
        if (w.Hold != WalkerHold.None)
        {
            if (w.Hold == WalkerHold.Riding) return; // the ferry moves it
            w.Px = w.X; w.Py = w.Y;
            if (!RiverHold(ref w, dt)) return;
        }
        w.Px = w.X; w.Py = w.Y;
        if (w.State == WalkState.Home || w.State == WalkState.Away)
        {
            w.Timer -= dt;
            if (w.Timer > 0) return;
            if (w.State == WalkState.Home)
            {
                int t = PickDestination(ref w);
                if (t < 0) { w.Timer = _p.RetrySimSec; return; }
                Depart(ref w, t, WalkState.Out);
            }
            else if (_river == null || !HaulLeg(ref w))
            {
                if (!(_fields[w.Home][w.Cell] < float.PositiveInfinity)) { SendHome(ref w); return; }
                Depart(ref w, w.Home, WalkState.Back);
            }
        }
        // Walking.
        if (w.Cell == _map.Buildings[w.Target].Door) { Arrive(ref w); return; }
        if (!w.HasAim && !ComputeAim(ref w))
        {
            w.Stuck += dt;
            // Cut off from a destination but able to get home: give up the errand.
            if (w.State == WalkState.Out && !(_fields[w.Target][w.Cell] < float.PositiveInfinity) && _fields[w.Home][w.Cell] < float.PositiveInfinity)
            {
                w.State = WalkState.Back; w.Target = w.Home;
            }
            else if (w.Stuck > _p.StuckSimSec) SendHome(ref w);
            return;
        }
        w.Stuck = 0;
        // A runaway trip (should not happen) is ended rather than left wandering.
        if (w.TripLen > 4 * _n) { SendHome(ref w); return; }
        double dist = SpeedOn(w.Cell) * dt;
        for (int guard = 0; dist > 0 && guard < 4; guard++)
        {
            double dx = w.AimX - w.X, dy = w.AimY - w.Y, d = JsMath.Hypot(dx, dy);
            double move = Math.Min(d, dist);
            double ox = w.X, oy = w.Y, olen = w.TripLen;
            if (d > 1e-9)
            {
                w.X += (dx / d) * move; w.Y += (dy / d) * move;
                w.TripLen += move;
            }
            dist -= move;
            int c = (int)Math.Floor(w.Y) * _n + (int)Math.Floor(w.X);
            if (c != w.Cell && _river != null && RiverGate(ref w, c))
            {
                // A drawbridge that is not down, or a ferry stage: stop on this bank and wait.
                w.X = ox; w.Y = oy; w.TripLen = olen;
                break;
            }
            if (c != w.Cell)
            {
                _here[w.Cell]--; _here[c]++;
                w.Cell = c;
                if (_river != null) RiverEnter(ref w, c);
                Footstep(c, w.X, w.Y, dx, dy, d);
                w.HasAim = false;
            }
            if (move >= d) w.HasAim = false;
            if (!w.HasAim)
            {
                if (w.Cell == _map.Buildings[w.Target].Door || !ComputeAim(ref w)) break;
            }
        }
    }

    // ---------------------------------------------------------------- the aim rule (path.ts)
    // Pure functions of (field, cost) plus the world's wear discount, so the flow preview traces
    // exactly the route a walker would take, from the same code.

    /// <summary>
    /// One step of field descent from u, or -1. <paramref name="taste"/> scales the worn-ground discount
    /// (only when <paramref name="useDisc"/>); <paramref name="noiseId"/> &gt;= 0 adds that walker's fixed
    /// per-cell step noise. Taste 0 and noiseId -1 give the modal step.
    /// </summary>
    internal int DescendStep(float[] field, float[] cost, int u, double taste, bool useDisc, int noiseId)
    {
        float fu = field[u];
        if (!(fu < float.PositiveInfinity)) return -1;
        double noise = _p.StepNoise;
        int best = -1;
        double bv = double.PositiveInfinity;
        for (int k = 0; k < 8; k++)
        {
            int v = CostField.CanStep(_g, u, k, cost);
            if (v < 0 || !(field[v] < fu)) continue; // strict progress, so no loops
            double edge = Grid.DL[k] * 0.5 * ((double)cost[u] + cost[v]);
            double val = field[v] + (noiseId >= 0 ? edge * (1 + noise * (Hash.Hash2(noiseId, v) * 2 - 1)) : edge);
            if (taste != 0 && useDisc) val -= taste * (1 - Surf(v)) * Grid.DL[k];
            if (val < bv) { bv = val; best = v; }
        }
        return best;
    }

    internal double LineCost(float[] cost, double x0, double y0, double x1, double y1, double limit = double.PositiveInfinity)
        => CostField.LineCost(_g, cost, x0, y0, x1, y1, limit);

    internal double RouteLength(float[] field, float[] cost, int start, int door) => CostField.RouteLength(_g, field, cost, start, door);

    /// <summary>
    /// Where a walker at (x, y) in <paramref name="cell"/> heads next: it looks Lookahead cells down the
    /// field and aims at the farthest one whose straight line is open and no dearer than the field's route
    /// there. <paramref name="path"/> is scratch of at least Lookahead entries. False if there is no step.
    /// </summary>
    internal bool ChooseAim(float[] field, float[] cost, double x, double y, int cell, int door,
        double taste, bool useDisc, int noiseId, int[] path, out double aimX, out double aimY)
    {
        int n = _n, len = 0, u = cell;
        for (int s = 0; s < _p.Lookahead && u != door; s++)
        {
            u = DescendStep(field, cost, u, taste, useDisc, noiseId);
            if (u < 0) break;
            path[len++] = u;
        }
        aimX = aimY = 0;
        if (len == 0) return false;
        double fHere = field[cell];
        for (int j = len - 1; j >= 0; j--)
        {
            double ax = path[j] % n + 0.5, ay = path[j] / n + 0.5;
            double bound = (fHere - field[path[j]]) * _p.ShortcutSlack;
            double lc = LineCost(cost, x, y, ax, ay, j == 0 ? double.PositiveInfinity : bound);
            if (j == 0 || lc <= bound)
            {
                if (double.IsPositiveInfinity(lc))
                {
                    // Off-centre next to a corner: step back to the cell centre first.
                    aimX = cell % n + 0.5; aimY = cell / n + 0.5;
                }
                else { aimX = ax; aimY = ay; }
                return true;
            }
        }
        return false;
    }
}
