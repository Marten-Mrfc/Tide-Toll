using System;
using System.Collections.Generic;

namespace TideToll.Sim.Town;

/// <summary>The second age's purchases, views for the game, achievements and hints.</summary>
public sealed partial class Town
{
    public double FerryPrice => TownRulesRiver.FerryPrice;
    public double BerthPrice => TownRulesRiver.BerthPrice;
    public double CranePrice => TownRulesRiver.CraneBase * Math.Pow(TownRulesRiver.CraneGrowth, R?.Crane ?? 0);
    public double BoatPricesPrice => TownRulesRiver.PricesBase * Math.Pow(TownRulesRiver.PricesGrowth, R?.Prices ?? 0);
    public double RiverValuePrice(Good g) => (g == Good.Wool ? TownRulesRiver.WoolValueBase : TownRulesRiver.ClothValueBase) * Math.Pow(TownRulesRiver.RiverValueGrowth, g == Good.Wool ? R?.WoolValue ?? 0 : R?.ClothValue ?? 0);
    public double RaftPrice => TownRulesRiver.RaftBase * Math.Pow(TownRulesRiver.RaftGrowth, R?.Raft ?? 0);
    public double RopePrice => TownRulesRiver.RopeBase * Math.Pow(TownRulesRiver.RopeGrowth, R?.Rope ?? 0);
    public double TollPrice => TownRulesRiver.TollTrackBase * Math.Pow(TownRulesRiver.TollGrowth, R?.Toll ?? 0);
    public double WinchPrice => TownRulesRiver.WinchBase * Math.Pow(TownRulesRiver.WinchGrowth, R?.Winch ?? 0);

    /// <summary>Why a second-age item cannot be bought now ("" when it can, ignoring coin).</summary>
    public string RiverLockReason(ShopItemKind k, int index = -1)
    {
        var r = R;
        if (r == null) return "Comes with the second age";
        bool ferry = CrossingExists(RiverLinkKind.Ferry), draw = CrossingExists(RiverLinkKind.Drawbridge);
        return k switch
        {
            ShopItemKind.Ferry => r.FerryBought ? "Already built" : !r.FerryOffered ? "Unlocks at level " + (Plan.MaxLevel + 6) : "",
            ShopItemKind.Berth => r.BerthBought ? "Already built" : !r.BerthOffered ? "Unlocks at level " + (Plan.MaxLevel + 7) : "",
            ShopItemKind.Crane => !r.CraneOffered ? "Unlocks at level " + (Plan.MaxLevel + 10) : r.Crane >= TownRulesRiver.CraneMax ? "The biggest crane" : "",
            ShopItemKind.BoatPrices => r.Reached < 1 ? "Comes with the landing" : r.Prices >= TownRulesRiver.PricesMax ? "Best prices reached" : "",
            ShopItemKind.RiverValue when index == (int)Good.Wool => r.Reached < 4 ? "Comes with the pasture" : r.WoolValue >= TownRulesRiver.RiverValueMax ? "Best price reached" : "",
            ShopItemKind.RiverValue => r.Reached < 5 ? "Comes with the weaver" : r.ClothValue >= TownRulesRiver.RiverValueMax ? "Best price reached" : "",
            ShopItemKind.Raft => !ferry ? "Comes with the ferry" : r.Raft >= TownRulesRiver.RaftMax ? "The biggest raft" : "",
            ShopItemKind.Rope => !ferry ? "Comes with the ferry" : r.Rope >= TownRulesRiver.RopeMax ? "The best rope" : "",
            ShopItemKind.Toll => !draw ? "Comes with the drawbridge" : r.Toll >= TownRulesRiver.TollMax ? "The highest toll" : "",
            ShopItemKind.Winch => !draw ? "Comes with the drawbridge" : r.Winch >= TownRulesRiver.WinchMax ? "The fastest winch" : "",
            _ => "Not for sale",
        };
    }

