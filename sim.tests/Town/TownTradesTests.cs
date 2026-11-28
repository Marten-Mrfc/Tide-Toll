using TideToll.Sim.Town;
using T = TideToll.Sim.Town.Town;

namespace TideToll.Sim.Tests.Town;

/// <summary>Trades (n7 step 2, docs/plans/n7/trades-build-sheet.md part 1): the lean, the reward, the town's filling and
/// rebalancing, the player's counts and cards, Training, determinism, allocation and old saves.</summary>
public class TownTradesTests
{
    private static T Play(int seed, double minutes, BotKind kind = BotKind.Payback, Action<T>? each = null, List<TownEvent>? events = null)
    {
        var t = new T(seed);
        var bot = new TownBot(kind, seed);
        var ev = new List<TownEvent>();
        long n = (long)(minutes * 60 * TownRules.TickHz);
        for (long k = 0; k < n; k++)
        {
            t.Step(); bot.Act(t); each?.Invoke(t);
            t.DrainEvents(ev);
            events?.AddRange(ev);
        }
        return t;
    }

    private static T PlayUntil(int seed, Func<T, bool> until, BotKind kind = BotKind.Payback)
    {
        var t = new T(seed);
        var bot = new TownBot(kind, seed);
        while (!until(t) && t.Time < 40 * 60) { t.Step(); bot.Act(t); }
        return t;
    }

    [Fact]
    public void JobsBelongToTrades()
    {
        Assert.Equal(Trade.Water, T.TradeOfJob(JobKind.Water));
        Assert.Equal(Trade.Grain, T.TradeOfJob(JobKind.GrainHome));
        Assert.Equal(Trade.Grain, T.TradeOfJob(JobKind.GrainMarket));
        Assert.Equal(Trade.Flour, T.TradeOfJob(JobKind.Flour));
        // Step 3 as shipped (no porters): grain and flour to the boats stay the reapers' and millers' (step 2's);
        // wool and cloth are the shepherds' and weavers' (TownTrades2Tests.TheSecondAgesJobsBelongToTheirTrades).
        Assert.Equal(Trade.Grain, T.TradeOfJob(JobKind.GrainBoat));
        Assert.Equal(Trade.Flour, T.TradeOfJob(JobKind.FlourBoat));
        Assert.Equal(Trade.None, T.TradeOfJob(JobKind.None));
    }

    /// <summary>The lean: a job of the villager's own trade counts TradeLean times its rate; a Helper weighs every job at 1;
    /// so a member leaves its trade only for a job paying more than TradeLean times as much.</summary>
    [Fact]
    public void AMemberWeighsItsOwnTradeTradeLeanTimes()
    {
        Assert.InRange(TownRules.TradeLean, 1.25, 2);
        var t = new T(1);
        t.SetTownFillsTrades(false);
        Assert.True(t.SetVillagerTrade(0, Trade.Water));
        Assert.Equal(10 * TownRules.TradeLean, t.LeanedFor(0, JobKind.Water, 10), 9);
        Assert.Equal(10, t.LeanedFor(0, JobKind.GrainHome, 10), 9);
        t.SetVillagerTrade(0, Trade.None);
        Assert.Equal(10, t.LeanedFor(0, JobKind.Water, 10), 9);
        // In play: a water carrier with a grain job open still mostly carries water (the share of its trips at its own trade).
        var p = Play(2, 12, each: x => { if (x.Tick == 1) x.SetVillagerTrade(0, Trade.Water); });
        Assert.Equal(Trade.Water, p.S.Villagers[0].Trade);
    }

