using System.Text.Json;
using TideToll.Sim.Testing;
using Xunit.Abstractions;
using static TideToll.Sim.Testing.ToyHarness;

namespace TideToll.Sim.Tests;

/// <summary>
/// Exact parity with the toy (golden/toy-golden.json): maps, wear grids along a run, walker 0's first trip,
/// final stats and flow previews. The port keeps the toy's RNG, float32 storage and V8's hypot, so these
/// are compared bit for bit. The long traces (10 runs of 45 days) carry Category=Gate.
/// </summary>
public class ToyParityTests
{
    private readonly ITestOutputHelper _out;
    public ToyParityTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public void MapsMatchTheToyOnSeeds1To50()
    {
        int n = 0;
        foreach (var g in Golden.Root.GetProperty("maps").EnumerateArray())
        {
            int seed = g.GetProperty("seed").GetInt32();
            var m = MapGen.Generate(seed);
            var b = g.GetProperty("buildings").EnumerateArray().ToArray();
            Assert.Equal(b.Length, m.Buildings.Length);
            for (int i = 0; i < b.Length; i++)
            {
                var x = m.Buildings[i];
                Assert.Equal(new[] { b[i][0].GetInt32(), b[i][1].GetInt32(), b[i][2].GetInt32(), b[i][3].GetInt32(), b[i][4].GetInt32(), b[i][5].GetInt32() },
                    new[] { (int)x.Kind, x.X, x.Y, x.W, x.H, x.Door });
            }
            Assert.Equal(g.GetProperty("fordY").GetInt32(), m.FordY);
            Assert.Equal(g.GetProperty("riverHalfW").GetDouble(), m.RiverHalfW);
            Assert.Equal(g.GetProperty("water").GetUInt32(), Fnv<byte>(m.Water));
            Assert.Equal(g.GetProperty("ford").GetUInt32(), Fnv<byte>(m.Ford));
            Assert.Equal(g.GetProperty("crop").GetUInt32(), Fnv<byte>(m.Crop));
            Assert.Equal(g.GetProperty("footprint").GetUInt32(), Fnv<byte>(m.Footprint));
            Assert.Equal(g.GetProperty("bank").GetUInt32(), Fnv<sbyte>(m.Bank));
            Assert.Equal(g.GetProperty("noise").GetUInt32(), Fnv<float>(m.Noise));
            Assert.Equal(g.GetProperty("riverX").GetUInt32(), Fnv<float>(m.RiverX));
            n++;
        }
        Assert.Equal(50, n);
    }

    /// <summary>Runs one golden trace for <paramref name="days"/> days; returns a report line and whether every compared value matched.</summary>
    internal static (bool wearOk, string report) Trace(JsonElement t, int days)
    {
        int seed = t.GetProperty("seed").GetInt32(), walkers = t.GetProperty("walkers").GetInt32();
        var w = new World(seed, walkers);
        int tpd = w.Params.TicksPerDay;
        uint[] wearFnv = t.GetProperty("wearFnv").EnumerateArray().Select(e => e.GetUInt32()).ToArray();
        uint[] posFnv = t.GetProperty("posFnv").EnumerateArray().Select(e => e.GetUInt32()).ToArray();
        int[] firstTripGold = t.GetProperty("firstTrip").EnumerateArray().Select(e => e.GetInt32()).ToArray();
        var firstTrip = new List<int>();
        bool tripDone = false;
        int prev = -1, wearDiff = -1, posDiff = -1;
        var pos = new double[walkers * 2];
        for (int tk = 0; tk < days * tpd; tk++)
        {
            w.Tick();
            if (tk % tpd == tpd - 1)
            {
                int d = tk / tpd;
                if (wearDiff < 0 && Fnv(w.Wear) != wearFnv[d]) wearDiff = d + 1;
                for (int i = 0; i < walkers; i++) { pos[2 * i] = w.Walkers[i].X; pos[2 * i + 1] = w.Walkers[i].Y; }
                if (posDiff < 0 && Fnv<double>(pos) != posFnv[d]) posDiff = d + 1;
            }
            ref readonly var k = ref w.Walkers[0];
            if (!tripDone)
            {
                if (k.Cell != prev && k.Visible) firstTrip.Add(k.Cell);
                prev = k.Cell;
                if (k.State == WalkState.Home && firstTrip.Count > 1) tripDone = true;
            }
        }
        bool tripOk = firstTrip.SequenceEqual(firstTripGold);
        string report = $"seed {seed}, {walkers} walkers, {days} days: wear grid {(wearDiff < 0 ? "identical every day" : $"first differs on day {wearDiff}")}; " +
            $"walker positions {(posDiff < 0 ? "identical every day" : $"first differ on day {posDiff}")}; walker 0's first trip ({firstTripGold.Length} cells) {(tripOk ? "identical" : "differs")}";
        if (days == 45)
        {
            var f = t.GetProperty("final");
            var s = w.Stats();
            bool statsOk = s.DetourRatio == f.GetProperty("detourRatio").GetDouble() && s.TrunkShare == f.GetProperty("trunkShare").GetDouble()
                && s.TrailCoverage == f.GetProperty("trailCoverage").GetDouble() && s.TripsInWindow == f.GetProperty("tripsInWindow").GetInt32()
                && s.FirstTrailTime == f.GetProperty("firstTrailTime").GetDouble();
            report += $"; final stats {(statsOk ? "identical" : "differ")}";
            return (wearDiff < 0 && tripOk && statsOk, report);
        }
        return (wearDiff < 0 && tripOk, report);
    }

