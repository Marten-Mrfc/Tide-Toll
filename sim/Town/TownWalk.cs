using System;

namespace TideToll.Sim.Town;

/// <summary>Villagers on the road graph: speeds (wear, surface, crowding, pace, bell), routes by travel time, wear.</summary>
public sealed partial class Town
{
    private readonly double[] _dist;
    private readonly int[] _prevRoad;
    private readonly bool[] _done;
    private readonly double[] _pair;
    private long _pairTick = -1;

    internal double SurfaceFactor(int r) => S.RoadSurface[r] switch
    {
        Surface.Gravel => TownRules.GravelSpeedFactor,
        Surface.Cobbles => TownRules.CobbleSpeedFactor,
        _ => TownRules.TrailSpeedFactor + (1 - TownRules.TrailSpeedFactor) * S.RoadWear[r],
    };

    /// <summary>Walkers the road carries before they slow down.</summary>
    public double Comfort(int r)
    {
        double per = S.RoadSurface[r] switch
        {
            Surface.Gravel => TownRules.ComfortPerCellGravel,
            Surface.Cobbles => TownRules.ComfortPerCellCobbles,
            _ => TownRules.ComfortPerCellTrack,
        };
        double c = Math.Max(TownRules.MinComfort, Plan.Roads[r].Length * per);
        if (S.RoadSurface[r] != Surface.Track) c *= 1 + TownRules.PavePerkComfort * (S.Paved / TownRules.PerkEvery);
        if (S.Boons[(int)Boon.WiderLanes]) c *= 1 + TownRules.BoonLanes;
        return c;
    }

    /// <summary>Speed factor from crowding with <paramref name="walkers"/> on the road: 1 up to its comfort, then smoothly lower, never below CrowdMinFactor.</summary>
    internal double CrowdFactor(int r, int walkers)
    {
        double c = Comfort(r);
        double excess = walkers - c;
        if (excess <= 0) return 1;
        return Math.Max(TownRules.CrowdMinFactor, 1 / (1 + TownRules.CrowdSlowdown * excess / c));
    }

    internal double PaceFactor => PaceFactorAt(S.PaceLevel) * (S.Boons[(int)Boon.StrongerBacks] ? 1 + TownRules.BoonWalk : 1);
    /// <summary>Pace walking factor at a level (rules v3: plus TownRules.PaceMilestone at every fifth step; no boons).</summary>
    public static double PaceFactorAt(int level)
    {
        int l = Math.Min(level, TownRules.PaceMax + TownRulesRiver.PaceMore[^1]);
        return 1 + TownRules.PaceStep * l + TownRules.PaceMilestone * (l / TownRules.MilestoneEvery);
    }
    public bool BellRinging => Time < S.BellUntil;

    /// <summary>Walking speed on a road now, cells per second, for one more walker than are on it.</summary>
    internal double RoadSpeed(int r, int walkers) =>
        TownRules.BaseSpeed * PaceFactor * (BellRinging ? TownRules.BellHurry : 1) * SurfaceFactor(r) * CrowdFactor(r, walkers);

    /// <summary>Shortest travel times from node <paramref name="from"/> over open roads (Dijkstra; ties by lower node id).</summary>
    private void Dijkstra(int from)
    {
        int nn = Plan.Nodes.Length;
        for (int i = 0; i < nn; i++) { _dist[i] = double.PositiveInfinity; _prevRoad[i] = -1; _done[i] = false; }
        _dist[from] = 0;
        for (int it = 0; it < nn; it++)
        {
            int u = -1; double best = double.PositiveInfinity;
            for (int i = 0; i < nn; i++) if (!_done[i] && _dist[i] < best) { best = _dist[i]; u = i; }
            if (u < 0) break;
            _done[u] = true;
            var rs = Plan.Nodes[u].Roads;
            for (int k = 0; k < rs.Length; k++)
            {
                int r = rs[k];
                if (!S.RoadOpen[r]) continue;
                var road = Plan.Roads[r];
                int v = road.A == u ? road.B : road.A;
                double t = best + road.Length / RoadSpeed(r, S.RoadWalkers[r] + 1);
                if (t < _dist[v]) { _dist[v] = t; _prevRoad[v] = r; }
            }
        }
    }

