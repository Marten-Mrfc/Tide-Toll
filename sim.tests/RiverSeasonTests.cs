using TideToll.Sim.Testing;
using static TideToll.Sim.Tests.RiverBoatTests;
using static TideToll.Sim.Tests.RiverCrossingTests;
using static TideToll.Sim.Tests.RiverWalkerTests;

namespace TideToll.Sim.Tests;

/// <summary>
/// Spring high water and winter ice (doc, Gameplay: "Spring high water", "Winter ice", "Seasons and weather"): when they
/// come, what they close and open, the flood band, mud, the ice-wear layer, and the river's own weather stream.
/// </summary>
public class RiverSeasonTests
{
    private static readonly RiverRules Calm = Rich with { HaulGoods = true, SpringFlood = false, WinterIce = false };
    private static int TicksPerDay(World w) => w.Params.TicksPerDay;

    private static List<int> FordCells(World w)
    {
        var l = new List<int>();
        for (int c = 0; c < w.Grid.Count; c++) if (w.Map.Ford[c] != 0) l.Add(c);
        return l;
    }

    [Fact]
    public void AFloodComesOnTheFirstDayOfSpringWithTheDocsChanceForOneOrTwoDays()
    {
        int floods = 0, one = 0, two = 0, seeds = 120;
        for (int seed = 1; seed <= seeds; seed++)
        {
            // The clock starts on the first day of spring. A new town never floods in its first spring (RiverRuleFixTests);
            // the chance is measured there here, with that rule switched off, instead of ticking a year on 120 seeds.
            var w = Make(seed, 0, NoSchedule with { FloodInFirstSpring = true });
            if (!w.River!.Flooded) { Assert.False(Drain(w).Any(e => e.Kind == RiverEventKind.FloodRising)); continue; }
            floods++;
            var ev = Drain(w);
            Assert.Contains(ev, e => e.Kind == RiverEventKind.FloodRising);
            Assert.Contains(ev, e => e.Kind == RiverEventKind.FordClosed && e.Crossing == 0);
            Assert.False(w.River.FordOpen);
            double days = Math.Round(w.River.FloodEndsAt / w.Params.DaySimSec);
            if (days == 1) one++; else if (days == 2) two++; else Assert.Fail($"seed {seed}: a flood of {days} days");
        }
        double share = (double)floods / seeds;
        Assert.InRange(share, 0.58, 0.82); // 70% (doc, Riverbend); 120 fixed seeds
        Assert.True(one > floods / 4 && two > floods / 4, $"lengths: {one} of one day, {two} of two");
    }

