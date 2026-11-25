using System.Text.Json;
using TideToll.Sim.Testing;
using Xunit.Abstractions;
using static TideToll.Sim.Testing.ToyHarness;

namespace TideToll.Sim.Tests;

/// <summary>
/// House growth by the frontage rules (toy round 4: growth.ts and world.ts) on SimParams.Toy with GrowthOn: the plot
/// search (every candidate counted, the best eight with exact scores and the way-open check, the chosen plot) on seeds 1
/// and 2 at days 3 and 9; the traces with growth alone and with rain and growth (houses, walkers, the grown list, wear,
/// costs, mud and weather every day); the edits at fixed ticks and the flow previews in a live town with grown houses;
/// and a tick with growth active allocates nothing. Every expected value comes from the toy (toy-golden-v2.json).
/// </summary>
public class ToyV2GrowthTests
{
    private readonly ITestOutputHelper _out;
    public ToyV2GrowthTests(ITestOutputHelper output) => _out = output;

    internal static readonly SimParams Growth = SimParams.Toy with { GrowthOn = true };
    internal static readonly SimParams Live = SimParams.Toy with { RainOn = true, GrowthOn = true };

    [Fact]
    public void ThePlotSearchMatchesTheToy()
    {
        var plots = Golden2.Root.GetProperty("plots").EnumerateArray().ToArray();
        World? w = null;
        foreach (var g in plots)
        {
            int seed = g.GetProperty("seed").GetInt32();
            if (w == null || w.Seed != seed) w = new World(seed, 200, Live);
            while (w.Ticks < g.GetProperty("ticks").GetInt64()) w.Tick();
            string at = $"seed {seed} day {g.GetProperty("day").GetInt32()}";
            Assert.True(g.GetProperty("wearFnv").GetUInt32() == Fnv(w.Wear), $"{at}: wear differs before the search");
            Assert.Equal(g.GetProperty("houses").GetInt32(), w.Houses.Length);
            var (candidates, top, chosen) = w.PlotSearch();
            _out.WriteLine($"{at}: {candidates} candidates, best {top.FirstOrDefault().Plot.Score:R}");
            Assert.Equal(g.GetProperty("candidates").GetInt32(), candidates);
            var gt = g.GetProperty("top").EnumerateArray().ToArray();
            Assert.Equal(gt.Length, top.Length);
            for (int i = 0; i < gt.Length; i++)
            {
                var e = gt[i]; var (p, keeps) = top[i];
                Assert.True(e.GetProperty("x").GetInt32() == p.X && e.GetProperty("y").GetInt32() == p.Y && e.GetProperty("door").GetInt32() == p.Door
                    && e.GetProperty("front").GetInt32() == p.Front, $"{at}: plot {i} at ({p.X},{p.Y}) door {p.Door}");
                Assert.Equal(Golden2.I(e.GetProperty("fronted")), p.Fronted());
                Assert.True(e.GetProperty("score").GetDouble() == p.Score, $"{at}: plot {i} score {p.Score:R}, toy {e.GetProperty("score").GetDouble():R}");
                Assert.Equal(e.GetProperty("keeps").GetBoolean(), keeps);
            }
            var c = g.GetProperty("chosen");
            if (c.ValueKind == JsonValueKind.Null) Assert.Null(chosen);
            else
            {
                Assert.NotNull(chosen);
                Assert.True(c.GetProperty("x").GetInt32() == chosen!.Value.X && c.GetProperty("y").GetInt32() == chosen.Value.Y
                    && c.GetProperty("door").GetInt32() == chosen.Value.Door && c.GetProperty("score").GetDouble() == chosen.Value.Score, $"{at}: chosen plot");
            }
        }
    }

    /// <summary>Seed 1 at 200 walkers, growth alone, day by day for 10 days (houses on days 3, 5, 7 and 9).</summary>
    [Fact]
    public void Seed1WithGrowthMatchesTheToyDayByDayFor10Days() => Trace("tracesGrowth", 0, 10, Growth);

