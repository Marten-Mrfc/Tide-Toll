using System;
using System.Collections.Generic;

namespace TideToll.Sim.Town;

/// <summary>Targets of the second age (docs/plans/age2-design.md), in one place for the tests and the tool.</summary>
public static class TownRiverTargets
{
    public static int SeedFrom = 1, SeedTo = 10;
    public static double AgeMinutesMin = 20, AgeMinutesMax = 35;   // the three buy-when-you-can bots
    public static double PurchasesPerMinuteMin = 2;                 // over the whole age
    public static double NothingAffordableMax = 60;                 // seconds
    public static double MapGapMax = 210;                           // seconds (3.5 minutes)
    public static double LandingShareMin = 1.0 / 3;                 // of all coin earned in the second age, by its middle
    public static double CrossingWaitMax = 240;                     // seconds any villager waits at one crossing (one tide cycle)
    public static double MinuteMsMax = 10;                          // CPU per game minute at the end of the age
    public static int PlayMinutesMax = 120;                         // first age plus second age, a cap for the runs
}

/// <summary>What one bot measured in the second age (times in seconds since it was rung in; -1 never).</summary>
public sealed class RiverPacingResult
{
    public int Seed;
    public BotKind Bot;
    public double RungAt = -1;            // game time of the ring (end of the first age)
    public double Seal = -1;              // seconds from the ring to the borough seal
    public double[] LevelTimes = new double[12];
    public long Purchases;
    public double LongestNothingAffordable, LongestNothingAffordableAt;
    public double LongestMapGap, LongestMapGapAt;
    public int[] ByKind = new int[22];
    /// <summary>By RiverLinkKind: people crossed (by any crossing) per minute while that crossing was the newest one built.</summary>
    public double[] CrossPerMinute = new double[5];
    /// <summary>By RiverLinkKind: Town.CrossingCapacity of each crossing when it opened (CapAtOpen) and of the one before it then (PrevCapAtOpen), and at the seal.</summary>
    public double[] CapAtOpen = new double[5], PrevCapAtOpen = new double[5], CapAtSeal = new double[5];
    public double LandingShareAtMid = -1;
    public double LongestCrossingWait;
    public double CoinLanding, CoinToll, CoinMarket, CoinTown;  // earned in the second age by source
    public int BoatsCome, BoatsFilled, BoatsShort;
    public double MinuteMsAtEnd = -1;
    /// <summary>Green pass: touches when the age was rung in and at the seal; spots of the seed.</summary>
    public int TouchesAtRing, TouchesAtSeal, TouchSpots;
    /// <summary>Per minute of the second age: coin per minute, coin held, cheapest price on sale, villagers walking/queued/working/idle, river mode.</summary>
    public List<string> Trace = new();
    public double MinutesOfAge => Seal < 0 ? -1 : Seal / 60;
}

