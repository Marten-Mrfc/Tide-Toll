using System;
using System.Collections.Generic;

namespace TideToll.Sim.Town;

public struct PacingMinute
{
    public int Minute;
    public double Coin, CoinPerMinute;
    public int Purchases;
    public int Level, Villagers;
}

/// <summary>What one bot run measured (all times in game seconds; -1 when it never happened).</summary>
public sealed class PacingResult
{
    public int Seed;
    public BotKind Bot;
    public List<PacingMinute> Minutes = new();
    public double FirstCoin = -1, FirstPurchase = -1;
    public double LongestNothingAffordable;
    public double LongestNothingAffordableAt;
    public double[] LevelTimes = Array.Empty<double>();
    public double Charter = -1;
    public long Purchases;
    /// <summary>Lowest purchases in one minute in the first TownTargets.EarlyMinutes, and after them until the charter.</summary>
    public int MinEarlyPerMinute = int.MaxValue, MinLatePerMinute = int.MaxValue;
    public int WorstEarlyMinute = -1, WorstLateMinute = -1;
    // ---- Rules v2 ----
    /// <summary>Purchases by ShopItemKind (index = (int)kind) before the charter.</summary>
    public int[] ByKind = new int[12]; // rules v3: Hand = 11
    /// <summary>Load steps and building offers bought before the charter.</summary>
    public int LoadSteps, Buildings;
    /// <summary>Longest gap in seconds between new things on the map (level gift, building opened, road paved, cottage extended) after minute 2 and before the charter.</summary>
    public double LongestMapGap, LongestMapGapAt;
    public int RequestsAsked, RequestsMet;
    public double CoinAt20 = -1;
    // ---- Rules v2.2 ----
    /// <summary>Longest time in seconds without a purchase after minute 2 and before the charter (the saver's own check).</summary>
    public double LongestPurchaseGap, LongestPurchaseGapAt;
    // Rules v3
    /// <summary>Boon taken at each choice ((int)Boon, -1 none).</summary>
    public int[] Boons = { -1, -1, -1, -1 };
    public double DriveAsked = -1, DriveMet = -1;
    public int Milestones;
    /// <summary>Estimated worth of both boons of each choice when it was offered (A, B per choice).</summary>
    public double[] ChoiceWorth = new double[8];
    /// <summary>Minutes from level MaxLevel-1 to the charter (LastGap), and the longest of the three level gaps before it.</summary>
    public double LastGap => Charter < 0 || LevelTimes.Length < 2 || LevelTimes[^2] < 0 ? -1 : (Charter - LevelTimes[^2]) / 60;
    public double LongestEarlierGap
    {
        get
        {
            double g = 0;
            for (int i = Math.Max(1, LevelTimes.Length - 4); i < LevelTimes.Length - 1; i++)
                if (LevelTimes[i] >= 0 && LevelTimes[i - 1] >= 0) g = Math.Max(g, (LevelTimes[i] - LevelTimes[i - 1]) / 60);
            return g;
        }
    }
}