    [Fact]
    public void TheFloodClosesTheFordSlowsTheBandAndLeavesMudButBridgesWork()
    {
        var w = Make(2, 300, Calm);
        var r = w.River!;
        var fb = Build(w, CrossingType.Footbridge);
        ToyHarness.Run(w, 2);
        var ford = r.Crossings[0];
        var fordCells = FordCells(w);
        int band = -1;
        for (int c = 0; c < w.Grid.Count && band < 0; c++) if (r.FloodBand[c] != 0 && w.Map.Ford[c] == 0 && w.Map.Footprint[c] == 0 && w.Map.Crop[c] == 0) band = c;
        Assert.True(band >= 0);
        double bandCost = w.CellCost(band);
        Assert.True(w.CellCost(fordCells[0]) < double.PositiveInfinity && !w.IsBlocked(fordCells[0]));
        Drain(w);
        int version = w.Version;
        Assert.True(w.StartFlood(1).Ok);
        Assert.True(r.Flooded && !r.FordOpen);
        Assert.Equal(version, w.Version); // not a player's edit: the game's edit handlers do not run
        // The ford is closed; the band is waded at three times the cost.
        foreach (int c in fordCells) { Assert.True(w.IsBlocked(c)); Assert.True(double.IsPositiveInfinity(w.CellCost(c))); }
        Assert.Equal(3 * bandCost, w.CellCost(band), 6);
        var wearBefore = w.Wear.ToArray();
        int fordCrossed = ford.WalkersCrossed, bridgeCrossed = fb.WalkersCrossed;
        for (int t = 0; t < TicksPerDay(w) - 1; t++)
        {
            w.Tick();
            Assert.True(r.Flooded);
            foreach (var k in w.Walkers) if (k.Visible) Assert.False(w.Map.Ford[k.Cell] != 0, $"walker {k.Id} on the ford in a flood");
        }
        Assert.Equal(fordCrossed, ford.WalkersCrossed);
        Assert.True(fb.WalkersCrossed > bridgeCrossed + 10, $"the footbridge carried {fb.WalkersCrossed - bridgeCrossed} in the flood");
        // Flood water does not wear: no band cell gained wear (it only decays).
        var wearNow = w.Wear;
        for (int c = 0; c < w.Grid.Count; c++) if (r.FloodBand[c] != 0) Assert.True(wearNow[c] <= wearBefore[c], $"band cell {c} wore in the flood");
        // Then it recedes after one day: ford open, mud for the rest of spring on worn band cells.
        w.Tick();
        var ev = Drain(w);
        Assert.Contains(ev, e => e.Kind == RiverEventKind.FloodReceded);
        Assert.Contains(ev, e => e.Kind == RiverEventKind.FordOpen && e.Crossing == 0);
        Assert.True(!r.Flooded && r.FordOpen && r.Muddy);
        Assert.False(w.IsBlocked(fordCells[0]));
        AssertRiverInvariants(w, "after the flood");
    }

    [Fact]
    public void MudMultipliesTheCostOfWornBandCellsUntilSummer()
    {
        var w = Make(3, 300, Calm);
        var r = w.River!;
        var p = w.Params;
        ToyHarness.Run(w, 1);
        w.StartFlood(0.2);
        while (w.Ticks < p.TicksPerSeason - 1) w.Tick(); // to the last tick of spring
        Assert.True(r.Muddy);
        // Worn band cells too quiet for the crowding term: cost now (mud) against cost one tick later (summer, dry).
        var cells = new List<(int Cell, double Mul, double Cost, float Wear, float Flow)>();
        for (int c = 0; c < w.Grid.Count; c++)
        {
            if (r.FloodBand[c] == 0 || w.IsBlocked(c) || w.Map.Ford[c] != 0 || w.Occupancy[c] > 0.19) continue; // no crowding term
            double mul = p.GroundOf(w.Wear[c], w.Hollow[c] == 1) switch { Ground.Trail => 1.6, Ground.Track => 1.7, Ground.Hollow => 1.8, _ => 1.0 };
            cells.Add((c, mul, w.CellCost(c), w.Wear[c], w.Flow[c]));
        }
        Assert.Contains(cells, q => q.Mul > 1);
        w.Tick();
        Assert.Equal(1, w.Stats().Season);
        Assert.False(r.Muddy);
        int compared = 0;
        foreach (var (c, mul, cost, wear, flow) in cells)
        {
            if (w.Occupancy[c] > 0.19) continue;
            // A footstep in that one tick (wear and flow only ever fall otherwise) changed the dry ground under the mud.
            if (w.Wear[c] > wear || w.Flow[c] > flow) continue;
            double ratio = cost / w.CellCost(c);
            Assert.True(Math.Abs(ratio - mul) < 0.03 * mul, $"cell {c}: mud x{ratio:F3}, doc x{mul}");
            compared++;
        }
        Assert.True(compared > 10, $"{compared} cells compared");
    }

