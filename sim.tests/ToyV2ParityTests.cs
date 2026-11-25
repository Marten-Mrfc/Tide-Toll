using System.Text.Json;
using TideToll.Sim.Testing;
using Xunit.Abstractions;
using static TideToll.Sim.Testing.ToyHarness;

namespace TideToll.Sim.Tests;

/// <summary>The finished toy's comparison data (sim.tests/golden/toy-golden-v2.json; see golden/export.mjs), loaded once.</summary>
public static class Golden2
{
    private static readonly Lazy<JsonElement> Data = new(() =>
    {
        string local = Path.Combine(AppContext.BaseDirectory, "golden", "toy-golden-v2.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(local));
        return doc.RootElement.Clone();
    });
    public static JsonElement Root => Data.Value;

    internal static uint[] U(JsonElement e) => e.EnumerateArray().Select(x => x.GetUInt32()).ToArray();
    internal static int[] I(JsonElement e) => e.EnumerateArray().Select(x => x.GetInt32()).ToArray();
    /// <summary>A number the exporter wrote; non-finite ones are strings ("Infinity").</summary>
    internal static double D(JsonElement e) => e.ValueKind == JsonValueKind.String
        ? e.GetString() switch { "Infinity" => double.PositiveInfinity, "-Infinity" => double.NegativeInfinity, _ => double.NaN }
        : e.GetDouble();
}

/// <summary>
/// The trail model of the finished toy (rounds 3 to 5), SimParams.Toy, against the toy run with rain and house growth off
/// (they are not ported yet, so the C# world is the toy's "off"): parameters, maths probes and lookup tables, the new
/// random streams, the bucket-queue Dijkstra's values, routeLength and lineCost, and runs compared tick by tick and day by
/// day. Every expected value comes from the toy (golden/export-toy-golden-v2.ts). The 45-day traces and the gate table
/// carry Category=Gate.
/// </summary>
public class ToyV2ParityTests
{
    private readonly ITestOutputHelper _out;
    public ToyV2ParityTests(ITestOutputHelper output) => _out = output;

    private static readonly SimParams Toy = SimParams.Toy;