    /// <summary>The reward: at its own trade a villager works 1 + TradeWork (+ TradeLikes in the trade it likes) faster and
    /// its delivery is worth 1 + TradeWorth (+ TradeLikes) more; Training adds TradeStep a step to both; elsewhere, 1.</summary>
    [Fact]
    public void TheRewardAtItsOwnTrade()
    {
        var t = new T(1);
        t.SetTownFillsTrades(false);
        var v = t.S.Villagers[0];
        v.Job = JobKind.Water;
        Assert.Equal(1, t.TradeWorkFactor(v)); Assert.Equal(1, t.TradeWorthFactor(v)); // a Helper
        t.SetVillagerTrade(0, Trade.Water);
        double like = T.TradeOfLeaning(T.LeaningOf(1, 0)) == Trade.Water ? TownRules.TradeLikes : 0;
        Assert.Equal(1 + TownRules.TradeWork + like, t.TradeWorkFactor(v), 12);
        Assert.Equal(1 + TownRules.TradeWorth + like, t.TradeWorthFactor(v), 12);
        t.S.Training = 4;
        Assert.Equal(1 + TownRules.TradeWork + 4 * TownRules.TradeStep + like, t.TradeWorkFactor(v), 12);
        Assert.Equal(1 + TownRules.TradeWorth + 4 * TownRules.TradeStep + like, t.TradeWorthFactor(v), 12);
        v.Job = JobKind.GrainHome;
        Assert.Equal(1, t.TradeWorkFactor(v)); Assert.Equal(1, t.TradeWorthFactor(v)); // away from its trade
        Assert.True(TownRules.TradeWork > 0 && TownRules.TradeWorth > 0);

        // In play: the first delivery of a carrier pays the factor more than a Helper's, and comes sooner.
        double Coin(bool member, out double at)
        {
            var u = new T(3);
            u.SetTownFillsTrades(false);
            if (member) u.SetVillagerTrade(0, Trade.Water);
            var ev = new List<TownEvent>();
            for (int k = 0; k < 120 * TownRules.TickHz; k++)
            {
                u.Step(); u.DrainEvents(ev);
                foreach (var e in ev) if (e.Kind == TownEventKind.Delivery) { at = e.Time; return e.Coin; }
            }
            at = -1; return 0;
        }
        double helper = Coin(false, out double atH), carrier = Coin(true, out double atC);
        double like3 = T.TradeOfLeaning(T.LeaningOf(3, 0)) == Trade.Water ? TownRules.TradeLikes : 0;
        Assert.Equal(helper * (1 + TownRules.TradeWorth + like3), carrier, 6);
        Assert.True(atC < atH, $"carrier {atC} helper {atH}");
    }

    /// <summary>The town fills: within a second of arriving every villager has a trade (while the town fills and nobody set
    /// the Helpers' count); every open trade keeps a member.</summary>
    [Fact]
    public void TheTownGivesEveryVillagerATrade()
    {
        var events = new List<TownEvent>();
        double lastArrival = -1;
        var t = Play(1, 15, each: x =>
        {
            foreach (var e in x.PendingEvents) if (e.Kind == TownEventKind.VillagerArrived) lastArrival = Math.Max(lastArrival, e.Time);
            for (int i = 0; i < x.VillagerCount; i++)
                if (x.S.Villagers[i].Trade == Trade.None) Assert.True(x.Time - lastArrival <= 1.0 + 1e-9, $"villager {i} a Helper at {x.Time:F1}");
        }, events: events);
        Assert.Equal(0, t.Helpers);
        Assert.True(t.TradesShown);
        foreach (var tr in new[] { Trade.Water, Trade.Grain, Trade.Flour })
            if (t.TradeOpen(tr)) Assert.True(t.TradeInfo(tr).Members >= 1, $"{tr} has nobody");
        Assert.Contains(events, e => e.Kind == TownEventKind.TradeChanged && e.Level == 0);
    }

