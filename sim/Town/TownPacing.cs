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
    public int[] ByKind = new int[11];
    /// <summary>Load steps and building offers bought before the charter.</summary>
    public int LoadSteps, Buildings;
    /// <summary>Longest gap in seconds between new things on the map (level gift, building opened, road paved, cottage extended) after minute 2 and before the charter.</summary>
    public double LongestMapGap, LongestMapGapAt;
    public int RequestsAsked, RequestsMet;
    public double CoinAt20 = -1;
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
        for (long k = 0; k < ticks; k++)
        {
            t.Step();
            t.DrainEvents(events);
            foreach (var e in events)
            {
                if (e.Kind == TownEventKind.LevelReached) res.LevelTimes[e.Level - 1] = e.Time;
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
            if (bot.Act(t)) perMinute++;
            if (t.HasCharter && res.Charter < 0) res.Charter = t.Time;
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
            if (r.WorstEarlyMinute >= 0 && r.MinEarlyPerMinute < TownTargets.EarlyPurchasesPerMinute) m.Add($"{n}: {r.MinEarlyPerMinute} purchases in minute {r.WorstEarlyMinute} < {TownTargets.EarlyPurchasesPerMinute}");
            if (r.WorstLateMinute >= 0 && r.MinLatePerMinute < TownTargets.LatePurchasesPerMinute) m.Add($"{n}: {r.MinLatePerMinute} purchases in minute {r.WorstLateMinute} < {TownTargets.LatePurchasesPerMinute}");
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

    /// <summary>Bell worth: coin earned in 20 minutes with the bell rung whenever ready over the same bot never ringing it, minus 1.</summary>
    public static double BellWorth(int seed, BotKind kind, TownPlan? plan = null)
    {
        var a = Run(seed, kind, 20, stopAtCharter: false, plan: plan);
        var b = Run(seed, kind, 20, stopAtCharter: false, plan: plan, noBell: true);
        return a.CoinAt20 / b.CoinAt20 - 1;
    }
}