    [Fact]
    public void TheToyPresetHasTheFinishedToysParametersAndLegacyTheRoundTwoValues()
    {
        var g = Golden2.Root.GetProperty("params");
        var p = Toy;
        var pairs = new (string Name, double Toy, double Ours)[]
        {
            ("wearPerStep", g.GetProperty("wearPerStep").GetDouble(), p.WearPerStep), ("sideShare", g.GetProperty("sideShare").GetDouble(), p.SideShare),
            ("minSurface", g.GetProperty("minSurface").GetDouble(), p.MinSurface), ("fordCostMul", g.GetProperty("fordCostMul").GetDouble(), p.FordCostMul),
            ("explorerShare", g.GetProperty("explorerShare").GetDouble(), p.ExplorerShare), ("frugalDisc", g.GetProperty("frugalDisc").GetDouble(), p.FrugalDisc),
            ("detourCap", g.GetProperty("detourCap").GetDouble(), p.DetourCap), ("bridgeFlow", g.GetProperty("bridgeFlow").GetDouble(), p.BridgeFlow),
            ("gravelCost", g.GetProperty("gravelCost").GetDouble(), p.GravelCost), ("gravelSpeedMul", g.GetProperty("gravelSpeedMul").GetDouble(), p.GravelSpeedMul),
            ("gravelFloor", g.GetProperty("gravelFloor").GetDouble(), p.GravelFloor), ("gateCost", g.GetProperty("gateCost").GetDouble(), p.GateCost),
            ("gateSpeedMul", g.GetProperty("gateSpeedMul").GetDouble(), p.GateSpeedMul), ("walkSpeed", g.GetProperty("walkSpeed").GetDouble(), p.WalkSpeedCellsPerSec),
            ("dwellSimSec", g.GetProperty("dwellSimSec").GetDouble(), p.DwellSimSec), ("trailDiscount", g.GetProperty("trailDiscount").GetDouble(), p.TrailDiscount),
            ("discScale", g.GetProperty("discScale").GetDouble(), p.DiscScale), ("discPow", g.GetProperty("discPow").GetDouble(), p.DiscPow),
            ("flowDiscount", g.GetProperty("flowDiscount").GetDouble(), p.FlowDiscount), ("flowScale", g.GetProperty("flowScale").GetDouble(), p.FlowScale),
            ("flowHalfLife", g.GetProperty("flowHalfLife").GetDouble(), p.FlowHalfLife), ("halfLifeLow", g.GetProperty("halfLifeLow").GetDouble(), p.HalfLifeLow),
            ("halfLifeMid", g.GetProperty("halfLifeMid").GetDouble(), p.HalfLifeMid), ("halfLifeHigh", g.GetProperty("halfLifeHigh").GetDouble(), p.HalfLifeHigh),
            ("tasteNoise", g.GetProperty("tasteNoise").GetDouble(), p.TasteNoise), ("stepNoise", g.GetProperty("stepNoise").GetDouble(), p.StepNoise),
            ("staticNoise", g.GetProperty("staticNoise").GetDouble(), p.StaticNoise), ("cropPenalty", g.GetProperty("cropPenalty").GetDouble(), p.CropPenalty),
            ("lookahead", g.GetProperty("lookahead").GetDouble(), p.Lookahead), ("shortcutSlack", g.GetProperty("shortcutSlack").GetDouble(), p.ShortcutSlack),
            ("fordSpeedMul", g.GetProperty("fordSpeedMul").GetDouble(), p.FordSpeedMul), ("bridgeCost", g.GetProperty("bridgeCost").GetDouble(), p.BridgeCost),
            ("bridgeSpeedMul", g.GetProperty("bridgeSpeedMul").GetDouble(), p.BridgeSpeedMul), ("fieldRefreshSimSec", g.GetProperty("fieldRefreshSimSec").GetDouble(), p.FieldRefreshSimSec),
            ("crowdAvgSimSec", g.GetProperty("crowdAvgSimSec").GetDouble(), p.CrowdAvgSimSec), ("weidmannGamma", g.GetProperty("weidmannGamma").GetDouble(), p.WeidmannGamma),
            ("tripRateDays", g.GetProperty("tripRateDays").GetDouble(), p.TripRateDays), ("trunkTopShare", g.GetProperty("trunkTopShare").GetDouble(), p.TrunkTopShare),
        };
        Assert.All(pairs, x => Assert.True(x.Toy == x.Ours, $"{x.Name}: toy {x.Toy}, SimParams.Toy {x.Ours}"));
        var l = Golden2.Root.GetProperty("legacyParams");
        var q = SimParams.Legacy;
        Assert.Equal(l.GetProperty("wearPerStep").GetDouble(), q.WearPerStep);
        Assert.Equal(l.GetProperty("sideShare").GetDouble(), q.SideShare);
        Assert.Equal(l.GetProperty("minSurface").GetDouble(), q.MinSurface);
        Assert.Equal(l.GetProperty("fordCostMul").GetDouble(), q.FordCostMul);
        Assert.Equal(l.GetProperty("explorerShare").GetDouble(), q.ExplorerShare);
        Assert.Equal(l.GetProperty("detourCap").GetDouble(), q.DetourCap);
        Assert.Equal(l.GetProperty("bridgeFlow").GetDouble(), q.BridgeFlow);
        Assert.False(q.UseFrugal);
        Assert.True(Toy.UseFrugal);
        Assert.True(Golden2.Root.GetProperty("legacyMatches").GetBoolean(), "the exporter found the finished toy, run with the round-2 values, different from the legacy traces");
    }