    /// <summary>The rebalance: never more than one villager every TradeMoveEvery seconds moves from one trade to another
    /// (the one exception: a trade that has just opened gets its first member at once); never a villager set by the
    /// player, never the open card's, never into or out of a trade whose count the player set.</summary>
    [Fact]
    public void TheRebalanceIsSlowAndLeavesThePlayersAlone()
    {
        var t = PlayUntil(2, x => x.TradeOpen(Trade.Grain) && x.VillagerCount >= 6);
        var ev = new List<TownEvent>();
        t.DrainEvents(ev);
        int mine = 1, card = 2;
        Assert.True(t.SetVillagerTrade(mine, Trade.Grain));
        t.SetOpenCard(card);
        var cardTrade = t.S.Villagers[card].Trade;
        var bot = new TownBot(BotKind.Payback, 2);
        t.DrainEvents(ev);
        var members = new int[T.TradeCount];
        foreach (var v in t.S.Villagers) members[(int)v.Trade]++;
        double lastMove = -1e9; int moves = 0; bool countSet = false; int waterSet = -1;
        for (long k = 0; k < 15L * 60 * TownRules.TickHz; k++)
        {
            t.Step(); bot.Act(t);
            if (!countSet && k == 60 * TownRules.TickHz) { countSet = true; waterSet = t.TradeInfo(Trade.Water).Members; t.SetTradeCount(Trade.Water, waterSet); }
            t.DrainEvents(ev);
            foreach (var e in ev)
            {
                if (e.Kind != TownEventKind.TradeChanged) continue;
                Assert.Equal(0, e.Level); // only the town moves anyone here
                Assert.NotEqual(mine, e.Villager);
                Assert.NotEqual(card, e.Villager);
                if (countSet) { Assert.NotEqual((int)Trade.Water, e.Index); Assert.NotEqual((int)Trade.Water, e.Amount); }
                members[e.Amount]--; members[e.Index]++;
                if (e.Amount == (int)Trade.None) continue; // a newcomer given its trade
                if (members[e.Index] == 1) continue;       // the first member of a trade just opened
                Assert.True(e.Time - lastMove >= TownRules.TradeMoveEvery - 1e-6, $"two moves {e.Time - lastMove:F1} s apart");
                lastMove = e.Time; moves++;
            }
        }
        Assert.True(moves > 0, "the town never rebalanced");
        Assert.Equal(Trade.Grain, t.S.Villagers[mine].Trade);
        Assert.Equal(cardTrade, t.S.Villagers[card].Trade);
        Assert.Equal(waterSet, t.TradeInfo(Trade.Water).Members);
        Assert.True(t.TradeInfo(Trade.Water).ByPlayer);
    }

    /// <summary>Your count: up takes Helpers first (the one who likes the trade first, then the lowest number), then a member
    /// of the largest town-decided trade; down gives members to the Helpers (one set on its card last). One villager's
    /// trade: set and kept. Switching the town's filling on hands every count back.</summary>
    [Fact]
    public void YourCountAndOneVillagersTrade()
    {
        var t = PlayUntil(3, x => x.TradeOpen(Trade.Grain) && x.VillagerCount >= 6);
        t.SetTownFillsTrades(false);
        for (int i = 0; i < t.VillagerCount; i++) t.ChangeTrade(t.S.Villagers[i], Trade.None, true, false);
        var ev = new List<TownEvent>(); t.DrainEvents(ev);
        Assert.Equal(t.VillagerCount, t.Helpers);
        int n = t.VillagerCount;
        // Expected: Helpers who like Grain first, then the lowest number.
        var expect = Enumerable.Range(0, n).OrderBy(i => t.VillagerLikes(i) == Trade.Grain ? 0 : 1).ThenBy(i => i).Take(2).OrderBy(i => i).ToArray();
        Assert.True(t.SetTradeCount(Trade.Grain, 2));
        var got = Enumerable.Range(0, n).Where(i => t.S.Villagers[i].Trade == Trade.Grain).ToArray();
        Assert.Equal(expect, got);
        Assert.True(t.TradeInfo(Trade.Grain).ByPlayer);
        t.DrainEvents(ev);
        Assert.Equal(2, ev.Count(e => e.Kind == TownEventKind.TradeChanged && e.Level == 1 && e.Index == (int)Trade.Grain));
        Assert.False(t.SetTradeCount(Trade.Grain, 2)); // nothing changed
        // One villager set on its card stays when the count goes down while another is left.
        Assert.True(t.SetVillagerTrade(got[0], Trade.Grain));
        Assert.True(t.SetTradeCount(Trade.Grain, 1));
        Assert.Equal(Trade.Grain, t.S.Villagers[got[0]].Trade);
        Assert.Equal(Trade.None, t.S.Villagers[got[1]].Trade);
        // Up again with no Helper left takes from the largest town-decided trade, which keeps one.
        t.SetTradeCount(Trade.Water, n - 1); // everyone else carries water (yours now)
        Assert.Equal(0, t.Helpers);
        if (!t.TradeOpen(Trade.Flour)) Assert.False(t.SetTradeCount(Trade.Flour, 1)); // not open yet
        // The town's filling on again: every count is the town's, the card's villager stays set.
        Assert.True(t.SetTownFillsTrades(true));
        Assert.False(t.TradeInfo(Trade.Water).ByPlayer);
        Assert.False(t.TradeInfo(Trade.Grain).ByPlayer);
        Assert.True(t.S.Villagers[got[0]].TradeByPlayer);
        Assert.False(t.SetVillagerTrade(got[0], Trade.Grain)); // already so
        Assert.False(t.SetVillagerTrade(0, (Trade)9));
        Assert.False(t.SetVillagerTrade(999, Trade.Water));
    }

