using System;

namespace TideToll.Sim.Town;

/// <summary>
/// Trades (n7 step 2), the rules: the reward at a villager's own trade, the earnings each trade's members make, and the
/// town's filling and rebalancing (docs/plans/n7/trades-build-sheet.md part 1, items 2 to 4). The lean is in
/// TownPeople.cs (Leaned). Runs once a game second from Town.Step; allocates nothing; walks villagers by index.
/// </summary>
public sealed partial class Town
{
    /// <summary>What a trade's earnings keep from one game second to the next: about the last minute counts.</summary>
    private static readonly double TradeEarnKeep = Math.Exp(-1.0 / TownRules.RateWindowSeconds);

    /// <summary>Whether villager <paramref name="v"/> likes its own trade (its leaning's trade).</summary>
    private bool LikesOwnTrade(VillagerState v) => v.Trade != Trade.None && TradeOfLeaning(LeaningOf(S.Seed, v.Id)) == v.Trade;

    /// <summary>Work speed of a villager on its current job: 1 + TradeWork + Training x TradeStep (+ TradeLikes in the trade
    /// it likes) when the job is its trade's, else 1.</summary>
    internal double TradeWorkFactor(VillagerState v)
    {
        if (v.Trade == Trade.None || TradeOfJob(v.Job) != v.Trade) return 1;
        return 1 + TradeWorkBonus + (LikesOwnTrade(v) ? TownRules.TradeLikes : 0);
    }

    /// <summary>What a delivery of the villager's current job is worth against a Helper's: 1 + TradeWorth + Training x
    /// TradeStep (+ TradeLikes) when the job is its trade's, else 1.</summary>
    internal double TradeWorthFactor(VillagerState v)
    {
        if (v.Trade == Trade.None || TradeOfJob(v.Job) != v.Trade) return 1;
        return 1 + TradeWorthBonus + (LikesOwnTrade(v) ? TownRules.TradeLikes : 0);
    }

    /// <summary>A delivery's coin counts to the trade whose work it was, whoever made it (the smoothed figure the town fills
    /// and rebalances by): when others do a trade's work, that trade has too few members; when its members go elsewhere,
    /// it has too many.</summary>
    private void TradeEarned(VillagerState v, double coin) => S.TradeEarn[(int)TradeOfJob(v.Job)] += coin;

    /// <summary>Coin a minute a head trade <paramref name="t"/> made lately: the coin of the deliveries of its work, whoever
    /// made them, over the member-minutes the trade had (both decaying over about a minute). Until a trade has half a
    /// minute of members behind it, it counts as the best paid (so a new trade is tried).</summary>
    public double TradeEarnPerHead(Trade t) => RateOf((int)t);

    private double RateOf(int t) => S.TradeAt[t] < TradeTryMinimum ? double.MaxValue : S.TradeEarn[t] / S.TradeAt[t] * 60;

    /// <summary>Member-seconds (decayed) a trade needs behind it before its figure is believed.</summary>
    private const double TradeTryMinimum = 30;

    private bool TownTrade(int t) => t > 0 && !S.TradeCountByPlayer[t] && TradeOpen((Trade)t);

    /// <summary>Once a game second: the earnings decay; while the town fills trades, Helpers take a trade and at most one
    /// villager every TownRules.TradeMoveEvery seconds moves: first into a trade with fewer members than its workplaces have
    /// places (its floor), else from the worst-paid trade to the best when the gap is over TownRules.TradeMoveGap.</summary>
    private void StepTrades()
    {
        for (int t = 0; t < TradeCount; t++) { S.TradeEarn[t] *= TradeEarnKeep; S.TradeAt[t] *= TradeEarnKeep; }
        foreach (var v in S.Villagers) if (v.Trade != Trade.None) S.TradeAt[(int)v.Trade] += 1;
        if (!S.TownFillsTrades || S.Villagers.Length == 0) return;
        Span<int> members = stackalloc int[TradeCount], before = stackalloc int[TradeCount], floor = stackalloc int[TradeCount];
        CountMembers(members);
        members.CopyTo(before);
        int keepHelpers = S.TradeCountByPlayer[0] ? S.TradeWant[0] : 0;
        Floors(members, keepHelpers, floor);
        // 1. Helpers above the player's count of Helpers take a trade: the town trade furthest under its floor first, else
        //    the one that pays most a head; the Helper who likes it first, then the lowest number.
        while (members[0] > keepHelpers)
        {
            int to = FillTarget(members, before, floor);
            if (to < 0) break;
            var v = PickHelper((Trade)to);
            if (v == null) break;
            Move(v, to, members);
        }
        // 2. An open town trade with nobody in it takes one at once from the town trade with most members above its floor.
        for (int t = 1; t < TradeCount; t++)
        {
            if (!TownTrade(t) || members[t] > 0) continue;
            int from = Donor(members, floor, t);
            var v = from < 0 ? null : PickMover(from, (Trade)t);
            if (v != null) { Move(v, t, members); S.TradeMoveAt = Time; }
        }
        // 3. At most one move every TradeMoveEvery seconds.
        if (S.TradeMoveAt >= 0 && Time - S.TradeMoveAt < TownRules.TradeMoveEvery - 1e-9) return;
        //    a. into the town trade furthest under its floor, from the one with most members above its floor;
        int under = -1;
        for (int t = 1; t < TradeCount; t++)
            if (TownTrade(t) && members[t] < floor[t] && (under < 0 || floor[t] - members[t] > floor[under] - members[under])) under = t;
        if (under > 0)
        {
            int from = Donor(members, floor, under);
            var v = from < 0 ? null : PickMover(from, (Trade)under);
            if (v != null) { Move(v, under, members); S.TradeMoveAt = Time; return; }
        }
        //    b. else from the trade that pays least a head (above its floor) to the one that pays most, past the gap.
        int hi = -1, lo = -1; double hiE = 0, loE = 0;
        for (int t = 1; t < TradeCount; t++)
        {
            if (!TownTrade(t) || members[t] == 0) continue;
            double e = Figure(t);
            if (hi < 0 || e > hiE) { hi = t; hiE = e; }
            if (members[t] > Math.Max(1, floor[t]) && (lo < 0 || e < loE)) { lo = t; loE = e; }
        }
        if (hi < 0 || lo < 0 || hi == lo || hiE <= loE * (1 + TownRules.TradeMoveGap)) return;
        var mv = PickMover(lo, (Trade)hi);
        if (mv == null) return;
        Move(mv, hi, members);
        S.TradeMoveAt = Time;
    }

