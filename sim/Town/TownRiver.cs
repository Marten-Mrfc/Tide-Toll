using System;

namespace TideToll.Sim.Town;

/// <summary>The second age, The River: ringing it in, its levels, the tide, the boats, the ferry and the drawbridge.</summary>
public sealed partial class Town
{
    private RiverPlan? _rp;
    internal RiverState? R => S.River;

    /// <summary>The second age's plan (null until the age is rung in). Built from the same seed; never changes.</summary>
    public RiverPlan? RiverPlan => S.River != null ? (_rp ??= TownRiverPlanner.Build(Plan)) : null;
    /// <summary>True once the second age has been rung in.</summary>
    public bool InRiverAge => S.River != null;
    /// <summary>The borough seal: the second age is complete.</summary>
    public bool HasSeal => S.River != null && S.River.Seal;

    private void StartRiver()
    {
        _rp ??= TownRiverPlanner.Build(Plan);
        var r = new RiverState
        {
            StartTime = Time, EaseFrom = Time, RenownBase = S.Renown, RenownScale = Math.Max(TownRulesRiver.LevelRateMin, S.MaxCoinPerMinute),
            PlotGiven = new bool[_rp.Plots.Length], LinkOpen = new bool[_rp.Links.Length],
            Boats = new BoatState[TownRulesRiver.MaxBoats],
            NextBoatAt = Time + TownRulesRiver.FirstBoatAfter,
        };
        for (int i = 0; i < r.Boats.Length; i++) r.Boats[i] = new BoatState();
        S.River = r;
        S.Version = TownSave.CurrentVersion;
        CheckRiverLevels();
    }

    /// <summary>Renown needed for town level n (also the second age's levels, 17 to 28, once it is rung in).</summary>
    /// n7 retune: every second-age level costs renown on a fixed curve (TownRulesRiver.LevelCurve: the level minutes, growing by
    /// LevelGrowth a level) times the town's best coin rate at the ring (RenownScale). Nothing the town earns after the ring
    /// changes a level's cost, so earning more never makes the age longer and earning less never makes it shorter. Above the
    /// renown at the ring, every level's renown eases by LevelEase a minute from the ring (River.EaseFrom), the same for every
    /// town, so the age's length follows a town's income only by its logarithm: a town whose income grows slowly is not held
    /// far past the others, and one that grows fast is not let through far sooner. (Was, from
    /// the green pass: each level fixed at the level before in minutes of the town's best rate by then, so a poorer town sealed
    /// as soon or sooner.) River.LevelAt still holds each level's renown, fixed when the level before is reached: a save from
    /// before keeps the levels it had fixed and the rest follow the curve from the last one.
    public double RiverLevelRenown(int n)
    {
        if (n <= Plan.MaxLevel) return LevelRenownOf(n);
        double full = RiverLevelRenownFull(n);
        var r = R!;
        if (TownRulesRiver.LevelEase <= 0 || r.EaseFrom < 0) return full;
        return r.RenownBase + Math.Round((full - r.RenownBase) * Math.Exp(-TownRulesRiver.LevelEase * Math.Max(0, Time - r.EaseFrom) / 60));
    }

    private double RiverLevelRenownFull(int n)
    {
        var r = R!; var lv = RiverPlan!.Levels;
        int i = n - Plan.MaxLevel - 1;
        if (r.LevelAt.Length != lv.Length || r.LevelAt[0] <= 0) return r.RenownBase + Math.Round(r.RenownScale * lv[i].RenownAbove);
        if (r.LevelAt[i] > 0) return r.LevelAt[i];
        int k = i; while (r.LevelAt[k] <= 0) k--;
        return r.LevelAt[k] + Math.Round(r.RenownScale * (lv[i].RenownAbove - lv[k].RenownAbove));
    }

