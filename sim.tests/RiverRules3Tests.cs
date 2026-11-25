using TideToll.Sim.Testing;
using static TideToll.Sim.Tests.RiverBoatTests;
using static TideToll.Sim.Tests.RiverCrossingTests;

namespace TideToll.Sim.Tests;

/// <summary>
/// River rules, third pass (the lead's decisions B, C and D, 25 Nov 2026): grain carried through the ford gets wet and
/// sells for less, and a walker carrying grain wades more slowly; the keeper is unlocked by raising that drawbridge by
/// hand; a small starting purse; the ferryman's wage and the ferry's upkeep at the doc's numbers.
/// </summary>
public class RiverRules3Tests
{
    private static void RaiseByHand(World w, Crossing x)
    {
        Assert.True(w.SetBridge(x.Id, true).Ok);
        for (int t = 0; t < 400 && x.State != BridgeState.Closed; t++) w.Tick();
        Assert.Equal(BridgeState.Closed, x.State);
    }

    [Fact]
    public void TheKeeperIsHiredOnlyAfterTheBridgeHasBeenRaisedByHandOftenEnough()
    {
        var w = Make(1, 0, Rich);
        var r = w.River!;
        Assert.Equal(10, r.Rules.KeeperUnlockRaises);
        var x = Build(w, CrossingType.Drawbridge);
        Assert.Equal(0, x.HandRaises);
        Assert.Equal(10, x.RaisesToKeeper);
        Assert.False(x.KeeperUnlocked);
        var no = w.HireKeeper(x.Id);
        Assert.False(no.Ok);
        Assert.Equal("No keeper will take this bridge on yet: raise it by hand ten times more first (zero of ten raises done)", no.Reason);
        double money = r.Money;
        for (int i = 0; i < 3; i++) RaiseByHand(w, x);
        // A second press while it is already going up is not a raise.
        Assert.True(w.SetBridge(x.Id, true).Ok);
        Assert.True(w.SetBridge(x.Id, true).Ok);
        for (int t = 0; t < 400 && x.State != BridgeState.Closed; t++) w.Tick();
        Assert.Equal(4, x.HandRaises);
        Assert.Equal(6, x.RaisesToKeeper);
        Assert.Equal("No keeper will take this bridge on yet: raise it by hand six times more first (four of ten raises done)", w.HireKeeper(x.Id).Reason);
        Assert.Equal(money, r.Money); // a refused hire costs nothing
        Drain(w);
        while (x.RaisesToKeeper > 1) RaiseByHand(w, x);
        Assert.Equal("No keeper will take this bridge on yet: raise it by hand once more first (nine of ten raises done)", w.HireKeeper(x.Id).Reason);
        Assert.DoesNotContain(Drain(w), e => e.Kind == RiverEventKind.KeeperUnlocked);
        RaiseByHand(w, x);
        var unlocked = Drain(w).Where(e => e.Kind == RiverEventKind.KeeperUnlocked).ToList();
        Assert.Single(unlocked);
        Assert.Equal(x.Id, unlocked[0].Crossing);
        Assert.True(x.KeeperUnlocked);
        Assert.Equal(0, x.RaisesToKeeper);
        RaiseByHand(w, x); // raising on after the unlock: no second event
        Assert.DoesNotContain(Drain(w), e => e.Kind == RiverEventKind.KeeperUnlocked);
        Assert.True(w.HireKeeper(x.Id).Ok);
        Assert.True(x.Keeper);
        // The keeper's own raises are not hand raises.
        int hand = x.HandRaises;
        int barge = w.SpawnBoat(BoatKind.Barge).Id;
        bool raised = false;
        for (int t = 0; t < 600 && Exists(w, barge); t++) { w.Tick(); raised |= x.State == BridgeState.Raised; }
        Assert.True(raised, "the keeper never raised the bridge");
        Assert.Equal(hand, x.HandRaises);
        // Each drawbridge learns its own boats: a second one starts locked.
        var y = Build(w, CrossingType.Drawbridge, 12);
        Assert.Equal(10, y.RaisesToKeeper);
        Assert.False(w.HireKeeper(y.Id).Ok);
        // Other crossings have nothing to unlock.
        Assert.Equal(0, r.Crossings[0].RaisesToKeeper);
        Assert.False(r.Crossings[0].KeeperUnlocked);
    }

    [Fact]
    public void TheStartingPurseBuysAFerryOrAFootbridgeAndTheFerryCostsTheDocsNumbers()
    {
        var r = RiverRules.Default;
        Assert.Equal(50, r.StartMoney);
        Assert.True(r.StartMoney >= r.FerryPrice && r.StartMoney >= r.FootbridgePrice, "the purse buys either crossing");
        Assert.True(r.StartMoney < r.DrawbridgePrice);
        Assert.Equal(3, r.FerryWage);   // doc: the ferryman's wage, 3 a season
        Assert.Equal(2, r.FerryUpkeep); // doc: the ferry's upkeep, 2 a season
        foreach (var t in new[] { CrossingType.Ferry, CrossingType.Footbridge })
        {
            var w = new World(1, 100, RiverTestRules.Trail with { River = r });
            Assert.Equal(r.StartMoney, w.River!.Money);
            var res = RiverBot.Build(w, t);
            Assert.True(res.Id >= 0, $"a new town cannot afford a {t}");
            Assert.True(RiverBot.Build(w, CrossingType.Drawbridge).Id < 0);
        }
    }