    private void Move(VillagerState v, int to, Span<int> members)
    {
        members[(int)v.Trade]--; members[to]++;
        ChangeTrade(v, (Trade)to, false, false);
        S.TradeChangesByTown++;
    }

    /// <summary>Each open town trade's floor: the places of its workplaces, scaled down when the villagers the town places
    /// are fewer than all those places (a small town spreads what it has).</summary>
    private void Floors(Span<int> members, int keepHelpers, Span<int> floor)
    {
        floor.Clear();
        if (TownRules.TradeFloorShare <= 0) return;
        int placed = members[0] - keepHelpers, all = 0;
        for (int t = 1; t < TradeCount; t++)
        {
            if (!TownTrade(t)) continue;
            placed += members[t];
            floor[t] = t == (int)Trade.Boat ? PortersFloor() : (int)Math.Floor(TradePlaces((Trade)t) * TownRules.TradeFloorShare);
            all += floor[t];
        }
        if (all > placed && all > 0)
            for (int t = 1; t < TradeCount; t++) floor[t] = floor[t] * placed / all;
    }

    /// <summary>The porters' floor (step 3): the landing has no work places, so TownRules.TradePortersPerBerth villagers a
    /// berth.</summary>
    private int PortersFloor() => (int)Math.Floor(Berths * TownRules.TradePortersPerBerth);

    /// <summary>The town trade (not <paramref name="not"/>) with most members above its floor and at least two; -1 if none.</summary>
    private int Donor(Span<int> members, Span<int> floor, int not)
    {
        int from = -1;
        for (int k = 1; k < TradeCount; k++)
            if (k != not && TownTrade(k) && members[k] >= 2 && members[k] > floor[k] && (from < 0 || members[k] - floor[k] > members[from] - floor[from])) from = k;
        return from;
    }

    /// <summary>The figure the town fills and rebalances by (coin a minute a head).</summary>
    private double Figure(int t) => RateOf(t);

    private void CountMembers(Span<int> members)
    {
        members.Clear();
        foreach (var v in S.Villagers) members[(int)v.Trade]++;
    }

    /// <summary>The trade a Helper is given: an open town trade with no member first (lowest number), else the open town
    /// trade whose members earned most a head (lowest number on a tie), each figure thinned by the Helpers already given it
    /// this second (as if the trade's coin were shared by one more), so many newcomers at once spread; -1 when none is open.</summary>
    private int FillTarget(Span<int> members, Span<int> before, Span<int> floor)
    {
        int best = -1; double bestE = 0;
        for (int t = 1; t < TradeCount; t++)
            if (TownTrade(t) && members[t] < Math.Max(1, floor[t]) && (best < 0 || floor[t] - members[t] > floor[best] - members[best])) best = t;
        if (best > 0) return best;
        for (int t = 1; t < TradeCount; t++)
        {
            if (!TownTrade(t)) continue;
            double e = Figure(t);
            if (e < double.MaxValue) e *= (double)Math.Max(1, before[t]) / (members[t] + 1 - before[t] + Math.Max(1, before[t]));
            if (best < 0 || e > bestE) { best = t; bestE = e; }
        }
        return best;
    }

    /// <summary>A Helper the town may give trade <paramref name="t"/>: not set on its card, not the open card's; the one who
    /// likes t first, then the lowest number.</summary>
    private VillagerState? PickHelper(Trade t)
    {
        VillagerState? best = null;
        foreach (var v in S.Villagers)
        {
            if (v.Trade != Trade.None || v.TradeByPlayer || v.Id == _openCard) continue;
            if (best == null || (TradeOfLeaning(LeaningOf(S.Seed, v.Id)) == t && TradeOfLeaning(LeaningOf(S.Seed, best.Id)) != t)) best = v;
        }
        return best;
    }

    /// <summary>A member of trade <paramref name="from"/> the town may move to <paramref name="to"/>: never one set on its
    /// card, never the open card's; one who likes <paramref name="to"/> first, then one who does not like its own trade, then
    /// the lowest number.</summary>
    private VillagerState? PickMover(int from, Trade to)
    {
        VillagerState? best = null; int bestKey = int.MaxValue;
        foreach (var v in S.Villagers)
        {
            if ((int)v.Trade != from || v.TradeByPlayer || v.Id == _openCard) continue;
            var likes = TradeOfLeaning(LeaningOf(S.Seed, v.Id));
            int key = likes == to ? 0 : likes != v.Trade ? 1 : 2;
            if (key < bestKey) { bestKey = key; best = v; }
        }
        return best;
    }
}
