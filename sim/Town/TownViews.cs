using System;
using System.Collections.Generic;

namespace TideToll.Sim.Town;

public struct VillagerView
{
    public int Id;
    public int Home;                 // cottage plot id
    public VillagerPhase Phase;
    public JobKind Job;
    public Good Carry;
    public int Amount;
    public double X, Y;              // map cells now (cell centres at +0.5)
    public double PrevX, PrevY;      // last tick, for interpolation
    public double Heading;           // radians, atan2(dy, dx) in map cells (y grows south)
    public int Road;                 // road walked now, -1 when not walking
    public double Dist;              // distance along that road from its node A, cells
    public int Workplace;            // workplace aimed at, queued at or working at (-1 none)
    public int QueuePosition;        // 0 = next in line, -1 not queued
    // n7 people (output only)
    public int Dir;                  // +1 walking from the road's node A to B, -1 from B to A (when walking)
    public int Stage;                // 0 to the first workplace, 1 to the second (flour), 2 to the delivery
    public int Slot;                 // work spot taken at the workplace (-1 none)
    public double WorkLeft;          // seconds of work left (when working)
    public Leaning Leaning;          // the kind of work it leans toward (Town.VillagerName gives the name)
    public int Trips;                // deliveries made
    public double Earned;            // coin those deliveries paid
    public int RiverHome;            // river cottage plot (RiverPlan.Plots) when it lives by the river, else -1
    public bool OnRiver;             // walking or standing on the river plan (the second age)
}

public struct RoadView
{
    public int Id;
    public bool Open;
    public double Wear;              // 0 faint trail .. 1 worn in
    public bool WornIn;
    public Surface Surface;
    public int Walkers;
    public double Comfort;           // walkers it carries before they slow down
    public double Crowding;          // Walkers / Comfort (above 1: slowed)
    public double SpeedFactor;       // surface and wear factor times crowding factor
    public double Footsteps;         // cells walked on it in all
}

public struct WorkplaceView
{
    public int Index;
    public int Plot;
    public PlotKind Kind;
    public bool Open;
    public int Places;
    public int Busy;
    public int QueueLength;
}

public sealed partial class Town
{
    public double Coin => S.Coin;
    public double CoinEarned => S.CoinEarned;
    public double Renown => S.Renown;
    public int Level => S.Level;
    public int MaxLevel => S.River != null ? Plan.MaxLevel + TownRulesRiver.LevelRenown.Length : Plan.MaxLevel;
    public bool HasCharter => S.Charter;
    public bool NextAgeRung => S.NextAgeRung;
    public long Trips => S.Trips;
    public long Purchases => S.Purchases;
    public int PaceLevel => S.PaceLevel;
    public int LoadLevel => S.LoadLevel;
    public int RoadsPaved => S.Paved;
    public int BellRings => S.BellRings;
    public double FirstCoinTime => S.FirstCoinTime;
    public double FirstPurchaseTime => S.FirstPurchaseTime;

    /// <summary>Renown needed for the next level (the current level's when at the last one).</summary>
    public double NextLevelRenown => S.River != null ? RiverLevelRenown(Math.Min(S.Level + 1, MaxLevel)) : S.Level < Plan.MaxLevel ? LevelRenownOf(S.Level + 1) : LevelRenownOf(S.Level);

    /// <summary>Fill of the level bar, 0..1 (1 at the last level).</summary>
    public double LevelBar
    {
        get
        {
            if (S.River != null)
            {
                if (S.Level >= MaxLevel) return 1;
                double ra = S.Level == Plan.MaxLevel ? S.River.RenownBase : RiverLevelRenown(S.Level), rb = RiverLevelRenown(S.Level + 1);
                return Math.Clamp((S.Renown - ra) / (rb - ra), 0, 1);
            }
            if (S.Level >= Plan.MaxLevel) return 1;
            double a = LevelRenownOf(S.Level), b = LevelRenownOf(S.Level + 1);
            return Math.Clamp((S.Renown - a) / (b - a), 0, 1);
        }
    }

