using System;

namespace TideToll.Sim.Town;

/// <summary>Trips and jobs: choosing a job, queueing for a place, working, carrying, delivering.</summary>
public sealed partial class Town
{
    private void StepVillager(VillagerState v)
    {
        v.PrevX = v.X; v.PrevY = v.Y;
        switch (v.Phase)
        {
            case VillagerPhase.Idle:
                Arrive(v, 0);
                break;
            case VillagerPhase.Walking:
                if (Walk(v)) Arrive(v, 0);
                break;
            case VillagerPhase.Working:
                v.WorkLeft -= TownRules.Dt * (BellRinging ? TownRules.BellHurry : 1);
                if (v.WorkLeft <= 1e-9) FinishWork(v);
                break;
            case VillagerPhase.Queued:
                break;
        }
        Place(v);
    }

    /// <summary>The villager is at its target node (or idle): start work, deliver, or pick the next job.</summary>
    private void Arrive(VillagerState v, int depth)
    {
        if (depth > 4) return; // nothing to do right here; try again next tick
        if (v.Job == JobKind.None)
        {
            if (!ChooseJob(v)) return;
            if (v.Phase == VillagerPhase.Idle) Arrive(v, depth + 1);
            return;
        }
        if (v.Stage < 2)
        {
            var w = S.Workplaces[v.Workplace];
            if (!w.Open) { DropJob(v); return; }
            if (w.Busy < w.Places && w.QueueLen == 0) StartWork(v);
            else
            {
                w.Queue[w.QueueLen++] = v.Id;
                v.Phase = VillagerPhase.Queued;
            }
            return;
        }
        Deliver(v);
        DropJob(v);
        if (ChooseJob(v) && v.Phase == VillagerPhase.Idle) Arrive(v, depth + 1);
    }

    private void DropJob(VillagerState v)
    {
        v.Job = JobKind.None; v.Stage = 0; v.Workplace = -1; v.Phase = VillagerPhase.Idle;
    }

    private void StartWork(VillagerState v)
    {
        var w = S.Workplaces[v.Workplace];
        w.Busy++;
        v.Phase = VillagerPhase.Working;
        var kind = Plan.Plots[w.Plot].Kind;
        v.WorkLeft = kind == PlotKind.Well ? TownRules.WellWork : kind == PlotKind.Field ? TownRules.FieldWork : TownRules.MillWork;
    }

    private void FinishWork(VillagerState v)
    {
        var w = S.Workplaces[v.Workplace];
        w.Busy--;
        // The first in the queue takes the place.
        if (w.QueueLen > 0)
        {
            int next = w.Queue[0];
            for (int i = 1; i < w.QueueLen; i++) w.Queue[i - 1] = w.Queue[i];
            w.QueueLen--;
            StartWork(S.Villagers[next]);
        }
        v.Amount = LoadUnits;
        if (v.Job == JobKind.Flour && v.Stage == 0)
        {
            v.Carry = Good.Grain;
            v.Stage = 1;
            v.Workplace = MillWorkplace();
            if (v.Workplace < 0 || !SetRoute(v, Plan.Plots[S.Workplaces[v.Workplace].Plot].Node)) { DropJobCarry(v); return; }
            return;
        }
        v.Carry = v.Job switch { JobKind.Water => Good.Water, JobKind.Flour => Good.Flour, _ => Good.Grain };
        v.Stage = 2;
        int dest = v.Job is JobKind.Water or JobKind.GrainHome ? Plan.Plots[v.Home].Node : Plan.Plots[_marketPlot].Node;
        v.Workplace = -1;
        if (!SetRoute(v, dest)) DropJobCarry(v);
        else if (v.Phase == VillagerPhase.Idle) Arrive(v, 1);
    }

    private void DropJobCarry(VillagerState v)
    {
        v.Carry = Good.None; v.Amount = 0;
        DropJob(v);
    }

    private int MillWorkplace()
    {
        for (int i = 0; i < S.Workplaces.Length; i++)
            if (S.Workplaces[i].Open && Plan.Plots[S.Workplaces[i].Plot].Kind == PlotKind.Mill) return i;
        return -1;
    }

    internal double JobValue(JobKind j) => j switch
    {
        JobKind.Water => TownRules.WaterValue,
        JobKind.GrainHome => TownRules.GrainHomeValue,
        JobKind.GrainMarket => TownRules.GrainMarketValue,
        JobKind.Flour => TownRules.FlourValue,
        _ => 0,
    };