public static class TownRiverPacing
{
    public static RiverPacingResult Run(int seed, BotKind kind, int maxMinutes = 0, TownPlan? plan = null)
    {
        if (maxMinutes <= 0) maxMinutes = TownRiverTargets.PlayMinutesMax;
        var t = plan != null ? new Town(plan) : new Town(seed);
        var bot = new TownBot(kind, seed) { PlayOn = true };
        var res = new RiverPacingResult { Seed = seed, Bot = kind };
        Array.Fill(res.LevelTimes, -1);
        var items = new List<ShopItem>();
        var events = new List<TownEvent>();
        long ticks = (long)maxMinutes * 60 * TownRules.TickHz;
        double spell = -1, lastNew = -1, earnedAtRing = 0, midCheckedAt = -1;
        int firstRiverLevel = t.Plan.MaxLevel + 1;
        // Crossing epochs: people crossed by any crossing per minute while each crossing was the newest one.
        var epochStart = new double[5]; var epochCount = new long[5]; int newest = 0;
        Array.Fill(epochStart, -1);
        for (long k = 0; k < ticks; k++)
        {
            t.Step();
            t.DrainEvents(events);
            bool river = t.InRiverAge;
            foreach (var e in events)
            {
                if (e.Kind == TownEventKind.NextAge) { res.RungAt = e.Time; lastNew = e.Time; earnedAtRing = t.CoinEarned; res.TouchesAtRing = t.TouchCount; res.TouchSpots = t.TouchSpots.Length; }
                if (!river || res.Seal >= 0) continue;
                double rt = e.Time - res.RungAt;
                if (e.Kind == TownEventKind.LevelReached && e.Level >= firstRiverLevel) res.LevelTimes[e.Level - firstRiverLevel] = rt;
                if (e.Kind == TownEventKind.Purchase) { res.Purchases++; res.ByKind[(int)e.Item]++; }
                // New on the map: level gifts, buildings, paving, extensions, a new villager, and the river upgrades that change a model
                // (ferry, berth, crane, raft, rope, winch).
                if (e.Kind is TownEventKind.LevelReached or TownEventKind.BuildingOpened or TownEventKind.RoadPaved or TownEventKind.CottageExtended or TownEventKind.TouchAdded or TownEventKind.VillagerArrived
                    || (e.Kind == TownEventKind.Purchase && e.Item is ShopItemKind.Ferry or ShopItemKind.Berth or ShopItemKind.Crane or ShopItemKind.Raft or ShopItemKind.Rope or ShopItemKind.Winch))
                {
                    if (e.Time - lastNew > res.LongestMapGap) { res.LongestMapGap = e.Time - lastNew; res.LongestMapGapAt = lastNew - res.RungAt; }
                    lastNew = e.Time;
                }
                if (e.Kind == TownEventKind.Seal)
                {
                    res.Seal = rt;
                    res.TouchesAtSeal = t.TouchCount;
                    for (int c = 1; c < 5; c++) res.CapAtSeal[c] = t.CrossingCapacity((RiverLinkKind)c);
                    res.CoinLanding = t.LandingCoin - t.TollCoin * 0; res.CoinToll = t.TollCoin; res.CoinMarket = t.RiverMarketCoin;
                    res.CoinTown = t.CoinEarned - earnedAtRing - t.LandingCoin - t.TollCoin - t.RiverMarketCoin;
                    res.BoatsCome = t.BoatsCome; res.BoatsFilled = t.BoatsFilled; res.BoatsShort = t.BoatsLeftShort;
                }
            }
            if (res.Seal >= 0) break;
            if (!river) { bot.Act(t); continue; }
            double now = t.Time - res.RungAt;
            long crossedAll = 0;
            for (int c = 1; c < 5; c++) crossedAll += t.Crossings((RiverLinkKind)c);
            for (int c = newest + 1; c < 5; c++)
                if (t.CrossingExists((RiverLinkKind)c))
                {
                    if (newest > 0) res.CrossPerMinute[newest] = (crossedAll - epochCount[newest]) * 60 / Math.Max(1, now - epochStart[newest]);
                    res.CapAtOpen[c] = t.CrossingCapacity((RiverLinkKind)c);
                    res.PrevCapAtOpen[c] = c > 1 ? t.CrossingCapacity((RiverLinkKind)(c - 1)) : 0;
                    newest = c; epochStart[c] = now; epochCount[c] = crossedAll;
                }
            if (newest > 0) res.CrossPerMinute[newest] = (crossedAll - epochCount[newest]) * 60 / Math.Max(1, now - epochStart[newest]);
            res.LongestCrossingWait = Math.Max(res.LongestCrossingWait, t.LongestCrossingWait);
            // The landing's share of the age's coin by the middle of the age (the 6th river level stands for the middle).
            if (midCheckedAt < 0 && res.LevelTimes[5] >= 0)
            {
                midCheckedAt = now;
                double all = t.CoinEarned - earnedAtRing;
                res.LandingShareAtMid = all > 0 ? (t.LandingCoin + t.TollCoin) / all : 0;
            }
            if (t.Tick % 300 == 0)
            {
                t.ShopItems(items);
                double cheapest = double.MaxValue; string cn = "";
                foreach (var it in items) if (it.Reason.Length == 0 || it.Reason == "Not enough coin") if (it.Price < cheapest) { cheapest = it.Price; cn = it.Name; }
                int[] ph = new int[5]; int rm = 0;
                foreach (var v in t.S.Villagers) { ph[(int)v.Phase]++; if (v.RMode != 0) rm++; }
                res.Trace.Add($"{now / 60:0} cpm {t.CoinPerMinute:0} coin {t.Coin:0} cheapest {cn} {cheapest:0} idle/walk/queue/work {ph[0]}/{ph[1]}/{ph[2]}/{ph[3]} river {rm} level {t.Level} landing {t.LandingCoin:0} boats {t.BoatsCome}/{t.BoatsFilled}/{t.BoatsLeftShort}");
            }
            if (t.Tick % TownBot.ThinkTicks == 0)
            {
                t.ShopItems(items);
                bool any = false;
                foreach (var it in items) if (it.Affordable) { any = true; break; }
                if (!any && spell < 0) spell = now;
                if (any && spell >= 0)
                {
                    if (now - spell > res.LongestNothingAffordable) { res.LongestNothingAffordable = now - spell; res.LongestNothingAffordableAt = spell; }
                    spell = -1;
                }
            }
            bot.Act(t); // after the sample, as the first age's pacing does
        }
        return res;
    }
}
