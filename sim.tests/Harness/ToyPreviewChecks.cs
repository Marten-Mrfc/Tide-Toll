using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using static TideToll.Sim.Testing.ToyHarness;

namespace TideToll.Sim.Testing;

/// <summary>The toy's flow preview checks (previewIsolationTest, previewBasicsTest, previewAgreementTest).</summary>
public static class ToyPreviewChecks
{
    public static void Isolation(CheckLog log)
    {
        World a = new(3, 250), b = new(3, 250);
        int N = a.Grid.Width, TPD = TicksPerDay(a);
        Run(a, 2); Run(b, 2);
        int calls = 0;
        for (int i = 0; i < 3 * TPD; i++)
        {
            a.Tick(); b.Tick();
            if (i % 2 == 0)
            {
                // Fences, bridges and erases, as the page asks while a player drags.
                int k = calls++ % 4, y = 20 + calls % 80;
                if (k == 0) b.PreviewFlows(new WhatIf { Block = Line4(b, 10 + calls % 50, 20, 30, 70).FindAll(b.CanFence).ToArray() });
                else if (k == 1) b.PreviewFlows(new WhatIf { Bridge = b.BridgeCellsAt(b.RiverX[y], y) ?? Array.Empty<int>() });
                else if (k == 2) b.PreviewFlows(new WhatIf { Open = new[] { y * N + 40 } });
                else b.PreviewFlows(new WhatIf());
            }
        }
        bool ok = a.Wear.SequenceEqual(b.Wear) && a.Cost.SequenceEqual(b.Cost) && a.Flow.SequenceEqual(b.Flow) && a.TripRate.SequenceEqual(b.TripRate);
        for (int t = 0; ok && t < a.Buildings.Length; t++) ok = a.Field(t).SequenceEqual(b.Field(t));
        for (int i = 0; ok && i < a.WalkerCount; i++)
            ok = a.Walkers[i].X == b.Walkers[i].X && a.Walkers[i].Y == b.Walkers[i].Y && a.Walkers[i].Cell == b.Walkers[i].Cell;
        ok = ok && a.RngState == b.RngState;
        log.Log($"\nFLOW PREVIEW: isolation (seed 3, 250 walkers, {calls} preview queries over 3 days)");
        log.Check(calls >= 200 && ok, "preview: queries leave wear, costs, fields, walkers and the RNG stream byte-identical");
    }

