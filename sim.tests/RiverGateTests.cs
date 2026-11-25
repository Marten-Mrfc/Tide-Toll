using TideToll.Sim.Testing;
using Xunit.Abstractions;

namespace TideToll.Sim.Tests;

/// <summary>
/// Runs the river experiment once: every player (S1 to S4, S5 missing 10%, 30% and 50% of boats, S6) on seeds 101 to
/// 120 at 200 walkers, and the ferry, the attentive manual player and the keeper at 60, for 30 in-game days.
/// </summary>
public sealed class RiverRunsFixture
{
    public List<RiverRunResult> Rows { get; }
    public static readonly RiverPlayer[] At60 = { RiverPlayer.S2, RiverPlayer.S5Miss10, RiverPlayer.S6 };

    public RiverRunsFixture()
    {
        int threads = Math.Max(1, Math.Min(4, Environment.ProcessorCount - 1));
        var seeds = Enumerable.Range(101, 20);
        Rows = RiverBot.RunAll(seeds, RiverPlayer.All, 200, 30, threads);
        Rows.AddRange(RiverBot.RunAll(seeds, At60, 60, 30, threads));
    }
}

/// <summary>
/// Automated stand-in for the river playtest: the crossing choices are real trade-offs (the lead's criteria (a) to
/// (g), decision E of river pass 3), and the spec's earlier checks. Each check is one test, so the report shows exactly which hold. Category=Gate.
/// </summary>
[Trait("Category", "Gate")]
public class RiverGateTests : IClassFixture<RiverRunsFixture>
{
    private readonly RiverRunsFixture _f;
    private readonly ITestOutputHelper _out;
    public RiverGateTests(RiverRunsFixture f, ITestOutputHelper output) { _f = f; _out = output; }

    private void Check(string startsWith)
    {
        var log = new CheckLog();
        RiverGateChecks.Check(_f.Rows, log);
        var sw = new StringWriter();
        RiverBot.Print(sw, _f.Rows, perSeed: false);
        _out.WriteLine(sw.ToString());
        _out.WriteLine(log.ToString());
        var c = log.Checks.Single(k => k.What.StartsWith(startsWith));
        Assert.True(c.Ok, c.What + "\n" + sw);
    }

    [Fact] public void A_FootbridgeGivesTheFastestTrips() => Check("(a) footbridge gives the fastest");
    [Fact] public void A_FootbridgeHasClearlyTheWorstTrade() => Check("(a) footbridge has clearly the worst trade");
    // (b) in the lead's new form (decision E, river pass 3); the old form ("the ford is clearly the slowest") is printed as report only.
    [Fact] public void B_TheFreeFordCostsTrade() => Check("(b) the free ford costs trade");
    [Fact] public void B_FordOnlyIsRarelyTheRichest() => Check("(b) ford only is the richest strategy");
    [Fact] public void B_NobodyCrossesTheFordInTheFlood() => Check("(b) nobody crosses the ford");
    [Fact] public void C_FerryIsTheBetterBuyInASmallVillage() => Check("(c) at 60 walkers the ferry");
    [Fact] public void C_FerryCapacityBitesAt200() => Check("(c) at 200 walkers the ferry's capacity bites");
    [Fact] public void D_DrawbridgePaysBackAt200() => Check("(d) at 200 walkers the hand-raised drawbridge");
    [Fact] public void D_DrawbridgeDoesNotPayBackAt60() => Check("(d) at 60 walkers it does not");
    [Fact] public void E_KeeperBeatsThePlayerMissing30() => Check("(e) at 200 walkers the keeper ends with more net money than the player missing 30%");
    [Fact] public void E_KeeperBeatsThePlayerMissing50() => Check("(e) at 200 walkers the keeper ends with more net money than the player missing 50%");
    [Fact] public void E_KeeperIsNeverHiredBeforeItsUnlock() => Check("(e) the keeper is never hired before its unlock");
    [Fact] public void F_NoStrategyDominates() => Check("(f) no strategy is best");
    [Fact] public void G_ACrossingIsAffordableOnTheFirstDay() => Check("(g) one crossing is affordable in the first in-game day");
    [Fact] public void G_TheDrawbridgeComesAfterSixToTenDays() => Check("(g) at 200 walkers the drawbridge is bought after 6 to 10");
    [Fact] public void G_TheKeeperComesNoSoonerThanItsUnlock() => Check("(g) the keeper comes no sooner than its unlock");
    [Fact] public void FootbridgeTurnsBackBarges() => Check("footbridge turns back more than 60%");
    [Fact] public void FordTurnsBackAlmostNobody() => Check("ford only turns back fewer than 10%");
    [Fact] public void NeverRaisedDrawbridgeIsNoBetterThanAFootbridge() => Check("never-raised drawbridge earns no more");
    [Fact] public void RaisedDrawbridgesKeepBoatsComing() => Check("manual (10% missed) and keeper drawbridges turn back");
    [Fact] public void ManualBatchingKeepsTheWorstWaitLower() => Check("manual drawbridge has the lower worst walker wait");
}
