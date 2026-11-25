using TideToll.Sim.Town;

namespace TideToll.Sim.Tests.Town;

/// <summary>The design brief's pacing targets (TownTargets in sim/Town/TownRules.cs) on seed 1, all three bots.</summary>
public class TownPacingTests
{
    private static readonly Lazy<List<PacingResult>> Runs = new(() =>
    {
        var plan = TownPlanner.Build(1);
        return new List<PacingResult>
        {
            TownPacing.Run(1, BotKind.Cheapest, TownTargets.RunMinutes, plan: plan),
            TownPacing.Run(1, BotKind.Payback, TownTargets.RunMinutes, plan: plan),
            TownPacing.Run(1, BotKind.Random, TownTargets.RunMinutes, plan: plan),
        };
    });

    private static void NoMiss(Func<string, bool> which)
    {
        var m = TownPacing.Misses(Runs.Value).Where(which).ToList();
        Assert.True(m.Count == 0, string.Join("\n", m));
    }

    [Fact] public void FirstCoinAndFirstPurchaseComeEarly() => NoMiss(s => s.Contains("first coin") || s.Contains("first purchase"));
    [Fact] public void PurchasesKeepComing() => NoMiss(s => s.Contains("purchases in minute"));
    [Fact] public void NeverLongWithNothingAffordable() => NoMiss(s => s.Contains("nothing affordable"));
    [Fact] public void EveryBotFinishesTheAgeInTime() => NoMiss(s => s.Contains("charter") || s.Contains("spread"));
}
