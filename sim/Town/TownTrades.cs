using System;
using System.Collections.Generic;

namespace TideToll.Sim.Town;

/// <summary>A villager's trade (n7 step 2; Wool, Cloth and Boat: step 3, the second age's). Appended only: saves read these
/// numbers. None is a Helper: no trade, it weighs every job alike. Water: carriers, Grain: reapers, Flour: millers,
/// Wool: shepherds, Cloth: weavers, Boat: porters (grain and flour to the landing).</summary>
public enum Trade : byte { None = 0, Water = 1, Grain = 2, Flour = 3, Wool = 4, Cloth = 5, Boat = 6 }

/// <summary>One trade as the Trades card shows it (<see cref="Town.TradeInfo"/>).</summary>
public struct TradeView
{
    public bool Open;          // its workplace exists (Water from the start, Grain with the first field, Flour with the mill;
                               // Wool with the pasture, Cloth with the weaver's cottage, Boat with the landing)
    public int Members;        // villagers in it (for Trade.None: the Helpers)
    public int Places;         // work places of its workplaces (well, fields, mill, pasture, weaver); 0 for the Helpers and
                               // the porters (the landing has no work places)
    public int Away;           // members on a job of another trade now
    public int Waiting;        // members queued for a place, or with nothing to do, now
    public bool ByPlayer;      // the player set this count: the town leaves it alone
    public double WorkBonus;   // its members work this much faster at it (0.2 = +20 %), Training included
    public double WorthBonus;  // its goods fetch this much more when its members deliver them, Training included
}

/// <summary>
/// Trades (n7 step 2, docs/plans/n7/trades-build-sheet.md part 1): the data, the views and the player's calls. The rules
/// (the lean, the reward, the town's filling and rebalancing) are in TownTradeRules.cs.
/// </summary>
public sealed partial class Town
{
    /// <summary>Number of trades including None (the Helpers' index 0).</summary>
    public const int TradeCount = 7;

    /// <summary>The first age's trades (Water, Grain, Flour) are 1 to FirstAgeTrades - 1; the second age's follow.</summary>
    public const int FirstAgeTrades = 4;

    /// <summary>The trade a job belongs to (None for no job). Grain and flour to the boats are the porters' (step 3): the
    /// reapers and millers keep the home and market work.</summary>
    public static Trade TradeOfJob(JobKind j) => j switch
    {
        JobKind.Water => Trade.Water,
        JobKind.GrainHome or JobKind.GrainMarket => Trade.Grain,
        JobKind.Flour => Trade.Flour,
        JobKind.WoolBoat or JobKind.WoolMarket => Trade.Wool,
        JobKind.ClothBoat or JobKind.ClothMarket => Trade.Cloth,
        JobKind.GrainBoat => TownRules.TradePorters ? Trade.Boat : Trade.Grain,
        JobKind.FlourBoat => TownRules.TradePorters ? Trade.Boat : Trade.Flour,
        _ => Trade.None,
    };

    /// <summary>Whether job <paramref name="j"/> is at trade <paramref name="t"/>'s own work: the job's trade, and with
    /// TownRules.BoatTripsOwnedTwice also the trade of the good a boat trip carries (a reaper's grain to a boat, a
    /// miller's flour). The lean, the reward and the own-trade count all read this.</summary>
    public static bool AtOwnTrade(Trade t, JobKind j)
    {
        if (t == Trade.None) return false;
        if (TradeOfJob(j) == t) return true;
        return TownRules.BoatTripsOwnedTwice && (j == JobKind.GrainBoat ? t == Trade.Grain : j == JobKind.FlourBoat && t == Trade.Flour);
    }

    /// <summary>The trade that matches a leaning (the porters match none: nobody leans to them).</summary>
    public static Trade TradeOfLeaning(Leaning l) => l switch
    {
        Leaning.Water => Trade.Water, Leaning.Grain => Trade.Grain, Leaning.Flour => Trade.Flour,
        Leaning.Wool => Trade.Wool, Leaning.Cloth => Trade.Cloth, _ => Trade.None,
    };

    private static PlotKind TradePlot(Trade t) => t == Trade.Water ? PlotKind.Well : t == Trade.Grain ? PlotKind.Field : PlotKind.Mill;

    /// <summary>The second age's place where a trade's work is done (the pasture, the weaver's cottage, the landing).</summary>
    private static RiverPlotKind TradeRiverPlot(Trade t) => t == Trade.Wool ? RiverPlotKind.Pasture : t == Trade.Cloth ? RiverPlotKind.Weaver : RiverPlotKind.Landing;