    /// <summary>
    /// The maths on results paths computed here, beside the toy's (V8's) values: the decay factors (pow), the trip-rate decay,
    /// the crowding and walking-speed exp (Weidmann), plain exp, sin and pow samples, and the river's sine (riverX hash).
    /// </summary>
    private static List<(string What, double Ours, double Toy)> Probes()
    {
        var g = Golden2.Root.GetProperty("probes");
        var p = Toy;
        var l = new List<(string, double, double)>();
        var pows = g.GetProperty("pows");
        foreach (var (name, half) in new[] { ("low", p.HalfLifeLow), ("mid", p.HalfLifeMid), ("high", p.HalfLifeHigh), ("hollow", p.HalfLifeHollow), ("flow", p.FlowHalfLife) })
            l.Add(($"decay factor {name}: pow(0.5, 1/{half})", Math.Pow(0.5, 1 / half), pows.GetProperty(name).GetDouble()));
        l.Add(("trip-rate decay exp", Math.Exp(-(1.0 / p.TickHz) / (p.TripRateDays * p.DaySimSec)), g.GetProperty("tripDecay").GetDouble()));
        foreach (var e in g.GetProperty("crowdExp").EnumerateArray())
        {
            double occ = e.GetProperty("occ").GetDouble(), rho = (float)occ / p.CellAreaM2;
            l.Add(($"crowd exp at occ {occ:R}", Math.Exp(-p.WeidmannGamma * (1 / rho - 1 / p.WeidmannRhoMax)), e.GetProperty("value").GetDouble()));
        }
        foreach (var e in g.GetProperty("speedExp").EnumerateArray())
        {
            double rho = e.GetProperty("here").GetInt32() / p.CellAreaM2;
            l.Add(($"speed exp at {e.GetProperty("here").GetInt32()} walkers", Math.Exp(-p.WeidmannGamma * (1 / rho - 1 / p.WeidmannRhoMax)), e.GetProperty("value").GetDouble()));
        }
        foreach (var (name, f) in new (string, Func<double, double>)[] { ("exp", Math.Exp), ("sin", Math.Sin), ("pow", x => Math.Pow(x, p.DiscPow)) })
            foreach (var e in g.GetProperty(name).EnumerateArray())
            {
                double x = e.GetProperty("x").GetDouble();
                l.Add(($"{name}({x:R})", f(x), e.GetProperty("value").GetDouble()));
            }
        return l;
    }

    /// <summary>
    /// What the simulation stores must match the toy on this machine: both lookup tables (float32) bit for bit, the river's
    /// sine as stored (riverX, float32), and every maths probe once rounded to float32, the precision results are kept at.
    /// The bit-for-bit comparison of the doubles is <see cref="MathsProbesMatchV8BitForBit"/> (Gate).
    /// </summary>
    [Fact]
    public void LookupTablesMatchTheToyAndTheMathsProbesAgreeAtStoredPrecision()
    {
        var g = Golden2.Root.GetProperty("probes");
        var w = new World(1, 0, Toy);
        Assert.Equal(g.GetProperty("wearLutLen").GetInt32(), w.WearLookup.Length);
        Assert.Equal(g.GetProperty("flowLutLen").GetInt32(), w.FlowLookup.Length);
        Assert.Equal(g.GetProperty("wearLutFnv").GetUInt32(), Fnv(w.WearLookup));
        Assert.Equal(g.GetProperty("flowLutFnv").GetUInt32(), Fnv(w.FlowLookup));
        foreach (var e in g.GetProperty("riverXFnv").EnumerateArray())
            Assert.True(Fnv<float>(MapGen.Generate(e.GetProperty("seed").GetInt32()).RiverX) == e.GetProperty("value").GetUInt32(), $"riverX (sin) of seed {e.GetProperty("seed").GetInt32()}");
        var bad = Probes().Where(x => (float)x.Ours != (float)x.Toy).Select(x => $"{x.What} = {x.Ours:R} against {x.Toy:R}").ToList();
        Assert.True(bad.Count == 0, $"{bad.Count} maths probes differ from V8's even at float32:\n" + string.Join("\n", bad));
    }

