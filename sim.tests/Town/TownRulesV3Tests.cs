using TideToll.Sim.Town;
using T = TideToll.Sim.Town.Town;

namespace TideToll.Sim.Tests.Town;

/// <summary>Rules v3 (docs/plans/town-rules3-progress.md): choices of two, the charter drive, worth and milestones, extra hands.</summary>
public class TownRulesV3Tests
{
    private static void Rich(T t) => t.S.Coin = 1e9;

    private static void ToLevel(T t, int level)
    {
        while (t.Level < level)
        {
            double coin = t.S.Coin; int pace = t.S.PaceLevel;
            t.S.Coin = 1e9; t.S.Renown = t.Plan.Level(t.Level + 1).Renown;
            Assert.True(t.BuyPace() || t.BuyVillager());
            t.S.Coin = coin; t.S.PaceLevel = pace;
            t.Step();
        }
    }

    // ---- 1. Choices of two ----

    [Fact]
    public void AChoiceOfTwoIsOfferedAtItsLevelAndTheTownRunsOn()
    {
        var t = new T(1);
        Assert.False(t.ChoiceOpen);
        Assert.False(t.Choice.Open);
        var ev = new List<TownEvent>();
        ToLevel(t, TownRules.ChoiceLevels[0]);
        t.DrainEvents(ev);
        Assert.Contains(ev, e => e.Kind == TownEventKind.ChoiceOffered && e.Index == 0 && e.Level == TownRules.ChoiceLevels[0]);
        var c = t.Choice;
        Assert.True(c.Open);
        Assert.Equal(0, c.Choice);
        Assert.Equal(Boon.WiderLanes, c.A); Assert.Equal(Boon.StrongerBacks, c.B);
        Assert.False(string.IsNullOrEmpty(c.AName)); Assert.False(string.IsNullOrEmpty(c.BText));
        Assert.DoesNotContain("—", c.AText + c.BText);
        Assert.True(c.AWorth >= 0 && c.BWorth >= 0);
        // The town runs on while the choice is open.
        long trips = t.Trips;
        for (int i = 0; i < 600; i++) t.Step();
        Assert.True(t.Trips > trips);
        Assert.True(t.ChoiceOpen);
        // A boon of another choice cannot be taken; side B can; then it is gone for good.
        Assert.False(t.Choose(Boon.MarketRight));
        Assert.True(t.ChooseSide(1));
        Assert.False(t.ChoiceOpen);
        Assert.True(t.HasBoon(Boon.StrongerBacks));
        Assert.False(t.HasBoon(Boon.WiderLanes));
        Assert.False(t.Choose(Boon.WiderLanes));
        Assert.Equal((int)Boon.StrongerBacks, t.ChoicePicked(0));
        t.DrainEvents(ev);
        Assert.Contains(ev, e => e.Kind == TownEventKind.BoonChosen && e.Index == (int)Boon.StrongerBacks && e.Level == 0 && e.Text == T.BoonName(Boon.StrongerBacks));
        var held = new List<Boon>(); t.BoonsHeld(held);
        Assert.Equal(new[] { Boon.StrongerBacks }, held);
    }

    [Fact]
    public void BoonsHaveTheirEffects()
    {
        var a = new T(1); var b = new T(1);
        ToLevel(a, TownRules.ChoiceLevels[2]); ToLevel(b, TownRules.ChoiceLevels[2]);
        Assert.True(a.ChooseSide(0)); Assert.True(b.ChooseSide(1)); // lanes / backs
        Assert.True(a.ChooseSide(0)); Assert.True(b.ChooseSide(1)); // well / rows
        int h = a.Housing;
        double well = a.WorkTime(PlotKind.Well), field = b.WorkTime(PlotKind.Field);
        Assert.True(a.ChooseSide(0));                                 // a third room
        Assert.True(b.ChooseSide(1));                                 // faster hands
        Assert.True(a.Housing > h);
        Assert.True(b.WorkTime(PlotKind.Field) < field);
        Assert.Equal(well, a.WorkTime(PlotKind.Well), 9);
        Assert.True(a.WorkTime(PlotKind.Well) < new T(1).WorkTime(PlotKind.Well));
        int road = 0;
        Assert.True(a.Comfort(road) > b.Comfort(road));
    }

    [Fact]
    public void ChoicesAreSavedAndLoaded()
    {
        var t = new T(3);
        ToLevel(t, TownRules.ChoiceLevels[1]);
        Assert.True(t.ChooseSide(0));
        Assert.True(t.ChoiceOpen);                                    // the second choice is still open
        var t2 = TownSave.Load(TownSave.Save(t));
        Assert.True(t2.ChoiceOpen);
        Assert.Equal(1, t2.Choice.Choice);
        Assert.True(t2.HasBoon(Boon.WiderLanes));
        Assert.True(t2.ChooseSide(1)); Assert.True(t.ChooseSide(1));
        for (int i = 0; i < 1200; i++) { t.Step(); t2.Step(); }
        Assert.Equal(TownSave.Save(t), TownSave.Save(t2));
    }

