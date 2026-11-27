using System;

namespace TideToll.Sim.Town;

/// <summary>
/// The second age's jobs: carrying Grain and Flour to the boats, Wool from the pasture, Cloth from the weaver.
/// A river job is an itinerary of steps (walk the first-age roads to the bank node, walk the river plan, work, deliver).
/// Villagers choose them by the same best-coin-per-second rule as every other job.
/// </summary>
public sealed partial class Town
{
    private const int OpTown = 1, OpRiver = 2, OpWork = 3, OpBoat = 4, OpMarket = 5, OpExit = 6;
    private const int SitePasture = 0, SiteWeaver = 1;
    private readonly double[] _rdist = new double[RiverPoint.Count];
    private readonly bool[] _rdone = new bool[RiverPoint.Count];

    private int RiverCottageWithRoom()
    {
        var rp = RiverPlan!;
        foreach (var p in rp.Plots)
        {
            if (p.Kind != RiverPlotKind.Cottage || !R!.PlotGiven[p.Id]) continue;
            int k = 0;
            foreach (var o in S.Villagers) if (o.RHome == p.Id) k++;
            if (k < TownRules.PerCottage) return p.Id;
        }
        return -1;
    }

    internal static bool IsRiverJob(JobKind j) => j >= JobKind.GrainBoat;

    internal double RiverJobValue(JobKind j) => j switch
    {
        JobKind.GrainBoat => BoatUnitPrice(Good.Grain),
        JobKind.FlourBoat => BoatUnitPrice(Good.Flour),
        JobKind.WoolBoat => BoatUnitPrice(Good.Wool),
        JobKind.ClothBoat => BoatUnitPrice(Good.Cloth),
        JobKind.WoolMarket => StorePrice(Good.Wool),
        JobKind.ClothMarket => StorePrice(Good.Cloth),
        _ => 0,
    };

    /// <summary>What a boat pays per unit of a good now (before the town's coin bonus).</summary>
    public double BoatUnitPrice(Good g) => StorePrice(g) * TownRulesRiver.BoatPremium * BoatPriceFactor * (OpeningTrade ? TownRulesRiver.OpeningPremium : 1);
    /// <summary>Green pass: the landing's opening trade: until the opening boats have all been served, boats pay more.</summary>
    public bool OpeningTrade => R != null && R.BoatsFilled + R.BoatsShort < TownRulesRiver.OpeningBoats;

    // ---- River routes ----

    private double WalkSpeed => TownRules.BaseSpeed * PaceFactor * (BellRinging ? TownRules.BellHurry : 1);

    private bool LinkUsable(RiverLink l) => R!.LinkOpen[l.Id] && (l.Kind != RiverLinkKind.Ferry || R.FerryBought);

    private double FordWait()
    {
        if (FordOpen) return 0;
        double a = Math.Acos(1 - 2 * TownRulesRiver.FordOpenBelow) / (2 * Math.PI);
        double ph = TidePhase;
        return ph > a && ph < 1 - a ? (1 - a - ph) * TownRulesRiver.TidePeriod : 0;
    }

    private double LinkCost(RiverLink l)
    {
        double sp = WalkSpeed;
        return l.Kind switch
        {
            // Queues count: people already waiting at either end, served a width (or a raft load) at a time.
            RiverLinkKind.Ford => l.Length / (sp * TownRulesRiver.FordSpeed) * (1 + (WaitingAt(l.Id, l.A) + WaitingAt(l.Id, l.B)) / (double)TownRulesRiver.FordWidth) + FordWait(),
            RiverLinkKind.Ferry => (l.Length / FerrySpeed + TownRulesRiver.FerryDock) * (2 + 2 * (WaitingAt(l.Id, l.A) + WaitingAt(l.Id, l.B)) / (double)FerryCapacity),
            RiverLinkKind.Drawbridge => l.Length / (sp * TownRulesRiver.BridgeSpeed) + 1 + (R!.Lift > 0 ? LiftSeconds : 0),
            RiverLinkKind.Footbridge => l.Length / (sp * TownRulesRiver.BridgeSpeed),
            _ => l.Length / sp,
        };
    }

