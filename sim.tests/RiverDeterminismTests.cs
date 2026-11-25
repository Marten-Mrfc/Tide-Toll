using TideToll.Sim.Testing;
using static TideToll.Sim.Tests.RiverBoatTests;

namespace TideToll.Sim.Tests;

/// <summary>
/// The river rules change nothing when off, use their own random stream when on, give the same run for the same
/// seed and commands, and allocate nothing on the tick.
/// </summary>
public class RiverDeterminismTests
{
    [Fact]
    public void RiverOnWithoutGoodsOrCrossingsLeavesWalkersAndWearUntouched()
    {
        // Boats come and go on their own stream; walkers, wear and the walker stream must not notice. (The spring flood
        // and winter ice do change walkers' routes, on purpose: they are off here; RiverSeasonTests covers them.)
        for (int seed = 1; seed <= 2; seed++)
        {
            var plain = new World(seed, 200, RiverTestRules.Plain);
            var river = new World(seed, 200, RiverTestRules.Trail with { River = RiverRules.Default with { HaulGoods = false, SpringFlood = false, WinterIce = false } });
            ToyHarness.Run(plain, 20);
            ToyHarness.Run(river, 20);
            Assert.Equal((ToyHarness.Fnv(plain.Wear), plain.RngState), (ToyHarness.Fnv(river.Wear), river.RngState));
            Assert.True(river.River!.Stats.Arrived.Sum() > 20, "boats sailed");
            Assert.Equal(plain.Buildings.Length, river.Buildings.Length);
        }
    }

    private static (uint Wear, uint Rng, double Money, double Rep, string Boats, string Events) Scripted(int seed, int raiseAt)
    {
        var w = new World(seed, 250, RiverTestRules.Trail with { River = RiverRules.Default with { StartMoney = 600 } });
        var events = new List<RiverEvent>();
        var log = new System.Text.StringBuilder();
        int draw = -1;
        for (int t = 0; t < 8 * 150; t++)
        {
            if (t == 30) Build(w, CrossingType.Ferry);
            if (t == 60) draw = Build(w, CrossingType.Drawbridge, 9).Id;
            if (t == 700) w.HireKeeper(draw);
            if (draw >= 0 && t % raiseAt == 0) w.SetBridge(draw, true);
            if (t == 900) w.SetWalkerCount(180);
            w.Tick();
            w.River!.DrainEvents(events);
        }
        foreach (var e in events) log.Append(e).Append('\n');
        var boats = string.Join(";", w.River!.Boats.ToArray().Select(b => $"{b.Id}:{b.State}:{b.Y:R}:{b.Cargo}"));
        return (ToyHarness.Fnv(w.Wear), w.RngState, w.River.Money, w.River.Reputation, boats, log.ToString());
    }

    [Fact]
    public void SameSeedAndCommandsGiveTheSameRiver()
    {
        var a = Scripted(6, 211);
        var b = Scripted(6, 211);
        Assert.Equal(a, b);
        Assert.Contains("BoatSold", a.Events);
        Assert.Contains("FerryLanded", a.Events);
        var c = Scripted(6, 157); // a different raise schedule changes the run
        Assert.NotEqual(a.Events, c.Events);
    }

    [Fact]
    public void TickWithTheRiverOnAllocatesNothing()
    {
        var w = new World(3, 500, RiverTestRules.Trail with { River = RiverRules.Default with { StartMoney = 1000 } });
        Build(w, CrossingType.Ferry);
        int d = Build(w, CrossingType.Drawbridge, 9).Id;
        w.HireKeeper(d);
        var events = new List<RiverEvent>(1024);
        for (int i = 0; i < 900; i++) { w.Tick(); w.River!.DrainEvents(events); events.Clear(); }
        long a0 = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 900; i++)
        {
            if (i % 200 == 0) w.SetBridge(d, true);
            w.Tick();
            w.River!.DrainEvents(events);
            events.Clear();
        }
        var s = w.Stats();
        var view = w.River!;
        double sum = view.Money + view.Reputation + view.IncomeDay + view.Boats.Length + view.Crossings[1].QueueEast;
        long allocated = GC.GetAllocatedBytesForCurrentThread() - a0;
        Assert.Equal(0, allocated);
        Assert.True(sum > 0 && s.Walkers == 500);
        Assert.True(view.Stats.Trips > 0 && view.Stats.Arrived.Sum() > 0);
    }
}

/// <summary>New test 1 of the spec: a world with River null equals one made with new SimParams() after 45 days on seeds 1 to 3.</summary>
[Collection(LongRunsCollection.Name)]
public class RiverNullLongRun
{
    internal static void Check()
    {
        var results = new (uint, uint, SimStats)[3, 2];
        Parallel.For(0, 6, new ParallelOptions { MaxDegreeOfParallelism = 3 }, i =>
        {
            int seed = 1 + i / 2;
            var w = i % 2 == 0 ? new World(seed, 200) : new World(seed, 200, new SimParams { River = null });
            ToyHarness.Run(w, 45);
            results[i / 2, i % 2] = (ToyHarness.Fnv(w.Wear), w.RngState, w.Stats());
        });
        for (int s = 0; s < 3; s++) Assert.Equal(results[s, 0], results[s, 1]);
    }

    [Fact]
    public void RiverNullMatchesAPlainWorldFor45Days() => Check();
}