    /// <summary>Whether a trade's workplace exists: Water from the start, Grain with the first field, Flour with the mill;
    /// in the second age Wool with the pasture, Cloth with the weaver's cottage, Boat with the landing (each given).</summary>
    public bool TradeOpen(Trade t)
    {
        if (t == Trade.None || (int)t >= TradeCount) return false;
        if (t == Trade.Boat && !TownRules.TradePorters) return false; // V1: no porters (the name stays, the trade never opens)
        if ((int)t >= FirstAgeTrades) return S.River != null && RiverPlotWorks(TradeRiverPlot(t));
        var pk = TradePlot(t);
        foreach (var w in S.Workplaces) if (w.Open && Plan.Plots[w.Plot].Kind == pk) return true;
        return false;
    }

    /// <summary>Work places of a trade's open workplaces: the well, the fields, the mill, the pasture, the weaver's cottage;
    /// 0 for the porters (the landing has berths, not work places) and for a trade not open.</summary>
    internal int TradePlaces(Trade t)
    {
        if (!TradeOpen(t)) return 0;
        if (t == Trade.Wool) return TownRulesRiver.PasturePlaces;
        if (t == Trade.Cloth) return TownRulesRiver.WeaverPlaces;
        if (t == Trade.Boat) return 0;
        int p = 0; var pk = TradePlot(t);
        foreach (var w in S.Workplaces) if (w.Open && Plan.Plots[w.Plot].Kind == pk) p += w.Places;
        return p;
    }

    /// <summary>True from the first field on: the Trades card and the Training row show.</summary>
    public bool TradesShown => TradeOpen(Trade.Grain);

    /// <summary>Places of a trade's open workplaces busy now (working), and all of them: the well, the fields, the mill,
    /// the pasture, the weaver's cottage (0 and 0 for the porters and the Helpers). For the tools and tests.</summary>
    public int TradeBusy(Trade t, out int places)
    {
        places = TradePlaces(t);
        if (places == 0) return 0;
        if (t == Trade.Wool) return R!.PastureBusy;
        if (t == Trade.Cloth) return R!.WeaverBusy;
        int busy = 0; var pk = TradePlot(t);
        foreach (var w in S.Workplaces) if (w.Open && Plan.Plots[w.Plot].Kind == pk) busy += w.Busy;
        return busy;
    }

    /// <summary>Villagers with no trade.</summary>
    public int Helpers => TradeMembers(Trade.None);

    /// <summary>Whether the town fills and rebalances the trades (the player's switch "Town fills trades").</summary>
    public bool TownFillsTrades => S.TownFillsTrades;

    /// <summary>The trade villager <paramref name="i"/> likes: its leaning's trade, None while that trade is not open.</summary>
    public Trade VillagerLikes(int i)
    {
        var t = TradeOfLeaning(LeaningOf(S.Seed, i));
        return TradeOpen(t) ? t : Trade.None;
    }

    internal int TradeMembers(Trade t)
    {
        int n = 0;
        foreach (var v in S.Villagers) if (v.Trade == t) n++;
        return n;
    }

    /// <summary>A trade's bonus on work speed and on its goods' worth (fractions), without the like.</summary>
    public double TradeWorkBonus => TownRules.TradeWork + S.Training * TownRules.TradeStep;
    public double TradeWorthBonus => TownRules.TradeWorth + S.Training * TownRules.TradeStep;

    /// <summary>One trade as the Trades card shows it; Trade.None gives the Helpers.</summary>
    public TradeView TradeInfo(Trade t)
    {
        var tv = new TradeView { Open = t == Trade.None || TradeOpen(t), ByPlayer = (int)t < TradeCount && S.TradeCountByPlayer[(int)t] };
        if (t != Trade.None) { tv.WorkBonus = TradeWorkBonus; tv.WorthBonus = TradeWorthBonus; }
        if (t != Trade.None && (int)t < TradeCount) tv.Places = TradePlaces(t);
        foreach (var v in S.Villagers)
        {
            if (v.Trade != t) continue;
            tv.Members++;
            if (v.Job == JobKind.None || v.Phase == VillagerPhase.Queued) tv.Waiting++;
            else if (t != Trade.None && !AtOwnTrade(t, v.Job)) tv.Away++;
        }
        return tv;
    }

    // ---- The player's calls ----

    /// <summary>The game says whose card is open (-1: none); the town never changes that villager's trade.</summary>
    public void SetOpenCard(int id) => _openCard = id;
    private int _openCard = -1;

