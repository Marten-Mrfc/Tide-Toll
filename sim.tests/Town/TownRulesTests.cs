using TideToll.Sim.Testing;
using TideToll.Sim.Town;
using T = TideToll.Sim.Town.Town;

namespace TideToll.Sim.Tests.Town;

public class TownRulesTests
{
    private static void Play(T t, TownBot bot, int seconds, Action<T, List<TownEvent>>? each = null)
    {
        var ev = new List<TownEvent>();
        for (int k = 0; k < seconds * TownRules.TickHz; k++)
        {
            t.Step();
            bot.Act(t);
            if (each != null) { t.DrainEvents(ev); each(t, ev); }
        }
    }

    [Fact]
    public void SameSeedSameActionsSameTown()
    {
        var a = new T(3); var b = new T(3);
        Play(a, new TownBot(BotKind.Random, 3), 300);
        Play(b, new TownBot(BotKind.Random, 3), 300);
        Assert.Equal(TownSave.Save(a), TownSave.Save(b));
        Assert.True(a.Purchases > 5);
    }

    [Theory]
    [InlineData(BotKind.Cheapest)]
    [InlineData(BotKind.Payback)]
    public void SaveRoundTripContinuesExactly(BotKind kind)
    {
        var a = new T(2);
        var botA = new TownBot(kind, 2);
        Play(a, botA, 7 * 60);
        string json = TownSave.Save(a);
        var b = TownSave.Load(json);
        Assert.Equal(json, TownSave.Save(b));
        Play(a, botA, 5 * 60);
        Play(b, new TownBot(kind, 2), 5 * 60);
        Assert.Equal(TownSave.Save(a), TownSave.Save(b));
        Assert.True(a.Trips > 20);
    }

    [Fact]
    public void LoadRefusesAnotherVersion()
    {
        // Rules v2: the save version is 2 (was 1); another version, version 1 included, is refused.
        string json = TownSave.Save(new T(1));
        Assert.Contains("\"Version\":2", json);
        Assert.Throws<InvalidOperationException>(() => TownSave.Load(json.Replace("\"Version\":2", "\"Version\":99")));
        Assert.Throws<InvalidOperationException>(() => TownSave.Load(json.Replace("\"Version\":2", "\"Version\":1")));
    }

    [Fact]
    public void CoinComesOnlyFromDeliveriesAndPricesNeverFall()
    {
        var t = new T(4);
        var bot = new TownBot(BotKind.Random, 4);
        double coin = t.Coin, delivered = 0, spent = 0, rewards = 0;
        double vp = t.VillagerPrice, pp = t.PacePrice, lp = t.LoadPrice;
        var items = new List<ShopItem>();
        var ev = new List<TownEvent>();
        for (int k = 0; k < 20 * 60 * TownRules.TickHz; k++)
        {
            t.Step();
            bot.Act(t);
            t.DrainEvents(ev);
            foreach (var e in ev)
            {
                if (e.Kind == TownEventKind.Delivery) { Assert.True(e.Coin > 0); delivered += e.Coin; }
                if (e.Kind == TownEventKind.Purchase) spent += e.Coin;
                if (e.Kind == TownEventKind.RequestMet) { Assert.True(e.Coin > 0); rewards += e.Coin; } // rules v2: request rewards
            }
            // Rules v2: a tolerance instead of rounding to 6 places (a sum landing on ...xxx5 rounded the two sides apart).
            Assert.True(Math.Abs(coin + delivered + rewards - spent - t.Coin) < 1e-6, $"coin {t.Coin} vs {coin + delivered + rewards - spent}");
            Assert.True(t.VillagerPrice >= vp && t.PacePrice >= pp && t.LoadPrice >= lp, "a price fell");
            vp = t.VillagerPrice; pp = t.PacePrice; lp = t.LoadPrice;
        }
        Assert.Equal(delivered, t.CoinEarned, 6);
        Assert.Equal(rewards, t.RequestCoin, 6);
        Assert.True(delivered > 0);
    }