    private void AddRiverItems(List<ShopItem> into)
    {
        var r = R!;
        if (r.FerryOffered && !r.FerryBought) into.Add(Item(ShopItemKind.Ferry, -1, "The ferry", 0, FerryPrice, RiverLockReason(ShopItemKind.Ferry)));
        if (r.BerthOffered && !r.BerthBought) into.Add(Item(ShopItemKind.Berth, -1, "A second berth", 0, BerthPrice, RiverLockReason(ShopItemKind.Berth)));
        if (r.CraneOffered) into.Add(Item(ShopItemKind.Crane, -1, "Crane", r.Crane, CranePrice, RiverLockReason(ShopItemKind.Crane)));
        if (r.Reached >= 1) into.Add(Item(ShopItemKind.BoatPrices, -1, "Better boat prices", r.Prices, BoatPricesPrice, RiverLockReason(ShopItemKind.BoatPrices)));
        if (r.Reached >= 4) into.Add(Item(ShopItemKind.RiverValue, (int)Good.Wool, "Wool price", r.WoolValue, RiverValuePrice(Good.Wool), RiverLockReason(ShopItemKind.RiverValue, (int)Good.Wool)));
        if (r.Reached >= 5) into.Add(Item(ShopItemKind.RiverValue, (int)Good.Cloth, "Cloth price", r.ClothValue, RiverValuePrice(Good.Cloth), RiverLockReason(ShopItemKind.RiverValue, (int)Good.Cloth)));
        if (CrossingExists(RiverLinkKind.Ferry))
        {
            into.Add(Item(ShopItemKind.Raft, -1, "A bigger raft", r.Raft, RaftPrice, RiverLockReason(ShopItemKind.Raft)));
            into.Add(Item(ShopItemKind.Rope, -1, "A faster rope", r.Rope, RopePrice, RiverLockReason(ShopItemKind.Rope)));
        }
        if (CrossingExists(RiverLinkKind.Drawbridge))
        {
            into.Add(Item(ShopItemKind.Toll, -1, "A higher toll", r.Toll, TollPrice, RiverLockReason(ShopItemKind.Toll)));
            into.Add(Item(ShopItemKind.Winch, -1, "A faster winch", r.Winch, WinchPrice, RiverLockReason(ShopItemKind.Winch)));
        }
    }

    /// <summary>Buys a second-age item (kind and index as in ShopItems). False when locked, at its end or short of coin.</summary>
    public bool BuyRiver(ShopItemKind k, int index = -1)
    {
        var r = R;
        if (r == null || RiverLockReason(k, index).Length > 0) return false;
        double p = k switch
        {
            ShopItemKind.Ferry => FerryPrice, ShopItemKind.Berth => BerthPrice, ShopItemKind.Crane => CranePrice,
            ShopItemKind.BoatPrices => BoatPricesPrice, ShopItemKind.RiverValue => RiverValuePrice((Good)index),
            ShopItemKind.Raft => RaftPrice, ShopItemKind.Rope => RopePrice, ShopItemKind.Toll => TollPrice, ShopItemKind.Winch => WinchPrice,
            _ => double.PositiveInfinity,
        };
        if (!Pay(p)) return false;
        int level = 1;
        switch (k)
        {
            case ShopItemKind.Ferry: r.FerryBought = true; Emit(new TownEvent { Kind = TownEventKind.BuildingOpened, Index = -1, Item = k, Plot = -1, Text = "The ferry" }); break;
            case ShopItemKind.Berth: r.BerthBought = true; Emit(new TownEvent { Kind = TownEventKind.BuildingOpened, Index = -1, Item = k, Plot = -1, Text = "A second berth" }); break;
            case ShopItemKind.Crane: level = ++r.Crane; break;
            case ShopItemKind.BoatPrices: level = ++r.Prices; break;
            case ShopItemKind.RiverValue: level = index == (int)Good.Wool ? ++r.WoolValue : ++r.ClothValue; break;
            case ShopItemKind.Raft: level = ++r.Raft; break;
            case ShopItemKind.Rope: level = ++r.Rope; break;
            case ShopItemKind.Toll: level = ++r.Toll; break;
            case ShopItemKind.Winch: level = ++r.Winch; break;
        }
        Bought(k, index, p, level);
        return true;
    }

