using System;
using System.Collections.Generic;

namespace TideToll.Sim.Town;

/// <summary>Green pass: the game builders' small wishes (docs/plans/town-sim-api.md "Added in the green pass").</summary>
public sealed partial class Town
{
    private static readonly string[] FinishedReasons =
    {
        "Fastest pace reached", "Best tools reached", "Best price reached", "Best prices reached", "The bell is at its best",
        "All Load steps bought", "Every cottage is extended", "Already built", "The biggest crane", "The biggest raft",
        "The best rope", "The highest toll", "The fastest winch", "All training done",
    };

    /// <summary>Whether a reason means "not on sale yet" (a later level or something still to come).</summary>
    public static bool IsLockedReason(string reason) =>
        reason.StartsWith("Unlocks at", StringComparison.Ordinal) || reason.StartsWith("Comes with", StringComparison.Ordinal) || reason == "Cobbles come later";

    /// <summary>Whether a reason means "this track or thing is done: nothing more of it will come".</summary>
    public static bool IsFinishedReason(string reason)
    {
        foreach (var f in FinishedReasons) if (ReferenceEquals(f, reason) || f == reason) return true;
        return false;
    }

    /// <summary>Whether the building offer of a kind is bought (false if the plan has none).</summary>
    public bool OfferBoughtOf(OfferKind k)
    {
        int o = OfferOf(k);
        return o >= 0 && S.OfferBought[o];
    }

    /// <summary>CanPave and the coin for it: true only when the road can be paved right now.</summary>
    public bool CanPaveNow(int road) => CanPave(road, out _) && S.Coin >= PavePrice(road);

    /// <summary>CanExtend and the coin for it: true only when the cottage can be extended right now.</summary>
    public bool CanExtendNow(int plot) => CanExtend(plot, out _) && S.Coin >= ExtendPrice;

    /// <summary>After the borough seal: the second age is complete. Saved (the seal is in the save).</summary>
    public bool SecondAgeComplete => HasSeal;

    /// <summary>Text for the end of the second age, or "" before the seal.</summary>
    public string SecondAgeCompleteText => HasSeal ? "The second age is complete. The borough is sealed." : "";

    /// <summary>Fills <paramref name="into"/> (cleared first) with what can still be bought, now or later (not finished).
    /// After the seal this is what is left to buy.</summary>
    public void StillOnSale(List<ShopItem> into)
    {
        ShopItems(into);
        for (int i = into.Count - 1; i >= 0; i--) if (into[i].Finished || into[i].Kind == ShopItemKind.NextAge) into.RemoveAt(i);
    }

    /// <summary>How many shop items are not finished (see StillOnSale).</summary>
    public int StillOnSaleCount
    {
        get
        {
            var l = _onSaleScratch ??= new List<ShopItem>(64);
            StillOnSale(l);
            return l.Count;
        }
    }
    private List<ShopItem>? _onSaleScratch;
}
