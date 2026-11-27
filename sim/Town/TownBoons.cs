using System;
using System.Collections.Generic;

namespace TideToll.Sim.Town;

/// <summary>Rules v3: the boons of the choices of two (pairs 0/1, 2/3, 4/5, 6/7, one pair per TownRules.ChoiceLevels entry).</summary>
public enum Boon : byte
{
    WiderLanes = 0, StrongerBacks = 1,
    DeeperWell = 2, LongerRows = 3,
    ThirdRoom = 4, FasterHands = 5,
    MarketRight = 6, MillersGuild = 7,
}

/// <summary>Rules v3: the open choice of two boons (Open false when none is waiting).</summary>
public struct ChoiceView
{
    public bool Open;
    public int Choice;            // 0..3 (index into TownRules.ChoiceLevels)
    public int Level;             // town level that offered it
    public Boon A, B;             // side 0 and side 1
    public string AName, AText, BName, BText;
    public double AWorth, BWorth; // estimated extra coin per minute now (Town.BoonWorth)
}

/// <summary>Rules v3: choices of two, milestones and the worth estimate of every shop item.</summary>
public sealed partial class Town
{
    private static readonly string[] BoonNames =
    {
        "Wider lanes", "Stronger backs", "A deeper well", "Longer field rows",
        "A third room", "Faster hands", "A market right", "A miller's guild",
    };
    public static string BoonName(Boon b) => BoonNames[(int)b];
    private static string Pct(double x) => Math.Round(x * 100).ToString(System.Globalization.CultureInfo.InvariantCulture) + " %";

    /// <summary>One plain line saying what the boon does, with its number (built from TownRules, so it follows tuning).</summary>
    public static string BoonText(Boon b) => b switch
    {
        Boon.WiderLanes => "Every road carries " + Pct(TownRules.BoonLanes) + " more people before it crowds.",
        Boon.StrongerBacks => "Everyone walks " + Pct(TownRules.BoonWalk) + " faster.",
        Boon.DeeperWell => "Water is drawn " + Pct(TownRules.BoonWellWork) + " faster at the well.",
        Boon.LongerRows => "Grain is cut " + Pct(TownRules.BoonFieldWork) + " faster in the fields.",
        Boon.ThirdRoom => "Every cottage houses one more villager.",
        Boon.FasterHands => "All work at the well, fields and mill is " + Pct(TownRules.BoonHands) + " faster.",
        Boon.MarketRight => "Grain pays " + Pct(TownRules.BoonGrain) + " more.",
        Boon.MillersGuild => "Flour pays " + Pct(TownRules.BoonFlour) + " more.",
        _ => "",
    };
    public bool HasBoon(Boon b) => S.Boons[(int)b];

    /// <summary>Fills <paramref name="into"/> (cleared first) with the boons the town holds, in Boon order.</summary>
    public void BoonsHeld(List<Boon> into)
    {
        into.Clear();
        for (int i = 0; i < S.Boons.Length; i++) if (S.Boons[i]) into.Add((Boon)i);
    }

    /// <summary>The boon taken at choice <paramref name="choice"/> (0..3), or -1 while not decided or not offered.</summary>
    public int ChoicePicked(int choice) => S.ChoicePicked[choice];

    private int OpenChoice()
    {
        int open = S.ChoicesOffered & ~S.ChoicesMade;
        for (int i = 0; i < TownRules.ChoiceLevels.Length; i++) if ((open & (1 << i)) != 0) return i;
        return -1;
    }

    public bool ChoiceOpen => OpenChoice() >= 0;

    /// <summary>The oldest undecided choice (Open false when there is none).</summary>
    public ChoiceView Choice
    {
        get
        {
            int c = OpenChoice();
            if (c < 0) return new ChoiceView { Choice = -1, AName = "", AText = "", BName = "", BText = "" };
            Boon a = (Boon)(2 * c), b = (Boon)(2 * c + 1);
            return new ChoiceView
            {
                Open = true, Choice = c, Level = TownRules.ChoiceLevels[c], A = a, B = b,
                AName = BoonName(a), AText = BoonText(a), BName = BoonName(b), BText = BoonText(b),
                AWorth = BoonWorth(a), BWorth = BoonWorth(b),
            };
        }
    }