    /// <summary>Helpers the player made stay Helpers while the town fills the rest.</summary>
    [Fact]
    public void HelpersYouMakeStayHelpers()
    {
        var t = PlayUntil(4, x => x.TradeOpen(Trade.Grain) && x.VillagerCount >= 6);
        for (int k = 0; k < TownRules.TickHz * 2; k++) t.Step();
        int water = t.TradeInfo(Trade.Water).Members;
        Assert.True(water >= 2);
        Assert.True(t.SetTradeCount(Trade.Water, water - 1));
        Assert.Equal(1, t.Helpers);
        var bot = new TownBot(BotKind.Payback, 4);
        for (int k = 0; k < 120 * TownRules.TickHz; k++) { t.Step(); bot.Act(t); }
        Assert.True(t.Helpers >= 1);
        Assert.Equal(water - 1, t.TradeInfo(Trade.Water).Members);
    }

    [Fact]
    public void TrainingIsOneRowOfTenSteps()
    {
        var items = new List<ShopItem>();
        var t = new T(1);
        t.ShopItems(items);
        Assert.DoesNotContain(items, i => i.Kind == ShopItemKind.Training); // not before the first field
        t = PlayUntil(1, x => x.TradesShown);
        for (int k = 0; k < TownRules.TickHz * 2; k++) t.Step();
        t.ShopItems(items);
        var row = items.Single(i => i.Kind == ShopItemKind.Training);
        Assert.Equal("Training", row.Name);
        Assert.Equal(t.TrainingPrice, row.Price);
        Assert.True(row.Worth > 0, "the bots' estimate knows the row");
        int load = items.FindIndex(i => i.Kind == ShopItemKind.Load);
        Assert.True(load < 0 || items.FindIndex(i => i.Kind == ShopItemKind.Training) == load + 1);
        Assert.Equal(TownRules.TradeStep, t.TrainingStepBonus);
        for (int s = 0; s < TownRules.TrainingMax; s++)
        {
            double before = t.TrainingPrice;
            t.S.Coin = 1e12;
            Assert.True(t.BuyTraining());
            Assert.Equal(s + 1, t.Training);
            Assert.True(t.TrainingPrice > before || s + 1 == TownRules.TrainingMax);
        }
        Assert.False(t.BuyTraining());
        Assert.Equal(0, t.TrainingStepBonus);
        t.ShopItems(items);
        row = items.Single(i => i.Kind == ShopItemKind.Training);
        Assert.True(row.Finished);
        Assert.Equal(TownRules.TradeWork + 10 * TownRules.TradeStep, t.TradeInfo(Trade.Water).WorkBonus, 12);
        // Locked while nobody has a trade.
        var u = PlayUntil(1, x => x.TradesShown);
        u.SetTownFillsTrades(false);
        for (int i = 0; i < u.VillagerCount; i++) u.ChangeTrade(u.S.Villagers[i], Trade.None, true, false);
        u.ShopItems(items);
        row = items.Single(i => i.Kind == ShopItemKind.Training);
        Assert.True(row.Locked);
        Assert.False(u.BuyTraining());
    }