    /// <summary>Sets the count of a trade (Trade.None: the Helpers); the count is the player's from now on and the town
    /// never moves anyone into or out of it. Up: Helpers first (the one who likes the trade first, then the lowest number),
    /// then a member of the largest town-decided trade. Down: members become Helpers (one set on its card last, then the
    /// one who likes the trade least, then the highest number). False if nothing changed.</summary>
    public bool SetTradeCount(Trade t, int n)
    {
        if ((int)t >= TradeCount || (t != Trade.None && !TradeOpen(t))) return false;
        n = Math.Clamp(n, 0, S.Villagers.Length);
        bool changed = !S.TradeCountByPlayer[(int)t];
        S.TradeCountByPlayer[(int)t] = true;
        int have = TradeMembers(t);
        while (have < n)
        {
            var v = PickIntoTrade(t);
            if (v == null) break;
            ChangeTrade(v, t, true, false);
            have++; changed = true;
        }
        while (have > n)
        {
            var v = PickOutOfTrade(t);
            if (v == null) break;
            ChangeTrade(v, Trade.None, true, false);
            if (S.TradeCountByPlayer[0] == false) S.TradeCountByPlayer[0] = true; // the Helpers made here stay Helpers
            have--; changed = true;
        }
        S.TradeWant[(int)t] = have;
        if (t != Trade.None) S.TradeWant[0] = TradeMembers(Trade.None);
        return changed;
    }

    /// <summary>Sets one villager's trade; that villager is the player's from now on (the town never moves it).</summary>
    public bool SetVillagerTrade(int id, Trade t)
    {
        if (id < 0 || id >= S.Villagers.Length || (int)t >= TradeCount || (t != Trade.None && !TradeOpen(t))) return false;
        var v = S.Villagers[id];
        if (v.Trade == t && v.TradeByPlayer) return false;
        if (v.Trade != t) ChangeTrade(v, t, true, true);
        v.TradeByPlayer = true;
        return true;
    }

    /// <summary>Whether SetTradeCount(t, members + 1) would move anyone now (by the same rule: a Helper not set on its card,
    /// else a member of the largest town-decided trade that keeps one; never the open card's). No allocation.</summary>
    public bool CanRaiseTrade(Trade t)
    {
        if ((int)t >= TradeCount || (t != Trade.None && !TradeOpen(t))) return false;
        return TradeMembers(t) < S.Villagers.Length && PickIntoTrade(t) != null;
    }

    /// <summary>Whether SetTradeCount(t, members - 1) would move anyone now (a member that is not the open card's). No allocation.</summary>
    public bool CanLowerTrade(Trade t)
    {
        if ((int)t >= TradeCount || (t != Trade.None && !TradeOpen(t))) return false;
        return PickOutOfTrade(t) != null;
    }

    /// <summary>Clears villager <paramref name="id"/>'s "set by the player" mark: the town may move it again. Emits nothing
    /// (a TradeChanged comes only if the town then moves it). False if it was not set.</summary>
    public bool GiveVillagerBackToTown(int id)
    {
        if (id < 0 || id >= S.Villagers.Length || !S.Villagers[id].TradeByPlayer) return false;
        S.Villagers[id].TradeByPlayer = false;
        return true;
    }

    /// <summary>On: every trade's count is the town's again (villagers set one by one stay set). Off: the town leaves the
    /// trades as they are. False if nothing changed.</summary>
    public bool SetTownFillsTrades(bool on)
    {
        bool changed = S.TownFillsTrades != on;
        S.TownFillsTrades = on;
        if (on)
            for (int i = 0; i < TradeCount; i++) { changed |= S.TradeCountByPlayer[i]; S.TradeCountByPlayer[i] = false; }
        return changed;
    }

    /// <summary>Who joins trade <paramref name="t"/> when its count goes up: a Helper not set on its card (who likes it first,
    /// then the lowest number), else a member of the largest town-decided trade (who likes t first, then the lowest number)
    /// that keeps one member; never a villager set on its card, never the open card's.</summary>
    private VillagerState? PickIntoTrade(Trade t)
    {
        VillagerState? best = null;
        if (t != Trade.None)
            foreach (var v in S.Villagers)
            {
                if (v.Trade != Trade.None || v.TradeByPlayer || v.Id == _openCard) continue;
                if (best == null || (VillagerLikes(v.Id) == t && VillagerLikes(best.Id) != t)) best = v;
            }
        if (best != null) return best;
        int from = -1, fromN = 1;
        for (int k = 1; k < TradeCount; k++)
        {
            if (k == (int)t || S.TradeCountByPlayer[k]) continue;
            int m = TradeMembers((Trade)k);
            if (m > fromN) { fromN = m; from = k; }
        }
        if (from < 0) return null;
        foreach (var v in S.Villagers)
        {
            if ((int)v.Trade != from || v.TradeByPlayer || v.Id == _openCard) continue;
            if (best == null || (VillagerLikes(v.Id) == t && VillagerLikes(best.Id) != t)) best = v;
        }
        return best;
    }

