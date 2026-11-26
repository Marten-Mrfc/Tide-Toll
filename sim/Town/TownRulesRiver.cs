namespace TideToll.Sim.Town;

/// <summary>
/// Every number of the second age, The River (docs/plans/age2-design.md). Kept apart from TownRules so the first age's
/// numbers never move. Static fields so the balancer can try values; nothing writes them while a town runs.
/// </summary>
public static class TownRulesRiver
{
    // ---- Levels ----
    // Renown above the renown held at the ring, in minutes of the town's best coin rate by then (TownState.MaxCoinPerMinute,
    // at least LevelRateMin), so a big town and a small one both take about the same time over the age.
    public static double[] LevelRenown = { 0, 0.6, 1.4, 2.4, 3.8, 5.7, 8.0, 10.9, 14.4, 19.0, 24.7, 31.6 };
    public static double LevelRateMin = 2000;
    public static string[] LevelGift =
    {
        "The landing and its road",
        "Two cottages by the river",
        "The ford and a lane on the far bank",
        "The pasture (Wool)",
        "The weaver (Cloth)",
        "The ferry",
        "A second berth",
        "Two cottages on the far bank",
        "The footbridge",
        "The crane",
        "The drawbridge",
        "The borough seal",
    };

    // ---- The tide ----
    public static double TidePeriod = 240;        // seconds for a whole cycle (high to high)
    public static double TideStart = 0.5;          // phase (0..1) of the cycle when the age is rung in: mid tide, rising... see TidePhase
    public static double HighWater = 0.6;          // boats may arrive above this level
    public static double FordOpenBelow = 0.4;      // the ford can be walked below this level

    // ---- Boats ----
    public static int MaxBoats = 3;                // on the river at once (moored, waiting or moving)
    public static double BoatGap = 15;             // seconds between two arrivals
    public static double BoatSpeed = 3.0;          // cells per second on the river
    // Cargo wanted: the town's coin per minute (at the boat's arrival) times these minutes, in units at the boat's price
    // (rowboat, barge; before the crane), at least BoatMinUnits. So boats take a steady share of the town's work at any size.
    public static double[] BoatMinutes = { 0.6, 1.4 };
    public static int BoatMinUnits = 10;
    public static double[] BoatPatience = { 150, 210 }; // seconds a boat waits once moored (rowboat, barge)
    public static double WaitPatience = 120;       // seconds a boat waits on the river for a berth before it gives up
    public static int BargeEvery = 3;              // every third boat is a barge
    public static double FillBonus = 0.3;          // bonus when filled: this share of the cargo's whole price
    // Boats pay BoatPremium times the market price of a good (with every value factor). Boats never ask for Water.
    public static double BoatPremium = 1.6;
    // Market price of the new goods relative to the first age's: Wool = WoolRel x Grain at the market, Cloth = ClothRel x Flour
    // (each with its own value factors). The landing store pays market price when no boat wants a good.
    public static double WoolRel = 2.0, ClothRel = 1.8;
    public static double ShearTime = 3.0, WeaveTime = 4.0; // seconds of work per load
    public static int PasturePlaces = 3, WeaverPlaces = 2;

    // ---- Crossings ----
    public static double FordSpeed = 0.35;         // walking speed factor on the ford
    // People on a crossing at once (single file on the stones; the bridges are wider).
    public static int FordWidth = 3, FootWidth = 6, DrawWidth = 8;
    public static double DrawOpenShare = 0.85;     // share of the time the drawbridge is down, for its capacity estimate
    public static double BridgeSpeed = 1.0;        // footbridge and drawbridge
    public static int FerryCapacity = 3;           // people per crossing before the raft upgrade
    public static int FerryRaftStep = 1;           // more people per raft step
    public static double FerrySpeed = 1.2;         // cells per second of the raft
    public static double FerryRopeStep = 0.35;     // more speed per rope step
    public static double FerryDock = 2.0;          // seconds the raft waits at a bank to load
    public static double LiftTime = 4.0;           // seconds to lift or lower the drawbridge before the winch
    public static double WinchStep = 0.12;          // lift time shrinks by this share per winch step
    public static double TollBase = 3000;            // coin per boat that passes the drawbridge
    public static double TollStep = 0.25;          // more toll per toll step

    // ---- Purchases ----
    public static double FerryPrice = 2000, BerthPrice = 5000;
    public static double CraneBase = 4000, CraneGrowth = 1.45; public static int CraneMax = 6; public static double CraneStep = 0.25; // +25 % cargo per step
    public static double PricesBase = 1200, PricesGrowth = 1.10; public static int PricesMax = 20; public static double PricesStep = 0.04;
    public static double WoolValueBase = 1500, ClothValueBase = 2500, RiverValueGrowth = 1.10; public static int RiverValueMax = 20; public static double RiverValueStep = 0.04;
    public static double RaftBase = 4000, RaftGrowth = 1.45; public static int RaftMax = 5;
    public static double RopeBase = 3000, RopeGrowth = 1.4; public static int RopeMax = 5;
    public static double TollTrackBase = 2500, TollGrowth = 1.25; public static int TollMax = 8;
    public static double WinchBase = 3000, WinchGrowth = 1.3; public static int WinchMax = 6;

    // ---- Requests from skippers ----
    public static double SkipperRequestEvery = 2; // every second boat that moors carries a named request (a bigger cargo, bonus doubled)
    public static string[] SkipperNames = { "Marit", "Old Tam", "Wenna", "Hob", "Isolde", "Garth" };
}