    /// <summary>Seconds from every river point to <paramref name="to"/> into _rdist (infinity: no open way).</summary>
    private void RiverDijkstra(int to)
    {
        var rp = RiverPlan!;
        for (int i = 0; i < _rdist.Length; i++) { _rdist[i] = double.PositiveInfinity; _rdone[i] = false; }
        _rdist[to] = 0;
        while (true)
        {
            int u = -1;
            for (int i = 0; i < _rdist.Length; i++) if (!_rdone[i] && !double.IsPositiveInfinity(_rdist[i]) && (u < 0 || _rdist[i] < _rdist[u])) u = i;
            if (u < 0) return;
            _rdone[u] = true;
            foreach (var l in rp.Links)
            {
                if (!LinkUsable(l)) continue;
                int o = l.A == u ? l.B : l.B == u ? l.A : -1;
                if (o < 0) continue;
                double d = _rdist[u] + LinkCost(l);
                if (d < _rdist[o]) _rdist[o] = d;
            }
        }
    }

    private double RiverTime(int from, int to) { RiverDijkstra(to); return _rdist[from]; }

    /// <summary>Units still wanted by boats on the river for a good, less what villagers already carry or fetch for them.</summary>
    internal int BoatNeed(Good g, VillagerState? self)
    {
        int need = 0;
        foreach (var b in R!.Boats) if (b.Phase is BoatPhase.Arriving or BoatPhase.Waiting or BoatPhase.Moored && b.Good == g) need += b.Want - b.Loaded;
        foreach (var o in S.Villagers)
            if (o != self && GoodOf(o.Job) == g && (o.Job is JobKind.GrainBoat or JobKind.FlourBoat or JobKind.WoolBoat or JobKind.ClothBoat)) need -= LoadUnits;
        return need;
    }

    private int SiteBusy(int site) => site == SitePasture ? R!.PastureBusy : R!.WeaverBusy;
    private int SitePlaces(int site) => site == SitePasture ? TownRulesRiver.PasturePlaces : TownRulesRiver.WeaverPlaces;
    internal double SiteWait(int site, VillagerState self)
    {
        int c = SiteBusy(site);
        foreach (var o in S.Villagers)
        {
            if (o == self) continue;
            if (o.RMode != 0 && o.Phase == VillagerPhase.Queued && o.RSite == site) c++;
            else if (SiteFixNow && o.RSite != site && BoundForSite(o, site)) c++; // n7: those on their way there count too
        }
        int over = c - SitePlaces(site) + 1;
        return over <= 0 ? 0 : over * (site == SitePasture ? TownRulesRiver.ShearTime : TownRulesRiver.WeaveTime) / ToolsFactor / SitePlaces(site);
    }

    /// <summary>True when the villager's itinerary still has work at <paramref name="site"/> ahead (not yet there).</summary>
    private static bool BoundForSite(VillagerState o, int site)
    {
        if (o.Job is not (JobKind.WoolBoat or JobKind.WoolMarket or JobKind.ClothBoat or JobKind.ClothMarket)) return false;
        int step = Op(OpWork, site);
        for (int k = o.RPos; k < o.RLen; k++) if (o.RSteps[k] == step) return true;
        return false;
    }