    [Fact]
    public void ThePaybackBotDoesNotAlwaysPickTheSameSide()
    {
        // The lead's decision 1: on seeds 1 to 10 neither side of any choice is picked by the payback bot more than 8 times.
        var runs = new PacingResult[TownTargets.SeedTo - TownTargets.SeedFrom + 1];
        Parallel.For(TownTargets.SeedFrom, TownTargets.SeedTo + 1, s => runs[s - TownTargets.SeedFrom] = TownPacing.Run(s, BotKind.Payback, TownTargets.RunMinutes));
        for (int c = 0; c < TownRules.ChoiceLevels.Length; c++)
        {
            int a = runs.Count(r => r.Boons[c] == 2 * c), b = runs.Count(r => r.Boons[c] == 2 * c + 1);
            Assert.True(a + b == runs.Length, $"choice {c}: decided in {a + b} of {runs.Length} runs");
            Assert.True(a <= 8 && b <= 8, $"choice {c}: side A {a}, side B {b} of {runs.Length}");
        }
    }

    [Fact]
    public void BotsDecideByTheirNature()
    {
        var r = TownPacing.Run(2, BotKind.Cheapest, TownTargets.RunMinutes);
        for (int c = 0; c < 4; c++) Assert.Equal(2 * c, r.Boons[c]); // cheapest takes the first
    }

    // ---- 2. The charter drive ----

    [Fact]
    public void TheCharterDriveComesAfterTheSecondToLastLevelAndIsMetInParts()
    {
        var t = new T(1);
        var ev = new List<TownEvent>();
        ToLevel(t, t.MaxLevel - 2);
        Assert.False(t.Drive.Active);
        Assert.Equal("", t.DriveText);
        ToLevel(t, t.MaxLevel - 1);
        t.DrainEvents(ev);
        var d = t.Drive;
        Assert.True(d.Active);
        Assert.Equal(T.DriveName, d.Name);
        Assert.True(d.Amount >= 10);
        Assert.True(d.RewardRenown > 0);
        Assert.StartsWith(T.DriveName, t.DriveText);
        Assert.Contains(ev, e => e.Kind == TownEventKind.DriveAsked && e.Amount == d.Amount);
        // Progress stays and renown comes in parts; it cannot be missed (no time limit).
        double renown = t.Renown;
        int steps = 0;
        while (t.Drive.Delivered == 0 && steps++ < 20 * 60 * TownRules.TickHz) t.Step();
        Assert.True(t.Drive.Delivered > 0);
        Assert.True(t.Drive.RenownGiven > 0);
        Assert.True(t.Renown > renown);
        for (int i = 0; i < 40 * 60 * TownRules.TickHz && !t.Drive.Done; i++) t.Step();
        Assert.True(t.Drive.Done, $"delivered {t.Drive.Delivered} of {t.Drive.Amount}");
        Assert.False(t.Drive.Active);
        Assert.Equal(t.Drive.RewardRenown, t.Drive.RenownGiven, 6);
        t.DrainEvents(ev);
        Assert.Contains(ev, e => e.Kind == TownEventKind.DriveMet);
        Assert.Contains(ev, e => e.Kind == TownEventKind.DriveProgress);
    }

    [Fact]
    public void TheLastGapIsNoLongerThanTheOnesBeforeIt()
    {
        // The lead's decision 2, measured on the average over seeds 1 to 10 per bot (decided: a single run's gaps are noisy):
        // level 15 to the charter is no longer than the longest of the three level gaps before it.
        foreach (var k in new[] { BotKind.Cheapest, BotKind.Payback, BotKind.Random, BotKind.Saver })
        {
            var runs = new PacingResult[TownTargets.SeedTo - TownTargets.SeedFrom + 1];
            Parallel.For(TownTargets.SeedFrom, TownTargets.SeedTo + 1, s => runs[s - TownTargets.SeedFrom] = TownPacing.Run(s, k, TownTargets.RunMinutes));
            double last = runs.Average(r => r.LastGap), before = runs.Average(r => r.LongestEarlierGap);
            Assert.True(runs.All(r => r.DriveAsked > 0), $"{k}: a run without the drive");
            Assert.True(last <= before, $"{k}: last gap {last:F2} min > longest of the three before {before:F2} min");
        }
    }

    // ---- 3. Steps that show ----