    /// <summary>
    /// Every maths probe bit for bit against V8 (the toy). On this machine (Linux x64, .NET 8, 25 Nov 2026) about one probe
    /// in ten differs in the last bit of the double (pow(0.5, 1/20), some exp and pow samples): results still match because
    /// they are stored as float32, but walker positions (doubles) drift by an ulp, and a long run could one day round a
    /// stored value the other way. This test fails honestly while that is so; a portable exp/pow (fdlibm) would fix it.
    /// </summary>
    [Fact]
    [Trait("Category", "Gate")]
    public void MathsProbesMatchV8BitForBit()
    {
        var all = Probes();
        var bad = all.Where(x => x.Ours != x.Toy).Select(x => $"{x.What} = {x.Ours:R} against {x.Toy:R}").ToList();
        foreach (var b in bad) _out.WriteLine(b);
        Assert.True(bad.Count == 0, $"{bad.Count} of {all.Count} maths probes differ from V8's in the last bit:\n" + string.Join("\n", bad));
    }

    [Fact]
    public void TheWeatherAndGrowthStreamsAndTheExplorerHashMatchTheToy()
    {
        foreach (var e in Golden2.Root.GetProperty("streams").EnumerateArray())
        {
            int seed = e.GetProperty("seed").GetInt32();
            // The toy multiplies in doubles and converts with ToInt32; exact for seeds below about 3.4 million.
            uint ws = unchecked((uint)((long)seed * 2654435761L) ^ 0x5eedu), gs = unchecked((uint)(seed * 40503) ^ 0x6f7du);
            Assert.Equal(e.GetProperty("weatherSeed").GetUInt32(), ws);
            Assert.Equal(e.GetProperty("growthSeed").GetUInt32(), gs);
            var a = new Mulberry32(ws); var b = new Mulberry32(gs);
            foreach (var v in e.GetProperty("weather").EnumerateArray()) Assert.Equal(v.GetDouble(), a.NextDouble());
            foreach (var v in e.GetProperty("growth").EnumerateArray()) Assert.Equal(v.GetDouble(), b.NextDouble());
        }
        var ex = Golden2.Root.GetProperty("explorers").EnumerateArray().Select(x => x.GetDouble()).ToArray();
        for (int id = 0; id < ex.Length; id++) Assert.Equal(ex[id], Hash.Hash2(id, 77));
        // Walkers are explorers by that hash, with no random draw.
        var w = new World(1, 64, Toy);
        for (int i = 0; i < 64; i++) Assert.Equal(ex[w.Walkers[i].Id] < Toy.ExplorerShare, w.Walkers[i].Explorer);
    }

    [Fact]
    public void TheBucketQueueFieldRouteLengthAndLineCostMatchTheToy()
    {
        var g = Golden2.Root.GetProperty("dijkstraLow");
        var grid = new Grid(128, 128);
        var cost = new float[grid.Count];
        var r = new Mulberry32(11);
        for (int i = 0; i < cost.Length; i++) cost[i] = (float)(0.3 + 0.5 * r.NextDouble());
        for (int x = 0; x < 100; x++) cost[70 * 128 + x] = float.PositiveInfinity;
        var field = new float[grid.Count];
        CostField.Dijkstra(grid, 100 * 128 + 20, cost, field, new BinaryHeap(1024));
        // The toy's bucket queue settles in a different order but must give exactly the heap's values.
        Assert.Equal(g.GetProperty("fieldFnv").GetUInt32(), Fnv<float>(field));
        Assert.Equal(g.GetProperty("infCount").GetInt32(), field.Count(float.IsPositiveInfinity));
        foreach (var e in g.GetProperty("probes").EnumerateArray())
            Assert.Equal(Golden2.D(e.GetProperty("value")), field[e.GetProperty("y").GetInt32() * 128 + e.GetProperty("x").GetInt32()]);
        foreach (var e in g.GetProperty("routes").EnumerateArray())
            Assert.Equal(Golden2.D(e.GetProperty("length")), CostField.RouteLength(grid, field, cost, e.GetProperty("y").GetInt32() * 128 + e.GetProperty("x").GetInt32(), 100 * 128 + 20));
        foreach (var e in g.GetProperty("lines").EnumerateArray())
        {
            double limit = e.GetProperty("limit").GetDouble();
            double v = CostField.LineCost(grid, cost, e.GetProperty("x0").GetDouble(), e.GetProperty("y0").GetDouble(), e.GetProperty("x1").GetDouble(), e.GetProperty("y1").GetDouble(),
                limit < 0 ? double.PositiveInfinity : limit);
            Assert.Equal(e.GetProperty("value").GetDouble(), double.IsPositiveInfinity(v) ? -1 : v);
        }
    }

