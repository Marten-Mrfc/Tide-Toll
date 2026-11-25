using TideToll.Sim.Town;
using T = TideToll.Sim.Town.Town;

namespace TideToll.Sim.Tests.Town;

/// <summary>Rules v2: finite tracks, new tracks, extending cottages, requests, hints, work spots, and the r1 bug fixes.</summary>
public class TownRulesV2Tests
{
    private static void Rich(T t) => t.S.Coin = 1e9;

    private static void ToLevel(T t, int level)
    {
        // Renown just enough for the next level, then a purchase to apply it (the coin and Pace level it cost are put back).
        while (t.Level < level)
        {
            double coin = t.S.Coin; int pace = t.S.PaceLevel;
            t.S.Coin = 1e9; t.S.Renown = t.Plan.Level(t.Level + 1).Renown;
            Assert.True(t.BuyPace() || t.BuyVillager());
            t.S.Coin = coin; t.S.PaceLevel = pace;
            t.Step();
        }
    }

    [Fact]
    public void PaceHasTwentyClearStepsAndThenSaysSo()
    {
        var t = new T(1); Rich(t);
        double prev = T.PaceFactorAt(0);
        for (int i = 0; i < TownRules.PaceMax; i++)
        {
            Assert.True(t.BuyPace());
            double f = T.PaceFactorAt(t.PaceLevel);
            Assert.Equal(TownRules.PaceStep, f - prev, 9);
            prev = f;
        }
        Assert.Equal(20, t.PaceLevel);
        Assert.False(t.BuyPace());
        var items = new List<ShopItem>(); t.ShopItems(items);
        var pace = items.First(x => x.Kind == ShopItemKind.Pace);
        Assert.False(pace.Affordable);
        Assert.Equal("Fastest pace reached", pace.Reason);
    }

    [Fact]
    public void LoadStepsUnlockAtTownLevelsAndSayWhen()
    {
        var t = new T(1); Rich(t);
        var items = new List<ShopItem>(); t.ShopItems(items);
        var load = items.First(x => x.Kind == ShopItemKind.Load);
        Assert.Equal("Unlocks at level " + TownRules.LoadLevels[0], load.Reason);
        Assert.False(t.BuyLoad());
        ToLevel(t, TownRules.LoadLevels[0]);
        Assert.True(t.BuyLoad());
        Assert.Equal("Unlocks at level " + TownRules.LoadLevels[1], t.LoadLockReason);
        Assert.False(t.BuyLoad());
        ToLevel(t, TownRules.LoadLevels[2]);
        Assert.True(t.BuyLoad()); Assert.True(t.BuyLoad());
        Assert.False(t.BuyLoad());
    }

    [Fact]
    public void ExtendingACottageHousesOneMoreTwiceAtMost()
    {
        var t = new T(1); Rich(t);
        int c1 = t.Villager(0).Home;
        Assert.False(t.CanExtend(c1, out string why));
        Assert.Equal("Unlocks at level " + TownRules.ExtendFromLevel, why);
        ToLevel(t, TownRules.ExtendFromLevel);
        int h = t.Housing;
        var ev = new List<TownEvent>(); t.DrainEvents(ev);
        int s = t.SuggestedExtend();
        Assert.True(s >= 0);
        double price = t.ExtendPrice;
        Assert.True(t.Extend(c1));
        Assert.True(t.ExtendPrice > price);
        Assert.Equal(h + 1, t.Housing);
        Assert.Equal(1, t.Extensions(c1));
        t.DrainEvents(ev);
        Assert.Contains(ev, e => e.Kind == TownEventKind.CottageExtended && e.Plot == c1 && e.Level == 1);
        Assert.Contains(ev, e => e.Kind == TownEventKind.Purchase && e.Item == ShopItemKind.Extend && e.Index == c1);
        Assert.True(t.Extend(c1));
        Assert.False(t.Extend(c1));
        Assert.False(t.CanExtend(c1, out why));
        Assert.Equal("Already extended twice", why);
        Assert.False(t.CanExtend(t.Plan.Plots.First(p => p.Kind == PlotKind.Well).Id, out _));
    }