    /// <summary>Takes side 0 (A) or 1 (B) of the open choice, for good. False when no choice is open.</summary>
    public bool ChooseSide(int side)
    {
        int c = OpenChoice();
        if (c < 0 || side is < 0 or > 1) return false;
        return Choose((Boon)(2 * c + side));
    }

    /// <summary>Takes a boon of the open choice, for good; the other is gone. False when it is not on the open choice.</summary>
    public bool Choose(Boon b)
    {
        int c = OpenChoice();
        if (c < 0 || (int)b / 2 != c) return false;
        S.ChoicesMade |= 1 << c;
        S.ChoicePicked[c] = (int)b;
        S.Boons[(int)b] = true;
        Emit(new TownEvent { Kind = TownEventKind.BoonChosen, Index = (int)b, Level = c, Text = BoonName(b) });
        return true;
    }

    private void OfferChoicesAt(int level)
    {
        for (int i = 0; i < TownRules.ChoiceLevels.Length; i++)
            if (TownRules.ChoiceLevels[i] == level && (S.ChoicesOffered & (1 << i)) == 0)
            {
                S.ChoicesOffered |= 1 << i;
                Emit(new TownEvent { Kind = TownEventKind.ChoiceOffered, Index = i, Level = level });
            }
    }

    // ---- Effects ----

    internal int BoonRoom => S.Boons[(int)Boon.ThirdRoom] ? TownRules.BoonRoom : 0;

    internal double BoonValue(Good g) =>
        g == Good.Grain && S.Boons[(int)Boon.MarketRight] ? 1 + TownRules.BoonGrain
        : g == Good.Flour && S.Boons[(int)Boon.MillersGuild] ? 1 + TownRules.BoonFlour : 1;

    /// <summary>Seconds of work at a workplace of this kind before Tools and the bell (boons included).</summary>
    internal double WorkTime(PlotKind k)
    {
        double t = k == PlotKind.Well ? TownRules.WellWork : k == PlotKind.Field ? TownRules.FieldWork : TownRules.MillWork;
        if (k == PlotKind.Well && S.Boons[(int)Boon.DeeperWell]) t /= 1 + TownRules.BoonWellWork;
        if (k == PlotKind.Field && S.Boons[(int)Boon.LongerRows]) t /= 1 + TownRules.BoonFieldWork;
        if (S.Boons[(int)Boon.FasterHands]) t /= 1 + TownRules.BoonHands;
        return t;
    }

    // ---- Worth estimates (coin per minute now) ----

    private const double WorkShareOfTrip = 0.4;   // rough share of a trip spent working

    internal double ShareOf(Good g) => S.EarnedBy[(int)g] / Math.Max(1, S.CoinEarned);

    /// <summary>Estimated extra coin per minute the boon would add now (rough, from the town's own numbers).</summary>
    public double BoonWorth(Boon b)
    {
        double rate = Math.Max(6, CoinPerMinute);
        int n = Math.Max(1, S.Villagers.Length);
        switch (b)
        {
            case Boon.DeeperWell: return rate * ShareOf(Good.Water) * WorkShareOfTrip * (1 - 1 / (1 + TownRules.BoonWellWork));
            case Boon.LongerRows: return rate * (ShareOf(Good.Grain) + ShareOf(Good.Flour)) * WorkShareOfTrip * (1 - 1 / (1 + TownRules.BoonFieldWork));
            case Boon.WiderLanes:
            {
                int over = 0, all = 0;
                for (int r = 0; r < Plan.Roads.Length; r++)
                {
                    if (!S.RoadOpen[r]) continue;
                    all += S.RoadWalkers[r];
                    if (S.RoadWalkers[r] + 1 > Comfort(r)) over += S.RoadWalkers[r]; // crowded, or one more would crowd it
                }
                double crowded = all > 0 ? (double)over / all : 0;
                return rate * (0.1 + crowded) * 0.6 * (1 - 1 / (1 + TownRules.BoonLanes));
            }
            case Boon.StrongerBacks: return rate * 0.55 * TownRules.BoonWalk;
            case Boon.ThirdRoom:
            {
                int cottages = 0;
                foreach (var p in Plan.Plots) if (p.Kind == PlotKind.Cottage && S.PlotGiven[p.Id]) cottages++;
                return rate / n * 0.9 * cottages * TownRules.BoonRoom * 0.5;
            }
            case Boon.FasterHands:
            {
                int queued = 0;
                foreach (var w in S.Workplaces) if (w.Open) queued += w.QueueLen;
                return rate * (WorkShareOfTrip + (double)queued / n) * (1 - 1 / (1 + TownRules.BoonHands));
            }
            case Boon.MarketRight: return rate * ShareOf(Good.Grain) * TownRules.BoonGrain;
            case Boon.MillersGuild: return rate * Math.Max(ShareOf(Good.Flour), 0.3) * TownRules.BoonFlour;
            default: return 0;
        }
    }

