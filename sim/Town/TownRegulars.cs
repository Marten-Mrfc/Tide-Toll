using System;
using System.Collections.Generic;

namespace TideToll.Sim.Town;

/// <summary>
/// Green pass 2: Regulars, the first age's ladder of small steps. Each one is a regular customer won: every delivery pays
/// TownRules.RegularsStep more (for good; it goes on in the second age). On sale at every first-age level: the ladder starts
/// again at each level at TownRules.RegularsMinutes of the town's best coin rate when the level was reached, each step
/// TownRules.RegularsGrowth dearer than the last, so something cheap is always on sale and the ladder follows the town.
/// It sits beside the other tracks and never buys any of them out. Not on sale in the second age (its own tracks go on there).
/// </summary>
public sealed partial class Town
{
    /// <summary>Regulars won in all (each: every delivery pays TownRules.RegularsStep more).</summary>
    public int Regulars => S.Regulars;
    /// <summary>Regulars won at the current town level (the ladder starts again at each level).</summary>
    public int RegularsThisLevel => S.RegularsAtLevel;

    /// <summary>The coin factor Regulars give every delivery (1 + step * regulars).</summary>
    public double RegularsFactor => 1 + TownRules.RegularsStep * S.Regulars;

    /// <summary>Price of the next regular: TownRules.RegularsMinutes of the best coin rate at this level's start, times growth ^ bought this level.</summary>
    public double RegularsPrice => Math.Ceiling(TownRules.RegularsMinutes * Math.Max(TownRules.RegularsRateMin, S.RegularsRate)
        * Math.Pow(TownRules.RegularsGrowth, S.RegularsAtLevel));

    /// <summary>"" when a regular can be won now (ignoring coin), else why not.</summary>
    public string RegularsLockReason => S.River != null ? "Regulars are won in the first age" : "";

    /// <summary>The ladder starts again at each level, priced from the best coin rate so far.</summary>
    internal void RegularsNewLevel()
    {
        S.RegularsAtLevel = 0;
        S.RegularsRate = Math.Max(S.RegularsRate, S.MaxCoinPerMinute);
    }

    private void AddRegularsItem(List<ShopItem> into)
    {
        if (S.River != null) return;
        into.Add(Item(ShopItemKind.Regulars, -1, "Regulars", S.Regulars, RegularsPrice, ""));
    }

    public bool BuyRegulars()
    {
        if (RegularsLockReason.Length > 0) return false;
        double p = RegularsPrice;
        if (!Pay(p)) return false;
        S.Regulars++;
        S.RegularsAtLevel++;
        Bought(ShopItemKind.Regulars, -1, p, S.Regulars);
        return true;
    }
}
