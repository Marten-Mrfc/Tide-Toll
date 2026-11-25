using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime;

namespace TideToll.Sim.Testing;

/// <summary>
/// The hunt for the rare slow step (tools: <c>ticks --hunt</c>; fix round before the playtest, 25 Nov 2026): many towns run
/// one after another on one thread, as the game runs them, every tick timed in wall time and in the thread's CPU time, and
/// every tick over the limit printed with what ran in it: route searches of the field refresh (the urgent refresh after an
/// edit, a flood or the ice is several at once), the decay step, a new house, rain starting or stopping, a flood or the ice
/// starting or ending, a day or season boundary, the river's events, garbage collections, methods compiled by the JIT
/// during the tick (on this thread: a first call; on any thread: tiering too), and whether each of those was the first of
/// its kind in this hunt (one hunt a process in the tool). Between slow steps the hunt loop allocates nothing of its own.
/// </summary>
public static class SlowStepHunt
{
    public sealed class Options
    {
        public string Preset = "river";
        public int Walkers = 500, Days = 60, FirstSeed = 1, Towns = 10;
        public double OverMs = 15;
    }

    public sealed record Step(int Seed, long Tick, double Day, double WallMs, double CpuMs, string What);

    public sealed record Report(Options Opt, long Steps, List<Step> Slow, double WallMaxMs, double CpuMaxMs);

