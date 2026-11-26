using TideToll.Sim.Testing;
using TideToll.Sim.Town;
using T = TideToll.Sim.Town.Town;

namespace TideToll.Sim.Tests.Town;

/// <summary>The second age's targets (TownRiverTargets) for the four bots on seeds 1 to 10. One shared set of runs.</summary>
public class TownRiverPacingTests
{
    private static readonly BotKind[] Buyers = { BotKind.Cheapest, BotKind.Payback, BotKind.Random };
    private static readonly BotKind[] All = { BotKind.Cheapest, BotKind.Payback, BotKind.Random, BotKind.Saver };

    private static readonly Lazy<RiverPacingResult[]> Runs = new(() =>
    {
        int ns = TownRiverTargets.SeedTo - TownRiverTargets.SeedFrom + 1;
        var res = new RiverPacingResult[ns * All.Length];
        Parallel.For(0, res.Length, new ParallelOptions { MaxDegreeOfParallelism = 3 },
            i => res[i] = TownRiverPacing.Run(TownRiverTargets.SeedFrom + i / All.Length, All[i % All.Length]));
        return res;
    });

    private static void Check(Func<RiverPacingResult, bool> ok, Func<RiverPacingResult, string> say, BotKind[] bots)
    {
        var miss = new List<string>();
        foreach (var r in Runs.Value)
            if (Array.IndexOf(bots, r.Bot) >= 0 && !ok(r)) miss.Add($"seed {r.Seed} {TownBot.Name(r.Bot)}: {say(r)}");
        Assert.True(miss.Count == 0, string.Join("\n", miss));
    }

    [Fact]
    public void EveryBotWinsTheSeal() => Check(r => r.Seal > 0, r => "no seal", All);

    [Fact]
    public void TheSecondAgeTakes20To35MinutesForTheBuyers() =>
        Check(r => r.MinutesOfAge >= TownRiverTargets.AgeMinutesMin && r.MinutesOfAge <= TownRiverTargets.AgeMinutesMax, r => $"{r.MinutesOfAge:0.0} minutes", Buyers);

    [Fact]
    public void AtLeastTwoPurchasesAMinute() =>
        Check(r => r.Seal > 0 && r.Purchases / (r.Seal / 60) >= TownRiverTargets.PurchasesPerMinuteMin, r => $"{(r.Seal > 0 ? r.Purchases / (r.Seal / 60) : 0):0.00} a minute", All);

    [Fact]
    public void NeverMoreThan60SecondsWithNothingAffordable() =>
        Check(r => r.LongestNothingAffordable <= TownRiverTargets.NothingAffordableMax, r => $"{r.LongestNothingAffordable:0} s from {r.LongestNothingAffordableAt / 60:0.0} min", All);

    [Fact]
    public void SomethingNewOnTheMapEvery3AndAHalfMinutes() =>
        Check(r => r.LongestMapGap <= TownRiverTargets.MapGapMax, r => $"{r.LongestMapGap:0} s from {r.LongestMapGapAt / 60:0.0} min", All);

    [Fact]
    public void TheSaverBuysEveryKindOfNewThing() =>
        Check(r =>
        {
            for (int k = (int)ShopItemKind.Ferry; k <= (int)ShopItemKind.Winch; k++) if (r.ByKind[k] == 0) return false;
            return true;
        }, r =>
        {
            var s = "";
            for (int k = (int)ShopItemKind.Ferry; k <= (int)ShopItemKind.Winch; k++) if (r.ByKind[k] == 0) s += (ShopItemKind)k + " ";
            return "never bought " + s;
        }, new[] { BotKind.Saver });

    /// <summary>Capacity (Town.CrossingCapacity: people a minute with a full queue), when each crossing opens and at the seal.
    /// Use per minute (CrossPerMinute) follows demand and is printed by the tool, not tested.</summary>
    [Fact]
    public void EachCrossingCarriesMoreAMinuteThanTheOneBefore() =>
        Check(r =>
        {
            for (int c = 2; c < 5; c++) if (!(r.CapAtOpen[c] > r.PrevCapAtOpen[c]) || !(r.CapAtSeal[c] > r.CapAtSeal[c - 1])) return false;
            return r.CapAtSeal[1] > 0;
        }, r => $"at opening {r.PrevCapAtOpen[2]:0}<{r.CapAtOpen[2]:0}, {r.PrevCapAtOpen[3]:0}<{r.CapAtOpen[3]:0}, {r.PrevCapAtOpen[4]:0}<{r.CapAtOpen[4]:0}; at the seal {r.CapAtSeal[1]:0}/{r.CapAtSeal[2]:0}/{r.CapAtSeal[3]:0}/{r.CapAtSeal[4]:0}", All);