    private void Deliver(VillagerState v)
    {
        double coin = v.Amount * JobValue(v.Job) * CoinBonus;
        Earn(coin);
        S.Trips++;
        S.Delivered[(int)v.Carry] += v.Amount;
        var nd = Plan.Nodes[v.Node];
        int plot = v.Job is JobKind.Water or JobKind.GrainHome ? v.Home : _marketPlot;
        Emit(new TownEvent { Kind = TownEventKind.Delivery, Villager = v.Id, Good = v.Carry, Amount = v.Amount, Coin = coin, X = nd.X + 0.5, Y = nd.Y + 0.5, Plot = plot });
        v.Carry = Good.None; v.Amount = 0;
    }

    /// <summary>Expected wait for a place at a workplace, counting who is working, queued or on the way there.</summary>
    private double ExpectedWait(int wi, VillagerState self)
    {
        var w = S.Workplaces[wi];
        int committed = w.Busy + w.QueueLen;
        foreach (var o in S.Villagers)
            if (o != self && o.Workplace == wi && o.Phase == VillagerPhase.Walking) committed++;
        int over = committed - w.Places + 1;
        if (over <= 0) return 0;
        var kind = Plan.Plots[w.Plot].Kind;
        double work = kind == PlotKind.Well ? TownRules.WellWork : kind == PlotKind.Field ? TownRules.FieldWork : TownRules.MillWork;
        return over * work / w.Places;
    }

    /// <summary>Picks the job paying the most coin per second (travel, work and expected wait), then sets off. Ties: lower workplace, then job order.</summary>
    private bool ChooseJob(VillagerState v)
    {
        int home = Plan.Plots[v.Home].Node;
        int market = _marketPlot >= 0 && PlotWorks(_marketPlot) ? Plan.Plots[_marketPlot].Node : -1;
        int mill = MillWorkplace();
        int millNode = mill >= 0 ? Plan.Plots[S.Workplaces[mill].Plot].Node : -1;
        double best = 0; JobKind bestJob = JobKind.None; int bestW = -1;
        double load = LoadUnits * CoinBonus;
        for (int wi = 0; wi < S.Workplaces.Length; wi++)
        {
            var w = S.Workplaces[wi];
            if (!w.Open) continue;
            var kind = Plan.Plots[w.Plot].Kind;
            if (kind == PlotKind.Mill) continue;
            int wn = Plan.Plots[w.Plot].Node;
            double toW = TravelTime(v.Node, wn);
            if (double.IsPositiveInfinity(toW)) continue;
            double wait = ExpectedWait(wi, v);
            if (kind == PlotKind.Well)
            {
                Consider(JobKind.Water, wi, toW + wait + TownRules.WellWork + TravelTime(wn, home), ref best, ref bestJob, ref bestW, load);
                continue;
            }
            double baseT = toW + wait + TownRules.FieldWork;
            Consider(JobKind.GrainHome, wi, baseT + TravelTime(wn, home), ref best, ref bestJob, ref bestW, load);
            if (market >= 0) Consider(JobKind.GrainMarket, wi, baseT + TravelTime(wn, market), ref best, ref bestJob, ref bestW, load);
            if (market >= 0 && millNode >= 0)
                Consider(JobKind.Flour, wi, baseT + TravelTime(wn, millNode) + ExpectedWait(mill, v) + TownRules.MillWork + TravelTime(millNode, market), ref best, ref bestJob, ref bestW, load);
        }
        if (bestJob == JobKind.None) return false;
        v.Job = bestJob; v.Stage = 0; v.Workplace = bestW;
        if (!SetRoute(v, Plan.Plots[S.Workplaces[bestW].Plot].Node)) { DropJob(v); return false; }
        return true;
    }

    private void Consider(JobKind j, int wi, double seconds, ref double best, ref JobKind bestJob, ref int bestW, double load)
    {
        if (double.IsPositiveInfinity(seconds)) return;
        double rate = load * JobValue(j) / Math.Max(0.1, seconds);
        if (rate > best + 1e-12) { best = rate; bestJob = j; bestW = wi; }
    }

    /// <summary>Index of a villager in its workplace queue (0 = next), or -1.</summary>
    internal int QueuePosition(VillagerState v)
    {
        if (v.Workplace < 0) return -1;
        var w = S.Workplaces[v.Workplace];
        for (int i = 0; i < w.QueueLen; i++) if (w.Queue[i] == v.Id) return i;
        return -1;
    }
}
