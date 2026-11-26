using System.Collections.Generic;
using System;

namespace TideToll.Sim.Town;

/// <summary>Rules v2: requests (someone asks for an amount of one good within a time) and the hint line.</summary>
public sealed partial class Town
{
    /// <summary>Who asks, by index (TownEvent.Index of RequestAsked, RequestView.Who).</summary>
    public static readonly string[] RequestWho = { "The innkeeper", "The miller", "The baker" };
    private static readonly string[] GoodWord = { "", "Water", "Grain", "Flour" };

    public bool RequestActive => S.ReqActive;
    public int RequestsMet => S.ReqMet;
    public int RequestsMissed => S.ReqMissed;
    public int RequestsAsked => S.ReqCount;
    /// <summary>Rules v2.1: how the request amounts have adapted to this player (1 at the start; above 1 after many met).</summary>
    public double RequestScale => S.ReqScale;

    /// <summary>The current request (Active false when none runs).</summary>
    public RequestView Request => new()
    {
        Active = S.ReqActive, Who = S.ReqWho, WhoName = S.ReqActive ? RequestWho[S.ReqWho] : "", Good = S.ReqGood, Amount = S.ReqAmount,
        Delivered = S.ReqDelivered, SecondsLeft = S.ReqActive ? Math.Max(0, S.ReqEnds - Time) : 0,
        Seconds = S.ReqEnds - S.ReqStart, RewardCoin = S.ReqRewardCoin, RewardRenown = S.ReqActive ? RenownFor(S.ReqRewardCoin) : 0,
        NextIn = S.ReqActive || S.ReqNextAt < 0 ? -1 : Math.Max(0, S.ReqNextAt - Time),
    };

    /// <summary>The current request in words ("The miller asks for 12 Grain within 90 seconds"), or "".</summary>
    public string RequestText => S.ReqActive
        ? $"{RequestWho[S.ReqWho]} asks for {S.ReqAmount} {GoodWord[(int)S.ReqGood]} within {Math.Round(S.ReqEnds - S.ReqStart)} seconds"
        : "";

    private static uint Hash(uint a, uint b)
    {
        uint h = a * 0x9E3779B1u ^ (b + 0x7F4A7C15u) * 0x85EBCA77u;
        h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
        return h;
    }

    /// <summary>Coin one unit of a good brings now without the request bonus (Grain at the market when it works).</summary>
    internal double UnitValue(Good g)
    {
        double b = g == Good.Water ? TownRules.WaterValue : g == Good.Flour ? TownRules.FlourValue
            : _marketPlot >= 0 && PlotWorks(_marketPlot) ? TownRules.GrainMarketValue : TownRules.GrainHomeValue;
        return b * ValueFactor(g) * CoinBonus;
    }

    /// <summary>Units per second the workplaces of a good could turn out if every place were busy.</summary>
    internal double Capacity(Good g)
    {
        double c = 0;
        foreach (var w in S.Workplaces)
        {
            if (!w.Open) continue;
            var k = Plan.Plots[w.Plot].Kind;
            if (g == Good.Water && k == PlotKind.Well) c += w.Places * LoadUnits * ToolsFactor / WorkTime(PlotKind.Well);
            if (g == Good.Grain && k == PlotKind.Field) c += w.Places * LoadUnits * ToolsFactor / WorkTime(PlotKind.Field);
            if (g == Good.Flour && k == PlotKind.Mill) c += w.Places * LoadUnits * ToolsFactor / WorkTime(PlotKind.Mill);
        }
        return c;
    }

    private void StepRequests()
    {
        if (S.ReqActive)
        {
            if (Time >= S.ReqEnds)
            {
                S.ReqActive = false;
                S.ReqMissed++;
                S.ReqScale = Math.Max(TownRules.RequestScaleMin, S.ReqScale * TownRules.RequestShrinkMissed);
                S.ReqNextAt = Time + TownRules.RequestGapAfterMiss;
                Emit(new TownEvent { Kind = TownEventKind.RequestMissed, Good = S.ReqGood, Amount = S.ReqAmount, Level = S.ReqDelivered, Index = S.ReqWho });
            }
            return;
        }
        if (S.ReqNextAt < 0 || Time < S.ReqNextAt || S.Charter || S.DriveActive) return; // rules v3: no new request during the charter drive
        // Which goods can be made now: Water always, Grain with a field, Flour with the mill and the market.
        int n = 1; bool grain = Capacity(Good.Grain) > 0, flour = Capacity(Good.Flour) > 0 && _marketPlot >= 0 && PlotWorks(_marketPlot);
        if (grain) n++;
        if (flour) n++;
        uint h = Hash((uint)S.Seed, (uint)S.ReqCount);
        int pick = (int)(h % (uint)n);
        Good g = pick == 0 ? Good.Water : pick == 1 && grain ? Good.Grain : Good.Flour;
        double secs = TownRules.RequestSeconds[(h >> 8) % (uint)TownRules.RequestSeconds.Length];
        double rate = Math.Max(30, CoinPerMinute) / 60;
        double unit = UnitValue(g);
        // Rules v2.2: requests start easy and grow; the first few are scaled down (RequestEaseIn) on top of the adaptation.
        var ease = TownRules.RequestEaseIn;
        double scale = S.ReqScale * ease[Math.Min(S.ReqCount, ease.Length - 1)];
        double amount = TownRules.RequestShare * scale * rate * secs / unit;
        amount = Math.Min(amount, TownRules.RequestCapacityShare * Capacity(g) * secs);
        int amt = Math.Max(Math.Min(TownRules.RequestMinAmount, (int)Math.Ceiling(TownRules.RequestMinAmount * scale)), (int)Math.Round(amount));
        S.ReqActive = true; S.ReqGood = g; S.ReqWho = (int)g - 1; S.ReqAmount = amt; S.ReqDelivered = 0;
        S.ReqStart = Time; S.ReqEnds = Time + secs;
        S.ReqRewardCoin = Math.Ceiling(amt * unit * TownRules.RequestReward);
        S.ReqCount++;
        Emit(new TownEvent { Kind = TownEventKind.RequestAsked, Good = g, Amount = amt, Coin = S.ReqRewardCoin, Value = secs, Index = S.ReqWho });
    }

