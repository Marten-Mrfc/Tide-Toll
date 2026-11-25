using System.Text.Json;
using TideToll.Sim.Testing;
using Xunit.Abstractions;
using static TideToll.Sim.Testing.ToyHarness;

namespace TideToll.Sim.Tests;

/// <summary>
/// Rain and mud (toy round 4: weather.ts and world.ts) on SimParams.Toy with RainOn, house growth off: the weather alone
/// on 50 seeds for 120 days (its own stream, two draws a day), the traces with rain on and growth off (wear, flow, both
/// costs, mud, weather every day for 45 days), and the edits at fixed ticks with rain on (mud follows each edit at once;
/// the forced shower and its stop). Every expected value comes from the toy (toy-golden-v2.json).
/// </summary>
public class ToyV2RainTests
{
    private readonly ITestOutputHelper _out;
    public ToyV2RainTests(ITestOutputHelper output) => _out = output;

    private static readonly SimParams Rain = SimParams.Toy with { RainOn = true };

    private static uint FnvF64(List<double> v) => Fnv<double>(v.ToArray());

    [Fact]
    public void TheWeatherAloneMatchesTheToyOn50SeedsFor120Days()
    {
        var p = SimParams.Toy;
        int tpd = p.TicksPerDay;
        foreach (var g in Golden2.Root.GetProperty("weather").EnumerateArray())
        {
            int seed = g.GetProperty("seed").GetInt32();
            var wx = new RainWeather(seed, p);
            double t = 0;
            var all = new List<double>();
            var day = new List<double>();
            var days = new List<uint>();
            var starts = new List<int>();
            for (int tick = 0; tick < g.GetProperty("days").GetInt32() * tpd; tick++)
            {
                t += 1.0 / p.TickHz;
                if (wx.Update(t)) starts.Add(tick);
                double[] v = { wx.Rain, wx.Wetness, wx.Raining ? 1 : 0, wx.MudUntil };
                all.AddRange(v); day.AddRange(v);
                if (tick % tpd == tpd - 1) { days.Add(FnvF64(day)); day.Clear(); }
            }
            Assert.True(Golden2.I(g.GetProperty("rainStarts")).SequenceEqual(starts), $"seed {seed}: rain starts [{string.Join(",", starts)}]");
            Assert.Equal(g.GetProperty("rainDays").GetInt32(), wx.RainDays);
            if (g.TryGetProperty("dayFnv", out var dg))
            {
                uint[] gold = Golden2.U(dg);
                for (int d = 0; d < gold.Length; d++) Assert.True(gold[d] == days[d], $"seed {seed}: weather first differs on day {d + 1}");
            }
            Assert.True(g.GetProperty("fnv").GetUInt32() == FnvF64(all), $"seed {seed}: the weather's 120-day hash differs");
        }
    }

    /// <summary>Seed 1 at 200 walkers with rain on, day by day for 12 days (its first showers fall on days 6 and 7).</summary>
    [Fact]
    public void Seed1WithRainMatchesTheToyDayByDayFor12Days()
    {
        var (ok, report) = ToyV2ParityTests.TraceV2(Golden2.Root.GetProperty("tracesRain")[0], 12, Rain);
        _out.WriteLine(report);
        Assert.True(ok, report);
    }

    [Fact]
    [Trait("Category", "Gate")]
    public void AllRainTracesMatchTheToyFor45Days()
    {
        var traces = Golden2.Root.GetProperty("tracesRain").EnumerateArray().ToArray();
        var results = new (bool ok, string report)[traces.Length];
        Parallel.For(0, traces.Length, new ParallelOptions { MaxDegreeOfParallelism = 3 }, i => results[i] = ToyV2ParityTests.TraceV2(traces[i], 45, Rain));
        foreach (var r in results) { _out.WriteLine(r.report); Console.WriteLine("TRACE " + r.report); }
        Assert.True(results.All(r => r.ok), string.Join("\n", results.Where(r => !r.ok).Select(r => r.report)));
    }

    /// <summary>The edits at fixed ticks with rain on: mud follows every edit at once, and the forced shower (tick 1000) and its stop with the ground dried (1200).</summary>
    [Fact]
    public void EditsWithRainMatchTheToy()
    {
        var g = Golden2.Root.GetProperty("edits")[2];
        Assert.True(g.GetProperty("opts").GetProperty("rain").GetBoolean());
        Assert.False(g.GetProperty("opts").GetProperty("growth").GetBoolean());
        ToyV2ToolTests.ReplayEdits(g, Rain);
    }

    [Fact]
    public void RainIsOffWithTheRiverRulesAndNeverDrawsFromTheWalkersStream()
    {
        var river = new World(1, 200, Rain with { River = RiverRules.Default });
        Assert.False(river.RainOn);
        river.StartRain();
        Assert.False(river.Weather.Raining);
        // Rain on or off, the walkers' stream is the same until the first shower changes where they walk.
        var a = new World(3, 200, SimParams.Toy);
        var b = new World(3, 200, Rain);
        for (int i = 0; i < 60; i++) { a.Tick(); b.Tick(); }
        Assert.Equal(a.RngState, b.RngState);
        Assert.Equal(Fnv(a.Wear), Fnv(b.Wear));
    }
}