    /// <summary>Seed 1 at 200 walkers, rain and growth, day by day for 10 days.</summary>
    [Fact]
    public void Seed1LiveMatchesTheToyDayByDayFor10Days() => Trace("tracesLive", 0, 10, Live);

    /// <summary>Seed 1 at 200 walkers, rain and growth: the wear after every tick of the first two days.</summary>
    [Fact]
    public void Seed1LiveMatchesTheToyTickByTickForTwoDays()
    {
        var t = Golden2.Root.GetProperty("tracesLive")[0];
        uint[] gold = Golden2.U(t.GetProperty("tickWearFnv"));
        var w = new World(1, 200, Live);
        for (int tick = 0; tick < gold.Length; tick++)
        {
            w.Tick();
            Assert.True(gold[tick] == Fnv(w.Wear), $"wear first differs after tick {tick + 1} (time {w.Time:R})");
        }
    }

    private void Trace(string section, int which, int days, SimParams p)
    {
        var (ok, report) = ToyV2ParityTests.TraceV2(Golden2.Root.GetProperty(section)[which], days, p);
        _out.WriteLine(report);
        Assert.True(ok, report);
    }

    [Fact]
    [Trait("Category", "Gate")]
    public void AllGrowthAndLiveTracesMatchTheToyFor45Days()
    {
        var jobs = Golden2.Root.GetProperty("tracesGrowth").EnumerateArray().Select(t => (t, Growth))
            .Concat(Golden2.Root.GetProperty("tracesLive").EnumerateArray().Select(t => (t, Live))).ToArray();
        var results = new (bool ok, string report)[jobs.Length];
        Parallel.For(0, jobs.Length, new ParallelOptions { MaxDegreeOfParallelism = 3 }, i => results[i] = ToyV2ParityTests.TraceV2(jobs[i].t, 45, jobs[i].Item2));
        foreach (var r in results) { _out.WriteLine(r.report); Console.WriteLine("TRACE " + r.report); }
        Assert.True(results.All(r => r.ok), string.Join("\n", results.Where(r => !r.ok).Select(r => r.report)));
    }

    /// <summary>The edits at fixed ticks in a live town (rain and growth): every tool outcome and hash every 50 ticks.</summary>
    [Fact]
    public void EditsInALiveTownMatchTheToy()
    {
        var g = Golden2.Root.GetProperty("edits")[1];
        Assert.True(g.GetProperty("opts").GetProperty("growth").GetBoolean());
        ToyV2ToolTests.ReplayEdits(g, Live);
    }

    /// <summary>Seed 2 at day 20 with 16 houses (9 grown), rain on: every preview as the toy's.</summary>
    [Fact]
    public void PreviewsInALiveTownMatchTheToy()
    {
        var g = Golden2.Root.GetProperty("previewsLive")[0];
        ToyV2PreviewTests.PreviewSet(g, Live, _out.WriteLine);
    }

    /// <summary>Seed 1 at 200 walkers with rain and growth: ticks while houses arrive (field rebuilds, plot searches, new villagers) allocate nothing.</summary>
    [Fact]
    public void TicksWithGrowthActiveAllocateNothing()
    {
        var w = new World(1, 200, Live);
        Run(w, 2.2); // the first household arrived at 30 s; the search, the cottage and its fields have run once
        int houses = w.Houses.Length;
        Assert.True(w.Grown.Length >= 1);
        long a0 = GC.GetAllocatedBytesForCurrentThread();
        Run(w, 4); // two more households (days 5 and 7), showers and mud
        w.Stats();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - a0;
        Assert.True(w.Houses.Length >= houses + 2, $"houses {houses} -> {w.Houses.Length}");
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void GrowthIsOffWithTheRiverRulesAndKeepsItsOwnStream()
    {
        var river = new World(1, 200, Live with { River = RiverRules.Default });
        Assert.False(river.GrowthOn);
        Run(river, 3);
        Assert.Empty(river.Grown.ToArray());
        Assert.Equal(World.GrowthStreamSeed(1), (uint)((1L * 40503) ^ 0x6f7d));
    }
}
