using System.Security.Cryptography;
using TideToll.Sim.Town;
using T = TideToll.Sim.Town.Town;

namespace TideToll.Sim.Tests.Town;

/// <summary>Trades (n7 step 2): with the town's filling off and every villager a Helper, a run is tick for tick the run
/// before trades existed. The hashes were recorded on the main branch (70922b8) before any trade rule was written.</summary>
public class TownTradesPinTests
{
    /// <summary>A fingerprint of a whole run: every 10 ticks the coin, renown, level, purchases and every villager's job,
    /// phase and position; the bot plays to the charter, rings the second age in and plays it for 8 minutes.</summary>
    internal static string RunHash(int seed, BotKind kind, bool tradesOff)
    {
        var t = new T(seed);
        if (tradesOff) t.SetTownFillsTrades(false);
        var bot = new TownBot(kind, seed) { PlayOn = true };
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        long ringAt = -1, limit = 60L * 60 * TownRules.TickHz;
        for (long k = 0; k < limit; k++)
        {
            t.Step(); bot.Act(t);
            if (ringAt < 0 && t.InRiverAge) { ringAt = k; limit = k + 8L * 60 * TownRules.TickHz; }
            if (k % 10 != 0) continue;
            w.Write(t.Tick); w.Write(t.Coin); w.Write(t.S.Renown); w.Write(t.Level); w.Write(t.S.Purchases);
            for (int i = 0; i < t.VillagerCount; i++)
            {
                var v = t.S.Villagers[i];
                w.Write((byte)v.Job); w.Write((byte)v.Phase); w.Write(v.X); w.Write(v.Y);
            }
        }
        w.Write(ringAt);
        w.Flush();
        return Convert.ToHexString(SHA256.HashData(ms.ToArray()))[..16];
    }

    /// <summary>The first age with trades on as shipped (step 2), to the ring: every 10 ticks as RunHash, and every
    /// villager's trade too. Recorded before step 3 (the second age's trades) was written: nothing of step 3 opens before
    /// the ring, so the first age must come out tick for tick the same.</summary>
    internal static string FirstAgeHash(int seed, BotKind kind)
    {
        var t = new T(seed);
        var bot = new TownBot(kind, seed) { PlayOn = true };
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        long k = 0;
        for (; k < 60L * 60 * TownRules.TickHz && !t.InRiverAge; k++)
        {
            t.Step(); bot.Act(t);
            if (k % 10 != 0) continue;
            w.Write(t.Tick); w.Write(t.Coin); w.Write(t.S.Renown); w.Write(t.Level); w.Write(t.S.Purchases); w.Write(t.S.TradeChanges);
            for (int i = 0; i < t.VillagerCount; i++)
            {
                var v = t.S.Villagers[i];
                w.Write((byte)v.Job); w.Write((byte)v.Phase); w.Write((byte)v.Trade); w.Write(v.X); w.Write(v.Y);
            }
        }
        w.Write(k);
        w.Flush();
        return Convert.ToHexString(SHA256.HashData(ms.ToArray()))[..16];
    }

    [Theory]
    [InlineData(1, BotKind.Payback, "CA5F988ECDE942EA")]
    [InlineData(2, BotKind.Payback, "1F78017201464560")]
    [InlineData(3, BotKind.Payback, "F318868FD88FDF69")]
    [InlineData(1, BotKind.Cheapest, "7A0C48BFB26978A7")]
    [InlineData(2, BotKind.Cheapest, "491DF8DF201505A6")]
    [InlineData(3, BotKind.Cheapest, "9BAC0B48F2358BA9")]
    public void WithTradesOnTheFirstAgeIsAsBeforeTheSecondAgesTrades(int seed, BotKind kind, string want)
    {
        Assert.Equal(want, FirstAgeHash(seed, kind));
    }

    [Theory]
    [InlineData(1, BotKind.Payback, "23405C6D2BFF08FF")]
    [InlineData(2, BotKind.Payback, "720BAB93E8574BC7")]
    [InlineData(3, BotKind.Payback, "EC5C8EFC433BE9D2")]
    [InlineData(1, BotKind.Cheapest, "24C2446B81552C58")]
    [InlineData(2, BotKind.Cheapest, "8D58F95A19B63546")]
    [InlineData(3, BotKind.Cheapest, "CD1BBC535FF89E58")]
    public void WithTradesOffARunIsTheRunBeforeTrades(int seed, BotKind kind, string want)
    {
        Assert.Equal(want, RunHash(seed, kind, true));
    }
}
