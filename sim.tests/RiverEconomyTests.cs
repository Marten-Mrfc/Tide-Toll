using TideToll.Sim.Testing;
using static TideToll.Sim.Tests.RiverBoatTests;

namespace TideToll.Sim.Tests;

/// <summary>Money: the starting purse, prices, refusals with a reason in words, erase costs, and the season's books.</summary>
public class RiverEconomyTests
{
    private static int RowFrom(World w, int dy) => w.River!.LandingRow + dy;

    private static RiverResult PlaceAtFirstRow(World w, CrossingType t, int from = 4)
    {
        RiverResult r = default;
        for (int dy = from; dy < 24; dy++)
        {
            int y = RowFrom(w, dy);
            r = w.PlaceCrossing(t, w.RiverX[y], y + 0.5);
            if (r.Ok || r.Reason.StartsWith("Not enough coin")) return r;
        }
        return r;
    }

    [Fact]
    public void PricesAndRefusalsInWords()
    {
        var w = Make(1, 0);
        var r = w.River!;
        Assert.Equal(120, r.Money);
        var no = PlaceAtFirstRow(w, CrossingType.Drawbridge);
        Assert.False(no.Ok);
        Assert.Equal("Not enough coin: the drawbridge costs 240c, you have 120c", no.Reason);
        Assert.Contains(Drain(w), e => e.Kind == RiverEventKind.CannotAfford && e.Value == 240);
        Assert.True(PlaceAtFirstRow(w, CrossingType.Ferry).Ok);
        Assert.Equal(80, r.Money);
        // With the river rules PlaceBridge builds a footbridge for 50c ...
        Bridge? fb = null;
        for (int dy = 9; dy < 24 && fb == null; dy++) fb = w.PlaceBridge(w.RiverX[RowFrom(w, dy)], RowFrom(w, dy) + 0.5);
        Assert.NotNull(fb);
        Assert.Equal(30, r.Money);
        Assert.Equal(CrossingType.Footbridge, r.Crossings.Single(c => c.BridgeId == fb!.Id).Type);
        // ... and refuses the next one with a reason.
        int y2 = RowFrom(w, 20);
        Assert.Null(w.PlaceBridge(w.RiverX[y2], y2 + 0.5));
        Assert.Equal("Not enough coin: the footbridge costs 50c, you have 30c", w.LastRiverRefusal);
        Assert.Equal("Only a drawbridge has a keeper", w.HireKeeper(r.Crossings.Single(c => c.BridgeId == fb!.Id).Id).Reason);
        Assert.Single(w.Bridges);
    }

    [Fact]
    public void InvalidSpotsAreRefusedWithTheReason()
    {
        var w = Make(1, 0, NoSchedule with { StartMoney = 5000 });
        var m = w.Map;
        int fy = m.FordY;
        Assert.Equal("Too close to the ford", w.PlaceFerry(m.RiverX[fy], fy + 0.5).Reason);
        int ly = w.River!.LandingRow;
        Assert.Equal("That would block the landing", w.PlaceFerry(m.RiverX[ly], ly + 0.5).Reason);
        Assert.Equal("Crossings go across the river: click on the water", w.PlaceFerry(2, ly + 10.5).Reason);
        var ok = PlaceAtFirstRow(w, CrossingType.Ferry);
        Assert.True(ok.Ok);
        int row = w.River.Crossings.Single(c => c.Id == ok.Id).Row;
        Assert.StartsWith("Too close to the ferry", w.PlaceDrawbridge(m.RiverX[row + 1], row + 1.5).Reason);
        Assert.Equal("That is not a drawbridge", w.SetBridge(ok.Id, true).Reason);
        Assert.Equal("The river rules are off", new World(1, 0).PlaceFerry(m.RiverX[ly + 6], ly + 6.5).Reason);
        // A ferry stage cannot be fenced.
        var x = w.River.Crossings.Single(c => c.Id == ok.Id);
        Assert.False(w.CanFence(x.WestEnd));
        Assert.False(w.CanFence(x.EastEnd));
    }

    [Fact]
    public void SeasonBooksTaxHousesAndChargeUpkeepAndWages()
    {
        var w = Make(1, 0, NoSchedule with { StartMoney = 1000 });
        var r = w.River!;
        PlaceAtFirstRow(w, CrossingType.Ferry);          // 40c, 2c upkeep + 3c ferryman a season
        var d = PlaceAtFirstRow(w, CrossingType.Drawbridge, 9); // 240c, 4c upkeep
        Assert.True(d.Ok, d.Reason);
        Assert.True(w.HireKeeper(d.Id).Ok);              // 30c, 5c a season
        Assert.Equal(1000 - 40 - 240 - 30, r.Money, 9);
        Drain(w);
        double before = r.Money;
        for (int i = 0; i < 599; i++) w.Tick();
        Assert.Equal(before, r.Money, 9);
        w.Tick(); // tick 600: the first season ends
        double expected = w.Houses.Length * 1.0 - (2 + 3) - (4 + 5);
        Assert.Equal(before + expected, r.Money, 9);
        Assert.Contains(Drain(w), e => e.Kind == RiverEventKind.SeasonSettled && Math.Abs(e.Value - expected) < 1e-9);
        // Erasing costs half the build price and refunds nothing.
        double m = r.Money;
        var dx = r.Crossings.Single(c => c.Id == d.Id);
        Assert.Equal(1, w.Erase(dx.Cells.ToArray()));
        Assert.Equal(m - 120, r.Money, 9);
    }

    [Fact]
    public void DebtGrowsByTenPercentASeason()
    {
        var w = Make(1, 0, NoSchedule with { StartMoney = 40, HearthTax = 0 });
        Assert.True(PlaceAtFirstRow(w, CrossingType.Ferry).Ok); // 0c left, 5c a season
        ToyHarness.Run(w, 4);
        Assert.Equal(-5, w.River!.Money, 9);
        ToyHarness.Run(w, 4);
        Assert.Equal(-5 - 5 - 0.5, w.River.Money, 9);
    }
}