    public static void Basics(CheckLog log)
    {
        log.Log("\nFLOW PREVIEW: basics (seed 2, 200 walkers, day 20)");
        var w = new World(2, 200, SimParams.Legacy);
        int N = w.Grid.Width;
        Run(w, 20);
        var empty = w.PreviewFlows(new WhatIf());
        log.Check(empty.Changes.Count == 0 && empty.ReroutedPerDay == 0, "preview: an empty change shows no changed routes");

        // Speed: a full preview (base fields stale) and a hypothetical-only one. Timed in the thread's CPU time, so
        // other jobs on a shared machine do not count; the median of 6 must be under 20 ms, and a busy machine
        // (cache and frequency effects that CPU time does not remove) gets up to three tries at the same bar.
        int[] fence = FenceAcrossRoute(w, w.Houses[0], 0.3);
        double median = double.PositiveInfinity, worst = 0, hypMedian = 0;
        for (int attempt = 0; attempt < 3 && !(median < 20); attempt++)
        {
            var times = new List<double>();
            var hypTimes = new List<double>();
            for (int i = 0; i < 6; i++)
            {
                // The first try runs as the toy does (the base is rebuilt after a second of sim time); a retry times
                // fresh predictors on the same world, so the checks below see the same town whatever the timing did.
                FlowPredictor p = attempt == 0 ? null! : new FlowPredictor(w);
                if (attempt == 0) Run(w, 0.1);
                double t0 = CpuClock.NowMs();
                if (attempt == 0) w.PreviewFlows(new WhatIf { Block = fence }); else p.Compute(new WhatIf { Block = fence });
                times.Add(CpuClock.NowMs() - t0);
                t0 = CpuClock.NowMs();
                if (attempt == 0) w.PreviewFlows(new WhatIf { Block = fence[1..] }); else p.Compute(new WhatIf { Block = fence[1..] });
                hypTimes.Add(CpuClock.NowMs() - t0);
            }
            times.Sort(); hypTimes.Sort();
            if (times[3] < median) { median = times[3]; worst = times[5]; hypMedian = hypTimes[3]; }
            log.Log($"  try {attempt + 1}: full preview {F(times[3], 1)} ms median (worst {F(times[5], 1)}), with cached base {F(hypTimes[3], 1)} ms median");
        }
        log.Log($"  preview time in C# ({(CpuClock.IsCpuTime ? "thread CPU time" : "wall time")}): full {F(median, 1)} ms median (worst {F(worst, 1)}), with cached base {F(hypMedian, 1)} ms median");
        // The toy times optimised JIT code in Node; the C# equivalent is a Release build. In Debug the time is
        // printed but not judged (./check and the tools run Release).
        if (TickBench.IsRelease) log.Check(median < 20, "preview: a full preview takes under 20 ms (median) in C#");
        else log.Log("  SKIP  preview: a full preview takes under 20 ms (median) in C# (Debug build: timed only in Release)");

        // Bridge: the preview route crosses it, and the built bridge gets walkers.
        var market = w.Buildings[w.OfKind(BuildingKind.Market)[0]];
        int my = market.Door / N;
        int[]? cells = null;
        int row = -1;
        for (int d = 0; d < 40 && cells == null; d++)
            foreach (int y in new[] { my + d, my - d })
                if (cells == null && y > 3 && y < N - 4 && Math.Abs(y - w.Map.FordY) >= 8) { cells = w.BridgeCellsAt(w.RiverX[y], y); row = y; }
        if (cells == null) { log.Check(false, "preview: a footbridge's predicted routes cross the bridge (no row takes a bridge)"); return; }
        var pb = w.PreviewFlows(new WhatIf { Bridge = cells });
        bool OnBridge(float[] pts)
        {
            for (int i = 0; i < pts.Length; i += 2) if (cells!.Contains((int)Math.Floor(pts[i + 1]) * N + (int)Math.Floor(pts[i]))) return true;
            return false;
        }
        int crossing = pb.Changes.Count(c => OnBridge(c.NewPts));
        log.Log($"  bridge on row {row}: {pb.Changes.Count} changed routes shown, {crossing} cross the bridge; \"{FlowPredictor.Describe(pb, x => KindName(w.Buildings[x].Kind))}\"");
        log.Check(crossing > 0, "preview: a footbridge's predicted routes cross the bridge");
        w.PlaceBridge(w.RiverX[row], row);
        Run(w, 2);
        var fw = w.Metrics.Window();
        long steps = 0;
        foreach (int c in cells) steps += fw[c];
        log.Check(steps > 0, $"preview: the bridge then carries walkers ({steps} footsteps in 2 days)");

        // Unreachable: a fence ring round the well's door.
        var well = w.Buildings[w.OfKind(BuildingKind.Well)[0]];
        var ring = RingAround(w, well).FindAll(w.CanFence).ToArray();
        var pu = w.PreviewFlows(new WhatIf { Block = ring });
        bool blockedWell = pu.Blocked.Exists(q => q.Dest == well.Id);
        log.Log($"  fence ring round the well: \"{FlowPredictor.Describe(pu, x => w.Buildings[x].Kind == BuildingKind.Well ? "Well" : KindName(w.Buildings[x].Kind))}\"");
        log.Check(blockedWell && pu.Changes.Exists(c => c.Dest == well.Id && !c.Reachable), "preview: enclosing a destination is shown as unreachable");
    }