    private void RequestDelivered(int units)
    {
        S.ReqDelivered += units;
        if (S.ReqDelivered < S.ReqAmount) return;
        S.ReqActive = false;
        S.ReqMet++;
        S.ReqScale = Math.Min(TownRules.RequestScaleMax, S.ReqScale * TownRules.RequestGrowMet);
        S.ReqNextAt = Time + TownRules.RequestGap;
        double coin = S.ReqRewardCoin, renown = RenownFor(coin);
        S.Coin += coin;
        S.RequestCoin += coin;
        S.Renown += renown;
        Emit(new TownEvent { Kind = TownEventKind.RequestMet, Good = S.ReqGood, Amount = S.ReqAmount, Coin = coin, Value = renown, Index = S.ReqWho });
        CheckLevels();
    }

    public double RequestCoin => S.RequestCoin;

    // ---- Hints ----

    /// <summary>Road the hint is about (-1 none), for the game to highlight. Read after <see cref="Hint"/>.</summary>
    public int HintRoad { get; private set; } = -1;
    /// <summary>Plot the hint is about (-1 none). Read after <see cref="Hint"/>.</summary>
    public int HintPlot { get; private set; } = -1;

    // Green pass (the game's wish): the hint builds no string per read; texts with a name in them are built once and kept.
    private static readonly string[] LoadHints = BuildLoadHints();
    private static string[] BuildLoadHints()
    {
        var a = new string[TownRules.LoadNames.Length];
        for (int i = 0; i < a.Length; i++) a[i] = "The " + TownRules.LoadNames[i] + " is ready. Every trip carries one more.";
        return a;
    }
    private readonly Dictionary<string, string>[] _handHints = { new(), new(), new() };
    private string HandHint(PlotKind kind, string hand)
    {
        int k = kind == PlotKind.Well ? 0 : kind == PlotKind.Field ? 1 : 2;
        if (!_handHints[k].TryGetValue(hand, out var s))
        {
            s = "A queue at the " + (k == 0 ? "well" : k == 1 ? "field" : "mill") + ". " + hand + " would help.";
            _handHints[k][hand] = s;
        }
        return s;
    }

    /// <summary>One line of advice from the state of the town, or "" when there is nothing to say. Once a frame is fine.</summary>
    public string Hint
    {
        get
        {
            HintRoad = -1; HintPlot = -1;
            // A Load step that has just unlocked (shown for a minute and a half).
            if (S.LoadLevel < TownRules.LoadPrices.Length && LoadLockReason.Length == 0 && S.LoadUnlockedAt[S.LoadLevel] >= 0 && Time - S.LoadUnlockedAt[S.LoadLevel] < 90)
                return LoadHints[S.LoadLevel];
            // Housing full and a cottage could be extended.
            if (HousingRoom <= 0)
            {
                int c = SuggestedExtend();
                if (c >= 0) { HintPlot = c; return "Every cottage is full. Extend one for another villager."; }
            }
            // A queue a building or an upgrade would fix.
            for (int i = 0; i < S.Workplaces.Length; i++)
            {
                var w = S.Workplaces[i];
                if (w.QueueLen < 2) continue;
                var kind = Plan.Plots[w.Plot].Kind;
                HintPlot = w.Plot;
                if (w.HandOffered) return HandHint(kind, HandName(i)); // rules v3; green pass: built once per text, not per read
                int bucket = OfferOf(OfferKind.SecondBucket), field2 = OfferOf(OfferKind.SecondField);
                if (kind == PlotKind.Well && bucket >= 0 && S.OfferAvailable[bucket] && !S.OfferBought[bucket]) return "A queue at the well. A second bucket would help.";
                if (kind == PlotKind.Field && field2 >= 0 && S.OfferAvailable[field2] && !S.OfferBought[field2]) return "A queue at the field. A second field would help.";
                if (ToolsLockReason.Length == 0)
                    return kind == PlotKind.Well ? "A queue at the well. Better tools would help." : kind == PlotKind.Field ? "A queue at the field. Better tools would help." : "A queue at the mill. Better tools would help.";
                HintPlot = -1;
            }
            // A crowded road that could be paved.
            for (int r = 0; r < Plan.Roads.Length; r++)
            {
                if (!S.RoadOpen[r] || S.RoadWalkers[r] <= Comfort(r) || !CanPave(r, out _)) continue;
                HintRoad = r;
                return S.RoadSurface[r] == Surface.Track ? "A road is crowded. Pave it so more can walk it." : "A road is crowded. Cobbles would carry more.";
            }
            return "";
        }
    }
}

/// <summary>Rules v2: the current request, for the request card.</summary>
public struct RequestView
{
    public bool Active;
    public int Who;               // index into Town.RequestWho
    public string WhoName;
    public Good Good;
    public int Amount, Delivered;
    public double SecondsLeft, Seconds;
    public double RewardCoin, RewardRenown;
    public double NextIn;         // seconds until the next request when none runs (-1 unknown or before requests start)
}
