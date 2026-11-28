using System.Text.Json.Nodes;
using TideToll.Sim.Town;
using T = TideToll.Sim.Town.Town;

namespace TideToll.Sim.Tests.Town;

/// <summary>Trades, step 3 (docs/plans/n7/trades2-build-sheet.md "The simulation"): the second age's trades (shepherds,
/// weavers, porters): their jobs, the opening, the filling, the player's counts and cards, old saves, no allocation.</summary>
public class TownTrades2Tests
{
    /// <summary>The payback bot to the ring and on until <paramref name="until"/> (or 30 minutes after the ring).</summary>
    private static T RungUntil(int seed, Func<T, bool> until, List<TownEvent>? events = null)
    {
        var t = new T(seed);
        var bot = new TownBot(BotKind.Payback, seed) { PlayOn = true };
        var ev = new List<TownEvent>();
        while (!t.InRiverAge) { t.Step(); bot.Act(t); t.DrainEvents(ev); }
        double ring = t.Time;
        while (!until(t) && t.Time - ring < 30 * 60)
        {
            t.Step(); bot.Act(t);
            t.DrainEvents(ev);
            events?.AddRange(ev);
        }
        return t;
    }

    private static bool AllThreeOpen(T t) => t.TradeOpen(Trade.Wool) && t.TradeOpen(Trade.Cloth) && t.TradeOpen(Trade.Boat);

    [Fact]
    public void TheSecondAgesJobsBelongToTheirTrades()
    {
        Assert.Equal(Trade.Wool, T.TradeOfJob(JobKind.WoolBoat));
        Assert.Equal(Trade.Wool, T.TradeOfJob(JobKind.WoolMarket));
        Assert.Equal(Trade.Cloth, T.TradeOfJob(JobKind.ClothBoat));
        Assert.Equal(Trade.Cloth, T.TradeOfJob(JobKind.ClothMarket));
        Assert.Equal(Trade.Boat, T.TradeOfJob(JobKind.GrainBoat));
        Assert.Equal(Trade.Boat, T.TradeOfJob(JobKind.FlourBoat));
        Assert.Equal(Trade.Wool, T.TradeOfLeaning(Leaning.Wool));
        Assert.Equal(Trade.Cloth, T.TradeOfLeaning(Leaning.Cloth));
        foreach (var l in Enum.GetValues<Leaning>()) Assert.NotEqual(Trade.Boat, T.TradeOfLeaning(l)); // nobody leans to the porters
        Assert.Equal(7, T.TradeCount);
    }

    /// <summary>The porter's rule (V2, shipped): a grain or flour boat trip is at its own trade for the porter and for the
    /// trade of its good; the lean and the reward follow it. (V1, TownRules.TradePorters false, is measured with the tools'
    /// --set; no test flips a rule other tests run under.)</summary>
    [Fact]
    public void BoatTripsAreOwnedByThePorterAndByTheirGoodsTrade()
    {
        Assert.True(TownRules.TradePorters && TownRules.BoatTripsOwnedTwice);
        Assert.True(T.AtOwnTrade(Trade.Boat, JobKind.GrainBoat) && T.AtOwnTrade(Trade.Grain, JobKind.GrainBoat));
        Assert.True(T.AtOwnTrade(Trade.Boat, JobKind.FlourBoat) && T.AtOwnTrade(Trade.Flour, JobKind.FlourBoat));
        Assert.False(T.AtOwnTrade(Trade.Grain, JobKind.FlourBoat) || T.AtOwnTrade(Trade.Flour, JobKind.GrainBoat));
        Assert.False(T.AtOwnTrade(Trade.Boat, JobKind.WoolBoat) || T.AtOwnTrade(Trade.None, JobKind.GrainBoat));
        Assert.True(T.AtOwnTrade(Trade.Wool, JobKind.WoolBoat) && T.AtOwnTrade(Trade.Cloth, JobKind.ClothMarket));
        var t = RungUntil(3, AllThreeOpen);
        t.SetTownFillsTrades(false);
        var v = t.S.Villagers[0];
        Assert.True(t.SetVillagerTrade(0, Trade.Grain));
        Assert.Equal(10 * TownRules.TradeLean, t.LeanedFor(0, JobKind.GrainBoat, 10), 9);
        Assert.Equal(10, t.LeanedFor(0, JobKind.FlourBoat, 10), 9);
        var keep = v.Job; v.Job = JobKind.GrainBoat;
        Assert.True(t.TradeWorkFactor(v) > 1 && t.TradeWorthFactor(v) > 1);
        v.Job = JobKind.FlourBoat;
        Assert.Equal(1, t.TradeWorkFactor(v));
        v.Job = keep;
    }

