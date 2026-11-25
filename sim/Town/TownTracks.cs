using System;
using System.Collections.Generic;

namespace TideToll.Sim.Town;

/// <summary>Rules v2 tracks: Tools, a value track per good, extending cottages, the bell track.</summary>
public sealed partial class Town
{
    private static readonly string[] GoodNames = { "", "Water", "Grain", "Flour" };

    public int ToolsLevel => S.ToolsLevel;
    /// <summary>Coin earned by deliveries of a good.</summary>
    public double EarnedBy(Good g) => S.EarnedBy[(int)g];
    public int BellTrackLevel => S.BellTrackLevel;
    public int ValueLevel(Good g) => S.ValueLevel[(int)g];
    public int Extensions(int plot) => S.Extensions[plot];
    public int ExtensionsBought => S.ExtensionsBought;

    public double ToolsPrice => TownRules.ToolsBase * Math.Pow(TownRules.ToolsGrowth, S.ToolsLevel);
    public double ValuePrice(Good g) => TownRules.ValueBase[(int)g] * Math.Pow(TownRules.ValueGrowth[(int)g], S.ValueLevel[(int)g]);
    public double ExtendPrice => TownRules.ExtendBase * Math.Pow(TownRules.ExtendGrowth, S.ExtensionsBought);
    public double BellTrackPrice => TownRules.BellTrackBase * Math.Pow(TownRules.BellTrackGrowth, S.BellTrackLevel);

    /// <summary>"" when Tools can be bought now (ignoring coin), else why not.</summary>
    public string ToolsLockReason => S.ToolsLevel >= TownRules.ToolsMax ? "Best tools reached"
        : S.Level < TownRules.ToolsFromLevel ? "Unlocks at level " + TownRules.ToolsFromLevel : "";

    /// <summary>Whether the value track of a good is shown: Water always, Grain once the first field is given, Flour once the mill works.</summary>
    public bool ValueTrackShown(Good g)
    {
        if (g == Good.Water) return true;
        foreach (var w in S.Workplaces)
        {
            var k = Plan.Plots[w.Plot].Kind;
            if (g == Good.Grain && k == PlotKind.Field && w.Open) return true;
            if (g == Good.Flour && k == PlotKind.Mill && w.Open) return true;
        }
        return false;
    }

    public string ValueLockReason(Good g) => g is < Good.Water or > Good.Flour ? "No such good"
        : !ValueTrackShown(g) ? (g == Good.Grain ? "Comes with the first field" : "Comes with the mill")
        : S.ValueLevel[(int)g] >= TownRules.ValueMax ? "Best price reached" : "";

    public string BellTrackLockReason => !S.BellUnlocked ? "Comes with the bell"
        : S.BellTrackLevel >= TownRules.BellTrackMax ? "The bell is at its best" : "";

    /// <summary>Whether a cottage can be extended now (ignoring coin), with the reason in words if not.</summary>
    public bool CanExtend(int plot, out string reason)
    {
        reason = "";
        if (plot < 0 || plot >= Plan.Plots.Length || Plan.Plots[plot].Kind != PlotKind.Cottage) { reason = "Not a cottage"; return false; }
        if (!S.PlotGiven[plot]) { reason = "No such cottage yet"; return false; }
        if (S.Level < TownRules.ExtendFromLevel) { reason = "Unlocks at level " + TownRules.ExtendFromLevel; return false; }
        if (S.Extensions[plot] >= TownRules.ExtendMax) { reason = "Already extended twice"; return false; }
        return true;
    }

    /// <summary>The cottage the shop suggests extending: a full one with fewest extensions (lowest id on a tie), else any that can be; -1 if none.</summary>
    public int SuggestedExtend()
    {
        int best = -1; bool bestFull = false;
        foreach (var p in Plan.Plots)
        {
            if (!CanExtend(p.Id, out _)) continue;
            bool full = Residents(p.Id) >= CottageRoom(p.Id);
            if (best < 0 || (full && !bestFull) || (full == bestFull && S.Extensions[p.Id] < S.Extensions[best])) { best = p.Id; bestFull = full; }
        }
        return best;
    }

    private void AddTrackItems(List<ShopItem> into)
    {
        into.Add(Item(ShopItemKind.Tools, -1, "Tools", S.ToolsLevel, ToolsPrice, ToolsLockReason));
        for (int g = 1; g <= 3; g++)
            if (ValueTrackShown((Good)g)) into.Add(Item(ShopItemKind.Value, g, GoodNames[g] + " price", S.ValueLevel[g], ValuePrice((Good)g), ValueLockReason((Good)g)));
        int c = SuggestedExtend();
        if (c >= 0) into.Add(Item(ShopItemKind.Extend, c, "Extend a cottage", S.ExtensionsBought, ExtendPrice, ""));
        else into.Add(Item(ShopItemKind.Extend, -1, "Extend a cottage", S.ExtensionsBought, ExtendPrice,
            S.Level < TownRules.ExtendFromLevel ? "Unlocks at level " + TownRules.ExtendFromLevel : "Every cottage is extended"));
        if (S.BellUnlocked) into.Add(Item(ShopItemKind.BellTrack, -1, "Louder bell", S.BellTrackLevel, BellTrackPrice, BellTrackLockReason));
    }

    public bool BuyTools()
    {
        if (ToolsLockReason.Length > 0) return false;
        double p = ToolsPrice;
        if (!Pay(p)) return false;
        S.ToolsLevel++;
        Bought(ShopItemKind.Tools, -1, p, S.ToolsLevel);
        return true;
    }

    public bool BuyValue(Good g)
    {
        if (ValueLockReason(g).Length > 0) return false;
        double p = ValuePrice(g);
        if (!Pay(p)) return false;
        S.ValueLevel[(int)g]++;
        Bought(ShopItemKind.Value, (int)g, p, S.ValueLevel[(int)g]);
        return true;
    }

    /// <summary>Extends one cottage: it houses one more villager.</summary>
    public bool Extend(int plot)
    {
        if (!CanExtend(plot, out _)) return false;
        double p = ExtendPrice;
        if (!Pay(p)) return false;
        S.Extensions[plot]++;
        S.ExtensionsBought++;
        var pl = Plan.Plots[plot];
        Emit(new TownEvent { Kind = TownEventKind.CottageExtended, Plot = plot, Level = S.Extensions[plot], X = pl.DoorX + 0.5, Y = pl.DoorY + 0.5 });
        Bought(ShopItemKind.Extend, plot, p, S.ExtensionsBought);
        return true;
    }

    public bool BuyBellTrack()
    {
        if (BellTrackLockReason.Length > 0) return false;
        double p = BellTrackPrice;
        if (!Pay(p)) return false;
        S.BellTrackLevel++;
        Bought(ShopItemKind.BellTrack, -1, p, S.BellTrackLevel);
        return true;
    }
}