/// <summary>Plays a bot on a seed and measures the pacing (used by the 'town' tool and the pacing tests).</summary>
public static class TownPacing
{
    public static PacingResult Run(int seed, BotKind kind, int minutes, bool stopAtCharter = true, TownPlan? plan = null, bool noBell = false, bool noPave = false)
    {
        var t = plan != null ? new Town(plan) : new Town(seed);
        var bot = new TownBot(kind, seed) { NoBell = noBell, NoPave = noPave };
        double lastNew = 120;
        var res = new PacingResult { Seed = seed, Bot = kind, LevelTimes = new double[t.MaxLevel] };
        Array.Fill(res.LevelTimes, -1);
        res.LevelTimes[0] = 0;
        var items = new List<ShopItem>();
        var events = new List<TownEvent>();
        long ticks = (long)minutes * 60 * TownRules.TickHz;
        double spellStart = -1;
        int perMinute = 0;
        double lastBuy = 120;
        for (long k = 0; k < ticks; k++)
        {
            t.Step();
            t.DrainEvents(events);
            foreach (var e in events)
            {
                if (e.Kind == TownEventKind.LevelReached) res.LevelTimes[e.Level - 1] = e.Time;
                if (e.Kind == TownEventKind.BoonChosen) res.Boons[e.Level] = e.Index;
                if (e.Kind == TownEventKind.ChoiceOffered) { var cv = t.Choice; res.ChoiceWorth[2 * e.Index] = cv.AWorth; res.ChoiceWorth[2 * e.Index + 1] = cv.BWorth; }
                if (e.Kind == TownEventKind.DriveAsked) res.DriveAsked = e.Time;
                if (e.Kind == TownEventKind.DriveMet) res.DriveMet = e.Time;
                if (e.Kind == TownEventKind.Milestone && res.Charter < 0) res.Milestones++;
                if (res.Charter < 0 && e.Kind == TownEventKind.Purchase)
                {
                    res.ByKind[(int)e.Item]++;
                    if (e.Item == ShopItemKind.Load) res.LoadSteps++;
                    if (e.Item == ShopItemKind.Building) res.Buildings++;
                }
                if (res.Charter < 0 && e.Kind is TownEventKind.LevelReached or TownEventKind.BuildingOpened or TownEventKind.RoadPaved or TownEventKind.CottageExtended)
                {
                    if (e.Time > 120 && e.Time - lastNew > res.LongestMapGap) { res.LongestMapGap = e.Time - lastNew; res.LongestMapGapAt = lastNew; }
                    if (e.Time > 120) lastNew = e.Time;
                }
            }
            if (t.Tick == 20 * 60 * TownRules.TickHz) res.CoinAt20 = t.CoinEarned + t.RequestCoin;
            // Nothing affordable? (sampled when the bot thinks)
            if (t.Tick % TownBot.ThinkTicks == 0 && !t.CanRingNextAge)
            {
                t.ShopItems(items);
                bool any = false;
                foreach (var it in items) if (it.Affordable) { any = true; break; }
                if (!any && spellStart < 0) spellStart = t.Time;
                if (any && spellStart >= 0)
                {
                    double len = t.Time - spellStart;
                    if (len > res.LongestNothingAffordable) { res.LongestNothingAffordable = len; res.LongestNothingAffordableAt = spellStart; }
                    spellStart = -1;
                }
            }
            if (bot.Act(t))
            {
                perMinute++;
                if (res.Charter < 0 && t.Time > 120)
                {
                    if (t.Time - lastBuy > res.LongestPurchaseGap) { res.LongestPurchaseGap = t.Time - lastBuy; res.LongestPurchaseGapAt = lastBuy; }
                    lastBuy = t.Time;
                }
            }
            if (t.HasCharter && res.Charter < 0)
            {
                res.Charter = t.Time;
                if (t.Time - lastBuy > res.LongestPurchaseGap) { res.LongestPurchaseGap = t.Time - lastBuy; res.LongestPurchaseGapAt = lastBuy; }
            }
            if (t.Tick % (60 * TownRules.TickHz) == 0)
            {
                int m = (int)(t.Tick / (60 * TownRules.TickHz));
                res.Minutes.Add(new PacingMinute { Minute = m, Coin = t.Coin, CoinPerMinute = t.CoinPerMinute, Purchases = perMinute, Level = t.Level, Villagers = t.VillagerCount });
                bool beforeCharter = res.Charter < 0 || res.Charter > t.Time - 60;
                if (beforeCharter)
                {
                    if (m <= TownTargets.EarlyMinutes) { if (perMinute < res.MinEarlyPerMinute) { res.MinEarlyPerMinute = perMinute; res.WorstEarlyMinute = m; } }
                    else if (res.Charter < 0 && perMinute < res.MinLatePerMinute) { res.MinLatePerMinute = perMinute; res.WorstLateMinute = m; }
                }
                perMinute = 0;
                if (stopAtCharter && res.Charter >= 0) break;
            }
        }
        t.DrainEvents(events);
        foreach (var e in events)
            if (e.Kind == TownEventKind.LevelReached) res.LevelTimes[e.Level - 1] = e.Time;
        if (spellStart >= 0 && !t.CanRingNextAge)
        {
            double len = t.Time - spellStart;
            if (len > res.LongestNothingAffordable) { res.LongestNothingAffordable = len; res.LongestNothingAffordableAt = spellStart; }
        }
        res.RequestsAsked = t.RequestsAsked; res.RequestsMet = t.RequestsMet;
        res.FirstCoin = t.FirstCoinTime;
        res.FirstPurchase = t.FirstPurchaseTime;
        res.Purchases = t.Purchases;
        return res;
    }