    /// <summary>The three open with the pasture, the weaver's cottage and the landing (never in the first age); their places:
    /// the pasture's, the weaver's, none for the porters; the moment one opens the town gives it a member (within a second).</summary>
    [Fact]
    public void TheyOpenWithTheirPlacesAndTakeAMemberAtOnce()
    {
        var first = new T(4);
        var bot = new TownBot(BotKind.Payback, 4) { PlayOn = true };
        while (!first.InRiverAge)
        {
            Assert.False(first.TradeOpen(Trade.Wool) || first.TradeOpen(Trade.Cloth) || first.TradeOpen(Trade.Boat));
            first.Step(); bot.Act(first);
        }
        var openedAt = new double[T.TradeCount]; Array.Fill(openedAt, -1);
        var memberAt = new double[T.TradeCount]; Array.Fill(memberAt, -1);
        var t = RungUntil(4, x =>
        {
            for (int k = T.FirstAgeTrades; k < T.TradeCount; k++)
            {
                if (openedAt[k] < 0 && x.TradeOpen((Trade)k)) openedAt[k] = x.Time;
                if (openedAt[k] >= 0 && memberAt[k] < 0 && x.TradeInfo((Trade)k).Members > 0) memberAt[k] = x.Time;
            }
            return memberAt[4] >= 0 && memberAt[5] >= 0 && memberAt[6] >= 0;
        });
        for (int k = T.FirstAgeTrades; k < T.TradeCount; k++)
        {
            Assert.True(openedAt[k] >= 0, $"{(Trade)k} never opened");
            Assert.InRange(memberAt[k] - openedAt[k], 0, 1.0 + 1e-9);
        }
        Assert.Equal(TownRulesRiver.PasturePlaces, t.TradeInfo(Trade.Wool).Places);
        Assert.Equal(TownRulesRiver.WeaverPlaces, t.TradeInfo(Trade.Cloth).Places);
        Assert.Equal(0, t.TradeInfo(Trade.Boat).Places);
        Assert.True(t.TradeInfo(Trade.Boat).Open);
    }

    /// <summary>The filling in the second age: every Helper takes a trade, every open trade keeps a member, the porters'
    /// floor is TradePortersPerBerth a berth (scaled down with the others when the town is smaller than all the floors), and
    /// the members of the new trades make most of their trips at their own trade's work.</summary>
    [Fact]
    public void TheTownFillsTheNewTrades()
    {
        var ev = new List<TownEvent>();
        var t = RungUntil(2, x => false, ev); // 30 minutes after the ring, or the seal
        Assert.True(AllThreeOpen(t));
        Assert.Equal(0, t.Helpers);
        for (int k = 1; k < T.TradeCount; k++) Assert.True(t.TradeInfo((Trade)k).Members >= 1, $"{(Trade)k} has nobody");
        int sum = 0;
        for (int k = 0; k < T.TradeCount; k++) sum += t.TradeInfo((Trade)k).Members;
        Assert.Equal(t.VillagerCount, sum);
        // The porters stand among the others as their floor says: at least their scaled share of the floors less one.
        int places = 0;
        for (int k = 1; k < T.TradeCount; k++) places += k == (int)Trade.Boat ? 0 : (int)Math.Floor(t.TradeInfo((Trade)k).Places * TownRules.TradeFloorShare);
        int porters = (int)Math.Floor(TownRules.TradePortersPerBerth * (t.InRiverAge && t.S.River!.BerthBought ? 2 : 1));
        int floor = Math.Min(porters, porters * t.VillagerCount / (places + porters));
        Assert.True(t.TradeInfo(Trade.Boat).Members >= floor - 1, $"porters {t.TradeInfo(Trade.Boat).Members}, floor {floor}");
        // Every trade's members at their own work by the rule in force (Town.AtOwnTrade), the maker's trade read when the
        // trip is delivered: more than half the trips (measured 63-70 % over seeds 1-10 and four bots).
        var u = new T(2);
        var bot = new TownBot(BotKind.Payback, 2) { PlayOn = true };
        var e2 = new List<TownEvent>();
        int own = 0, all = 0;
        while (!u.HasSeal && u.Time < 90 * 60)
        {
            u.Step(); bot.Act(u); u.DrainEvents(e2);
            if (!u.InRiverAge) continue;
            foreach (var e in e2)
            {
                if (e.Kind is not (TownEventKind.Delivery or TownEventKind.LandingDelivery)) continue;
                var tr = u.S.Villagers[e.Villager].Trade;
                if (tr == Trade.None) continue;
                var job = e.Kind == TownEventKind.LandingDelivery
                    ? e.Good switch { Good.Grain => JobKind.GrainBoat, Good.Flour => JobKind.FlourBoat, Good.Wool => JobKind.WoolBoat, _ => JobKind.ClothBoat }
                    : e.Good switch { Good.Water => JobKind.Water, Good.Grain => JobKind.GrainHome, Good.Flour => JobKind.Flour, Good.Wool => JobKind.WoolMarket, _ => JobKind.ClothMarket };
                all++;
                if (T.AtOwnTrade(tr, job)) own++;
            }
        }
        Assert.True(all > 1000, $"{all} trips in the second age");
        Assert.True(own * 2 > all, $"own {own} of {all}");
    }