    /// <summary>Adds the river jobs to the choice. Wool and Cloth jobs have no first-age workplace (bestW = -1).</summary>
    private void ConsiderRiver(VillagerState v, ref double best, ref JobKind bestJob, ref int bestW, double load)
    {
        var rp = RiverPlan!; var r = R!;
        if (!r.PlotGiven[rp.PlotOf(RiverPlotKind.Landing).Id]) return;
        int bank = rp.BankNode;
        double toBank = TravelTime(v.Node, bank);
        if (double.IsPositiveInfinity(toBank)) return;
        double bankToLanding = RiverTime(RiverPoint.Bank, RiverPoint.Landing);
        int market = _marketPlot >= 0 && PlotWorks(_marketPlot) ? Plan.Plots[_marketPlot].Node : -1;
        bool grainNeed = BoatNeed(Good.Grain, v) > 0, flourNeed = BoatNeed(Good.Flour, v) > 0;
        int mill = MillWorkplace();
        for (int wi = 0; wi < S.Workplaces.Length; wi++)
        {
            var w = S.Workplaces[wi];
            if (!w.Open || Plan.Plots[w.Plot].Kind != PlotKind.Field) continue;
            int wn = Plan.Plots[w.Plot].Node;
            double baseT = TravelTime(v.Node, wn) + ExpectedWait(wi, v) + WorkTime(PlotKind.Field) / ToolsFactor;
            if (grainNeed) RConsider(JobKind.GrainBoat, wi, baseT + TravelTime(wn, bank) + bankToLanding, ref best, ref bestJob, ref bestW, load);
            if (flourNeed && mill >= 0)
            {
                int mn = Plan.Plots[S.Workplaces[mill].Plot].Node;
                RConsider(JobKind.FlourBoat, wi, baseT + TravelTime(wn, mn) + ExpectedWait(mill, v) + WorkTime(PlotKind.Mill) / ToolsFactor + TravelTime(mn, bank) + bankToLanding, ref best, ref bestJob, ref bestW, load);
            }
        }
        if (!RiverPlotWorks(RiverPlotKind.Pasture)) return;
        double toPasture = RiverTime(RiverPoint.Bank, RiverPoint.Pasture);
        if (double.IsPositiveInfinity(toPasture)) return;
        double pastureToLanding = _rdist[RiverPoint.Landing], pastureToBank = toPasture, pastureToWeaver = _rdist[RiverPoint.Weaver];
        double wool = toBank + toPasture + SiteWait(SitePasture, v) + TownRulesRiver.ShearTime / ToolsFactor;
        if (BoatNeed(Good.Wool, v) > 0) RConsider(JobKind.WoolBoat, -1, wool + pastureToLanding, ref best, ref bestJob, ref bestW, load);
        if (market >= 0) RConsider(JobKind.WoolMarket, -1, wool + pastureToBank + TravelTime(bank, market), ref best, ref bestJob, ref bestW, load);
        if (!RiverPlotWorks(RiverPlotKind.Weaver)) return;
        double cloth = wool + pastureToWeaver + SiteWait(SiteWeaver, v) + TownRulesRiver.WeaveTime / ToolsFactor;
        double weaverToLanding = RiverTime(RiverPoint.Weaver, RiverPoint.Landing), weaverToBank = RiverTime(RiverPoint.Weaver, RiverPoint.Bank);
        if (BoatNeed(Good.Cloth, v) > 0) RConsider(JobKind.ClothBoat, -1, cloth + weaverToLanding, ref best, ref bestJob, ref bestW, load);
        if (market >= 0) RConsider(JobKind.ClothMarket, -1, cloth + weaverToBank + TravelTime(bank, market), ref best, ref bestJob, ref bestW, load);
    }

    private void RConsider(JobKind j, int wi, double seconds, ref double best, ref JobKind bestJob, ref int bestW, double load)
    {
        if (double.IsPositiveInfinity(seconds) || double.IsNaN(seconds)) return;
        double rate = Leaned(j, load * RiverJobValue(j) / Math.Max(0.1, seconds));
        if (rate > best + 1e-12) { best = rate; bestJob = j; bestW = wi; }
    }

    // ---- Itineraries ----

    private void Steps(VillagerState v, int a, int b = 0, int c = 0, int d = 0, int e = 0, int f = 0, int g = 0, int h = 0)
    {
        v.RLen = 0; v.RPos = 0;
        Span<int> s = stackalloc int[8] { a, b, c, d, e, f, g, h };
        for (int i = 0; i < 8 && s[i] != 0; i++) v.RSteps[v.RLen++] = s[i];
    }

    private static int Op(int op, int arg) => op * 1000 + arg;

