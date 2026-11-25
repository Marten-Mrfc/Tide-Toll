using TideToll.Sim.Testing;
using static TideToll.Sim.Tests.RiverBoatTests;
using static TideToll.Sim.Tests.RiverCrossingTests;
using static TideToll.Sim.Tests.RiverWalkerTests;

namespace TideToll.Sim.Tests;

/// <summary>
/// The river fixes of 25 November 2026 (bug hunt and the lead's rule): a ferry frozen mid-crossing sails again after
/// the thaw; flood repairs stop at the debt limit; a long flood ends with spring and leaves no mud after it; no flood in
/// the first spring of a new town; and the guard that the river rules with no goods and no crossings leave a plain
/// world's wear alone outside the flood band and outside winter.
/// </summary>
public class RiverRuleFixTests
{
    private static readonly RiverRules Calm = Rich with { HaulGoods = true, SpringFlood = false, WinterIce = false };

    [Fact]
    public void AFerryCaughtMidCrossingByTheIceDocksWhereItWasGoingAndSailsAgainAfterTheThaw()
    {
        var w = Make(2, 300, Calm);
        var x = Build(w, CrossingType.Ferry);
        int from = 0;
        for (int t = 0; t < 4 * w.Params.TicksPerDay; t++)
        {
            int side = x.FerrySide;
            w.Tick();
            if (side != 0 && x.FerrySide == 0 && x.Passengers > 0) { from = side; break; } // it just left a stage with riders
        }
        Assert.True(from != 0, "the ferry never sailed with riders");
        w.Tick(); // mid-crossing
        Assert.Equal(0, x.FerrySide);
        Assert.InRange(x.Progress, 1e-6, 1 - 1e-6);
        Drain(w);
        Assert.True(w.Freeze().Ok);
        Assert.Equal(-from, x.FerrySide); // docked at the stage it was sailing to
        Assert.Equal(0, x.Passengers);
        Assert.Equal(from < 0 ? 1.0 : 0.0, x.Progress);
        ToyHarness.Run(w, 1);
        Assert.Equal(-from, x.FerrySide); // frozen in
        Assert.True(w.Thaw().Ok);
        Drain(w);
        ToyHarness.Run(w, 1);
        var ev = Drain(w);
        Assert.Contains(ev, e => e.Crossing == x.Id && e.Kind == RiverEventKind.FerryDeparted);
        Assert.Contains(ev, e => e.Crossing == x.Id && e.Kind == RiverEventKind.FerryLanded);
        AssertRiverInvariants(w, "after the thaw");
    }

    [Fact]
    public void FloodRepairsNeverTakeThePurseBelowTheDebtLimit()
    {
        var rules = Calm with { FloodBandCells = 30, StartMoney = 0, FloodRepairCost = 1000 };
        var w = Make(4, 0, rules);
        var r = w.River!;
        Drain(w);
        Assert.True(w.StartFlood(1).Ok);
        Assert.Equal(rules.DebtLimit, r.Money, 9);
        Assert.Contains(Drain(w), e => e.Kind == RiverEventKind.FloodRepairs && Math.Abs(e.Value - -rules.DebtLimit) < 1e-9);
        Assert.Equal(-rules.DebtLimit, r.Stats.Upkeep, 9);
        // At the limit already: the next flood charges nothing and says nothing about repairs.
        while (r.Flooded) w.Tick();
        double m = r.Money;
        Assert.True(w.StartFlood(1).Ok);
        Assert.Equal(m, r.Money, 9);
        Assert.DoesNotContain(Drain(w), e => e.Kind == RiverEventKind.FloodRepairs);
    }

    [Fact]
    public void ALongFloodInSpringEndsWhenSummerBeginsAndLeavesNoMudAfterSpring()
    {
        var w = Make(3, 100, Calm);
        var r = w.River!;
        var p = w.Params;
        ToyHarness.Run(w, 1);
        Drain(w);
        Assert.True(w.StartFlood(10).Ok); // asked for 10 days on day 2 of a 4-day spring
        Assert.Equal(p.SeasonSimSec, r.FloodEndsAt, 9);
        Assert.Contains(Drain(w), e => e.Kind == RiverEventKind.FloodRising && Math.Abs(e.Value - 3) < 1e-9);
        while (w.Ticks < p.TicksPerSeason - 1) { w.Tick(); Assert.True(r.Flooded); }
        w.Tick(); // the first tick of summer
        Assert.Equal(1, w.Stats().Season);
        Assert.False(r.Flooded);
        Assert.False(r.Muddy);
        Assert.True(r.FordOpen);
        // A flood outside spring (an event or a tutorial) keeps its days but leaves no mud when it goes down.
        Assert.True(w.StartFlood(1).Ok);
        for (int t = 0; t < p.TicksPerDay; t++) w.Tick();
        Assert.False(r.Flooded);
        Assert.False(r.Muddy);
    }

    [Fact]
    public void NoFloodInTheFirstSpringOfANewTownLaterSpringsFollowTheChance()
    {
        Assert.False(RiverRules.Default.FloodInFirstSpring);
        var rules = NoSchedule with { FloodChance = 1, WinterIce = false };
        for (int seed = 1; seed <= 3; seed++)
        {
            var w = Make(seed, 0, rules);
            var r = w.River!;
            Assert.False(r.Flooded, $"seed {seed}: flooded on the first day of the first spring");
            Assert.DoesNotContain(Drain(w), e => e.Kind == RiverEventKind.FloodRising);
            int daysPerYear = (int)(4 * w.Params.SeasonSimSec / w.Params.DaySimSec);
            for (int t = 0; t < daysPerYear * w.Params.TicksPerDay - 1; t++) { w.Tick(); Assert.False(r.Flooded); }
            w.Tick(); // the first day of the second spring: chance 1
            Assert.True(r.Flooded, $"seed {seed}: no flood in the second spring with chance 1");
            Assert.Contains(Drain(w), e => e.Kind == RiverEventKind.FloodRising);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void DefaultRiverRulesWithNoGoodsAndNoCrossingsLeaveAPlainWorldsWearAloneOutsideTheBandAndWinter(int seed)
    {
        var plain = new World(seed, 200);
        var river = new World(seed, 200, SimParams.Default with { River = RiverRules.Default with { HaulGoods = false } });
        var band = river.River!.FloodBand.ToArray();
        var p = plain.Params;
        int winter = (int)(3 * p.SeasonSimSec / p.DaySimSec); // the first day of the first winter
        for (int day = 1; day <= winter; day++)
        {
            for (int t = 0; t < p.TicksPerDay; t++) { plain.Tick(); river.Tick(); }
            Assert.True(river.Stats().Season < 3 || day == winter);
            var a = plain.Wear;
            var b = river.Wear;
            for (int c = 0; c < a.Length; c++)
                if (band[c] == 0) Assert.True(a[c] == b[c], $"seed {seed} day {day}: wear differs at cell {c} ({a[c]} plain, {b[c]} river)");
        }
        Assert.Equal(plain.RngState, river.RngState);
    }
}