    [Fact]
    public void BuildingsInTheBandPayRepairsEachFlood()
    {
        var w = Make(4, 0, Calm with { FloodBandCells = 30 });
        var r = w.River!;
        int inBand = 0;
        foreach (var b in w.Buildings)
        {
            if (b.Kind == BuildingKind.Landing) continue;
            bool hit = r.FloodBand[b.Door] != 0;
            for (int y = b.Y; y < b.Y + b.H && !hit; y++)
                for (int x = b.X; x < b.X + b.W && !hit; x++) hit = r.FloodBand[y * w.Grid.Width + x] != 0;
            if (hit) inBand++;
        }
        Assert.True(inBand > 0, "no building in a 30-cell band");
        double m = r.Money;
        Drain(w);
        w.StartFlood(1);
        Assert.Equal(m - 10 * inBand, r.Money, 9);
        Assert.Contains(Drain(w), e => e.Kind == RiverEventKind.FloodRepairs && e.Value == 10 * inBand);
    }

    [Fact]
    public void TheWeatherHasItsOwnStreamAndNeverMovesTheBoatsSchedule()
    {
        var a = Make(7, 100, RiverRules.Default with { FloodChance = 1, IceChance = 1, WinterIce = false });
        var b = Make(7, 100, RiverRules.Default with { SpringFlood = false, WinterIce = false });
        for (int t = 0; t < 6 * 150; t++)
        {
            a.Tick(); b.Tick();
            Assert.Equal(b.River!.NextArrival(BoatKind.Barge), a.River!.NextArrival(BoatKind.Barge));
        }
        Assert.True(a.River!.Stats.Upkeep >= 0 && b.River!.Stats.Arrived.Sum() == a.River.Stats.Arrived.Sum());
    }

    [Fact]
    public void TheRiverFreezesOnTheSecondDayOfWinterByChanceAndThawsOnTheFirstDayOfSpring()
    {
        int frozen = 0, seeds = 40;
        for (int seed = 1; seed <= seeds; seed++)
        {
            var w = Make(seed, 0, NoSchedule with { SpringFlood = false });
            int tpd = TicksPerDay(w);
            for (int t = 0; t < 13 * tpd - 1; t++) { w.Tick(); Assert.False(w.River!.Frozen, $"seed {seed}: frozen before winter day 2 (tick {t})"); }
            w.Tick(); // the clock reaches day 14, the second day of winter
            Assert.Equal(13L * tpd, w.Ticks); // days 13 to 16 are winter (0-based 12 to 15)
            if (!w.River!.Frozen) continue;
            frozen++;
            Assert.Contains(Drain(w), e => e.Kind == RiverEventKind.RiverFrozen);
            for (int t = 0; t < 3 * tpd - 1; t++) w.Tick();
            Assert.True(w.River.Frozen);
            w.Tick(); // the clock reaches the first day of the next spring
            Assert.Equal(16L * tpd, w.Ticks);
            Assert.False(w.River.Frozen);
            Assert.Contains(Drain(w), e => e.Kind == RiverEventKind.Thaw);
        }
        Assert.InRange((double)frozen / seeds, 0.3, 0.7); // 50% (doc, Hamlet)
    }

