using System;
using System.Collections.Generic;

namespace TideToll.Sim.Town;

public enum BotKind : byte { Cheapest = 0, Payback = 1, Random = 2 }

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
    public BotKind Kind { get; }
    private Mulberry32 _rng;
    private readonly List<ShopItem> _items = new();

    public TownBot(BotKind kind, int seed = 1)
    {
        Kind = kind;
        _rng = new Mulberry32(unchecked((uint)(seed * 2654435761u + (uint)kind * 97u + 5u)));
    }

    public static string Name(BotKind k) => k switch { BotKind.Cheapest => "cheapest first", BotKind.Payback => "best payback first", _ => "random affordable" };

    public bool Act(Town t)
    {
        if (t.Tick % ThinkTicks != 0) return false;
        if (t.BellReady) t.RingBell();
        if (t.CanRingNextAge) return false; // the age is done; the bots stop at the charter
        t.ShopItems(_items);
        int pick = Kind switch { BotKind.Cheapest => Cheapest(), BotKind.Payback => Payback(t), _ => RandomPick() };
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

    /// <summary>Estimated extra coin per minute bought per coin; buys the best when affordable, or one nearly as good meanwhile.</summary>
    private int Payback(Town t)
    {
        double rate = Math.Max(6, t.CoinPerMinute);
        int best = -1; double bestScore = 0;
        var score = new double[_items.Count];
        for (int i = 0; i < _items.Count; i++)
        {
            var it = _items[i];
            if (it.Reason.Length > 0 && it.Reason != "Not enough coin") continue;
            double gain = it.Kind switch
            {
                ShopItemKind.Villager => rate / Math.Max(1, t.VillagerCount) * 0.9,
                ShopItemKind.Pace => rate * (Town.PaceFactorAt(t.PaceLevel + 1) / Town.PaceFactorAt(t.PaceLevel) - 1),
                ShopItemKind.Load => rate / (1 + t.LoadLevel),
                ShopItemKind.Pave => rate * 0.04,
                ShopItemKind.Building => rate * 0.5,
                _ => 0,
            };
            // Renown matters too: levels bring the town's next pieces.
            score[i] = (gain + 0.02 * it.Renown) / Math.Max(1, it.Price);
            if (score[i] > bestScore) { bestScore = score[i]; best = i; }
        }
        if (best < 0) return -1;
        if (_items[best].Affordable) return best;
        // Save for the best only when it is less than SaveSeconds of income away; else buy the best affordable meanwhile.
        double wait = (_items[best].Price - t.Coin) / (rate / 60);
        if (wait < SaveSeconds) return -1;
        int alt = -1;
        for (int i = 0; i < _items.Count; i++)
            if (_items[i].Affordable && (alt < 0 || score[i] > score[alt])) alt = i;
        return alt;
    }
}
