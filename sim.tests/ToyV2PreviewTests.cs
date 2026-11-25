using System.Text.Json;
using TideToll.Sim.Testing;
using Xunit.Abstractions;
using static TideToll.Sim.Testing.ToyHarness;

namespace TideToll.Sim.Tests;

/// <summary>
/// The flow preview, version 2 (the finished toy's preview.ts: both kinds of walker, explorer and ordinary groups per
/// house, pairs from 0.2 trips a day, the trip-weighted mean cost change, the gravel note and the toy's wording), against
/// the previews in toy-golden-v2.json: a fence across a route, a footbridge, a fence ring round the well, gravel on, near
/// and far from a route, a gate in the fence and its removal. Every expected value comes from the toy.
/// </summary>
public class ToyV2PreviewTests
{
    private readonly ITestOutputHelper _out;
    public ToyV2PreviewTests(ITestOutputHelper output) => _out = output;

    private static int[] Ints(JsonElement e) => e.ValueKind == JsonValueKind.Null ? Array.Empty<int>() : e.EnumerateArray().Select(x => x.GetInt32()).ToArray();

    /// <summary>Runs one preview set of the comparison data (the exporter's previewSet) and compares every preview.</summary>
    internal static void PreviewSet(JsonElement g, SimParams p, Action<string> log)
    {
        var w = new World(g.GetProperty("seed").GetInt32(), g.GetProperty("walkers").GetInt32(), p);
        Run(w, g.GetProperty("days").GetInt32());
        // Same inputs first: if these match, the previews must match exactly (same Dijkstra, same trace).
        Assert.Equal(g.GetProperty("houses").GetInt32(), w.Houses.Length);
        Assert.Equal(g.GetProperty("wearFnv").GetUInt32(), Fnv(w.Wear));
        Assert.Equal(g.GetProperty("costFnv").GetUInt32(), Fnv(w.Cost));
        Assert.Equal(g.GetProperty("costXFnv").GetUInt32(), Fnv(w.CostX));
        Assert.Equal(g.GetProperty("tripRateFnv").GetUInt32(), Fnv<float>(TripTable(w)));
        Same(w, g.GetProperty("block"), w.PreviewFlows(new WhatIf { Block = Ints(g.GetProperty("fence")) }, 99), "block", log);
        Same(w, g.GetProperty("bridge"), w.PreviewFlows(new WhatIf { Bridge = Ints(g.GetProperty("bridgeCells")) }), "bridge", log);
        Same(w, g.GetProperty("enclose"), w.PreviewFlows(new WhatIf { Block = Ints(g.GetProperty("ring")) }), "enclose", log);
        foreach (var k in new[] { "On", "Near", "Far" })
        {
            int[] cells = Ints(g.GetProperty("gravel" + k));
            Assert.All(cells, c => Assert.True(w.CanGravel(c), $"gravel {k}: cell {c} refused"));
            Same(w, g.GetProperty($"gravel{k}Preview"), w.PreviewFlows(new WhatIf { Gravel = cells }), "gravel " + k, log);
        }
        // The fence is built, a gate in it previewed, then built and its removal previewed.
        w.PlaceFence(Ints(g.GetProperty("fence")));
        Run(w, 1);
        var target = g.GetProperty("gateTarget");
        int[] gate = Ints(target.GetProperty("cells"));
        Assert.Single(gate);
        var t = w.GateTarget(gate[0] % w.Grid.Width + 0.5, gate[0] / w.Grid.Width + 0.5);
        Assert.Equal(gate, t.Cells);
        Assert.False(t.Remove);
        Same(w, g.GetProperty("gate"), w.PreviewFlows(new WhatIf { Gate = gate }), "gate", log);
        Assert.True(w.PlaceGate(gate[0]));
        Run(w, 1);
        Same(w, g.GetProperty("ungate"), w.PreviewFlows(new WhatIf { Ungate = gate }), "ungate", log);
        var after = g.GetProperty("after");
        Assert.Equal(after.GetProperty("ticks").GetInt64(), w.Ticks);
        Assert.Equal(after.GetProperty("wearFnv").GetUInt32(), Fnv(w.Wear));
        Assert.Equal(after.GetProperty("costFnv").GetUInt32(), Fnv(w.Cost));
        Assert.Equal(after.GetProperty("costXFnv").GetUInt32(), Fnv(w.CostX));
    }

