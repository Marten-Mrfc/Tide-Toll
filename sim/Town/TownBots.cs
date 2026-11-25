using System;
using System.Collections.Generic;

namespace TideToll.Sim.Town;

public enum BotKind : byte { Cheapest = 0, Payback = 1, Random = 2, Saver = 3 }

/// <summary>
/// A player bot that plays only through the actions the game uses (ShopItems, Buy, Pave, RingBell, RingNextAge).
/// Call <see cref="Act"/> once per tick after Town.Step. It thinks once every <see cref="ThinkTicks"/> ticks
/// (a person clicks about twice a second) and buys at most one thing per think.
/// </summary>
public sealed class TownBot
{
    public const int ThinkTicks = 5;
    /// <summary>The payback bot saves for its best pick only when that is less than this many seconds of income away.</summary>
    public const double SaveSeconds = 20;
    /// <summary>The saving bot waits for its best pick up to this many seconds of income (rules v2).</summary>
    public const double SaverSeconds = 40;
    public BotKind Kind { get; }
    /// <summary>Test switches: never ring the bell; never pave.</summary>
    public bool NoBell, NoPave;
    private Mulberry32 _rng;
    private readonly List<ShopItem> _items = new();

    public TownBot(BotKind kind, int seed = 1)
    {
        Kind = kind;
        _rng = new Mulberry32(unchecked((uint)(seed * 2654435761u + (uint)kind * 97u + 5u)));
    }

    public static string Name(BotKind k) => k switch { BotKind.Cheapest => "cheapest first", BotKind.Payback => "best payback first", BotKind.Saver => "saver", _ => "random affordable" };

    public bool Act(Town t)
    {
        if (t.Tick % ThinkTicks != 0) return false;
        if (t.BellReady && !NoBell) t.RingBell();
        if (t.CanRingNextAge) return false; // the age is done; the bots stop at the charter
        t.ShopItems(_items);
        if (NoPave)
            for (int i = _items.Count - 1; i >= 0; i--) if (_items[i].Kind == ShopItemKind.Pave) _items.RemoveAt(i);
        int pick = Kind switch { BotKind.Cheapest => Cheapest(), BotKind.Payback => Payback(t, SaveSeconds), BotKind.Saver => Payback(t, SaverSeconds), _ => RandomPick() };
        return pick >= 0 && t.Buy(_items[pick]);
    }

    private int Cheapest()
    {
        int best = -1;
        for (int i = 0; i < _items.Count; i++)
            if (_items[i].Affordable && (best < 0 || _items[i].Price < _items[best].Price)) best = i;
        return best;
    }

    private int RandomPick()
    {
        int n = 0;
        for (int i = 0; i < _items.Count; i++) if (_items[i].Affordable) n++;
        if (n == 0) return -1;
        int k = (int)Math.Floor(_rng.NextDouble() * n);
        for (int i = 0; i < _items.Count; i++)
            if (_items[i].Affordable && k-- == 0) return i;
        return -1;
    }

    /// <summary>Estimated extra coin per minute an item buys (rough, from the town's own numbers).</summary>
    public static double Gain(Town t, ShopItem it, double rate)
    {
        int n = Math.Max(1, t.VillagerCount);
        double earned = Math.Max(1, t.CoinEarned);
        switch (it.Kind)
        {
            case ShopItemKind.Villager: return rate / n * 0.9;
            case ShopItemKind.Pace: return rate * 0.55 * (Town.PaceFactorAt(t.PaceLevel + 1) / Town.PaceFactorAt(t.PaceLevel) - 1) + 0.002 * it.Price;
            case ShopItemKind.Load: return rate / (TownRules.BaseLoad + t.LoadLevel);
            case ShopItemKind.Pave: return rate * 0.05;
            case ShopItemKind.Building: return rate * 0.3;
            case ShopItemKind.Tools: return rate * 0.3 * (TownRules.ToolsStep / (1 + TownRules.ToolsStep * t.ToolsLevel)) + 0.002 * it.Price;
            case ShopItemKind.Value:
            {
                var g = (Good)it.Index;
                double share = Math.Max(0.15, t.EarnedBy(g) / earned);
                return rate * share * TownRules.ValueStep / (1 + TownRules.ValueStep * t.ValueLevel(g));
            }
            case ShopItemKind.Extend:
                // Room for one more villager: worth a villager, at the price of both.
                return t.HousingRoom > 0 ? 0 : rate / n * 0.9 * it.Price / (it.Price + t.VillagerPrice);
            case ShopItemKind.BellTrack: return rate * 0.025;
            default: return 0;
        }
    }

    /// <summary>Best extra coin per minute per coin; buys the best when affordable, saves for it when it is less than
    /// <paramref name="saveSeconds"/> of income away, else buys the best affordable meanwhile.</summary>
    private int Payback(Town t, double saveSeconds)
    {
        double rate = Math.Max(6, t.CoinPerMinute);
        int best = -1; double bestScore = 0;
        if (_score.Length < _items.Count) _score = new double[_items.Count * 2];
        var score = _score;
        for (int i = 0; i < _items.Count; i++)
        {
            var it = _items[i];
            score[i] = 0;
            if (it.Reason.Length > 0 && it.Reason != "Not enough coin") continue;
            // Renown matters too: levels bring the town's next pieces.
            score[i] = (Gain(t, it, rate) + 0.02 * it.Renown) / Math.Max(1, it.Price);
            if (score[i] > bestScore) { bestScore = score[i]; best = i; }
        }
        if (best < 0) return -1;
        if (_items[best].Affordable) return best;
        double wait = (_items[best].Price - t.Coin) / (rate / 60);
        if (wait < saveSeconds) return -1;
        int alt = -1;
        for (int i = 0; i < _items.Count; i++)
            if (_items[i].Affordable && (alt < 0 || score[i] > score[alt])) alt = i;
        return alt;
    }

    private double[] _score = new double[32];
}