    private void FixNextRiverLevel()
    {
        var r = R!; var lv = RiverPlan!.Levels;
        if (r.Reached < 1 || r.Reached >= lv.Length || r.LevelAt[r.Reached] > 0) return;
        r.LevelAt[r.Reached] = r.LevelAt[r.Reached - 1] + Math.Round(r.RenownScale * (lv[r.Reached].RenownAbove - lv[r.Reached - 1].RenownAbove));
    }

    private void CheckRiverLevels()
    {
        var rp = RiverPlan!;
        var r = R!;
        if (r.LevelAt.Length == rp.Levels.Length && r.LevelAt[0] <= 0 && r.Reached == 0) r.LevelAt[0] = r.RenownBase + Math.Round(r.RenownScale * rp.Levels[0].RenownAbove);
        while (r.Reached < rp.Levels.Length && S.Renown >= RiverLevelRenown(Plan.MaxLevel + 1 + r.Reached))
        {
            ApplyRiverLevel(r.Reached++);
            if (r.LevelAt.Length == rp.Levels.Length && r.LevelAt[0] > 0) FixNextRiverLevel();
        }
    }

    private void ApplyRiverLevel(int i)
    {
        var rp = RiverPlan!; var r = R!;
        var lv = rp.Levels[i];
        S.Level = lv.Number;
        foreach (int p in lv.Plots)
        {
            r.PlotGiven[p] = true;
            if (rp.Plots[p].Kind == RiverPlotKind.Cottage) r.Cottages++;
        }
        foreach (int l in lv.Links) r.LinkOpen[l] = true;
        Emit(new TownEvent { Kind = TownEventKind.LevelReached, Level = lv.Number, Text = lv.Gift });
        switch (i)
        {
            case 5: r.FerryOffered = true; Emit(new TownEvent { Kind = TownEventKind.OfferAvailable, Item = ShopItemKind.Ferry, Index = -1, Text = "The ferry" }); break;
            case 6: r.BerthOffered = true; Emit(new TownEvent { Kind = TownEventKind.OfferAvailable, Item = ShopItemKind.Berth, Index = -1, Text = "A second berth" }); break;
            case 9: r.CraneOffered = true; Emit(new TownEvent { Kind = TownEventKind.OfferAvailable, Item = ShopItemKind.Crane, Index = -1, Text = "The crane" }); break;
        }
        if (i == rp.Levels.Length - 1 && !r.Seal)
        {
            r.Seal = true;
            Emit(new TownEvent { Kind = TownEventKind.Seal, Level = lv.Number, Text = "The borough seal" });
        }
    }

    // ---- The tide ----

    /// <summary>Phase of the tide cycle, 0..1 (0 = low water, 0.5 = high water).</summary>
    private double TidePhase
    {
        get
        {
            double t = (Time - R!.StartTime) / TownRulesRiver.TidePeriod + TownRulesRiver.TideStart;
            return t - Math.Floor(t);
        }
    }

    /// <summary>Water level 0 (lowest) .. 1 (highest); 0 before the second age.</summary>
    public double TideLevel => R == null ? 0 : 0.5 - 0.5 * Math.Cos(2 * Math.PI * TidePhase);
    /// <summary>True while the water rises.</summary>
    public bool TideRising => R != null && TidePhase < 0.5;
    /// <summary>Game seconds until the tide turns (high or low water).</summary>
    public double TideSecondsToTurn => R == null ? 0 : ((TidePhase < 0.5 ? 0.5 : 1.0) - TidePhase) * TownRulesRiver.TidePeriod;
    /// <summary>High water: boats can arrive.</summary>
    public bool HighWater => R != null && TideLevel >= TownRulesRiver.HighWater;
    /// <summary>The ford can be walked (low water and the ford given).</summary>
    public bool FordOpen => R != null && TideLevel < TownRulesRiver.FordOpenBelow;

    // ---- Step ----