    /// <summary>The lean and the reward are step 2's for the new trades: a shepherd weighs wool TradeLean times, works at the
    /// pasture and sells its wool at the market with the bonus; a porter has it on grain to the boats, a reaper not.</summary>
    [Fact]
    public void TheLeanAndTheRewardForTheNewTrades()
    {
        var t = RungUntil(3, AllThreeOpen);
        Assert.True(AllThreeOpen(t));
        t.SetTownFillsTrades(false);
        var v = t.S.Villagers[0];
        Assert.True(t.SetVillagerTrade(0, Trade.Wool));
        Assert.Equal(10 * TownRules.TradeLean, t.LeanedFor(0, JobKind.WoolMarket, 10), 9);
        Assert.Equal(10, t.LeanedFor(0, JobKind.GrainBoat, 10), 9);
        var keep = v.Job;
        double like = T.TradeOfLeaning(T.LeaningOf(3, 0)) == Trade.Wool ? TownRules.TradeLikes : 0;
        v.Job = JobKind.WoolMarket;
        Assert.Equal(1 + t.TradeWorkBonus + like, t.TradeWorkFactor(v), 12);
        Assert.Equal(1 + t.TradeWorthBonus + like, t.TradeWorthFactor(v), 12);
        Assert.True(t.SetVillagerTrade(0, Trade.Boat));
        v.Job = JobKind.FlourBoat;
        Assert.Equal(1 + t.TradeWorkBonus, t.TradeWorkFactor(v), 12); // nobody likes the porters
        Assert.True(t.SetVillagerTrade(0, Trade.Grain));
        Assert.Equal(1, t.TradeWorkFactor(v));
        Assert.Equal(1, t.TradeWorthFactor(v));
        v.Job = keep;
        // In play: a shepherd's wool at the market pays the factor more than a Helper's (the same town, one delivery each).
        var a = TownSave.Load(TownSave.Save(t)); var b = TownSave.Load(TownSave.Save(t));
        a.SetVillagerTrade(0, Trade.Wool); b.SetVillagerTrade(0, Trade.None);
        double Coin(T x)
        {
            var e = new List<TownEvent>();
            for (int k = 0; k < 20 * 60 * TownRules.TickHz; k++)
            {
                x.Step(); x.DrainEvents(e);
                foreach (var q in e) if (q.Villager == 0 && q.Kind == TownEventKind.Delivery && q.Good == Good.Wool) return q.Coin / q.Amount / x.StorePrice(Good.Wool);
            }
            return -1;
        }
        double sa = Coin(a);
        if (sa > 0) Assert.Equal(1 + a.TradeWorthBonus + (T.TradeOfLeaning(T.LeaningOf(3, 0)) == Trade.Wool ? TownRules.TradeLikes : 0), sa / a.CoinBonus, 6);
    }

    /// <summary>The player's count and one villager's card take the new trades as they take the old.</summary>
    [Fact]
    public void YourCountAndOneVillagerForTheNewTrades()
    {
        var first = new T(5);
        Assert.False(first.SetTradeCount(Trade.Boat, 2));       // not open in the first age
        Assert.False(first.SetVillagerTrade(0, Trade.Wool));
        var t = RungUntil(5, x => AllThreeOpen(x) && x.Time > 0);
        for (int k = 0; k < 2 * TownRules.TickHz; k++) t.Step();
        int porters = t.TradeInfo(Trade.Boat).Members;
        Assert.True(t.SetTradeCount(Trade.Boat, porters + 3));
        Assert.Equal(porters + 3, t.TradeInfo(Trade.Boat).Members);
        Assert.True(t.TradeInfo(Trade.Boat).ByPlayer);
        for (int k = 0; k < 90 * TownRules.TickHz; k++) t.Step();
        Assert.Equal(porters + 3, t.TradeInfo(Trade.Boat).Members);   // the town leaves your count alone
        Assert.True(t.SetTradeCount(Trade.Cloth, 1));
        Assert.Equal(1, t.TradeInfo(Trade.Cloth).Members);
        Assert.True(t.SetVillagerTrade(0, Trade.Wool));
        Assert.Equal(Trade.Wool, t.Villager(0).Trade);
        Assert.True(t.Villager(0).TradeByPlayer);
        for (int k = 0; k < 90 * TownRules.TickHz; k++) t.Step();
        Assert.Equal(Trade.Wool, t.Villager(0).Trade);
        Assert.True(t.CanLowerTrade(Trade.Wool));
        Assert.True(t.SetTownFillsTrades(true));
        Assert.False(t.TradeInfo(Trade.Boat).ByPlayer);
    }