    public static Report Run(Options o, TextWriter? live = null)
    {
        var slow = new List<Step>();
        var seen = new HashSet<string>(); // kinds already seen in this hunt: the first of each is marked
        var seenEvents = new bool[64];   // river event kinds already seen (no string building between slow steps)
        var tags = new List<string>(32);
        var events = new List<RiverEvent>(512);
        long steps = 0;
        double wallMax = 0, cpuMax = 0;
        for (int town = 0; town < o.Towns; town++)
        {
            int seed = o.FirstSeed + town;
            var w = TickProfile.Make(seed, o.Walkers, o.Preset);
            var river = w.River;
            long n = (long)o.Days * w.Params.TicksPerDay;
            int tpd = w.Params.TicksPerDay, tps = w.Params.TicksPerSeason;
            double nextDecay = w.Params.DecayStepSimSec;
            bool flooded = river?.Flooded ?? false, frozen = river?.Frozen ?? false, raining = w.Weather.Raining;
            for (long i = 0; i < n; i++)
            {
                long s0 = w.FieldSearches, v0 = w.Version;
                int houses = w.Houses.Length;
                int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);
                long jitThread = System.Runtime.JitInfo.GetCompiledMethodCount(currentThread: true);
                long jitAll = System.Runtime.JitInfo.GetCompiledMethodCount(currentThread: false);
                long w0 = Stopwatch.GetTimestamp();
                double c0 = CpuClock.NowMs();
                w.Tick();
                double cpu = CpuClock.NowMs() - c0;
                double wall = (Stopwatch.GetTimestamp() - w0) * 1000.0 / Stopwatch.Frequency;
                steps++;
                wallMax = Math.Max(wallMax, wall);
                cpuMax = Math.Max(cpuMax, cpu);
                bool decay = w.Time >= nextDecay - 1e-9;
                if (decay) nextDecay += w.Params.DecayStepSimSec;
                // Read the state that changes every tick before deciding (so the comparison is always up to date).
                bool nowFlooded = river?.Flooded ?? false, nowFrozen = river?.Frozen ?? false, nowRaining = w.Weather.Raining;
                if (river != null) { events.Clear(); river.DrainEvents(events); }
                if (Math.Max(wall, cpu) > o.OverMs)
                {
                    tags.Clear();
                    long searches = w.FieldSearches - s0;
                    if (searches > 0) Tag(tags, seen, $"{searches} route searches", searches > 2 ? "many route searches" : "route search");
                    if (w.Version != v0) Tag(tags, seen, "edited", "edit");
                    if (decay) Tag(tags, seen, "decay step", "decay");
                    if (w.Houses.Length != houses) Tag(tags, seen, "NEW HOUSE", "house");
                    if (nowRaining != raining) Tag(tags, seen, nowRaining ? "rain starts" : "rain stops", nowRaining ? "rain starts" : "rain stops");
                    if (nowFlooded != flooded) Tag(tags, seen, nowFlooded ? "FLOOD STARTS" : "flood ends", nowFlooded ? "flood starts" : "flood ends");
                    if (nowFrozen != frozen) Tag(tags, seen, nowFrozen ? "FREEZE STARTS" : "thaw", nowFrozen ? "freeze" : "thaw");
                    if (w.Ticks % tpd == 0) Tag(tags, seen, $"day {w.Ticks / tpd} ends", "day boundary");
                    if (w.Ticks % tps == 0) Tag(tags, seen, "SEASON BOUNDARY", "season boundary");
                    foreach (var e in events)
                    {
                        bool first = !seenEvents[(int)e.Kind];
                        seenEvents[(int)e.Kind] = true;
                        tags.Add(first ? e.Kind + " (FIRST in this hunt)" : e.Kind.ToString());
                    }
                    int dg0 = GC.CollectionCount(0) - g0, dg1 = GC.CollectionCount(1) - g1, dg2 = GC.CollectionCount(2) - g2;
                    if (dg0 + dg1 + dg2 > 0) Tag(tags, seen, $"GC gen0 {dg0} gen1 {dg1} gen2 {dg2}", "gc");
                    long jt = System.Runtime.JitInfo.GetCompiledMethodCount(currentThread: true) - jitThread;
                    long ja = System.Runtime.JitInfo.GetCompiledMethodCount(currentThread: false) - jitAll;
                    if (jt > 0) Tag(tags, seen, $"JIT {jt} methods compiled on this thread (first calls)", "jit");
                    else if (ja > 0) Tag(tags, seen, $"JIT {ja} methods compiled on other threads (tiering)", "jit other");
                    if (town == 0 && i < 600) tags.Add("warm-up (first 600 ticks of the process's first town)");
                    var step = new Step(seed, w.Ticks, w.Ticks / (double)tpd, wall, cpu, string.Join(", ", tags));
                    slow.Add(step);
                    live?.WriteLine(Line(step, w.WalkerCount));
                }
                else
                {
                    // Still note the firsts, so a slow step is only called "first" if it really was.
                    if (w.Houses.Length != houses) seen.Add("house");
                    if (nowFlooded != flooded) seen.Add(nowFlooded ? "flood starts" : "flood ends");
                    if (nowFrozen != frozen) seen.Add(nowFrozen ? "freeze" : "thaw");
                    if (nowRaining != raining) seen.Add(nowRaining ? "rain starts" : "rain stops");
                    if (decay) seen.Add("decay");
                    if (w.Ticks % tps == 0) seen.Add("season boundary");
                    if (w.Ticks % tpd == 0) seen.Add("day boundary");
                    if (w.Version != v0) seen.Add("edit");
                    long searches = w.FieldSearches - s0;
                    if (searches > 0) seen.Add(searches > 2 ? "many route searches" : "route search");
                    foreach (var e in events) seenEvents[(int)e.Kind] = true;
                }
                flooded = nowFlooded; frozen = nowFrozen; raining = nowRaining;
            }
            live?.WriteLine($"  town {seed} done: {n} steps; {slow.Count} slow so far over {steps} steps");
        }
        return new Report(o, steps, slow, wallMax, cpuMax);
    }

    private static void Tag(List<string> tags, HashSet<string> seen, string text, string kind)
        => tags.Add(seen.Add(kind) ? text + " (FIRST in this hunt)" : text);

    public static string Line(Step s, int walkers)
        => $"  SLOW seed {s.Seed} tick {s.Tick,6} (day {s.Day:F2}): wall {s.WallMs,6:F2} ms, thread CPU {s.CpuMs,6:F2} ms, {walkers} walkers: {s.What}";

    public static string Summary(Report r)
        => $"HUNT {r.Opt.Preset}, {r.Opt.Walkers} walkers, seeds {r.Opt.FirstSeed}-{r.Opt.FirstSeed + r.Opt.Towns - 1}, {r.Opt.Days} days each: {r.Steps} steps, "
           + $"{r.Slow.Count} over {r.Opt.OverMs} ms (wall or thread CPU); worst wall {r.WallMaxMs:F2} ms, worst thread CPU {r.CpuMaxMs:F2} ms; "
           + $"GC mode {(GCSettings.IsServerGC ? "server" : "workstation")}, {GCSettings.LatencyMode}";
}