    /// <summary>Seed 1, 200 walkers: the wear grid after every tick of the first two days (a divergence is found at its first tick).</summary>
    [Fact]
    public void Seed1MatchesTheToyTickByTickForTwoDays()
    {
        var t = Golden2.Root.GetProperty("tracesOff")[0];
        uint[] gold = Golden2.U(t.GetProperty("tickWearFnv"));
        var w = new World(1, 200, Toy);
        for (int tick = 0; tick < gold.Length; tick++)
        {
            w.Tick();
            Assert.True(gold[tick] == Fnv(w.Wear), $"wear first differs after tick {tick + 1} (time {w.Time:R})");
        }
    }

    [Fact]
    public void Seed1MatchesTheToyDayByDayFor10Days()
    {
        var (ok, report) = TraceV2(Golden2.Root.GetProperty("tracesOff")[0], 10);
        _out.WriteLine(report);
        Assert.True(ok, report);
    }

    [Fact]
    [Trait("Category", "Gate")]
    public void AllOffTracesMatchTheToyFor45Days()
    {
        var traces = Golden2.Root.GetProperty("tracesOff").EnumerateArray().ToArray();
        var results = new (bool ok, string report)[traces.Length];
        Parallel.For(0, traces.Length, new ParallelOptions { MaxDegreeOfParallelism = 3 }, i => results[i] = TraceV2(traces[i], 45));
        foreach (var r in results) { _out.WriteLine(r.report); Console.WriteLine("TRACE " + r.report); }
        Assert.True(results.All(r => r.ok), string.Join("\n", results.Where(r => !r.ok).Select(r => r.report)));
    }

    /// <summary>The finished code on the Legacy preset gives the finished toy's own run with the round-2 values.</summary>
    [Fact]
    [Trait("Category", "Gate")]
    public void TheLegacyPresetMatchesTheFinishedToyRunWithRoundTwoValues()
    {
        foreach (var c in Golden2.Root.GetProperty("legacyCheck").EnumerateArray())
        {
            var w = new World(c.GetProperty("seed").GetInt32(), c.GetProperty("walkers").GetInt32(), SimParams.Legacy);
            uint[] gold = Golden2.U(c.GetProperty("wearFnv"));
            for (int d = 0; d < gold.Length; d++) { Run(w, 1); Assert.True(gold[d] == Fnv(w.Wear), $"seed {c.GetProperty("seed").GetInt32()}: wear differs on day {d + 1}"); }
        }
    }

