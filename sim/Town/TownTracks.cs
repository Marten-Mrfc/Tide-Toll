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

    public double ToolsPrice => TrackPrice(TownRules.ToolsBase, TownRules.ToolsGrowth, S.ToolsLevel, TownRules.ToolsMax);
    public double ValuePrice(Good g) => TrackPrice(TownRules.ValueBase[(int)g], TownRules.ValueGrowth[(int)g], S.ValueLevel[(int)g], TownRules.ValueMax);
    public double ExtendPrice => TrackPrice(TownRules.ExtendBase, TownRules.ExtendGrowth, S.ExtensionsBought, TownRules.ExtendAt[^1]);
    public double BellTrackPrice => TrackPrice(TownRules.BellTrackBase, TownRules.BellTrackGrowth, S.BellTrackLevel, TownRules.BellTrackMax);

    /// <summary>Green pass: base * growth ^ level up to the first age's last step (exactly as before); the second age's further
    /// steps start at TownRulesRiver.TrackStart of the last first-age price and grow by TownRulesRiver.TrackGrowth each, so the old tracks give a dense ladder of prices.</summary>
    public double TrackPrice(double b, double growth, int level, int firstAgeMax) => S.River == null || level < firstAgeMax
        ? b * Math.Pow(growth, level)
        : b * Math.Pow(growth, firstAgeMax) * TownRulesRiver.TrackStart * Math.Pow(TownRulesRiver.TrackGrowth, level - firstAgeMax);

    /// <summary>Rules v2.1: how many steps of a track are on sale at the current town level (table indexed by level - 1).</summary>
    internal int OnSale(int[] table) => table[Math.Min(S.Level, table.Length) - 1];

    /// <summary>Rules v2.1: "" when step bought+1 of a track is on sale now, else "Unlocks at level N".</summary>
    internal string StepLock(int[] table, int bought)
    {
        if (OnSale(table) > bought) return "";
        for (int i = 0; i < table.Length; i++) if (table[i] > bought) return LevelText(i + 1);
        return "Unlocks at a later level";
    }

    /// <summary>Green pass: in the second age a track goes on past its first-age table (more[] by river level, steps beyond
    /// the table's last value). In the first age this is StepLock exactly.</summary>
    internal string StepLockR(int[] table, int[] more, int bought)
    {
        if (S.River == null || S.Level <= table.Length) return StepLock(table, bought);
        int baseN = table[^1];
        if (baseN + more[Math.Min(S.Level - table.Length, more.Length) - 1] > bought) return "";
        for (int i = 0; i < more.Length; i++) if (baseN + more[i] > bought) return LevelText(table.Length + 1 + i);
        return "Unlocks at a later level";
    }

    private static readonly string[] LevelTexts = BuildLevelTexts();
    private static string[] BuildLevelTexts() { var a = new string[64]; for (int i = 0; i < a.Length; i++) a[i] = "Unlocks at level " + i; return a; }
    internal static string LevelText(int n) => n >= 0 && n < LevelTexts.Length ? LevelTexts[n] : "Unlocks at level " + n;

    /// <summary>Green pass: the last step of each track (the first age's in the first age; more in the second).</summary>
    public int PaceCap => TownRules.PaceMax + (S.River != null ? TownRulesRiver.PaceMore[^1] : 0);
    public int ToolsCap => TownRules.ToolsMax + (S.River != null ? TownRulesRiver.ToolsMore[^1] : 0);
    public int ValueCap => TownRules.ValueMax + (S.River != null ? TownRulesRiver.ValueMore[^1] : 0);
    public int BellTrackCap => TownRules.BellTrackMax + (S.River != null ? TownRulesRiver.BellTrackMore[^1] : 0);
    public int ExtendCap => S.River != null ? TownRulesRiver.ExtendMaxRiver : TownRules.ExtendMax;

    /// <summary>"" when Tools can be bought now (ignoring coin), else why not.</summary>
    public string ToolsLockReason => S.ToolsLevel >= ToolsCap ? "Best tools reached"
        : S.Level < TownRules.ToolsFromLevel ? LevelText(TownRules.ToolsFromLevel) : StepLockR(TownRules.ToolsAt, TownRulesRiver.ToolsMore, S.ToolsLevel);

    /// <summary>Rules v2.1: "" when Pace can be bought now (ignoring coin), else why not.</summary>
    public string PaceLockReason => S.PaceLevel >= PaceCap ? "Fastest pace reached" : StepLockR(TownRules.PaceAt, TownRulesRiver.PaceMore, S.PaceLevel);

    /// <summary>Rules v2.1: "" when another cottage extension is on sale at this town level (ignoring coin and room), else why not.</summary>
    public string ExtendLockReason => S.Level < TownRules.ExtendFromLevel ? LevelText(TownRules.ExtendFromLevel)
        : StepLockR(TownRules.ExtendAt, TownRulesRiver.ExtendMore, S.ExtensionsBought);

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
        : S.ValueLevel[(int)g] >= ValueCap ? "Best price reached"
        : StepLockR(g == Good.Flour ? TownRules.FlourValueAt : TownRules.ValueAt, TownRulesRiver.ValueMore, S.ValueLevel[(int)g]);

    public string BellTrackLockReason => !S.BellUnlocked ? "Comes with the bell"
        : S.BellTrackLevel >= BellTrackCap ? "The bell is at its best" : StepLockR(TownRules.BellTrackAt, TownRulesRiver.BellTrackMore, S.BellTrackLevel);

    /// <summary>Whether a cottage can be extended now (ignoring coin), with the reason in words if not.</summary>
    public bool CanExtend(int plot, out string reason)
    {
        reason = "";
        if (plot < 0 || plot >= Plan.Plots.Length || Plan.Plots[plot].Kind != PlotKind.Cottage) { reason = "Not a cottage"; return false; }
        if (!S.PlotGiven[plot]) { reason = "No such cottage yet"; return false; }
        if (S.Level < TownRules.ExtendFromLevel) { reason = LevelText(TownRules.ExtendFromLevel); return false; }
        if (S.Extensions[plot] >= ExtendCap) { reason = ExtendCap == 2 ? "Already extended twice" : "Already extended three times"; return false; }
        string lk = ExtendLockReason;
        if (lk.Length > 0) { reason = lk; return false; }
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
        string elk = ExtendLockReason;
        if (c < 0 && elk.Length > 0 && S.Level >= TownRules.ExtendFromLevel) into.Add(Item(ShopItemKind.Extend, -1, "Extend a cottage", S.ExtensionsBought, ExtendPrice, elk));
        else if (c >= 0) into.Add(Item(ShopItemKind.Extend, c, "Extend a cottage", S.ExtensionsBought, ExtendPrice, ""));
        else into.Add(Item(ShopItemKind.Extend, -1, "Extend a cottage", S.ExtensionsBought, ExtendPrice,
            S.Level < TownRules.ExtendFromLevel ? LevelText(TownRules.ExtendFromLevel) : "Every cottage is extended"));
        if (S.BellUnlocked) into.Add(Item(ShopItemKind.BellTrack, -1, "Louder bell", S.BellTrackLevel, BellTrackPrice, BellTrackLockReason));
        AddHandItems(into);
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