    /// <summary>Starts a Wool or Cloth job from the villager's node (first-age road graph).</summary>
    private bool StartRiverJob(VillagerState v, JobKind j)
    {
        var rp = RiverPlan!;
        int bank = Op(OpTown, rp.BankNode), market = _marketPlot >= 0 ? Plan.Plots[_marketPlot].Node : 0;
        v.Job = j; v.Stage = 0; v.Workplace = -1;
        switch (j)
        {
            case JobKind.WoolBoat: Steps(v, bank, Op(OpRiver, RiverPoint.Pasture), Op(OpWork, SitePasture), Op(OpRiver, RiverPoint.Landing), Op(OpBoat, 0), Op(OpRiver, RiverPoint.Bank), Op(OpExit, 0)); break;
            case JobKind.WoolMarket: Steps(v, bank, Op(OpRiver, RiverPoint.Pasture), Op(OpWork, SitePasture), Op(OpRiver, RiverPoint.Bank), Op(OpTown, market), Op(OpMarket, 0)); break;
            case JobKind.ClothBoat: Steps(v, bank, Op(OpRiver, RiverPoint.Pasture), Op(OpWork, SitePasture), Op(OpRiver, RiverPoint.Weaver), Op(OpWork, SiteWeaver), Op(OpRiver, RiverPoint.Landing), Op(OpBoat, 0), Op(OpRiver, RiverPoint.Bank)); v.RSteps[v.RLen++] = Op(OpExit, 0); break;
            default: Steps(v, bank, Op(OpRiver, RiverPoint.Pasture), Op(OpWork, SitePasture), Op(OpRiver, RiverPoint.Weaver), Op(OpWork, SiteWeaver), Op(OpRiver, RiverPoint.Bank), Op(OpTown, market), Op(OpMarket, 0)); break;
        }
        RiverNext(v);
        return true;
    }

    /// <summary>Grain or Flour made for a boat: carry it to the landing, then back to the bank node.</summary>
    private void StartBoatCarry(VillagerState v)
    {
        var rp = RiverPlan!;
        v.Carry = v.Job == JobKind.FlourBoat ? Good.Flour : Good.Grain;
        v.Stage = 2; v.Workplace = -1;
        Steps(v, Op(OpTown, rp.BankNode), Op(OpRiver, RiverPoint.Landing), Op(OpBoat, 0), Op(OpRiver, RiverPoint.Bank), Op(OpExit, 0));
        RiverNext(v);
    }

    private void EndRiverJob(VillagerState v)
    {
        v.RLen = 0; v.RPos = 0; v.Carry = Good.None; v.Amount = 0; v.RSite = -1;
        if (v.RMode != 0) { v.RMode = 0; v.Node = RiverPlan!.BankNode; v.Road = -1; }
        DropJob(v);
    }

    /// <summary>Runs the itinerary from its current step until the villager has to walk, wait or work.</summary>
    private void RiverNext(VillagerState v)
    {
        var rp = RiverPlan!;
        for (int guard = 0; guard < 16 && v.RPos < v.RLen; guard++)
        {
            int s = v.RSteps[v.RPos], op = s / 1000, arg = s % 1000;
            switch (op)
            {
                case OpTown:
                    if (v.RMode != 0) { v.RMode = 0; v.Node = rp.BankNode; v.RLink = -1; }
                    v.RPos++;
                    if (!SetRoute(v, arg)) { EndRiverJob(v); return; }
                    if (v.Phase == VillagerPhase.Walking) return;
                    break;
                case OpRiver:
                    if (v.RMode == 0) { v.RMode = 1; v.RPoint = RiverPoint.Bank; v.RLink = -1; v.Road = -1; }
                    if (v.RPoint == arg && v.RLink < 0) { v.RPos++; break; }
                    v.RTarget = arg; v.Phase = VillagerPhase.Walking;
                    return;
                case OpWork:
                    v.RSite = arg;
                    if (SiteBusy(arg) >= SitePlaces(arg)) { v.Phase = VillagerPhase.Queued; return; }
                    if (arg == SitePasture) R!.PastureBusy++; else R!.WeaverBusy++;
                    v.Phase = VillagerPhase.Working;
                    v.WorkLeft = arg == SitePasture ? TownRulesRiver.ShearTime : TownRulesRiver.WeaveTime;
                    return;
                case OpBoat: DeliverLanding(v); v.RPos++; break;
                case OpMarket: DeliverMarket(v); v.RPos++; break;
                case OpExit: v.RMode = 0; v.Node = rp.BankNode; v.Road = -1; v.RPos++; break;
            }
        }
        if (v.RPos >= v.RLen) { EndRiverJob(v); }
    }