    [Fact]
    public void ValueTracksAppearWithTheirGoodsAndPayMore()
    {
        var t = new T(1); Rich(t);
        var items = new List<ShopItem>(); t.ShopItems(items);
        Assert.Contains(items, x => x.Kind == ShopItemKind.Value && x.Index == (int)Good.Water);
        Assert.DoesNotContain(items, x => x.Kind == ShopItemKind.Value && x.Index == (int)Good.Grain);
        Assert.False(t.BuyValue(Good.Grain));
        double before = t.JobValue(JobKind.Water);
        Assert.True(t.BuyValue(Good.Water));
        Assert.Equal(before * (1 + TownRules.ValueStep), t.JobValue(JobKind.Water), 9);
        ToLevel(t, 3); // the first field
        t.ShopItems(items);
        Assert.Contains(items, x => x.Kind == ShopItemKind.Value && x.Index == (int)Good.Grain);
        Assert.DoesNotContain(items, x => x.Kind == ShopItemKind.Value && x.Index == (int)Good.Flour);
    }

    [Fact]
    public void ToolsAndTheBellTrackWork()
    {
        var t = new T(1); Rich(t);
        Assert.False(t.BuyTools());
        ToLevel(t, TownRules.ToolsFromLevel);
        double f = t.ToolsFactor;
        Assert.True(t.BuyTools());
        Assert.True(t.ToolsFactor > f);
        double d = t.BellDuration, r = t.BellRest;
        Assert.True(t.BuyBellTrack());
        Assert.True(t.BellDuration > d && t.BellRest < r);
        for (int i = 1; i < TownRules.BellTrackMax; i++) Assert.True(t.BuyBellTrack());
        Assert.False(t.BuyBellTrack());
        Assert.Equal("The bell is at its best", t.BellTrackLockReason);
    }

    [Fact]
    public void RequestsAreAskedMetOrMissedSavedAndDeterministic()
    {
        string Run(out int asked, out int met, out int missed, out List<TownEvent> all)
        {
            var t = new T(2); var bot = new TownBot(BotKind.Payback, 2);
            var ev = new List<TownEvent>(); all = new List<TownEvent>();
            for (int k = 0; k < 15 * 60 * TownRules.TickHz; k++) { t.Step(); bot.Act(t); t.DrainEvents(ev); all.AddRange(ev); }
            asked = t.RequestsAsked; met = t.RequestsMet; missed = t.RequestsMissed;
            return TownSave.Save(t);
        }
        string a = Run(out int asked, out int met, out int missed, out var evs);
        string b = Run(out _, out _, out _, out _);
        Assert.Equal(a, b);
        Assert.True(asked >= 5, $"only {asked} requests in 15 minutes");
        Assert.Equal(asked, evs.Count(e => e.Kind == TownEventKind.RequestAsked));
        Assert.Equal(met, evs.Count(e => e.Kind == TownEventKind.RequestMet));
        Assert.Equal(missed, evs.Count(e => e.Kind == TownEventKind.RequestMissed));
        Assert.True(met > 0 && missed > 0, $"met {met}, missed {missed}");
        Assert.True(asked - met - missed is 0 or 1);
        foreach (var e in evs.Where(e => e.Kind == TownEventKind.RequestAsked)) Assert.True(e.Amount >= TownRules.RequestMinAmount && e.Coin > 0 && e.Value > 0);
        var loaded = TownSave.Load(a);
        Assert.Equal(asked, loaded.RequestsAsked);
    }

    [Fact]
    public void RequestViewAndTextDescribeTheAsk()
    {
        var t = new T(3); var bot = new TownBot(BotKind.Cheapest, 3);
        while (!t.RequestActive) { t.Step(); bot.Act(t); }
        var r = t.Request;
        Assert.True(r.Active && r.Amount > 0 && r.SecondsLeft > 0 && r.RewardCoin > 0);
        Assert.Equal(T.RequestWho[r.Who], r.WhoName);
        Assert.Matches(@"^The \w+ asks for \d+ (Water|Grain|Flour) within \d+ seconds$", t.RequestText);
        // While it runs the asked good pays the bonus.
        var job = r.Good == Good.Water ? JobKind.Water : r.Good == Good.Flour ? JobKind.Flour : JobKind.GrainHome;
        double withBonus = t.JobValue(job);
        t.S.ReqActive = false;
        Assert.Equal(withBonus / TownRules.RequestBonus, t.JobValue(job), 9);
    }

