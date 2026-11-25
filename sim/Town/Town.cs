using System;
using System.Collections.Generic;

namespace TideToll.Sim.Town;

/// <summary>
/// The first-age town: plan, villagers on the road graph, coin, the shop, renown and levels.
/// Call <see cref="Step"/> TownRules.TickHz times per game second; read the views; drain events; call the actions.
/// Deterministic: the same seed and the same actions at the same ticks give the same town.
/// </summary>
public sealed partial class Town
{
    public TownPlan Plan { get; }
    internal TownState S;
    private readonly List<TownEvent> _events = new(64);
    private readonly int[] _plotWorkplace;
    private int _marketPlot = -1;

    public Town(int seed) : this(TownPlanner.Build(seed)) { }

    public Town(TownPlan plan) : this(plan, null) { }

    internal Town(TownPlan plan, TownState? loaded)
    {
        Plan = plan;
        int nr = plan.Roads.Length, nn = plan.Nodes.Length;
        _plotWorkplace = new int[plan.Plots.Length];
        _dist = new double[nn]; _prevRoad = new int[nn]; _done = new bool[nn];
        _pair = new double[nn * nn];
        var wps = new List<WorkplaceState>();
        foreach (var p in plan.Plots)
        {
            _plotWorkplace[p.Id] = -1;
            if (p.Kind == PlotKind.Market) _marketPlot = p.Id;
            if (p.Kind is PlotKind.Well or PlotKind.Field or PlotKind.Mill)
            {
                _plotWorkplace[p.Id] = wps.Count;
                int places = p.Kind == PlotKind.Well ? TownRules.WellPlaces : p.Kind == PlotKind.Field ? TownRules.FieldPlaces : TownRules.MillPlaces;
                wps.Add(new WorkplaceState { Plot = p.Id, Places = places });
            }
        }
        if (loaded != null) { S = loaded; return; }
        S = new TownState
        {
            Seed = plan.Seed,
            RoadOpen = new bool[nr], RoadWear = new double[nr], RoadSurface = new Surface[nr], RoadSteps = new double[nr], RoadWalkers = new int[nr],
            PlotGiven = new bool[plan.Plots.Length],
            OfferAvailable = new bool[plan.Offers.Length], OfferBought = new bool[plan.Offers.Length],
            Workplaces = wps.ToArray(),
            Achieved = new bool[TownAchievements.All.Length],
        };
        ApplyLevel(1);
        AddVillager();
    }

    /// <summary>Game time in seconds.</summary>
    public double Time => S.Tick * TownRules.Dt;
    public long Tick => S.Tick;
    public int Seed => Plan.Seed;

    /// <summary>One fixed step of 1 / TownRules.TickHz game seconds.</summary>
    public void Step()
    {
        S.Tick++;
        _pairTick = -1;
        double t = Time;
        // Rolling coin per minute: a ring of per-second earnings.
        long sec = (long)Math.Floor(t);
        while (S.RateSecond < sec)
        {
            S.RateSecond++;
            S.RateRing[(int)(S.RateSecond % TownRules.RateWindowSeconds)] = 0;
        }
        var vs = S.Villagers;
        for (int i = 0; i < vs.Length; i++) StepVillager(vs[i]);
        double cpm = CoinPerMinute;
        if (cpm > S.MaxCoinPerMinute) S.MaxCoinPerMinute = cpm;
        if (S.Tick % TownRules.TickHz == 0) CheckAchievements();
    }

    /// <summary>Copies the events since the last drain into <paramref name="into"/> (cleared first) and forgets them.</summary>
    public void DrainEvents(List<TownEvent> into)
    {
        into.Clear();
        into.AddRange(_events);
        _events.Clear();
    }

    /// <summary>Events not drained yet (read-only; cleared by DrainEvents).</summary>
    public IReadOnlyList<TownEvent> PendingEvents => _events;

    internal void Emit(TownEvent e)
    {
        e.Time = Time;
        _events.Add(e);
    }

    // ---- Levels ----

