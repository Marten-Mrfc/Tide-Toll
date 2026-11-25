using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using static TideToll.Sim.Testing.ToyHarness;

namespace TideToll.Sim.Testing;

/// <summary>
/// The toy's headless checks (test/sim.test.ts at a057b50), one method per toy function, same setup,
/// same measurements, same pass criteria and the same printed lines.
/// </summary>
public static class ToyChecks
{
    public static void Reroute(int seed, CheckLog log)
    {
        var w = new World(seed, 200, SimParams.Legacy);
        int N = w.Grid.Width, NC = w.Grid.Count, TPD = TicksPerDay(w);
        Run(w, 20);
        uint[] fw = w.Metrics.Window().ToArray();
        // The busiest trail cell well away from doors and the river.
        int best = -1;
        for (int c = 0; c < NC; c++)
        {
            if (w.Land[c] == 0 || w.Wear[c] < 25 || w.Crop[c] != 0) continue;
            int x = c % N, y = c / N;
            if (x < 10 || y < 10 || x > N - 11 || y > N - 11) continue;
            if (Math.Abs(x + 0.5 - w.RiverX[y]) < w.Map.RiverHalfW + 8 || DoorDistance(w, c) < 10) continue;
            if (best < 0 || fw[c] > fw[best]) best = c;
        }
        if (best < 0) { log.Check(false, "reroute: fence placed across the trail (no busy trail cell found)"); return; }
        int bx = best % N, by = best / N;
        // Trail direction: principal axis of trail cells within 4 cells, weighted by traffic.
        double sxx = 0, syy = 0, sxy = 0;
        for (int y = by - 4; y <= by + 4; y++)
            for (int x = bx - 4; x <= bx + 4; x++)
            {
                if (!w.Grid.Inside(x, y)) continue;
                int c = y * N + x;
                double m = fw[c];
                if (w.Wear[c] < 25 || m == 0) continue;
                sxx += m * (x - bx) * (x - bx); syy += m * (y - by) * (y - by); sxy += m * (x - bx) * (y - by);
            }
        double ang = 0.5 * Math.Atan2(2 * sxy, sxx - syy); // along the trail
        double px = -Math.Sin(ang), py = Math.Cos(ang); // across it
        const int H = 7;
        var fence = Line4(w, (int)JsMath.Round(bx - px * H), (int)JsMath.Round(by - py * H), (int)JsMath.Round(bx + px * H), (int)JsMath.Round(by + py * H));
        float[] before = w.Wear.ToArray();
        int tripsBefore = w.Stats().TripsInWindow;
        int placed = w.PlaceFence(fence.ToArray());
        int[] ends = { fence[0], fence[^1] };
        int onFence = 0;
        double newTrailAt = -1, t0 = w.Time;
        for (int i = 0; i < 6 * TPD; i++)
        {
            w.Tick();
            foreach (ref readonly var k in w.Walkers) if (k.Visible && w.Fence[k.Cell] != 0) onFence++;
            if (newTrailAt < 0 && i % 10 == 0)
            {
                // New trail cells (not trail before the fence) near either end of the fence.
                int n = 0;
                foreach (int e in ends)
                    for (int y = e / N - 5; y <= e / N + 5; y++)
                        for (int x = e % N - 5; x <= e % N + 5; x++)
                        {
                            if (!w.Grid.Inside(x, y)) continue;
                            int c = y * N + x;
                            if (w.Fence[c] == 0 && w.Wear[c] >= 25 && before[c] < 25) n++;
                        }
                if (n >= 8) newTrailAt = w.Time - t0;
            }
        }
        var fwAfter = w.Metrics.Window();
        long fenceSteps = 0;
        foreach (int c in fence) if (w.Fence[c] != 0) fenceSteps += fwAfter[c];
        int tripsAfter = w.Stats().TripsInWindow;
        double day = w.Params.DaySimSec;
        log.Log($"\nREROUTE TEST (seed {seed}): fence of {placed} cells across the busiest trail at ({bx},{by}), 6 days after placing");
        log.Log($"  footsteps on fence cells: {fenceSteps}; walker-ticks on fence cells: {onFence}; trips in window before/after: {tripsBefore}/{tripsAfter}");
        log.Log($"  new trail round the fence ends: {(newTrailAt < 0 ? "not formed" : $"formed after {F(newTrailAt, 1)} s sim ({F(newTrailAt / day, 1)} days)")}");
        log.Check(placed >= 10, "reroute: fence placed across the trail");
        log.Check(fenceSteps == 0 && onFence == 0, "reroute: traffic across the fenced line drops to zero");
        log.Check(newTrailAt >= 0 && newTrailAt <= 3 * day, "reroute: a new trail forms round the fence within 3 in-game days (45 s at 1x)");
        log.Check(tripsAfter >= 0.7 * tripsBefore, "reroute: trips keep completing (at least 70% of the rate before)");
    }