    private void StepRiver()
    {
        var r = R!;
        for (int k = 1; k < 5; k++) if (CrossingExists((RiverLinkKind)k)) r.OpenSeconds[k] += TownRules.Dt;
        StepBoats();
        StepFerry();
        StepDrawbridge();
        if (S.Tick % TownRules.TickHz == 0)
        {
            // n7 retune: the levels' renown eases with time (RiverLevelRenown), so look once a second as well as on each purchase.
            if (r.EaseFrom < 0) r.EaseFrom = Time; // a save from before the retune: the ease starts when it is played on
            if (TownRulesRiver.LevelEase > 0 && !r.Seal) CheckRiverLevels();
            CheckRiverAchievements();
        }
    }

    /// <summary>Whether a crossing of this kind is built and working (the ferry: given and bought).</summary>
    public bool CrossingExists(RiverLinkKind k)
    {
        if (R == null || k == RiverLinkKind.Lane) return false;
        int l = RiverPlan!.LinkOf(k);
        return l >= 0 && R.LinkOpen[l] && (k != RiverLinkKind.Ferry || R.FerryBought);
    }

    internal int Berths => R!.BerthBought ? 2 : 1;
    public double CraneFactor => 1 + TownRulesRiver.CraneStep * (R?.Crane ?? 0);
    public double BoatPriceFactor => 1 + TownRulesRiver.PricesStep * (R?.Prices ?? 0);
    public double RiverValueFactor(Good g) => g == Good.Wool ? 1 + TownRulesRiver.RiverValueStep * (R?.WoolValue ?? 0) : g == Good.Cloth ? 1 + TownRulesRiver.RiverValueStep * (R?.ClothValue ?? 0) : ValueFactor(g);
    public double Toll => TownRulesRiver.TollBase * (1 + TownRulesRiver.TollStep * (R?.Toll ?? 0));
    public double LiftSeconds => TownRulesRiver.LiftTime * Math.Pow(1 - TownRulesRiver.WinchStep, R?.Winch ?? 0);
    public int FerryCapacity => TownRulesRiver.FerryCapacity + TownRulesRiver.FerryRaftStep * (R?.Raft ?? 0);
    public double FerrySpeed => TownRulesRiver.FerrySpeed * (1 + TownRulesRiver.FerryRopeStep * (R?.Rope ?? 0));

    private static uint Hash(int seed, int n, int salt)
    {
        uint h = (uint)seed * 2654435761u ^ (uint)n * 2246822519u ^ (uint)salt * 3266489917u;
        h ^= h >> 15; h *= 2246822519u; h ^= h >> 13; h *= 3266489917u; h ^= h >> 16;
        return h;
    }

    private bool RiverPlotWorks(RiverPlotKind k)
    {
        var rp = RiverPlan!;
        foreach (var p in rp.Plots) if (p.Kind == k && R!.PlotGiven[p.Id]) return true;
        return false;
    }