    /// <summary>"Next: ..." in words, or the next-age line once the charter is in.</summary>
    public string NextGift => S.River != null ? (S.Level < MaxLevel ? "Next: " + RiverPlan!.Levels[S.Level - Plan.MaxLevel].Gift : "The borough seal is yours")
        : S.Level < Plan.MaxLevel ? "Next: " + Plan.Level(S.Level + 1).Gift : S.NextAgeRung ? "The next age is coming" : "Ring in the next age";

    public bool PlotGiven(int plot) => S.PlotGiven[plot];
    /// <summary>Given and, when it is a building offer, bought.</summary>
    public bool PlotWorking(int plot) => PlotWorks(plot);
    public bool OfferAvailable(int offer) => S.OfferAvailable[offer];
    public bool OfferBought(int offer) => S.OfferBought[offer];
    public bool RoadOpen(int road) => S.RoadOpen[road];

    public int VillagerCount => S.Villagers.Length;

    public VillagerView Villager(int i)
    {
        var v = S.Villagers[i];
        return new VillagerView
        {
            Id = v.Id, Home = v.Home, Phase = v.Phase, Job = v.Job, Carry = v.Carry, Amount = v.Amount,
            X = v.X, Y = v.Y, PrevX = v.PrevX, PrevY = v.PrevY, Heading = v.Heading,
            Road = v.Phase == VillagerPhase.Walking ? v.Road : -1, Dist = v.Dist, Workplace = v.Workplace,
            QueuePosition = v.Phase == VillagerPhase.Queued ? QueuePosition(v) : -1,
            Dir = v.Dir, Stage = v.Stage, Slot = v.Slot, WorkLeft = v.WorkLeft, Leaning = LeaningOf(S.Seed, v.Id),
            Trips = v.Trips, Earned = v.Earned, RiverHome = v.RHome, OnRiver = v.RMode != 0,
        };
    }

    public int RoadCount => Plan.Roads.Length;

    public RoadView RoadInfo(int r)
    {
        int w = S.RoadWalkers[r];
        double comfort = Comfort(r);
        return new RoadView
        {
            Id = r, Open = S.RoadOpen[r], Wear = S.RoadWear[r], WornIn = S.RoadWear[r] >= 1, Surface = S.RoadSurface[r],
            Walkers = w, Comfort = comfort, Crowding = w / comfort, SpeedFactor = SurfaceFactor(r) * CrowdFactor(r, w), Footsteps = S.RoadSteps[r],
        };
    }

    /// <summary>The open road with the most walkers on it now (most footsteps on a tie, then lowest id); -1 before any road.</summary>
    public int BusiestRoad
    {
        get
        {
            int best = -1;
            for (int r = 0; r < Plan.Roads.Length; r++)
            {
                if (!S.RoadOpen[r]) continue;
                if (best < 0 || S.RoadWalkers[r] > S.RoadWalkers[best] || (S.RoadWalkers[r] == S.RoadWalkers[best] && S.RoadSteps[r] > S.RoadSteps[best])) best = r;
            }
            return best;
        }
    }

    public int WorkplaceCount => S.Workplaces.Length;

    public WorkplaceView Workplace(int i)
    {
        var w = S.Workplaces[i];
        return new WorkplaceView { Index = i, Plot = w.Plot, Kind = Plan.Plots[w.Plot].Kind, Open = w.Open, Places = w.Places, Busy = w.Busy, QueueLength = w.QueueLen };
    }

    /// <summary>Villager id standing at place <paramref name="pos"/> of a workplace queue.</summary>
    public int QueuedVillager(int workplace, int pos) => S.Workplaces[workplace].Queue[pos];

    /// <summary>Where place <paramref name="pos"/> of a workplace queue stands (map cells) and which way it faces.</summary>
    public void QueueSpot(int workplace, int pos, out double x, out double y, out double heading)
    {
        int v = pos < S.Workplaces[workplace].QueueLen ? S.Workplaces[workplace].Queue[pos] : -1;
        QueuePoint(workplace, v >= 0 ? S.Villagers[v].ArrivedRoad : -1, pos, out x, out y, out heading);
    }
}
