using TideToll.Sim.Town;

namespace TideToll.Sim.Tests.Town;

/// <summary>
/// The pacing targets (TownTargets in sim/Town/TownRules.cs, the one place they live) for the four bots.
/// Rules v2: seeds TownTargets.SeedFrom to SeedTo (was seed 1 only) and the saving bot added; new targets below.
/// </summary>
public class TownPacingTests
{
    private static readonly BotKind[] Bots = { BotKind.Cheapest, BotKind.Payback, BotKind.Random, BotKind.Saver };

    private static readonly Lazy<List<PacingResult>[]> Runs = new(() =>
    {
        int n = TownTargets.SeedTo - TownTargets.SeedFrom + 1;
        var all = new List<PacingResult>[n];
        Parallel.For(0, n, i =>
        {
            int seed = TownTargets.SeedFrom + i;
            var plan = TownPlanner.Build(seed);
            var l = new List<PacingResult>();
            foreach (var b in Bots) l.Add(TownPacing.Run(seed, b, TownTargets.RunMinutes, plan: plan));
            all[i] = l;
        });
        return all;
    });

    private static void NoMiss(Func<string, bool> which)
    {
        var m = new List<string>();
        foreach (var rs in Runs.Value) m.AddRange(TownPacing.Misses(rs).Where(which));
        Assert.True(m.Count == 0, m.Count + " misses:\n" + string.Join("\n", m));
    }

    [Fact] public void FirstCoinAndFirstPurchaseComeEarly() => NoMiss(s => s.Contains("first coin") || s.Contains("first purchase"));
    [Fact] public void PurchasesKeepComing() => NoMiss(s => s.Contains("purchases in minute"));
    [Fact] public void NeverLongWithNothingAffordable() => NoMiss(s => s.Contains("nothing affordable"));
    [Fact] public void EveryBotFinishesTheAgeInTime() => NoMiss(s => s.Contains("charter at") || s.Contains("no charter") || s.Contains("spread"));
    [Fact] public void NoTrackDominates() => NoMiss(s => s.Contains("% of purchases"));
    [Fact] public void SaverAndPaybackBuyEveryKindLoadStepAndBuilding() => NoMiss(s => s.Contains("never bought") || s.Contains("Load steps") || s.Contains("buildings before"));
    [Fact] public void SomethingNewOnTheMapOften() => NoMiss(s => s.Contains("nothing new on the map"));

    [Fact]
    public void TheBellIsWorthTenToTwentyFivePercent()
    {
        // Rules v2.1 (the lead's decision): the average over seeds SeedFrom..SeedTo per bot must be in BellMin..BellMax,
        // every single run in the sanity band BellRunMin..BellRunMax. Was: seed 1 only, each bot in BellMin..BellMax.
        // The random bot is left out: with and without the bell its random picks diverge, so the comparison measures chance.
        var bad = new List<string>();
        int n = TownTargets.SeedTo - TownTargets.SeedFrom + 1;
        foreach (var b in Bots)
        {
            if (b == BotKind.Random) continue;
            var w = new double[n];
            Parallel.For(0, n, i => w[i] = TownPacing.BellWorth(TownTargets.SeedFrom + i, b));
            for (int i = 0; i < n; i++)
                if (w[i] < TownTargets.BellRunMin || w[i] > TownTargets.BellRunMax) bad.Add($"seed {TownTargets.SeedFrom + i} {TownBot.Name(b)}: bell +{w[i] * 100:F1} % outside the single-run band");
            double avg = w.Average();
            if (avg < TownTargets.BellMin || avg > TownTargets.BellMax) bad.Add($"{TownBot.Name(b)}: bell +{avg * 100:F1} % on average over seeds {TownTargets.SeedFrom}-{TownTargets.SeedTo}");
        }
        Assert.True(bad.Count == 0, string.Join("\n", bad));
    }

    [Fact]
    public void PavingMatters()
    {
        var bad = new List<string>();
        foreach (var b in Bots)
        {
            var a = Runs.Value[1 - TownTargets.SeedFrom][Array.IndexOf(Bots, b)];
            var np = TownPacing.Run(1, b, TownTargets.RunMinutes, noPave: true);
            double c = np.Charter < 0 ? double.PositiveInfinity : np.Charter;
            if (c < a.Charter * (1 + TownTargets.NoPaveSlower)) bad.Add($"seed 1 {TownBot.Name(b)}: never paving {c / 60:F1} min vs {a.Charter / 60:F1}");
        }
        Assert.True(bad.Count == 0, string.Join("\n", bad));
    }

    [Fact]
    public void ANormalBotMeetsHalfToFourFifthsOfTheRequests()
    {
        // Rules v2.1 (the lead's decision): the average share over seeds SeedFrom..SeedTo per bot must be in
        // RequestsMetMin..Max, every single run in the sanity band RequestsRunMin..Max. Was: every run in RequestsMetMin..Max.
        var bad = new List<string>();
        foreach (var b in new[] { BotKind.Cheapest, BotKind.Payback })
        {
            var shares = new List<double>();
            foreach (var rs in Runs.Value)
                foreach (var r in rs)
                {
                    if (r.Bot != b) continue;
                    double share = r.RequestsAsked == 0 ? 0 : (double)r.RequestsMet / r.RequestsAsked;
                    shares.Add(share);
                    if (share < TownTargets.RequestsRunMin || share > TownTargets.RequestsRunMax) bad.Add($"seed {r.Seed} {TownBot.Name(r.Bot)}: met {r.RequestsMet} of {r.RequestsAsked}, outside the single-run band");
                }
            double avg = shares.Average();
            if (avg < TownTargets.RequestsMetMin || avg > TownTargets.RequestsMetMax) bad.Add($"{TownBot.Name(b)}: met {avg * 100:F0} % on average over seeds {TownTargets.SeedFrom}-{TownTargets.SeedTo}");
        }
        Assert.True(bad.Count == 0, string.Join("\n", bad));
    }
}