    /// <summary>Travel time between two nodes now (all pairs, worked out at most once per tick).</summary>
    internal double TravelTime(int a, int b)
    {
        int nn = Plan.Nodes.Length;
        if (_pairTick != S.Tick)
        {
            _pairTick = S.Tick;
            for (int s = 0; s < nn; s++)
            {
                Dijkstra(s);
                for (int i = 0; i < nn; i++) _pair[s * nn + i] = _dist[i];
            }
        }
        return _pair[a * nn + b];
    }

    /// <summary>Sets the villager walking from its node to <paramref name="target"/>. False if no open way exists.</summary>
    internal bool SetRoute(VillagerState v, int target)
    {
        v.Target = target;
        v.PathLen = 0; v.PathPos = 0;
        if (target == v.Node) { v.Phase = VillagerPhase.Idle; return true; }
        Dijkstra(v.Node);
        if (double.IsPositiveInfinity(_dist[target])) return false;
        int n = 0;
        for (int c = target; c != v.Node; )
        {
            int r = _prevRoad[c];
            v.Path[n++] = r;
            var road = Plan.Roads[r];
            c = road.A == c ? road.B : road.A;
        }
        Array.Reverse(v.Path, 0, n);
        v.PathLen = n;
        EnterRoad(v);
        return true;
    }

    private void EnterRoad(VillagerState v)
    {
        int r = v.Path[v.PathPos];
        var road = Plan.Roads[r];
        v.Road = r;
        v.Dir = road.A == v.Node ? 1 : -1;
        v.Dist = v.Dir == 1 ? 0 : road.Length;
        v.Phase = VillagerPhase.Walking;
        S.RoadWalkers[r]++;
    }

    /// <summary>Moves a walking villager for one tick. Returns true when it reached its target node this tick.</summary>
    private bool Walk(VillagerState v)
    {
        double budget = TownRules.Dt; // seconds of walking left this tick
        while (budget > 1e-12)
        {
            int r = v.Road;
            var road = Plan.Roads[r];
            double speed = RoadSpeed(r, S.RoadWalkers[r]);
            double left = v.Dir == 1 ? road.Length - v.Dist : v.Dist;
            double step = speed * budget;
            if (step < left)
            {
                v.Dist += v.Dir * step;
                Footsteps(r, step);
                budget = 0;
                break;
            }
            Footsteps(r, left);
            budget -= left / speed;
            S.RoadWalkers[r]--;
            v.Node = v.Dir == 1 ? road.B : road.A;
            v.ArrivedRoad = r;
            v.PathPos++;
            if (v.PathPos >= v.PathLen)
            {
                v.Road = -1;
                v.Phase = VillagerPhase.Idle;
                v.Dist = 0;
                return true;
            }
            EnterRoad(v);
        }
        return false;
    }

    private void Footsteps(int r, double cells)
    {
        S.RoadSteps[r] += cells;
        if (S.RoadWear[r] >= 1) return;
        S.RoadWear[r] += cells / (Plan.Roads[r].Length * TownRules.PassesToWearIn);
        if (S.RoadWear[r] >= 1)
        {
            S.RoadWear[r] = 1;
            Emit(new TownEvent { Kind = TownEventKind.RoadWornIn, Index = r });
        }
    }

    /// <summary>Puts the villager's drawn position where it is now (road, queue line or node); keeps last tick's for interpolation.</summary>
    private void Place(VillagerState v)
    {
        if (v.Phase == VillagerPhase.Walking)
        {
            Plan.Roads[v.Road].At(v.Dist, out v.X, out v.Y, out double h);
            v.Heading = v.Dir == 1 ? h : NormAngle(h + Math.PI);
            return;
        }
        if (v.Phase == VillagerPhase.Queued)
        {
            int pos = QueuePosition(v);
            QueuePoint(v.Workplace, v.ArrivedRoad, pos, out v.X, out v.Y, out v.Heading);
            return;
        }
        if (v.Phase == VillagerPhase.Working && v.Workplace >= 0)
        {
            WorkSpot(S.Workplaces[v.Workplace].Plot, v.Slot, out v.X, out v.Y, out v.Heading);
            return;
        }
        if (v.Node == Plan.Plots[v.Home].Node)
        {
            WorkSpot(v.Home, v.Id, out v.X, out v.Y, out v.Heading);
            return;
        }
        var nd = Plan.Nodes[v.Node];
        v.X = nd.X + 0.5; v.Y = nd.Y + 0.5;
    }