    private void StepBoats()
    {
        var r = R!; var rp = RiverPlan!;
        if (!r.PlotGiven[rp.PlotOf(RiverPlotKind.Landing).Id]) return;
        double sp = TownRulesRiver.BoatSpeed * TownRules.Dt;
        int n = Plan.Map.Size;
        double drawY = rp.DrawRow + 0.5;
        bool draw = CrossingExists(RiverLinkKind.Drawbridge);
        for (int i = 0; i < r.Boats.Length; i++)
        {
            var b = r.Boats[i];
            b.PrevX = b.X; b.PrevY = b.Y;
            switch (b.Phase)
            {
                case BoatPhase.Gone:
                    if (HighWater && Time >= r.NextBoatAt) SpawnBoat(b);
                    break;
                case BoatPhase.Arriving:
                {
                    double ty = b.Berth >= 0 ? rp.BerthY[b.Berth] : rp.WaitY[b.WaitSlot];
                    double ny = b.Y - sp;
                    if (draw && b.Y >= drawY + 2 - 1e-9 && ny < drawY + 2 && r.Lift < 1) ny = drawY + 2; // waits below the bridge until it is up
                    if (draw && b.Y > drawY && ny <= drawY && !b.TollPaid) { b.TollPaid = true; PayToll(); }
                    if (ny <= ty)
                    {
                        ny = ty;
                        if (b.Berth >= 0) { b.Phase = BoatPhase.Moored; b.Patience = TownRulesRiver.BoatPatience[(int)b.Kind]; }
                        else { b.Phase = BoatPhase.Waiting; b.Patience = TownRulesRiver.WaitPatience; }
                    }
                    b.Y = ny;
                    break;
                }
                case BoatPhase.Waiting:
                {
                    int free = FreeBerth();
                    if (free >= 0) { b.Berth = free; b.WaitSlot = -1; b.Phase = BoatPhase.Arriving; break; }
                    b.Patience -= TownRules.Dt;
                    if (b.Patience <= 0) Leave(b, true);
                    break;
                }
                case BoatPhase.Moored:
                    b.Patience -= TownRules.Dt;
                    if (b.Loaded >= b.Want) Leave(b, false);
                    else if (b.Patience <= 0) Leave(b, true);
                    break;
                case BoatPhase.Leaving:
                {
                    double ny = b.Y + sp;
                    if (draw && b.Y <= drawY - 2 + 1e-9 && ny > drawY - 2 && r.Lift < 1) ny = drawY - 2;
                    b.Y = ny;
                    if (b.Y >= n + 1) { b.Phase = BoatPhase.Gone; b.Berth = -1; }
                    break;
                }
            }
            if (b.Phase != BoatPhase.Gone) PlaceBoat(b);
        }
    }

    private int FreeBerth()
    {
        for (int k = 0; k < Berths; k++)
        {
            bool used = false;
            foreach (var o in R!.Boats) if (o.Phase is BoatPhase.Arriving or BoatPhase.Moored && o.Berth == k) used = true;
            if (!used) return k;
        }
        return -1;
    }

    private void SpawnBoat(BoatState b)
    {
        var r = R!; var rp = RiverPlan!;
        int berth = FreeBerth(), wait = -1;
        if (berth < 0)
        {
            for (int k = 0; k < rp.WaitY.Length && wait < 0; k++)
            {
                bool used = false;
                foreach (var o in r.Boats) if (o.Phase is BoatPhase.Arriving or BoatPhase.Waiting && o.WaitSlot == k) used = true;
                if (!used) wait = k;
            }
            if (wait < 0) return;
        }
        int id = r.BoatsCome++;
        b.Id = id;
        b.Kind = (id % TownRulesRiver.BargeEvery) == TownRulesRiver.BargeEvery - 1 ? BoatKind.Barge : BoatKind.Rowboat;
        // The good: one of those the town can make now, drawn from the seed and the boat number.
        Span<Good> goods = stackalloc Good[4];
        int ng = 0;
        goods[ng++] = Good.Grain;
        if (MillWorkplace() >= 0) goods[ng++] = Good.Flour;
        if (RiverPlotWorks(RiverPlotKind.Pasture) && CrossingCount() > 0) goods[ng++] = Good.Wool;
        if (RiverPlotWorks(RiverPlotKind.Weaver) && RiverPlotWorks(RiverPlotKind.Pasture) && CrossingCount() > 0) goods[ng++] = Good.Cloth;
        uint h = Hash(Seed, id, 7);
        // The newest good is asked for twice as often as each older one, so new trades show at once.
        int pick = (int)(h % (uint)(ng + 1));
        b.Good = goods[Math.Min(pick, ng - 1)];
        // River polish: while the ford is the only way over and the pasture works, one more boat in three wants Wool, so the far
        // bank is worth reaching by the slow, low-water ford (and people wait on the bank at high water).
        if (FordOnlyStretch && RiverPlotWorks(RiverPlotKind.Pasture) && h % 3 == 1) b.Good = Good.Wool;
        b.Want = Math.Max(TownRulesRiver.BoatMinUnits, (int)Math.Round(CoinPerMinute * TownRulesRiver.BoatMinutes[(int)b.Kind] / Math.Max(0.1, BoatUnitPrice(b.Good) * CoinBonus) * CraneFactor));
        b.Request = id % 2 == 1;
        b.Skipper = (int)(Hash(Seed, id, 11) % (uint)TownRulesRiver.SkipperNames.Length);
        if (b.Request) b.Want = (int)Math.Round(b.Want * 1.25);
        if (id < TownRulesRiver.OpeningBoats) b.Want = Math.Max(TownRulesRiver.BoatMinUnits, (int)Math.Round(b.Want * TownRulesRiver.OpeningCargo));
        b.Loaded = 0; b.TollPaid = false; b.Filled = false;
        b.Berth = berth; b.WaitSlot = wait;
        b.Phase = BoatPhase.Arriving;
        b.Y = Plan.Map.Size + 0.5;
        b.X = rp.CourseX[Plan.Map.Size - 1];
        b.PrevX = b.X; b.PrevY = b.Y;
        r.NextBoatAt = Time + TownRulesRiver.BoatGap;
        Emit(new TownEvent { Kind = TownEventKind.BoatArrived, Index = i32(b), Good = b.Good, Amount = b.Want, Level = (int)b.Kind, Value = b.Request ? 1 : 0, X = b.X, Y = b.Y });
    }

