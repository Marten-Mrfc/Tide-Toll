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
        _spots = plan.TouchSpots ??= TownTouchPlanner.Build(plan); // built here so no step allocates it
        if (loaded != null) { S = loaded; if (S.River != null) _rp = TownRiverPlanner.Build(plan); return; }
        S = new TownState
        {
            Seed = plan.Seed,
            RoadOpen = new bool[nr], RoadWear = new double[nr], RoadSurface = new Surface[nr], RoadSteps = new double[nr], RoadWalkers = new int[nr],
            PlotGiven = new bool[plan.Plots.Length],
            OfferAvailable = new bool[plan.Offers.Length], OfferBought = new bool[plan.Offers.Length],
            Workplaces = wps.ToArray(),
            Achieved = new bool[TownAchievements.All.Length],
            Extensions = new int[plan.Plots.Length],
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
        StepRequests();
        StepHands();
        if (S.River != null) StepRiver();
        if (S.Tick % TownRules.TickHz == 0) { CheckAchievements(); StepTouches(); }
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
        for (int i = 0; i < TownRules.LoadLevels.Length; i++)
            if (TownRules.LoadLevels[i] == n)
            {
                S.LoadUnlockedAt[i] = Time;
                Emit(new TownEvent { Kind = TownEventKind.OfferAvailable, Item = ShopItemKind.Load, Index = i, Text = TownRules.LoadNames[i] });
            }
        if (n == TownRules.ToolsFromLevel) Emit(new TownEvent { Kind = TownEventKind.OfferAvailable, Item = ShopItemKind.Tools, Index = -1, Text = "Tools" });
        if (n == TownRules.ExtendFromLevel) Emit(new TownEvent { Kind = TownEventKind.OfferAvailable, Item = ShopItemKind.Extend, Index = -1, Text = "Extend a cottage" });
        if (n == TownRules.RequestFromLevel && S.ReqNextAt < 0) S.ReqNextAt = Time + TownRules.RequestFirstDelay;
        RefreshWorkplaces();
        Emit(new TownEvent { Kind = TownEventKind.LevelReached, Level = n, Text = lv.Gift });
        OfferChoicesAt(n);
        if (n == Plan.MaxLevel - 1 && S.CharterRenown <= 0)
            S.CharterRenown = Math.Max(Plan.Level(Plan.MaxLevel).Renown, Math.Round(S.Renown + TownRules.CharterMinutes * S.MaxCoinPerMinute));
        if (n == Plan.MaxLevel - 1 && !S.DriveActive && !S.DriveDone) StartDrive();
        if (lv.Charter && !S.Charter)
        {
            S.Charter = true;
            Emit(new TownEvent { Kind = TownEventKind.Charter, Level = n, Text = "The town charter" });
        }
    }

    /// <summary>Renown needed for first-age level n. Green pass: the charter (the last level) needs at least the table's renown,
    /// and at least TownRules.CharterMinutes of the town's best coin rate above the renown held at level MaxLevel - 1, fixed
    /// when that level is reached (TownState.CharterRenown), so a strong town does not race to the charter.</summary>
    public double LevelRenownOf(int n) => n == Plan.MaxLevel && S.CharterRenown > 0 ? S.CharterRenown : Plan.Level(n).Renown;

    internal void CheckLevels()
    {
        while (S.Level < Plan.MaxLevel && S.Renown >= LevelRenownOf(S.Level + 1)) ApplyLevel(S.Level + 1);
        if (S.River != null) CheckRiverLevels();
    }

    internal bool PlotWorks(int plot)
    {
        if (!S.PlotGiven[plot]) return false;
        int o = Plan.Plots[plot].Offer;
        return o < 0 || S.OfferBought[o];
    }

    /// <summary>Offer id of a kind (-1 if the plan has none); never a hard-coded id.</summary>
    internal int OfferOf(OfferKind k)
    {
        for (int i = 0; i < Plan.Offers.Length; i++) if (Plan.Offers[i].Kind == k) return i;
        return -1;
    }

    private void RefreshWorkplaces()
    {
        int bucket = OfferOf(OfferKind.SecondBucket);
        foreach (var w in S.Workplaces)
        {
            w.Open = PlotWorks(w.Plot);
            var pk = Plan.Plots[w.Plot].Kind;
            if (pk == PlotKind.Well) w.Places = TownRules.WellPlaces + (bucket >= 0 && S.OfferBought[bucket] ? TownRules.SecondBucketPlaces : 0) + w.Hands;
            else w.Places = (pk == PlotKind.Field ? TownRules.FieldPlaces : TownRules.MillPlaces) + w.Hands; // rules v3: extra hands
            // A place that opens is taken at once by the head of the queue (r1 bug hunt, finding 1).
            while (w.Open && w.Busy < w.Places && w.QueueLen > 0) StartWork(S.Villagers[PopQueue(w)]);
        }
    }

    // ---- Villagers ----

    public int Housing
    {
        get
        {
            int c = 0;
            foreach (var p in Plan.Plots) if (p.Kind == PlotKind.Cottage && S.PlotGiven[p.Id]) c += TownRules.PerCottage + S.Extensions[p.Id] + BoonRoom;
            if (S.River != null) c += S.River.Cottages * TownRules.PerCottage;
            return c;
        }
    }

    /// <summary>Villagers a given cottage houses (PerCottage plus its extensions); 0 for anything else.</summary>
    public int CottageRoom(int plot) => Plan.Plots[plot].Kind == PlotKind.Cottage && S.PlotGiven[plot] ? TownRules.PerCottage + S.Extensions[plot] + BoonRoom : 0;

    /// <summary>Villagers living in a cottage now.</summary>
    public int Residents(int plot)
    {
        int k = 0;
        foreach (var v0 in S.Villagers) if (v0.Home == plot) k++;
        return k;
    }

    private void AddVillager()
    {
        // The given cottage with most free room (lowest id on a tie).
        int best = -1, bestN = int.MinValue;
        foreach (var p in Plan.Plots)
        {
            if (p.Kind != PlotKind.Cottage || !S.PlotGiven[p.Id]) continue;
            int free = CottageRoom(p.Id) - Residents(p.Id);
            if (free > bestN) { bestN = free; best = p.Id; }
        }
        var home = Plan.Plots[best];
        var v = new VillagerState { Id = S.Villagers.Length, Home = best, Node = home.Node };
        if (S.River != null && bestN <= 0) v.RHome = RiverCottageWithRoom(); // second age: lives by the river (first-age cottage stays its errand home)
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
    internal int LoadUnits => TownRules.BaseLoad + S.LoadLevel;

    internal void Earn(double coin, Good g)
    {
        S.EarnedBy[(int)g] += coin;
        Earn(coin);
    }

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