    public bool BuyFerry() => BuyRiver(ShopItemKind.Ferry);
    public bool BuyBerth() => BuyRiver(ShopItemKind.Berth);
    public bool BuyCrane() => BuyRiver(ShopItemKind.Crane);
    public bool BuyBoatPrices() => BuyRiver(ShopItemKind.BoatPrices);
    public bool BuyRiverValue(Good g) => BuyRiver(ShopItemKind.RiverValue, (int)g);
    public bool BuyRaft() => BuyRiver(ShopItemKind.Raft);
    public bool BuyRope() => BuyRiver(ShopItemKind.Rope);
    public bool BuyToll() => BuyRiver(ShopItemKind.Toll);
    public bool BuyWinch() => BuyRiver(ShopItemKind.Winch);

    /// <summary>Second-age purchase counts by kind (Ferry..Winch), for the bots and the tool.</summary>
    public int RiverTrackLevel(ShopItemKind k, int index = -1) => R == null ? 0 : k switch
    {
        ShopItemKind.Ferry => R.FerryBought ? 1 : 0, ShopItemKind.Berth => R.BerthBought ? 1 : 0, ShopItemKind.Crane => R.Crane,
        ShopItemKind.BoatPrices => R.Prices, ShopItemKind.RiverValue => index == (int)Good.Wool ? R.WoolValue : R.ClothValue,
        ShopItemKind.Raft => R.Raft, ShopItemKind.Rope => R.Rope, ShopItemKind.Toll => R.Toll, ShopItemKind.Winch => R.Winch, _ => 0,
    };

    /// <summary>Rough extra coin per minute of a second-age item (the shop's Worth and the bots' estimate).</summary>
    internal double RiverWorth(ShopItemKind k, int index, double rate)
    {
        if (R == null) return 0;
        double all = Math.Max(1, CoinEarned), landing = R.EarnedLanding / all;
        return k switch
        {
            ShopItemKind.Ferry => rate * 0.08,
            ShopItemKind.Berth => rate * Math.Max(0.1, landing * 0.4),
            ShopItemKind.Crane => rate * Math.Max(0.05, landing * 0.25),
            ShopItemKind.BoatPrices => rate * landing * TownRulesRiver.PricesStep,
            ShopItemKind.RiverValue => rate * Math.Max(0.02, R.EarnedBy[index] / all) * TownRulesRiver.RiverValueStep,
            ShopItemKind.Raft or ShopItemKind.Rope => rate * 0.03,
            ShopItemKind.Toll => TownRulesRiver.TollBase * TownRulesRiver.TollStep * 3,
            ShopItemKind.Winch => rate * 0.04,
            _ => 0,
        };
    }

    // ---- Views ----

    public int BoatCount => R?.Boats.Length ?? 0;

    /// <summary>Boat slot i (0..BoatCount-1); Phase Gone: draw nothing.</summary>
    public BoatView Boat(int i)
    {
        var b = R!.Boats[i];
        return new BoatView
        {
            Id = b.Id, Kind = b.Kind, Phase = b.Phase, Good = b.Good, Want = b.Want, Loaded = b.Loaded,
            Patience = b.Phase is BoatPhase.Moored or BoatPhase.Waiting ? Math.Max(0, b.Patience) : 0,
            PatienceMax = b.Phase == BoatPhase.Waiting ? TownRulesRiver.WaitPatience : TownRulesRiver.BoatPatience[(int)b.Kind],
            Berth = b.Berth, Request = b.Request, Skipper = TownRulesRiver.SkipperNames[b.Skipper],
            X = b.X, Y = b.Y, PrevX = b.PrevX, PrevY = b.PrevY, Heading = b.Heading,
        };
    }

    /// <summary>The skipper's request of a boat in words, or "" when it carries none.</summary>
    public string BoatRequestText(int i)
    {
        var b = R!.Boats[i];
        if (!b.Request || b.Phase is BoatPhase.Gone or BoatPhase.Leaving) return "";
        return "The " + (b.Kind == BoatKind.Barge ? "barge " : "rowboat ") + TownRulesRiver.SkipperNames[b.Skipper] + " wants " + b.Want + " " + b.Good + " before the tide turns";
    }