    /// <summary>The trip table as the toy stores it, Buildings.Length square (the world keeps a fixed stride when houses can grow).</summary>
    internal static float[] TripTable(World w)
    {
        int b = w.Buildings.Length, s = w.TripRateStride;
        var t = new float[b * b];
        for (int h = 0; h < b; h++) for (int d = 0; d < b; d++) t[h * b + d] = w.TripRate[h * s + d];
        return t;
    }

    private static GravelNote NoteOf(JsonElement e) => e.ValueKind == JsonValueKind.Null ? GravelNote.None : e.GetString() switch
    {
        "far" => GravelNote.Far, "on" => GravelNote.On, "near" => GravelNote.Near, _ => throw new ArgumentException(e.GetString()),
    };

    private static void Same(World w, JsonElement g, FlowPreview p, string what, Action<string> log)
    {
        string describe = FlowPredictor.Describe(p, b => KindName(w.Buildings[b].Kind));
        log($"{what,-12} toy: \"{g.GetProperty("describe").GetString()}\"  C#: \"{describe}\"");
        Assert.Equal(g.GetProperty("describe").GetString(), describe);
        Assert.True(g.GetProperty("reroutedPerDay").GetDouble() == p.ReroutedPerDay, $"{what}: rerouted {p.ReroutedPerDay:R}, toy {g.GetProperty("reroutedPerDay").GetDouble():R}");
        Assert.True(g.GetProperty("meanCostRatio").GetDouble() == p.MeanCostRatio, $"{what}: meanCostRatio {p.MeanCostRatio:R}, toy {g.GetProperty("meanCostRatio").GetDouble():R}");
        Assert.Equal(NoteOf(g.GetProperty("note")), p.Note);
        Assert.Equal(g.GetProperty("more").GetInt32(), p.More);
        Assert.Equal(g.GetProperty("version").GetInt32(), p.Version);
        Assert.Equal(g.GetProperty("blocked").EnumerateArray().Select(e => (e[0].GetInt32(), e[1].GetInt32())), p.Blocked.Select(b => (b.Dest, b.Houses)));
        var changes = g.GetProperty("changes").EnumerateArray().ToArray();
        Assert.Equal(changes.Length, p.Changes.Count);
        for (int i = 0; i < changes.Length; i++)
        {
            var e = changes[i]; var c = p.Changes[i];
            string at = $"{what} change {i}";
            Assert.True(e.GetProperty("home").GetInt32() == c.Home && e.GetProperty("dest").GetInt32() == c.Dest, $"{at}: pair {c.Home}->{c.Dest}");
            Assert.True(e.GetProperty("rate").GetDouble() == c.Rate, $"{at}: rate {c.Rate:R}, toy {e.GetProperty("rate").GetDouble():R}");
            Assert.True(e.GetProperty("explorers").GetBoolean() == c.Explorers && e.GetProperty("frugal").GetBoolean() == c.Frugal, $"{at}: group explorers={c.Explorers} frugal={c.Frugal}");
            Assert.Equal(e.GetProperty("reachable").GetBoolean(), c.Reachable);
            if (c.Reachable) Assert.True(e.GetProperty("costRatio").GetDouble() == c.CostRatio, $"{at}: costRatio {c.CostRatio:R}");
            else Assert.True(double.IsPositiveInfinity(c.CostRatio), at);
            Assert.True(e.GetProperty("oldLen").GetInt32() == c.OldPts.Length && e.GetProperty("oldFnv").GetUInt32() == Fnv<float>(c.OldPts), $"{at}: old route");
            Assert.True(e.GetProperty("newLen").GetInt32() == c.NewPts.Length && e.GetProperty("newFnv").GetUInt32() == Fnv<float>(c.NewPts), $"{at}: new route");
        }
    }