    private static int i32(BoatState b) => b.Id;

    private int CrossingCount()
    {
        int c = 0;
        for (int k = 1; k < 5; k++) if (CrossingExists((RiverLinkKind)k)) c++;
        return c;
    }

    private void Leave(BoatState b, bool shortOfCargo)
    {
        var r = R!;
        if (!shortOfCargo)
        {
            b.Filled = true;
            r.BoatsFilled++;
            double bonus = TownRulesRiver.FillBonus * (b.Request ? 2 : 1) * b.Want * BoatUnitPrice(b.Good) * CoinBonus;
            RiverEarn(bonus, b.Good);
            if (b.Request) r.BoatRequestsMet++;
            Emit(new TownEvent { Kind = TownEventKind.BoatFilled, Index = b.Id, Good = b.Good, Amount = b.Loaded, Coin = bonus, X = b.X, Y = b.Y, Value = b.Request ? 1 : 0 });
        }
        else
        {
            r.BoatsShort++;
            Emit(new TownEvent { Kind = TownEventKind.BoatLeft, Index = b.Id, Good = b.Good, Amount = b.Loaded, Level = b.Want, X = b.X, Y = b.Y });
        }
        b.Phase = BoatPhase.Leaving;
        b.Berth = -1; b.WaitSlot = -1;
    }

    private void PayToll()
    {
        var r = R!;
        double t = Toll;
        r.Tolls++;
        r.EarnedToll += t;
        Earn(t);
        Emit(new TownEvent { Kind = TownEventKind.TollPaid, Coin = t, X = RiverPlan!.CourseX[RiverPlan.DrawRow], Y = RiverPlan.DrawRow + 0.5 });
    }

    private void PlaceBoat(BoatState b)
    {
        var rp = RiverPlan!;
        int n = Plan.Map.Size;
        int row = Math.Clamp((int)Math.Floor(b.Y), 0, n - 1);
        double cx = rp.CourseX[row];
        double tx = cx, ty = 0;
        if (b.Berth >= 0) { tx = rp.BerthX[b.Berth]; ty = rp.BerthY[b.Berth]; }
        if (b.Berth >= 0 && b.Phase != BoatPhase.Leaving)
        {
            double f = Math.Clamp(1 - (b.Y - ty) / 4, 0, 1);
            b.X = cx + (tx - cx) * f;
        }
        else b.X = cx + (b.Phase == BoatPhase.Waiting ? 0 : 0);
        double dx = b.X - b.PrevX, dy = b.Y - b.PrevY;
        if (dx * dx + dy * dy > 1e-8) b.Heading = Math.Atan2(dy, dx);
        else if (b.Phase == BoatPhase.Moored) b.Heading = -Math.PI / 2;
    }