    /// <summary>The ferry: raft position 0 (near bank) .. 1 (far bank), moving direction, people aboard.</summary>
    public FerryView Ferry
    {
        get
        {
            var r = R;
            if (r == null || !CrossingExists(RiverLinkKind.Ferry)) return default;
            var l = RiverPlan!.Links[RiverPlan.LinkOf(RiverLinkKind.Ferry)];
            l.At(r.RaftPos * l.Length, out double x, out double y, out double h);
            return new FerryView { Built = true, Pos = r.RaftPos, Dir = r.RaftDir, Aboard = r.AboardN, Capacity = FerryCapacity, X = x, Y = y, Heading = h,
                WaitingNear = WaitingAt(l.Id, l.A), WaitingFar = WaitingAt(l.Id, l.B), Trips = r.RaftTrips };
        }
    }

    /// <summary>Villager id of the k-th person aboard the raft (k below Ferry.Aboard).</summary>
    public int FerryRider(int k) => R!.Aboard[k];

    /// <summary>People waiting at a crossing's bank (bank 0 near, 1 far).</summary>
    public int CrossingWaiting(RiverLinkKind k, int bank)
    {
        if (R == null) return 0;
        int li = RiverPlan!.LinkOf(k);
        if (li < 0) return 0;
        var l = RiverPlan.Links[li];
        return WaitingAt(li, bank == 0 ? l.A : l.B);
    }

    /// <summary>People who have crossed by this crossing, and per minute since it opened.</summary>
    public long Crossings(RiverLinkKind k) => R?.Crossed[(int)k] ?? 0;
    public double CrossingsPerMinute(RiverLinkKind k) => R == null || R.OpenSeconds[(int)k] < 1 ? 0 : R.Crossed[(int)k] * 60 / R.OpenSeconds[(int)k];

    public bool RiverPlotGiven(int plot) => R != null && R.PlotGiven[plot];
    public bool RiverLinkOpen(int link) => R != null && R.LinkOpen[link] && (RiverPlan!.Links[link].Kind != RiverLinkKind.Ferry || R.FerryBought);
    public double LandingCoin => R?.EarnedLanding ?? 0;
    public double TollCoin => R?.EarnedToll ?? 0;
    public double RiverMarketCoin => R?.EarnedMarket ?? 0;
    public long Tolls => R?.Tolls ?? 0;
    public int BoatsFilled => R?.BoatsFilled ?? 0;
    public int BoatsCome => R?.BoatsCome ?? 0;
    public int BoatsLeftShort => R?.BoatsShort ?? 0;
    public long RiverDelivered(Good g) => R?.Delivered[(int)g] ?? 0;
    public double RiverEarnedBy(Good g) => R?.EarnedBy[(int)g] ?? 0;
    public double RiverStartTime => R?.StartTime ?? -1;

    /// <summary>Second-age hint, or "" (Hint shows it first when it says something).</summary>
    public string RiverHint
    {
        get
        {
            var r = R;
            if (r == null) return "";
            foreach (var b in r.Boats) if (b.Phase == BoatPhase.Waiting && b.Patience < 40) return r.BerthOffered && !r.BerthBought ? "A boat waits for a berth. A second berth would help." : "A boat waits for a berth.";
            if (CrossingWaiting(RiverLinkKind.Ford, 0) + CrossingWaiting(RiverLinkKind.Ford, 1) >= 3 && !CrossingExists(RiverLinkKind.Ferry))
                return r.FerryOffered ? "People wait at the ford for low water. The ferry would carry them now." : "People wait at the ford for low water.";
            var f = Ferry;
            if (f.Built && f.WaitingNear + f.WaitingFar >= f.Capacity * 2 && r.Raft < TownRulesRiver.RaftMax) return "A queue at the ferry. A bigger raft would help.";
            foreach (var b in r.Boats) if (b.Phase == BoatPhase.Moored && b.Patience < 30 && b.Loaded < b.Want) return "A boat will leave soon. Send it " + b.Good + ".";
            return "";
        }
    }