    public static void Agreement(int[] seeds, CheckLog log)
    {
        log.Log($"\nFLOW PREVIEW: agreement with the walkers (seeds {string.Join(", ", seeds)}, 200 walkers, fence placed on day 20)");
        log.Log("seed | fence | pairs | readout       | detour points near new trail | trips/day after: all  new route | predicted/day");
        double predicted = 0, measured = 0;
        int detourPts = 0, detourNear = 0, eraseShorter = 0;
        foreach (int seed in seeds)
        {
            var w = new World(seed, 200, SimParams.Legacy);
            int N = w.Grid.Width, TPD = TicksPerDay(w);
            Run(w, 20);
            int[] fence = FenceAcrossRoute(w, w.Houses[seed % w.Houses.Length], 0.3);
            var pv = w.PreviewFlows(new WhatIf { Block = fence }, 99);
            float[] before = w.Wear.ToArray();
            w.PlaceFence(fence);
            int B = w.Buildings.Length;
            var routes = new Dictionary<int, float[]>();
            foreach (var c in pv.Changes) routes[c.Home * B + c.Dest] = c.NewPts;
            // Follow every trip of a changed pair for 6 days: count departures, and classify completed
            // trips by whether they kept within 3 cells of the predicted route for at least 80% of their positions.
            const int DaysAfter = 6;
            var track = new Dictionary<int, List<double>>();
            var prev = new WalkState[w.WalkerCount];
            for (int j = 0; j < prev.Length; j++) prev[j] = w.Walkers[j].State;
            int departures = 0, done = 0, onNew = 0;
            for (int i = 0; i < DaysAfter * TPD; i++)
            {
                w.Tick();
                var ws = w.Walkers;
                for (int j = 0; j < ws.Length; j++)
                {
                    ref readonly var k = ref ws[j];
                    int key = k.Home * B + k.Target;
                    if (k.State == WalkState.Out && prev[j] != WalkState.Out && routes.ContainsKey(key)) { track[k.Id] = new List<double>(); departures++; }
                    prev[j] = k.State;
                    if (!track.TryGetValue(k.Id, out var tr)) continue;
                    if (k.State == WalkState.Out) { if (i % 3 == 0) { tr.Add(k.X); tr.Add(k.Y); } continue; }
                    track.Remove(k.Id);
                    // The toy reads routes.get(key) here; a walker that gave up its errand has no route (the toy would throw).
                    routes.TryGetValue(key, out var route);
                    int near = 0;
                    for (int q = 0; q < tr.Count; q += 2) if (route != null && DistToPts(tr[q], tr[q + 1], route) <= 3) near++;
                    done++;
                    if (near >= 0.8 * (tr.Count / 2.0)) onNew++;
                }
            }
            double perDay = (double)departures / DaysAfter * (done > 0 ? (double)onNew / done : 0);
            predicted += pv.ReroutedPerDay; measured += perDay;
            // Where the predicted route leaves the old one, is there new trail within 3 cells?
            int pts = 0, nearPts = 0;
            foreach (var c in pv.Changes.Where(x => x.Reachable))
                for (int q = 0; q < c.NewPts.Length; q += 2)
                {
                    if (DistToPts(c.NewPts[q], c.NewPts[q + 1], c.OldPts) <= 3) continue;
                    pts++;
                    int cx = (int)Math.Floor(c.NewPts[q]), cy = (int)Math.Floor(c.NewPts[q + 1]);
                    bool ok = false;
                    for (int y = cy - 3; y <= cy + 3 && !ok; y++)
                        for (int x = cx - 3; x <= cx + 3 && !ok; x++)
                            if (w.Grid.Inside(x, y) && JsMath.Hypot(x - cx, y - cy) <= 3 && w.Wear[y * N + x] >= 25 && before[y * N + x] < 25) ok = true;
                    if (ok) nearPts++;
                }
            detourPts += pts; detourNear += nearPts;
            // Erase: hovering erase over the whole fence predicts shorter routes again.
            var pe = w.PreviewFlows(new WhatIf { Open = fence });
            if (pe.Changes.Exists(c => c.Reachable && c.CostRatio < 1)) eraseShorter++;
            string readout = FlowPredictor.Describe(pv, x => KindName(w.Buildings[x].Kind)).PadRight(13);
            if (readout.Length > 34) readout = readout[..34];
            log.Log($"{seed,4} | {fence.Length,5} | {pv.Changes.Count,5} | {readout} | {nearPts,4} of {pts,4}                 | {F((double)departures / DaysAfter, 1),5} {F(perDay, 1),10} | {F(pv.ReroutedPerDay, 1),8}");
        }
        double share = (double)detourNear / Math.Max(1, detourPts), err = measured != 0 ? predicted / measured - 1 : double.PositiveInfinity;
        log.Log($"  all seeds: {F(share * 100, 0)}% of predicted detour points lie within 3 cells of new trail; predicted {F(predicted, 1)} vs measured {F(measured, 1)} rerouted trips a day ({(err >= 0 ? "+" : "")}{F(err * 100, 0)}%)");
        log.Check(detourPts > 0 && share >= 0.8, "preview: at least 80% of the predicted detour lies within 3 cells of the trail that forms (3 days later)");
        log.Check(eraseShorter >= seeds.Length - 1, $"preview: erasing the fence 6 days later predicts shorter routes ({eraseShorter} of {seeds.Length} seeds)");
        log.Check(Math.Abs(err) <= 0.15, "preview: the readout is within 15% of the trips per day that take the new route (summed over seeds)");
    }
}