    [Fact]
    public void HousingLimitsVillagers()
    {
        var t = new T(5);
        var ev = new List<TownEvent>();
        for (int k = 0; k < 15 * 60 * TownRules.TickHz; k++)
        {
            t.Step();
            while (t.BuyVillager()) { }
            t.BuyPace();
            Assert.True(t.VillagerCount <= t.Housing);
        }
        Assert.Equal(t.Housing, t.VillagerCount);
        Assert.False(t.BuyVillager());
    }

    [Fact]
    public void PavingOnlyWhenWornIn()
    {
        var t = new T(6);
        var ev = new List<TownEvent>();
        var bot = new TownBot(BotKind.Cheapest, 6);
        bool pavedSome = false;
        for (int k = 0; k < 20 * 60 * TownRules.TickHz; k++)
        {
            t.Step();
            for (int r = 0; r < t.RoadCount; r++)
                if (!t.RoadInfo(r).WornIn) Assert.False(t.CanPave(r, out _));
            // Try to pave a road that is not worn in, with coin to spare.
            for (int r = 0; r < t.RoadCount; r++)
                if (t.RoadOpen(r) && !t.RoadInfo(r).WornIn) Assert.False(t.Pave(r));
            bot.Act(t);
            t.DrainEvents(ev);
            foreach (var e in ev)
                if (e.Kind == TownEventKind.RoadPaved) { Assert.True(t.RoadInfo(e.Index).WornIn); pavedSome = true; }
        }
        Assert.True(pavedSome);
    }

    [Fact]
    public void RenownAndGiftsArriveInOrder()
    {
        var t = new T(8);
        var bot = new TownBot(BotKind.Cheapest, 8);
        int last = 1;
        Play(t, bot, 50 * 60, (town, ev) =>
        {
            foreach (var e in ev)
            {
                if (e.Kind != TownEventKind.LevelReached || e.Level == 1) continue; // level 1 is announced at the start
                Assert.Equal(last + 1, e.Level);
                last = e.Level;
                var lv = town.Plan.Level(e.Level);
                Assert.True(town.Renown >= lv.Renown);
                Assert.Equal(lv.Gift, e.Text);
                foreach (int p in lv.Plots) Assert.True(town.PlotGiven(p));
                foreach (int r in lv.Roads) Assert.True(town.RoadOpen(r));
                foreach (int o in lv.Offers) Assert.True(town.OfferAvailable(o));
            }
            // Nothing from a later level is given early.
            for (int r = 0; r < town.RoadCount; r++) if (town.Plan.Roads[r].Level > town.Level) Assert.False(town.RoadOpen(r));
        });
        Assert.Equal(t.MaxLevel, last);
        Assert.True(t.HasCharter);
        Assert.True(t.RingNextAge());
        Assert.False(t.RingNextAge());
        Play(t, bot, 30); // the town stays valid after the hook
    }

    public static IEnumerable<object[]> BotsAndSeeds()
    {
        foreach (BotKind k in Enum.GetValues<BotKind>()) yield return new object[] { k };
    }

    [Theory]
    [MemberData(nameof(BotsAndSeeds))]
    public void VillagersStayOnRoadsAndNeverGetStuck(BotKind kind)
    {
        for (int seed = 1; seed <= 10; seed++)
        {
            var t = new T(seed);
            var bot = new TownBot(kind, seed);
            var lastChange = new List<double>();
            var lastState = new List<(double, double, VillagerPhase)>();
            for (int k = 0; k < 60 * 60 * TownRules.TickHz; k++)
            {
                t.Step();
                bot.Act(t);
                while (lastChange.Count < t.VillagerCount) { lastChange.Add(t.Time); lastState.Add((0, 0, VillagerPhase.Idle)); }
                if (k % 7 != 0) continue;
                for (int i = 0; i < t.VillagerCount; i++)
                {
                    var v = t.Villager(i);
                    // Rules v2 (decision 7): working villagers stand at their work spot in or beside the workplace, idle ones at home
                    // at the door or in the yard; everyone else stays on the roads.
                    if (v.Phase is VillagerPhase.Working or VillagerPhase.Idle && !OnSomeRoad(t, v.X, v.Y))
                        Assert.True(NearPlot(t, v.Phase == VillagerPhase.Working ? t.Workplace(v.Workplace).Plot : v.Home, v.X, v.Y), $"seed {seed} {kind}: villager {i} ({v.Phase}) at {v.X:F2},{v.Y:F2} is not at its plot");
                    else Assert.True(OnSomeRoad(t, v.X, v.Y), $"seed {seed} {kind}: villager {i} off the roads at {v.X:F2},{v.Y:F2} ({v.Phase})");
                    if (v.Phase == VillagerPhase.Walking)
                    {
                        Assert.True(t.RoadOpen(v.Road));
                        t.Plan.Roads[v.Road].At(v.Dist, out double x, out double y, out _);
                        Assert.True(Math.Abs(x - v.X) < 1e-9 && Math.Abs(y - v.Y) < 1e-9);
                    }
                    var s = (v.X, v.Y, v.Phase);
                    if (s != lastState[i]) { lastState[i] = s; lastChange[i] = t.Time; }
                    Assert.True(t.Time - lastChange[i] < 90, $"seed {seed} {kind}: villager {i} stuck since {lastChange[i]:F0} s ({v.Phase})");
                }
            }
            Assert.True(t.Trips > 100, $"seed {seed} {kind}: only {t.Trips} trips");
        }
    }