    public static void BridgeCheck(int seed, CheckLog log)
    {
        var w = new World(seed, 200, SimParams.Legacy);
        int N = w.Grid.Width, TPD = TicksPerDay(w);
        Run(w, 15);
        // Bridge on the row of the market door, or the nearest row that takes one.
        var market = w.Buildings[w.OfKind(BuildingKind.Market)[0]];
        int my = market.Door / N;
        Bridge? bridge = null;
        for (int d = 0; d < 40 && bridge == null; d++)
            foreach (int y in new[] { my + d, my - d })
            {
                if (bridge != null || y < 4 || y > N - 5 || Math.Abs(y - w.Map.FordY) < 8) continue;
                bridge = w.PlaceBridge(w.RiverX[y], y);
            }
        if (bridge == null) { log.Check(false, "bridge: a footbridge could be placed"); return; }
        Run(w, 1); // let walkers re-plan and finish trips already under way
        var cc = new CrossingCounter(w);
        for (int i = 0; i < 4 * TPD; i++) { w.Tick(); cc.Sample(w); }
        double share = (double)cc.Bridge / Math.Max(1, cc.Bridge + cc.Ford);
        log.Log($"\nBRIDGE TEST (seed {seed}): footbridge of {bridge.Cells.Count} cells on row {bridge.Row} (ford at row {w.Map.FordY}); 4 days after");
        log.Log($"  crossings by bridge: {cc.Bridge}, by ford: {cc.Ford}; bridge share {Pct(share)}");
        log.Check(share >= 0.4, "bridge: the new footbridge takes at least 40% of river crossings");
        // Removing it sends traffic back to the ford.
        w.Erase(new[] { bridge.Cells[0] });
        Run(w, 1);
        var cc2 = new CrossingCounter(w);
        for (int i = 0; i < 2 * TPD; i++) { w.Tick(); cc2.Sample(w); }
        log.Check(cc2.Bridge == 0 && cc2.Ford > 0 && w.Bridges.Count == 0, "bridge: erasing it returns all crossings to the ford");
    }

    public static void Robustness(CheckLog log)
    {
        var w = new World(5, 300, SimParams.Legacy);
        int NC = w.Grid.Count, TPD = TicksPerDay(w);
        Run(w, 4);
        // Wall a house in completely, fence a crowded strip, then check every walker.
        var ring = RingAround(w, w.Buildings[w.Houses[0]]);
        w.PlaceFence(ring.ToArray());
        // A fence right through the busiest cells, with walkers on them (stable sort, as the toy's).
        uint[] fw = w.Metrics.Window().ToArray();
        byte[] land = w.Land.ToArray();
        int[] busy = Enumerable.Range(0, NC).Where(c => land[c] != 0).OrderByDescending(c => fw[c]).Take(40).ToArray();
        w.PlaceFence(busy);
        int bad = 0, stuckLong = 0;
        for (int i = 0; i < 3 * TPD; i++)
        {
            w.Tick();
            foreach (ref readonly var k in w.Walkers)
            {
                if (k.Visible && w.IsBlocked(k.Cell)) bad++;
                if (k.Stuck > w.Params.StuckSimSec + 0.2) stuckLong++;
            }
        }
        log.Log($"\nROBUSTNESS: house walled in and 40 busy cells fenced; walker-ticks on blocked cells: {bad}; stuck past limit: {stuckLong}");
        log.Check(bad == 0, "robustness: no walker ever stands on a blocked cell");
        log.Check(stuckLong == 0, "robustness: no walker stays stuck past the limit");
        w.Erase(ring.Concat(busy).ToArray());
        Run(w, 2);
        bool clear = true;
        foreach (byte f in w.Fence) if (f != 0) { clear = false; break; }
        log.Check(clear && w.Stats().TripsInWindow > 50, "robustness: erasing restores normal traffic");
    }

    public static void Determinism(CheckLog log)
    {
        World a = new(9, 250), b = new(9, 250);
        void Act(World w)
        {
            Run(w, 3);
            w.PlaceFence(Line4(w, 30, 30, 50, 60).ToArray());
            w.PlaceBridge(w.RiverX[20], 20);
            Run(w, 2);
        }
        Act(a); Act(b);
        bool same = a.Wear.SequenceEqual(b.Wear) && a.WalkerCount == b.WalkerCount;
        for (int i = 0; same && i < a.WalkerCount; i++) same = a.Walkers[i].X == b.Walkers[i].X;
        log.Check(same, "determinism: same seed and inputs give an identical run");
    }
}