    [Fact]
    public void ShopItemsTellWhatTheyAreWorth()
    {
        var t = new T(1);
        for (int i = 0; i < 3000; i++) t.Step();
        var items = new List<ShopItem>(); t.ShopItems(items);
        Assert.True(items.First(x => x.Kind == ShopItemKind.Villager).Worth > 0);
        Assert.True(items.First(x => x.Kind == ShopItemKind.Pace).Worth > 0);
        Assert.True(items.Where(x => x.Kind == ShopItemKind.Value).All(x => x.Worth > 0));
        Assert.True(items.All(x => x.Worth >= 0 && !double.IsNaN(x.Worth)));
        var pave = items.First(x => x.Kind == ShopItemKind.Pave);
        if (pave.Index < 0) Assert.Equal(0, pave.Worth);
    }

    [Fact]
    public void EveryFifthStepIsANamedMilestoneWithABiggerEffect()
    {
        var t = new T(1); Rich(t);
        ToLevel(t, t.MaxLevel);
        var ev = new List<TownEvent>(); t.DrainEvents(ev);
        for (int i = 0; i < 5; i++) Assert.True(t.BuyPace());
        for (int i = 0; i < 5; i++) Assert.True(t.BuyTools());
        for (int i = 0; i < 5; i++) Assert.True(t.BuyValue(Good.Water));
        t.DrainEvents(ev);
        var ms = ev.Where(e => e.Kind == TownEventKind.Milestone).ToList();
        Assert.Equal(3, ms.Count);
        Assert.Contains(ms, e => e.Item == ShopItemKind.Pace && e.Level == 5 && e.Text == "Long strides");
        Assert.Contains(ms, e => e.Item == ShopItemKind.Tools && e.Level == 5 && e.Text == "Sharp tools");
        Assert.Contains(ms, e => e.Item == ShopItemKind.Value && e.Index == (int)Good.Water && e.Text == "Water: a fair name");
        Assert.True(T.PaceFactorAt(5) - T.PaceFactorAt(4) > T.PaceFactorAt(4) - T.PaceFactorAt(3));
        Assert.True(T.ToolsFactorAt(5) - T.ToolsFactorAt(4) > T.ToolsFactorAt(4) - T.ToolsFactorAt(3));
        Assert.True(T.ValueFactorAt(5) - T.ValueFactorAt(4) > T.ValueFactorAt(4) - T.ValueFactorAt(3));
        Assert.Equal("", T.MilestoneName(ShopItemKind.Pace, -1, 4));
    }

    // ---- 4. Queues ask for a purchase ----

    [Fact]
    public void AQueueThatStaysOffersAnExtraHand()
    {
        int offered = 0, bought = 0;
        foreach (int seed in new[] { 1, 2, 3 })
        {
            var t = new T(seed);
            var bot = new TownBot(BotKind.Payback, seed);
            var ev = new List<TownEvent>();
            var items = new List<ShopItem>();
            for (int i = 0; i < 30 * 60 * TownRules.TickHz && !t.HasCharter; i++)
            {
                t.Step(); bot.Act(t);
                t.DrainEvents(ev);
                foreach (var e in ev)
                {
                    if (e.Kind == TownEventKind.OfferAvailable && e.Item == ShopItemKind.Hand)
                    {
                        offered++;
                        Assert.EndsWith(" hand at the " + (t.Plan.Plots[e.Plot].Kind == PlotKind.Well ? "well" : t.Plan.Plots[e.Plot].Kind == PlotKind.Field ? "field" : "mill"), e.Text);
                        Assert.True(t.HandOffered(e.Index));
                        t.ShopItems(items);
                        Assert.Contains(items, x => x.Kind == ShopItemKind.Hand && x.Index == e.Index && x.Name == e.Text && x.Price == t.HandPrice(e.Index));
                    }
                    if (e.Kind == TownEventKind.Purchase && e.Item == ShopItemKind.Hand) bought++;
                }
            }
            for (int w = 0; w < t.WorkplaceCount; w++) Assert.True(t.HandsBought(w) <= TownRules.HandMax);
        }
        Assert.True(offered > 0 && bought > 0, $"offered {offered}, bought {bought}");
    }

    [Fact]
    public void BuyingAHandAddsAPlaceAndTheHintNamesIt()
    {
        var t = new T(1); Rich(t);
        Assert.False(t.BuyHand(0));
        var w = t.S.Workplaces[0];
        w.HandOffered = true;
        int places = w.Places;
        string name = t.HandName(0);
        Assert.Equal("A second hand at the well", name);
        // A queue at that workplace: the hint names the offer.
        w.QueueLen = 2; w.Queue[0] = 0; w.Queue[1] = 0;
        Assert.Contains(name, t.Hint);
        w.QueueLen = 0;
        Assert.True(t.BuyHand(0));
        Assert.Equal(places + 1, w.Places);
        Assert.Equal(1, t.HandsBought(0));
        Assert.False(t.HandOffered(0));
        Assert.False(t.BuyHand(0));
    }
}