    [Fact]
    public void SameSeedAndCallsGiveTheSameTown()
    {
        string Run()
        {
            var t = Play(5, 14, each: x =>
            {
                if (x.Tick == 3000) x.SetOpenCard(1);
                if (x.Tick == 4000 && x.TradeOpen(Trade.Grain)) x.SetTradeCount(Trade.Grain, 3);
                if (x.Tick == 6000) x.SetVillagerTrade(0, Trade.Grain);
                if (x.Tick == 9000) x.SetTownFillsTrades(true);
            });
            return TownSave.Save(t);
        }
        Assert.Equal(Run(), Run());
    }

    /// <summary>No allocation in the tick with trades on, a rebalancing move inside the measured window.</summary>
    [Fact]
    public void StepsAllocateNothingWithTradesOn()
    {
        var t = PlayUntil(1, x => x.TradeOpen(Trade.Flour) && x.VillagerCount >= 12);
        var ev = new List<TownEvent>(512);
        for (int k = 0; k < 600; k++) { t.Step(); if (k % 10 == 0) t.DrainEvents(ev); }
        t.DrainEvents(ev);
        long moves = t.S.TradeChangesByTown;
        long b0 = GC.GetAllocatedBytesForCurrentThread();
        for (int k = 0; k < 120 * TownRules.TickHz; k++) { t.Step(); if (k % 10 == 0) { ev.Clear(); t.DrainEvents(ev); } }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - b0);
        Assert.True(t.S.TradeChangesByTown > moves, "no rebalancing move in the window");
    }

    /// <summary>Saves written before trades (versions 2 and 3) load with every villager a Helper and the town filling;
    /// within a second each has a trade; the town then saves and loads exactly.</summary>
    [Theory]
    [InlineData("town-v2-seed3-12min.json")]
    [InlineData("town-v3-seed3-river6min.json")]
    public void OldSavesLoadWithHelpersAndTheTownFills(string file)
    {
        string json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Town", "data", file));
        Assert.DoesNotContain("TownFillsTrades", json);
        var t = TownSave.Load(json);
        Assert.True(t.TownFillsTrades);
        Assert.Equal(t.VillagerCount, t.Helpers);
        for (int k = 0; k < TownRules.TickHz; k++) t.Step();
        Assert.Equal(0, t.Helpers);
        string again = TownSave.Save(t);
        var u = TownSave.Load(again);
        Assert.Equal(again, TownSave.Save(u));
        for (int k = 0; k < 60 * TownRules.TickHz; k++) { t.Step(); u.Step(); }
        Assert.Equal(TownSave.Save(t), TownSave.Save(u));
    }

    [Fact]
    public void ViewsSayTheTradeAndTheLike()
    {
        var t = PlayUntil(6, x => x.TradesShown && x.VillagerCount >= 5);
        for (int k = 0; k < TownRules.TickHz * 2; k++) t.Step();
        int sum = 0;
        for (int i = 0; i < t.VillagerCount; i++)
        {
            var v = t.Villager(i);
            Assert.Equal(t.S.Villagers[i].Trade, v.Trade);
            var like = T.TradeOfLeaning(T.LeaningOf(6, i));
            Assert.Equal(t.TradeOpen(like) ? like : Trade.None, v.Likes);
        }
        for (int k = 0; k < T.TradeCount; k++) sum += t.TradeInfo((Trade)k).Members;
        Assert.Equal(t.VillagerCount, sum);
        Assert.Equal(0, t.TradeInfo(Trade.None).Places);
        Assert.True(t.TradeInfo(Trade.Water).Places >= 1);
    }

    /// <summary>The careful and careless players against left alone (seeds 1 to 3, the payback bot, the first age):
    /// careful at most about 8 % sooner to the charter, careless at most about 15 % later and never an idle villager.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void CarefulAndCarelessStayNearLeftAlone(int seed)
    {
        double Charter(TradePlay play, out double idleShare)
        {
            var t = new T(seed);
            var bot = new TownBot(BotKind.Payback, seed);
            var p = new TownTradePlayer(play);
            long idle = 0, samples = 0;
            while (!t.HasCharter && t.Time < 60 * 60)
            {
                t.Step(); bot.Act(t); p.Act(t);
                if (t.Tick % TownRules.TickHz == 0)
                    foreach (var v in t.S.Villagers) { samples++; if (v.Job == JobKind.None && v.Phase == VillagerPhase.Idle) idle++; }
            }
            idleShare = (double)idle / samples;
            return t.Time;
        }
        double alone = Charter(TradePlay.LeftAlone, out _);
        double careful = Charter(TradePlay.Careful, out _);
        double carriers = Charter(TradePlay.CarelessCarriers, out double idle);
        Assert.True(careful >= alone * 0.92, $"careful {careful / 60:F2} min against {alone / 60:F2}");
        Assert.True(carriers <= alone * 1.15, $"careless {carriers / 60:F2} min against {alone / 60:F2}");
        Assert.True(idle < 0.01, $"idle {idle:P1}");
    }

    /// <summary>CanRaiseTrade / CanLowerTrade say exactly whether SetTradeCount up or down by one would move anyone.</summary>
    [Fact]
    public void CanRaiseAndLowerSayWhatSetTradeCountWouldDo()
    {
        var t = PlayUntil(7, x => x.TradeOpen(Trade.Grain) && x.VillagerCount >= 6);
        for (int k = 0; k < TownRules.TickHz * 2; k++) t.Step();
        t.SetOpenCard(0);
        foreach (var tr in new[] { Trade.None, Trade.Water, Trade.Grain, Trade.Flour })
            for (int dir = -1; dir <= 1; dir += 2)
            {
                var copy = TownSave.Load(TownSave.Save(t));
                copy.SetOpenCard(0);
                bool can = dir > 0 ? copy.CanRaiseTrade(tr) : copy.CanLowerTrade(tr);
                int before = copy.TradeInfo(tr).Members;
                if (copy.TradeOpen(tr) || tr == Trade.None) copy.SetTradeCount(tr, before + dir);
                Assert.Equal(can, copy.TradeInfo(tr).Members != before);
            }
        // Nobody to lower in an empty trade; nobody to raise when every other villager is set on its card.
        var u = TownSave.Load(TownSave.Save(t));
        for (int i = 0; i < u.VillagerCount; i++) u.SetVillagerTrade(i, Trade.Water);
        Assert.False(u.CanRaiseTrade(Trade.Grain));
        Assert.False(u.CanLowerTrade(Trade.Grain));
        Assert.True(u.CanLowerTrade(Trade.Water));
        Assert.False(u.CanRaiseTrade(Trade.Flour) && !u.TradeOpen(Trade.Flour));
        long a0 = GC.GetAllocatedBytesForCurrentThread();
        bool x = u.CanRaiseTrade(Trade.Water) | u.CanLowerTrade(Trade.Grain);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - a0);
    }

    /// <summary>GiveVillagerBackToTown clears the mark, emits nothing, and the town may then move the villager.</summary>
    [Fact]
    public void AVillagerGivenBackIsTheTownsAgain()
    {
        var t = PlayUntil(8, x => x.TradeOpen(Trade.Grain) && x.VillagerCount >= 6);
        for (int k = 0; k < TownRules.TickHz * 2; k++) t.Step();
        var ev = new List<TownEvent>();
        Assert.True(t.SetVillagerTrade(0, Trade.Water));
        Assert.True(t.S.Villagers[0].TradeByPlayer);
        t.DrainEvents(ev);
        Assert.True(t.GiveVillagerBackToTown(0));
        Assert.False(t.S.Villagers[0].TradeByPlayer);
        Assert.Equal(Trade.Water, t.S.Villagers[0].Trade);
        Assert.Empty(t.PendingEvents);
        Assert.False(t.GiveVillagerBackToTown(0));
        Assert.False(t.GiveVillagerBackToTown(999));
        // The town may move it again: a count raised elsewhere may take it now.
        Assert.True(t.SetVillagerTrade(0, Trade.Water));
        t.GiveVillagerBackToTown(0);
        for (int i = 1; i < t.VillagerCount; i++) t.SetVillagerTrade(i, Trade.Water);
        Assert.True(t.CanRaiseTrade(Trade.Grain) || t.TradeInfo(Trade.Water).Members < 2);
        t.SetTradeCount(Trade.Grain, t.TradeInfo(Trade.Grain).Members + 1);
        Assert.Equal(Trade.Grain, t.S.Villagers[0].Trade);
    }
}