    [Fact]
    public void Seed1WearGridMatchesTheToyFor10Days()
    {
        var (ok, report) = Trace(Golden.Root.GetProperty("traces")[0], 10);
        _out.WriteLine(report);
        Assert.True(ok, report);
    }

    [Fact]
    [Trait("Category", "Gate")]
    public void AllGoldenTracesMatchTheToyFor45Days()
    {
        var traces = Golden.Root.GetProperty("traces").EnumerateArray().ToArray();
        var results = new (bool ok, string report)[traces.Length];
        Parallel.For(0, traces.Length, new ParallelOptions { MaxDegreeOfParallelism = 3 }, i => results[i] = Trace(traces[i], 45));
        foreach (var r in results) { _out.WriteLine(r.report); Console.WriteLine("TRACE " + r.report); }
        Assert.True(results.All(r => r.ok), string.Join("\n", results.Where(r => !r.ok).Select(r => r.report)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void FlowPreviewsMatchTheToyExactly(int which)
    {
        var g = Golden.Root.GetProperty("previews")[which];
        var w = new World(g.GetProperty("seed").GetInt32(), 200);
        Run(w, 20);
        // Same inputs first: if these match, the previews must match exactly (same Dijkstra, same trace).
        Assert.Equal(g.GetProperty("wearFnv").GetUInt32(), Fnv(w.Wear));
        Assert.Equal(g.GetProperty("costFnv").GetUInt32(), Fnv(w.Cost));
        Assert.Equal(g.GetProperty("tripRateFnv").GetUInt32(), Fnv(w.TripRate));
        var market = w.OfKind(BuildingKind.Market)[0];
        float[] route = new FlowPredictor(w).TraceRoute(market, w.Buildings[w.Houses[0]].Door);
        Assert.Equal(g.GetProperty("routeLen").GetInt32(), route.Length);
        Assert.Equal(g.GetProperty("routeFnv").GetUInt32(), Fnv<float>(route));
        int[] fence = FenceAcrossRoute(w, w.Houses[0], 0.3);
        Assert.Equal(g.GetProperty("fence").EnumerateArray().Select(e => e.GetInt32()), fence);
        AssertPreview(w, g.GetProperty("block"), w.PreviewFlows(new WhatIf { Block = fence }, 99));
        int[] cells = g.GetProperty("bridgeCells").EnumerateArray().Select(e => e.GetInt32()).ToArray();
        int row = g.GetProperty("bridgeRow").GetInt32();
        Assert.Equal(cells, w.BridgeCellsAt(w.RiverX[row], row));
        AssertPreview(w, g.GetProperty("bridge"), w.PreviewFlows(new WhatIf { Bridge = cells }));
        var ring = RingAround(w, w.Buildings[w.OfKind(BuildingKind.Well)[0]]).FindAll(w.CanFence).ToArray();
        Assert.Equal(g.GetProperty("ring").EnumerateArray().Select(e => e.GetInt32()), ring);
        AssertPreview(w, g.GetProperty("enclose"), w.PreviewFlows(new WhatIf { Block = ring }));
    }

    private void AssertPreview(World w, JsonElement g, FlowPreview p)
    {
        string describe = FlowPredictor.Describe(p, b => KindName(w.Buildings[b].Kind));
        _out.WriteLine($"toy: \"{g.GetProperty("describe").GetString()}\"  C#: \"{describe}\"");
        Assert.Equal(g.GetProperty("describe").GetString(), describe);
        Assert.Equal(g.GetProperty("reroutedPerDay").GetDouble(), p.ReroutedPerDay);
        Assert.Equal(g.GetProperty("more").GetInt32(), p.More);
        Assert.Equal(g.GetProperty("version").GetInt32(), p.Version);
        Assert.Equal(g.GetProperty("blocked").EnumerateArray().Select(e => (e[0].GetInt32(), e[1].GetInt32())), p.Blocked.Select(b => (b.Dest, b.Houses)));
        var changes = g.GetProperty("changes").EnumerateArray().ToArray();
        Assert.Equal(changes.Length, p.Changes.Count);
        for (int i = 0; i < changes.Length; i++)
        {
            var e = changes[i]; var c = p.Changes[i];
            Assert.Equal(e.GetProperty("home").GetInt32(), c.Home);
            Assert.Equal(e.GetProperty("dest").GetInt32(), c.Dest);
            Assert.Equal(e.GetProperty("rate").GetDouble(), c.Rate);
            Assert.Equal(e.GetProperty("reachable").GetBoolean(), c.Reachable);
            if (c.Reachable) Assert.Equal(e.GetProperty("costRatio").GetDouble(), c.CostRatio);
            Assert.Equal(e.GetProperty("oldLen").GetInt32(), c.OldPts.Length);
            Assert.Equal(e.GetProperty("newLen").GetInt32(), c.NewPts.Length);
            Assert.Equal(e.GetProperty("oldFnv").GetUInt32(), Fnv<float>(c.OldPts));
            Assert.Equal(e.GetProperty("newFnv").GetUInt32(), Fnv<float>(c.NewPts));
        }
    }
}