    private void DeliverLanding(VillagerState v)
    {
        var r = R!; var rp = RiverPlan!;
        if (v.Amount <= 0) return;
        Good g = v.Carry;
        int boat = -1;
        for (int i = 0; i < r.Boats.Length; i++)
        {
            var b = r.Boats[i];
            if (b.Phase == BoatPhase.Moored && b.Good == g && b.Loaded < b.Want) { boat = i; break; }
        }
        double coin;
        if (boat >= 0)
        {
            var b = r.Boats[boat];
            int put = Math.Min(v.Amount, b.Want - b.Loaded);
            b.Loaded += put;
            double unit = BoatUnitPrice(g);
            double store = StorePrice(g);
            coin = (put * unit + (v.Amount - put) * store) * CoinBonus;
        }
        else coin = v.Amount * StorePrice(g) * CoinBonus;
        coin *= TradeWorthFactor(v); // n7 trades: grain and flour carried by their own trade's members
        RiverEarn(coin, g);
        TradeEarned(v, coin);
        v.Trips++; v.Earned += coin;
        r.Delivered[(int)g] += v.Amount;
        if ((int)g < S.Delivered.Length) S.Delivered[(int)g] += v.Amount;
        S.Trips++;
        Emit(new TownEvent { Kind = TownEventKind.LandingDelivery, Villager = v.Id, Good = g, Amount = v.Amount, Coin = coin, X = rp.Px[RiverPoint.Landing], Y = rp.Py[RiverPoint.Landing], Index = boat >= 0 ? r.Boats[boat].Id : -1 });
        v.Carry = Good.None; v.Amount = 0;
    }

    /// <summary>What the landing store (or the market) pays per unit when no boat wants the good.</summary>
    public double StorePrice(Good g) => g switch
    {
        Good.Grain => TownRules.GrainMarketValue * ValueFactor(Good.Grain),
        Good.Flour => TownRules.FlourValue * ValueFactor(Good.Flour),
        Good.Wool => TownRules.GrainMarketValue * ValueFactor(Good.Grain) * TownRulesRiver.WoolRel * RiverValueFactor(Good.Wool),
        Good.Cloth => TownRules.FlourValue * ValueFactor(Good.Flour) * TownRulesRiver.ClothRel * RiverValueFactor(Good.Cloth),
        _ => 0,
    };

    private void DeliverMarket(VillagerState v)
    {
        var r = R!;
        if (v.Amount <= 0) return;
        double coin = v.Amount * StorePrice(v.Carry) * CoinBonus;
        r.EarnedMarket += coin;
        TradeEarned(v, coin);
        r.EarnedBy[(int)v.Carry] += coin;
        Earn(coin);
        v.Trips++; v.Earned += coin;
        r.Delivered[(int)v.Carry] += v.Amount;
        S.Trips++;
        var nd = Plan.Nodes[v.Node];
        Emit(new TownEvent { Kind = TownEventKind.Delivery, Villager = v.Id, Good = v.Carry, Amount = v.Amount, Coin = coin, X = nd.X + 0.5, Y = nd.Y + 0.5, Plot = _marketPlot });
        v.Carry = Good.None; v.Amount = 0;
    }

    // ---- Stepping a villager in river mode ----

