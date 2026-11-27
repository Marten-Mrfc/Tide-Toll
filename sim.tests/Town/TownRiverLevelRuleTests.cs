using TideToll.Sim.Town;
using T = TideToll.Sim.Town.Town;

namespace TideToll.Sim.Tests.Town;

/// <summary>n7 retune: the second age's level-cost rule (TownRulesRiver, levels; docs/plans/n7/retune-progress.md). A level's
/// renown is fixed at the ring (the best rate then, a fixed curve) and eases with time the same for every town: nothing the
/// town earns after the ring moves it, so earning more never makes the age longer and earning less never makes it shorter.</summary>
public class TownRiverLevelRuleTests
{
    private static string RungIn(int seed, double minutesAfter)
    {
        var t = new T(seed);
        var bot = new TownBot(BotKind.Payback, seed) { PlayOn = true };
        while (!t.InRiverAge) { t.Step(); bot.Act(t); }
        double ring = t.Time;
        while (t.Time - ring < minutesAfter * 60) { t.Step(); bot.Act(t); }
        return TownSave.Save(t);
    }

    /// <summary>Two copies of one second-age town, one of them given a far higher best coin rate and three times the coin:
    /// every level not reached yet needs the same renown in both, now and as they step on with no purchases.
    /// (Under the rule it replaces, the copy with the higher best rate had dearer levels.)</summary>
    [Fact]
    public void ALevelsRenownDoesNotFollowWhatTheTownEarnsAfterTheRing()
    {
        string json = RungIn(2, 4);
        var a = TownSave.Load(json); var b = TownSave.Load(json);
        b.S.MaxCoinPerMinute *= 4; b.S.Coin *= 3;
        for (int step = 0; step < 4; step++)
        {
            Assert.True(a.Level < a.MaxLevel);
            for (int n = a.Level + 1; n <= a.MaxLevel; n++) Assert.Equal(a.RiverLevelRenown(n), b.RiverLevelRenown(n));
            for (int k = 0; k < 30 * TownRules.TickHz; k++) { a.Step(); b.Step(); }
        }
    }

    /// <summary>A level's renown never rises while the town waits for it: it eases by LevelEase a minute from the ring.</summary>
    [Fact]
    public void ALevelsRenownOnlyEasesWithTime()
    {
        var t = TownSave.Load(RungIn(4, 1));
        var bot = new TownBot(BotKind.Payback, 4) { PlayOn = true };
        int next = t.Level + 1, seen = 0;
        double last = t.RiverLevelRenown(next), atRing = t.S.River!.RenownBase;
        while (!t.HasSeal && t.Time < 200 * 60)
        {
            t.Step(); bot.Act(t);
            if (t.Tick % (5 * TownRules.TickHz) != 0) continue;
            if (t.Level >= next) { next = t.Level + 1; if (next > t.MaxLevel) break; last = t.RiverLevelRenown(next); continue; }
            double now = t.RiverLevelRenown(next);
            Assert.True(now <= last, $"level {next}: {now} after {last}");
            Assert.True(now > atRing);
            last = now; seen++;
        }
        Assert.True(t.HasSeal);
        Assert.True(seen > 100);
    }

    /// <summary>The same town from the same ring, played on by the same bot: a copy that keeps a quarter more of what it
    /// earns seals no later, and one that loses a quarter of it seals no sooner (seeds 1 to 3).</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ARicherRunSealsNoLaterThanAPoorerOne(int seed)
    {
        string json = RungIn(seed, 0);
        double Seal(double keep)
        {
            var t = TownSave.Load(json);
            var bot = new TownBot(BotKind.Payback, seed) { PlayOn = true };
            double t0 = t.Time, earned = t.CoinEarned;
            while (!t.HasSeal && t.Time - t0 < 120 * 60)
            {
                t.Step();
                double got = t.CoinEarned - earned; earned = t.CoinEarned;
                t.S.Coin = Math.Max(0, t.S.Coin + (keep - 1) * got);
                bot.Act(t);
            }
            Assert.True(t.HasSeal);
            return (t.Time - t0) / 60;
        }
        double poor = Seal(0.75), even = Seal(1), rich = Seal(1.25);
        Assert.True(rich <= even && even <= poor, $"seal at {rich:0.00} / {even:0.00} / {poor:0.00} minutes (richer / as played / poorer)");
    }
}