    /// <summary>Who leaves trade <paramref name="t"/> when its count goes down: one not set on its card while another is
    /// left, then the one who likes it least, then the highest number.</summary>
    private VillagerState? PickOutOfTrade(Trade t)
    {
        VillagerState? best = null;
        int bestKey = int.MaxValue;
        foreach (var v in S.Villagers)
        {
            if (v.Trade != t || v.Id == _openCard) continue;
            int key = (v.TradeByPlayer ? 2 : 0) + (VillagerLikes(v.Id) == t ? 1 : 0);
            if (key <= bestKey) { bestKey = key; best = v; } // later (higher number) wins a tie
        }
        return best;
    }

    /// <summary>Moves a villager to a trade and says so (TradeChanged: Index = new trade, Amount = old, Level 1 by the player).</summary>
    internal void ChangeTrade(VillagerState v, Trade t, bool byPlayer, bool card)
    {
        var old = v.Trade;
        v.Trade = t;
        if (!card && !byPlayer) v.TradeByPlayer = false;
        S.TradeChanges++;
        Emit(new TownEvent { Kind = TownEventKind.TradeChanged, Villager = v.Id, Index = (int)t, Amount = (int)old, Level = byPlayer ? 1 : 0, X = v.X, Y = v.Y });
    }

    // ---- Training: one shop row, ten steps ----

    /// <summary>Training steps bought (0 to TownRules.TrainingMax).</summary>
    public int Training => S.Training;

    /// <summary>Price of the next Training step: TrainingMinutes of the town's best coin a minute (at least TrainingRateMin),
    /// times TrainingGrowth ^ steps bought.</summary>
    public double TrainingPrice => Math.Ceiling(TownRules.TrainingMinutes * Math.Max(TownRules.TrainingRateMin, S.MaxCoinPerMinute) * Math.Pow(TownRules.TrainingGrowth, S.Training));

    /// <summary>What one more Training step adds to both bonuses (TradeWorkBonus and TradeWorthBonus), as a fraction
    /// (0.04 = +4 %); 0 when all ten steps are bought. For the row's tooltip.</summary>
    public double TrainingStepBonus => S.Training >= TownRules.TrainingMax ? 0 : TownRules.TradeStep;

    /// <summary>"" when a Training step can be bought now (ignoring coin), else why not.</summary>
    public string TrainingLockReason
    {
        get
        {
            if (!TradesShown) return "Comes with the first field";
            if (S.Training >= TownRules.TrainingMax) return "All training done";
            foreach (var v in S.Villagers) if (v.Trade != Trade.None) return "";
            return "Comes with the first trade";
        }
    }

    private void AddTrainingItem(List<ShopItem> into)
    {
        if (!TradesShown) return;
        bool done = S.Training >= TownRules.TrainingMax;
        into.Add(Item(ShopItemKind.Training, -1, "Training", S.Training, done ? 0 : TrainingPrice, TrainingLockReason));
    }

    public bool BuyTraining()
    {
        if (TrainingLockReason.Length > 0) return false;
        double p = TrainingPrice;
        if (!Pay(p)) return false;
        S.Training++;
        Bought(ShopItemKind.Training, -1, p, S.Training);
        return true;
    }

    /// <summary>The Training row's worth (coin a minute): one step speeds the members' work at their trade and raises what
    /// their trade's goods fetch, on the share of income the members make at their own trade.</summary>
    private double TrainingWorth(double rate)
    {
        if (TrainingLockReason.Length > 0) return 0;
        double atTrade = 0;
        for (int t = 1; t < TradeCount; t++) atTrade += S.TradeEarn[t];
        double share = Math.Min(1, atTrade / Math.Max(1, CoinPerMinute)); // the share of income made at the members' own trades
        double step = TownRules.TradeStep;
        // Worth: what its goods fetch rises by step / (1 + worth bonus); work: about TrainingWorkShare of a trip is work.
        return rate * share * (step / (1 + TradeWorthBonus) + TownRules.TrainingWorkShare * step / (1 + TradeWorkBonus));
    }
}