    private void StepRiverVillager(VillagerState v)
    {
        v.PrevX = v.X; v.PrevY = v.Y;
        switch (v.Phase)
        {
            case VillagerPhase.Working:
                v.WorkLeft -= TownRules.Dt * (BellRinging ? TownRules.BellHurry : 1) * ToolsFactor;
                if (v.WorkLeft <= 1e-9)
                {
                    if (v.RSite == SitePasture) { R!.PastureBusy--; v.Carry = Good.Wool; }
                    else { R!.WeaverBusy--; v.Carry = Good.Cloth; }
                    v.Amount = LoadUnits;
                    v.RSite = -1; v.RPos++;
                    v.Phase = VillagerPhase.Idle;
                    RiverNext(v);
                }
                break;
            case VillagerPhase.Queued when v.RWait == -1 && v.RSite >= 0:
                RiverNext(v); // waiting for a place at the pasture or the weaver
                break;
            default:
                if (RiverMove(v)) { v.RPos++; RiverNext(v); }
                break;
        }
        if (v.RMode != 0) RiverPlace(v);
        else if (v.Phase == VillagerPhase.Walking && v.Road >= 0) Place(v);
    }

    /// <summary>Moves towards RTarget for one tick; true when there.</summary>
    private bool RiverMove(VillagerState v)
    {
        var rp = RiverPlan!; var r = R!;
        if (v.RLink < 0)
        {
            if (v.RPoint == v.RTarget) return true;
            RiverDijkstra(v.RTarget);
            int next = -1; double bestD = double.PositiveInfinity;
            foreach (var l in rp.Links)
            {
                if (!LinkUsable(l)) continue;
                int o = l.A == v.RPoint ? l.B : l.B == v.RPoint ? l.A : -1;
                if (o < 0) continue;
                double d = LinkCost(l) + _rdist[o];
                if (d < bestD - 1e-9) { bestD = d; next = l.Id; }
            }
            if (next < 0) { WaitAt(v, -2); return false; }
            var nl = rp.Links[next];
            if (!CanEnter(nl, v)) { WaitAt(v, next); return false; }
            v.RLink = next; v.RDir = nl.A == v.RPoint ? 1 : -1; v.RDist = v.RDir == 1 ? 0 : nl.Length;
            v.Phase = VillagerPhase.Walking; v.RWait = -1; v.RStuckSince = -1;
            if (nl.Kind == RiverLinkKind.Drawbridge) r.OnDraw++;
            if (nl.Kind == RiverLinkKind.Ford) r.OnFord++;
            if (nl.Kind == RiverLinkKind.Footbridge) r.OnFoot++;
            if (nl.Kind == RiverLinkKind.Ferry) { r.Aboard[r.AboardN++] = v.Id; return false; }
        }
        var link = rp.Links[v.RLink];
        if (link.Kind == RiverLinkKind.Ferry) return false; // carried by the raft (StepFerry lands it)
        double sp = WalkSpeed * (link.Kind == RiverLinkKind.Ford ? TownRulesRiver.FordSpeed : 1);
        v.RDist += v.RDir * sp * TownRules.Dt;
        if (v.RDist < 0 || v.RDist > link.Length)
        {
            v.RPoint = v.RDir == 1 ? link.B : link.A;
            v.RLink = -1;
            if (link.Kind != RiverLinkKind.Lane) r.Crossed[(int)link.Kind]++;
            if (link.Kind == RiverLinkKind.Drawbridge) r.OnDraw--;
            if (link.Kind == RiverLinkKind.Ford) r.OnFord--;
            if (link.Kind == RiverLinkKind.Footbridge) r.OnFoot--;
            return v.RPoint == v.RTarget;
        }
        return false;
    }

    private void WaitAt(VillagerState v, int link)
    {
        v.Phase = VillagerPhase.Queued; v.RWait = link;
        if (v.RStuckSince < 0) v.RStuckSince = Time;
    }