    // ---- Earnings ----

    internal void RiverEarn(double coin, Good g)
    {
        var r = R!;
        r.EarnedLanding += coin;
        r.EarnedBy[(int)g] += coin;
        if ((int)g < S.EarnedBy.Length) S.EarnedBy[(int)g] += coin;
        Earn(coin);
    }

    // ---- The ferry ----

    private void StepFerry()
    {
        var r = R!;
        if (!CrossingExists(RiverLinkKind.Ferry)) return;
        var link = RiverPlan!.Links[RiverPlan.LinkOf(RiverLinkKind.Ferry)];
        if (r.RaftDir == 0)
        {
            r.RaftDock -= TownRules.Dt;
            if (r.RaftDock > 0) return;
            int side = r.RaftPos < 0.5 ? 0 : 1;
            if (r.AboardN > 0 || WaitingAt(link.Id, side == 0 ? link.B : link.A) > 0) r.RaftDir = side == 0 ? 1 : -1;
            return;
        }
        r.RaftPos += r.RaftDir * FerrySpeed * TownRules.Dt / link.Length;
        if (r.RaftPos <= 0 || r.RaftPos >= 1)
        {
            r.RaftPos = r.RaftPos <= 0 ? 0 : 1;
            r.RaftDir = 0; r.RaftDock = TownRulesRiver.FerryDock; r.RaftTrips++;
            int end = r.RaftPos < 0.5 ? link.A : link.B;
            for (int i = 0; i < r.AboardN; i++)
            {
                var v = S.Villagers[r.Aboard[i]];
                v.RPoint = end; v.RLink = -1; v.Phase = VillagerPhase.Walking;
                r.Crossed[(int)RiverLinkKind.Ferry]++;
            }
            r.AboardN = 0;
            Emit(new TownEvent { Kind = TownEventKind.FerryDocked, Index = end == link.A ? 0 : 1, X = RiverPlan.Px[end], Y = RiverPlan.Py[end] });
        }
    }

    private int WaitingAt(int link, int point)
    {
        int c = 0;
        foreach (var v in S.Villagers) if (v.RMode != 0 && v.RWait == link && v.RPoint == point) c++;
        return c;
    }

    // ---- The drawbridge ----

    private bool BoatNearDraw()
    {
        double dy = RiverPlan!.DrawRow + 0.5;
        foreach (var b in R!.Boats)
            if ((b.Phase == BoatPhase.Arriving && b.Y > dy - 1 && b.Y < dy + 6) || (b.Phase == BoatPhase.Leaving && b.Y < dy + 1 && b.Y > dy - 6)) return true;
        return false;
    }

    private void StepDrawbridge()
    {
        var r = R!;
        if (!CrossingExists(RiverLinkKind.Drawbridge)) return;
        bool want = BoatNearDraw();
        double was = r.Lift;
        if (want && r.OnDraw == 0) r.Lift = Math.Min(1, r.Lift + TownRules.Dt / LiftSeconds);
        else if (!want) r.Lift = Math.Max(0, r.Lift - TownRules.Dt / LiftSeconds);
        if (was == 0 && r.Lift > 0) { r.Lifts++; Emit(new TownEvent { Kind = TownEventKind.DrawbridgeRaised, X = RiverPlan!.CourseX[RiverPlan.DrawRow], Y = RiverPlan.DrawRow + 0.5 }); }
        if (was > 0 && r.Lift == 0) Emit(new TownEvent { Kind = TownEventKind.DrawbridgeLowered, X = RiverPlan!.CourseX[RiverPlan.DrawRow], Y = RiverPlan.DrawRow + 0.5 });
    }

    /// <summary>Drawbridge lift angle in radians (0 down, TownRulesRiver.LiftAngle up).</summary>
    public double DrawbridgeAngle => (R?.Lift ?? 0) * 1.2;
}
