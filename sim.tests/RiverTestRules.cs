namespace TideToll.Sim.Tests;

/// <summary>
/// River rules with the money, prices, wages and ferry numbers stated here, for tests whose expectations depend on
/// them. The production defaults (RiverRules.Default) are tuned by the strategy bot and may change; a test that
/// checks a price in words or a season's books states the numbers it checks instead of assuming the defaults.
/// </summary>
internal static class RiverTestRules
{
    /// <summary>
    /// No boat schedule, no goods, and every number of the economy stated: 120c to start, ferry 40c (2c upkeep + 3c
    /// ferryman a season), footbridge 50c, drawbridge 240c (4c upkeep), keeper 30c (5c a season), hearth tax 1c,
    /// boats pay 2 x 2c a unit, the market 70% of 2c. Ferry: 6 aboard, 0.5 s to turn round, 1.0 s across.
    /// </summary>
    internal static readonly RiverRules Priced = RiverBoatTests.NoSchedule with
    {
        StartMoney = 120,
        FerryPrice = 40, FerryUpkeep = 2, FerryWage = 3,
        FootbridgePrice = 50,
        DrawbridgePrice = 240, DrawbridgeUpkeep = 4,
        KeeperPrice = 30, KeeperWage = 5,
        HearthTax = 1,
        GrainPrice = 2, BoatValueMul = 2, MarketShare = 0.7,
        FerryCapacity = 6, FerryTurnSec = 0.5, FerryCrossSec = 1.0,
        DebtLimit = -100, DebtInterest = 0.1, EraseCostShare = 0.5,
    };
}
