using System;

namespace TideToll.Sim.Town;

public enum Good : byte { None = 0, Water = 1, Grain = 2, Flour = 3 }

public enum JobKind : byte { None = 0, Water = 1, GrainHome = 2, GrainMarket = 3, Flour = 4 }

/// <summary>What a villager is doing. Walking: on a road. Queued: waiting for a place. Working: in a place.</summary>
public enum VillagerPhase : byte { Idle = 0, Walking = 1, Queued = 2, Working = 3 }

public enum Surface : byte { Track = 0, Gravel = 1, Cobbles = 2 }

/// <summary>One villager's whole state (saved as is).</summary>
public sealed class VillagerState
{
    public int Id;
    public int Home;               // plot id of the cottage
    public VillagerPhase Phase;
    public JobKind Job;
    public int Stage;              // 0 to first workplace, 1 to second workplace (flour), 2 to the delivery
    public int Workplace = -1;     // workplace index currently aimed at, queued at or working at
    public int Node;               // node the villager is at, or the node the current road leg started from
    public int Target = -1;        // node aimed at
    public int Road = -1;          // road walked now
    public int Dir = 1;            // +1 from road A to B, -1 from B to A
    public double Dist;            // distance along the road from A
    public int[] Path = new int[48]; // roads still to walk (in order)
    public int PathLen, PathPos;
    public int ArrivedRoad = -1;   // road the villager came in on (for the queue line)
    public Good Carry;
    public int Amount;
    public double WorkLeft;
    public int Slot = -1;          // rules v2: work spot taken at the workplace (-1 none)
    public double X, Y, PrevX, PrevY, Heading;
}

/// <summary>A place where villagers work: the well, a field, the mill. Places limit how many work at once; the rest queue.</summary>
public sealed class WorkplaceState
{
    public int Plot;
    public int Places;
    public bool Open;
    public int Busy;
    public int[] Queue = new int[64];
    public int QueueLen;
    public int SlotMask;           // rules v2: work spots in use (bit i = spot i)
}

/// <summary>The whole mutable state of a town (the plan is rebuilt from the seed). Saved to JSON as is.</summary>
public sealed class TownState
{
    public int Version = TownSave.CurrentVersion;
    public int Seed;
    public long Tick;
    public double Coin;
    public double CoinEarned;
    public double CoinSpent;
    public double Renown;
    public int Level = 1;
    public bool Charter;
    public bool NextAgeRung;

    public bool[] RoadOpen = Array.Empty<bool>();
    public double[] RoadWear = Array.Empty<double>();
    public Surface[] RoadSurface = Array.Empty<Surface>();
    public double[] RoadSteps = Array.Empty<double>();   // cells walked on each road (for the busiest road)
    public int[] RoadWalkers = Array.Empty<int>();       // walkers on each road now
    public bool[] PlotGiven = Array.Empty<bool>();
    public bool[] OfferAvailable = Array.Empty<bool>();
    public bool[] OfferBought = Array.Empty<bool>();

    public int PaceLevel, LoadLevel, VillagersBought, Paved;
    public bool BellUnlocked, CobblesUnlocked;
    public double BellUntil, BellReadyAt;
    public int BellRings;

    public VillagerState[] Villagers = Array.Empty<VillagerState>();
    public WorkplaceState[] Workplaces = Array.Empty<WorkplaceState>();

    // Coin per minute: earnings per game second in a ring of TownRules.RateWindowSeconds.
    public double[] RateRing = new double[TownRules.RateWindowSeconds];
    public long RateSecond;

    // Counts for achievements and the views.
    public long Trips;
    public long Purchases;
    public long[] Delivered = new long[4];               // by Good
    public double FirstCoinTime = -1, FirstPurchaseTime = -1;
    public bool[] PerkTaken = new bool[64];
    public bool[] Achieved = Array.Empty<bool>();
    public double MaxCoinPerMinute;

    // ---- Rules v2 ----
    public int ToolsLevel, BellTrackLevel;
    public int[] ValueLevel = new int[4];               // by Good
    public int[] Extensions = Array.Empty<int>();        // by plot id (cottages only)
    public int ExtensionsBought;
    public double[] EarnedBy = new double[4];            // coin earned by Good
    public double[] LoadUnlockedAt = { -1, -1, -1 };     // time each Load step unlocked
    // Requests: one at a time, deterministic from the seed and the request number.
    public bool ReqActive;
    public Good ReqGood;
    public int ReqWho, ReqAmount, ReqDelivered;
    public double ReqStart, ReqEnds, ReqRewardCoin;
    public double ReqNextAt = -1;
    public int ReqCount, ReqMet, ReqMissed;
    public double ReqScale = 1;                          // rules v2.1: adapts to the player (met: bigger; missed: smaller)
    public double RequestCoin;                           // coin paid as request rewards (not in CoinEarned)
}
