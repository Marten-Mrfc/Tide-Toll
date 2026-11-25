using TideToll.Sim.Testing;
using static TideToll.Sim.Tests.RiverBoatTests;

namespace TideToll.Sim.Tests;

/// <summary>The flow preview for a crossing that would be built: same per-cell cost as the real one, and the boats it would hold.</summary>
public class RiverPreviewTests
{
    private static (int Row, WhatIf Change) Spot(World w, CrossingType t)
    {
        for (int dy = 4; dy < 24; dy++)
        {
            int y = w.River!.LandingRow + dy;
            if (w.CrossingWhatIf(t, w.RiverX[y], y + 0.5) is { } c) return (y, c);
        }
        throw new InvalidOperationException();
    }

    [Fact]
    public void PreviewOfAFootbridgeNamesTheBargesItTurnsBackAndMatchesTheBuiltRoutes()
    {
        var w = Make(2, 200, RiverRules.Default with { StartMoney = 1000 });
        ToyHarness.Run(w, 4);
        var (row, change) = Spot(w, CrossingType.Footbridge);
        uint wear = ToyHarness.Fnv(w.Wear), cost = ToyHarness.Fnv(w.Cost), rng = w.RngState;
        double money = w.River!.Money;
        var p = w.PreviewFlows(change);
        Assert.Equal((wear, cost, rng, money), (ToyHarness.Fnv(w.Wear), ToyHarness.Fnv(w.Cost), w.RngState, w.River.Money));
        double barges = w.River.ArrivalsPerDay(BoatKind.Barge);
        Assert.Equal(barges, p.BoatsBlockedPerDay, 9);
        Assert.Equal(0, p.BoatsNeedRaisePerDay);
        string text = FlowPredictor.Describe(p, b => w.Buildings[b].Kind.ToString());
        Assert.Contains($"Blocks barges: about {Math.Max(1, Math.Round(barges)):0} boat", text);
        Assert.True(p.ReroutedPerDay > 0, text);
        // The preview used the cost the built footbridge has.
        foreach (int c in change.Crossing!.Value.Cells) Assert.Equal(w.CellCost(c, CostOverride.Bridge), w.WhatIfCrossingCost(CrossingType.Footbridge, change.Crossing.Value.Cells.Length, c), 9);
        Assert.True(w.PlaceFootbridge(w.RiverX[row], row + 0.5).Ok);
        foreach (int c in change.Crossing.Value.Cells) Assert.Equal(w.CellCost(c), w.CellCost(c, CostOverride.Bridge), 9);
    }

    [Fact]
    public void PreviewOfAFerryAndADrawbridge()
    {
        var w = Make(2, 200, RiverRules.Default with { StartMoney = 1000 });
        ToyHarness.Run(w, 4);
        var (row, ferry) = Spot(w, CrossingType.Ferry);
        var pf = w.PreviewFlows(ferry);
        Assert.Equal(0, pf.BoatsBlockedPerDay);
        Assert.DoesNotContain("barges", FlowPredictor.Describe(pf, b => b.ToString()));
        var cells = ferry.Crossing!.Value.Cells;
        Assert.True(w.PlaceFerry(w.RiverX[row], row + 0.5).Ok);
        Assert.Equal(w.CellCost(cells[0]), w.WhatIfCrossingCost(CrossingType.Ferry, cells.Length, cells[0]), 9); // AvgWait 0 on a new ferry
        var (_, draw) = Spot(w, CrossingType.Drawbridge);
        var pd = w.PreviewFlows(draw);
        Assert.Equal(w.River!.ArrivalsPerDay(BoatKind.Barge), pd.BoatsNeedRaisePerDay, 9);
        Assert.Contains("Barges need it raised", FlowPredictor.Describe(pd, b => b.ToString()));
        Assert.Null(w.CrossingWhatIf(CrossingType.Drawbridge, w.RiverX[w.Map.FordY], w.Map.FordY + 0.5));
    }
}