    private bool CanEnter(RiverLink l, VillagerState v)
    {
        var r = R!;
        switch (l.Kind)
        {
            case RiverLinkKind.Ford: return FordOpen && r.OnFord < TownRulesRiver.FordWidth;
            case RiverLinkKind.Footbridge: return r.OnFoot < TownRulesRiver.FootWidth;
            case RiverLinkKind.Ferry:
            {
                int side = v.RPoint == l.A ? 0 : 1;
                bool docked = r.RaftDir == 0 && (side == 0 ? r.RaftPos <= 0 : r.RaftPos >= 1);
                return docked && r.AboardN < FerryCapacity && r.AboardN < r.Aboard.Length;
            }
            case RiverLinkKind.Drawbridge: return r.Lift <= 0 && !BoatNearDraw() && r.OnDraw < TownRulesRiver.DrawWidth;
            default: return true;
        }
    }

    private void RiverPlace(VillagerState v)
    {
        var rp = RiverPlan!; var r = R!;
        if (v.RLink >= 0)
        {
            var l = rp.Links[v.RLink];
            double d = l.Kind == RiverLinkKind.Ferry ? r.RaftPos * l.Length : v.RDist;
            l.At(d, out v.X, out v.Y, out double h);
            v.Heading = v.RDir == 1 ? h : NormAngle(h + Math.PI);
            return;
        }
        double x = rp.Px[Math.Max(0, v.RPoint)], y = rp.Py[Math.Max(0, v.RPoint)];
        if (v.Phase == VillagerPhase.Queued && v.RWait >= 0)
        {
            int pos = 0;
            foreach (var o in S.Villagers) if (o.Id < v.Id && o.RMode != 0 && o.Phase == VillagerPhase.Queued && o.RWait == v.RWait && o.RPoint == v.RPoint) pos++;
            double away = rp.PointFar[v.RPoint] ? 1 : -1; // queue away from the water
            x += away * 0.8 * pos;
        }
        else if (v.Phase is VillagerPhase.Working or VillagerPhase.Queued && v.RSite >= 0)
        {
            var p = rp.PlotOf(v.RSite == SitePasture ? RiverPlotKind.Pasture : RiverPlotKind.Weaver);
            int k = v.Id % (p.W * p.H);
            x = p.X + 0.5 + k % p.W; y = p.Y + 0.5 + k / p.W;
            if (v.Phase == VillagerPhase.Queued) { x = rp.Px[p.Point] - 0.8; y = rp.Py[p.Point]; }
        }
        v.X = x; v.Y = y;
    }

    /// <summary>
    /// People a minute a crossing can carry now with a full queue (0 when it does not exist): its width (or the raft's load)
    /// over the time one crossing takes, times the share of time it is open (the ford: low water; the drawbridge: down).
    /// </summary>
    public double CrossingCapacity(RiverLinkKind k)
    {
        if (!CrossingExists(k)) return 0;
        var l = RiverPlan!.Links[RiverPlan.LinkOf(k)];
        double walk = TownRules.BaseSpeed * PaceFactor;
        switch (k)
        {
            case RiverLinkKind.Ford:
                double open = 2 * Math.Acos(1 - 2 * TownRulesRiver.FordOpenBelow) / (2 * Math.PI);
                return TownRulesRiver.FordWidth * 60 / (l.Length / (walk * TownRulesRiver.FordSpeed)) * open;
            case RiverLinkKind.Ferry: return FerryCapacity * 60 / (l.Length / FerrySpeed + TownRulesRiver.FerryDock);
            case RiverLinkKind.Footbridge: return TownRulesRiver.FootWidth * 60 / (l.Length / walk);
            default: return TownRulesRiver.DrawWidth * 60 / (l.Length / walk) * TownRulesRiver.DrawOpenShare;
        }
    }

    /// <summary>Longest time any villager has been waiting at a crossing right now (0 when nobody waits).</summary>
    public double LongestCrossingWait
    {
        get
        {
            double m = 0;
            foreach (var v in S.Villagers) if (v.RMode != 0 && v.RStuckSince >= 0) m = Math.Max(m, Time - v.RStuckSince);
            return m;
        }
    }
}
