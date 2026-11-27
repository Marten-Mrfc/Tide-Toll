using System;

namespace TideToll.Sim.Town;

public enum Good : byte { None = 0, Water = 1, Grain = 2, Flour = 3, Wool = 4, Cloth = 5 }

public enum JobKind : byte { None = 0, Water = 1, GrainHome = 2, GrainMarket = 3, Flour = 4,
    // The second age (river jobs; the destination is the landing or the market)
    GrainBoat = 5, FlourBoat = 6, WoolBoat = 7, WoolMarket = 8, ClothBoat = 9, ClothMarket = 10 }

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
    // The second age: river mode (1 = walking the river plan, not a first-age road), its itinerary and where on the river.
    public int RMode;
    public int[] RSteps = new int[12];
    public int RLen, RPos;
    public int RPoint = -1, RTarget = -1, RLink = -1, RDir = 1, RWait = -1, RSite = -1;
    public double RDist;
    public int RHome = -1;         // river cottage plot (RiverPlan.Plots) when the villager lives by the river
    public double RStuckSince = -1; // time it began waiting at a crossing (-1 not waiting)
    // n7 people: counters no rule reads (missing in an older save: 0). Name and leaning are not stored: Town.VillagerName, LeaningOf.
    public int Trips;              // deliveries made
    public double Earned;          // coin those deliveries paid
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
    // Rules v3: extra hands bought here, and the queue watch that offers them.
    public int Hands;              // extra places bought ("A second hand at the mill")
    public double QueueSince = -1; // time the queue reached TownRules.HandQueueLength (-1: shorter now)
    public bool HandOffered;       // a hand is on offer here now
}

/// <summary>The whole mutable state of a town (the plan is rebuilt from the seed). Saved to JSON as is.</summary>
public sealed class TownState
{
    public int Version = TownSave.FirstAgeVersion; // set to TownSave.CurrentVersion when the second age is rung in
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
    // Green pass 2: Regulars (missing in an older save: 0)
    public int Regulars, RegularsAtLevel;
    public double RegularsRate;

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

    // ---- Rules v3 (missing in an older v2 save: these defaults) ----
    public bool[] Boons = new bool[8];                   // by Boon
    public int ChoicesOffered, ChoicesMade;              // bit i = choice i (TownRules.ChoiceLevels[i])
    public int[] ChoicePicked = { -1, -1, -1, -1 };      // boon taken at each choice (-1 not yet)
    public bool DriveActive, DriveDone;
    public Good DriveGood;
    public int DriveAmount, DriveDelivered;
    public double DriveRenown, DriveRenownGiven, DriveStart = -1, DriveEnd = -1;

    // ---- Green pass (missing in an older save: these defaults; the touches then start from the coin earned at load) ----
    public int TouchCount;
    public int[] TouchOrder = new int[TownRules.TouchMax]; // spot index (Town.TouchSpots) of each touch, in order
    public double TouchNextCoin;                          // coin earned at which the next touch appears (0: not set yet)
    public double TouchLastCoin, TouchLastTime;           // coin earned and game time at the last touch (or when the beat was set)
    public double CharterRenown;                          // renown the charter needs, fixed at level MaxLevel - 1 (0: the table's)
    public double TouchPrevRate;                          // income a minute over the beat before the last (0: none yet)

    // ---- The second age (save version 3; null before it is rung in) ----
    public RiverState? River;
}