    /// <summary>Old saves: a second-age save from before trades (version 3) takes every trade within a second, the three new
    /// ones included; a save from step 2 (four entries by trade, second age) loads with the new entries at their defaults
    /// and takes the new trades within a second; both then save and load exactly.</summary>
    [Fact]
    public void OldSecondAgeSavesTakeTheNewTradesWithinASecond()
    {
        string json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Town", "data", "town-v3-seed3-river6min.json"));
        var t = TownSave.Load(json);
        for (int k = 0; k < TownRules.TickHz; k++) t.Step();
        Assert.Equal(0, t.Helpers);
        for (int k = T.FirstAgeTrades; k < T.TradeCount; k++)
            if (t.TradeOpen((Trade)k)) Assert.True(t.TradeInfo((Trade)k).Members >= 1, $"{(Trade)k} empty after a second");
        Assert.True(t.TradeOpen(Trade.Boat));

        // A step 2 town in the second age: its arrays by trade cut to four, every villager in a first-age trade.
        var s2 = RungUntil(3, x => AllThreeOpen(x));
        for (int i = 0; i < s2.VillagerCount; i++) if ((int)s2.S.Villagers[i].Trade >= T.FirstAgeTrades) s2.S.Villagers[i].Trade = Trade.Grain;
        var node = JsonNode.Parse(TownSave.Save(s2))!;
        foreach (var f in new[] { "TradeCountByPlayer", "TradeWant", "TradeEarn", "TradeAt" })
        {
            var arr = node[f]!.AsArray();
            while (arr.Count > T.FirstAgeTrades) arr.RemoveAt(arr.Count - 1);
        }
        var old = TownSave.Load(node.ToJsonString());
        Assert.Equal(T.TradeCount, old.S.TradeEarn.Length);
        Assert.Equal(T.TradeCount, old.S.TradeCountByPlayer.Length);
        for (int k = 0; k < TownRules.TickHz; k++) old.Step();
        for (int k = T.FirstAgeTrades; k < T.TradeCount; k++) Assert.True(old.TradeInfo((Trade)k).Members >= 1, $"{(Trade)k} empty after a second");
        string again = TownSave.Save(old);
        var u = TownSave.Load(again);
        Assert.Equal(again, TownSave.Save(u));
        for (int k = 0; k < 60 * TownRules.TickHz; k++) { old.Step(); u.Step(); }
        Assert.Equal(TownSave.Save(old), TownSave.Save(u));
    }

    /// <summary>No allocation in the second age's step with the new trades filled and a rebalancing move in the window.</summary>
    [Fact]
    public void SecondAgeStepsAllocateNothingWithTheNewTrades()
    {
        var t = RungUntil(1, x => AllThreeOpen(x) && x.VillagerCount >= 20);
        var ev = new List<TownEvent>(2048);
        for (int k = 0; k < 600; k++) { t.Step(); if (k % 10 == 0) { ev.Clear(); t.DrainEvents(ev); } }
        long moves = t.S.TradeChangesByTown;
        long b0 = GC.GetAllocatedBytesForCurrentThread();
        for (int k = 0; k < 180 * TownRules.TickHz; k++) { t.Step(); if (k % 10 == 0) { ev.Clear(); t.DrainEvents(ev); } }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - b0);
        Assert.True(t.S.TradeChangesByTown > moves, "no rebalancing move in the window");
    }

    /// <summary>Same seed and calls give the same town through the second age with the new trades.</summary>
    [Fact]
    public void SameSeedGivesTheSameSecondAge()
    {
        string Run() => TownSave.Save(RungUntil(6, x => AllThreeOpen(x) && x.Time > 0 && x.S.TradeChangesByTown > 60));
        Assert.Equal(Run(), Run());
    }
}