    [Fact]
    public void GrainCarriedThroughTheFordIsWetAndSellsForLess()
    {
        // No boats, a market that buys everything: every unit is sold at the market. A footbridge as well, so some grain
        // comes over dry. Per walker, the test tracks whether its load has been in the ford since it was picked up.
        var rules = NoSchedule with { HaulGoods = true, StartMoney = 500, MarketUnitsPerHouseDay = 1000, WetGrainValueShare = 0.4 };
        // A town whose ford is within reach of the landing, so walkers use both (the first such seed where both happen).
        World w = null!;
        RiverView r = null!;
        foreach (int seed in Enumerable.Range(1, 16))
        {
            if (Math.Abs(Make(seed, 0, rules).Map.FordY - Make(seed, 0, rules).River!.LandingRow) > 14) continue;
            w = Make(seed, 250, rules);
            Build(w, CrossingType.Footbridge, 8);
            r = w.River!;
            var inFord = new Dictionary<int, bool>();
            for (int t = 0; t < 6 * w.Params.TicksPerDay; t++)
            {
                w.Tick();
                foreach (var k in w.Walkers)
                {
                    if (k.Cargo == 0) { inFord[k.Id] = false; continue; }
                    bool f = inFord.GetValueOrDefault(k.Id) || (k.Visible && !r.Frozen && w.Map.Water[k.Cell] != 0 && w.Map.Ford[k.Cell] != 0);
                    inFord[k.Id] = f;
                    Assert.Equal(f, k.CargoWet);
                }
            }
            if (r.Stats.GrainDeliveredWet > 0 && r.Stats.GrainDeliveredDry > 0) break;
        }
        var s2 = r.Stats;
        Assert.True(s2.GrainDeliveredWet > 0, "no grain came through the ford");
        Assert.True(s2.GrainDeliveredDry > 0, "no grain came over dry");
        Assert.Equal(s2.GrainDelivered, s2.GrainDeliveredWet + s2.GrainDeliveredDry);
        Assert.Equal(0, s2.GrainUnsold);
        double unit = rules.GrainPrice * rules.MarketShare;
        Assert.Equal(s2.GrainToMarket * unit, s2.MarketIncome + s2.WetValueLost, 6);
        Assert.Equal(s2.GrainDeliveredWet * unit * (1 - rules.WetGrainValueShare), s2.WetValueLost, 6);
    }

    [Fact]
    public void WetGrainSellsForLessToABoatToo()
    {
        // The market buys nothing, so every unit sold in the window went to the barge.
        var rules = NoSchedule with { HaulGoods = true, StartMoney = 0, WetGrainValueShare = 0.25, MarketUnitsPerHouseDay = 0 };
        var w = Make(2, 250, rules);
        var r = w.River!;
        ToyHarness.Run(w, 4);
        int wet0 = r.Stats.GrainDeliveredWet, boats0 = r.Stats.GrainToBoats;
        double lost0 = r.Stats.WetValueLost, boatIncome0 = r.Stats.BoatIncome;
        int id = w.SpawnBoat(BoatKind.Barge).Id;
        while (BoatById(w, id).State != BoatState.Mooring) w.Tick();
        while (Exists(w, id) && BoatById(w, id).State == BoatState.Mooring) w.Tick();
        int sold = r.Stats.GrainToBoats - boats0;
        Assert.True(sold > 0, "the barge bought nothing");
        double unit = rules.GrainPrice * rules.BoatValueMul;
        double got = r.Stats.BoatIncome - boatIncome0, lost = r.Stats.WetValueLost - lost0;
        Assert.Equal(sold * unit, got + lost, 6);
        Assert.True(lost > 0 || r.Stats.GrainDeliveredWet == wet0, "wet grain sold to the barge at full price");
    }

    [Fact]
    public void AWalkerCarryingGrainWadesMoreSlowlyOnlyInTheFord()
    {
        var w = Make(1, 0, NoSchedule with { HaulGoods = true });
        int ford = -1, grass = -1;
        for (int c = 0; c < w.Grid.Count && (ford < 0 || grass < 0); c++)
        {
            if (ford < 0 && w.Map.Ford[c] != 0) ford = c;
            if (grass < 0 && w.Land[c] == 1 && w.Map.Ford[c] == 0) grass = c;
        }
        double mul = w.River!.Rules.LoadedWadeSpeedMul;
        Assert.InRange(mul, 0.01, 0.99);
        Assert.Equal(w.SpeedOn(ford) * mul, w.SpeedOn(ford, loaded: true), 12);
        Assert.Equal(w.SpeedOn(grass), w.SpeedOn(grass, loaded: true));
        // On the ice nobody wades.
        w.Freeze();
        Assert.Equal(w.SpeedOn(ford), w.SpeedOn(ford, loaded: true));
        // Without the river rules nobody carries grain and the ford is as the toy has it.
        var plain = new World(1, 0, RiverTestRules.Plain);
        Assert.Equal(plain.SpeedOn(ford), plain.SpeedOn(ford, loaded: true));
    }
}