    /// <summary>Estimated extra coin per minute one more step of a shop item would add (index as in ShopItem), at coin rate <paramref name="rate"/> per minute.</summary>
    internal double WorthAt(ShopItemKind k, int index, double rate)
    {
        int n = Math.Max(1, S.Villagers.Length);
        switch (k)
        {
            case ShopItemKind.Villager: return rate / n * 0.9;
            case ShopItemKind.Pace: return S.PaceLevel >= PaceCap ? 0 : rate * 0.55 * (PaceFactorAt(S.PaceLevel + 1) / PaceFactorAt(S.PaceLevel) - 1);
            case ShopItemKind.Load: return rate / LoadUnits;
            case ShopItemKind.Pave: return index < 0 ? 0 : rate * 0.05;
            case ShopItemKind.Building: return rate * 0.3;
            case ShopItemKind.Tools: return rate * 0.3 * (ToolsFactorAt(S.ToolsLevel + 1) / ToolsFactorAt(S.ToolsLevel) - 1);
            case ShopItemKind.Value:
            {
                int l = S.ValueLevel[index];
                double share = Math.Max(0.15, ShareOf((Good)index));
                return rate * share * (ValueFactorAt(l + 1) / ValueFactorAt(l) - 1);
            }
            case ShopItemKind.Extend:
            {
                if (index < 0 || HousingRoom > 0) return 0;
                double ep = ExtendPrice;
                return rate / n * 0.9 * ep / (ep + VillagerPrice);
            }
            case ShopItemKind.BellTrack: return rate * 0.025;
            case ShopItemKind.Training: return TrainingWorth(rate);
            case ShopItemKind.Regulars: return S.River != null ? 0 : rate * TownRules.RegularsStep / CoinBonus;
            case ShopItemKind.Hand:
            {
                if (index < 0 || index >= S.Workplaces.Length) return 0;
                var w = S.Workplaces[index];
                var pk = Plan.Plots[w.Plot].Kind;
                double share = pk == PlotKind.Well ? ShareOf(Good.Water) : pk == PlotKind.Mill ? ShareOf(Good.Flour) : ShareOf(Good.Grain) + ShareOf(Good.Flour);
                return rate * share * 0.5 / Math.Max(1, w.Places);
            }
            default: return RiverWorth(k, index, rate);
        }
    }

    /// <summary>Rules v3: the worth shown on a shop item (coin per minute at the current rate).</summary>
    internal double EstimateWorth(ShopItemKind k, int index) => Math.Round(WorthAt(k, index, Math.Max(6, CoinPerMinute)), 1);

    // ---- Milestones ----

    private static readonly string[] PaceMilestones = { "Long strides", "Sure feet", "Quick march", "Light step", "Fleet feet" };
    private static readonly string[] ToolsMilestones = { "Sharp tools", "Fine tools", "Master tools" };
    private static readonly string[] ValueMilestones = { "a fair name", "a good name" };

    /// <summary>Name of the milestone at a track level (every TownRules.MilestoneEvery-th step), or "" when that level is none.</summary>
    public static string MilestoneName(ShopItemKind k, int index, int level)
    {
        if (level <= 0 || level % TownRules.MilestoneEvery != 0) return "";
        int i = level / TownRules.MilestoneEvery - 1;
        string[] names = k == ShopItemKind.Pace ? PaceMilestones : k == ShopItemKind.Tools ? ToolsMilestones : k == ShopItemKind.Value ? ValueMilestones : Array.Empty<string>();
        if (names.Length == 0) return "";
        string name = names[Math.Min(i, names.Length - 1)];
        return k == ShopItemKind.Value && index is >= 1 and <= 3 ? GoodNames[index] + ": " + name : name;
    }
}
