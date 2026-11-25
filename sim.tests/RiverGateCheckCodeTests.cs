using TideToll.Sim.Testing;

namespace TideToll.Sim.Tests;

/// <summary>
/// The river checks' own code, on made-up rows (fix round before the playtest, 25 Nov 2026): what check (a) reads
/// from the table, not what the bot measures.
/// </summary>
public class RiverGateCheckCodeTests
{
    private static RiverRunResult Row(int seed, RiverPlayer p, double trade, double trip)
        => new(seed, 200, 30, p, 50, 6, -1, 10, trade, trade / 2, trade / 2, 100, 0.05, 0.05, 20,
            1, 2, trip, 50, 1000, 10, 100, 50, 40, 0.1, 10, 0, 5, 0, 0, 0, 0);

    private static (bool Ok, string What) CheckA(List<RiverRunResult> rows, string startsWith)
    {
        var log = new CheckLog();
        RiverGateChecks.Check(rows, log);
        return log.Checks.Single(c => c.What.StartsWith(startsWith));
    }

    /// <summary>Every player on three seeds; the ford-only player has the lowest trade after the footbridge (S3 at 70% of it).</summary>
    private static List<RiverRunResult> Table(double s4Trip)
    {
        var rows = new List<RiverRunResult>();
        foreach (int seed in new[] { 1, 2, 3 })
            foreach (var p in RiverPlayer.All)
            {
                double trade = p == RiverPlayer.S3 ? 700 : p == RiverPlayer.S1 ? 1000 : p == RiverPlayer.S4 ? 600 : 2000;
                double trip = p == RiverPlayer.S3 ? 15.0 : p == RiverPlayer.S4 ? s4Trip : 17.0;
                rows.Add(Row(seed, p, trade, trip));
            }
        return rows;
    }

    [Fact]
    public void WorstTradeCheckPassesWhenTheFordOnlyPlayerIsTheNextLowest()
    {
        // RiverPlayer is a record struct and the ford-only player equals default(RiverPlayer): the check used to test
        // nextTrade.Key != default and so failed whenever S1 came next, although the numbers passed.
        Assert.Equal(default(RiverPlayer), RiverPlayer.S1);
        var c = CheckA(Table(16), "(a) footbridge has clearly the worst trade");
        Assert.True(c.Ok, c.What);
        Assert.Contains("next lowest S1 1000c", c.What);
        // The limit is unchanged: at 81% of the next lowest it fails.
        var close = Table(16).Select(r => r.Player == RiverPlayer.S3 ? r with { TradeIncome = 810 } : r).ToList();
        Assert.False(CheckA(close, "(a) footbridge has clearly the worst trade").Ok);
    }

    [Fact]
    public void FastestTripsCheckLeavesTheNeverRaisedDrawbridgeToTheReport()
    {
        // Restated by the lead: the never-raised drawbridge (S4) is only a footbridge at four times the price; it is printed
        // beside the check, report only, with its time.
        var rows = Table(14.81);
        var c = CheckA(rows, "(a) footbridge gives the fastest walker trips");
        Assert.True(c.Ok, c.What);
        var log = new CheckLog();
        RiverGateChecks.Check(rows, log);
        Assert.Contains(log.Lines, l => l.Contains("report only") && l.Contains("never-raised drawbridge") && l.Contains("14.81 s"));
        // Against the players bought for walking it still bites: a ferry faster than the footbridge fails it.
        var ferryFaster = rows.Select(r => r.Player == RiverPlayer.S2 ? r with { MeanTrip = 14.5 } : r).ToList();
        Assert.False(CheckA(ferryFaster, "(a) footbridge gives the fastest walker trips").Ok);
    }
}
