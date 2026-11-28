using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TideToll.Sim.Town;

namespace TideToll.Tools;

/// <summary>
/// hands [--seeds 1-10] [--helpers]: extra hands offered and bought per run (payback and cheapest bots), the first age to
/// the charter and the second from the ring to the seal, and the minute of the first offer. --helpers: the town's filling
/// off, every villager a Helper (called by reflection, so the same file builds on code from before trades).
/// </summary>
public static class HandsCommand
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private sealed class Row { public int Seed; public BotKind Bot; public int Off1, Buy1, Off2, Buy2; public double First = -1, Charter = -1; public int FirstLevel; }

    public static int Run(Dictionary<string, string> o)
    {
        string range = o.TryGetValue("seeds", out var r) ? r : "1-10";
        var p = range.Split('-');
        int a = int.Parse(p[0], Inv), z = p.Length > 1 ? int.Parse(p[1], Inv) : a;
        bool helpers = o.ContainsKey("helpers");
        NoHands = o.ContainsKey("nohands"); // as game-town plays: the bot never buys a hand, and stops at the charter
        var bots = new[] { BotKind.Payback, BotKind.Cheapest };
        var rows = new Row[(z - a + 1) * bots.Length];
        System.Threading.Tasks.Parallel.For(0, rows.Length, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 4 },
            i => rows[i] = Play(a + i / bots.Length, bots[i % bots.Length], helpers));
        foreach (var x in rows)
            Console.WriteLine($"HANDS {x.Seed,3} {x.Bot,-8} | 1st offered {x.Off1} bought {x.Buy1} first offer {(x.First < 0 ? "-" : (x.First / 60).ToString("F1", Inv))} min (level {x.FirstLevel}) | charter {x.Charter / 60:F1} | 2nd offered {x.Off2} bought {x.Buy2}");
        foreach (var k in bots)
        {
            var s = rows.Where(x => x.Bot == k).ToArray();
            var f = s.Where(x => x.First >= 0).Select(x => x.First / 60).ToArray();
            Console.WriteLine($"HANDS MEAN {k}: 1st offered {s.Average(x => x.Off1):F1}, bought {s.Average(x => x.Buy1):F1}, none offered in {s.Count(x => x.Off1 == 0)} of {s.Length}, first offer at {(f.Length > 0 ? f.Average().ToString("F1", Inv) : "-")} min, level {s.Where(x => x.First >= 0).Select(x => x.FirstLevel).DefaultIfEmpty(0).Average():F1} (earliest {s.Where(x => x.First >= 0).Select(x => x.FirstLevel).DefaultIfEmpty(0).Min()}) | 2nd offered {s.Average(x => x.Off2):F1}, bought {s.Average(x => x.Buy2):F1}, none offered in {s.Count(x => x.Off2 == 0)}");
        }
        return 0;
    }

    private static bool NoHands;

    private static Row Play(int seed, BotKind kind, bool helpers)
    {
        var t = new Town(seed);
        if (helpers) typeof(Town).GetMethod("SetTownFillsTrades")?.Invoke(t, new object[] { false });
        var bot = new TownBot(kind, seed) { PlayOn = !NoHands, NoHands = NoHands };
        var row = new Row { Seed = seed, Bot = kind };
        var ev = new List<TownEvent>();
        bool rung = false, done = false;
        for (long k = 0; k < (NoHands ? 60L : 120L) * 60 * TownRules.TickHz && !done; k++)
        {
            t.Step(); bot.Act(t);
            t.DrainEvents(ev);
            foreach (var e in ev)
            {
                if (e.Kind == TownEventKind.NextAge) rung = true;
                if (e.Kind == TownEventKind.Charter) row.Charter = e.Time;
                if (e.Kind == TownEventKind.Seal) done = true;
                if (e.Kind == TownEventKind.OfferAvailable && e.Item == ShopItemKind.Hand)
                {
                    if (rung) row.Off2++; else { row.Off1++; if (row.First < 0) { row.First = e.Time; row.FirstLevel = t.Level; } }
                }
                if (e.Kind == TownEventKind.Purchase && e.Item == ShopItemKind.Hand) { if (rung) row.Buy2++; else row.Buy1++; }
            }
        }
        return row;
    }
}