    [Fact]
    public void TheLandingEarnsAThirdOfAllCoinByTheMiddleOfTheAge() =>
        Check(r => r.LandingShareAtMid >= TownRiverTargets.LandingShareMin, r => $"{r.LandingShareAtMid:0.00}", All);

    [Fact]
    public void NobodyIsStrandedOrStuckAtACrossing() =>
        Check(r => r.LongestCrossingWait <= TownRiverTargets.CrossingWaitMax, r => $"waited {r.LongestCrossingWait:0} s", All);

    [Fact]
    public void OneGameMinuteAtTheEndOfTheSecondAgeIsCheap()
    {
        var t = new T(1);
        var bot = new TownBot(BotKind.Payback, 1) { PlayOn = true };
        var ev = new List<TownEvent>(256);
        while (!t.HasSeal && t.Time < TownRiverTargets.PlayMinutesMax * 60) { t.Step(); bot.Act(t); if (t.Tick % 50 == 0) t.DrainEvents(ev); }
        Assert.True(t.HasSeal);
        for (int k = 0; k < 600; k++) t.Step();
        double best = double.MaxValue;
        for (int rep = 0; rep < 3; rep++)
        {
            double t0 = CpuClock.NowMs();
            for (int k = 0; k < 60 * TownRules.TickHz; k++) t.Step();
            best = Math.Min(best, CpuClock.NowMs() - t0);
        }
        Assert.True(best < TownRiverTargets.MinuteMsMax, $"one game minute took {best:F2} ms of CPU with {t.VillagerCount} villagers");
        t.DrainEvents(ev);
        long b0 = GC.GetAllocatedBytesForCurrentThread();
        for (int k = 0; k < 60 * TownRules.TickHz; k++) { t.Step(); if (k % 10 == 0) { ev.Clear(); t.DrainEvents(ev); } }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - b0);
    }

    // ---- River polish ----

    [Fact]
    public void TheFordIsTheOnlyWayOverForARealStretch() =>
        Check(r => r.FordOnlySeconds / 60 >= TownRiverTargets.FordOnlyMinutesMin, r => $"{r.FordOnlySeconds / 60:0.0} minutes", All);

    [Fact]
    public void VillagersCrossTheFordAndWaitAtHighWaterBeforeTheFerry()
    {
        long crossings = 0; double wait = 0; var used = new HashSet<int>();
        foreach (var r in Runs.Value)
        {
            crossings += r.FordCrossingsBeforeFerry; wait += r.FordHighWaterWaitBeforeFerry;
            if (r.FordCrossingsBeforeFerry > 0) used.Add(r.Seed);
        }
        Assert.True(crossings >= TownRiverTargets.FordCrossingsAllMin, $"{crossings} ford crossings before the ferry in all runs");
        Assert.True(wait >= TownRiverTargets.FordHighWaterWaitAllMin, $"{wait:0} villager-seconds waiting at high water");
        Assert.True(used.Count >= TownRiverTargets.FordSeedsUsedMin, $"the ford used on {used.Count} seeds");
    }

    [Fact]
    public void BoatsPayTollsBeforeTheSeal() =>
        Check(r => r.TollsBeforeSeal >= TownRiverTargets.TollsMin && r.DrawbridgeMinutes >= TownRiverTargets.DrawbridgeMinutesMin,
            r => $"{r.TollsBeforeSeal} tolls in {r.DrawbridgeMinutes:0.0} minutes", All);

    [Fact]
    public void TollStepsPayBackBeforeTheSeal()
    {
        double spent = 0, earned = 0;
        foreach (var r in Runs.Value) { spent += r.TollStepSpent; earned += r.TollStepEarned; }
        Assert.True(spent > 0 && earned >= spent * TownRiverTargets.TollStepsBackAllMin, $"toll steps: spent {spent:0}, earned {earned:0}");
        Check(r => r.TollStepSpent <= 0 || r.TollStepEarned >= r.TollStepSpent * TownRiverTargets.TollStepsBackMin, r => $"spent {r.TollStepSpent:0}, earned {r.TollStepEarned:0}", All);
    }
}