    [Fact]
    public void OnTheIceBoatsStopTradePausesAndWalkersCrossAnywhereOnASeparateWearLayer()
    {
        var rules = RiverRules.Default with { SpringFlood = false, WinterIce = false, StartMoney = 1000, RowboatGapSec = 4, BargeGapSec = 3 };
        var w = Make(2, 300, rules);
        var r = w.River!;
        var ferry = Build(w, CrossingType.Ferry);
        var draw = Build(w, CrossingType.Drawbridge, 9);
        ToyHarness.Run(w, 2);
        Assert.True(r.Boats.Length > 0);
        var boatsBefore = r.Boats.ToArray();
        int arrived = r.Stats.Arrived.Sum(), toBoats = r.Stats.GrainToBoats, market = r.Stats.GrainToMarket;
        var wearBefore = w.Wear.ToArray();
        Drain(w);
        int version = w.Version;
        Assert.True(w.Freeze().Ok);
        Assert.Equal(version, w.Version);
        Assert.Equal("The river is frozen: no boat can pass", w.SetBridge(draw.Id, true).Reason);
        Assert.Equal(0, ferry.Passengers);
        var water = Enumerable.Range(0, w.Grid.Count).Where(c => w.Map.Water[c] != 0 && w.BridgeAt[c] < 0).ToList();
        Assert.All(water, c => Assert.False(w.IsBlocked(c)));
        var ice = new float[w.Grid.Count];
        for (int t = 0; t < 2 * TicksPerDay(w); t++)
        {
            w.Tick();
            // Ice wear never decays while frozen.
            var iw = r.IceWear;
            foreach (int c in water) { Assert.True(iw[c] >= ice[c] - 1e-6f); ice[c] = iw[c]; }
        }
        // Boats held fast: same place, same patience and mooring clocks, nothing new arrived, nobody bought.
        var boatsAfter = r.Boats.ToArray();
        Assert.Equal(boatsBefore.Length, boatsAfter.Length);
        for (int i = 0; i < boatsBefore.Length; i++)
        {
            Assert.Equal(boatsBefore[i].Id, boatsAfter[i].Id);
            Assert.Equal(boatsBefore[i].Y, boatsAfter[i].Y);
            Assert.Equal(boatsBefore[i].PatienceLeft, boatsAfter[i].PatienceLeft);
            Assert.Equal(boatsBefore[i].MoorLeft, boatsAfter[i].MoorLeft);
        }
        Assert.Equal(arrived, r.Stats.Arrived.Sum());
        Assert.Equal(toBoats, r.Stats.GrainToBoats);
        Assert.True(r.Stats.GrainToMarket > market, "grain still sells at the market");
        // Walkers cross on the ice, and their paths wear the ice layer only.
        Assert.True(r.Stats.IceCrossings > 20, $"{r.Stats.IceCrossings} crossings on the ice");
        Assert.True(water.Max(c => r.IceWear[c]) > 25, "no path across the ice");
        Assert.All(water, c => Assert.Equal(0f, w.Wear[c]));
        Assert.Equal(0, ferry.Passengers);
        AssertRiverInvariants(w, "on the ice");
        // The thaw wipes the ice layer and sets everyone on a bank; boats sail on.
        Assert.True(w.Thaw().Ok);
        Assert.All(water, c => Assert.Equal(0f, r.IceWear[c]));
        AssertRiverInvariants(w, "after the thaw");
        foreach (var k in w.Walkers) if (k.Visible && k.Hold != WalkerHold.Riding) Assert.False(w.Map.Water[k.Cell] != 0 && w.Map.Ford[k.Cell] == 0 && w.BridgeAt[k.Cell] < 0);
        ToyHarness.Run(w, 1);
        Assert.True(r.Stats.Arrived.Sum() > arrived);
    }

    [Fact]
    public void ATickWithTheFloodAndTheIceAllocatesNothing()
    {
        var w = Make(3, 500, Rich with { HaulGoods = true, KeeperUnlockRaises = 0 });
        Build(w, CrossingType.Ferry);
        int d = Build(w, CrossingType.Drawbridge, 9).Id;
        Assert.True(w.HireKeeper(d).Ok);
        var events = new List<RiverEvent>(1024);
        for (int i = 0; i < 300; i++) { w.Tick(); w.River!.DrainEvents(events); events.Clear(); }
        w.StartFlood(1); w.Freeze();
        for (int i = 0; i < 300; i++) { w.Tick(); w.River!.DrainEvents(events); events.Clear(); }
        w.Thaw(); w.Freeze();
        long a0 = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 300; i++) { w.Tick(); w.River!.DrainEvents(events); events.Clear(); }
        w.Thaw();
        w.StartFlood(0.5);
        for (int i = 0; i < 300; i++) { w.Tick(); w.River!.DrainEvents(events); events.Clear(); }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - a0);
    }
}