    /// <summary>Every brief target the runs miss, in words with the amount.</summary>
    public static List<string> Misses(List<PacingResult> rs)
    {
        var m = new List<string>();
        double fastest = double.MaxValue, slowest = 0;
        foreach (var r in rs)
        {
            string n = $"seed {r.Seed} {TownBot.Name(r.Bot)}";
            if (r.FirstCoin < 0 || r.FirstCoin > TownTargets.FirstCoinBy) m.Add($"{n}: first coin {r.FirstCoin:F1} s > {TownTargets.FirstCoinBy} s");
            if (r.FirstPurchase < 0 || r.FirstPurchase > TownTargets.FirstPurchaseBy) m.Add($"{n}: first purchase {r.FirstPurchase:F1} s > {TownTargets.FirstPurchaseBy} s");
            // Rules v2.2, the lead's decision: the purchases-per-minute floor is a target of the design brief for its three
            // players (cheapest, best payback, random), who buy as soon as they can, so it measures whether there is something
            // to buy. The saver waits on purpose, so the floor measures nothing for it; it has its own checks instead (every
            // kind, Load step and building before the charter, charter in AgeMin..AgeMax, and never more than
            // SaverMaxPurchaseGap seconds without a purchase after minute 2).
            if (r.Bot == BotKind.Saver)
            {
                if (r.LongestPurchaseGap > TownTargets.SaverMaxPurchaseGap) m.Add($"{n}: {r.LongestPurchaseGap:F0} s without a purchase (from minute {r.LongestPurchaseGapAt / 60:F1}) > {TownTargets.SaverMaxPurchaseGap} s");
            }
            else if (r.WorstEarlyMinute >= 0 && r.MinEarlyPerMinute < TownTargets.EarlyPurchasesPerMinute) m.Add($"{n}: {r.MinEarlyPerMinute} purchases in minute {r.WorstEarlyMinute} < {TownTargets.EarlyPurchasesPerMinute}");
            if (r.Bot != BotKind.Saver && r.WorstLateMinute >= 0 && r.MinLatePerMinute < TownTargets.LatePurchasesPerMinute) m.Add($"{n}: {r.MinLatePerMinute} purchases in minute {r.WorstLateMinute} < {TownTargets.LatePurchasesPerMinute}");
            if (r.LongestNothingAffordable > TownTargets.MaxNothingAffordable) m.Add($"{n}: nothing affordable for {r.LongestNothingAffordable:F0} s > {TownTargets.MaxNothingAffordable} s");
            if (r.Charter < 0) m.Add($"{n}: no charter");
            else
            {
                double min = r.Charter / 60;
                if (min < TownTargets.AgeMinMinutes || min > TownTargets.AgeMaxMinutes) m.Add($"{n}: charter at {min:F1} min, outside {TownTargets.AgeMinMinutes}-{TownTargets.AgeMaxMinutes}");
                fastest = Math.Min(fastest, r.Charter); slowest = Math.Max(slowest, r.Charter);
            }
        }
        foreach (var r in rs)
        {
            string n = $"seed {r.Seed} {TownBot.Name(r.Bot)}";
            long total = 0; foreach (int c in r.ByKind) total += c;
            for (int k = 0; k < r.ByKind.Length; k++)
                if (total > 0 && r.ByKind[k] > TownTargets.MaxTrackShare * total) m.Add($"{n}: track {(ShopItemKind)k} is {100.0 * r.ByKind[k] / total:F0} % of purchases > {TownTargets.MaxTrackShare * 100:F0} %");
            if (r.Bot is BotKind.Saver or BotKind.Payback)
            {
                foreach (var k in TownTargets.EveryKind)
                    if (r.ByKind[(int)k] == 0) m.Add($"{n}: never bought {k} before the charter");
                if (r.LoadSteps < TownRules.LoadPrices.Length) m.Add($"{n}: bought {r.LoadSteps} of {TownRules.LoadPrices.Length} Load steps before the charter");
                if (r.Buildings < 4) m.Add($"{n}: bought {r.Buildings} of 4 buildings before the charter");
            }
            if (r.LongestMapGap > TownTargets.MaxMapGap) m.Add($"{n}: nothing new on the map for {r.LongestMapGap / 60:F1} min (from minute {r.LongestMapGapAt / 60:F1}) > {TownTargets.MaxMapGap / 60:F1}");
        }
        if (rs.Count > 1 && slowest > 0 && fastest < double.MaxValue && slowest / fastest > TownTargets.MaxBotSpread)
            m.Add($"bots spread {slowest / fastest:F2} > {TownTargets.MaxBotSpread}");
        return m;
    }

    /// <summary>
    /// Rules v2.2 (the lead's decision): the bell's worth is measured on frozen towns, because two full runs with and without
    /// the bell buy different things and that noise swamps the bell. The payback bot plays the seed; its town is saved at
    /// each of TownTargets.BellAtMinutes; each save is loaded twice and runs TownTargets.BellFrozenMinutes with no purchases,
    /// once ringing the bell whenever it is ready and once never. Returns, per save, coin earned from deliveries with the bell
    /// over without it, minus 1.
    /// </summary>
    public static double[] BellWorth(int seed, TownPlan? plan = null)
    {
        plan ??= TownPlanner.Build(seed);
        var t = new Town(plan);
        var bot = new TownBot(BotKind.Payback, seed);
        var events = new List<TownEvent>();
        var at = TownTargets.BellAtMinutes;
        var w = new double[at.Length];
        long last = (long)at[^1] * 60 * TownRules.TickHz;
        int next = 0;
        for (long k = 0; k < last && next < at.Length; k++)
        {
            t.Step(); t.DrainEvents(events); bot.Act(t);
            if (t.Tick == (long)at[next] * 60 * TownRules.TickHz)
            {
                string save = TownSave.Save(t);
                w[next] = Frozen(save, plan, true) / Frozen(save, plan, false) - 1;
                next++;
            }
        }
        return w;
    }

    /// <summary>Coin earned from deliveries in TownTargets.BellFrozenMinutes from a save, buying nothing.</summary>
    private static double Frozen(string save, TownPlan plan, bool bell)
    {
        var t = TownSave.Load(save, plan);
        var events = new List<TownEvent>();
        double start = t.CoinEarned;
        long ticks = (long)(TownTargets.BellFrozenMinutes * 60 * TownRules.TickHz);
        for (long k = 0; k < ticks; k++)
        {
            t.Step(); t.DrainEvents(events);
            if (bell && t.Tick % TownBot.ThinkTicks == 0 && t.BellReady) t.RingBell();
        }
        return t.CoinEarned - start;
    }
}