    /// <summary>The gate table on seeds 101-120 (rain and growth off), every value and the printed table exactly as the toy's.</summary>
    [Fact]
    [Trait("Category", "Gate")]
    public void TheGateTableOnSeeds101To120IsExactlyTheToys()
    {
        var gold = Golden2.Root.GetProperty("table").EnumerateArray().ToArray();
        var rows = new ExactRow[gold.Length];
        Parallel.For(0, gold.Length, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Math.Min(4, Environment.ProcessorCount - 1)) },
            i => rows[i] = GateTables.Exact(gold[i].GetProperty("seed").GetInt32(), gold[i].GetProperty("walkers").GetInt32(), Toy));
        string table = GateTables.ExactTable("G3 seeds 101-120 (200 walkers, rain and growth off)", rows);
        _out.WriteLine(table);
        Console.WriteLine(table);
        var bad = new List<string>();
        for (int i = 0; i < gold.Length; i++)
        {
            var g = gold[i]; var r = rows[i];
            if (g.GetProperty("fingerprint").GetString() != r.Fingerprint || g.GetProperty("width").GetInt32() != r.Width || g.GetProperty("ft").GetDouble() != r.Ft
                || g.GetProperty("mean").GetDouble() != r.Mean || g.GetProperty("det").GetDouble() != r.Det || g.GetProperty("cov").GetDouble() != r.Cov)
                bad.Add($"seed {r.Seed}: fingerprint {r.Fingerprint} (toy {g.GetProperty("fingerprint").GetString()}), width {r.Width} (toy {g.GetProperty("width").GetInt32()}), mean {r.Mean:R} (toy {g.GetProperty("mean").GetDouble():R})");
        }
        Assert.True(bad.Count == 0, string.Join("\n", bad) + "\n" + table);
        Assert.Equal(Golden2.Root.GetProperty("tableText").GetString(), table);
    }

    /// <summary>Runs one off trace of the finished toy on SimParams.Toy; reports the first day each hash differs.</summary>
    internal static (bool ok, string report) TraceV2(JsonElement t, int days, SimParams? preset = null)
    {
        int seed = t.GetProperty("seed").GetInt32(), walkers = t.GetProperty("walkers").GetInt32();
        var w = new World(seed, walkers, preset ?? Toy);
        // Traces with rain or growth on also hash the mud and record the weather, houses and walkers every day.
        bool live = t.TryGetProperty("mudFnv", out var mudEl);
        uint[] mudGold = live ? Golden2.U(mudEl) : Array.Empty<uint>();
        var wxGold = live ? t.GetProperty("weather").EnumerateArray().Select(a => a.EnumerateArray().Select(Golden2.D).ToArray()).ToArray() : Array.Empty<double[]>();
        int[] housesGold = live ? Golden2.I(t.GetProperty("houses")) : Array.Empty<int>(), walkGold = live ? Golden2.I(t.GetProperty("walkerCount")) : Array.Empty<int>();
        int mudDiff = -1, wxDiff = -1, popDiff = -1;
        int tpd = w.Params.TicksPerDay;
        var names = new[] { "wear", "flow", "cost", "costX", "positions" };
        var gold = new[] { "wearFnv", "flowFnv", "costFnv", "costXFnv", "posFnv" }.Select(k => Golden2.U(t.GetProperty(k))).ToArray();
        int[] frugal = Golden2.I(t.GetProperty("frugalOut"));
        int[] firstTripGold = Golden2.I(t.GetProperty("firstTrip"));
        var diff = new int[5]; Array.Fill(diff, -1);
        int frugalDiff = -1;
        var firstTrip = new List<int>();
        bool tripDone = false;
        int prev = -1;
        var pos = new double[walkers * 2];
        for (int tk = 0; tk < days * tpd; tk++)
        {
            w.Tick();
            if (tk % tpd == tpd - 1)
            {
                int d = tk / tpd;
                if (pos.Length != w.WalkerCount * 2) pos = new double[w.WalkerCount * 2];
                for (int i = 0; i < w.WalkerCount; i++) { pos[2 * i] = w.Walkers[i].X; pos[2 * i + 1] = w.Walkers[i].Y; }
                uint[] now = { Fnv(w.Wear), Fnv(w.Flow), Fnv(w.Cost), Fnv(w.CostX), Fnv<double>(pos) };
                for (int k = 0; k < 5; k++) if (diff[k] < 0 && now[k] != gold[k][d]) diff[k] = d + 1;
                int fr = 0;
                foreach (var k in w.Walkers) if (k.Frugal && k.Visible) fr++;
                if (frugalDiff < 0 && fr != frugal[d]) frugalDiff = d + 1;
                if (live)
                {
                    var x = w.Weather;
                    double[] wx = { x.Rain, x.Wetness, x.Raining ? 1 : 0, x.RainStart, x.RainEnd, x.MudUntil, x.RainDays };
                    if (mudDiff < 0 && Fnv(w.Mud) != mudGold[d]) mudDiff = d + 1;
                    if (wxDiff < 0 && !wx.SequenceEqual(wxGold[d])) wxDiff = d + 1;
                    if (popDiff < 0 && (w.Houses.Length != housesGold[d] || w.WalkerCount != walkGold[d])) popDiff = d + 1;
                }
            }
            ref readonly var k0 = ref w.Walkers[0];
            if (!tripDone)
            {
                if (k0.Cell != prev && k0.Visible) firstTrip.Add(k0.Cell);
                prev = k0.Cell;
                if (k0.State == WalkState.Home && firstTrip.Count > 1) tripDone = true;
            }
        }
        int explorers = 0;
        foreach (var k in w.Walkers) if (k.Explorer) explorers++;
        bool tripOk = firstTrip.SequenceEqual(firstTripGold), exOk = explorers == t.GetProperty("explorers").GetInt32();
        var parts = new List<string>();
        for (int k = 0; k < 5; k++) parts.Add($"{names[k]} {(diff[k] < 0 ? "identical" : $"first differs on day {diff[k]}")}");
        parts.Add($"frugal walkers out {(frugalDiff < 0 ? "identical" : $"first differ on day {frugalDiff}")}");
        if (live)
        {
            parts.Add($"mud {(mudDiff < 0 ? "identical" : $"first differs on day {mudDiff}")}");
            parts.Add($"weather {(wxDiff < 0 ? "identical" : $"first differs on day {wxDiff}")}");
            parts.Add($"houses and walkers {(popDiff < 0 ? "identical" : $"first differ on day {popDiff}")}");
        }
        string report = $"seed {seed}, {walkers} walkers, {days} days: {string.Join("; ", parts)}; walker 0's first trip {(tripOk ? "identical" : "differs")}; explorers {explorers} {(exOk ? "identical" : "differ")}";
        // Positions drift by an ulp where .NET's exp differs from V8's (README, Parity); wear, costs and fields must not.
        bool ok = diff[0] < 0 && diff[1] < 0 && diff[2] < 0 && diff[3] < 0 && frugalDiff < 0 && tripOk && exOk && mudDiff < 0 && wxDiff < 0 && popDiff < 0;
        if (days == 45)
        {
            var f = t.GetProperty("final");
            var s = w.Stats();
            bool statsOk = s.DetourRatio == f.GetProperty("detourRatio").GetDouble() && s.TrunkShare == f.GetProperty("trunkShare").GetDouble()
                && s.TrailCoverage == f.GetProperty("trailCoverage").GetDouble() && s.TripsInWindow == f.GetProperty("tripsInWindow").GetInt32()
                && s.FirstTrailTime == f.GetProperty("firstTrailTime").GetDouble();
            report += $"; final stats {(statsOk ? "identical" : "differ")}";
            ok &= statsOk;
            if (t.TryGetProperty("grown", out var gr))
            {
                // [building, x, y, door, front, time, day] per grown house, in order.
                var want = gr.EnumerateArray().Select(a => a.EnumerateArray().Select(Golden2.D).ToArray()).ToArray();
                var have = w.Grown.ToArray().Select(h => new double[] { h.Building, h.X, h.Y, h.Door, h.Front, h.Time, h.Day }).ToArray();
                bool grownOk = want.Length == have.Length && want.Zip(have).All(z => z.First.SequenceEqual(z.Second));
                report += $"; grown houses {have.Length} {(grownOk ? "identical" : $"differ (toy {want.Length})")}";
                ok &= grownOk;
            }
        }
        return (ok, report);
    }
}