    /// <summary>
    /// Rules v2: where a villager stands while working at a plot or idle at home (map cells). Well: on its rim;
    /// field: along its rows; mill: around the door inside the footprint; cottage: at the door and in the yard.
    /// Spot <paramref name="slot"/> is a small number (work spot index, or villager id at home).
    /// </summary>
    public void WorkSpot(int plot, int slot, out double x, out double y, out double heading)
    {
        var p = Plan.Plots[plot];
        if (slot < 0) slot = 0;
        double cx = p.X + p.W / 2.0, cy = p.Y + p.H / 2.0;
        double dx = p.DoorX + 0.5 - cx, dy = p.DoorY + 0.5 - cy;
        double dl = Math.Sqrt(dx * dx + dy * dy);
        if (dl < 1e-9) { dx = 0; dy = 1; dl = 1; }
        dx /= dl; dy /= dl; // unit vector from the centre to the door
        switch (p.Kind)
        {
            case PlotKind.Well:
            {
                // On the rim, first spot facing the door, then round the well.
                double a0 = Math.Atan2(dy, dx), a = a0 + slot * 2.3;
                double r = Math.Min(p.W, p.H) / 2.0 + 0.15;
                x = cx + r * Math.Cos(a); y = cy + r * Math.Sin(a);
                heading = NormAngle(a + Math.PI);
                return;
            }
            case PlotKind.Field:
            {
                // Rows run east-west; one worker per row, staggered along it.
                int rows = Math.Max(1, p.H - 1), cols = Math.Max(1, p.W - 2);
                int row = slot % rows, col = (slot * 3 + 1) % cols;
                x = p.X + 1 + col + 0.5; y = p.Y + 0.5 + row + 0.5 * (row % 2);
                heading = row % 2 == 0 ? 0 : Math.PI;
                return;
            }
            case PlotKind.Cottage:
            {
                // At the door first, then in the yard beside it.
                double side = (slot % 2 == 0 ? 1 : -1) * (0.45 + 0.35 * (slot % 3));
                double ex = p.DoorX + 0.5 - dx * 0.2, ey = p.DoorY + 0.5 - dy * 0.2;
                x = ex - dy * side; y = ey + dx * side;
                heading = Math.Atan2(-dy, -dx);
                return;
            }
            default:
            {
                // Mill and others: inside, beside the door.
                double side = (slot % 2 == 0 ? 1 : -1) * 0.6 * ((slot + 2) / 2);
                double ex = p.DoorX + 0.5 - dx * 1.0, ey = p.DoorY + 0.5 - dy * 1.0;
                x = ex - dy * side; y = ey + dx * side;
                heading = Math.Atan2(dy, dx);
                return;
            }
        }
    }

    /// <summary>The spot of place <paramref name="pos"/> (0 = first) in the queue of a workplace, along the road the person came in on.</summary>
    internal void QueuePoint(int wp, int road, int pos, out double x, out double y, out double heading)
    {
        int node = Plan.Plots[S.Workplaces[wp].Plot].Node;
        if (road < 0)
        {
            var rs = Plan.Nodes[node].Roads;
            road = rs.Length > 0 ? rs[0] : -1;
        }
        var nd = Plan.Nodes[node];
        if (road < 0) { x = nd.X + 0.5; y = nd.Y + 0.5; heading = 0; return; }
        var rd = Plan.Roads[road];
        double back = Math.Min(rd.Length, (pos + 1) * TownRules.QueueSpacing);
        double d = rd.A == node ? back : rd.Length - back;
        rd.At(d, out x, out y, out double h);
        heading = rd.A == node ? NormAngle(h + Math.PI) : h; // facing the door
    }

    private static double NormAngle(double a)
    {
        while (a > Math.PI) a -= 2 * Math.PI;
        while (a <= -Math.PI) a += 2 * Math.PI;
        return a;
    }
}