    [Fact]
    public void HintSpeaksOfFullHousingAndIsEmptyAtTheStart()
    {
        var t = new T(1);
        Assert.Equal("", t.Hint);
        Rich(t);
        ToLevel(t, TownRules.ExtendFromLevel);
        while (t.BuyLoad()) { }   // the "Load step ready" hint comes first while it lasts
        while (t.BuyVillager()) { }
        Assert.Equal(0, t.HousingRoom);
        Assert.Contains("Extend", t.Hint);
        Assert.True(t.HintPlot >= 0);
    }

    [Fact]
    public void WorkersStandAtDifferentSpotsInsideTheirWorkplace()
    {
        var t = new T(1); var bot = new TownBot(BotKind.Payback, 1);
        bool sawTwo = false;
        for (int k = 0; k < 12 * 60 * TownRules.TickHz; k++)
        {
            t.Step(); bot.Act(t);
            for (int w = 0; w < t.WorkplaceCount; w++)
            {
                var spots = new List<(double, double)>();
                for (int i = 0; i < t.VillagerCount; i++)
                {
                    var v = t.Villager(i);
                    if (v.Phase != VillagerPhase.Working || v.Workplace != w) continue;
                    var p = t.Plan.Plots[t.Workplace(w).Plot];
                    Assert.True(v.X >= p.X - 1 && v.X <= p.X + p.W + 1 && v.Y >= p.Y - 1 && v.Y <= p.Y + p.H + 1, $"worker at {v.X:F2},{v.Y:F2} not at plot {p.Id}");
                    Assert.DoesNotContain((v.X, v.Y), spots);
                    spots.Add((v.X, v.Y));
                }
                if (spots.Count >= 2) sawTwo = true;
            }
        }
        Assert.True(sawTwo);
    }

    [Fact]
    public void NoOneQueuesWhileAPlaceIsFree()
    {
        // r1 bug hunt finding 1: a place freed (or added by the second bucket) was left empty while a queue waited.
        for (int seed = 1; seed <= 3; seed++)
        {
            var t = new T(seed); var bot = new TownBot(BotKind.Cheapest, seed);
            for (int k = 0; k < 15 * 60 * TownRules.TickHz; k++)
            {
                t.Step(); bot.Act(t);
                for (int w = 0; w < t.WorkplaceCount; w++)
                {
                    var x = t.Workplace(w);
                    if (x.QueueLength > 0) Assert.True(x.Busy >= x.Places, $"seed {seed} t {t.Time:F1}: queue of {x.QueueLength} with {x.Busy}/{x.Places} busy at workplace {w}");
                }
            }
        }
    }

    [Fact]
    public void BuyingTheSecondBucketFillsItFromTheQueueAtOnce()
    {
        var t = new T(1);
        ToLevel(t, 4);
        var bucket = t.OfferOf(OfferKind.SecondBucket);
        int well = 0; while (t.Plan.Plots[t.Workplace(well).Plot].Kind != PlotKind.Well) well++;
        // Make a queue at the one-place well by hand.
        var w = t.S.Workplaces[well];
        Rich(t);
        while (t.BuyVillager()) { }
        for (int k = 0; k < 3000 && w.QueueLen == 0; k++) t.Step();
        Assert.True(w.QueueLen > 0, "no queue formed at the well");
        Assert.True(t.BuyOffer(bucket));
        Assert.Equal(2, w.Places);
        Assert.Equal(2, w.Busy);
    }

    [Fact]
    public void RenownRoundsHalfUp()
    {
        // r1 bug hunt finding 2: Math.Round used banker's rounding (2.5 -> 2, 3.5 -> 4).
        Assert.Equal(3, T.RenownFor(2.5));
        Assert.Equal(4, T.RenownFor(3.5));
        Assert.Equal(7, T.RenownFor(7.25));
    }

    [Fact]
    public void SixteenLevelsAndTheFirstIsTheStart()
    {
        var p = TownPlanner.Build(1);
        Assert.Equal(16, p.MaxLevel);
        for (int lv = 2; lv <= p.MaxLevel; lv++)
            Assert.True(p.Level(lv).Plots.Length + p.Level(lv).Roads.Length + p.Level(lv).Offers.Length > 0, $"level {lv} gives nothing on the map");
    }
}