    // ---- Achievements (16) ----

    public static readonly (string Name, string Text)[] RiverAchievements =
    {
        ("First mooring", "A boat moors at the landing."),
        ("Full hold", "Fill a boat."),
        ("Ten holds", "Fill 10 boats."),
        ("Fifty holds", "Fill 50 boats."),
        ("Wet feet", "Cross the ford 20 times."),
        ("Ferryman", "The ferry makes 50 crossings."),
        ("Dry shoes", "Cross the footbridge 100 times."),
        ("Raise the bridge", "Lift the drawbridge 10 times."),
        ("Toll keeper", "Take 25 tolls."),
        ("First fleece", "Bring 50 Wool."),
        ("Weaver's pride", "Bring 50 Cloth."),
        ("Skipper's friend", "Meet 5 skippers' requests."),
        ("Two berths", "Build the second berth."),
        ("The crane", "Build the crane."),
        ("River trade", "Earn 100000 coin at the landing."),
        ("Borough", "Win the borough seal."),
    };

    public int RiverAchievementCount => RiverAchievements.Length;

    private double RiverAchValue(int i)
    {
        var r = R!;
        return i switch
        {
            0 => r.BoatsCome > 0 && (r.BoatsFilled + r.BoatsShort > 0 || AnyMoored()) ? 1 : 0,
            1 => r.BoatsFilled, 2 => r.BoatsFilled, 3 => r.BoatsFilled,
            4 => r.Crossed[(int)RiverLinkKind.Ford], 5 => r.Crossed[(int)RiverLinkKind.Ferry], 6 => r.Crossed[(int)RiverLinkKind.Footbridge],
            7 => r.Lifts, 8 => r.Tolls, 9 => r.Delivered[(int)Good.Wool] + 0, 10 => r.Delivered[(int)Good.Cloth],
            11 => r.BoatRequestsMet, 12 => r.BerthBought ? 1 : 0, 13 => r.Crane, 14 => r.EarnedLanding, 15 => r.Seal ? 1 : 0, _ => 0,
        };
    }

    private static readonly double[] RiverAchTarget = { 1, 1, 10, 50, 20, 50, 100, 10, 25, 50, 50, 5, 1, 1, 100000, 1 };

    private bool AnyMoored() { foreach (var b in R!.Boats) if (b.Phase == BoatPhase.Moored) return true; return false; }

    /// <summary>Second-age achievement i (0..15): same view as the first age's, Index = 100 + i, Id "river0".."river15".</summary>
    public AchievementView RiverAchievement(int i)
    {
        double v = R == null ? 0 : RiverAchValue(i), t = RiverAchTarget[i];
        return new AchievementView { Index = 100 + i, Id = "river" + i, Name = RiverAchievements[i].Name, Description = RiverAchievements[i].Text, Value = v, Target = t, Progress = Math.Clamp(v / t, 0, 1), Unlocked = R != null && R.Achieved[i] };
    }

    private void CheckRiverAchievements()
    {
        var r = R!;
        for (int i = 0; i < RiverAchievements.Length; i++)
        {
            if (r.Achieved[i] || RiverAchValue(i) < RiverAchTarget[i]) continue;
            r.Achieved[i] = true;
            Emit(new TownEvent { Kind = TownEventKind.AchievementUnlocked, Index = 100 + i, Text = RiverAchievements[i].Name });
        }
    }
}

public struct BoatView
{
    public int Id;
    public BoatKind Kind;
    public BoatPhase Phase;
    public Good Good;
    public int Want, Loaded;
    public double Patience, PatienceMax;
    public int Berth;
    public bool Request;
    public string Skipper;
    public double X, Y, PrevX, PrevY, Heading;
}

public struct FerryView
{
    public bool Built;
    public double Pos;
    public int Dir, Aboard, Capacity, WaitingNear, WaitingFar, Trips;
    public double X, Y, Heading;
}