    private void ApplyLevel(int n)
    {
        var lv = Plan.Level(n);
        S.Level = n;
        foreach (int p in lv.Plots) S.PlotGiven[p] = true;
        foreach (int r in lv.Roads) { S.RoadOpen[r] = true; S.RoadWear[r] = 0; }
        foreach (int o in lv.Offers)
        {
            S.OfferAvailable[o] = true;
            Emit(new TownEvent { Kind = TownEventKind.OfferAvailable, Item = ShopItemKind.Building, Index = o, Text = Plan.Offers[o].Name });
        }
        if (lv.UnlocksBell) { S.BellUnlocked = true; Emit(new TownEvent { Kind = TownEventKind.OfferAvailable, Item = ShopItemKind.Bell, Index = -1, Text = "The bell" }); }
        if (lv.UnlocksCobbles) { S.CobblesUnlocked = true; Emit(new TownEvent { Kind = TownEventKind.OfferAvailable, Item = ShopItemKind.Pave, Index = -1, Text = "Cobbles" }); }
        RefreshWorkplaces();
        Emit(new TownEvent { Kind = TownEventKind.LevelReached, Level = n, Text = lv.Gift });
        if (lv.Charter && !S.Charter)
        {
            S.Charter = true;
            Emit(new TownEvent { Kind = TownEventKind.Charter, Level = n, Text = "The town charter" });
        }
    }

    private void CheckLevels()
    {
        while (S.Level < Plan.MaxLevel && S.Renown >= Plan.Level(S.Level + 1).Renown) ApplyLevel(S.Level + 1);
    }

    internal bool PlotWorks(int plot)
    {
        if (!S.PlotGiven[plot]) return false;
        int o = Plan.Plots[plot].Offer;
        return o < 0 || S.OfferBought[o];
    }

    private void RefreshWorkplaces()
    {
        foreach (var w in S.Workplaces)
        {
            w.Open = PlotWorks(w.Plot);
            if (Plan.Plots[w.Plot].Kind == PlotKind.Well) w.Places = TownRules.WellPlaces + (S.OfferBought[0] ? TownRules.SecondBucketPlaces : 0);
        }
    }

    // ---- Villagers ----

    public int Housing
    {
        get
        {
            int c = 0;
            foreach (var p in Plan.Plots) if (p.Kind == PlotKind.Cottage && S.PlotGiven[p.Id]) c++;
            return c * TownRules.PerCottage;
        }
    }

    private void AddVillager()
    {
        // The given cottage with fewest residents (lowest id on a tie).
        int best = -1, bestN = int.MaxValue;
        foreach (var p in Plan.Plots)
        {
            if (p.Kind != PlotKind.Cottage || !S.PlotGiven[p.Id]) continue;
            int k = 0;
            foreach (var v0 in S.Villagers) if (v0.Home == p.Id) k++;
            if (k < bestN) { bestN = k; best = p.Id; }
        }
        var home = Plan.Plots[best];
        var v = new VillagerState { Id = S.Villagers.Length, Home = best, Node = home.Node };
        var nd = Plan.Nodes[home.Node];
        v.X = v.PrevX = nd.X + 0.5; v.Y = v.PrevY = nd.Y + 0.5;
        var arr = new VillagerState[S.Villagers.Length + 1];
        Array.Copy(S.Villagers, arr, S.Villagers.Length);
        arr[^1] = v;
        S.Villagers = arr;
        Emit(new TownEvent { Kind = TownEventKind.VillagerArrived, Villager = v.Id, Plot = best, X = v.X, Y = v.Y });
    }

    // ---- Coin ----

    internal double CoinBonus => 1 + TownRules.VillagerPerkCoin * (S.VillagersBought / TownRules.PerkEvery);
    internal int LoadUnits => 1 + S.LoadLevel;

    internal void Earn(double coin)
    {
        if (S.FirstCoinTime < 0) S.FirstCoinTime = Time;
        S.Coin += coin;
        S.CoinEarned += coin;
        S.RateRing[(int)(S.RateSecond % TownRules.RateWindowSeconds)] += coin;
    }

    /// <summary>Coin earned over the last minute of game time (rolling; less than a minute of data counts what there is).</summary>
    public double CoinPerMinute
    {
        get
        {
            double s = 0;
            var r = S.RateRing;
            for (int i = 0; i < r.Length; i++) s += r[i];
            return s;
        }
    }
}