    private static bool OnSomeRoad(T t, double x, double y)
    {
        for (int r = 0; r < t.RoadCount; r++)
        {
            if (!t.RoadOpen(r)) continue;
            var rd = t.Plan.Roads[r];
            for (int s = 0; s + 1 < rd.Xs.Length; s++)
                if (SegDist(x, y, rd.Xs[s] + 0.5, rd.Ys[s] + 0.5, rd.Xs[s + 1] + 0.5, rd.Ys[s + 1] + 0.5) < 1e-6) return true;
        }
        // At a node with no open road yet (never expected after level 1).
        return false;
    }

    private static bool NearPlot(T t, int plot, double x, double y)
    {
        var p = t.Plan.Plots[plot];
        return x >= p.X - 1.2 && y >= p.Y - 1.2 && x <= p.X + p.W + 1.2 && y <= p.Y + p.H + 1.2;
    }

    private static double SegDist(double px, double py, double ax, double ay, double bx, double by)
    {
        double dx = bx - ax, dy = by - ay, l2 = dx * dx + dy * dy;
        double u = l2 > 0 ? Math.Clamp(((px - ax) * dx + (py - ay) * dy) / l2, 0, 1) : 0;
        double qx = ax + u * dx - px, qy = ay + u * dy - py;
        return Math.Sqrt(qx * qx + qy * qy);
    }

    [Fact]
    public void BellHurriesThenRests()
    {
        var t = new T(1);
        Assert.False(t.RingBell()); // not before level 2
        var bot = new TownBot(BotKind.Cheapest, 1);
        while (t.Level < 2) { t.Step(); bot.Act(t); }
        // the bot rings it when ready; wait for it to be ready and ring by hand
        while (!t.BellReady) t.Step();
        Assert.True(t.RingBell());
        Assert.True(t.BellRinging);
        Assert.False(t.RingBell());
        for (int k = 0; k < TownRules.BellDuration * TownRules.TickHz + 1; k++) t.Step();
        Assert.False(t.BellRinging);
    }

    [Fact]
    public void OneGameMinuteAtTheEndOfTheAgeIsCheap()
    {
        var t = new T(1);
        var bot = new TownBot(BotKind.Cheapest, 1);
        while (!t.HasCharter && t.Time < 60 * 60) { t.Step(); bot.Act(t); }
        for (int k = 0; k < 600; k++) t.Step(); // warm up
        double best = double.MaxValue;
        for (int rep = 0; rep < 3; rep++)
        {
            double t0 = CpuClock.NowMs();
            for (int k = 0; k < 60 * TownRules.TickHz; k++) t.Step();
            best = Math.Min(best, CpuClock.NowMs() - t0);
        }
        Assert.True(best < 10, $"one game minute took {best:F2} ms of CPU with {t.VillagerCount} villagers");
        // Once events are drained regularly, stepping allocates nothing.
        var ev = new List<TownEvent>(256);
        t.DrainEvents(ev);
        long b0 = GC.GetAllocatedBytesForCurrentThread();
        for (int k = 0; k < 60 * TownRules.TickHz; k++) { t.Step(); if (k % 10 == 0) { ev.Clear(); t.DrainEvents(ev); } }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - b0);
    }
}