    /// <summary>Seeds 2 and 4, 200 walkers, day 20, rain and growth off.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void PreviewsMatchTheToyExactly(int which)
    {
        var g = Golden2.Root.GetProperty("previews")[which];
        Assert.False(g.GetProperty("opts").GetProperty("rain").GetBoolean() || g.GetProperty("opts").GetProperty("growth").GetBoolean());
        PreviewSet(g, SimParams.Toy, _out.WriteLine);
    }

    /// <summary>The toy's readout wording (describePreview), case by case.</summary>
    [Fact]
    public void TheReadoutUsesTheToysWording()
    {
        string D(FlowPreview p) => FlowPredictor.Describe(p, b => b == 0 ? "well" : "market");
        Assert.Equal("No change to routes", D(new FlowPreview { OnAverage = true, ReroutedPerDay = 0.4 }));
        Assert.Equal("Too far from any route to change anything", D(new FlowPreview { OnAverage = true, Note = GravelNote.Far }));
        Assert.Equal("On a route walkers already use: it keeps the trail from growing back", D(new FlowPreview { OnAverage = true, Note = GravelNote.On }));
        Assert.Equal("May draw walkers from nearby trails", D(new FlowPreview { OnAverage = true, Note = GravelNote.Near }));
        Assert.Equal("Reroutes 1 walker a day", D(new FlowPreview { OnAverage = true, ReroutedPerDay = 0.6, MeanCostRatio = 1.019 }));
        Assert.Equal("Reroutes 3 walkers a day (+2% longer on average)", D(new FlowPreview { OnAverage = true, ReroutedPerDay = 3.4, MeanCostRatio = 1.02 }));
        Assert.Equal("Reroutes about 20 walkers a day (13% shorter on average)", D(new FlowPreview { OnAverage = true, ReroutedPerDay = 21.4, MeanCostRatio = 0.87 }));
        var blocked = new FlowPreview { OnAverage = true, ReroutedPerDay = 0.2 };
        blocked.Blocked.Add((0, 3));
        Assert.Equal("Blocks the well for 3 houses", D(blocked));
        var both = new FlowPreview { OnAverage = true, ReroutedPerDay = 33.9, MeanCostRatio = 1.079 };
        both.Blocked.Add((0, 1));
        Assert.Equal("Blocks the well for 1 house. Reroutes about 35 walkers a day (+8% longer on average)", D(both));
    }

    /// <summary>The bounded-step path gives exactly the one-go result on the new preset, for every kind of change.</summary>
    [Fact]
    public void SlicedPreviewEqualsTheOneGoPreviewOnTheToyPreset()
    {
        var g = Golden2.Root.GetProperty("previews")[0];
        var w = new World(2, 200, SimParams.Toy);
        Run(w, 20);
        uint wear = Fnv(w.Wear), rng = w.RngState;
        var changes = new (string, WhatIf)[]
        {
            ("block", new WhatIf { Block = Ints(g.GetProperty("fence")) }), ("bridge", new WhatIf { Bridge = Ints(g.GetProperty("bridgeCells")) }),
            ("enclose", new WhatIf { Block = Ints(g.GetProperty("ring")) }), ("gravel", new WhatIf { Gravel = Ints(g.GetProperty("gravelOn")) }),
            ("gravel far", new WhatIf { Gravel = Ints(g.GetProperty("gravelFar")) }),
        };
        foreach (var (name, change) in changes)
        {
            var whole = new FlowPredictor(w).Compute(change, 99);
            foreach (int budget in new[] { 1, 97, 1000, 1 << 20 })
            {
                var p = new FlowPredictor(w);
                p.Begin(change, 99);
                while (p.Step(budget)) { }
                PreviewSlicingTests.AssertSame(whole, p.Result!, $"{name} budget {budget}");
                Assert.True(whole.MeanCostRatio.Equals(p.Result!.MeanCostRatio) && whole.Note == p.Result.Note, $"{name} budget {budget}: mean or note");
                Assert.True(whole.Changes.Select(c => (c.Explorers, c.Frugal)).SequenceEqual(p.Result.Changes.Select(c => (c.Explorers, c.Frugal))), $"{name} budget {budget}: groups");
            }
        }
        Assert.Equal((wear, rng), (Fnv(w.Wear), w.RngState));
    }
}
